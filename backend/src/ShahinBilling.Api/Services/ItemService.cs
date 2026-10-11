using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

public class ItemService(MongoContext db)
{
    public async Task<List<Item>> ListAsync(string businessId, string? search, bool? active)
    {
        var f = Builders<Item>.Filter;
        var filter = f.Eq(x => x.BusinessId, businessId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var rx = new BsonRegularExpression(Regex.Escape(search.Trim()), "i");
            filter &= f.Or(f.Regex(x => x.Name, rx), f.Regex(x => x.SizeOrVariant, rx), f.Regex(x => x.TypeName, rx));
        }
        if (active.HasValue) filter &= f.Eq(x => x.IsActive, active.Value);
        return await db.Items.Find(filter).SortBy(x => x.Name).ThenBy(x => x.SizeOrVariant).ToListAsync();
    }

    public async Task<Item> GetAsync(string businessId, string id) =>
        await db.Items.Find(x => x.BusinessId == businessId && x.Id == id).FirstOrDefaultAsync()
        ?? throw new NotFoundException("Item");

    public async Task<Item> CreateAsync(string businessId, Item item)
    {
        item.Id = ObjectId.GenerateNewId().ToString();
        item.BusinessId = businessId;
        item.CreatedAt = item.UpdatedAt = DateTime.UtcNow;
        await Normalize(businessId, item);
        await db.Items.InsertOneAsync(item);
        return item;
    }

    public async Task<Item> UpdateAsync(string businessId, string id, Item item)
    {
        var existing = await GetAsync(businessId, id);
        item.Id = id;
        item.BusinessId = businessId;
        item.CreatedAt = existing.CreatedAt;
        item.UpdatedAt = DateTime.UtcNow;
        await Normalize(businessId, item);
        await db.Items.ReplaceOneAsync(x => x.Id == id && x.BusinessId == businessId, item);
        return item;
    }

    /// <summary>
    /// Creates one item for every row (variant + size + rate) and every chosen colour of one type and cloth.
    /// Combinations that already exist are skipped, so running it again never duplicates. Rows with no rate are ignored.
    /// </summary>
    public async Task<GenerateItemsResult> GenerateAsync(string businessId, GenerateItemsRequest req)
    {
        var type = await db.ProductTypes.Find(t => t.BusinessId == businessId && t.Id == req.TypeId).FirstOrDefaultAsync()
                   ?? throw new AppException("That type no longer exists. Pick the type again.");

        var cloth = type.ClothTypes.Count > 0 ? ImportService.Pick(req.Cloth ?? "", type.ClothTypes, "cloth", type.Name, "") : "";
        var coloursOfCloth = type.ClothColours.FirstOrDefault(c => c.Cloth.Equals(cloth, StringComparison.OrdinalIgnoreCase))?.Colours ?? new();
        var colours = coloursOfCloth.Count == 0
            ? new List<string> { "" }
            : (req.Colours ?? new()).Select(c => ImportService.Pick(c, coloursOfCloth, "colour", type.Name, "")).Where(c => c.Length > 0).Distinct().ToList();
        if (colours.Count == 0) throw new AppException($"Choose at least one colour of {cloth}.");

        var rows = (req.Rows ?? new()).Where(r => r.Rate.HasValue).ToList();
        if (rows.Count == 0) throw new AppException("Enter a rate for at least one row.");
        if (rows.Count * colours.Count > 500) throw new AppException("That would create more than 500 items at once. Do it in smaller steps.");

        var existing = await db.Items.Find(i => i.BusinessId == businessId && i.TypeId == type.Id).ToListAsync();
        static string Key(string v, string c, string col, string s) => $"{v}|{c}|{col}|{s}".ToLowerInvariant();
        var seen = new HashSet<string>(existing.Select(i => Key(i.Variant, i.Cloth, i.Colour, i.Size)));

        int created = 0, skipped = 0;
        foreach (var colour in colours)
            foreach (var row in rows)
            {
                if (row.Rate!.Value < 0) throw new AppException("Rate can't be negative.");
                var variant = type.UsesVariants ? ImportService.Pick(row.Variant ?? "", type.Variants, "variant", type.Name, "") : "";
                var size = ImportService.Pick(row.Size ?? "", type.Sizes.Count > 0 ? type.Sizes : null, "size", type.Name, $"{type.Name} has no sizes.");
                if (!seen.Add(Key(variant, cloth, colour, size))) { skipped++; continue; }
                await CreateAsync(businessId, new Item
                {
                    TypeId = type.Id, Variant = variant, Cloth = cloth, Colour = colour, Size = size,
                    Unit = string.IsNullOrWhiteSpace(req.Unit) ? "PCS" : req.Unit.Trim(), GstRate = req.GstRate, DefaultRate = row.Rate.Value,
                });
                created++;
            }
        return new GenerateItemsResult(created, skipped);
    }

