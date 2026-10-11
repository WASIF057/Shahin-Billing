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

    /// <summary>The app colours. Only the owner can change them; staff see them through GET.</summary>
    [Authorize(Roles = AppRoles.Owner)]
    [HttpPut("theme")]
    public async Task<UiTheme> UpdateTheme(UiTheme theme)
    {
        static bool Ok(string? c) => string.IsNullOrEmpty(c) || System.Text.RegularExpressions.Regex.IsMatch(c, "^#[0-9a-fA-F]{6}$");
        if (!Ok(theme.Primary) || !Ok(theme.Accent)) throw new ShahinBilling.Api.Infrastructure.AppException("Colours must look like #2a5b8f.");
        theme.Primary = (theme.Primary ?? "").ToLowerInvariant();
        theme.Accent = (theme.Accent ?? "").ToLowerInvariant();
        var saved = await service.UpdateThemeAsync(BusinessId, theme);
        await Log("business.theme", "Setup", BusinessId, "Changed the app colour theme");
        return saved;
    }

    [HttpGet("/api/meta/states")]
    public IEnumerable<object> States() => StateCodes.All.Select(kv => new { code = kv.Key, name = kv.Value });
}
