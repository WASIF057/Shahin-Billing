using Microsoft.AspNetCore.Authorization;
using ShahinBilling.Api.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

[Authorize(Roles = AppRoles.Owner)]
public class EmailController(
    EmailTemplateService templates, EmailService email, EmailSender sender, IValidator<EmailTemplate> validator) : StaffApiControllerBase
{
    /// <summary>Whether the mail server (Smtp in appsettings) is set up.</summary>
    [HttpGet]
    public object Status() => new { smtpConfigured = sender.IsConfigured };

    /// <summary>The automatic reminder settings (off by default).</summary>
    [HttpGet("reminders")]
    public async Task<ReminderSettings> GetReminders([FromServices] BusinessService businesses) => (await businesses.GetAsync(BusinessId)).Reminders;

    [HttpPut("reminders")]
    public async Task<ReminderSettings> SaveReminders(ReminderSettings s, [FromServices] BusinessService businesses, [FromServices] IValidator<ReminderSettings> v)
    {
        await v.ValidateAndThrowAsync(s);
        var saved = await businesses.UpdateRemindersAsync(BusinessId, s);
        await Log("reminders.settings", "Email", null,
            saved.Enabled ? $"Switched on automatic payment reminders (after {saved.AfterDays} days, every {saved.RepeatEveryDays} days, up to {saved.MaxReminders})" : "Switched off automatic payment reminders");
        return saved;
    }

    [HttpGet("templates")]
    public Task<List<EmailTemplate>> List() => templates.ListAsync(BusinessId);

    [HttpGet("templates/{id}")]
    public Task<EmailTemplate> Get(string id) => templates.GetAsync(BusinessId, id);

    [HttpPost("templates")]
    public async Task<EmailTemplate> Create(EmailTemplate t)
    {
        await validator.ValidateAndThrowAsync(t);
        var created = await templates.CreateAsync(BusinessId, t);
        await Log("email.created", "Email", created.Id, $"Created email template {created.Name}");
        return created;
    }

    [HttpPut("templates/{id}")]
    public async Task<EmailTemplate> Update(string id, EmailTemplate t)
    {
        await validator.ValidateAndThrowAsync(t);
        var updated = await templates.UpdateAsync(BusinessId, id, t);
        await Log("email.edited", "Email", id, $"Edited email template {updated.Name}");
        return updated;
    }

    public record ActiveRequest(bool Active);

    [HttpPatch("templates/{id}/active")]
    public async Task<IActionResult> SetActive(string id, ActiveRequest req)
    {
        await templates.SetActiveAsync(BusinessId, id, req.Active);
        var t = await templates.GetAsync(BusinessId, id);
        await Log("email.active", "Email", id, $"{(req.Active ? "Switched on" : "Switched off")} email template {t.Name}");
        return NoContent();
    }

    [HttpDelete("templates/{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var t = await templates.GetAsync(BusinessId, id);
        await templates.DeleteAsync(BusinessId, id);
        await Log("email.deleted", "Email", id, $"Deleted email template {t.Name}");
        return NoContent();
    }

    /// <summary>Sends this template, filled with sample values, to your own business email.</summary>
    [HttpPost("templates/{id}/test")]
    public async Task<object> Test(string id)
    {
        await email.SendTestAsync(BusinessId, id);
        return new { message = "Test email sent. Check your inbox." };
    }
}
