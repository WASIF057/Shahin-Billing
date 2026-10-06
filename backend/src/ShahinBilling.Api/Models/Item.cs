using MongoDB.Bson;

namespace ShahinBilling.Api.Models;

public class Item : BusinessEntity
{
    public string Name { get; set; } = "";
    /// <summary>What bills show next to the name. For typed items this is built from Variant and Size.</summary>
    public string SizeOrVariant { get; set; } = "";
    /// <summary>Set when the item was picked from Item setup (Bed, Pillow...). Empty for older items.</summary>
    public string TypeId { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string Variant { get; set; } = "";
    public string Size { get; set; } = "";
    public string Cloth { get; set; } = "";
    /// <summary>Colour of the cloth (from the cloth's own list in Item setup). Empty when that cloth has no colours.</summary>
    public string Colour { get; set; } = "";
    public string Description { get; set; } = "";
    public string HsnCode { get; set; } = "";
    public string Unit { get; set; } = "PCS";
    public decimal GstRate { get; set; } = 18;
    /// <summary>Rate exclusive of GST, used for every client without a special rate.</summary>
    public decimal DefaultRate { get; set; }
    public List<SpecialRate> SpecialRates { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

/// <summary>A different rate for a selected group of clients.</summary>
public class SpecialRate
{
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public decimal Rate { get; set; }
    public List<string> ClientIds { get; set; } = new();
}
