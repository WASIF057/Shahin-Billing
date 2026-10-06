using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Pdf;

/// <summary>The GST tax invoice layout. "Classic" = bordered table, "Modern" = light lines, coloured header row.</summary>
public partial class InvoiceDocument(Invoice inv, TemplateSettings t, string copy) : IDocument
{
    private readonly bool _modern = t.Layout.Equals("Modern", StringComparison.OrdinalIgnoreCase);
    private readonly string _primary = HexColor().IsMatch(t.PrimaryColor ?? "") ? t.PrimaryColor! : "#1F4E79";
    private readonly float _fs = t.FontSize switch { "Small" => 8, "Large" => 10, _ => 9 };
    private BusinessSnapshot B => inv.BusinessSnapshot;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();

    public DocumentMetadata GetMetadata() => new() { Title = $"Invoice {inv.InvoiceNumber}", Author = B.Name };
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    private string CopyLabel => copy switch
    {
        "Duplicate" => "Duplicate for Transporter",
        "Triplicate" => "Triplicate for Supplier",
        _ => "Original for Recipient"
    };

    private static string Rs(decimal v) => Money.FormatIndian(v);

    private static byte[]? FromDataUrl(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;
        try
        {
            var comma = dataUrl.IndexOf(',');
            return Convert.FromBase64String(comma >= 0 ? dataUrl[(comma + 1)..] : dataUrl);
        }
        catch { return null; }
    }

