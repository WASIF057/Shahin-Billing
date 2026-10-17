using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ShahinBilling.Tests;

[Collection("api")]
public class HealthAndErrorTests
{
    [Fact]
    public async Task Health_answers_ok_when_the_api_and_database_are_up()
    {
        using var f = new AppFactory();
        var r = await f.CreateClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("ok", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task An_unknown_api_address_gets_a_json_404_not_an_empty_one()
    {
        using var f = new AppFactory();
        var r = await f.CreateClient().GetAsync("/api/this-does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Contains("json", r.Content.Headers.ContentType?.MediaType);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(404, body.GetProperty("status").GetInt32());
        Assert.Contains("does not exist", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task A_wrong_method_gets_a_json_405()
    {
        using var f = new AppFactory();
        var r = await f.CreateClient().PostAsync("/api/health", null);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, r.StatusCode);
        Assert.Equal(405, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32());
    }
}
