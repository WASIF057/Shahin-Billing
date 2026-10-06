using System.Text.Json;
using MongoDB.Driver;
using ShahinBilling.Api.Data;

namespace ShahinBilling.Api.Services;

/// <summary>Builds the backup file for one business: everything of theirs as JSON (password hashes are never included).</summary>
public class BackupExporter(MongoContext db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<byte[]> BuildAsync(string businessId)
    {
        var bid = businessId;
        var data = new
        {
            exportedAt = DateTime.UtcNow,
            business = await db.Businesses.Find(b => b.Id == bid).FirstOrDefaultAsync(),
            productTypes = await db.ProductTypes.Find(x => x.BusinessId == bid).ToListAsync(),
            emailTemplates = await db.EmailTemplates.Find(x => x.BusinessId == bid).ToListAsync(),
            billFormats = await db.BillFormats.Find(x => x.BusinessId == bid).ToListAsync(),
            items = await db.Items.Find(x => x.BusinessId == bid).ToListAsync(),
            clients = await db.Clients.Find(x => x.BusinessId == bid).ToListAsync(),
            orders = await db.Orders.Find(x => x.BusinessId == bid).ToListAsync(),
            invoices = await db.Invoices.Find(x => x.BusinessId == bid).ToListAsync()
        };
        return JsonSerializer.SerializeToUtf8Bytes(data, Json);
    }
}
