using Microsoft.AspNetCore.Authorization;
using ShahinBilling.Api.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

public class BusinessController(BusinessService service) : StaffApiControllerBase
{
    [HttpGet]
    public Task<Business> Get() => service.GetAsync(BusinessId);

    [Authorize(Roles = AppRoles.Owner)]
    [HttpPut]
    public async Task<Business> Update(Business business, [FromServices] IValidator<Business> v)
    {
        await v.ValidateAndThrowAsync(business);
        var saved = await service.UpdateAsync(BusinessId, business);
        await Log("business.edited", "Setup", saved.Id, "Changed the business details");
        return saved;
    }

    [HttpGet("/api/meta/states")]
    public IEnumerable<object> States() => StateCodes.All.Select(kv => new { code = kv.Key, name = kv.Value });
}
