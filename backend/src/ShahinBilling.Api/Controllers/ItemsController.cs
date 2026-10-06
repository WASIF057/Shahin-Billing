using Microsoft.AspNetCore.Authorization;
using ShahinBilling.Api.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

public class ItemsController(ItemService service, IValidator<Item> validator, ShahinBilling.Api.Reports.ReportService reports) : StaffApiControllerBase
{
    [HttpGet]
    public Task<List<Item>> List([FromQuery] string? search, [FromQuery] bool? active) => service.ListAsync(BusinessId, search, active);

    /// <summary>Items as an Excel file, one sheet per type. ?typeId= (empty = all, "none" = no type, or a type id).</summary>
    [Authorize(Roles = AppRoles.Owner)]
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string? typeId)
    {
        var (bytes, label) = await reports.ItemsAsync(BusinessId, typeId);
        var safe = string.Concat(label.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-'));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Items-{safe}-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    [HttpGet("{id}")]
    public Task<Item> Get(string id) => service.GetAsync(BusinessId, id);

    [Authorize(Roles = AppRoles.Owner)]
    [HttpPost]
    public async Task<Item> Create(Item item)
    {
        await validator.ValidateAndThrowAsync(item);
        var created = await service.CreateAsync(BusinessId, item);
        await Log("item.created", "Item", created.Id, $"Added item {Label(created)}");
        return created;
    }

    [Authorize(Roles = AppRoles.Owner)]
    [HttpPut("{id}")]
    public async Task<Item> Update(string id, Item item)
    {
        await validator.ValidateAndThrowAsync(item);
        var updated = await service.UpdateAsync(BusinessId, id, item);
        await Log("item.edited", "Item", id, $"Edited item {Label(updated)} (rate Rs. {ShahinBilling.Api.Helpers.Money.FormatIndian(updated.DefaultRate)})");
        return updated;
    }

    private static string Label(Item i) => string.IsNullOrEmpty(i.SizeOrVariant) ? i.Name : $"{i.Name} ({i.SizeOrVariant})";

    public record ActiveRequest(bool Active);

    [Authorize(Roles = AppRoles.Owner)]
    [HttpPatch("{id}/active")]
    public async Task<IActionResult> SetActive(string id, ActiveRequest req)
    {
        await service.SetActiveAsync(BusinessId, id, req.Active);
        var i = await service.GetAsync(BusinessId, id);
        await Log(req.Active ? "item.shown" : "item.hidden", "Item", id, $"{(req.Active ? "Showed" : "Hid")} item {Label(i)} {(req.Active ? "in" : "from")} billing");
        return NoContent();
    }

    [Authorize(Roles = AppRoles.Owner)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var i = await service.GetAsync(BusinessId, id);
        await service.DeleteAsync(BusinessId, id);
        await Log("item.deleted", "Item", id, $"Deleted item {Label(i)}");
        return NoContent();
    }

    [HttpGet("{id}/rate")]
    public Task<RateResponse> Rate(string id, [FromQuery] string? clientId) => service.RateForClientAsync(BusinessId, id, clientId);
}
