namespace ShahinBilling.Api.Models;

public class Business : Entity
{
    public string Name { get; set; } = "";
    public string Logo { get; set; } = "";            // data URL (png/jpg), max ~500 KB
    /// <summary>What the app shows as its logo: "Themed" (the built-in logo in the theme colours) or "Image" (the uploaded Logo). Empty = Image when a logo is uploaded, otherwise Themed. Bills always use the uploaded Logo.</summary>
    public string LogoStyle { get; set; } = "";
    public Address Address { get; set; } = new();
    public string Phone { get; set; } = "";
    public string AlternatePhone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Website { get; set; } = "";
    public string Gstin { get; set; } = "";
    public string Pan { get; set; } = "";
    public BankDetails Bank { get; set; } = new();
    public string Signature { get; set; } = "";       // data URL (png/jpg), max ~300 KB
    public string AuthorisedSignatoryName { get; set; } = "";
    public string TermsAndConditions { get; set; } =
        "1. Goods once sold will not be taken back.\n2. Interest @18% p.a. will be charged if payment is not made within the due date.\n3. Subject to local jurisdiction.";
    public string DeclarationText { get; set; } =
        "We declare that this invoice shows the actual price of the goods described and that all particulars are true and correct.";
    public InvoiceNumbering InvoiceNumbering { get; set; } = new();
    public TemplateSettings Template { get; set; } = new();
    public EmailSettings EmailSettings { get; set; } = new();
    /// <summary>Ask for an emailed 6-digit code when logging in (only while email is set up).</summary>
    public bool RequireLoginOtp { get; set; } = true;
    public ReminderSettings Reminders { get; set; } = new();
    /// <summary>The colours the owner chose for the app. Everyone in the business sees them. Empty = the default look.</summary>
    public UiTheme Theme { get; set; } = new();
}

public class UiTheme
{
    public string Primary { get; set; } = "";   // #rrggbb
    public string Accent { get; set; } = "";    // #rrggbb
}
