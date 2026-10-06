using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

public class InvoiceService(MongoContext db, BusinessService businesses, InvoiceNumberService numbers)
{
    // ---------- Queries ----------

    /// <summary>What the next new bill will be numbered (shown while creating; final only once saved).</summary>
    public async Task<string> NextNumberAsync(string businessId, bool nonGst = false)
    {
        var business = await businesses.GetAsync(businessId);
        return InvoiceNumberService.Format(business.InvoiceNumbering, await numbers.PeekNextSeqAsync(businessId, nonGst), nonGst);
    }

    public async Task<PagedResult<InvoiceListItem>> ListAsync(string businessId, InvoiceQuery q)
    {
        var filter = BuildFilter(businessId, q);
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var total = await db.Invoices.CountDocumentsAsync(filter);
        var docs = await db.Invoices.Find(filter)
            .SortByDescending(x => x.InvoiceDate).ThenByDescending(x => x.Seq)
            .Skip((page - 1) * size).Limit(size).ToListAsync();
        return new PagedResult<InvoiceListItem>(docs.Select(ToListItem).ToList(), total, page, size);
    }

    public static FilterDefinition<Invoice> BuildFilter(string businessId, InvoiceQuery q)
    {
        var f = Builders<Invoice>.Filter;
        var filter = f.Eq(x => x.BusinessId, businessId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var rx = new BsonRegularExpression(Regex.Escape(q.Search.Trim()), "i");
            filter &= f.Or(f.Regex(x => x.InvoiceNumber, rx), f.Regex(x => x.BillTo.Name, rx));
        }
        if (!string.IsNullOrWhiteSpace(q.ClientId)) filter &= f.Eq(x => x.ClientId, q.ClientId);
        if (q.From.HasValue) filter &= f.Gte(x => x.InvoiceDate, Money.AsUtcDate(q.From.Value));
        if (q.To.HasValue) filter &= f.Lt(x => x.InvoiceDate, Money.AsUtcDate(q.To.Value).AddDays(1));
        if (q.Status.HasValue) filter &= f.Eq(x => x.Status, q.Status.Value);
        if (q.PaymentStatus.HasValue)
            filter &= f.Eq(x => x.PaymentStatus, q.PaymentStatus.Value) & f.Ne(x => x.Status, InvoiceStatus.Cancelled);
        return filter;
    }

    public static InvoiceListItem ToListItem(Invoice x) => new(
        x.Id, x.InvoiceNumber, x.InvoiceDate, x.ClientId, x.BillTo.Name,
        x.Totals.GrandTotal, x.AmountPaid, x.BalanceDue, x.Status, x.PaymentStatus, x.IsNonGst);

    public async Task<Invoice> GetAsync(string businessId, string id) =>
        await db.Invoices.Find(x => x.BusinessId == businessId && x.Id == id).FirstOrDefaultAsync()
        ?? throw new NotFoundException("Bill");

    // ---------- Commands ----------

