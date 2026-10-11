namespace ShahinBilling.Api.Helpers;

public static class TextClean
{
    /// <summary>Trims and turns curly/look-alike quote marks into plain ones, so 4″ and 4" are the same size everywhere.</summary>
    public static string Plain(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Trim()
            .Replace('″', '"').Replace('“', '"').Replace('”', '"').Replace('‟', '"').Replace('＂', '"')
            .Replace('′', '\'').Replace('‘', '\'').Replace('’', '\'');
    }
}
