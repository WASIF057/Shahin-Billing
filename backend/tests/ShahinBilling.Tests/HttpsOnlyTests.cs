using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ShahinBilling.Api.Infrastructure;
using Xunit;

namespace ShahinBilling.Tests;

public class HttpsOnlyTests
{
    private static async Task<(DefaultHttpContext Ctx, bool Passed)> Run(string method, string? remote, string? cfVisitor = null, string? proto = null, bool https = false, bool on = true)
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Security:RequireHttps"] = on ? "true" : "false" }).Build();
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        ctx.Request.Method = method;
        ctx.Request.Host = new HostString("billing.example.com");
        ctx.Request.Path = "/bills";
        ctx.Request.QueryString = new QueryString("?x=1");
        ctx.Request.IsHttps = https;
        if (remote != null) ctx.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        if (cfVisitor != null) ctx.Request.Headers["CF-Visitor"] = cfVisitor;
        if (proto != null) ctx.Request.Headers["X-Forwarded-Proto"] = proto;
        var passed = false;
        await new HttpsOnlyMiddleware(_ => { passed = true; return Task.CompletedTask; }, cfg).InvokeAsync(ctx);
        return (ctx, passed);
    }

    [Fact] public async Task A_visitor_on_https_through_the_tunnel_gets_in() => Assert.True((await Run("GET", "127.0.0.1", cfVisitor: "{\"scheme\":\"https\"}")).Passed);
    [Fact] public async Task X_Forwarded_Proto_https_gets_in() => Assert.True((await Run("POST", "127.0.0.1", proto: "https")).Passed);
    [Fact] public async Task A_direct_https_connection_gets_in() => Assert.True((await Run("POST", "198.51.100.7", https: true)).Passed);

    [Fact]
    public async Task A_page_request_on_http_is_sent_to_the_https_address()
    {
        var (ctx, passed) = await Run("GET", "127.0.0.1", cfVisitor: "{\"scheme\":\"http\"}");
        Assert.False(passed);
        Assert.Equal(308, ctx.Response.StatusCode);
        Assert.Equal("https://billing.example.com/bills?x=1", ctx.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task A_login_on_http_is_refused_not_redirected()
    {
        var (ctx, passed) = await Run("POST", "127.0.0.1", proto: "http");
        Assert.False(passed);
        Assert.Equal(400, ctx.Response.StatusCode);
        ctx.Response.Body.Position = 0;
        Assert.Contains("HTTPS", await new StreamReader(ctx.Response.Body).ReadToEndAsync());
    }

    [Fact] public async Task A_direct_plain_http_connection_from_outside_is_stopped() => Assert.False((await Run("GET", "198.51.100.7")).Passed);
    [Fact] public async Task Someone_outside_cannot_claim_https_with_a_header() => Assert.False((await Run("POST", "198.51.100.7", proto: "https")).Passed);
    [Fact] public async Task An_internal_request_with_no_headers_is_let_through_so_the_site_cannot_lock_itself_out() => Assert.True((await Run("GET", "127.0.0.1")).Passed);
    [Fact] public async Task A_private_network_request_with_no_headers_is_let_through() => Assert.True((await Run("GET", "192.168.1.20")).Passed);
    [Fact] public async Task Switched_off_it_lets_everything_through() => Assert.True((await Run("POST", "198.51.100.7", on: false)).Passed);
}
