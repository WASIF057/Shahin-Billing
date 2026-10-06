using ShahinBilling.Api.Auth;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Pdf;
using MongoDB.Driver;

namespace ShahinBilling.Api.Services;

/// <summary>Mail server details. These are secrets, so they live in appsettings.Development.json / user-secrets, not in the database.</summary>
public class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "";
    /// <summary>true = STARTTLS (port 587), false = plain SSL on connect (port 465).</summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>Filled in with real values (the example file's "YOUR-..." placeholders don't count).</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromEmail)
        && !FromEmail.Contains("YOUR-", StringComparison.OrdinalIgnoreCase)
        && !Username.Contains("YOUR-", StringComparison.OrdinalIgnoreCase)
        && !Password.Contains("YOUR-", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Fills {{Placeholders}} in an email template. Unknown placeholders become empty.</summary>
public static partial class EmailTemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*([A-Za-z]+)\s*\}\}")]
    private static partial Regex Placeholder();

    public static string Render(string template, IReadOnlyDictionary<string, string> values) =>
        Placeholder().Replace(template ?? "", m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : "");

    /// <summary>Plain text -> simple HTML (escaped, line breaks kept) so the owner never has to write HTML.</summary>
    public static string ToHtml(string text) =>
        "<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:1.5;color:#222\">"
        + WebUtility.HtmlEncode(text).Replace("\r\n", "\n").Replace("\n", "<br>") + "</div>";
}

/// <summary>Sends mail through SMTP. Singleton so a background send never touches a disposed request scope.</summary>
public class EmailSender(IOptions<SmtpSettings> options, ILogger<EmailSender> log)
{
    private readonly SmtpSettings _smtp = options.Value;
    public bool IsConfigured => _smtp.IsConfigured;

    public virtual async Task SendAsync(IEnumerable<string> to, string subject, string body, string? attachmentName, byte[]? attachment)
    {
        if (!_smtp.IsConfigured) throw new AppException("Email isn't set up yet. Put your real email and app password in the Smtp section of appsettings.Development.json, then restart the API.");

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromEmail));
        foreach (var t in to) msg.To.Add(MailboxAddress.Parse(t));
        msg.Subject = subject;

        var builder = new BodyBuilder { TextBody = body, HtmlBody = EmailTemplateRenderer.ToHtml(body) };
        if (attachment != null) builder.Attachments.Add(attachmentName ?? "invoice.pdf", attachment, ContentType.Parse("application/pdf"));
        msg.Body = builder.ToMessageBody();

        try
        {
            using var client = new MailKit.Net.Smtp.SmtpClient();
            await client.ConnectAsync(_smtp.Host, _smtp.Port, _smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect);
            if (!string.IsNullOrWhiteSpace(_smtp.Username)) await client.AuthenticateAsync(_smtp.Username, _smtp.Password.Replace(" ", ""));
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
            log.LogInformation("Email sent to {To}: {Subject}", string.Join(", ", to), subject);
        }
        catch (MailKit.Security.AuthenticationException ex)
        {
            log.LogWarning(ex, "SMTP login failed");
            throw new AppException("The mail server didn't accept the username or password. For Gmail, use a 16-letter App Password (not your normal password) and make sure 2-Step Verification is on.");
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or MailKit.Net.Smtp.SmtpCommandException or MailKit.ProtocolException
                                       or System.IO.IOException or TimeoutException or System.Security.Authentication.AuthenticationException)
        {
            log.LogWarning(ex, "SMTP send failed");
            throw new AppException($"Couldn't send the email: {ex.Message} (server {_smtp.Host}, port {_smtp.Port}).");
        }
    }
}

