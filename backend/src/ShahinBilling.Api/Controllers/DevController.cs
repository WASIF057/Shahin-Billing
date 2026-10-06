using Microsoft.AspNetCore.Authorization;
using ShahinBilling.Api.Auth;
using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

/// <summary>Development helpers. Returns 404 outside the Development environment.</summary>
[Authorize(Roles = AppRoles.Owner)]
public class DevController(SeedService seed, IWebHostEnvironment env) : StaffApiControllerBase
{
    [HttpPost("seed")]
    public async Task<IActionResult> Seed()
    {
        if (!env.IsDevelopment()) return NotFound();
        return Ok(new { message = await seed.SeedAsync(BusinessId) });
    }
}
