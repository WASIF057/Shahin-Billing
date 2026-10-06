using Microsoft.AspNetCore.Authorization;
using ShahinBilling.Api.Auth;
using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Reports;

namespace ShahinBilling.Api.Controllers;

[Authorize(Roles = AppRoles.Owner)]
public class ReportsController(ReportService reports) : StaffApiControllerBase
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>?month=2026-09</summary>
    [HttpGet("gstr1")]
    public async Task<IActionResult> Gstr1([FromQuery] string month)
    {
        if (!DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var m))
            throw new AppException("Choose a month.");
        var bytes = await reports.Gstr1Async(BusinessId, m.Year, m.Month);
        return File(bytes, Xlsx, $"GSTR1-{month}.xlsx");
    }

    /// <summary>One client's bills in a date range. ?city= limits it to one of the client's cities.</summary>
    [HttpGet("client")]
    public async Task<IActionResult> Client([FromQuery] string clientId, [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? city)
    {
        if (to < from) throw new AppException("The end date must be after the start date.");
        var (bytes, name) = await reports.ClientAsync(BusinessId, clientId, from, to, city);
        var safe = string.Concat(name.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        var cityPart = string.IsNullOrWhiteSpace(city) ? "" : "-" + string.Concat(city.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        return File(bytes, Xlsx, $"Client-{safe}{cityPart}-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx");
    }

    /// <summary>The same as /reports/client, as a PDF statement of account you can send to the client.</summary>
    [HttpGet("client-statement")]
    public async Task<IActionResult> ClientStatement([FromQuery] string clientId, [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? city)
    {
        if (to < from) throw new AppException("The end date must be after the start date.");
        var (bytes, name) = await reports.ClientStatementAsync(BusinessId, clientId, from, to, city);
        var safe = string.Concat(name.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        return File(bytes, "application/pdf", $"Statement-{safe}-{from:yyyyMMdd}-{to:yyyyMMdd}.pdf");
    }

    [HttpGet("sales")]
    public async Task<IActionResult> Sales([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (to < from) throw new AppException("The end date must be after the start date.");
        var bytes = await reports.SalesAsync(BusinessId, from, to);
        return File(bytes, Xlsx, $"Sales-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx");
    }
}
