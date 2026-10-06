using System.Globalization;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Reports;

public class ReportService(MongoContext db)
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>GSTR-1 helper workbook for one month: B2B sheet + HSN summary. Final bills only.</summary>
    public async Task<byte[]> Gstr1Async(string businessId, int year, int month)
    {
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);
        var bills = await db.Invoices.Find(x => x.BusinessId == businessId && x.Status == InvoiceStatus.Final && !x.IsNonGst
                                                 && x.InvoiceDate >= from && x.InvoiceDate < to)
            .SortBy(x => x.InvoiceDate).ThenBy(x => x.Seq).ToListAsync();

        using var wb = new XLWorkbook();

        var b2b = wb.Worksheets.Add("B2B");
        string[] head =
        {
            "GSTIN/UIN of Recipient", "Receiver Name", "Invoice Number", "Invoice Date", "Invoice Value",
            "Place Of Supply", "Reverse Charge", "Applicable % of Tax Rate", "Invoice Type", "E-Commerce GSTIN",
            "Rate", "Taxable Value", "Cess Amount"
        };
        WriteHeader(b2b, head);
        var r = 2;
        foreach (var inv in bills)
        foreach (var s in inv.GstSummary)
        {
            b2b.Cell(r, 1).Value = inv.BillTo.Gstin;
            b2b.Cell(r, 2).Value = inv.BillTo.Name;
            b2b.Cell(r, 3).Value = inv.InvoiceNumber;
            b2b.Cell(r, 4).Value = inv.InvoiceDate.ToString("dd-MMM-yyyy", Inv);
            b2b.Cell(r, 5).Value = (double)inv.Totals.GrandTotal;
            b2b.Cell(r, 6).Value = $"{inv.PlaceOfSupplyStateCode}-{inv.PlaceOfSupplyState}";
            b2b.Cell(r, 7).Value = "N";
            b2b.Cell(r, 8).Value = "";
            b2b.Cell(r, 9).Value = "Regular B2B";
            b2b.Cell(r, 10).Value = "";
            b2b.Cell(r, 11).Value = (double)s.GstRate;
            b2b.Cell(r, 12).Value = (double)s.TaxableValue;
            b2b.Cell(r, 13).Value = 0;
            r++;
        }
        Money(b2b, 5); Money(b2b, 12);
        b2b.Columns().AdjustToContents();

        var hsn = wb.Worksheets.Add("HSN Summary");
        WriteHeader(hsn, new[]
        {
            "HSN", "Description", "UQC", "Total Quantity", "Total Value", "Rate", "Taxable Value",
            "Integrated Tax Amount", "Central Tax Amount", "State/UT Tax Amount", "Cess Amount"
        });
        r = 2;
        var groups = bills.SelectMany(b => b.Lines).GroupBy(l => new { l.HsnCode, l.GstRate }).OrderBy(g => g.Key.HsnCode);
        foreach (var g in groups)
        {
            hsn.Cell(r, 1).Value = g.Key.HsnCode;
            hsn.Cell(r, 2).Value = string.Join(", ", g.Select(l => l.Name).Distinct());
            hsn.Cell(r, 3).Value = Uqc(g.First().Unit);
            hsn.Cell(r, 4).Value = (double)g.Sum(l => l.Quantity);
            hsn.Cell(r, 5).Value = (double)g.Sum(l => l.LineTotal);
            hsn.Cell(r, 6).Value = (double)g.Key.GstRate;
            hsn.Cell(r, 7).Value = (double)g.Sum(l => l.TaxableValue);
            hsn.Cell(r, 8).Value = (double)g.Sum(l => l.Igst);
            hsn.Cell(r, 9).Value = (double)g.Sum(l => l.Cgst);
            hsn.Cell(r, 10).Value = (double)g.Sum(l => l.Sgst);
            hsn.Cell(r, 11).Value = 0;
            r++;
        }
        foreach (var c in new[] { 5, 7, 8, 9, 10 }) Money(hsn, c);
        hsn.Columns().AdjustToContents();

        return Save(wb);
    }

    public async Task<byte[]> SalesAsync(string businessId, DateTime from, DateTime to)
    {
        var start = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(to.Date, DateTimeKind.Utc).AddDays(1);
        var bills = await db.Invoices.Find(x => x.BusinessId == businessId && x.InvoiceDate >= start && x.InvoiceDate < end)
            .SortBy(x => x.InvoiceDate).ThenBy(x => x.Seq).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sales");
        WriteHeader(ws, new[]
        {
            "Invoice No.", "Date", "Client", "Client GSTIN", "Place of Supply", "Taxable", "CGST", "SGST", "IGST",
            "Round Off", "Grand Total", "Paid", "Balance", "Status", "Payment Status", "Bill Type"
        });
        var r = 2;
        foreach (var b in bills)
        {
            ws.Cell(r, 1).Value = b.InvoiceNumber;
            ws.Cell(r, 2).Value = b.InvoiceDate.ToString("dd-MM-yyyy", Inv);
            ws.Cell(r, 3).Value = b.BillTo.Name;
            ws.Cell(r, 4).Value = b.BillTo.Gstin;
            ws.Cell(r, 5).Value = b.PlaceOfSupplyState;
            ws.Cell(r, 6).Value = (double)b.Totals.TaxableTotal;
            ws.Cell(r, 7).Value = (double)b.Totals.CgstTotal;
            ws.Cell(r, 8).Value = (double)b.Totals.SgstTotal;
            ws.Cell(r, 9).Value = (double)b.Totals.IgstTotal;
            ws.Cell(r, 10).Value = (double)b.Totals.RoundOff;
            ws.Cell(r, 11).Value = (double)b.Totals.GrandTotal;
            ws.Cell(r, 12).Value = (double)b.AmountPaid;
            ws.Cell(r, 13).Value = (double)b.BalanceDue;
            ws.Cell(r, 14).Value = b.Status.ToString();
            ws.Cell(r, 15).Value = b.Status == InvoiceStatus.Cancelled ? "-" : b.PaymentStatus.ToString();
            ws.Cell(r, 16).Value = b.IsNonGst ? "Non-GST" : "GST";
            r++;
        }
        for (var c = 6; c <= 13; c++) Money(ws, c);
        ws.Columns().AdjustToContents();
        return Save(wb);
    }

    /// <summary>All final bills of one client in a date range, with totals. city: only that city (empty = all).
    /// When the client has bills in several cities and no city is chosen, a second sheet totals each city.</summary>
    /// <summary>A client's finalized bills in a date range (optionally one city), oldest first.</summary>
    private async Task<(Client Client, List<Invoice> Bills, string City)> ClientBillsAsync(
        string businessId, string clientId, DateTime from, DateTime to, string? city)
    {
        var client = await db.Clients.Find(c => c.BusinessId == businessId && c.Id == clientId).FirstOrDefaultAsync()
                     ?? throw new Infrastructure.NotFoundException("Client");
        var start = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(to.Date, DateTimeKind.Utc).AddDays(1);
        var bills = await db.Invoices.Find(x => x.BusinessId == businessId && x.ClientId == clientId && x.Status == InvoiceStatus.Final
                                                 && x.InvoiceDate >= start && x.InvoiceDate < end)
            .SortBy(x => x.InvoiceDate).ThenBy(x => x.Seq).ToListAsync();
        city = (city ?? "").Trim();
        if (city.Length > 0) bills = bills.Where(b => b.BillTo.Address.City.Equals(city, StringComparison.OrdinalIgnoreCase)).ToList();
        return (client, bills, city);
    }

    /// <summary>The same bills as a printable PDF statement of account.</summary>
    public async Task<(byte[] Bytes, string ClientName)> ClientStatementAsync(string businessId, string clientId, DateTime from, DateTime to, string? city)
    {
        var (client, bills, cityName) = await ClientBillsAsync(businessId, clientId, from, to, city);
        var business = await db.Businesses.Find(b => b.Id == businessId).FirstOrDefaultAsync() ?? throw new Infrastructure.NotFoundException("Business");
        var doc = new Pdf.StatementDocument(business, client, cityName, from.Date, to.Date, bills);
        return (doc.GeneratePdf(), client.Name);
    }

    public async Task<(byte[] Bytes, string ClientName)> ClientAsync(string businessId, string clientId, DateTime from, DateTime to, string? city)
    {
        var (client, bills, cityName) = await ClientBillsAsync(businessId, clientId, from, to, city);
        city = cityName;

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Bills");
        WriteHeader(ws, new[]
        {
            "Bill No.", "Date", "City", "Type", "Taxable / Total", "CGST", "SGST", "IGST", "Grand Total", "Paid", "Balance", "Payment Status"
        });
        var r = 2;
        foreach (var b in bills)
        {
            ws.Cell(r, 1).Value = b.InvoiceNumber;
            ws.Cell(r, 2).Value = b.InvoiceDate.ToString("dd-MM-yyyy", Inv);
            ws.Cell(r, 3).Value = b.BillTo.Address.City;
            ws.Cell(r, 4).Value = b.IsNonGst ? "Non-GST" : "GST";
            ws.Cell(r, 5).Value = (double)b.Totals.TaxableTotal;
            ws.Cell(r, 6).Value = (double)b.Totals.CgstTotal;
            ws.Cell(r, 7).Value = (double)b.Totals.SgstTotal;
            ws.Cell(r, 8).Value = (double)b.Totals.IgstTotal;
            ws.Cell(r, 9).Value = (double)b.Totals.GrandTotal;
            ws.Cell(r, 10).Value = (double)b.AmountPaid;
            ws.Cell(r, 11).Value = (double)b.BalanceDue;
            ws.Cell(r, 12).Value = b.PaymentStatus.ToString();
            r++;
        }
        ws.Cell(r, 1).Value = $"Total ({bills.Count} bill{(bills.Count == 1 ? "" : "s")})";
        for (var c = 5; c <= 11; c++) ws.Cell(r, c).FormulaA1 = $"SUM({ws.Cell(2, c).Address}:{ws.Cell(Math.Max(r - 1, 2), c).Address})";
        ws.Row(r).Style.Font.Bold = true;
        ws.Row(r).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        for (var c = 5; c <= 11; c++) Money(ws, c);
        ws.Columns().AdjustToContents();

        var cities = bills.Select(b => b.BillTo.Address.City).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (city.Length == 0 && cities.Count > 1)
        {
            var wc = wb.Worksheets.Add("By city");
            WriteHeader(wc, new[] { "City", "Bills", "Grand Total", "Paid", "Balance" });
            var cr = 2;
            foreach (var g in bills.GroupBy(b => b.BillTo.Address.City, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key))
            {
                wc.Cell(cr, 1).Value = string.IsNullOrEmpty(g.Key) ? "(no city)" : g.Key;
                wc.Cell(cr, 2).Value = g.Count();
                wc.Cell(cr, 3).Value = (double)g.Sum(b => b.Totals.GrandTotal);
                wc.Cell(cr, 4).Value = (double)g.Sum(b => b.AmountPaid);
                wc.Cell(cr, 5).Value = (double)g.Sum(b => b.BalanceDue);
                cr++;
            }
            wc.Cell(cr, 1).Value = "Total";
            wc.Cell(cr, 2).Value = bills.Count;
            wc.Cell(cr, 3).Value = (double)bills.Sum(b => b.Totals.GrandTotal);
            wc.Cell(cr, 4).Value = (double)bills.Sum(b => b.AmountPaid);
            wc.Cell(cr, 5).Value = (double)bills.Sum(b => b.BalanceDue);
            wc.Row(cr).Style.Font.Bold = true;
            wc.Row(cr).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            for (var c = 3; c <= 5; c++) Money(wc, c);
            wc.Columns().AdjustToContents();
        }
        return (Save(wb), client.Name);
    }

    /// <summary>Items as Excel, one sheet per type. typeId: empty = every type, "none" = items with no type, or one type's id.</summary>
    public async Task<(byte[] Bytes, string Label)> ItemsAsync(string businessId, string? typeId)
    {
        var types = await db.ProductTypes.Find(t => t.BusinessId == businessId).SortBy(t => t.Name).ToListAsync();
        var items = await db.Items.Find(i => i.BusinessId == businessId).ToListAsync();
        var label = "All";
        if (!string.IsNullOrEmpty(typeId))
        {
            if (typeId == "none") { items = items.Where(i => string.IsNullOrEmpty(i.TypeId)).ToList(); label = "No-type"; }
            else { items = items.Where(i => i.TypeId == typeId).ToList(); label = types.FirstOrDefault(t => t.Id == typeId)?.Name ?? "Type"; }
        }

        var groups = new List<(string Name, List<Item> Items)>();
        foreach (var t in types)
        {
            var list = items.Where(i => i.TypeId == t.Id).ToList();
            if (list.Count > 0) groups.Add((t.Name, list));
        }
        var untyped = items.Where(i => string.IsNullOrEmpty(i.TypeId) || types.All(t => t.Id != i.TypeId)).ToList();
        if (untyped.Count > 0) groups.Add(("No type", untyped));
        if (groups.Count == 0) groups.Add(("Items", new List<Item>()));

        using var wb = new XLWorkbook();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, list) in groups)
        {
            var safe = new string(name.Select(c => "[]:*?/\\".Contains(c) ? '-' : c).ToArray()).Trim();
            if (safe.Length == 0) safe = "Items";
            if (safe.Length > 28) safe = safe[..28];
            var sheetName = safe;
            for (var n = 2; !usedNames.Add(sheetName); n++) sheetName = $"{safe} {n}";

            var ws = wb.Worksheets.Add(sheetName);
            WriteHeader(ws, new[] { "Item", "Variant", "Cloth", "Colour", "Size", "Unit", "GST %", "Default Rate", "Clients with special rate", "Status" });
            var r = 2;
            foreach (var i in list.OrderBy(x => x.Name).ThenBy(x => x.SizeOrVariant))
            {
                var typed = !string.IsNullOrEmpty(i.TypeId);
                ws.Cell(r, 1).Value = i.Name;
                ws.Cell(r, 2).Value = typed ? i.Variant : "";
                ws.Cell(r, 3).Value = typed ? i.Cloth : "";
                ws.Cell(r, 4).Value = typed ? i.Colour : "";
                ws.Cell(r, 5).Value = typed ? i.Size : i.SizeOrVariant;
                ws.Cell(r, 6).Value = i.Unit;
                ws.Cell(r, 7).Value = (double)i.GstRate;
                ws.Cell(r, 8).Value = (double)i.DefaultRate;
                ws.Cell(r, 9).Value = i.SpecialRates.Sum(s => s.ClientIds.Count);
                ws.Cell(r, 10).Value = i.IsActive ? "Active" : "Hidden";
                r++;
            }
            Money(ws, 8);
            ws.Columns().AdjustToContents();
        }
        return (Save(wb), label);
    }

    private static void WriteHeader(IXLWorksheet ws, string[] head)
    {
        for (var i = 0; i < head.Length; i++) ws.Cell(1, i + 1).Value = head[i];
        var row = ws.Row(1);
        row.Style.Font.Bold = true;
        row.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
        ws.SheetView.FreezeRows(1);
    }

    private static void Money(IXLWorksheet ws, int col) => ws.Column(col).Style.NumberFormat.Format = "#,##0.00";

    private static string Uqc(string unit) => unit.ToUpperInvariant() switch
    {
        "PCS" or "PC" or "PIECE" or "PIECES" => "PCS-PIECES",
        "NOS" or "NO" => "NOS-NUMBERS",
        "SET" or "SETS" => "SET-SETS",
        "KG" or "KGS" => "KGS-KILOGRAMS",
        "MTR" or "M" => "MTR-METERS",
        _ => "OTH-OTHERS"
    };

    private static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
