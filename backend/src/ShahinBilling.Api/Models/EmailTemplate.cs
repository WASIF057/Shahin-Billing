namespace ShahinBilling.Api.Models;

/// <summary>One named email the app can send automatically.
/// Placeholders: {{InvoiceNumber}} {{InvoiceDate}} {{Time}} {{ClientName}} {{ClientCity}} {{BusinessName}} {{BusinessPhone}}
/// {{GrandTotal}} {{AmountInWords}} {{Event}} {{PaymentStatus}} {{PreviousStatus}} {{AmountPaid}} {{BalanceDue}}
/// {{PaymentAmount}} {{PaymentMode}} {{PaymentDate}}</summary>
public class EmailTemplate : BusinessEntity
{
    public const string ToClient = "Client";
    public const string ToBusiness = "Business";
    public const string OnGenerated = "Generated";          // a bill becomes final
    public const string OnDownloaded = "Downloaded";        // a bill PDF is downloaded
    public const string OnPaymentChanged = "PaymentChanged"; // payment status changes
    public const string OnOrderAccepted = "OrderAccepted";   // staff accept a client's order
    public const string OnOrderCancelled = "OrderCancelled"; // staff cancel a client's order
    public const string OnOrderPlaced = "OrderPlaced";       // a client places an order on the ordering website
    public const string OnReminder = "Reminder";             // asking the client to pay (by button, or automatically)

    public string Name { get; set; } = "";
    /// <summary>Who receives it: Client or Business (the owner's own email).</summary>
    public string Recipient { get; set; } = ToClient;
    /// <summary>When it is sent: any of Generated, Downloaded, PaymentChanged, Reminder, OrderPlaced, OrderAccepted, OrderCancelled.</summary>
    public List<string> Triggers { get; set; } = new();
    public bool AttachPdf { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
