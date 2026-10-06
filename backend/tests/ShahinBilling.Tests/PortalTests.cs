using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Services;
using Xunit;

namespace ShahinBilling.Tests;

public class OrderRuleTests
{
    [Fact]
    public void Order_numbers_are_padded() => Assert.Equal("ORD-0007", OrderService.OrderNumber(7));

    [Fact]
    public void The_item_list_shows_names_and_quantities_and_no_prices()
    {
        var text = EmailService.OrderItemsText(new[]
        {
            new OrderLine { Label = "Bed — Box · Cotton · 6ft", Quantity = 2, Unit = "PCS" },
            new OrderLine { Label = "Pillow — Medium", Quantity = 10.5m, Unit = "PCS" },
        });
        Assert.Equal("- Bed — Box · Cotton · 6ft x 2 PCS\n- Pillow — Medium x 10.5 PCS", text);
        Assert.DoesNotContain("Rs", text);
    }

    [Fact]
    public void The_standard_order_emails_only_use_placeholders_that_exist()
    {
        var known = EmailService.OrderValues(new Order { OrderNumber = "ORD-0001" }, new Business { Name = "Shop" });
        var all = EmailTemplateService.DefaultOrderTemplates().Concat(EmailTemplateService.DefaultOrderStatusTemplates());
        foreach (var t in all)
            foreach (var text in new[] { t.Subject, t.Body })
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"\{\{(\w+)\}\}"))
                    Assert.True(known.ContainsKey(m.Groups[1].Value), $"{t.Name}: unknown placeholder {m.Value}");
    }

    [Fact]
    public void Nothing_a_client_can_receive_has_a_price_field()
    {
        // The shapes sent to a client's browser. If someone adds a rate or tax field here, this fails.
        var banned = new[] { "rate", "price", "gst", "amount", "total", "discount", "cost", "special" };
        foreach (var type in new[] { typeof(PortalCatalogItem), typeof(PortalOrderDto), typeof(PortalOrderLineDto), typeof(PortalProfile) })
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Assert.DoesNotContain(banned, b => p.Name.Contains(b, StringComparison.OrdinalIgnoreCase));
    }
}

public class ColourRuleTests
{
    [Fact]
    public void The_item_text_is_variant_cloth_colour_size_skipping_blanks()
    {
        Assert.Equal("Box · Polyester · Red · 6ft", ItemService.ComposeLabel("Box", "Polyester", "Red", "6ft"));
        Assert.Equal("Polyester · Red · 6ft", ItemService.ComposeLabel("", "Polyester", "Red", "6ft"));
        Assert.Equal("Medium", ItemService.ComposeLabel("", "", "", "Medium"));
    }

    [Fact]
    public void Colours_are_kept_only_for_cloths_the_type_has_cleaned_and_in_the_cloths_own_spelling()
    {
        var cloths = new List<string> { "Cotton", "Polyester" };
        var result = ProductTypeService.CleanColourSets(cloths, new List<ClothColourSet>
        {
            new() { Cloth = "cotton", Colours = new() { " White ", "Blue", "white" } },
            new() { Cloth = "Polyester", Colours = new() { "Red", "", "Green" } },
            new() { Cloth = "Nylon", Colours = new() { "Black" } },          // not one of the type's cloths
            new() { Cloth = "Cotton", Colours = new() { "Duplicate set" } }, // a second set for the same cloth
        });
        Assert.Equal(2, result.Count);
        Assert.Equal("Cotton", result[0].Cloth);
        Assert.Equal(new[] { "White", "Blue" }, result[0].Colours);
        Assert.Equal(new[] { "Red", "Green" }, result[1].Colours);
        Assert.Empty(ProductTypeService.CleanColourSets(cloths, new List<ClothColourSet> { new() { Cloth = "Cotton", Colours = new() } }));
    }
}

