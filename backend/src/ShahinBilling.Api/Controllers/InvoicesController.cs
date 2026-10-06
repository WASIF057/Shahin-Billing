using Microsoft.AspNetCore.Authorization;
using ShahinBilling.Api.Auth;
using FluentValidation;
using QuestPDF.Fluent;
using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Models;
using ShahinBilling.Api.Pdf;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

public class InvoicesController(
    InvoiceService service, BusinessService businesses, InvoicePdfService pdf, EmailService email, OrderService orders, IValidator<InvoiceRequest> validator)
    : StaffApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<InvoiceListItem>> List([FromQuery] InvoiceQuery q) => service.ListAsync(BusinessId, q);

    [HttpGet("{id}")]
    public Task<Invoice> Get(string id) => service.GetAsync(BusinessId, id);

    [HttpPost]
    public async Task<Invoice> Create(InvoiceRequest req)
    {
        await validator.ValidateAndThrowAsync(req);
        // A bill made from an order: check the order first, so nothing is saved against a wrong or already-used order
        var fromOrder = string.IsNullOrWhiteSpace(req.OrderId) ? null : await orders.ForBillingAsync(BusinessId, req.OrderId, req.ClientId);
        var inv = await service.CreateAsync(BusinessId, req);
        if (fromOrder != null)
        {
            await orders.MarkBilledAsync(BusinessId, fromOrder.Id, inv);
            await Log("order.billed", "Order", fromOrder.Id, $"Order {fromOrder.OrderNumber} billed as {inv.InvoiceNumber}");
        }
        await email.NotifyAsync(BusinessId, inv, "generated");   // only sends if the bill was saved as Final
        await Log(inv.Status == InvoiceStatus.Final ? "bill.generated" : "bill.draft", "Bill", inv.Id,
            $"{(inv.Status == InvoiceStatus.Final ? "Generated" : "Saved a draft of")} bill {inv.InvoiceNumber} for {inv.BillTo.Name}, Rs. {Money.FormatIndian(inv.Totals.GrandTotal)}");
        return inv;
    }

    [HttpPut("{id}")]
    public async Task<Invoice> Update(string id, InvoiceRequest req)
    {
        await validator.ValidateAndThrowAsync(req);
        var wasFinal = (await service.GetAsync(BusinessId, id)).Status == InvoiceStatus.Final;
        var inv = await service.UpdateAsync(BusinessId, id, req);
        if (!wasFinal) await email.NotifyAsync(BusinessId, inv, "generated");   // draft just became final
        await Log("bill.edited", "Bill", inv.Id, $"Edited bill {inv.InvoiceNumber} ({inv.BillTo.Name}), now Rs. {Money.FormatIndian(inv.Totals.GrandTotal)}{(!wasFinal && inv.Status == InvoiceStatus.Final ? ", finalized" : "")}");
        return inv;
    }

    /// <summary>The number the next new bill will get.</summary>
    [HttpGet("next-number")]
    public async Task<object> NextNumber([FromQuery] bool nonGst = false) => new { number = await service.NextNumberAsync(BusinessId, nonGst) };

    /// <summary>Calculates a bill without saving it (server-side check of the live totals).</summary>
    [HttpPost("calculate")]
    public Task<Invoice> Calculate(InvoiceRequest req) => service.BuildAsync(BusinessId, req);

    /// <summary>Renders the unsaved bill as a PDF (the "View bill" button). Nothing is saved and no number is used.</summary>
    [HttpPost("preview-pdf")]
    public async Task<IActionResult> PreviewPdf(InvoiceRequest req, [FromQuery] string? invoiceId = null)
    {
        await validator.ValidateAndThrowAsync(req);
        var existing = string.IsNullOrEmpty(invoiceId) ? null : await service.GetAsync(BusinessId, invoiceId);
        var inv = await service.BuildAsync(BusinessId, req, existing);
        var business = await businesses.GetAsync(BusinessId);
        if (existing == null) inv.InvoiceNumber = await service.NextNumberAsync(BusinessId, req.NonGst);   // a saved bill keeps its own number
        inv.CreatedAt = DateTime.UtcNow;
        return File(pdf.Generate(inv, business.Template, null), "application/pdf");
    }

    [HttpPost("{id}/finalize")]
    public async Task<Invoice> Finalize(string id)
    {
        var wasFinal = (await service.GetAsync(BusinessId, id)).Status == InvoiceStatus.Final;
        var inv = await service.FinalizeAsync(BusinessId, id);
        if (!wasFinal) await email.NotifyAsync(BusinessId, inv, "generated");
        await Log("bill.finalized", "Bill", inv.Id, $"Finalized bill {inv.InvoiceNumber} for {inv.BillTo.Name}");
        return inv;
    }

    [Authorize(Roles = AppRoles.Owner)]
    [HttpPost("{id}/cancel")]
    public async Task<Invoice> Cancel(string id, CancelRequest req)
    {
        var inv = await service.CancelAsync(BusinessId, id, req.Reason);
        await Log("bill.cancelled", "Bill", inv.Id, $"Cancelled bill {inv.InvoiceNumber} ({inv.BillTo.Name}). Reason: {req.Reason}");
        return inv;
    }

    /// <summary>Emails a payment reminder for this bill now.</summary>
    [HttpPost("{id}/remind")]
    public async Task<object> Remind(string id)
    {
        var inv = await service.GetAsync(BusinessId, id);
        var to = await email.SendReminderAsync(BusinessId, inv);
        await Log("bill.reminder", "Bill", inv.Id, $"Sent a payment reminder for bill {inv.InvoiceNumber} ({inv.BillTo.Name}) to {string.Join(", ", to)}");
        return new { message = $"Reminder sent to {string.Join(", ", to)}." };
    }

    [HttpPost("{id}/duplicate")]
    public async Task<Invoice> Duplicate(string id)
    {
        var inv = await service.DuplicateAsync(BusinessId, id);
        await Log("bill.duplicated", "Bill", inv.Id, $"Made a draft copy ({inv.InvoiceNumber}) of another bill for {inv.BillTo.Name}");
        return inv;
    }

    [HttpPost("{id}/payments")]
    public async Task<Invoice> AddPayment(string id, PaymentRequest req, [FromServices] IValidator<PaymentRequest> v)
    {
        await v.ValidateAndThrowAsync(req);
        var before = (await service.GetAsync(BusinessId, id)).PaymentStatus;
        var inv = await service.AddPaymentAsync(BusinessId, id, req);
        await email.NotifyPaymentAsync(BusinessId, inv, before);   // emails only if the payment status changed
        await Log("payment.added", "Payment", inv.Id, $"Recorded Rs. {Money.FormatIndian(req.Amount)} ({req.Mode}) on bill {inv.InvoiceNumber}, now {EmailService.StatusText(inv.PaymentStatus)}");
        return inv;
    }

    [Authorize(Roles = AppRoles.Owner)]
    [HttpDelete("{id}/payments/{paymentId}")]
    public async Task<Invoice> RemovePayment(string id, string paymentId)
    {
        var before = (await service.GetAsync(BusinessId, id)).PaymentStatus;
        var inv = await service.RemovePaymentAsync(BusinessId, id, paymentId);
        await email.NotifyPaymentAsync(BusinessId, inv, before);
        await Log("payment.removed", "Payment", inv.Id, $"Removed a payment from bill {inv.InvoiceNumber}, now {EmailService.StatusText(inv.PaymentStatus)}");
        return inv;
    }

    /// <summary>A receipt PDF for one payment on the bill.</summary>
    [HttpGet("{id}/payments/{paymentId}/receipt")]
    public async Task<IActionResult> Receipt(string id, string paymentId)
    {
        var inv = await service.GetAsync(BusinessId, id);
        var index = inv.Payments.FindIndex(p => p.Id == paymentId);
        if (index < 0) throw new ShahinBilling.Api.Infrastructure.NotFoundException("Payment");
        var business = await businesses.GetAsync(BusinessId);
        var balanceAfter = inv.Totals.GrandTotal - inv.Payments.Take(index + 1).Sum(p => p.Amount);
        var doc = new ReceiptDocument(inv, inv.Payments[index], index + 1, balanceAfter, (inv.TemplateOverride ?? business.Template).PrimaryColor);
        var bytes = doc.GeneratePdf();
        return File(bytes, "application/pdf", $"Receipt-{inv.InvoiceNumber.Replace('/', '-')}-R{index + 1}.pdf");
    }

    /// <summary>?copies=original,duplicate,triplicate (default: the Bill Format setting). ?inline=true opens in the browser.</summary>
    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> Pdf(string id, [FromQuery] string? copies, [FromQuery] bool inline = false, [FromQuery] bool notify = false)
    {
        var inv = await service.GetAsync(BusinessId, id);
        var business = await businesses.GetAsync(BusinessId);
        var template = inv.TemplateOverride ?? business.Template;
        var bytes = pdf.Generate(inv, template, copies?.Split(',', StringSplitOptions.RemoveEmptyEntries));
        var name = $"{inv.InvoiceNumber.Replace('/', '-')}.pdf";
        if (notify) await email.NotifyAsync(BusinessId, inv, "downloaded");   // the Download buttons ask for this; previews don't
        if (inline)
        {
            Response.Headers.ContentDisposition = $"inline; filename=\"{name}\"";
            return File(bytes, "application/pdf");
        }
        return File(bytes, "application/pdf", name);
    }
}
