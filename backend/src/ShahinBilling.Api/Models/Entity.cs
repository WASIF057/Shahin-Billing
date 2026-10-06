using MongoDB.Bson;

namespace ShahinBilling.Api.Models;

/// <summary>Base for every stored document. Ids are stored as plain strings.</summary>
public abstract class Entity
{
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Every business-owned document carries the owning business id.
/// The value always comes from the logged-in user's token, never from the client.</summary>
public abstract class BusinessEntity : Entity
{
    public string BusinessId { get; set; } = "";
}
