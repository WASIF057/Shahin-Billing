using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using ShahinBilling.Api.Data;

namespace ShahinBilling.Api.Auth;

/// <summary>Checks on every request that the login is still allowed: the user exists, is switched on, and has not ended their sessions.
/// The answer is cached for 10 seconds, so ending a session takes effect almost at once without a database read on every call.</summary>
public class SessionValidator(MongoContext db, IMemoryCache cache)
{
    private class State { public int Version { get; set; } public bool Active { get; set; } }

    public async Task<bool> IsValidAsync(ClaimsPrincipal p)
    {
        var id = p.FindFirstValue(ClaimsExtensions.UserIdClaim);
        if (id == null) return false;
        var tv = int.TryParse(p.FindFirstValue("tv"), out var v) ? v : 0;   // sessions started before this existed count as version 0

        var state = await cache.GetOrCreateAsync("sess:" + id, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
            // Read the whole user, not a projection: accounts made before these fields existed have neither stored,
            // and only a full read gives them the defaults (switched on, session version 0).
            var u = await db.Users.Find(x => x.Id == id).FirstOrDefaultAsync();
            return u == null ? null : new State { Version = u.TokenVersion, Active = u.IsActive };
        });
        return state != null && state.Active && state.Version == tv;
    }

    public void Forget(string userId) => cache.Remove("sess:" + userId);
}
