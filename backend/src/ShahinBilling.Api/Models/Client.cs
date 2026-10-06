namespace ShahinBilling.Api.Models;

public class Client : BusinessEntity
{
    public string Name { get; set; } = "";
    public string Gstin { get; set; } = "";
    public string Pan { get; set; } = "";
    public string ContactPerson { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public Address BillingAddress { get; set; } = new();
    public bool ShippingSameAsBilling { get; set; } = true;
    public Address ShippingAddress { get; set; } = new();
    /// <summary>The cities this client has stores in (same GSTIN and details). One is picked on each bill.</summary>
    public List<string> Cities { get; set; } = new();
    public string Notes { get; set; } = "";
    /// <summary>Starting point for new bills: pick Non-GST bill when this client is chosen. Can be changed on each bill.</summary>
    public bool BilledWithoutGst { get; set; }
    public bool IsActive { get; set; } = true;
}
