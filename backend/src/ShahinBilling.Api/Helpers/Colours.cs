namespace ShahinBilling.Api.Helpers;

public static class Colours
{
    /// <summary>True for a #rrggbb colour that is light enough for dark text on top of it.</summary>
    public static bool IsLight(string? hex)
    {
        if (hex is not { Length: 7 } || hex[0] != '#') return false;
        if (!int.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out var n)) return false;
        double r = (n >> 16) & 255, g = (n >> 8) & 255, b = n & 255;
        return 0.299 * r + 0.587 * g + 0.114 * b >= 180;
    }
}
