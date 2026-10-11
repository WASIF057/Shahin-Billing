using ShahinBilling.Api.Helpers;
using Xunit;

namespace ShahinBilling.Tests;

public class TextCleanTests
{
    [Theory]
    [InlineData("Box Bed 4″", "Box Bed 4\"")]
    [InlineData("  Box Bed 6” ", "Box Bed 6\"")]
    [InlineData("Box Bed 4\"", "Box Bed 4\"")]
    [InlineData(null, "")]
    public void Look_alike_quotes_become_plain(string? input, string expected) =>
        Assert.Equal(expected, TextClean.Plain(input));
}