    /// <summary>Deletes an item that no bill uses. Bills keep a copy of the item, but a used item is kept so each bill's history stays complete; hide it instead.</summary>
    public async Task DeleteAsync(string businessId, string id)
    {
        await GetAsync(businessId, id);
        var used = await db.Invoices.CountDocumentsAsync(i => i.BusinessId == businessId && i.Lines.Any(l => l.ItemId == id));
        if (used > 0)
            throw new AppException($"This item is on {used} bill{(used == 1 ? "" : "s")}, so it can't be deleted. Use the eye icon to hide it from billing instead.");
        await db.Items.DeleteOneAsync(x => x.Id == id && x.BusinessId == businessId);
    }

    public async Task SetActiveAsync(string businessId, string id, bool active)
    {
        var r = await db.Items.UpdateOneAsync(x => x.Id == id && x.BusinessId == businessId,
            Builders<Item>.Update.Set(x => x.IsActive, active).Set(x => x.UpdatedAt, DateTime.UtcNow));
        if (r.MatchedCount == 0) throw new NotFoundException("Item");
    }

    public async Task<RateResponse> RateForClientAsync(string businessId, string id, string? clientId)
    {
        var item = await GetAsync(businessId, id);
        var (rate, special) = RateResolver.Resolve(item, clientId);
        return new RateResponse(rate, special);
    }

    /// <summary>The text shown after the product name on bills and orders: variant, cloth, colour and size.</summary>
    public static string ComposeLabel(string variant, string cloth, string colour, string size) =>
        string.Join(" \u00b7 ", new[] { variant, cloth, colour, size }.Where(x => !string.IsNullOrEmpty(x)));

    /// <summary>Removes empty groups, unknown clients; enforces "a client in at most one group".</summary>
    private async Task Normalize(string businessId, Item item)
    {
        // Items picked from Item setup carry their type; the text shown on bills is built from variant + size
        if (!string.IsNullOrWhiteSpace(item.TypeId))
        {
            var type = await db.ProductTypes.Find(t => t.BusinessId == businessId && t.Id == item.TypeId).FirstOrDefaultAsync()
                       ?? throw new AppException("That type no longer exists. Pick the type again.");
            item.TypeName = type.Name;
            item.Name = type.Name;   // the item is called after its type (Bed, Pillow...); variant and size tell them apart
            item.Size = TextClean.Plain(item.Size);
            item.Variant = type.UsesVariants ? TextClean.Plain(item.Variant) : "";
            item.Cloth = type.ClothTypes.Count > 0 ? TextClean.Plain(item.Cloth) : "";
            var colours = type.ClothColours.FirstOrDefault(c => c.Cloth.Equals(item.Cloth, StringComparison.OrdinalIgnoreCase))?.Colours;
            item.Colour = item.Cloth.Length > 0 && colours is { Count: > 0 } ? TextClean.Plain(item.Colour) : "";
            item.SizeOrVariant = ComposeLabel(item.Variant, item.Cloth, item.Colour, item.Size);
        }
        else { item.TypeId = ""; item.TypeName = ""; }

        foreach (var s in item.SpecialRates)
        {
            if (string.IsNullOrEmpty(s.Id)) s.Id = ObjectId.GenerateNewId().ToString();
            s.ClientIds = s.ClientIds.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        }
        item.SpecialRates = item.SpecialRates.Where(s => s.ClientIds.Count > 0).ToList();

        var dupes = RateResolver.FindDuplicateClients(item);
        if (dupes.Count > 0)
        {
            var names = await db.Clients.Find(c => c.BusinessId == businessId && dupes.Contains(c.Id))
                .Project(c => c.Name).ToListAsync();
            throw new AppException(
                $"A client can have only one special rate per item. Remove the duplicate for: {string.Join(", ", names)}.");
        }

        var allIds = item.SpecialRates.SelectMany(s => s.ClientIds).Distinct().ToList();
        if (allIds.Count > 0)
        {
            var known = await db.Clients.Find(c => c.BusinessId == businessId && allIds.Contains(c.Id))
                .Project(c => c.Id).ToListAsync();
            foreach (var s in item.SpecialRates) s.ClientIds = s.ClientIds.Where(known.Contains).ToList();
        }
    }
}
