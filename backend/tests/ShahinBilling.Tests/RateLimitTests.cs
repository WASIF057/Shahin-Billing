using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShahinBilling.Api.Infrastructure;
using Xunit;

namespace ShahinBilling.Tests;

public class ClientIpTests
{
    private static HttpContext Ctx(string? remote, string? cf = null, string? xff = null, bool trust = true)
    {
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimit:TrustProxyHeaders"] = trust ? "true" : "false" }).Build())
            .BuildServiceProvider();
        var ctx = new DefaultHttpContext { RequestServices = services };
        if (remote != null) ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remote);
        if (cf != null) ctx.Request.Headers["CF-Connecting-IP"] = cf;
        if (xff != null) ctx.Request.Headers["X-Forwarded-For"] = xff;
        return ctx;
    }

    [Fact] public void Behind_a_local_tunnel_the_real_visitor_is_used() => Assert.Equal("203.0.113.9", ClientIp.Of(Ctx("127.0.0.1", cf: "203.0.113.9")));
    [Fact] public void A_private_network_proxy_is_trusted_too() => Assert.Equal("203.0.113.9", ClientIp.Of(Ctx("192.168.1.20", xff: "203.0.113.9, 10.0.0.1")));
    [Fact] public void IPv6_mapped_loopback_counts_as_local() => Assert.Equal("203.0.113.9", ClientIp.Of(Ctx("::ffff:127.0.0.1", cf: "203.0.113.9")));
    [Fact] public void Someone_reaching_the_api_directly_cannot_fake_their_address() => Assert.Equal("198.51.100.7", ClientIp.Of(Ctx("198.51.100.7", cf: "203.0.113.9", xff: "203.0.113.9")));
    [Fact] public void A_bad_header_is_ignored() => Assert.Equal("127.0.0.1", ClientIp.Of(Ctx("127.0.0.1", cf: "not-an-ip")));
    [Fact] public void Header_reading_can_be_switched_off() => Assert.Equal("127.0.0.1", ClientIp.Of(Ctx("127.0.0.1", cf: "203.0.113.9", trust: false)));
    [Fact] public void No_address_at_all_is_still_a_key() => Assert.Equal("unknown", ClientIp.Of(Ctx(null)));
}

/// <summary>Starts the API with very small limits and checks that it answers 429 once they are used up.</summary>
[Collection("api")]
public class RateLimitTests
{
    private static async Task<T> WithLimits<T>(Dictionary<string, string> limits, Func<AppFactory, Task<T>> test)
    {
        foreach (var (k, v) in limits) Environment.SetEnvironmentVariable(k, v);
        try { using var f = new AppFactory(); return await test(f); }
        finally { foreach (var k in limits.Keys) Environment.SetEnvironmentVariable(k, null); }
    }

    [Fact]
    public async Task Anonymous_callers_are_stopped_after_their_limit_with_a_clear_answer()
    {
        var (codes, last) = await WithLimits(new() { ["RateLimit__AnonymousPerMinute"] = "5" }, async f =>
        {
            var c = f.CreateClient();
            var results = new List<HttpStatusCode>();
            HttpResponseMessage? r = null;
            for (var i = 0; i < 7; i++) { r = await c.GetAsync("/api/clients"); results.Add(r.StatusCode); }
            return (results, r!);
        });

        Assert.All(codes.Take(5), s => Assert.Equal(HttpStatusCode.Unauthorized, s));   // not logged in, but let through the limiter
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[5]);
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[6]);
        Assert.True(last.Headers.Contains("Retry-After"));
        var body = await last.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(429, body.GetProperty("status").GetInt32());
        Assert.Contains("Too many requests", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task The_limit_can_be_switched_off()
    {
        var codes = await WithLimits(new() { ["RateLimit__AnonymousPerMinute"] = "2", ["RateLimit__Enabled"] = "false" }, async f =>
        {
            var c = f.CreateClient();
            var results = new List<HttpStatusCode>();
            for (var i = 0; i < 8; i++) results.Add((await c.GetAsync("/api/clients")).StatusCode);
            return results;
        });
        Assert.All(codes, s => Assert.Equal(HttpStatusCode.Unauthorized, s));
    }

    [Fact]
    public async Task Login_attempts_have_their_own_tighter_limit()
    {
        var codes = await WithLimits(new() { ["RateLimit__AuthPerMinute"] = "3" }, async f =>
        {
            var c = f.CreateClient();
            var results = new List<HttpStatusCode>();
            for (var i = 0; i < 5; i++)
                results.Add((await c.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.com", password = "wrong-password" })).StatusCode);
            return results;
        });
        Assert.Equal(HttpStatusCode.Unauthorized, codes[0]);
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[3]);
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[4]);
    }
}
