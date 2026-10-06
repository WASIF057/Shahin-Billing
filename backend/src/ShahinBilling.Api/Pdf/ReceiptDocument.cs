using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Pdf;

/// <summary>A receipt for one payment recorded on a bill.</summary>
public class ReceiptDocument(Invoice inv, Payment pay, int number, decimal balanceAfter, string primaryColor) : IDocument
{
    private BusinessSnapshot B => inv.BusinessSnapshot;
    private readonly string _primary = System.Text.RegularExpressions.Regex.IsMatch(primaryColor ?? "", "^#[0-9A-Fa-f]{6}$") ? primaryColor! : "#1F4E79";

    public DocumentMetadata GetMetadata() => new() { Title = $"Receipt {inv.InvoiceNumber}/R{number}", Author = B.Name };
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    private static string Rs(decimal v) => Money.FormatIndian(v);

    private static byte[]? Image(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;
        try { var i = dataUrl.IndexOf(','); return Convert.FromBase64String(i >= 0 ? dataUrl[(i + 1)..] : dataUrl); }
        catch { return null; }
    }

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A5.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));
            page.Content().Border(1f).BorderColor(_primary).Padding(14).Column(col =>
            {
                col.Spacing(8);
                var logo = Image(B.Logo);
                col.Item().Row(row =>
                {
                    if (logo != null) row.ConstantItem(54).Height(48).Image(logo).FitArea();
                    row.RelativeItem().PaddingLeft(logo != null ? 10 : 0).Column(x =>
                    {
                        x.Item().Text(B.Name).FontSize(14).Bold();
                        var addr = string.Join(", ", new[] { B.Address.City, B.Address.State }.Where(s => !string.IsNullOrWhiteSpace(s)));
                        var contact = string.Join("   ", new[] { addr, B.Phone }.Where(s => !string.IsNullOrWhiteSpace(s)));
                        if (contact.Length > 0) x.Item().Text(contact);
                    });
                    row.ConstantItem(200).AlignRight().Column(x =>
                    {
                        x.Item().AlignRight().Text("PAYMENT RECEIPT").FontSize(14).Bold().FontColor(_primary);
                        x.Item().AlignRight().Text($"Receipt no.: {inv.InvoiceNumber}/R{number}").SemiBold();
                        x.Item().AlignRight().Text($"Date: {pay.Date:dd-MM-yyyy}");
                    });
                });
                col.Item().LineHorizontal(1f).LineColor(_primary);

                col.Item().Text(t =>
                {
                    t.Span("Received with thanks from ");
                    t.Span(inv.BillTo.Name).Bold();
                    if (!string.IsNullOrWhiteSpace(inv.BillTo.Address.City)) t.Span($" ({inv.BillTo.Address.City})");
                    t.Span(" the sum of");
                });
                col.Item().Background(Colors.Grey.Lighten4).Padding(8).Column(x =>
                {
                    x.Item().Text($"Rs. {Rs(pay.Amount)}").FontSize(20).Bold().FontColor(_primary);
                    x.Item().Text(AmountInWords.Convert(pay.Amount)).FontSize(9);
                });

                col.Item().Text(t =>
                {
                    t.Span("Towards invoice ");
                    t.Span(inv.InvoiceNumber).Bold();
                    t.Span($" dated {inv.InvoiceDate:dd-MM-yyyy} (invoice total Rs. {Rs(inv.Totals.GrandTotal)}).");
                });
                col.Item().Text(t =>
                {
                    t.Span("Paid by ").SemiBold(); t.Span(pay.Mode);
                    if (!string.IsNullOrWhiteSpace(pay.Reference)) { t.Span("   Reference: ").SemiBold(); t.Span(pay.Reference); }
                });
                if (!string.IsNullOrWhiteSpace(pay.Note)) col.Item().Text($"Note: {pay.Note}");
                col.Item().Text(t =>
                {
                    t.Span("Balance due after this payment: ").SemiBold();
                    t.Span($"Rs. {Rs(balanceAfter)}").Bold();
                    if (balanceAfter <= 0) t.Span("   (fully paid)").FontColor(_primary).SemiBold();
                });

                col.Item().ExtendVertical().AlignBottom().AlignRight().Width(190).Column(x =>
                {
                    x.Item().AlignRight().Text($"For {B.Name}").SemiBold();
                    var sig = Image(B.Signature);
                    if (sig != null) x.Item().Height(40).AlignRight().Image(sig).FitArea(); else x.Item().Height(40);
                    x.Item().AlignRight().Text(string.IsNullOrEmpty(B.AuthorisedSignatoryName) ? "Authorised Signatory" : $"{B.AuthorisedSignatoryName}\nAuthorised Signatory");
                });
            });
        });
    }
}
