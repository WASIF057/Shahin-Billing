using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

/// <summary>The orders "bucket" for the owner and staff.</summary>
public class OrdersController(OrderService service, EmailService email) : StaffApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<Order>> List([FromQuery] OrderQuery q) => service.ListAsync(BusinessId, q);

    /// <summary>How many orders are waiting (shown as a badge in the menu).</summary>
    [HttpGet("summary")]
    public async Task<object> Summary() => new { newCount = await service.CountNewAsync(BusinessId) };

    /// <summary>For the open screens: how many are waiting, and any order placed in the last 10 minutes (so they can pop up and ring).</summary>
    [HttpGet("watch")]
    public async Task<object> Watch() => new { newCount = await service.CountNewAsync(BusinessId), fresh = await service.RecentNewAsync(BusinessId) };

    /// <summary>Take an order over the phone for a client. Optionally emails the client and/or the business a copy.</summary>
    [HttpPost]
    public async Task<Order> Take(TakeOrderRequest req)
    {
        var o = await service.TakeAsync(BusinessId, UserId, UserName, req);
        await email.NotifyOrderAsync(BusinessId, o, req.EmailClient, req.EmailMe);
        await Log("order.taken", "Order", o.Id, $"{UserName} took order {o.OrderNumber} for {o.ClientName} by phone ({o.Lines.Count} product{(o.Lines.Count == 1 ? "" : "s")}, {o.Priority})");
        return o;
    }

    [HttpGet("{id}")]
    public Task<Order> Get(string id) => service.GetAsync(BusinessId, id);

    [HttpPatch("{id}/status")]
    public async Task<Order> SetStatus(string id, OrderStatusRequest req)
    {
        var o = await service.SetStatusAsync(BusinessId, id, req.Status, req.Reason, UserName);
        // The client is emailed about it (when those templates are switched on)
        await email.NotifyOrderStatusAsync(BusinessId, o, req.Status == OrderStatus.Accepted ? EmailTemplate.OnOrderAccepted : EmailTemplate.OnOrderCancelled);
        await Log(req.Status == OrderStatus.Accepted ? "order.accepted" : "order.cancelled", "Order", o.Id,
            req.Status == OrderStatus.Accepted ? $"Accepted order {o.OrderNumber} from {o.ClientName}" : $"Cancelled order {o.OrderNumber} from {o.ClientName}. Reason: {o.CancelReason}");
        return o;
    }
}