    private static string Join(params string?[] parts) =>
        string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string AddressText(Address a) =>
        Join(a.Line1, a.Line2, a.City, string.IsNullOrEmpty(a.State) ? null : a.State, a.Pincode);

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(_fs).FontColor(Colors.Grey.Darken4));

            page.Header().Element(ComposeHeader);
            page.Content().PaddingTop(8).Element(ComposeContent);
            page.Footer().Element(ComposeFooter);

            if (inv.Status == InvoiceStatus.Cancelled)
                page.Foreground().AlignCenter().AlignMiddle().Rotate(-30)
                    .Text("CANCELLED").FontSize(80).Bold().FontColor(Colors.Red.Lighten3);
            else if (inv.Status == InvoiceStatus.Draft)
                page.Foreground().AlignCenter().AlignMiddle().Rotate(-30)
                    .Text("DRAFT").FontSize(80).Bold().FontColor(Colors.Grey.Lighten2);
        });
    }

    // ---------------- Header ----------------
    private void ComposeHeader(IContainer c)
    {
        var logo = t.ShowLogo ? FromDataUrl(B.Logo) : null;
        c.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(inv.IsNonGst ? (string.IsNullOrWhiteSpace(t.NonGstTitle) ? "BILL" : t.NonGstTitle) : t.InvoiceTitle).FontSize(_fs + 6).Bold().FontColor(_primary);
                row.ConstantItem(210).AlignRight().AlignMiddle()
                    .Border(1.2f).BorderColor(_primary).PaddingVertical(4).PaddingHorizontal(8)
                    .AlignCenter().Text(CopyLabel).Bold().FontSize(_fs + 2).FontColor(_primary);
            });

            col.Item().PaddingTop(6).Row(row =>
            {
                if (logo != null) row.ConstantItem(64).Height(58).Image(logo).FitArea();
                row.RelativeItem().PaddingLeft(logo != null ? 10 : 0).Column(b =>
                {
                    b.Item().Text(B.Name).FontSize(_fs + 5).Bold();
                    var addr = AddressText(B.Address);
                    if (addr.Length > 0) b.Item().Text(addr);
                    var contact = Join(
                        string.IsNullOrEmpty(B.Phone) ? null : $"Ph: {B.Phone}",
                        string.IsNullOrEmpty(B.AlternatePhone) ? null : B.AlternatePhone,
                        B.Email, B.Website);
                    if (contact.Length > 0) b.Item().Text(contact);
                    if (!inv.IsNonGst && !string.IsNullOrEmpty(B.Gstin))
                        b.Item().Text(txt =>
                        {
                            txt.Span("GSTIN: ").SemiBold();
                            txt.Span(B.Gstin);
                            if (!string.IsNullOrEmpty(B.Pan)) { txt.Span("   PAN: ").SemiBold(); txt.Span(B.Pan); }
                        });
                });

                row.ConstantItem(190).Column(m =>
                {
                    MetaLine(m, "Invoice No.", inv.InvoiceNumber, bold: true);
                    MetaLine(m, "Invoice Date", inv.InvoiceDate.ToString("dd-MM-yyyy"));
                    // Time of sale = when the bill was first saved, shown in Indian time
                    MetaLine(m, "Time", inv.CreatedAt.AddHours(5.5).ToString("hh:mm tt"));
                    // GST tax type still follows the state; the bill displays the city the goods go to (same as Bill To when Ship To is the same)
                    var cityText = !string.IsNullOrWhiteSpace(inv.ShipTo.Address.City) ? inv.ShipTo.Address.City
                        : !string.IsNullOrWhiteSpace(inv.BillTo.Address.City) ? inv.BillTo.Address.City : inv.PlaceOfSupplyState;
                    if (!inv.IsNonGst) MetaLine(m, "Place of Supply", cityText);
                    if (!string.IsNullOrWhiteSpace(B.Address.City)) MetaLine(m, "Dispatched From", B.Address.City);
                    if (t.ShowPoDetails && inv.PoDate.HasValue)
                        MetaLine(m, "PO Date", inv.PoDate.Value.ToString("dd-MM-yyyy"));
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(1.2f).LineColor(_primary);
        });
    }

    private void MetaLine(ColumnDescriptor m, string label, string value, bool bold = false)
    {
        m.Item().Row(r =>
        {
            r.ConstantItem(80).Text(label).FontColor(Colors.Grey.Darken1);
            var text = r.RelativeItem().AlignRight().Text(value);
            if (bold) text.Bold();
        });
    }

    // ---------------- Content ----------------
    private void ComposeContent(IContainer c)
    {
        c.Column(col =>
        {
            col.Spacing(8);

            col.Item().Row(row =>
            {
                row.RelativeItem().Element(x => PartyBox(x, "Bill To", inv.BillTo));
                row.ConstantItem(10);
                row.RelativeItem().Element(x => PartyBox(x, "Ship To", inv.ShipTo));
            });

            var tr = inv.Transport;
            if (t.ShowTransportDetails && tr != null &&
                new[] { tr.TransporterName, tr.VehicleNumber, tr.EwayBillNumber, tr.LrNumber }.Any(s => !string.IsNullOrWhiteSpace(s)))
            {
                col.Item().Text(txt =>
                {
                    void Part(string label, string? v)
                    {
                        if (string.IsNullOrWhiteSpace(v)) return;
                        txt.Span($"{label}: ").SemiBold();
                        txt.Span(v + "     ");
                    }
                    Part("Transporter", tr.TransporterName);
                    Part("Vehicle No.", tr.VehicleNumber);
                    Part("E-way Bill No.", tr.EwayBillNumber);
                    Part("LR No.", tr.LrNumber);
                    if (tr.DeliveryDate.HasValue) Part("Delivery", tr.DeliveryDate.Value.ToString("dd-MM-yyyy"));
                });
            }

            col.Item().Element(ItemsTable);

            col.Item().Row(row =>
            {
                row.RelativeItem(3).Column(left =>
                {
                    left.Spacing(6);
                    if (!inv.IsNonGst && t.ShowGstSummary && inv.GstSummary.Count > 0) left.Item().Element(GstSummaryTable);
                    left.Item().Text(txt =>
                    {
                        txt.Span("Amount in words: ").SemiBold();
                        txt.Span(inv.AmountInWords);
                    });
                });
                row.ConstantItem(12);
                row.RelativeItem(2).Element(TotalsBox);
            });

            // Bank details get their own full-width box under the totals
            if (t.ShowBankDetails && !string.IsNullOrEmpty(B.Bank.AccountNumber)) col.Item().Element(BankBox);

            if (!string.IsNullOrWhiteSpace(inv.Notes))
                col.Item().Text(txt => { txt.Span("Notes: ").SemiBold(); txt.Span(inv.Notes); });

            // One bordered box: terms and declaration on the left, the signature block on the right
            col.Item().ShowEntire().Border(0.8f).BorderColor(Colors.Grey.Medium).Padding(8).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    if (t.ShowTerms && !string.IsNullOrWhiteSpace(B.TermsAndConditions))
                    {
                        left.Item().Text("Terms & Conditions").SemiBold();
                        foreach (var line in B.TermsAndConditions.Split('\n'))
                            left.Item().Text(line.TrimEnd()).FontSize(_fs - 1);
                    }
                    if (t.ShowDeclaration && !string.IsNullOrWhiteSpace(B.DeclarationText))
                    {
                        left.Item().PaddingTop(4).Text("Declaration").SemiBold();
                        left.Item().Text(B.DeclarationText).FontSize(_fs - 1);
                    }
                });
                row.ConstantItem(17).AlignCenter().LineVertical(0.6f).LineColor(Colors.Grey.Lighten1);
                row.ConstantItem(180).Column(sig =>
                {
                    sig.Item().AlignRight().Text($"For {B.Name}").SemiBold();
                    var img = t.ShowSignature ? FromDataUrl(B.Signature) : null;
                    if (img != null) sig.Item().Height(48).AlignRight().Image(img).FitArea();
                    else sig.Item().Height(48);
                    sig.Item().AlignRight().Text(string.IsNullOrEmpty(B.AuthorisedSignatoryName)
                        ? "Authorised Signatory" : $"{B.AuthorisedSignatoryName}\nAuthorised Signatory");
                });
            });
        });
    }

    private void PartyBox(IContainer c, string title, PartySnapshot p)
    {
        c.Border(0.6f).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(col =>
        {
            col.Item().Text(title).FontSize(_fs - 1).FontColor(_primary).SemiBold();
            col.Item().Text(p.Name).Bold();
            if (!string.IsNullOrWhiteSpace(p.Address.City)) col.Item().Text(p.Address.City);
            if (!inv.IsNonGst && !string.IsNullOrEmpty(p.Gstin)) col.Item().Text(txt => { txt.Span("GSTIN: ").SemiBold(); txt.Span(p.Gstin); });
            if (!inv.IsNonGst && !string.IsNullOrEmpty(p.Address.StateCode))
                col.Item().Text($"State: {p.Address.State} ({p.Address.StateCode})");
            if (!string.IsNullOrWhiteSpace(p.Phone)) col.Item().Text(txt => { txt.Span("Mobile: ").SemiBold(); txt.Span(p.Phone); });
        });
    }

    private IContainer HeadCell(IContainer c) => _modern
        ? c.Background(_primary).PaddingVertical(4).PaddingHorizontal(3)
        : c.Border(0.5f).BorderColor(Colors.Grey.Medium).Background(Colors.Grey.Lighten3).Padding(3);

    private IContainer BodyCell(IContainer c) => _modern
        ? c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(3)
        : c.Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(3);

    private void Head(IContainer newCell, string text, bool right = false)
    {
        var cell = newCell.Element(HeadCell);
        if (right) cell = cell.AlignRight();
        var tx = cell.Text(text).SemiBold();
        if (_modern) tx.FontColor(Colors.White);
    }

    private void Body(IContainer newCell, string text, bool right = false, bool bold = false)
    {
        var cell = newCell.Element(BodyCell);
        if (right) cell = cell.AlignRight();
        var tx = cell.Text(text);
        if (bold) tx.SemiBold();
    }

    private void ItemsTable(IContainer c)
    {
        var showDisc = t.ShowDiscountColumn && inv.Lines.Any(l => l.Discount != 0);

        c.Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(22);           // #
                cd.RelativeColumn(3.2f);         // item
                cd.RelativeColumn(0.9f);         // qty
                cd.RelativeColumn(1.1f);         // rate
                if (showDisc) cd.RelativeColumn(1f);
                cd.RelativeColumn(1.5f);         // amount (after any discount)
            });

            table.Header(h =>
            {
                Head(h.Cell(), "#");
                Head(h.Cell(), "Item");
                Head(h.Cell(), "Qty", true);
                Head(h.Cell(), "Rate", true);
                if (showDisc) Head(h.Cell(), "Disc.", true);
                Head(h.Cell(), "Amount", true);
            });

            var i = 1;
            foreach (var l in inv.Lines)
            {
                Body(table.Cell(), (i++).ToString());
                table.Cell().Element(BodyCell).Column(col =>
                {
                    col.Item().Text(l.Name).SemiBold();
                    if (!string.IsNullOrEmpty(l.SizeOrVariant)) col.Item().Text(l.SizeOrVariant).FontSize(_fs - 1).FontColor(Colors.Grey.Darken1);
                });
                Body(table.Cell(), $"{Money.FormatQty(l.Quantity)} {l.Unit}", true);
                Body(table.Cell(), Rs(l.Rate), true);
                if (showDisc) Body(table.Cell(), l.Discount == 0 ? "-" : Rs(l.Discount), true);
                Body(table.Cell(), Rs(l.TaxableValue), true, true);
            }
        });
    }

    private void GstSummaryTable(IContainer c)
    {
        var inter = inv.IsInterState;
        c.Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(); cd.RelativeColumn(1.4f);
                if (inter) cd.RelativeColumn(1.3f); else { cd.RelativeColumn(1.2f); cd.RelativeColumn(1.2f); }
                cd.RelativeColumn(1.3f);
            });
            table.Header(h =>
            {
                Head(h.Cell(), "GST Rate");
                Head(h.Cell(), "Taxable", true);
                if (inter) Head(h.Cell(), "IGST", true); else { Head(h.Cell(), "CGST", true); Head(h.Cell(), "SGST", true); }
                Head(h.Cell(), "Total Tax", true);
            });
            foreach (var s in inv.GstSummary)
            {
                Body(table.Cell(), $"{s.GstRate:0.##}%");
                Body(table.Cell(), Rs(s.TaxableValue), true);
                if (inter) Body(table.Cell(), Rs(s.Igst), true); else { Body(table.Cell(), Rs(s.Cgst), true); Body(table.Cell(), Rs(s.Sgst), true); }
                Body(table.Cell(), Rs(s.Cgst + s.Sgst + s.Igst), true);
            }
        });
    }

    private void BankBox(IContainer c)
    {
        var b = B.Bank;
        c.Border(1.2f).BorderColor(Colors.Black).Padding(8).DefaultTextStyle(x => x.Bold()).Column(col =>
        {
            col.Item().Text("Bank Details for Payment").Bold().FontSize(_fs + 1).FontColor(_primary);
            void L(string label, string v) { if (!string.IsNullOrWhiteSpace(v)) col.Item().Text(txt => { txt.Span($"{label}: ").Bold(); txt.Span(v).Bold(); }); }
            L("Account Name", b.AccountName);
            L("Account No.", b.AccountNumber);
            L("IFSC", b.Ifsc);
            L("Bank", Join(b.BankName, b.Branch));
            if (t.ShowUpi) L("UPI ID", b.UpiId);
        });
    }

    private void TotalsBox(IContainer c)
    {
        var tot = inv.Totals;
        c.Column(col =>
        {
            void Line(string label, string value, bool strong = false)
            {
                col.Item().PaddingVertical(2).Row(r =>
                {
                    var l = r.RelativeItem().Text(label);
                    var v = r.ConstantItem(95).AlignRight().Text(value);
                    if (strong) { l.Bold(); v.Bold(); }
                });
            }
            // Show the rate next to the tax name when the whole bill uses a single GST rate.
            var rates = inv.GstSummary.Select(g => g.GstRate).Distinct().ToList();
            string Tax(string name, bool half) => rates.Count == 1 ? $"{name} @{(half ? rates[0] / 2 : rates[0]):0.##}%" : name;
            Line("Total", Rs(tot.TaxableTotal));
            if (!inv.IsNonGst)
            {
                if (inv.IsInterState) Line(Tax("IGST", false), Rs(tot.IgstTotal));
                else { Line(Tax("CGST", true), Rs(tot.CgstTotal)); Line(Tax("SGST", true), Rs(tot.SgstTotal)); }
            }
            if (tot.RoundOff != 0) Line("Round Off", (tot.RoundOff > 0 ? "+" : "") + Rs(tot.RoundOff));
            col.Item().PaddingTop(3).LineHorizontal(1).LineColor(_primary);
            col.Item().PaddingTop(3).Row(r =>
            {
                r.RelativeItem().Text("Grand Total").FontSize(_fs + 2).Bold().FontColor(_primary);
                r.ConstantItem(110).AlignRight().Text($"Rs. {Money.FormatIndian(tot.GrandTotal)}").FontSize(_fs + 2).Bold().FontColor(_primary);
            });
            if (inv.AmountPaid > 0)
            {
                Line("Paid", Rs(inv.AmountPaid));
                Line("Balance Due", Rs(inv.BalanceDue), true);
            }
        });
    }

    // ---------------- Footer ----------------
    private void ComposeFooter(IContainer c)
    {
        c.PaddingTop(6).Row(r =>
        {
            r.RelativeItem().Text(t.FooterNote ?? "").FontSize(_fs - 1.5f).FontColor(Colors.Grey.Darken1);
            r.ConstantItem(90).AlignRight().Text(x =>
            {
                x.DefaultTextStyle(s => s.FontSize(_fs - 1.5f).FontColor(Colors.Grey.Darken1));
                x.Span("Page ");
                x.CurrentPageNumber();
                x.Span(" of ");
                x.TotalPages();
            });
        });
    }
}
