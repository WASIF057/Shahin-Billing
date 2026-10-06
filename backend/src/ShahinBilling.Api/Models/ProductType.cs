namespace ShahinBilling.Api.Models;

/// <summary>A kind of product (Bed, Pillow, Cushion, Bolster...) with the choices offered when adding an item of that kind.</summary>
public class ProductType : BusinessEntity
{
    public string Name { get; set; } = "";
    /// <summary>true = items of this type also pick a variant (e.g. Sada, Quilt). Only some types need it.</summary>
    public bool UsesVariants { get; set; }
    public List<string> Variants { get; set; } = new();
    public List<string> Sizes { get; set; } = new();
    /// <summary>Cloth choices for this type (e.g. Cotton, Polyester). Leave empty if the type has no cloth choice.</summary>
    public List<string> ClothTypes { get; set; } = new();
    /// <summary>Colours for each cloth (Polyester may have different colours from Cotton). A cloth with no entry has no colour choice.</summary>
    public List<ClothColourSet> ClothColours { get; set; } = new();
}

public class ClothColourSet
{
    public string Cloth { get; set; } = "";
    public List<string> Colours { get; set; } = new();
}
