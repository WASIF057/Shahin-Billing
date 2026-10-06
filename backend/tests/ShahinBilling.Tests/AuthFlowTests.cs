using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ShahinBilling.Api.Services;
using Xunit;

namespace ShahinBilling.Tests;

/// <summary>Records the emails instead of sending them, so a test can read the 6-digit codes.</summary>
public class FakeSender(IOptions<SmtpSettings> o, ILogger<EmailSender> l) : EmailSender(o, l)
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();
    public override Task SendAsync(IEnumerable<string> to, string subject, string body, string? attachmentName, byte[]? attachment)
    {
        lock (Sent) foreach (var t in to) Sent.Add((t, subject, body));
        return Task.CompletedTask;
    }
    public string LastCode() { lock (Sent) return Regex.Match(Sent[^1].Body, @"\b(\d{6})\b").Groups[1].Value; }
}

/// <summary>The whole API running in memory, on a throwaway database that is dropped afterwards. Needs MongoDB on localhost.</summary>
public class AppFactory : WebApplicationFactory<Program>
{
    public readonly string Db = "shahin_test_" + Guid.NewGuid().ToString("N")[..8];
    private const string Conn = "mongodb://localhost:27017/";
    public FakeSender Sender => Services.GetRequiredService<FakeSender>();

    public AppFactory()
    {
        // Set like a real server would: Program.cs reads some settings (the signing key) while it is starting up,
        // before a test's in-memory overrides can reach it.
        foreach (var (k, v) in new Dictionary<string, string>
        {
            ["Mongo__ConnectionString"] = Conn, ["Mongo__Database"] = Db,
            ["Jwt__Key"] = "integration-test-signing-key-0123456789abcdef",
            ["Smtp__Host"] = "fake.local", ["Smtp__FromEmail"] = "billing@test.local",
            ["Backup__Enabled"] = "false", ["RateLimit__AuthPerMinute"] = "1000", ["Security__RequireOtp"] = "true", ["Google__ClientId"] = "",
        }) Environment.SetEnvironmentVariable(k, v);
    }

    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        b.UseEnvironment("Development");
        b.ConfigureTestServices(s =>
        {
            s.RemoveAll<EmailSender>();
            s.AddSingleton<FakeSender>();
            s.AddSingleton<EmailSender>(sp => sp.GetRequiredService<FakeSender>());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) try { new MongoClient(Conn).DropDatabase(Db); } catch { /* best effort */ }
    }
}

/// <summary>Live-API tests share process-wide settings, so they run one after another.</summary>
[Collection("api")]
public class AuthFlowTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();
    private static HttpClient Authed(AppFactory f, string token)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private static async Task<string> RegisterAsync(AppFactory f, HttpClient c)
    {
        var reg = await c.PostAsJsonAsync("/api/auth/register", new { name = "Test Owner", email = "owner@test.local", password = "password123", businessName = "Test Co" });
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);
        var j = await Json(reg);
        Assert.True(j.GetProperty("otpRequired").GetBoolean());   // the email must be confirmed first
        var ok = await c.PostAsJsonAsync("/api/auth/verify-registration", new { challengeId = j.GetProperty("challengeId").GetString(), code = f.Sender.LastCode() });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        return (await Json(ok)).GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Sign_up_then_log_in_with_an_emailed_code_and_use_the_token()
    {
        using var f = new AppFactory();
        var c = f.CreateClient();

        var token = await RegisterAsync(f, c);
        Assert.Equal(HttpStatusCode.OK, (await Authed(f, token).GetAsync("/api/business")).StatusCode);   // the sign-up token works

        // log in: password, then the code
        var login = await c.PostAsJsonAsync("/api/auth/login", new { email = "owner@test.local", password = "password123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var lj = await Json(login);
        Assert.True(lj.GetProperty("otpRequired").GetBoolean());
        var verify = await c.PostAsJsonAsync("/api/auth/verify-otp", new { challengeId = lj.GetProperty("challengeId").GetString(), code = f.Sender.LastCode() });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var token2 = (await Json(verify)).GetProperty("token").GetString()!;

        // the token from the code step must be accepted everywhere an owner goes
        var api = Authed(f, token2);
        foreach (var url in new[] { "/api/business", "/api/dashboard", "/api/items", "/api/clients", "/api/invoices", "/api/email/templates" })
        {
            var r = await api.GetAsync(url);
            Assert.True(r.StatusCode == HttpStatusCode.OK, $"{url} returned {(int)r.StatusCode}");
        }
    }

    [Fact]
    public async Task A_wrong_code_is_refused_and_the_right_one_still_works_afterwards()
    {
        using var f = new AppFactory();
        var c = f.CreateClient();
        await RegisterAsync(f, c);

        var lj = await Json(await c.PostAsJsonAsync("/api/auth/login", new { email = "owner@test.local", password = "password123" }));
        var id = lj.GetProperty("challengeId").GetString();
        var right = f.Sender.LastCode();
        var wrong = right == "000000" ? "111111" : "000000";

        var bad = await c.PostAsJsonAsync("/api/auth/verify-otp", new { challengeId = id, code = wrong });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Contains("tries", (await Json(bad)).GetProperty("title").GetString());

        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/auth/verify-otp", new { challengeId = id, code = right })).StatusCode);
    }

    [Fact]
    public async Task A_code_works_only_once()
    {
        using var f = new AppFactory();
        var c = f.CreateClient();
        await RegisterAsync(f, c);

        var lj = await Json(await c.PostAsJsonAsync("/api/auth/login", new { email = "owner@test.local", password = "password123" }));
        var body = new { challengeId = lj.GetProperty("challengeId").GetString(), code = f.Sender.LastCode() };
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/auth/verify-otp", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/verify-otp", body)).StatusCode);
    }

    [Fact]
    public async Task An_account_made_before_staff_logins_existed_can_still_use_the_app()
    {
        using var f = new AppFactory();
        var c = f.CreateClient();
        await RegisterAsync(f, c);

        // Make the account look like an old one: no isActive and no tokenVersion stored
        var users = new MongoClient("mongodb://localhost:27017/").GetDatabase(f.Db).GetCollection<MongoDB.Bson.BsonDocument>("users");
        await users.UpdateOneAsync(MongoDB.Driver.FilterDefinition<MongoDB.Bson.BsonDocument>.Empty,
            MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Unset("isActive").Unset("tokenVersion"));

        var lj = await Json(await c.PostAsJsonAsync("/api/auth/login", new { email = "owner@test.local", password = "password123" }));
        var verify = await c.PostAsJsonAsync("/api/auth/verify-otp", new { challengeId = lj.GetProperty("challengeId").GetString(), code = f.Sender.LastCode() });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var token = (await Json(verify)).GetProperty("token").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await Authed(f, token).GetAsync("/api/business")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Authed(f, token).GetAsync("/api/dashboard")).StatusCode);
    }
}
