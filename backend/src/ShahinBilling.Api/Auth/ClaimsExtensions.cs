using System.Security.Claims;
using ShahinBilling.Api.Infrastructure;

namespace ShahinBilling.Api.Auth;

public static class ClaimsExtensions
{
    public const string UserIdClaim = "sub";
    public const string BusinessIdClaim = "businessId";

    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(UserIdClaim) ?? throw new AppException("Not logged in.", 401);

    public static string GetBusinessId(this ClaimsPrincipal user) =>
        user.FindFirstValue(BusinessIdClaim) ?? throw new AppException("Not logged in.", 401);
}
