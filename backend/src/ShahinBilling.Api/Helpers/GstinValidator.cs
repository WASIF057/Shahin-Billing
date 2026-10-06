using System.Text.RegularExpressions;

namespace ShahinBilling.Api.Helpers;

public static partial class GstinValidator
{
    private const string Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    [GeneratedRegex("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$")]
    private static partial Regex Pattern();

    /// <summary>Checks format, a known state code, and the checksum (15th) character.</summary>
    public static bool IsValid(string? gstin)
    {
        if (string.IsNullOrWhiteSpace(gstin)) return false;
        gstin = gstin.Trim().ToUpperInvariant();
        if (!Pattern().IsMatch(gstin)) return false;
        if (!StateCodes.All.ContainsKey(gstin[..2])) return false;
        return gstin[14] == ChecksumChar(gstin[..14]);
    }

    public static char ChecksumChar(string first14)
    {
        var sum = 0;
        for (var i = 0; i < 14; i++)
        {
            var value = Chars.IndexOf(first14[i]);
            var product = value * (i % 2 == 0 ? 1 : 2);
            sum += product / 36 + product % 36;
        }
        return Chars[(36 - sum % 36) % 36];
    }
}
