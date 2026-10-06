using MongoDB.Bson;
using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

public class ProductTypeService(MongoContext db)
{
    public async Task<List<ProductType>> ListAsync(string businessId) =>
        await db.ProductTypes.Find(x => x.BusinessId == businessId).SortBy(x => x.Name).ToListAsync();

    public async Task<ProductType> GetAsync(string businessId, string id) =>
        await db.ProductTypes.Find(x => x.BusinessId == businessId && x.Id == id).FirstOrDefaultAsync()
        ?? throw new NotFoundException("Type");

    public async Task<ProductType> CreateAsync(string businessId, ProductType t)
    {
        t.Id = ObjectId.GenerateNewId().ToString();
        t.BusinessId = businessId;
        t.CreatedAt = t.UpdatedAt = DateTime.UtcNow;
        await Normalize(businessId, t);
        await db.ProductTypes.InsertOneAsync(t);
        return t;
    }

    public async Task<ProductType> UpdateAsync(string businessId, string id, ProductType t)
    {
        var existing = await GetAsync(businessId, id);
        t.Id = id;
        t.BusinessId = businessId;
        t.CreatedAt = existing.CreatedAt;
        t.UpdatedAt = DateTime.UtcNow;
        await Normalize(businessId, t);
        await db.ProductTypes.ReplaceOneAsync(x => x.Id == id && x.BusinessId == businessId, t);

        // Keep the type name on its items in step if it was renamed
        if (!string.Equals(existing.Name, t.Name, StringComparison.Ordinal))
            await db.Items.UpdateManyAsync(i => i.BusinessId == businessId && i.TypeId == id,
                Builders<Item>.Update.Set(i => i.TypeName, t.Name));
        return t;
    }

    public async Task DeleteAsync(string businessId, string id)
    {
        await GetAsync(businessId, id);
        var used = await db.Items.CountDocumentsAsync(i => i.BusinessId == businessId && i.TypeId == id);
        if (used > 0)
            throw new AppException($"{used} item{(used == 1 ? " uses" : "s use")} this type. Change or hide {(used == 1 ? "it" : "them")} first.");
        await db.ProductTypes.DeleteOneAsync(x => x.Id == id && x.BusinessId == businessId);
    }

    private async Task Normalize(string businessId, ProductType t)
    {
        t.Name = t.Name.Trim();
        t.Sizes = Clean(t.Sizes);
        t.ClothTypes = Clean(t.ClothTypes);
        t.ClothColours = CleanColourSets(t.ClothTypes, t.ClothColours);
        t.Variants = t.UsesVariants ? Clean(t.Variants) : new();

        var clash = await db.ProductTypes.Find(x => x.BusinessId == businessId && x.Id != t.Id).Project(x => x.Name).ToListAsync();
        if (clash.Any(n => n.Equals(t.Name, StringComparison.OrdinalIgnoreCase)))
            throw new AppException($"There is already a type called \u201c{t.Name}\u201d.");
    }

    /// <summary>Keeps colours only for cloths the type really has (using the cloth's own spelling), trims and de-duplicates them, and drops empty ones.</summary>
    public static List<ClothColourSet> CleanColourSets(List<string> cloths, List<ClothColourSet>? sets)
    {
        var result = new List<ClothColourSet>();
        foreach (var set in sets ?? new())
        {
            var cloth = cloths.FirstOrDefault(c => c.Equals((set.Cloth ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
            if (cloth == null || result.Any(r => r.Cloth == cloth)) continue;
            var colours = Clean(set.Colours);
            if (colours.Count > 0) result.Add(new ClothColourSet { Cloth = cloth, Colours = colours });
        }
        return result;
    }

    private static List<string> Clean(List<string>? list) =>
        (list ?? new()).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
