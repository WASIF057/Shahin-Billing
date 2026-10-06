using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

/// <summary>Sends automatic payment reminders for businesses that switched them on (Email templates screen).
/// Runs every 3 hours and only sends between 9 am and 7 pm Indian time, so clients never get a reminder at night.</summary>
public class ReminderHostedService(IServiceScopeFactory scopes, MongoContext db, ILogger<ReminderHostedService> log) : BackgroundService
{
    /// <summary>Is this bill due a reminder now? Pure function, unit tested.</summary>
    public static bool IsDue(Invoice b, ReminderSettings s, DateTime nowUtc)
    {
        if (!s.Enabled || b.Status != InvoiceStatus.Final || b.BalanceDue <= 0 || b.ReminderCount >= s.MaxReminders) return false;
        var today = nowUtc.AddHours(5.5).Date;
        if ((today - b.InvoiceDate.Date).TotalDays < s.AfterDays) return false;
        return b.LastReminderAt == null || (nowUtc - b.LastReminderAt.Value).TotalDays >= s.RepeatEveryDays;
    }

    public static bool IsSendingHour(DateTime nowUtc) { var h = nowUtc.AddHours(5.5).Hour; return h >= 9 && h < 19; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(1), ct); } catch (OperationCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            try { if (IsSendingHour(DateTime.UtcNow)) await RunOnceAsync(); }
            catch (Exception ex) { log.LogWarning(ex, "Automatic reminders failed"); }
            try { await Task.Delay(TimeSpan.FromHours(3), ct); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync()
    {
        var on = await db.Businesses.Find(b => b.Reminders.Enabled).ToListAsync();
        foreach (var business in on)
        {
            var bills = await db.Invoices.Find(x => x.BusinessId == business.Id && x.Status == InvoiceStatus.Final && x.BalanceDue > 0).ToListAsync();
            foreach (var bill in bills.Where(b => IsDue(b, business.Reminders, DateTime.UtcNow)))
            {
                using var scope = scopes.CreateScope();
                var email = scope.ServiceProvider.GetRequiredService<EmailService>();
                var activity = scope.ServiceProvider.GetRequiredService<ActivityService>();
                try
                {
                    var to = await email.SendReminderAsync(business.Id, bill);
                    await activity.LogAsync(business.Id, "system", "Automatic reminder", "reminder.auto", "Bill", bill.Id,
                        $"Reminder for bill {bill.InvoiceNumber} ({bill.BillTo.Name}) emailed to {string.Join(", ", to)}");
                }
                catch (Exception ex)
                {
                    // Try again after the repeat period rather than every 3 hours
                    await db.Invoices.UpdateOneAsync(x => x.Id == bill.Id, Builders<Invoice>.Update.Set(x => x.LastReminderAt, DateTime.UtcNow));
                    log.LogInformation("No reminder sent for {Number}: {Reason}", bill.InvoiceNumber, ex.Message);
                }
            }
        }
    }
}
