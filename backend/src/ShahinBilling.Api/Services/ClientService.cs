using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

public class ClientService(MongoContext db)
{
    public async Task<List<Client>> ListAsync(string businessId, string? search, bool? active)
    {
        var f = Builders<Client>.Filter;
        var filter = f.Eq(x => x.BusinessId, businessId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var rx = new BsonRegularExpression(Regex.Escape(search.Trim()), "i");
            filter &= f.Or(f.Regex(x => x.Name, rx), f.Regex(x => x.Gstin, rx),
                f.Regex(x => x.ContactPerson, rx), f.Regex(x => x.Phone, rx));
        }
        if (active.HasValue) filter &= f.Eq(x => x.IsActive, active.Value);
        return await db.Clients.Find(filter).SortBy(x => x.Name).ToListAsync();
    }

    public async Task<Client> GetAsync(string businessId, string id) =>
        await db.Clients.Find(x => x.BusinessId == businessId && x.Id == id).FirstOrDefaultAsync()
        ?? throw new NotFoundException("Client");

    public async Task<Client> CreateAsync(string businessId, Client c)
    {
        c.Id = ObjectId.GenerateNewId().ToString();
        c.BusinessId = businessId;
        c.CreatedAt = c.UpdatedAt = DateTime.UtcNow;
        Normalize(c);
        await db.Clients.InsertOneAsync(c);
        return c;
    }

    public async Task<Client> UpdateAsync(string businessId, string id, Client c)
    {
        var existing = await GetAsync(businessId, id);
        c.Id = id;
        c.BusinessId = businessId;
        c.CreatedAt = existing.CreatedAt;
        c.UpdatedAt = DateTime.UtcNow;
        Normalize(c);
        await db.Clients.ReplaceOneAsync(x => x.Id == id && x.BusinessId == businessId, c);
        return c;
    }

    /// <summary>Deletes a client that has no bills, and removes them from any item's special-rate list.</summary>
    public async Task DeleteAsync(string businessId, string id)
    {
        await GetAsync(businessId, id);
        var used = await db.Invoices.CountDocumentsAsync(i => i.BusinessId == businessId && i.ClientId == id);
        if (used > 0)
            throw new AppException($"This client has {used} bill{(used == 1 ? "" : "s")}, so it can't be deleted. Use the eye icon to hide the client from billing instead.");

        await db.Clients.DeleteOneAsync(x => x.Id == id && x.BusinessId == businessId);
        await db.Users.DeleteManyAsync(u => u.BusinessId == businessId && u.ClientId == id);   // their ordering login goes too

        // Take the client out of every special rate, then drop any rate left with no clients
        await db.Items.UpdateManyAsync(i => i.BusinessId == businessId,
            Builders<Item>.Update.Pull("specialRates.$[].clientIds", id));
        await db.Items.UpdateManyAsync(i => i.BusinessId == businessId,
            Builders<Item>.Update.PullFilter(i => i.SpecialRates, s => s.ClientIds.Count == 0));
    }

    public async Task SetActiveAsync(string businessId, string id, bool active)
    {
        var r = await db.Clients.UpdateOneAsync(x => x.Id == id && x.BusinessId == businessId,
            Builders<Client>.Update.Set(x => x.IsActive, active).Set(x => x.UpdatedAt, DateTime.UtcNow));
        if (r.MatchedCount == 0) throw new NotFoundException("Client");
    }

    public async Task<List<ClientSpecialRate>> SpecialRatesAsync(string businessId, string clientId)
    {
        await GetAsync(businessId, clientId);
        var items = await db.Items.Find(i => i.BusinessId == businessId && i.SpecialRates.Any(s => s.ClientIds.Contains(clientId)))
            .ToListAsync();
        return items.Select(i =>
        {
            var (rate, _) = RateResolver.Resolve(i, clientId);
            return new ClientSpecialRate(i.Id, i.Name, i.SizeOrVariant, i.DefaultRate, rate);
        }).OrderBy(r => r.ItemName).ToList();
    }

    private static void Normalize(Client c)
    {
        c.Gstin = c.Gstin.Trim().ToUpperInvariant();
        c.Cities = (c.Cities ?? new()).Select(x => x.Trim()).Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // Older clients only had a single city on the address: keep it as their one city
        if (c.Cities.Count == 0 && !string.IsNullOrWhiteSpace(c.BillingAddress.City)) c.Cities.Add(c.BillingAddress.City.Trim());
        c.BillingAddress.City = c.Cities.FirstOrDefault() ?? "";
        if (string.IsNullOrEmpty(c.BillingAddress.StateCode))
            c.BillingAddress.StateCode = StateCodes.FromGstin(c.Gstin);
        c.BillingAddress.State = StateCodes.NameOf(c.BillingAddress.StateCode);
        if (c.ShippingSameAsBilling) c.ShippingAddress = c.BillingAddress.Clone();
        else c.ShippingAddress.State = StateCodes.NameOf(c.ShippingAddress.StateCode);
    }
}
