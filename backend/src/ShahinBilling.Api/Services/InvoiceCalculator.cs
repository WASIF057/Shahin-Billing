using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

/// <summary>Business rules 4.2 and 4.3. Pure functions, fully unit tested.</summary>
public static class InvoiceCalculator
{
    public record Result(List<InvoiceLine> Lines, InvoiceTotals Totals, List<GstSummaryRow> Summary, string AmountInWords);

    public static bool IsInterState(string businessStateCode, string placeOfSupplyStateCode) =>
        !string.IsNullOrEmpty(businessStateCode) && !string.IsNullOrEmpty(placeOfSupplyStateCode)
        && businessStateCode != placeOfSupplyStateCode;

    /// <summary>Fills Amount, TaxableValue, taxes and LineTotal on each line, then totals.
    /// Uses Quantity, Rate, Discount and GstRate from the given lines.</summary>
    public static Result Calculate(IEnumerable<InvoiceLine> input, bool isInterState)
    {
        var lines = input.ToList();
        foreach (var l in lines)
        {
            l.Amount = Money.Round2(l.Quantity * l.Rate);
            l.TaxableValue = Money.Round2(l.Amount - l.Discount);
            if (isInterState)
            {
                l.Igst = Money.Round2(l.TaxableValue * l.GstRate / 100m);
                l.Cgst = 0;
                l.Sgst = 0;
            }
            else
            {
                l.Cgst = Money.Round2(l.TaxableValue * (l.GstRate / 2m) / 100m);
                l.Sgst = l.Cgst;
                l.Igst = 0;
            }
            l.LineTotal = l.TaxableValue + l.Cgst + l.Sgst + l.Igst;
        }

        var totals = new InvoiceTotals
        {
            TotalQuantity = lines.Sum(l => l.Quantity),
            DiscountTotal = lines.Sum(l => l.Discount),
            TaxableTotal = lines.Sum(l => l.TaxableValue),
            CgstTotal = lines.Sum(l => l.Cgst),
            SgstTotal = lines.Sum(l => l.Sgst),
            IgstTotal = lines.Sum(l => l.Igst)
        };
        var exact = totals.TaxableTotal + totals.CgstTotal + totals.SgstTotal + totals.IgstTotal;
        // No rounding to the whole rupee: the grand total is the exact total (paise kept). Round off stays 0 on new bills.
        totals.GrandTotal = exact;
        totals.RoundOff = 0;

        var summary = lines
            .GroupBy(l => l.GstRate)
            .OrderBy(g => g.Key)
            .Select(g => new GstSummaryRow
            {
                GstRate = g.Key,
                TaxableValue = g.Sum(l => l.TaxableValue),
                Cgst = g.Sum(l => l.Cgst),
                Sgst = g.Sum(l => l.Sgst),
                Igst = g.Sum(l => l.Igst)
            })
            .ToList();

        return new Result(lines, totals, summary, Helpers.AmountInWords.Convert(totals.GrandTotal));
    }

    /// <summary>Recomputes paid / balance / status from the payments list.</summary>
    public static void ApplyPayments(Invoice inv)
    {
        inv.AmountPaid = inv.Payments.Sum(p => p.Amount);
        inv.BalanceDue = inv.Totals.GrandTotal - inv.AmountPaid;
        inv.PaymentStatus =
            inv.AmountPaid <= 0 ? PaymentStatus.Unpaid :
            inv.AmountPaid >= inv.Totals.GrandTotal ? PaymentStatus.Paid :
            PaymentStatus.PartlyPaid;
    }
}
