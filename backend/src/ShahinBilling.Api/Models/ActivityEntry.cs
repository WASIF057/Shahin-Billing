namespace ShahinBilling.Api.Models;

/// <summary>One line of the activity log: who did what, and when. Kept for a year.</summary>
public class ActivityEntry
{
    public string Id { get; set; } = "";
    public string BusinessId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    /// <summary>e.g. "bill.created", "client.deleted"</summary>
    public string Action { get; set; } = "";
    /// <summary>Bill, Payment, Client, Item, Setup, Email, Team, Login, Backup, Import</summary>
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string Summary { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}
