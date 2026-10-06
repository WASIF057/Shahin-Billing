using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Pdf;
using QuestPDF.Fluent;
using ShahinBilling.Api.Services;
using Xunit;

namespace ShahinBilling.Tests;

public class RateResolverTests
{
    private static Item Pillow() => new()
    {
        Name = "Pillow", DefaultRate = 100,
        SpecialRates = { new SpecialRate { Rate = 120, ClientIds = { "c1", "c2" } } }
    };

    [Fact] public void Special_client_gets_special_rate() => Assert.Equal((120m, true), RateResolver.Resolve(Pillow(), "c1"));
    [Fact] public void Other_client_gets_default_rate() => Assert.Equal((100m, false), RateResolver.Resolve(Pillow(), "c9"));
    [Fact] public void No_client_gets_default_rate() => Assert.Equal((100m, false), RateResolver.Resolve(Pillow(), null));

    [Fact]
    public void Duplicate_client_across_groups_is_detected()
    {
        var item = Pillow();
        item.SpecialRates.Add(new SpecialRate { Rate = 110, ClientIds = { "c2", "c3" } });
        Assert.Equal(new[] { "c2" }, RateResolver.FindDuplicateClients(item));
    }
}

public class InvoiceCalculatorTests
{
    private static InvoiceLine Line(decimal qty, decimal rate, decimal gst, decimal disc = 0) =>
        new() { Quantity = qty, Rate = rate, GstRate = gst, Discount = disc };

    [Fact]
    public void Intra_state_splits_cgst_and_sgst()
    {
        var r = InvoiceCalculator.Calculate(new[] { Line(10, 100, 18) }, isInterState: false);
        var l = r.Lines[0];
        Assert.Equal(1000m, l.TaxableValue);
        Assert.Equal(90m, l.Cgst);
        Assert.Equal(90m, l.Sgst);
        Assert.Equal(0m, l.Igst);
        Assert.Equal(1180m, r.Totals.GrandTotal);
        Assert.Equal(0m, r.Totals.RoundOff);
    }

    [Fact]
    public void Inter_state_uses_igst()
    {
        var r = InvoiceCalculator.Calculate(new[] { Line(10, 100, 18) }, isInterState: true);
        Assert.Equal(180m, r.Lines[0].Igst);
        Assert.Equal(0m, r.Lines[0].Cgst);
        Assert.Equal(1180m, r.Totals.GrandTotal);
    }

    [Fact]
    public void Discount_reduces_taxable_value()
    {
        var r = InvoiceCalculator.Calculate(new[] { Line(10, 6500, 18, 500) }, false);
        Assert.Equal(64500m, r.Lines[0].TaxableValue);
        Assert.Equal(5805m, r.Lines[0].Cgst);
        Assert.Equal(76110m, r.Totals.GrandTotal);
    }

    [Fact]
    public void Grand_total_keeps_paise_and_has_no_round_off()
    {
        // 3 x 33.33 = 99.99 ; 9% = 8.9991 -> 9.00 each ; total 117.99 (not rounded to 118)
        var r = InvoiceCalculator.Calculate(new[] { Line(3, 33.33m, 18) }, false);
        Assert.Equal(117.99m, r.Totals.GrandTotal);
        Assert.Equal(0m, r.Totals.RoundOff);
    }

    [Fact]
    public void Grand_total_is_exact_even_with_no_tax()
    {
        var r = InvoiceCalculator.Calculate(new[] { Line(1, 100.40m, 0) }, false);
        Assert.Equal(100.40m, r.Totals.GrandTotal);
        Assert.Equal(0m, r.Totals.RoundOff);
    }

    [Fact]
    public void Gst_summary_groups_by_rate()
    {
        var r = InvoiceCalculator.Calculate(new[] { Line(1, 100, 18), Line(2, 100, 18), Line(1, 100, 5) }, false);
        Assert.Equal(2, r.Summary.Count);
        Assert.Equal(300m, r.Summary.Single(s => s.GstRate == 18).TaxableValue);
        Assert.Equal(2.5m, r.Summary.Single(s => s.GstRate == 5).Cgst);
    }

