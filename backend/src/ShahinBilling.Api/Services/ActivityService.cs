using MongoDB.Bson;
using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

public class ActivityService(MongoContext db, ILogger<ActivityService> log)
{
    /// <summary>Records one event. A problem writing the log is only logged on the server: it must never stop real work.</summary>
    public async Task LogAsync(string businessId, string userId, string userName, string action, string entityType, string? entityId, string summary)
    {
        try
        {
            await db.Activity.InsertOneAsync(new ActivityEntry
            {
                Id = ObjectId.GenerateNewId().ToString(), BusinessId = businessId, UserId = userId, UserName = userName,
                Action = action, EntityType = entityType, EntityId = entityId, Summary = summary, At = DateTime.UtcNow,
            });
        }
        catch (Exception ex) { log.LogWarning(ex, "Could not write the activity log"); }
    }

    public async Task<PagedResult<ActivityEntry>> QueryAsync(string businessId, DateTime? from, DateTime? to, string? userId, string? type, int page, int pageSize)
    {
        var f = Builders<ActivityEntry>.Filter;
        var filter = f.Eq(x => x.BusinessId, businessId);
        if (from.HasValue) filter &= f.Gte(x => x.At, DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc).AddHours(-5.5));   // dates are Indian days
        if (to.HasValue) filter &= f.Lt(x => x.At, DateTime.SpecifyKind(to.Value.Date, DateTimeKind.Utc).AddDays(1).AddHours(-5.5));
        if (!string.IsNullOrWhiteSpace(userId)) filter &= f.Eq(x => x.UserId, userId);
        if (!string.IsNullOrWhiteSpace(type)) filter &= f.Eq(x => x.EntityType, type);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, 200);
        var total = await db.Activity.CountDocumentsAsync(filter);
        var items = await db.Activity.Find(filter).SortByDescending(x => x.At).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync();
        return new PagedResult<ActivityEntry>(items, total, page, pageSize);
    }
}
