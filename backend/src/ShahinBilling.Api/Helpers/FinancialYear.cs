namespace ShahinBilling.Api.Helpers;

/// <summary>Indian financial year: 1 April to 31 March. Label format "2026-27".</summary>
public static class FinancialYear
{
    public static int StartYear(DateTime date) => date.Month >= 4 ? date.Year : date.Year - 1;

    public static string Label(DateTime date)
    {
        var start = StartYear(date);
        return $"{start}-{(start + 1) % 100:D2}";
    }

    public static DateTime StartDate(DateTime date) => new(StartYear(date), 4, 1, 0, 0, 0, DateTimeKind.Utc);
    public static DateTime EndDateExclusive(DateTime date) => StartDate(date).AddYears(1);
}