    [Theory]
    [InlineData("27", "27", false)]
    [InlineData("27", "29", true)]
    [InlineData("", "29", false)]
    public void Inter_state_detection(string biz, string pos, bool expected) =>
        Assert.Equal(expected, InvoiceCalculator.IsInterState(biz, pos));

    [Fact]
    public void Payment_status_follows_payments()
    {
        var inv = new Invoice { Totals = new InvoiceTotals { GrandTotal = 1000 } };
        InvoiceCalculator.ApplyPayments(inv);
        Assert.Equal(PaymentStatus.Unpaid, inv.PaymentStatus);
        inv.Payments.Add(new Payment { Amount = 400 });
        InvoiceCalculator.ApplyPayments(inv);
        Assert.Equal(PaymentStatus.PartlyPaid, inv.PaymentStatus);
        Assert.Equal(600m, inv.BalanceDue);
        inv.Payments.Add(new Payment { Amount = 600 });
        InvoiceCalculator.ApplyPayments(inv);
        Assert.Equal(PaymentStatus.Paid, inv.PaymentStatus);
    }
}

public class AmountInWordsTests
{
    [Theory]
    [InlineData(0L, "Rupees Zero Only")]
    [InlineData(1180L, "Rupees One Thousand One Hundred Eighty Only")]
    [InlineData(123450L, "Rupees One Lakh Twenty-Three Thousand Four Hundred Fifty Only")]
    [InlineData(10000000L, "Rupees One Crore Only")]
    [InlineData(25075021L, "Rupees Two Crore Fifty Lakh Seventy-Five Thousand Twenty-One Only")]
    public void Converts_using_indian_system(long amount, string expected) =>
        Assert.Equal(expected, AmountInWords.Convert(amount));

    [Fact]
    public void Includes_paise() =>
        Assert.Equal("Rupees Ten and Fifty Paise Only", AmountInWords.Convert(10.50m));
}

public class GstinValidatorTests
{
    [Theory]
    [InlineData("27AAPFU0939F1ZV", true)]
    [InlineData("29AAGCB7383J1Z4", true)]
    [InlineData("27aapfu0939f1zv", true)]   // lower case accepted
    [InlineData("27AAPFU0939F1ZX", false)]  // wrong checksum
    [InlineData("27AAPFU0939F1Z", false)]   // too short
    [InlineData("00AAPFU0939F1ZV", false)]  // bad state
    [InlineData("", false)]
    public void Validates(string gstin, bool expected) => Assert.Equal(expected, GstinValidator.IsValid(gstin));

    [Fact] public void State_comes_from_first_two_digits() => Assert.Equal("29", StateCodes.FromGstin("29AAGCB7383J1Z4"));
}

public class NumberingAndYearTests
{
    [Theory]
    [InlineData(2026, 3, 31, "2025-26")]
    [InlineData(2026, 4, 1, "2026-27")]
    [InlineData(2026, 12, 15, "2026-27")]
    public void Financial_year_starts_in_april(int y, int m, int d, string expected) =>
        Assert.Equal(expected, FinancialYear.Label(new DateTime(y, m, d)));

    [Fact]
    public void Formats_invoice_number() =>
        Assert.Equal("SE/0007",
            InvoiceNumberService.Format(new InvoiceNumbering { Prefix = "SE", Separator = "/", Padding = 4 }, 7));

    [Fact]
    public void Empty_prefix_is_skipped() =>
        Assert.Equal("012",
            InvoiceNumberService.Format(new InvoiceNumbering { Prefix = "", Separator = "-", Padding = 3 }, 12));

    [Theory]
    [InlineData("1234567.5", "12,34,567.50")]
    [InlineData("999", "999.00")]
    [InlineData("100000", "1,00,000.00")]
    public void Indian_number_format(string v, string expected) =>
        Assert.Equal(expected, Money.FormatIndian(decimal.Parse(v, System.Globalization.CultureInfo.InvariantCulture)));
}

