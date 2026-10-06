using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Auth;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

[Authorize(Roles = AppRoles.Owner)]
public class ActivityController(ActivityService service) : StaffApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<ActivityEntry>> List([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? userId,
        [FromQuery] string? type, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        service.QueryAsync(BusinessId, from, to, userId, type, page, pageSize);
}
