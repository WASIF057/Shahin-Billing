using System.Net;
using Xunit;

namespace ShahinBilling.Tests;

[Collection("api")]
public class SecurityHeadersTests
{
    [Fact]
    public async Task Every_answer_carries_the_browser_protection_headers_and_api_answers_are_not_cached()
    {
        using var f = new AppFactory();
        var r = await f.CreateClient().GetAsync("/api/clients");   // not logged in: a 401, which must have the headers too

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", r.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("camera=()", r.Headers.GetValues("Permissions-Policy").Single());
        Assert.Contains("no-store", r.Headers.GetValues("Cache-Control").Single());
    }

    [Fact]
    public async Task Blocked_requests_carry_the_headers_too()
    {
        Environment.SetEnvironmentVariable("RateLimit__AnonymousPerMinute", "1");
        try
        {
            using var f = new AppFactory();
            var c = f.CreateClient();
            await c.GetAsync("/api/clients");
            var blocked = await c.GetAsync("/api/clients");
            Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
            Assert.Equal("nosniff", blocked.Headers.GetValues("X-Content-Type-Options").Single());
        }
        finally { Environment.SetEnvironmentVariable("RateLimit__AnonymousPerMinute", null); }
    }
}