    /// <summary>Builds and calculates a bill without saving (used for previews and by Create/Update).</summary>
    public async Task<Invoice> BuildAsync(string businessId, InvoiceRequest req, Invoice? existing = null)
    {
        var business = await businesses.GetAsync(businessId);
        var client = await db.Clients.Find(c => c.BusinessId == businessId && c.Id == req.ClientId).FirstOrDefaultAsync()
                     ?? throw new AppException("Select a client for this bill.");

        var itemIds = req.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = (await db.Items.Find(i => i.BusinessId == businessId && itemIds.Contains(i.Id)).ToListAsync())
            .ToDictionary(i => i.Id);

        var inv = existing ?? new Invoice { BusinessId = businessId };
        inv.InvoiceDate = Money.AsUtcDate(req.InvoiceDate == default ? DateTime.UtcNow : req.InvoiceDate);
        inv.BusinessSnapshot = BusinessSnapshot.From(business);

        inv.ClientId = client.Id;
        inv.BillTo = new PartySnapshot
        {
            Name = client.Name, Gstin = client.Gstin, ContactPerson = client.ContactPerson,
            Phone = client.Phone, Address = client.BillingAddress.Clone()
        };
        // City: one of the client's cities; only the city changes, everything else stays the same
        var city = (req.City ?? "").Trim();
        if (client.Cities.Count > 0)
        {
            var chosen = city.Length == 0 ? client.Cities[0]
                : client.Cities.FirstOrDefault(x => x.Equals(city, StringComparison.OrdinalIgnoreCase))
                  ?? throw new AppException($"“{city}” is not a city of {client.Name}. Add it on the client first.");
            inv.BillTo.Address.City = chosen;
            inv.City = chosen;
        }
        else inv.City = inv.BillTo.Address.City;

        inv.ShipToSameAsBillTo = req.ShipToSameAsBillTo;
        if (req.ShipToSameAsBillTo)
        {
            inv.ShipTo = new PartySnapshot
            {
                Name = inv.BillTo.Name, Gstin = inv.BillTo.Gstin, ContactPerson = inv.BillTo.ContactPerson,
                Phone = inv.BillTo.Phone, Address = inv.BillTo.Address.Clone()
            };
        }
        else
        {
            inv.ShipTo = req.ShipTo ?? new PartySnapshot
            {
                Name = client.Name, Gstin = client.Gstin, ContactPerson = client.ContactPerson,
                Phone = client.Phone, Address = client.ShippingAddress.Clone()
            };
            inv.ShipTo.Address.State = StateCodes.NameOf(inv.ShipTo.Address.StateCode);
        }

        // Bills show a client by name and city only: drop any old address lines
        inv.ShipTo.Name = client.Name;
        inv.ShipTo.Phone = client.Phone;   // same client, so the same mobile number
        foreach (var a in new[] { inv.BillTo.Address, inv.ShipTo.Address }) { a.Line1 = ""; a.Line2 = ""; a.Pincode = ""; }
        if (string.IsNullOrWhiteSpace(inv.ShipTo.Address.City)) inv.ShipTo.Address.City = inv.BillTo.Address.City;

        // Rule 4.2: place of supply defaults to the Bill To state
        var pos = !string.IsNullOrEmpty(req.PlaceOfSupplyStateCode) ? req.PlaceOfSupplyStateCode : inv.BillTo.Address.StateCode;
        inv.PlaceOfSupplyStateCode = pos;
        inv.PlaceOfSupplyState = StateCodes.NameOf(pos);
        // A bill's type is fixed when it is first saved (its number comes from that series)
        inv.IsNonGst = existing?.IsNonGst ?? req.NonGst;
        inv.IsInterState = !inv.IsNonGst && InvoiceCalculator.IsInterState(BusinessService.StateCodeOf(business), pos);

        inv.PoNumber = req.PoNumber.Trim();
        inv.PoDate = Money.AsUtcDate(req.PoDate);
        inv.Transport = req.Transport ?? new TransportDetails();
        inv.Transport.VehicleNumber = (inv.Transport.VehicleNumber ?? "").Trim().ToUpperInvariant();
        inv.Transport.DeliveryDate = Money.AsUtcDate(inv.Transport.DeliveryDate);
        inv.Notes = req.Notes;

        var lines = new List<InvoiceLine>();
        foreach (var l in req.Lines)
        {
            if (!items.TryGetValue(l.ItemId, out var item))
                throw new AppException("One of the selected items no longer exists. Remove it and pick again.");
            var (resolved, isSpecial) = RateResolver.Resolve(item, client.Id);
            var rate = l.Rate ?? resolved;
            lines.Add(new InvoiceLine
            {
                ItemId = item.Id, Name = item.Name, SizeOrVariant = item.SizeOrVariant, HsnCode = item.HsnCode,
                Unit = item.Unit, GstRate = inv.IsNonGst ? 0 : item.GstRate, Quantity = l.Quantity, Rate = rate,
                IsSpecialRate = isSpecial && rate == resolved, Discount = l.Discount
            });
        }

        var calc = InvoiceCalculator.Calculate(lines, inv.IsInterState);
        inv.Lines = calc.Lines;
        inv.Totals = calc.Totals;
        inv.GstSummary = inv.IsNonGst ? new() : calc.Summary;
        inv.AmountInWords = calc.AmountInWords;
        InvoiceCalculator.ApplyPayments(inv);
        inv.UpdatedAt = DateTime.UtcNow;
        return inv;
    }

    public async Task<Invoice> CreateAsync(string businessId, InvoiceRequest req)
    {
        var inv = await BuildAsync(businessId, req);
        var business = await businesses.GetAsync(businessId);
        inv.Id = ObjectId.GenerateNewId().ToString();
        inv.CreatedAt = DateTime.UtcNow;
        inv.FinancialYear = FinancialYear.Label(inv.InvoiceDate);
        inv.Seq = await numbers.NextSeqAsync(businessId, inv.IsNonGst);
        inv.InvoiceNumber = InvoiceNumberService.Format(business.InvoiceNumbering, inv.Seq, inv.IsNonGst);
        inv.Status = req.Finalize ? InvoiceStatus.Final : InvoiceStatus.Draft;
        EnsureFinalizable(inv);
        await db.Invoices.InsertOneAsync(inv);
        return inv;
    }

