namespace ShahinBilling.Api.Dtos;

/// <summary>One cell of the price grid: a variant and size with its rate. Empty rate = don't create it.</summary>
public class GenerateItemRow
{
    public string? Variant { get; set; }
    public string? Size { get; set; }
    public decimal? Rate { get; set; }
}

public class GenerateItemsRequest
{
    public string TypeId { get; set; } = "";
    public string? Cloth { get; set; }
    public List<string>? Colours { get; set; }
    public string? Unit { get; set; }
    public decimal GstRate { get; set; } = 18;
    public List<GenerateItemRow>? Rows { get; set; }
}

public record GenerateItemsResult(int Created, int Skipped);
