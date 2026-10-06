using Microsoft.AspNetCore.Authorization;
using ShahinBilling.Api.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Pdf;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

[Authorize(Roles = AppRoles.Owner)]
public class TemplateController(
    BusinessService businesses, BillFormatService formats, InvoicePdfService pdf, IValidator<BillFormat> validator)
    : StaffApiControllerBase
{
    /// <summary>The settings in use right now (the active format).</summary>
    [HttpGet]
    public async Task<TemplateSettings> Get() => (await businesses.GetAsync(BusinessId)).Template;

    [HttpGet("formats")]
    public Task<List<BillFormat>> List() => formats.ListAsync(BusinessId);

    [HttpGet("formats/{id}")]
    public Task<BillFormat> GetFormat(string id) => formats.GetAsync(BusinessId, id);

    [HttpPost("formats")]
    public async Task<BillFormat> Create(BillFormat f)
    {
        await validator.ValidateAndThrowAsync(f);
        var created = await formats.CreateAsync(BusinessId, f);
        await Log("format.created", "Setup", created.Id, $"Created bill format {created.Name}");
        return created;
    }

    [HttpPut("formats/{id}")]
    public async Task<BillFormat> Update(string id, BillFormat f)
    {
        await validator.ValidateAndThrowAsync(f);
        var updated = await formats.UpdateAsync(BusinessId, id, f);
        await Log("format.edited", "Setup", id, $"Edited bill format {updated.Name}");
        return updated;
    }

    public record ActiveRequest(bool Active);

    [HttpPatch("formats/{id}/active")]
    public async Task<IActionResult> SetActive(string id, ActiveRequest req)
    {
        await formats.SetActiveAsync(BusinessId, id, req.Active);
        var f = await formats.GetAsync(BusinessId, id);
        await Log("format.active", "Setup", id, $"{(req.Active ? "Switched on" : "Switched off")} bill format {f.Name}");
        return NoContent();
    }

    [HttpDelete("formats/{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var f = await formats.GetAsync(BusinessId, id);
        await formats.DeleteAsync(BusinessId, id);
        await Log("format.deleted", "Setup", id, $"Deleted bill format {f.Name}");
        return NoContent();
    }

    /// <summary>Renders a sample bill with the given (unsaved) settings for the live preview.</summary>
    [HttpPost("preview-pdf")]
    public async Task<IActionResult> Preview(TemplateSettings template)
    {
        var business = await businesses.GetAsync(BusinessId);
        return File(pdf.Preview(business, template), "application/pdf");
    }
}
