using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Dtos;

/// <summary>What the Create/Edit Bill screen sends. Totals are never trusted from the client;
/// the server recalculates everything.</summary>
public class InvoiceRequest
{
    public DateTime InvoiceDate { get; set; }
    public string ClientId { get; set; } = "";
    /// <summary>Which of the client's cities this bill is for (empty = the client's first city).</summary>
    public string City { get; set; } = "";
    /// <summary>true = a Non-GST bill. Only used when the bill is first created; a saved bill keeps its type.</summary>
    public bool NonGst { get; set; }
    /// <summary>When the bill is made from a client's order: that order. It is marked Billed once the bill is saved.</summary>
    public string? OrderId { get; set; }
    public bool ShipToSameAsBillTo { get; set; } = true;
    public PartySnapshot? ShipTo { get; set; }
    public string PlaceOfSupplyStateCode { get; set; } = "";
    public string PoNumber { get; set; } = "";
    public DateTime? PoDate { get; set; }
    public TransportDetails Transport { get; set; } = new();
    public string Notes { get; set; } = "";
    public List<InvoiceLineRequest> Lines { get; set; } = new();
    /// <summary>true = Save &amp; Finalize, false = Save Draft.</summary>
    public bool Finalize { get; set; }
}

public class InvoiceLineRequest
{
    public string ItemId { get; set; } = "";
    public decimal Quantity { get; set; }
    /// <summary>null = use the client's resolved rate.</summary>
    public decimal? Rate { get; set; }
    public decimal Discount { get; set; }
}

public class CancelRequest
{
    public string Reason { get; set; } = "";
}

public class PaymentRequest
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Mode { get; set; } = "Bank Transfer";
    public string Reference { get; set; } = "";
    public string Note { get; set; } = "";
}

public class InvoiceQuery
{
    public string? Search { get; set; }
    public string? ClientId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public InvoiceStatus? Status { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record InvoiceListItem(
    string Id, string InvoiceNumber, DateTime InvoiceDate, string ClientId, string ClientName,
    decimal GrandTotal, decimal AmountPaid, decimal BalanceDue, InvoiceStatus Status, PaymentStatus PaymentStatus, bool IsNonGst = false);

public record PagedResult<T>(IReadOnlyList<T> Items, long Total, int Page, int PageSize);

public record RateResponse(decimal Rate, bool IsSpecial);

public record ClientSpecialRate(string ItemId, string ItemName, string SizeOrVariant, decimal DefaultRate, decimal SpecialRate);