public class EmailTemplateTests
{
    private static readonly Dictionary<string, string> Values = new() { ["ClientName"] = "Sathyanatha", ["InvoiceNumber"] = "SE/0001" };

    [Fact]
    public void Fills_known_placeholders() =>
        Assert.Equal("Dear Sathyanatha, invoice SE/0001",
            EmailTemplateRenderer.Render("Dear {{ClientName}}, invoice {{ InvoiceNumber }}", Values));

    [Fact]
    public void Unknown_placeholders_become_empty() =>
        Assert.Equal("Hello ", EmailTemplateRenderer.Render("Hello {{Nope}}", Values));

    [Fact]
    public void Html_version_escapes_text_and_keeps_line_breaks()
    {
        var html = EmailTemplateRenderer.ToHtml("a <b>\nc");
        Assert.Contains("a &lt;b&gt;<br>c", html);
    }

    [Theory]
    [InlineData("owner@example.com", true)]
    [InlineData("not an email", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Validates_email_addresses(string? s, bool ok) => Assert.Equal(ok, EmailService.IsValidEmail(s));
}

public class PaymentEmailTests
{
    [Fact]
    public void Payment_values_describe_status_change_and_money()
    {
        var inv = new Invoice
        {
            InvoiceNumber = "SE/0007", PaymentStatus = PaymentStatus.PartlyPaid, AmountPaid = 5000, BalanceDue = 7345,
            Totals = new InvoiceTotals { GrandTotal = 12345 },
        };
        var v = EmailService.PaymentValues(inv, PaymentStatus.Unpaid, new Payment { Amount = 5000, Mode = "UPI", Date = new DateTime(2026, 10, 3) });
        Assert.Equal("Part paid", v["PaymentStatus"]);
        Assert.Equal("Unpaid", v["PreviousStatus"]);
        Assert.Equal("Rs. 5,000.00", v["AmountPaid"]);
        Assert.Equal("Rs. 7,345.00", v["BalanceDue"]);
        Assert.Equal("03-10-2026", v["PaymentDate"]);
    }

    [Fact]
    public void Default_payment_wording_uses_only_known_placeholders()
    {
        var s = new EmailSettings();
        var v = EmailService.PaymentValues(new Invoice(), PaymentStatus.Unpaid, null);
        foreach (var text in new[] { s.PaymentClientSubject, s.PaymentClientBody, s.PaymentBusinessSubject, s.PaymentBusinessBody })
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\{\{(\w+)\}\}"))
                Assert.True(v.ContainsKey(m.Groups[1].Value), $"Unknown placeholder {m.Value}");
    }
}

public class EmailTemplateMigrationTests
{
    [Fact]
    public void Old_wording_becomes_four_templates_and_keeps_edits()
    {
        var s = new EmailSettings { ClientSubject = "My own subject", SendOnDownload = false, SendToBusiness = false };
        var list = EmailTemplateService.FromLegacy(s);

        Assert.Equal(4, list.Count);
        var client = list.Single(t => t.Name == "Bill email to client");
        Assert.Equal("My own subject", client.Subject);
        Assert.Equal(new[] { EmailTemplate.OnGenerated }, client.Triggers);   // download switch was off
        Assert.True(client.IsActive);
        Assert.False(list.Single(t => t.Name == "Bill email to me").IsActive);  // "send to business" was off
        Assert.All(list.Where(t => t.Name.StartsWith("Payment")), t => Assert.Equal(new[] { EmailTemplate.OnPaymentChanged }, t.Triggers));
    }

    [Fact]
    public void Client_and_self_templates_have_separate_recipients_and_wording()
    {
        var list = EmailTemplateService.FromLegacy(new EmailSettings());
        Assert.Equal(EmailTemplate.ToClient, list.Single(t => t.Name == "Bill email to client").Recipient);
        Assert.Equal(EmailTemplate.ToBusiness, list.Single(t => t.Name == "Bill email to me").Recipient);
        Assert.NotEqual(list.Single(t => t.Name == "Bill email to client").Body, list.Single(t => t.Name == "Bill email to me").Body);
    }

    [Fact]
    public void Everything_off_makes_every_template_inactive()
    {
        var list = EmailTemplateService.FromLegacy(new EmailSettings { Enabled = false });
        Assert.All(list, t => Assert.False(t.IsActive));
    }
}

public class NonGstBillTests
{
    [Fact]
    public void Non_gst_bills_use_their_own_prefix()
    {
        var n = new InvoiceNumbering { Prefix = "SE", NonGstPrefix = "NG", Separator = "-", Padding = 4 };
        Assert.Equal("SE-0007", InvoiceNumberService.Format(n, 7));
        Assert.Equal("NG-0007", InvoiceNumberService.Format(n, 7, nonGst: true));
    }

    [Fact]
    public void Zero_rate_lines_give_a_bill_with_no_tax_and_the_plain_total()
    {
        var lines = new List<InvoiceLine> { new() { Name = "Pillow", Quantity = 3, Rate = 90, GstRate = 0 } };
        var r = InvoiceCalculator.Calculate(lines, isInterState: false);
        Assert.Equal(0m, r.Totals.CgstTotal + r.Totals.SgstTotal + r.Totals.IgstTotal);
        Assert.Equal(270m, r.Totals.GrandTotal);
    }
}

public class DashboardPeriodTests
{
    private static readonly DateTime Today = new(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void No_dates_means_this_month_against_last_month()
    {
        var p = DashboardService.ComputePeriod(Today, null, null);
        Assert.False(p.Custom);
        Assert.Equal(new DateTime(2026, 10, 1), p.Start);
        Assert.Equal(new DateTime(2026, 11, 1), p.EndExclusive);
        Assert.Equal(new DateTime(2026, 9, 1), p.PrevStart);
    }

    [Fact]
    public void A_range_is_compared_with_the_range_of_the_same_length_before_it()
    {
        // 10 days: 11 Sep to 20 Sep inclusive -> previous = 1 Sep to 10 Sep
        var p = DashboardService.ComputePeriod(Today, new DateTime(2026, 9, 11), new DateTime(2026, 9, 20));
        Assert.True(p.Custom);
        Assert.Equal(new DateTime(2026, 9, 11), p.Start);
        Assert.Equal(new DateTime(2026, 9, 21), p.EndExclusive);
        Assert.Equal(new DateTime(2026, 9, 1), p.PrevStart);
        Assert.Equal(new DateTime(2026, 9, 11), p.PrevEndExclusive);
    }

    [Fact]
    public void Only_a_from_date_runs_up_to_today()
    {
        var p = DashboardService.ComputePeriod(Today, new DateTime(2026, 4, 1), null);
        Assert.Equal(new DateTime(2026, 10, 4), p.EndExclusive);
    }
}

public class OtpTests
{
    [Theory]
    [InlineData("wajid@gmail.com", "wa***@gmail.com")]
    [InlineData("a@x.com", "a***@x.com")]
    [InlineData("nonsense", "your email")]
    public void Masks_the_email_so_the_inbox_is_recognisable_but_not_exposed(string email, string expected) =>
        Assert.Equal(expected, OtpService.Mask(email));

    [Fact]
    public void Codes_are_always_six_digits()
    {
        for (var i = 0; i < 200; i++)
        {
            var c = OtpService.NewCode();
            Assert.Equal(6, c.Length);
            Assert.All(c, ch => Assert.True(char.IsDigit(ch)));
        }
    }
}

public class ResetPasswordValidationTests
{
    private static readonly ShahinBilling.Api.Validators.ResetPasswordValidator V = new();

    [Fact]
    public void Accepts_a_good_request() =>
        Assert.True(V.Validate(new ShahinBilling.Api.Dtos.ResetPasswordRequest { Email = "a@b.com", Code = "123456", NewPassword = "longenough" }).IsValid);

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    public void Rejects_a_code_that_is_not_six_digits(string code) =>
        Assert.False(V.Validate(new ShahinBilling.Api.Dtos.ResetPasswordRequest { Email = "a@b.com", Code = code, NewPassword = "longenough" }).IsValid);

    [Fact]
    public void Rejects_a_short_password() =>
        Assert.False(V.Validate(new ShahinBilling.Api.Dtos.ResetPasswordRequest { Email = "a@b.com", Code = "123456", NewPassword = "short" }).IsValid);
}

public class PdfRenderTests
{
    public PdfRenderTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static Business Biz() => new()
    {
        Name = "Shahin Enterprises", Phone = "98458 45109", Address = new Address { City = "Karkala", State = "Karnataka" },
        Bank = new BankDetails { AccountName = "Shahin", AccountNumber = "123456", Ifsc = "UBIN0901482", BankName = "Union Bank" },
    };

    private static Invoice Bill(string no, decimal total, decimal paid) => new()
    {
        InvoiceNumber = no, InvoiceDate = new DateTime(2026, 10, 3), Status = InvoiceStatus.Final,
        BillTo = new PartySnapshot { Name = "Sathyanatha Cloth Store", Address = new Address { City = "Koppa" } },
        BusinessSnapshot = BusinessSnapshot.From(Biz()),
        Totals = new InvoiceTotals { GrandTotal = total }, AmountPaid = paid, BalanceDue = total - paid,
    };

    [Fact]
    public void Statement_renders_with_bills_and_when_empty()
    {
        var client = new Client { Name = "Sathyanatha Cloth Store", Phone = "9845000000" };
        var some = new StatementDocument(Biz(), client, "Koppa", new DateTime(2026, 4, 1), new DateTime(2026, 10, 3),
            new List<Invoice> { Bill("No-0001", 1000, 400), Bill("No-0002", 2500.50m, 0) }).GeneratePdf();
        var none = new StatementDocument(Biz(), client, "", new DateTime(2026, 4, 1), new DateTime(2026, 10, 3), new List<Invoice>()).GeneratePdf();
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(some, 0, 4));
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(none, 0, 4));
    }

    [Fact]
    public void Receipt_renders()
    {
        var inv = Bill("No-0001", 1000, 400);
        var pdf = new ReceiptDocument(inv, new Payment { Amount = 400, Mode = "UPI", Reference = "UTR123", Date = new DateTime(2026, 10, 3) },
            1, 600, "#1F4E79").GeneratePdf();
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }
}

public class ReminderRuleTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 6, 0, 0, DateTimeKind.Utc);   // 11:30 am in India
    private static readonly ReminderSettings On = new() { Enabled = true, AfterDays = 15, RepeatEveryDays = 7, MaxReminders = 3 };

    private static Invoice Bill(int daysOld, decimal due = 100, DateTime? last = null, int count = 0) => new()
    {
        Status = InvoiceStatus.Final, InvoiceDate = Now.AddHours(5.5).Date.AddDays(-daysOld), BalanceDue = due, LastReminderAt = last, ReminderCount = count,
    };

    [Fact] public void Not_due_before_the_first_reminder_day() => Assert.False(ReminderHostedService.IsDue(Bill(14), On, Now));
    [Fact] public void Due_once_the_bill_is_old_enough() => Assert.True(ReminderHostedService.IsDue(Bill(15), On, Now));
    [Fact] public void Nothing_is_sent_when_it_is_switched_off() => Assert.False(ReminderHostedService.IsDue(Bill(40), new ReminderSettings { Enabled = false }, Now));
    [Fact] public void Paid_bills_are_never_reminded() => Assert.False(ReminderHostedService.IsDue(Bill(40, due: 0), On, Now));
    [Fact] public void Waits_for_the_repeat_period() => Assert.False(ReminderHostedService.IsDue(Bill(40, last: Now.AddDays(-6), count: 1), On, Now));
    [Fact] public void Repeats_after_the_repeat_period() => Assert.True(ReminderHostedService.IsDue(Bill(40, last: Now.AddDays(-7), count: 1), On, Now));
    [Fact] public void Stops_after_the_maximum() => Assert.False(ReminderHostedService.IsDue(Bill(60, last: Now.AddDays(-30), count: 3), On, Now));

    [Theory]
    [InlineData(2, 0, false)]     // 7:30 am in India
    [InlineData(3, 29, false)]    // 8:59 am
    [InlineData(3, 30, true)]     // 9:00 am
    [InlineData(13, 0, true)]     // 6:30 pm
    [InlineData(13, 30, false)]   // 7:00 pm
    public void Reminders_go_out_in_the_daytime_only(int utcHour, int minute, bool expected) =>
        Assert.Equal(expected, ReminderHostedService.IsSendingHour(new DateTime(2026, 10, 20, utcHour, minute, 0, DateTimeKind.Utc)));

    [Fact]
    public void Standard_reminder_template_only_uses_known_placeholders()
    {
        var t = EmailTemplateService.DefaultReminder();
        var known = EmailService.PaymentValues(new Invoice(), PaymentStatus.Unpaid, null);
        foreach (var text in new[] { t.Subject, t.Body })
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\{\{(\w+)\}\}"))
                Assert.True(known.ContainsKey(m.Groups[1].Value), $"Unknown placeholder {m.Value}");
        Assert.Contains(EmailTemplate.OnReminder, t.Triggers);
    }
}

