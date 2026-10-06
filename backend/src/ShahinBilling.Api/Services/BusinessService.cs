using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

public class BusinessService(MongoContext db)
{
    public async Task<Business> GetAsync(string businessId) =>
        await db.Businesses.Find(b => b.Id == businessId).FirstOrDefaultAsync() ?? throw new NotFoundException("Business");

    public async Task<Business> UpdateAsync(string businessId, Business input)
    {
        var existing = await GetAsync(businessId);
        input.Id = businessId;
        input.CreatedAt = existing.CreatedAt;
        input.UpdatedAt = DateTime.UtcNow;
        input.Gstin = input.Gstin.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(input.Address.StateCode))
            input.Address.StateCode = StateCodes.FromGstin(input.Gstin);
        if (!string.IsNullOrEmpty(input.Address.StateCode))
            input.Address.State = StateCodes.NameOf(input.Address.StateCode);
        input.Template ??= existing.Template;
        input.EmailSettings = existing.EmailSettings ?? new EmailSettings();   // only the Email screen changes this
        input.Reminders = existing.Reminders ?? new ReminderSettings();         // ...and this
        await db.Businesses.ReplaceOneAsync(b => b.Id == businessId, input);
        return input;
    }

    public async Task<TemplateSettings> UpdateTemplateAsync(string businessId, TemplateSettings template)
    {
        await GetAsync(businessId);
        await db.Businesses.UpdateOneAsync(b => b.Id == businessId,
            Builders<Business>.Update.Set(b => b.Template, template).Set(b => b.UpdatedAt, DateTime.UtcNow));
        return template;
    }

    public async Task<EmailSettings> UpdateEmailSettingsAsync(string businessId, EmailSettings settings)
    {
        await GetAsync(businessId);
        await db.Businesses.UpdateOneAsync(b => b.Id == businessId,
            Builders<Business>.Update.Set(b => b.EmailSettings, settings).Set(b => b.UpdatedAt, DateTime.UtcNow));
        return settings;
    }

    public async Task<ReminderSettings> UpdateRemindersAsync(string businessId, ReminderSettings settings)
    {
        await GetAsync(businessId);
        await db.Businesses.UpdateOneAsync(b => b.Id == businessId,
            Builders<Business>.Update.Set(b => b.Reminders, settings).Set(b => b.UpdatedAt, DateTime.UtcNow));
        return settings;
    }

    /// <summary>The business state code, falling back to the GSTIN prefix.</summary>
    public static string StateCodeOf(Business b) =>
        !string.IsNullOrEmpty(b.Address.StateCode) ? b.Address.StateCode : StateCodes.FromGstin(b.Gstin);
}
