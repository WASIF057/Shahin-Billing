using MongoDB.Bson;

namespace ShahinBilling.Api.Models;

public enum InvoiceStatus { Draft, Final, Cancelled }
public enum PaymentStatus { Unpaid, PartlyPaid, Paid }

public class Invoice : BusinessEntity
{
    public string InvoiceNumber { get; set; } = "";
    public int Seq { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string FinancialYear { get; set; } = "";

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public string CancelReason { get; set; } = "";
    public DateTime? CancelledAt { get; set; }

    /// <summary>Copy of the business at save time, so later edits never change old bills.</summary>
    public BusinessSnapshot BusinessSnapshot { get; set; } = new();

    public string ClientId { get; set; } = "";
    public PartySnapshot BillTo { get; set; } = new();
    public string City { get; set; } = "";
    /// <summary>true = a plain BILL with no GST (own number series, left out of GSTR-1). Fixed once the bill is first saved.</summary>
    public bool IsNonGst { get; set; }
    /// <summary>When the last payment reminder email went out, and how many have been sent.</summary>
    public DateTime? LastReminderAt { get; set; }
    public int ReminderCount { get; set; }
    public bool ShipToSameAsBillTo { get; set; } = true;
    public PartySnapshot ShipTo { get; set; } = new();

    public string PlaceOfSupplyState { get; set; } = "";
    public string PlaceOfSupplyStateCode { get; set; } = "";
    public bool IsInterState { get; set; }

    public string PoNumber { get; set; } = "";
    public DateTime? PoDate { get; set; }
    public TransportDetails Transport { get; set; } = new();

    public List<InvoiceLine> Lines { get; set; } = new();
    public InvoiceTotals Totals { get; set; } = new();
    public List<GstSummaryRow> GstSummary { get; set; } = new();
    public string AmountInWords { get; set; } = "";
    public string Notes { get; set; } = "";

    public List<Payment> Payments { get; set; } = new();
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }

    public TemplateSettings? TemplateOverride { get; set; }
}

public class BusinessSnapshot
{
    public string Name { get; set; } = "";
    public string Logo { get; set; } = "";
    public Address Address { get; set; } = new();
    public string Phone { get; set; } = "";
    public string AlternatePhone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Website { get; set; } = "";
    public string Gstin { get; set; } = "";
    public string Pan { get; set; } = "";
    public BankDetails Bank { get; set; } = new();
    public string Signature { get; set; } = "";
    public string AuthorisedSignatoryName { get; set; } = "";
    public string TermsAndConditions { get; set; } = "";
    public string DeclarationText { get; set; } = "";

    public static BusinessSnapshot From(Business b) => new()
    {
        Name = b.Name, Logo = b.Logo, Address = b.Address.Clone(), Phone = b.Phone,
        AlternatePhone = b.AlternatePhone, Email = b.Email, Website = b.Website,
        Gstin = b.Gstin, Pan = b.Pan,
        Bank = new BankDetails
        {
            AccountName = b.Bank.AccountName, AccountNumber = b.Bank.AccountNumber, Ifsc = b.Bank.Ifsc,
            BankName = b.Bank.BankName, Branch = b.Bank.Branch, UpiId = b.Bank.UpiId
        },
        Signature = b.Signature, AuthorisedSignatoryName = b.AuthorisedSignatoryName,
        TermsAndConditions = b.TermsAndConditions, DeclarationText = b.DeclarationText
    };
}

public class PartySnapshot
{
    public string Name { get; set; } = "";
    public string Gstin { get; set; } = "";
    public string ContactPerson { get; set; } = "";
    public string Phone { get; set; } = "";
    public Address Address { get; set; } = new();
}

public class TransportDetails
{
    public string TransporterName { get; set; } = "";
    public string VehicleNumber { get; set; } = "";
    public string EwayBillNumber { get; set; } = "";
    public string LrNumber { get; set; } = "";
    public DateTime? DeliveryDate { get; set; }
}

public class InvoiceLine
{
    public string ItemId { get; set; } = "";
    public string Name { get; set; } = "";
    public string SizeOrVariant { get; set; } = "";
    public string HsnCode { get; set; } = "";
    public string Unit { get; set; } = "PCS";
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public bool IsSpecialRate { get; set; }
    public decimal Amount { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal GstRate { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
    public decimal Igst { get; set; }
    public decimal LineTotal { get; set; }
}

public class InvoiceTotals
{
    public decimal TotalQuantity { get; set; }
    public decimal TaxableTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal CgstTotal { get; set; }
    public decimal SgstTotal { get; set; }
    public decimal IgstTotal { get; set; }
    public decimal RoundOff { get; set; }
    public decimal GrandTotal { get; set; }
}

public class GstSummaryRow
{
    public decimal GstRate { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
    public decimal Igst { get; set; }
}

public class Payment
{
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Mode { get; set; } = "Bank Transfer";   // Cash | Bank Transfer | UPI | Cheque
    public string Reference { get; set; } = "";
    public string Note { get; set; } = "";
}

public class Counter
{
    public string Id { get; set; } = "";      // the businessId (one running counter per business)
    public int Seq { get; set; }
}
