namespace ShahinBilling.Api.Helpers;

/// <summary>Converts amounts to words using the Indian system (thousand, lakh, crore).</summary>
public static class AmountInWords
{
    private static readonly string[] Ones =
    {
        "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
    };
    private static readonly string[] Tens =
        { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

    public static string Convert(decimal amount)
    {
        amount = Math.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        var rupees = (long)Math.Floor(amount);
        var paise = (int)Math.Round((amount - rupees) * 100);

        var words = rupees == 0 ? "Zero" : NumberToWords(rupees);
        var result = $"Rupees {words}";
        if (paise > 0) result += $" and {TwoDigits(paise)} Paise";
        return result + " Only";
    }

    public static string NumberToWords(long n)
    {
        if (n == 0) return "Zero";
        var parts = new List<string>();

        var crore = n / 10_000_000; n %= 10_000_000;
        var lakh = n / 100_000; n %= 100_000;
        var thousand = n / 1000; n %= 1000;
        var hundred = n / 100; n %= 100;

        if (crore > 0) parts.Add($"{NumberToWords(crore)} Crore");   // handles 100+ crore
        if (lakh > 0) parts.Add($"{TwoDigits((int)lakh)} Lakh");
        if (thousand > 0) parts.Add($"{TwoDigits((int)thousand)} Thousand");
        if (hundred > 0) parts.Add($"{Ones[hundred]} Hundred");
        if (n > 0) parts.Add(TwoDigits((int)n));

        return string.Join(" ", parts);
    }

    private static string TwoDigits(int n)
    {
        if (n < 20) return Ones[n];
        var t = Tens[n / 10];
        return n % 10 == 0 ? t : $"{t}-{Ones[n % 10]}";
    }
}
