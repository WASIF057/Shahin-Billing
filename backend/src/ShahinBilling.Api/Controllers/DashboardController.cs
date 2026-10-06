using Microsoft.AspNetCore.Authorization;
using ShahinBilling.Api.Auth;
using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

[Authorize(Roles = AppRoles.Owner)]
public class DashboardController(DashboardService service) : StaffApiControllerBase
{
    /// <summary>Optional filters: ?clientId= &amp;city= &amp;from= &amp;to= . Without dates it shows this month and the financial year.</summary>
    [HttpGet]
    public Task<DashboardDto> Get([FromQuery] string? clientId, [FromQuery] string? city, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        if (from.HasValue && to.HasValue && to < from) throw new AppException("The end date must be after the start date.");
        return service.GetAsync(BusinessId, clientId, city, from, to);
    }
}
