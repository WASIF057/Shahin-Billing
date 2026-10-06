namespace ShahinBilling.Api.Dtos;

public record MonthlySales(string Month, decimal Sales);
/// <summary>A row of the "top" list: a client, or a city when one client is selected.</summary>
public record TopClient(string ClientId, string Name, decimal Sales, int Bills);

public record PaymentBucket(int Bills, decimal Total, decimal Received, decimal Balance);
public record PaymentBreakdown(PaymentBucket Paid, PaymentBucket PartPaid, PaymentBucket Unpaid);

/// <summary>ThisMonthSales / LastMonthSales hold the chosen period and the one before it
/// (this month and last month when no dates are chosen).</summary>
public record DashboardDto(
    decimal ThisMonthSales,
    decimal LastMonthSales,
    decimal UnpaidAmount,
    int UnpaidBills,
    string FinancialYear,
    IReadOnlyList<MonthlySales> MonthlySales,
    IReadOnlyList<TopClient> TopClients,
    IReadOnlyList<InvoiceListItem> RecentBills,
    string PeriodLabel,
    string PreviousLabel,
    bool CustomRange,
    string TopGroupBy,
    int PeriodBills,
    PaymentBreakdown Payments);
