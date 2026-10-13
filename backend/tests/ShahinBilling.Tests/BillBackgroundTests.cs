using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Validators;
using Xunit;

namespace ShahinBilling.Tests;

public class BillBackgroundTests
{
    [Fact]
    public void Bill_background_is_white_by_default() => Assert.Equal("#FFFFFF", new TemplateSettings().BackgroundColour);

    [Theory]
    [InlineData("#FFFFFF", true)]
    [InlineData("#FFF8E6", true)]
    [InlineData("#EAF2FB", true)]
    [InlineData("#1F4E79", false)]
    [InlineData("#000000", false)]
    [InlineData("red", false)]
    [InlineData(null, false)]
    public void Only_light_colours_are_accepted(string? hex, bool light) => Assert.Equal(light, Colours.IsLight(hex));

    [Fact]
    public void Validator_accepts_a_light_paper_and_rejects_a_dark_one()
    {
        var v = new TemplateValidator();
        Assert.True(v.Validate(new TemplateSettings { BackgroundColour = "#F3F4F6" }).IsValid);
        Assert.False(v.Validate(new TemplateSettings { BackgroundColour = "#222222" }).IsValid);
        Assert.False(v.Validate(new TemplateSettings { BackgroundColour = "not a colour" }).IsValid);
    }
}
