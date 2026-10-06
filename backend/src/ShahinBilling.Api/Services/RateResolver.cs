using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

/// <summary>Business rule 4.1: a client's special rate wins, otherwise the item's default rate.</summary>
public static class RateResolver
{
    public static (decimal Rate, bool IsSpecial) Resolve(Item item, string? clientId)
    {
        if (!string.IsNullOrEmpty(clientId))
        {
            var special = item.SpecialRates.FirstOrDefault(s => s.ClientIds.Contains(clientId));
            if (special != null) return (special.Rate, true);
        }
        return (item.DefaultRate, false);
    }

    /// <summary>Returns the ids of clients that appear in more than one special-rate group.</summary>
    public static IReadOnlyList<string> FindDuplicateClients(Item item) =>
        item.SpecialRates
            .SelectMany(s => s.ClientIds.Distinct())
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
}
