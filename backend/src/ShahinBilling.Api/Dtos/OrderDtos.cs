using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Dtos;

// What a client may see. These deliberately have no rate, GST or price of any kind.
public record PortalCatalogItem(string ItemId, string Type, string Name, string Variant, string Cloth, string Colour, string Size, string Unit, string Label);
public record PortalProfile(string ClientName, string BusinessName, IReadOnlyList<string> Cities);
public record PortalOrderLineDto(string Label, decimal Quantity, string Unit);
public record PortalOrderDto(string Id, string OrderNumber, DateTime PlacedAt, OrderStatus Status, string City, string Note, string CancelReason,
    IReadOnlyList<PortalOrderLineDto> Lines, OrderPriority Priority);

public class OrderLineRequest
{
    public string ItemId { get; set; } = "";
    public decimal Quantity { get; set; }
}

public class PlaceOrderRequest
{
    public List<OrderLineRequest> Lines { get; set; } = new();
    public string City { get; set; } = "";
    public string Note { get; set; } = "";
    public OrderPriority Priority { get; set; } = OrderPriority.Normal;
}

/// <summary>An order owner or staff take by phone for a client.</summary>
public class TakeOrderRequest
{
    public string ClientId { get; set; } = "";
    public List<OrderLineRequest> Lines { get; set; } = new();
    public string City { get; set; } = "";
    public string Note { get; set; } = "";
    public OrderPriority Priority { get; set; } = OrderPriority.Normal;
    /// <summary>Email the client a copy of the order (uses the client's email).</summary>
    public bool EmailClient { get; set; }
    /// <summary>Email the business a copy.</summary>
    public bool EmailMe { get; set; }
}

public class OrderQuery
{
    public OrderStatus? Status { get; set; }
    public string? ClientId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class OrderStatusRequest
{
    public OrderStatus Status { get; set; }
    public string Reason { get; set; } = "";
}

public record PortalAccessDto(bool Exists, string? Email, bool IsActive, DateTime? CreatedAt, string? Warning);
