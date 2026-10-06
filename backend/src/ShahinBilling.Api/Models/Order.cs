namespace ShahinBilling.Api.Models;

public enum OrderStatus { New, Accepted, Billed, Cancelled }

/// <summary>How soon the client needs the order. Urgent ones are listed first.</summary>
public enum OrderPriority { Normal, High, Urgent }

/// <summary>Where an order came from: the client's ordering login, or taken by owner/staff over the phone.</summary>
public static class OrderSource
{
    public const string Website = "Website";
    public const string Phone = "Phone";
}

/// <summary>A product order placed by a client through their ordering login. Holds product names and quantities only: never prices.</summary>
public class Order : BusinessEntity
{
    public string OrderNumber { get; set; } = "";
    public int Seq { get; set; }
    public string ClientId { get; set; } = "";
    public string ClientName { get; set; } = "";
    /// <summary>The client's city this order is for (they pick one when they have several).</summary>
    public string City { get; set; } = "";
    /// <summary>The ordering login: who placed it, and where its confirmation email went.</summary>
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string UserEmail { get; set; } = "";
    public OrderStatus Status { get; set; } = OrderStatus.New;
    public OrderPriority Priority { get; set; } = OrderPriority.Normal;
    /// <summary>Priority as a number (0 Normal, 1 High, 2 Urgent) so the list can be sorted by it.</summary>
    public int PriorityRank { get; set; }
    public string Source { get; set; } = OrderSource.Website;
    /// <summary>For phone orders: the owner or staff member who took the call.</summary>
    public string TakenBy { get; set; } = "";
    public string Note { get; set; } = "";
    public List<OrderLine> Lines { get; set; } = new();
    public string CancelReason { get; set; } = "";
    /// <summary>Who accepted or cancelled it ("Client" or the staff member's name).</summary>
    public string HandledBy { get; set; } = "";
    public DateTime? HandledAt { get; set; }
    /// <summary>The bill made from this order.</summary>
    public string? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
}

public class OrderLine
{
    public string ItemId { get; set; } = "";
    public string Label { get; set; } = "";
    public string Name { get; set; } = "";
    public string Variant { get; set; } = "";
    public string Cloth { get; set; } = "";
    public string Colour { get; set; } = "";
    public string Size { get; set; } = "";
    public string Unit { get; set; } = "PCS";
    public decimal Quantity { get; set; }
}
