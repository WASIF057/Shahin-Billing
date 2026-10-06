using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Auth;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Always taken from the JWT, never from the request body (data isolation).</summary>
    protected string BusinessId => User.GetBusinessId();
    protected string UserId => User.GetUserId();
    protected string UserName => User.FindFirst("name")?.Value ?? "";

    /// <summary>Writes one line to the activity log (who did what). Never fails the request.</summary>
    protected Task Log(string action, string entityType, string? entityId, string summary) =>
        HttpContext.RequestServices.GetRequiredService<ActivityService>()
            .LogAsync(BusinessId, UserId, UserName, action, entityType, entityId, summary);
}
