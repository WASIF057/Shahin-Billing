using MongoDB.Bson;
using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

public class EmailTemplateService(MongoContext db, BusinessService businesses)
{
    /// <summary>The wording that used to live in the single Email settings box, as four named templates.</summary>
    public static List<EmailTemplate> FromLegacy(EmailSettings s)
    {
        var bill = new List<string>();
        if (s.SendOnFinalize) bill.Add(EmailTemplate.OnGenerated);
        if (s.SendOnDownload) bill.Add(EmailTemplate.OnDownloaded);
        if (bill.Count == 0) { bill.Add(EmailTemplate.OnGenerated); }

        return new()
        {
            new() { Name = "Bill email to client", Recipient = EmailTemplate.ToClient, Triggers = new(bill), AttachPdf = s.AttachPdf,
                    Subject = s.ClientSubject, Body = s.ClientBody, IsActive = s.Enabled && s.SendToClient && (s.SendOnFinalize || s.SendOnDownload) },
            new() { Name = "Bill email to me", Recipient = EmailTemplate.ToBusiness, Triggers = new(bill), AttachPdf = s.AttachPdf,
                    Subject = s.BusinessSubject, Body = s.BusinessBody, IsActive = s.Enabled && s.SendToBusiness && (s.SendOnFinalize || s.SendOnDownload) },
            new() { Name = "Payment update to client", Recipient = EmailTemplate.ToClient, Triggers = new() { EmailTemplate.OnPaymentChanged },
                    Subject = s.PaymentClientSubject, Body = s.PaymentClientBody, IsActive = s.Enabled && s.SendToClient && s.SendOnPaymentChange },
            new() { Name = "Payment update to me", Recipient = EmailTemplate.ToBusiness, Triggers = new() { EmailTemplate.OnPaymentChanged },
                    Subject = s.PaymentBusinessSubject, Body = s.PaymentBusinessBody, IsActive = s.Enabled && s.SendToBusiness && s.SendOnPaymentChange },
        };
    }

    /// <summary>Guards against a recipient value that is empty or unknown (e.g. a template saved while multi-select existed): treat as the client.</summary>
    private static EmailTemplate Upgrade(EmailTemplate t)
    {
        if (t.Recipient is not (EmailTemplate.ToClient or EmailTemplate.ToBusiness)) t.Recipient = EmailTemplate.ToClient;
        return t;
    }

    public static EmailTemplate DefaultReminder() => new()
    {
        Name = "Payment reminder to client", Recipient = EmailTemplate.ToClient, Triggers = new() { EmailTemplate.OnReminder }, AttachPdf = true, IsActive = true,
        Subject = "Payment reminder: invoice {{InvoiceNumber}}",
        Body = "Dear {{ClientName}},\n\nThis is a friendly reminder that invoice {{InvoiceNumber}} dated {{InvoiceDate}} for {{GrandTotal}} is still unpaid.\n\n" +
               "Amount received: {{AmountPaid}}\nBalance due: {{BalanceDue}}\n({{DaysOutstanding}} days since the bill date)\n\n" +
               "Please arrange the payment at your earliest. If you have already paid, kindly ignore this message.\n\nRegards,\n{{BusinessName}}\n{{BusinessPhone}}",
    };

    /// <summary>The two standard "order placed" emails: a confirmation to the client and a heads-up to the owner. Item lists only: no prices.</summary>
    public static List<EmailTemplate> DefaultOrderTemplates() => new()
    {
        new() { Name = "Order received (to client)", Recipient = EmailTemplate.ToClient, Triggers = new() { EmailTemplate.OnOrderPlaced }, IsActive = true,
                Subject = "We received your order {{OrderNumber}}",
                Body = "Dear {{ClientName}},\n\nThank you for your order {{OrderNumber}}, placed on {{OrderDate}}.\n\nItems ordered:\n{{OrderItems}}\n\nNote: {{OrderNote}}\n\nWe will confirm it shortly.\n\nRegards,\n{{BusinessName}}\n{{BusinessPhone}}" },
        new() { Name = "New order (to me)", Recipient = EmailTemplate.ToBusiness, Triggers = new() { EmailTemplate.OnOrderPlaced }, IsActive = true,
                Subject = "New order {{OrderNumber}} from {{ClientName}}",
                Body = "New order {{OrderNumber}} from {{ClientName}} ({{ClientCity}}), placed on {{OrderDate}} ({{OrderSource}}). Priority: {{OrderPriority}}.\n\nItems:\n{{OrderItems}}\n\nNote: {{OrderNote}}\n\nOpen Orders in the app to accept it or make the bill." },
    };

    /// <summary>The two emails a client gets when the owner or staff accept or cancel their order. Item lists only: no prices.</summary>
    public static List<EmailTemplate> DefaultOrderStatusTemplates() => new()
    {
        new() { Name = "Order accepted (to client)", Recipient = EmailTemplate.ToClient, Triggers = new() { EmailTemplate.OnOrderAccepted }, IsActive = true,
                Subject = "Your order {{OrderNumber}} is accepted",
                Body = "Dear {{ClientName}},\n\nGood news: we have accepted your order {{OrderNumber}}.\n\nItems:\n{{OrderItems}}\n\nWe will get it ready and send you the bill. If anything needs to change, please call us on {{BusinessPhone}}.\n\nRegards,\n{{BusinessName}}" },
        new() { Name = "Order cancelled (to client)", Recipient = EmailTemplate.ToClient, Triggers = new() { EmailTemplate.OnOrderCancelled }, IsActive = true,
                Subject = "Your order {{OrderNumber}} was cancelled",
                Body = "Dear {{ClientName}},\n\nWe are sorry: your order {{OrderNumber}} has been cancelled.\n\nReason: {{OrderCancelReason}}\n\nItems:\n{{OrderItems}}\n\nPlease call us on {{BusinessPhone}} if you have any questions.\n\nRegards,\n{{BusinessName}}" },
    };