    public async Task<Invoice> UpdateAsync(string businessId, string id, InvoiceRequest req)
    {
        var existing = await GetAsync(businessId, id);
        if (existing.Status == InvoiceStatus.Cancelled) throw new AppException("A cancelled bill can't be edited.");
        var inv = await BuildAsync(businessId, req, existing);   // number, FY and seq stay the same (rule 4.4)
        if (req.Finalize) inv.Status = InvoiceStatus.Final;
        EnsureFinalizable(inv);
        await db.Invoices.ReplaceOneAsync(x => x.Id == id && x.BusinessId == businessId, inv);
        return inv;
    }

    public async Task<Invoice> FinalizeAsync(string businessId, string id)
    {
        var inv = await GetAsync(businessId, id);
        if (inv.Status == InvoiceStatus.Cancelled) throw new AppException("A cancelled bill can't be finalized.");
        inv.Status = InvoiceStatus.Final;
        EnsureFinalizable(inv);
        inv.UpdatedAt = DateTime.UtcNow;
        await db.Invoices.ReplaceOneAsync(x => x.Id == id && x.BusinessId == businessId, inv);
        return inv;
    }

    public async Task<Invoice> CancelAsync(string businessId, string id, string reason)
    {
        var inv = await GetAsync(businessId, id);
        if (inv.Status == InvoiceStatus.Cancelled) return inv;
        inv.Status = InvoiceStatus.Cancelled;
        inv.CancelReason = reason.Trim();
        inv.CancelledAt = DateTime.UtcNow;
        inv.UpdatedAt = DateTime.UtcNow;
        await db.Invoices.ReplaceOneAsync(x => x.Id == id, inv);
        return inv;
    }

    /// <summary>Creates a new draft with the same client and items. Rates are re-resolved from today's masters.</summary>
    public async Task<Invoice> DuplicateAsync(string businessId, string id)
    {
        var src = await GetAsync(businessId, id);
        var req = new InvoiceRequest
        {
            InvoiceDate = DateTime.UtcNow,
            ClientId = src.ClientId,
            City = src.City,
            NonGst = src.IsNonGst,
            ShipToSameAsBillTo = src.ShipToSameAsBillTo,
            ShipTo = src.ShipToSameAsBillTo ? null : src.ShipTo,
            PlaceOfSupplyStateCode = src.PlaceOfSupplyStateCode,
            Notes = src.Notes,
            Lines = src.Lines.Select(l => new InvoiceLineRequest
                { ItemId = l.ItemId, Quantity = l.Quantity, Discount = l.Discount, Rate = null }).ToList(),
            Finalize = false
        };
        return await CreateAsync(businessId, req);
    }

    public async Task<Invoice> AddPaymentAsync(string businessId, string id, PaymentRequest p)
    {
        var inv = await GetAsync(businessId, id);
        if (inv.Status == InvoiceStatus.Cancelled) throw new AppException("Payments can't be added to a cancelled bill.");
        inv.Payments.Add(new Payment
        {
            Date = Money.AsUtcDate(p.Date == default ? DateTime.UtcNow : p.Date),
            Amount = Money.Round2(p.Amount), Mode = p.Mode, Reference = p.Reference, Note = p.Note
        });
        InvoiceCalculator.ApplyPayments(inv);
        inv.UpdatedAt = DateTime.UtcNow;
        await db.Invoices.ReplaceOneAsync(x => x.Id == id, inv);
        return inv;
    }

    public async Task<Invoice> RemovePaymentAsync(string businessId, string id, string paymentId)
    {
        var inv = await GetAsync(businessId, id);
        inv.Payments.RemoveAll(p => p.Id == paymentId);
        InvoiceCalculator.ApplyPayments(inv);
        inv.UpdatedAt = DateTime.UtcNow;
        await db.Invoices.ReplaceOneAsync(x => x.Id == id, inv);
        return inv;
    }

    private static void EnsureFinalizable(Invoice inv)
    {
        if (inv.Status == InvoiceStatus.Final && inv.Lines.Count == 0)
            throw new AppException("Add at least one item before finalizing the bill.");
    }
}
