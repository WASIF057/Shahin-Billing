using Microsoft.AspNetCore.Authorization;
using ShahinBilling.Api.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

/// <summary>Item setup: the types (Bed, Pillow...) and their item names, variants and sizes.</summary>
[Authorize(Roles = AppRoles.Owner)]
public class CatalogController(ProductTypeService service, IValidator<ProductType> validator) : StaffApiControllerBase
{
    [HttpGet]
    public Task<List<ProductType>> List() => service.ListAsync(BusinessId);

    [HttpPost]
    public async Task<ProductType> Create(ProductType t)
    {
        await validator.ValidateAndThrowAsync(t);
        var created = await service.CreateAsync(BusinessId, t);
        await Log("type.created", "Setup", created.Id, $"Added item type {created.Name}");
        return created;
    }

    [HttpPut("{id}")]
    public async Task<ProductType> Update(string id, ProductType t)
    {
        await validator.ValidateAndThrowAsync(t);
        var updated = await service.UpdateAsync(BusinessId, id, t);
        await Log("type.edited", "Setup", id, $"Edited item type {updated.Name}");
        return updated;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var t = await service.GetAsync(BusinessId, id);
        await service.DeleteAsync(BusinessId, id);
        await Log("type.deleted", "Setup", id, $"Deleted item type {t.Name}");
        return NoContent();
    }
}
