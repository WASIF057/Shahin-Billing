using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

/// <summary>Development only: fills an empty business with demo data so every feature can be tried.</summary>
public class SeedService(MongoContext db)
{
    public async Task<string> SeedAsync(string businessId)
    {
        if (await db.Items.Find(i => i.BusinessId == businessId).AnyAsync())
            return "This business already has items, so demo data was not added.";

        var biz = await db.Businesses.Find(b => b.Id == businessId).FirstAsync();
        if (string.IsNullOrEmpty(biz.Gstin))
        {
            biz.Gstin = "27ABCPS1234K1ZM";
            biz.Pan = "ABCPS1234K";
            biz.Address = new Address { Line1 = "Shop 4, Sai Complex", Line2 = "Station Road", City = "Mumbai", State = "Maharashtra", StateCode = "27", Pincode = "400070" };
            biz.Phone = "98765 43210";
            biz.Email = "accounts@example.com";
            biz.Bank = new BankDetails { AccountName = biz.Name, AccountNumber = "001234567890", Ifsc = "HDFC0000123", BankName = "HDFC Bank", Branch = "Kurla", UpiId = "shahin@hdfcbank" };
            biz.AuthorisedSignatoryName = "Proprietor";
            biz.InvoiceNumbering = new InvoiceNumbering { Prefix = "SE", Separator = "/", Padding = 4 };
            await db.Businesses.ReplaceOneAsync(b => b.Id == businessId, biz);
        }

        Client C(string name, string gstin, string city, string state, string code, string contact) => new()
        {
            BusinessId = businessId, Name = name, Gstin = gstin, ContactPerson = contact, Phone = "90000 00000",
            BillingAddress = new Address { Line1 = "Main Road", City = city, State = state, StateCode = code, Pincode = "400001" },
            ShippingSameAsBilling = true
        };
        var clients = new List<Client>
        {
            C("Comfort Furnishers", "27AADCS5678M1ZM", "Mumbai", "Maharashtra", "27", "Mr. Sameer"),
            C("Royal Hotels Pvt Ltd", "27AAKFR9012P1ZK", "Pune", "Maharashtra", "27", "Ms. Priya"),
            C("City Care Hospital", "27AAGCC3456Q1Z3", "Thane", "Maharashtra", "27", "Dr. Khan"),
            C("Nest Home Store", "29AABCN7890R1ZD", "Bengaluru", "Karnataka", "29", "Mr. Ravi")   // other state -> IGST
        };
        foreach (var c in clients) c.ShippingAddress = c.BillingAddress.Clone();
        await db.Clients.InsertManyAsync(clients);

        Item I(string name, string size, decimal rate) => new()
        {
            BusinessId = businessId, Name = name, SizeOrVariant = size, Unit = "PCS", GstRate = 18, DefaultRate = rate
        };
        var pillow = I("Fibre Pillow", "17x27 in", 100);
        pillow.SpecialRates.Add(new SpecialRate { Rate = 120, ClientIds = new() { clients[1].Id, clients[2].Id } });
        await db.Items.InsertManyAsync(new[]
        {
            I("Orthopedic Mattress", "72x36x6 in", 6500),
            I("Orthopedic Mattress", "75x60x8 in", 11500),
            I("Spring Mattress", "78x72x8 in", 14500),
            pillow,
            I("Memory Foam Pillow", "24x16x5 in", 450)
        });
        return "Demo data added: 5 items, 4 clients (one in Karnataka to test IGST), and a special pillow rate of 120 for 2 clients.";
    }
}
