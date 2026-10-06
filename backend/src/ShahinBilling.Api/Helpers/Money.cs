using System.Globalization;

namespace ShahinBilling.Api.Helpers;

public static class Money
{
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Indian digit grouping: 1,23,45,678.90. Does not depend on server culture.</summary>
    public static string FormatIndian(decimal value, int decimals = 2)
    {
        var negative = value < 0;
        value = Math.Round(Math.Abs(value), decimals, MidpointRounding.AwayFromZero);
        var text = value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        var parts = text.Split('.');
        var intPart = parts[0];
        string grouped;
        if (intPart.Length <= 3) grouped = intPart;
        else
        {
            var last3 = intPart[^3..];
            var rest = intPart[..^3];
            var groups = new List<string>();
            while (rest.Length > 2) { groups.Insert(0, rest[^2..]); rest = rest[..^2]; }
            if (rest.Length > 0) groups.Insert(0, rest);
            grouped = string.Join(",", groups) + "," + last3;
        }
        var result = decimals > 0 ? $"{grouped}.{parts[1]}" : grouped;
        return negative ? "-" + result : result;
    }

    /// <summary>Quantities: no trailing zeros (2, 2.5).</summary>
    public static string FormatQty(decimal q) => q.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Dates that come from the browser as "yyyy-MM-dd" are stored as UTC midnight of that day.</summary>
    public static DateTime AsUtcDate(DateTime d) => DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
    public static DateTime? AsUtcDate(DateTime? d) => d.HasValue ? AsUtcDate(d.Value) : null;
}
