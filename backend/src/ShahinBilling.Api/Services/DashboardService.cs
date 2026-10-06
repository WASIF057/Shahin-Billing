using System.Globalization;
using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

public class DashboardService(MongoContext db)
{
    public record Period(DateTime Start, DateTime EndExclusive, DateTime PrevStart, DateTime PrevEndExclusive, bool Custom);

    /// <summary>No dates: this month against last month. With dates: that range against the range of the same length just before it.</summary>
    public static Period ComputePeriod(DateTime today, DateTime? from, DateTime? to)
    {
        if (from == null && to == null)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            return new Period(monthStart, monthStart.AddMonths(1), monthStart.AddMonths(-1), monthStart, false);
        }
        var end = DateTime.SpecifyKind((to ?? today).Date, DateTimeKind.Utc);
        var start = DateTime.SpecifyKind((from ?? FinancialYear.StartDate(end)).Date, DateTimeKind.Utc);
        var endExclusive = end.AddDays(1);
        var length = endExclusive - start;
        return new Period(start, endExclusive, start - length, start, true);
    }

    public async Task<DashboardDto> GetAsync(string businessId, string? clientId = null, string? city = null, DateTime? from = null, DateTime? to = null)
    {
        var today = DateTime.UtcNow.Date;
        var fyStart = FinancialYear.StartDate(today);
        var fyEnd = FinancialYear.EndDateExclusive(today);
        var p = ComputePeriod(today, from, to);

        // Small business volumes: load the matching bills and aggregate in memory (simple and readable).
        var f = Builders<Invoice>.Filter;
        var filter = f.Eq(x => x.BusinessId, businessId);
        if (!string.IsNullOrWhiteSpace(clientId)) filter &= f.Eq(x => x.ClientId, clientId);
        if (!string.IsNullOrWhiteSpace(city)) filter &= f.Eq(x => x.BillTo.Address.City, city.Trim());
        var all = await db.Invoices.Find(filter & f.Eq(x => x.Status, InvoiceStatus.Final)).ToListAsync();

        decimal Sales(DateTime a, DateTime b) => all.Where(x => x.InvoiceDate >= a && x.InvoiceDate < b).Sum(x => x.Totals.GrandTotal);
        var inPeriod = all.Where(x => x.InvoiceDate >= p.Start && x.InvoiceDate < p.EndExclusive).ToList();

        // Payment figures: the chosen period, or every bill when no dates are chosen
        var basis = p.Custom ? inPeriod : all;
        PaymentBucket Bucket(PaymentStatus s)
        {
            var l = basis.Where(x => x.PaymentStatus == s).ToList();
            return new PaymentBucket(l.Count, l.Sum(x => x.Totals.GrandTotal), l.Sum(x => x.AmountPaid), l.Sum(x => x.BalanceDue));
        }
        var payments = new PaymentBreakdown(Bucket(PaymentStatus.Paid), Bucket(PaymentStatus.PartlyPaid), Bucket(PaymentStatus.Unpaid));
        var unpaid = basis.Where(x => x.PaymentStatus != PaymentStatus.Paid).ToList();

        // Chart: the financial year, or the months of the chosen range (latest 36 at most)
        DateTime chartStart, chartEnd;
        if (p.Custom)
        {
            chartStart = new DateTime(p.Start.Year, p.Start.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            chartEnd = p.EndExclusive.AddDays(-1);
            var months = (chartEnd.Year - chartStart.Year) * 12 + chartEnd.Month - chartStart.Month + 1;
            if (months > 36) chartStart = chartStart.AddMonths(months - 36);
        }
        else { chartStart = fyStart; chartEnd = fyEnd.AddDays(-1); }
        var monthCount = (chartEnd.Year - chartStart.Year) * 12 + chartEnd.Month - chartStart.Month + 1;
        var monthly = Enumerable.Range(0, Math.Max(monthCount, 1)).Select(i =>
        {
            var m = chartStart.AddMonths(i);
            var sales = all.Where(b => b.InvoiceDate.Year == m.Year && b.InvoiceDate.Month == m.Month
                                       && b.InvoiceDate >= (p.Custom ? p.Start : fyStart) && b.InvoiceDate < (p.Custom ? p.EndExclusive : fyEnd))
                .Sum(b => b.Totals.GrandTotal);
            return new MonthlySales(m.ToString("MMM yy", CultureInfo.InvariantCulture), sales);
        }).ToList();

        // Top list: clients, or the cities of one client when a client is chosen
        var topBills = p.Custom ? inPeriod : all.Where(b => b.InvoiceDate >= fyStart && b.InvoiceDate < fyEnd).ToList();
        var byCity = !string.IsNullOrWhiteSpace(clientId);
        var top = byCity
            ? topBills.GroupBy(b => string.IsNullOrWhiteSpace(b.BillTo.Address.City) ? "(no city)" : b.BillTo.Address.City, StringComparer.OrdinalIgnoreCase)
                .Select(g => new TopClient(clientId!, g.Key, g.Sum(b => b.Totals.GrandTotal), g.Count()))
                .OrderByDescending(t => t.Sales).Take(5).ToList()
            : topBills.GroupBy(b => b.ClientId)
                .Select(g => new TopClient(g.Key, g.First().BillTo.Name, g.Sum(b => b.Totals.GrandTotal), g.Count()))
                .OrderByDescending(t => t.Sales).Take(5).ToList();

        // Latest bills (any status) that match the filters
        var recentFilter = filter;
        if (p.Custom) recentFilter &= f.Gte(x => x.InvoiceDate, p.Start) & f.Lt(x => x.InvoiceDate, p.EndExclusive);
        var recent = await db.Invoices.Find(recentFilter).SortByDescending(x => x.CreatedAt).Limit(5).ToListAsync();

        var label = p.Custom
            ? $"{p.Start:dd-MM-yyyy} to {p.EndExclusive.AddDays(-1):dd-MM-yyyy}"
            : "this month";
        return new DashboardDto(
            Sales(p.Start, p.EndExclusive), Sales(p.PrevStart, p.PrevEndExclusive),
            unpaid.Sum(u => u.BalanceDue), unpaid.Count, FinancialYear.Label(today), monthly, top,
            recent.Select(InvoiceService.ToListItem).ToList(),
            label, p.Custom ? "the previous period" : "last month", p.Custom, byCity ? "city" : "client", inPeriod.Count, payments);
    }
}
