using MongoDB.Bson;
using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

public class BillFormatService(MongoContext db, BusinessService businesses)
{
    /// <summary>First time only: the format already in use becomes the first named format ("Standard"), active.</summary>
    private async Task EnsureAsync(string businessId)
    {
        if (await db.BillFormats.CountDocumentsAsync(x => x.BusinessId == businessId) > 0) return;
        var business = await businesses.GetAsync(businessId);
        var now = DateTime.UtcNow;
        await db.BillFormats.InsertOneAsync(new BillFormat
        {
            Id = ObjectId.GenerateNewId().ToString(), BusinessId = businessId, Name = "Standard",
            Settings = business.Template ?? new TemplateSettings(), IsActive = true, CreatedAt = now, UpdatedAt = now,
        });
    }

    public async Task<List<BillFormat>> ListAsync(string businessId)
    {
        await EnsureAsync(businessId);
        return await db.BillFormats.Find(x => x.BusinessId == businessId).SortBy(x => x.Name).ToListAsync();
    }

    public async Task<BillFormat> GetAsync(string businessId, string id)
    {
        await EnsureAsync(businessId);
        return await db.BillFormats.Find(x => x.BusinessId == businessId && x.Id == id).FirstOrDefaultAsync()
               ?? throw new NotFoundException("Bill format");
    }

    public async Task<BillFormat> CreateAsync(string businessId, BillFormat f)
    {
        await EnsureAsync(businessId);
        f.Id = ObjectId.GenerateNewId().ToString();
        f.BusinessId = businessId;
        f.IsActive = false;                      // a new format is switched on by choosing it, so nothing changes by accident
        f.CreatedAt = f.UpdatedAt = DateTime.UtcNow;
        await CheckNameAsync(businessId, f);
        await db.BillFormats.InsertOneAsync(f);
        return f;
    }

    public async Task<BillFormat> UpdateAsync(string businessId, string id, BillFormat f)
    {
        var existing = await GetAsync(businessId, id);
        f.Id = id;
        f.BusinessId = businessId;
        f.IsActive = existing.IsActive;
        f.CreatedAt = existing.CreatedAt;
        f.UpdatedAt = DateTime.UtcNow;
        await CheckNameAsync(businessId, f);
        await db.BillFormats.ReplaceOneAsync(x => x.Id == id && x.BusinessId == businessId, f);
        if (f.IsActive) await businesses.UpdateTemplateAsync(businessId, f.Settings);   // new PDFs use the edit straight away
        return f;
    }

    /// <summary>Switching one on switches the others off. Switching the only active one off is refused: a format must always be in use.</summary>
    public async Task SetActiveAsync(string businessId, string id, bool active)
    {
        var f = await GetAsync(businessId, id);
        if (!active)
        {
            if (f.IsActive) throw new AppException("One format has to be active. Switch on another format and this one turns off by itself.");
            return;
        }
        await db.BillFormats.UpdateManyAsync(x => x.BusinessId == businessId && x.Id != id,
            Builders<BillFormat>.Update.Set(x => x.IsActive, false));
        await db.BillFormats.UpdateOneAsync(x => x.Id == id && x.BusinessId == businessId,
            Builders<BillFormat>.Update.Set(x => x.IsActive, true).Set(x => x.UpdatedAt, DateTime.UtcNow));
        await businesses.UpdateTemplateAsync(businessId, f.Settings);
    }

    public async Task DeleteAsync(string businessId, string id)
    {
        var f = await GetAsync(businessId, id);
        if (f.IsActive) throw new AppException("This format is in use. Switch on another format first, then delete this one.");
        await db.BillFormats.DeleteOneAsync(x => x.Id == id && x.BusinessId == businessId);
    }

    private async Task CheckNameAsync(string businessId, BillFormat f)
    {
        f.Name = f.Name.Trim();
        var names = await db.BillFormats.Find(x => x.BusinessId == businessId && x.Id != f.Id).Project(x => x.Name).ToListAsync();
        if (names.Any(n => n.Equals(f.Name, StringComparison.OrdinalIgnoreCase)))
            throw new AppException($"There is already a format called \u201c{f.Name}\u201d.");
    }
}
