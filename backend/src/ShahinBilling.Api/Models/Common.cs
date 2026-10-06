namespace ShahinBilling.Api.Models;

public class Address
{
    public string Line1 { get; set; } = "";
    public string Line2 { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string StateCode { get; set; } = "";
    public string Pincode { get; set; } = "";

    public Address Clone() => (Address)MemberwiseClone();
}

public class BankDetails
{
    public string AccountName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string Ifsc { get; set; } = "";
    public string BankName { get; set; } = "";
    public string Branch { get; set; } = "";
    public string UpiId { get; set; } = "";
}

public class InvoiceNumbering
{
    public string Prefix { get; set; } = "INV";
    public string Separator { get; set; } = "/";
    public int Padding { get; set; } = 4;
    /// <summary>Prefix for Non-GST bills. They count in their own series so GST invoice numbers never have gaps.</summary>
    public string NonGstPrefix { get; set; } = "NG";
}

/// <summary>Controls how the bill PDF looks. Edited on the Bill Format screen.</summary>
public class TemplateSettings
{
    public string Layout { get; set; } = "Classic";          // Classic | Modern
    public string PrimaryColor { get; set; } = "#1F4E79";
    public string FontSize { get; set; } = "Normal";         // Small | Normal | Large
    public string InvoiceTitle { get; set; } = "TAX INVOICE";
    public string NonGstTitle { get; set; } = "BILL";
    public string FooterNote { get; set; } = "This is a computer generated invoice.";
    public bool ShowLogo { get; set; } = true;
    public bool ShowDiscountColumn { get; set; } = true;     // still auto-hidden when every discount is 0
    public bool ShowHsnColumn { get; set; } = true;
    public bool ShowGstSummary { get; set; } = true;
    public bool ShowBankDetails { get; set; } = true;
    public bool ShowUpi { get; set; } = true;
    public bool ShowTerms { get; set; } = true;
    public bool ShowDeclaration { get; set; } = true;
    public bool ShowSignature { get; set; } = true;
    public bool ShowTransportDetails { get; set; } = true;
    public bool ShowPoDetails { get; set; } = true;
    public List<string> DefaultCopies { get; set; } = new() { "Original" };
}

/// <summary>When bill emails go out and what they say. Edited on the Email screen.
/// Extra placeholders for payment emails: {{PaymentStatus}} {{PreviousStatus}} {{AmountPaid}} {{BalanceDue}} {{PaymentAmount}} {{PaymentMode}} {{PaymentDate}}.
/// Placeholders: {{InvoiceNumber}} {{InvoiceDate}} {{Time}} {{ClientName}} {{ClientCity}} {{BusinessName}} {{BusinessPhone}} {{GrandTotal}} {{AmountInWords}} {{Event}}</summary>
public class EmailSettings
{
    public bool Enabled { get; set; } = true;
    public bool SendOnFinalize { get; set; } = true;     // when a bill is generated (finalized)
    public bool SendOnDownload { get; set; } = true;     // when its PDF is downloaded
    public bool SendToBusiness { get; set; } = true;
    public bool SendToClient { get; set; } = true;
    public bool AttachPdf { get; set; } = true;
    public bool SendOnPaymentChange { get; set; } = true;   // when a bill becomes Paid / Part paid / Unpaid
    /// <summary>true once the wording above has been turned into named email templates (see EmailTemplateService).</summary>
    public bool TemplatesMigrated { get; set; }
    /// <summary>true once the standard "Payment reminder" template has been added to this business.</summary>
    public bool ReminderTemplateSeeded { get; set; }
    /// <summary>true once the standard "order received" templates have been added to this business.</summary>
    public bool OrderTemplatesSeeded { get; set; }
    /// <summary>true once the "order accepted" and "order cancelled" emails to the client have been added.</summary>
    public bool OrderStatusTemplatesSeeded { get; set; }

    public string ClientSubject { get; set; } = "Invoice {{InvoiceNumber}} from {{BusinessName}}";
    public string ClientBody { get; set; } =
        "Dear {{ClientName}},\n\nThank you for your business. Please find your invoice attached.\n\n" +
        "Invoice No.: {{InvoiceNumber}}\nDate: {{InvoiceDate}}\nAmount: {{GrandTotal}}\n\n" +
        "For any questions, call us on {{BusinessPhone}}.\n\nRegards,\n{{BusinessName}}";

    public string BusinessSubject { get; set; } = "Invoice {{InvoiceNumber}} {{Event}} for {{ClientName}}";
    public string BusinessBody { get; set; } =
        "Invoice {{InvoiceNumber}} was {{Event}}.\n\nClient: {{ClientName}} ({{ClientCity}})\nDate: {{InvoiceDate}} {{Time}}\n" +
        "Amount: {{GrandTotal}}\n{{AmountInWords}}";

    public string PaymentClientSubject { get; set; } = "Payment update for invoice {{InvoiceNumber}}: {{PaymentStatus}}";
    public string PaymentClientBody { get; set; } =
        "Dear {{ClientName}},\n\nThe payment status of your invoice {{InvoiceNumber}} is now {{PaymentStatus}}.\n\n" +
        "Invoice amount: {{GrandTotal}}\nAmount received: {{AmountPaid}}\nBalance due: {{BalanceDue}}\n\n" +
        "Thank you for your payment.\n\nRegards,\n{{BusinessName}}\n{{BusinessPhone}}";

    public string PaymentBusinessSubject { get; set; } = "Payment {{PaymentStatus}}: {{InvoiceNumber}} ({{ClientName}})";
    public string PaymentBusinessBody { get; set; } =
        "Invoice {{InvoiceNumber}} for {{ClientName}} ({{ClientCity}}) changed from {{PreviousStatus}} to {{PaymentStatus}}.\n\n" +
        "Invoice amount: {{GrandTotal}}\nAmount received so far: {{AmountPaid}}\nBalance due: {{BalanceDue}}\n\n" +
        "Last payment: {{PaymentAmount}} by {{PaymentMode}} on {{PaymentDate}}";
}

/// <summary>Automatic payment reminders. Off until the owner switches it on, so clients never get emails by surprise.</summary>
public class ReminderSettings
{
    public bool Enabled { get; set; }
    /// <summary>First reminder once a bill has been unpaid this many days after its bill date.</summary>
    public int AfterDays { get; set; } = 15;
    /// <summary>Then again every this many days.</summary>
    public int RepeatEveryDays { get; set; } = 7;
    /// <summary>At most this many automatic reminders per bill.</summary>
    public int MaxReminders { get; set; } = 3;
}