/// <summary>Sends the active email templates for each event. A failed email never breaks billing.</summary>
public class EmailService(
    MongoContext db, BusinessService businesses, EmailTemplateService templates, InvoicePdfService pdf, EmailSender sender,
    IConfiguration config, ILogger<EmailService> log)
{
    // "invoiceId:templateId" -> last time sent, so "Save & download PDF" (generate + download) sends one email per template
    private static readonly ConcurrentDictionary<string, DateTime> LastSent = new();
    private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(60);

    // Payment emails wait a few seconds so several quick changes (e.g. "mark unpaid" removing 3 payments) send one email
    private sealed record PendingPayment(CancellationTokenSource Cts, PaymentStatus First);
    private static readonly ConcurrentDictionary<string, PendingPayment> PendingPayments = new();
    private static readonly TimeSpan PaymentDelay = TimeSpan.FromSeconds(6);

    private sealed record Mail(string To, string Subject, string Body, bool Attach, string TemplateId);

    public static bool IsValidEmail(string? s) =>
        !string.IsNullOrWhiteSpace(s) && MailAddress.TryCreate(s.Trim(), out _);

    public static string StatusText(PaymentStatus s) => s switch
    {
        PaymentStatus.Paid => "Paid",
        PaymentStatus.PartlyPaid => "Part paid",
        _ => "Unpaid",
    };

    public static Dictionary<string, string> Values(Invoice inv, string eventText) => new()
    {
        ["InvoiceNumber"] = inv.InvoiceNumber,
        ["InvoiceDate"] = inv.InvoiceDate.ToString("dd-MM-yyyy"),
        ["Time"] = inv.CreatedAt.AddHours(5.5).ToString("hh:mm tt"),
        ["ClientName"] = inv.BillTo.Name,
        ["ClientCity"] = inv.BillTo.Address.City,
        ["BusinessName"] = inv.BusinessSnapshot.Name,
        ["BusinessPhone"] = inv.BusinessSnapshot.Phone,
        ["GrandTotal"] = "Rs. " + Money.FormatIndian(inv.Totals.GrandTotal),
        ["AmountInWords"] = inv.AmountInWords,
        ["Event"] = eventText,
    };

    public static Dictionary<string, string> PaymentValues(Invoice inv, PaymentStatus previous, Payment? last)
    {
        var v = Values(inv, "payment updated");
        v["PaymentStatus"] = StatusText(inv.PaymentStatus);
        v["PreviousStatus"] = StatusText(previous);
        v["AmountPaid"] = "Rs. " + Money.FormatIndian(inv.AmountPaid);
        v["BalanceDue"] = "Rs. " + Money.FormatIndian(inv.BalanceDue);
        v["PaymentAmount"] = last == null ? "-" : "Rs. " + Money.FormatIndian(last.Amount);
        v["PaymentMode"] = last == null ? "-" : last.Mode;
        v["PaymentDate"] = last == null ? "-" : last.Date.ToString("dd-MM-yyyy");
        v["DaysOutstanding"] = Math.Max((int)(DateTime.UtcNow.AddHours(5.5).Date - inv.InvoiceDate.Date).TotalDays, 0).ToString();
        return v;
    }

    /// <summary>One mail per template, addressed to the client or to the business email. Skips recipients with no valid address.</summary>
    private async Task<List<Mail>> BuildMailsAsync(
        string businessId, Business business, string clientId, IReadOnlyDictionary<string, string> values, IEnumerable<EmailTemplate> list,
        string? clientEmail = null)
    {
        Client? client = null;
        var loaded = false;
        User? owner = null;
        var mails = new List<Mail>();
        foreach (var t in list)
        {
            string? to;
            if (t.Recipient == EmailTemplate.ToBusiness)
            {
                to = business.Email;
                if (!IsValidEmail(to))   // no business email yet: use the owner's own login email so nothing is missed
                {
                    owner ??= await db.Users.Find(u => u.BusinessId == businessId && u.Role == AppRoles.Owner).FirstOrDefaultAsync();
                    to = owner?.Email;
                }
            }
            else
            {
                if (!loaded) { client = await db.Clients.Find(c => c.BusinessId == businessId && c.Id == clientId).FirstOrDefaultAsync(); loaded = true; }
                to = clientEmail ?? client?.Email;
            }
            if (!IsValidEmail(to)) continue;
            mails.Add(new Mail(to!.Trim(), EmailTemplateRenderer.Render(t.Subject, values), EmailTemplateRenderer.Render(t.Body, values), t.AttachPdf, t.Id));
        }
        return mails;
    }

    private void SendInBackground(string what, List<Mail> mails, byte[]? pdfBytes, string fileName)
    {
        _ = Task.Run(async () =>
        {
            foreach (var m in mails)
            {
                try { await sender.SendAsync(new[] { m.To }, m.Subject, m.Body, m.Attach && pdfBytes != null ? fileName : null, m.Attach ? pdfBytes : null); }
                catch (Exception ex) { log.LogWarning(ex, "Could not email {What} to {To}", what, m.To); }
            }
        });
    }

    // ---------------------------------------------------------------- orders from the ordering website
    /// <summary>One item per line: "- Bed &#8212; Box &#183; Cotton &#183; 6ft x 2 PCS". Quantities only, never a price.</summary>
    public static string OrderItemsText(IEnumerable<OrderLine> lines) =>
        string.Join("\n", lines.Select(l => $"- {l.Label} x {Money.FormatQty(l.Quantity)} {l.Unit}"));

    public static Dictionary<string, string> OrderValues(Order o, Business b) => new()
    {
        ["OrderNumber"] = o.OrderNumber,
        ["OrderDate"] = o.CreatedAt.AddHours(5.5).ToString("dd-MM-yyyy hh:mm tt"),
        ["OrderItems"] = OrderItemsText(o.Lines),
        ["OrderNote"] = string.IsNullOrWhiteSpace(o.Note) ? "(none)" : o.Note,
        ["OrderPriority"] = o.Priority == OrderPriority.Urgent ? "URGENT" : o.Priority.ToString(),
        ["OrderSource"] = o.Source == OrderSource.Phone ? "Phone call" : "Ordering website",
        ["OrderCancelReason"] = string.IsNullOrWhiteSpace(o.CancelReason) ? "-" : o.CancelReason,
        ["ClientName"] = o.ClientName,
        ["ClientCity"] = o.City,
        ["BusinessName"] = b.Name,
        ["BusinessPhone"] = b.Phone,
    };

    /// <summary>Emails the ordered items to the client (the person who ordered) and to the owner, using the active "order placed" templates.</summary>
    public async Task NotifyOrderAsync(string businessId, Order order, bool toClient = true, bool toBusiness = true)
    {
        try
        {
            if (!sender.IsConfigured) return;
            var list = (await templates.ActiveForAsync(businessId, EmailTemplate.OnOrderPlaced))
                .Where(t => t.Recipient == EmailTemplate.ToBusiness ? toBusiness : toClient).ToList();
            if (list.Count == 0) return;
            var business = await businesses.GetAsync(businessId);
            var mails = await BuildMailsAsync(businessId, business, order.ClientId, OrderValues(order, business), list, order.UserEmail);
            SendInBackground($"order {order.OrderNumber}", mails, null, "");
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Order email step failed for {Number}", order.OrderNumber);
        }
    }

    /// <summary>Tells the client (the person who ordered) that their order was accepted or cancelled by the owner or staff.
    /// trigger is EmailTemplate.OnOrderAccepted or OnOrderCancelled.</summary>
    public async Task NotifyOrderStatusAsync(string businessId, Order order, string trigger)
    {
        try
        {
            if (!sender.IsConfigured) return;
            var list = await templates.ActiveForAsync(businessId, trigger);
            if (list.Count == 0) return;
            var business = await businesses.GetAsync(businessId);
            var mails = await BuildMailsAsync(businessId, business, order.ClientId, OrderValues(order, business), list, order.UserEmail);
            SendInBackground($"order {order.OrderNumber} ({trigger})", mails, null, "");
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Order status email step failed for {Number}", order.OrderNumber);
        }
    }

    /// <summary>The invitation a client gets when the owner gives them an ordering login. They set their own password with "Forgot password?".</summary>
    public async Task SendPortalInviteAsync(string businessId, Client client, User user)
    {
        if (!sender.IsConfigured) throw new AppException("Email isn't connected yet.", 503);
        var business = await businesses.GetAsync(businessId);
        var url = (config["App:PublicUrl"] ?? "").Trim().TrimEnd('/');
        var where = url.Length > 0 ? $"open {url}" : "open the ordering website";
        var body = $"Hello {client.Name},\n\n{business.Name} has set up an online ordering login for you.\n\n" +
                   $"To start: {where}, click \"Forgot password?\", enter this email address ({user.Email}), and choose your own password with the 6-digit code we email you.\n\n" +
                   "After that you can log in and place orders whenever you like. Prices are never shown there: they stay between you and us.\n\n" +
                   $"{business.Name}\n{business.Phone}";
        await sender.SendAsync(new[] { user.Email }, $"{business.Name}: your ordering login is ready", body, null, null);
    }

    /// <summary>trigger: "generated" (bill finalized) or "downloaded" (PDF downloaded). Returns quickly; mail goes out in the background.</summary>
    public async Task NotifyAsync(string businessId, Invoice inv, string trigger)
    {
        try
        {
            if (inv.Status != InvoiceStatus.Final || !sender.IsConfigured) return;
            var key = trigger == "generated" ? EmailTemplate.OnGenerated : EmailTemplate.OnDownloaded;
            var list = await templates.ActiveForAsync(businessId, key);
            if (list.Count == 0) return;

            var now = DateTime.UtcNow;
            list = list.Where(t =>
            {
                var k = inv.Id + ":" + t.Id;
                if (LastSent.TryGetValue(k, out var last) && now - last < Quiet) return false;
                LastSent[k] = now;
                return true;
            }).ToList();
            if (list.Count == 0) return;

            var business = await businesses.GetAsync(businessId);
            var values = PaymentValues(inv, inv.PaymentStatus, inv.Payments.LastOrDefault());
            values["Event"] = trigger;
            var mails = await BuildMailsAsync(businessId, business, inv.ClientId, values, list);
            byte[]? pdfBytes = mails.Any(m => m.Attach) ? pdf.Generate(inv, inv.TemplateOverride ?? business.Template, null) : null;
            SendInBackground($"invoice {inv.InvoiceNumber}", mails, pdfBytes, $"{inv.InvoiceNumber.Replace('/', '-')}.pdf");
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Email step failed for invoice {Number}", inv.InvoiceNumber);
        }
    }

    /// <summary>Call after a payment is added or removed. Sends only if the payment status really changed
    /// (compared with the status before the first of a quick burst of changes).</summary>
    public async Task NotifyPaymentAsync(string businessId, Invoice inv, PaymentStatus previous)
    {
        try
        {
            if (inv.Status != InvoiceStatus.Final || !sender.IsConfigured) return;
            var list = await templates.ActiveForAsync(businessId, EmailTemplate.OnPaymentChanged);
            if (list.Count == 0) return;

            var first = PendingPayments.TryGetValue(inv.Id, out var pending) ? pending.First : previous;
            pending?.Cts.Cancel();
            if (first == inv.PaymentStatus) { PendingPayments.TryRemove(inv.Id, out _); return; }   // changed back: nothing to say

            var business = await businesses.GetAsync(businessId);
            var values = PaymentValues(inv, first, inv.Payments.LastOrDefault());
            var mails = await BuildMailsAsync(businessId, business, inv.ClientId, values, list);
            byte[]? pdfBytes = mails.Any(m => m.Attach) ? pdf.Generate(inv, inv.TemplateOverride ?? business.Template, null) : null;
            var fileName = $"{inv.InvoiceNumber.Replace('/', '-')}.pdf";

            var cts = new CancellationTokenSource();
            PendingPayments[inv.Id] = new PendingPayment(cts, first);
            _ = Task.Run(async () =>
            {
                try { await Task.Delay(PaymentDelay, cts.Token); }
                catch (TaskCanceledException) { return; }   // a newer change replaced this one
                if (PendingPayments.TryGetValue(inv.Id, out var cur) && cur.Cts == cts) PendingPayments.TryRemove(inv.Id, out _);
                foreach (var m in mails)
                {
                    try { await sender.SendAsync(new[] { m.To }, m.Subject, m.Body, m.Attach ? fileName : null, m.Attach ? pdfBytes : null); }
                    catch (Exception ex) { log.LogWarning(ex, "Could not email payment update for {Number} to {To}", inv.InvoiceNumber, m.To); }
                }
            });
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Payment email step failed for invoice {Number}", inv.InvoiceNumber);
        }
    }

    /// <summary>Emails a payment reminder for one bill using the active "Reminder" templates, and waits for the result so the caller can show
    /// what happened. Returns who it was sent to. Throws a clear message when nothing could be sent.</summary>
    public async Task<List<string>> SendReminderAsync(string businessId, Invoice inv)
    {
        if (!sender.IsConfigured) throw new AppException("Email isn't connected yet. Add the Smtp settings first.", 503);
        if (inv.Status != InvoiceStatus.Final) throw new AppException("Only finalized bills can be reminded.");
        if (inv.BalanceDue <= 0) throw new AppException("This bill has nothing due.");

        var list = await templates.ActiveForAsync(businessId, EmailTemplate.OnReminder);
        if (list.Count == 0)
            throw new AppException("There is no active reminder email. Open Email templates and switch on (or create) a template that is sent for \u201cPayment reminder\u201d.");

        var business = await businesses.GetAsync(businessId);
        var mails = await BuildMailsAsync(businessId, business, inv.ClientId, PaymentValues(inv, inv.PaymentStatus, inv.Payments.LastOrDefault()), list);
        if (mails.Count == 0)
            throw new AppException("There is no email address to send to. Add the client's email on the Clients screen.");

        byte[]? pdfBytes = mails.Any(m => m.Attach) ? pdf.Generate(inv, inv.TemplateOverride ?? business.Template, null) : null;
        var fileName = $"{inv.InvoiceNumber.Replace('/', '-')}.pdf";
        var sent = new List<string>();
        Exception? last = null;
        foreach (var m in mails)
        {
            try { await sender.SendAsync(new[] { m.To }, m.Subject, m.Body, m.Attach ? fileName : null, m.Attach ? pdfBytes : null); sent.Add(m.To); }
            catch (Exception ex) { last = ex; log.LogWarning(ex, "Could not send a reminder for {Number} to {To}", inv.InvoiceNumber, m.To); }
        }
        if (sent.Count == 0) throw last is AppException ? last : new AppException("The reminder could not be sent. Check the email settings and try again.", 503);

        await db.Invoices.UpdateOneAsync(x => x.Id == inv.Id && x.BusinessId == businessId,
            Builders<Invoice>.Update.Set(x => x.LastReminderAt, DateTime.UtcNow).Inc(x => x.ReminderCount, 1));
        return sent;
    }

    /// <summary>Sends one template, filled with sample values, to the business email so the owner can check it. Errors are shown to the user.</summary>
    public async Task SendTestAsync(string businessId, string templateId)
    {
        var t = await templates.GetAsync(businessId, templateId);
        var business = await businesses.GetAsync(businessId);
        if (!IsValidEmail(business.Email)) throw new AppException("Add your business email under My business first.");
        var sample = new Invoice
        {
            InvoiceNumber = InvoiceNumberService.Format(business.InvoiceNumbering, 1),
            InvoiceDate = DateTime.UtcNow.Date, CreatedAt = DateTime.UtcNow,
            BillTo = new PartySnapshot { Name = "Sample Client", Address = new Address { City = "Sample City" } },
            BusinessSnapshot = BusinessSnapshot.From(business),
            Totals = new InvoiceTotals { GrandTotal = 12345 },
            AmountInWords = "Rupees Twelve Thousand Three Hundred Forty-Five Only",
            AmountPaid = 5000, BalanceDue = 7345, PaymentStatus = PaymentStatus.PartlyPaid,
        };
        var values = PaymentValues(sample, PaymentStatus.Unpaid, new Payment { Amount = 5000, Mode = "UPI", Date = DateTime.UtcNow.Date });
        values["Event"] = t.Triggers.FirstOrDefault()?.ToLowerInvariant() ?? "test";
        await sender.SendAsync(new[] { business.Email.Trim() },
            "[TEST] " + EmailTemplateRenderer.Render(t.Subject, values), EmailTemplateRenderer.Render(t.Body, values), null, null);
    }
}
