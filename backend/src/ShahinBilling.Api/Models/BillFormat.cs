namespace ShahinBilling.Api.Models;

/// <summary>A named PDF look (layout, colour, what to show). Exactly one is Active; its settings are what every new PDF uses.</summary>
public class BillFormat : BusinessEntity
{
    public string Name { get; set; } = "";
    public TemplateSettings Settings { get; set; } = new();
    public bool IsActive { get; set; }
}