/// <summary>Runs the real API in memory. These check the client ordering login end to end, above all that it can never see prices or other people's data.</summary>
[Collection("api")]
public class PortalTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();
    private static HttpClient As(AppFactory f, string token)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private static async Task<string> OwnerAsync(AppFactory f, HttpClient c)
    {
        var reg = await Json(await c.PostAsJsonAsync("/api/auth/register", new { name = "Owner", email = "owner@test.local", password = "password123", businessName = "Test Co" }));
        var ok = await c.PostAsJsonAsync("/api/auth/verify-registration", new { challengeId = reg.GetProperty("challengeId").GetString(), code = f.Sender.LastCode() });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        return (await Json(ok)).GetProperty("token").GetString()!;
    }

    private static async Task<string> NewClientAsync(HttpClient owner, string name, string email)
    {
        var r = await owner.PostAsJsonAsync("/api/clients", new
        {
            name, email, isActive = true, cities = new[] { "Koppa", "Udupi" },
            billingAddress = new { stateCode = "29" },
        });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await Json(r)).GetProperty("id").GetString()!;
    }

    /// <summary>Owner gives the client a login; the client sets a password with "Forgot password?" and logs in with an emailed code.</summary>
    private static async Task<string> ClientLoginAsync(AppFactory f, HttpClient anon, HttpClient owner, string clientId, string email)
    {
        var made = await owner.PostAsync($"/api/clients/{clientId}/portal", null);
        Assert.Equal(HttpStatusCode.OK, made.StatusCode);
        Assert.True((await Json(made)).GetProperty("exists").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsJsonAsync("/api/auth/forgot-password", new { email })).StatusCode);
        var reset = await anon.PostAsJsonAsync("/api/auth/reset-password", new { email, code = f.Sender.LastCode(), newPassword = "clientpass123" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var login = await Json(await anon.PostAsJsonAsync("/api/auth/login", new { email, password = "clientpass123" }));
        var verify = await anon.PostAsJsonAsync("/api/auth/verify-otp", new { challengeId = login.GetProperty("challengeId").GetString(), code = f.Sender.LastCode() });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        return (await Json(verify)).GetProperty("token").GetString()!;
    }

    private static async Task<string> NewItemAsync(HttpClient owner)
    {
        var r = await owner.PostAsJsonAsync("/api/items", new
        {
            name = "Pillow", sizeOrVariant = "Medium", unit = "PCS", gstRate = 18, defaultRate = 1234.5, isActive = true,
            specialRates = Array.Empty<object>(),
        });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await Json(r)).GetProperty("id").GetString()!;
    }

    private static async Task<List<(string To, string Subject, string Body)>> MailsAsync(AppFactory f, string subjectPart, int count)
    {
        for (var i = 0; i < 50; i++)   // order emails are sent in the background
        {
            List<(string To, string Subject, string Body)> hit;
            lock (f.Sender.Sent) hit = f.Sender.Sent.Where(m => m.Subject.Contains(subjectPart)).ToList();
            if (hit.Count >= count) return hit;
            await Task.Delay(100);
        }
        lock (f.Sender.Sent) return f.Sender.Sent.Where(m => m.Subject.Contains(subjectPart)).ToList();
    }

    private record Setup(AppFactory F, HttpClient Anon, HttpClient Owner, HttpClient Client, string ItemId, string ClientId);

    private static async Task<Setup> SetupAsync(AppFactory f)
    {
        var anon = f.CreateClient();
        var owner = As(f, await OwnerAsync(f, anon));
        var itemId = await NewItemAsync(owner);
        var clientId = await NewClientAsync(owner, "Sathyanatha Cloth Store", "client@test.local");
        var client = As(f, await ClientLoginAsync(f, anon, owner, clientId, "client@test.local"));
        return new Setup(f, anon, owner, client, itemId, clientId);
    }

    private static object Order(string itemId, decimal qty = 3) => new { lines = new[] { new { itemId, quantity = qty } }, city = "Udupi", note = "Deliver Monday" };

    [Fact]
    public async Task A_client_sees_products_without_prices_orders_and_both_sides_get_the_item_list_by_email()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);

        var cat = await s.Client.GetAsync("/api/portal/catalog");
        Assert.Equal(HttpStatusCode.OK, cat.StatusCode);
        var raw = await cat.Content.ReadAsStringAsync();
        Assert.Contains("Pillow", raw);
        foreach (var banned in new[] { "rate", "price", "gst", "1234.5", "discount", "special" })
            Assert.DoesNotContain(banned, raw, StringComparison.OrdinalIgnoreCase);

        var placed = await s.Client.PostAsJsonAsync("/api/portal/orders", Order(s.ItemId));
        Assert.Equal(HttpStatusCode.OK, placed.StatusCode);
        var o = await Json(placed);
        Assert.Equal("ORD-0001", o.GetProperty("orderNumber").GetString());
        Assert.Equal("New", o.GetProperty("status").GetString());
        Assert.Equal("Udupi", o.GetProperty("city").GetString());
        Assert.DoesNotContain("1234.5", await placed.Content.ReadAsStringAsync());

        // an email to the client and one to the owner, each listing the items, neither with a price
        var mails = await MailsAsync(f, "ORD-0001", 2);
        Assert.Contains(mails, m => m.To == "client@test.local");
        Assert.Contains(mails, m => m.To == "owner@test.local");
        foreach (var m in mails)
        {
            Assert.Contains("Pillow", m.Body);
            Assert.Contains("x 3 PCS", m.Body);
            Assert.DoesNotContain("1234.5", m.Body);
        }

        var mine = await Json(await s.Client.GetAsync("/api/portal/orders"));
        Assert.Equal(1, mine.GetArrayLength());
    }

    [Fact]
    public async Task A_client_login_is_refused_by_every_owner_and_staff_screen()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);

        foreach (var url in new[]
        {
            "/api/items", "/api/clients", "/api/invoices", "/api/business", "/api/dashboard", "/api/orders", "/api/orders/summary",
            "/api/team", "/api/email/templates", "/api/activity", "/api/backup/export", "/api/template", "/api/catalog",
            "/api/reports/sales?from=2026-01-01&to=2026-12-31",
        })
        {
            var r = await s.Client.GetAsync(url);
            Assert.True(r.StatusCode == HttpStatusCode.Forbidden, $"{url} returned {(int)r.StatusCode} for a client login");
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Client.PostAsJsonAsync("/api/clients", new { name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Client.PostAsJsonAsync("/api/invoices", new { })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await s.Client.GetAsync("/api/auth/me")).StatusCode);   // their own account is fine
    }

    [Fact]
    public async Task The_owner_cannot_use_the_ordering_endpoints()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Owner.GetAsync("/api/portal/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Owner.PostAsJsonAsync("/api/portal/orders", Order(s.ItemId))).StatusCode);
    }

    [Fact]
    public async Task Orders_reach_the_owners_bucket_and_a_bill_can_be_made_from_one()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);
        var id = (await Json(await s.Client.PostAsJsonAsync("/api/portal/orders", Order(s.ItemId)))).GetProperty("id").GetString()!;

        Assert.Equal(1, (await Json(await s.Owner.GetAsync("/api/orders/summary"))).GetProperty("newCount").GetInt32());
        var list = await Json(await s.Owner.GetAsync("/api/orders?status=New"));
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal("Sathyanatha Cloth Store", list.GetProperty("items")[0].GetProperty("clientName").GetString());

        var accepted = await s.Owner.PatchAsJsonAsync($"/api/orders/{id}/status", new { status = "Accepted" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("Accepted", (await Json(accepted)).GetProperty("status").GetString());

        var bill = await s.Owner.PostAsJsonAsync("/api/invoices", new
        {
            invoiceDate = DateTime.UtcNow.Date, clientId = s.ClientId, city = "Udupi", orderId = id, shipToSameAsBillTo = true,
            placeOfSupplyStateCode = "29", finalize = false, lines = new[] { new { itemId = s.ItemId, quantity = 3 } },
        });
        Assert.Equal(HttpStatusCode.OK, bill.StatusCode);
        var invoiceNumber = (await Json(bill)).GetProperty("invoiceNumber").GetString();

        var after = await Json(await s.Owner.GetAsync($"/api/orders/{id}"));
        Assert.Equal("Billed", after.GetProperty("status").GetString());
        Assert.Equal(invoiceNumber, after.GetProperty("invoiceNumber").GetString());

        // the same order can't be billed twice
        var again = await s.Owner.PostAsJsonAsync("/api/invoices", new
        {
            invoiceDate = DateTime.UtcNow.Date, clientId = s.ClientId, orderId = id, shipToSameAsBillTo = true,
            lines = new[] { new { itemId = s.ItemId, quantity = 3 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

        // and the client sees only the status, never the bill
        var clientView = await s.Client.GetAsync($"/api/portal/orders/{id}");
        var raw = await clientView.Content.ReadAsStringAsync();
        Assert.Contains("Billed", raw);
        Assert.DoesNotContain(invoiceNumber!, raw);
    }

    [Fact]
    public async Task One_client_cannot_see_or_cancel_another_clients_order()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);
        var otherId = await NewClientAsync(s.Owner, "Other Traders", "other@test.local");
        var other = As(f, await ClientLoginAsync(f, s.Anon, s.Owner, otherId, "other@test.local"));   // both logins first: order emails go out in the background

        var id = (await Json(await s.Client.PostAsJsonAsync("/api/portal/orders", Order(s.ItemId)))).GetProperty("id").GetString()!;

        Assert.Equal(0, (await Json(await other.GetAsync("/api/portal/orders"))).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/portal/orders/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/portal/orders/{id}/cancel", null)).StatusCode);

        // the owner of the order can cancel it while it is new
        var cancelled = await s.Client.PostAsync($"/api/portal/orders/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("Cancelled", (await Json(cancelled)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Switching_the_login_off_ends_access_at_once()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);
        Assert.Equal(HttpStatusCode.OK, (await s.Client.GetAsync("/api/portal/catalog")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await s.Owner.PatchAsJsonAsync($"/api/clients/{s.ClientId}/portal/active", new { active = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await s.Client.GetAsync("/api/portal/catalog")).StatusCode);
    }

    [Fact]
    public async Task Bad_orders_are_refused()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);

        Assert.Equal(HttpStatusCode.BadRequest, (await s.Client.PostAsJsonAsync("/api/portal/orders", new { lines = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Client.PostAsJsonAsync("/api/portal/orders", Order(s.ItemId, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Client.PostAsJsonAsync("/api/portal/orders", Order(s.ItemId, 99999))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Client.PostAsJsonAsync("/api/portal/orders", Order("000000000000000000000000"))).StatusCode);

        // a hidden product can't be ordered and is not listed
        Assert.Equal(HttpStatusCode.NoContent, (await s.Owner.PatchAsJsonAsync($"/api/items/{s.ItemId}/active", new { active = false })).StatusCode);
        Assert.Equal(0, (await Json(await s.Client.GetAsync("/api/portal/catalog"))).GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Client.PostAsJsonAsync("/api/portal/orders", Order(s.ItemId))).StatusCode);
    }

    [Fact]
    public async Task The_open_screens_can_see_a_new_order_to_ring_for_and_a_client_cannot()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);

        var before = await Json(await s.Owner.GetAsync("/api/orders/watch"));
        Assert.Equal(0, before.GetProperty("newCount").GetInt32());
        Assert.Equal(0, before.GetProperty("fresh").GetArrayLength());

        await s.Client.PostAsJsonAsync("/api/portal/orders", Order(s.ItemId));

        var after = await Json(await s.Owner.GetAsync("/api/orders/watch"));
        Assert.Equal(1, after.GetProperty("newCount").GetInt32());
        Assert.Equal(1, after.GetProperty("fresh").GetArrayLength());
        Assert.Equal("ORD-0001", after.GetProperty("fresh")[0].GetProperty("orderNumber").GetString());

        Assert.Equal(HttpStatusCode.Forbidden, (await s.Client.GetAsync("/api/orders/watch")).StatusCode);
    }

    [Fact]
    public async Task Accepting_or_cancelling_an_order_emails_the_client_with_the_items()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);

        var a = (await Json(await s.Client.PostAsJsonAsync("/api/portal/orders", Order(s.ItemId)))).GetProperty("id").GetString()!;
        var b = (await Json(await s.Client.PostAsJsonAsync("/api/portal/orders", Order(s.ItemId, 5)))).GetProperty("id").GetString()!;
        await MailsAsync(f, "We received your order ORD-0002", 1);   // let the two "placed" emails finish first

        Assert.Equal(HttpStatusCode.OK, (await s.Owner.PatchAsJsonAsync($"/api/orders/{a}/status", new { status = "Accepted" })).StatusCode);
        var accepted = await MailsAsync(f, "Your order ORD-0001 is accepted", 1);
        Assert.Single(accepted);
        Assert.Equal("client@test.local", accepted[0].To);
        Assert.Contains("Pillow", accepted[0].Body);
        Assert.DoesNotContain("1234.5", accepted[0].Body);

        Assert.Equal(HttpStatusCode.OK, (await s.Owner.PatchAsJsonAsync($"/api/orders/{b}/status", new { status = "Cancelled", reason = "Out of stock" })).StatusCode);
        var cancelled = await MailsAsync(f, "Your order ORD-0002 was cancelled", 1);
        Assert.Single(cancelled);
        Assert.Equal("client@test.local", cancelled[0].To);
        Assert.Contains("Out of stock", cancelled[0].Body);
        Assert.Contains("Pillow", cancelled[0].Body);
    }

    [Fact]
    public async Task A_client_cancelling_their_own_order_does_not_send_the_staff_cancelled_email()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);
        var id = (await Json(await s.Client.PostAsJsonAsync("/api/portal/orders", Order(s.ItemId)))).GetProperty("id").GetString()!;
        await MailsAsync(f, "ORD-0001", 2);

        Assert.Equal(HttpStatusCode.OK, (await s.Client.PostAsync($"/api/portal/orders/{id}/cancel", null)).StatusCode);
        await Task.Delay(800);
        lock (f.Sender.Sent) Assert.DoesNotContain(f.Sender.Sent, m => m.Subject.Contains("was cancelled"));
    }

    [Fact]
    public async Task Colours_belong_to_a_cloth_and_reach_the_item_the_ordering_page_and_the_order_email()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);

        var made = await s.Owner.PostAsJsonAsync("/api/catalog", new
        {
            name = "Bed", usesVariants = false, variants = Array.Empty<string>(), clothTypes = new[] { "Cotton", "Polyester" }, sizes = new[] { "6ft" },
            clothColours = new object[]
            {
                new { cloth = "cotton", colours = new[] { "White", "Blue" } },
                new { cloth = "Polyester", colours = new[] { "Red", "Red ", "Green" } },
                new { cloth = "Nylon", colours = new[] { "Black" } },
            },
        });
        Assert.Equal(HttpStatusCode.OK, made.StatusCode);
        var type = await Json(made);
        var typeId = type.GetProperty("id").GetString()!;
        var sets = type.GetProperty("clothColours");
        Assert.Equal(2, sets.GetArrayLength());                                  // Nylon is not one of the type's cloths
        Assert.Equal("Cotton", sets[0].GetProperty("cloth").GetString());        // the cloth's own spelling
        Assert.Equal(2, sets[1].GetProperty("colours").GetArrayLength());        // "Red" and "Red " are one colour

        async Task<JsonElement> ItemAsync(string cloth, string colour) => await Json(await s.Owner.PostAsJsonAsync("/api/items", new
        {
            typeId, cloth, colour, size = "6ft", unit = "PCS", gstRate = 18, defaultRate = 500, isActive = true, specialRates = Array.Empty<object>(),
        }));
        var red = await ItemAsync("Polyester", "Red");
        Assert.Equal("Polyester · Red · 6ft", red.GetProperty("sizeOrVariant").GetString());
        Assert.Equal("Red", red.GetProperty("colour").GetString());
        var blue = await ItemAsync("Cotton", "Blue");
        Assert.Equal("Cotton · Blue · 6ft", blue.GetProperty("sizeOrVariant").GetString());

        // the client sees the colour on the ordering page, still without a price
        var catalog = await s.Client.GetAsync("/api/portal/catalog");
        var raw = await catalog.Content.ReadAsStringAsync();
        Assert.Contains("Polyester · Red · 6ft", raw.Replace("\u00b7", "·"));
        Assert.Contains("\"colour\":\"Red\"", raw);
        Assert.DoesNotContain("1234.5", raw);
        Assert.DoesNotContain("500", raw.Replace(typeId, ""));

        var order = await s.Client.PostAsJsonAsync("/api/portal/orders", new { lines = new[] { new { itemId = red.GetProperty("id").GetString(), quantity = 2 } }, city = "Udupi" });
        Assert.Equal(HttpStatusCode.OK, order.StatusCode);
        var mails = await MailsAsync(f, "ORD-0001", 2);
        Assert.All(mails, m => Assert.Contains("Polyester · Red", m.Body));
    }

    private static object Phone(string clientId, string itemId, string priority = "Normal", bool emailClient = false, bool emailMe = false) =>
        new { clientId, lines = new[] { new { itemId, quantity = 4 } }, city = "Koppa", note = "Phoned in", priority, emailClient, emailMe };

    [Fact]
    public async Task Staff_can_take_a_phone_order_which_starts_accepted_and_does_not_ring_the_alert()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);

        var r = await s.Owner.PostAsJsonAsync("/api/orders", Phone(s.ClientId, s.ItemId, "Urgent"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var o = await Json(r);
        Assert.Equal("Accepted", o.GetProperty("status").GetString());
        Assert.Equal("Phone", o.GetProperty("source").GetString());
        Assert.Equal("Urgent", o.GetProperty("priority").GetString());
        Assert.Equal("Owner", o.GetProperty("takenBy").GetString());

        var watch = await Json(await s.Owner.GetAsync("/api/orders/watch"));
        Assert.Equal(0, watch.GetProperty("fresh").GetArrayLength());   // nothing to ring for
        Assert.Equal(0, watch.GetProperty("newCount").GetInt32());

        // the client sees it under their own orders, with the priority and no prices
        var mine = await Json(await s.Client.GetAsync("/api/portal/orders"));
        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal("Urgent", mine[0].GetProperty("priority").GetString());
    }

    [Fact]
    public async Task A_phone_order_emails_only_who_was_ticked()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);
        await s.Owner.PostAsJsonAsync("/api/orders", Phone(s.ClientId, s.ItemId, "Normal", emailClient: true, emailMe: false));
        var mails = await MailsAsync(f, "ORD-0001", 1);
        await Task.Delay(500);   // give a wrong second mail the chance to show up
        mails = await MailsAsync(f, "ORD-0001", 1);
        Assert.Single(mails);
        Assert.Equal("client@test.local", mails[0].To);

        await s.Owner.PostAsJsonAsync("/api/orders", Phone(s.ClientId, s.ItemId, "Normal", emailClient: false, emailMe: false));
        await Task.Delay(500);
        lock (f.Sender.Sent) Assert.DoesNotContain(f.Sender.Sent, m => m.Subject.Contains("ORD-0002"));
    }

    [Fact]
    public async Task Urgent_orders_are_listed_first_and_a_client_can_set_the_priority_of_their_own_order()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);
        await s.Client.PostAsJsonAsync("/api/portal/orders", Order(s.ItemId));   // ORD-0001 Normal, oldest
        var urgent = await s.Client.PostAsJsonAsync("/api/portal/orders", new { lines = new[] { new { itemId = s.ItemId, quantity = 2 } }, city = "Udupi", note = "", priority = "Urgent" });
        Assert.Equal("Urgent", (await Json(urgent)).GetProperty("priority").GetString());
        await s.Client.PostAsJsonAsync("/api/portal/orders", Order(s.ItemId));   // ORD-0003 Normal, newest

        var list = await Json(await s.Owner.GetAsync("/api/orders?status=New"));
        Assert.Equal("ORD-0002", list.GetProperty("items")[0].GetProperty("orderNumber").GetString());
        Assert.Equal("ORD-0003", list.GetProperty("items")[1].GetProperty("orderNumber").GetString());
    }

    [Fact]
    public async Task A_phone_order_needs_a_real_client_and_products()
    {
        using var f = new AppFactory();
        var s = await SetupAsync(f);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Owner.PostAsJsonAsync("/api/orders", Phone("000000000000000000000000", s.ItemId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Owner.PostAsJsonAsync("/api/orders", new { clientId = s.ClientId, lines = Array.Empty<object>() })).StatusCode);
        // a client login can't take orders for anyone
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Client.PostAsJsonAsync("/api/orders", Phone(s.ClientId, s.ItemId))).StatusCode);
    }
}
