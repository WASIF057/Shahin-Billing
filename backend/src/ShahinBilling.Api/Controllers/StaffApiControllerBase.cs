using Microsoft.AspNetCore.Authorization;
using ShahinBilling.Api.Auth;

namespace ShahinBilling.Api.Controllers;

/// <summary>Everything the owner and staff use. A client's ordering login is refused here, so it can never read prices,
/// other clients, bills or settings. (Only AuthController and PortalController accept a client login.)</summary>
[Authorize(Roles = AppRoles.Owner + "," + AppRoles.Staff)]
public abstract class StaffApiControllerBase : ApiControllerBase { }
