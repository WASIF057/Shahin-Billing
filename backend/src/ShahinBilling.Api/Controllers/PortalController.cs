using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using ShahinBilling.Api.Auth;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

/// <summary>The ordering website for a client. Accepts only a client's login. Everything here is about that one client:
/// the client id comes from their token, never from the request, and nothing here carries a price.</summary>
[Authorize(Roles = AppRoles.Client)]
public class PortalController(MongoContext db, ItemService items, OrderService orders, EmailService email) : ApiControllerBase
{
    private string ClientId => User.FindFirst("clientId")?.Value ?? throw new AppException("This login isn't linked to a client.", 403);

    private async Task<Client> MyClientAsync()
    {
        var c = await db.Clients.Find(x => x.BusinessId == BusinessId && x.Id == ClientId).FirstOrDefaultAsync()
                ?? throw new AppException("Your account is no longer available. Please contact us.", 403);
        if (!c.IsActive) throw new AppException("Ordering is switched off for your account. Please contact us.", 403);
        return c;
    }

    [HttpGet("profile")]
    public async Task<PortalProfile> Profile()
    {
        var c = await MyClientAsync();
        var business = await db.Businesses.Find(b => b.Id == BusinessId).FirstOrDefaultAsync();
        return new PortalProfile(c.Name, business?.Name ?? "", c.Cities);
    }

    /// <summary>The products a client can order: names, variants, cloth and sizes. No rates, no GST.</summary>
    [HttpGet("catalog")]
    public async Task<List<PortalCatalogItem>> Catalog()
    {
        await MyClientAsync();
        var list = await items.ListAsync(BusinessId, null, true);
        return list.Select(i => new PortalCatalogItem(i.Id, i.TypeName, i.Name, i.Variant, i.Cloth, i.Colour, i.Size, i.Unit, OrderService.Label(i)))
                   .OrderBy(i => i.Type).ThenBy(i => i.Name).ThenBy(i => i.Variant).ThenBy(i => i.Cloth).ThenBy(i => i.Colour).ThenBy(i => i.Size).ToList();
    }

    [HttpPost("orders")]
    public async Task<PortalOrderDto> Place(PlaceOrderRequest req)
    {
        await MyClientAsync();
        var order = await orders.PlaceAsync(BusinessId, ClientId, UserId, UserName, User.FindFirst("email")?.Value ?? "", req);
        await email.NotifyOrderAsync(BusinessId, order);
        await Log("order.placed", "Order", order.Id, $"{order.ClientName} placed order {order.OrderNumber} ({order.Lines.Count} product{(order.Lines.Count == 1 ? "" : "s")})");
        return OrderService.ToPortal(order);
    }

    [HttpGet("orders")]
    public async Task<List<PortalOrderDto>> MyOrders()
    {
        await MyClientAsync();
        return (await orders.ForClientAsync(BusinessId, ClientId)).Select(OrderService.ToPortal).ToList();
    }

    [HttpGet("orders/{id}")]
    public async Task<PortalOrderDto> MyOrder(string id)
    {
        await MyClientAsync();
        return OrderService.ToPortal(await orders.GetForClientAsync(BusinessId, ClientId, id));
    }

    [HttpPost("orders/{id}/cancel")]
    public async Task<PortalOrderDto> Cancel(string id)
    {
        await MyClientAsync();
        var o = await orders.CancelByClientAsync(BusinessId, ClientId, id);
        await Log("order.cancelled", "Order", o.Id, $"{o.ClientName} cancelled order {o.OrderNumber}");
        return OrderService.ToPortal(o);
    }
}