    /// <summary>First time only: turn the old single-box wording into templates (so nothing the owner edited is lost),
    /// and add the standard reminder template.</summary>
    private async Task EnsureMigratedAsync(string businessId)
    {
        var business = await businesses.GetAsync(businessId);
        var s = business.EmailSettings ?? new EmailSettings();
        if (s.TemplatesMigrated && s.ReminderTemplateSeeded && s.OrderTemplatesSeeded && s.OrderStatusTemplatesSeeded) return;

        var now = DateTime.UtcNow;
        void Stamp(EmailTemplate t) { t.Id = ObjectId.GenerateNewId().ToString(); t.BusinessId = businessId; t.CreatedAt = t.UpdatedAt = now; }

        if (!s.TemplatesMigrated)
        {
            var list = FromLegacy(s);
            foreach (var t in list) Stamp(t);
            if (await db.EmailTemplates.CountDocumentsAsync(x => x.BusinessId == businessId) == 0)
                await db.EmailTemplates.InsertManyAsync(list);
            s.TemplatesMigrated = true;
        }
        if (!s.ReminderTemplateSeeded)
        {
            if (!await db.EmailTemplates.Find(x => x.BusinessId == businessId && x.Triggers.Contains(EmailTemplate.OnReminder)).AnyAsync())
            {
                var r = DefaultReminder();
                Stamp(r);
                await db.EmailTemplates.InsertOneAsync(r);
            }
            s.ReminderTemplateSeeded = true;
        }
        if (!s.OrderTemplatesSeeded)
        {
            if (!await db.EmailTemplates.Find(x => x.BusinessId == businessId && x.Triggers.Contains(EmailTemplate.OnOrderPlaced)).AnyAsync())
            {
                var list = DefaultOrderTemplates();
                foreach (var t in list) Stamp(t);
                await db.EmailTemplates.InsertManyAsync(list);
            }
            s.OrderTemplatesSeeded = true;
        }
        if (!s.OrderStatusTemplatesSeeded)
        {
            if (!await db.EmailTemplates.Find(x => x.BusinessId == businessId && (x.Triggers.Contains(EmailTemplate.OnOrderAccepted) || x.Triggers.Contains(EmailTemplate.OnOrderCancelled))).AnyAsync())
            {
                var list = DefaultOrderStatusTemplates();
                foreach (var t in list) Stamp(t);
                await db.EmailTemplates.InsertManyAsync(list);
            }
            s.OrderStatusTemplatesSeeded = true;
        }
        await businesses.UpdateEmailSettingsAsync(businessId, s);
    }

    public async Task<List<EmailTemplate>> ListAsync(string businessId)
    {
        await EnsureMigratedAsync(businessId);
        return (await db.EmailTemplates.Find(x => x.BusinessId == businessId).SortBy(x => x.Name).ToListAsync()).Select(Upgrade).ToList();
    }

    public async Task<EmailTemplate> GetAsync(string businessId, string id) =>
        Upgrade(await db.EmailTemplates.Find(x => x.BusinessId == businessId && x.Id == id).FirstOrDefaultAsync()
                ?? throw new NotFoundException("Email template"));

    public async Task<List<EmailTemplate>> ActiveForAsync(string businessId, string trigger)
    {
        await EnsureMigratedAsync(businessId);
        return (await db.EmailTemplates.Find(x => x.BusinessId == businessId && x.IsActive && x.Triggers.Contains(trigger)).ToListAsync()).Select(Upgrade).ToList();
    }

    public async Task<EmailTemplate> CreateAsync(string businessId, EmailTemplate t)
    {
        await EnsureMigratedAsync(businessId);
        t.Id = ObjectId.GenerateNewId().ToString();
        t.BusinessId = businessId;
        t.CreatedAt = t.UpdatedAt = DateTime.UtcNow;
        await Normalize(businessId, t);
        await db.EmailTemplates.InsertOneAsync(t);
        return t;
    }

    public async Task<EmailTemplate> UpdateAsync(string businessId, string id, EmailTemplate t)
    {
        var existing = await GetAsync(businessId, id);
        t.Id = id;
        t.BusinessId = businessId;
        t.CreatedAt = existing.CreatedAt;
        t.UpdatedAt = DateTime.UtcNow;
        await Normalize(businessId, t);
        await db.EmailTemplates.ReplaceOneAsync(x => x.Id == id && x.BusinessId == businessId, t);
        return t;
    }

    public async Task SetActiveAsync(string businessId, string id, bool active)
    {
        var r = await db.EmailTemplates.UpdateOneAsync(x => x.Id == id && x.BusinessId == businessId,
            Builders<EmailTemplate>.Update.Set(x => x.IsActive, active).Set(x => x.UpdatedAt, DateTime.UtcNow));
        if (r.MatchedCount == 0) throw new NotFoundException("Email template");
    }

    public async Task DeleteAsync(string businessId, string id)
    {
        var r = await db.EmailTemplates.DeleteOneAsync(x => x.Id == id && x.BusinessId == businessId);
        if (r.DeletedCount == 0) throw new NotFoundException("Email template");
    }

    private async Task Normalize(string businessId, EmailTemplate t)
    {
        t.Name = t.Name.Trim();
        t.Triggers = t.Triggers.Distinct().ToList();
        var names = await db.EmailTemplates.Find(x => x.BusinessId == businessId && x.Id != t.Id).Project(x => x.Name).ToListAsync();
        if (names.Any(n => n.Equals(t.Name, StringComparison.OrdinalIgnoreCase)))
            throw new AppException($"There is already a template called \u201c{t.Name}\u201d.");
    }
}
