using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Pdf;

public class InvoicePdfService
{
    public static readonly string[] AllCopies = { "Original", "Duplicate", "Triplicate" };

    /// <summary>One PDF; each requested copy is its own page set with its own page numbers.</summary>
    public byte[] Generate(Invoice invoice, TemplateSettings template, IEnumerable<string>? copies)
    {
        var list = (copies ?? Array.Empty<string>())
            .Select(c => AllCopies.FirstOrDefault(a => a.Equals(c.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Where(c => c != null).Select(c => c!).Distinct().ToList();
        if (list.Count == 0) list = template.DefaultCopies.Count > 0 ? template.DefaultCopies : new() { "Original" };

        var docs = list.Select(c => (IDocument)new InvoiceDocument(invoice, template, c)).ToList();
        return docs.Count == 1 ? docs[0].GeneratePdf() : Document.Merge(docs).UseOriginalPageNumbers().GeneratePdf();
    }

    /// <summary>A sample bill using the real business details, for the Bill Format live preview.</summary>
    public byte[] Preview(Business business, TemplateSettings template)
    {
        var stateCode = BusinessService.StateCodeOf(business);
        var inv = new Invoice
        {
            InvoiceNumber = InvoiceNumberService.Format(business.InvoiceNumbering, 1),
            InvoiceDate = DateTime.UtcNow.Date,
            CreatedAt = DateTime.UtcNow,
            Status = InvoiceStatus.Final,
            BusinessSnapshot = BusinessSnapshot.From(business),
            BillTo = new PartySnapshot
            {
                Name = "Sample Furniture Mart", Gstin = "27AAGCC3456Q1Z3", ContactPerson = "Mr. Rahul", Phone = "98200 00000",
                Address = new Address { Line1 = "12, Market Road", City = "Pune", State = "Maharashtra", StateCode = "27", Pincode = "411001" }
            },
            PlaceOfSupplyStateCode = string.IsNullOrEmpty(stateCode) ? "27" : stateCode,
            PoNumber = "PO-1045", PoDate = DateTime.UtcNow.Date.AddDays(-3),
            Transport = new TransportDetails { TransporterName = "City Logistics", VehicleNumber = "MH12 AB 1234", EwayBillNumber = "" },
        };
        inv.ShipTo = inv.BillTo;
        inv.PlaceOfSupplyState = StateCodes.NameOf(inv.PlaceOfSupplyStateCode);
        var lines = new List<InvoiceLine>
        {
            new() { Name = "Orthopedic Mattress", SizeOrVariant = "72x36x6 in", Unit = "PCS", Quantity = 10, Rate = 6500, GstRate = 18, Discount = 500 },
            new() { Name = "Fibre Pillow", SizeOrVariant = "17x27 in", Unit = "PCS", Quantity = 40, Rate = 120, GstRate = 18 }
        };
        var calc = InvoiceCalculator.Calculate(lines, false);
        inv.Lines = calc.Lines; inv.Totals = calc.Totals; inv.GstSummary = calc.Summary; inv.AmountInWords = calc.AmountInWords;
        return Generate(inv, template, new[] { "Original" });
    }
}