public class ImportRuleTests
{
    [Theory]
    [InlineData("Karnataka", "", "29")]
    [InlineData("karnataka", "", "29")]
    [InlineData("29", "", "29")]
    [InlineData("7", "", "07")]
    [InlineData("", "29ABLPP6364N1ZP", "29")]
    public void Finds_the_state_from_a_name_a_code_or_the_gstin(string text, string gstin, string expected) =>
        Assert.Equal(expected, ImportService.ResolveState(text, gstin));

    [Theory]
    [InlineData("Nowhere", "")]
    [InlineData("", "")]
    public void An_unknown_or_missing_state_is_a_clear_error(string text, string gstin) =>
        Assert.Throws<ShahinBilling.Api.Infrastructure.AppException>(() => ImportService.ResolveState(text, gstin));

    [Theory]
    [InlineData("1,250.50", 1250.50)]
    [InlineData("₹400", 400)]
    [InlineData("18%", 18)]
    [InlineData("Rs. 90", 90)]
    public void Reads_numbers_the_way_people_type_them(string text, double expected) =>
        Assert.Equal((decimal)expected, ImportService.ParseNumber(text, "rate", required: true));

    [Fact]
    public void A_missing_required_number_is_an_error_but_an_optional_one_is_fine()
    {
        Assert.Throws<ShahinBilling.Api.Infrastructure.AppException>(() => ImportService.ParseNumber("", "rate", required: true));
        Assert.Null(ImportService.ParseNumber("", "GST %", required: false));
        Assert.Throws<ShahinBilling.Api.Infrastructure.AppException>(() => ImportService.ParseNumber("abc", "rate", required: true));
    }

