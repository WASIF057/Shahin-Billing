using Microsoft.Extensions.Configuration;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Services;
using Xunit;

namespace ShahinBilling.Tests;

public class CaptchaTests
{
    private static CaptchaService Make(bool on = true) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Security:RequireCaptcha"] = on ? "true" : "false" }).Build());

    [Fact]
    public void A_new_picture_has_an_id_and_an_svg_image_with_no_readable_text()
    {
        var c = Make().Create();
        Assert.NotEmpty(c.Id);
        Assert.Equal(5, c.Answer.Length);
        Assert.StartsWith("data:image/svg+xml;base64,", c.Image);
        var svg = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(c.Image["data:image/svg+xml;base64,".Length..]));
        Assert.DoesNotContain("<text", svg);
        Assert.DoesNotContain(c.Answer, svg);
    }

    [Fact]
    public void The_right_letters_pass_in_any_case_and_with_spaces()
    {
        var s = Make();
        var c = s.Create();
        s.Require(c.Id, "  " + c.Answer.ToLowerInvariant() + " ");
    }

    [Fact]
    public void A_picture_can_be_used_only_once()
    {
        var s = Make();
        var c = s.Create();
        s.Require(c.Id, c.Answer);
        Assert.Throws<AppException>(() => s.Require(c.Id, c.Answer));
    }

    [Fact]
    public void A_wrong_answer_fails_and_uses_the_picture_up()
    {
        var s = Make();
        var c = s.Create();
        Assert.Throws<AppException>(() => s.Require(c.Id, "WRONG"));
        Assert.Throws<AppException>(() => s.Require(c.Id, c.Answer));   // the right answer no longer works either
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "ABCDE")]
    [InlineData("nope", "ABCDE")]
    public void Missing_or_unknown_pictures_fail(string? id, string? answer) =>
        Assert.Throws<AppException>(() => Make().Require(id, answer));

    [Fact]
    public void When_switched_off_nothing_is_checked()
    {
        var s = Make(on: false);
        Assert.False(s.Enabled);
        s.Require(null, null);
    }

    [Fact]
    public void Pictures_are_different_each_time()
    {
        var s = Make();
        Assert.NotEqual(s.Create().Image, s.Create().Image);
    }
}