    [Fact]
    public void A_choice_must_exist_on_the_type_but_capitals_do_not_matter_and_the_stored_spelling_is_used()
    {
        var sizes = new List<string> { "6ft", "3ft" };
        Assert.Equal("6ft", ImportService.Pick("6FT", sizes, "size", "Bed", "no sizes"));
        Assert.Equal("", ImportService.Pick("", sizes, "size", "Bed", "no sizes"));
        Assert.Throws<ShahinBilling.Api.Infrastructure.AppException>(() => ImportService.Pick("9ft", sizes, "size", "Bed", "no sizes"));
        Assert.Throws<ShahinBilling.Api.Infrastructure.AppException>(() => ImportService.Pick("6ft", null, "size", "Pillow", "Pillow has no sizes."));
    }
}

public class ImportTemplateTests
{
    [Fact]
    public async Task Client_template_is_a_real_workbook_with_the_expected_columns()
    {
        var svc = new ImportService(null!, null!, null!, null!, null!);   // the clients template does not touch the database
        var bytes = await svc.TemplateAsync("any", "clients");
        using var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet("Clients");
        Assert.Equal("Name*", ws.Cell(1, 1).GetString());
        Assert.Equal("Cities", ws.Cell(1, 7).GetString());
        Assert.NotNull(wb.Worksheet("How to fill this"));
    }
}
