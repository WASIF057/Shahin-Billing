using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Pdf;

/// <summary>Statement of account: a client's finalized bills in a date range, what was received and what is still due.</summary>
public class StatementDocument(Business b, Client client, string city, DateTime from, DateTime to, List<Invoice> bills) : IDocument
{
    private readonly string _primary = System.Text.RegularExpressions.Regex.IsMatch(b.Template?.PrimaryColor ?? "", "^#[0-9A-Fa-f]{6}$")
        ? b.Template!.PrimaryColor : "#1F4E79";

    public DocumentMetadata GetMetadata() => new() { Title = $"Statement - {client.Name}", Author = b.Name };
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
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Colors.Grey.Darken4));
            page.Header().Element(Header);
            page.Content().PaddingTop(10).Element(Content);
            page.Footer().AlignRight().Text(x =>
            {
                x.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                x.Span("Page "); x.CurrentPageNumber(); x.Span(" of "); x.TotalPages();
            });
        });
    }

    private void Header(IContainer c)
    {
        var logo = Image(b.Logo);
        c.Column(col =>
        {
            col.Item().Text("STATEMENT OF ACCOUNT").FontSize(16).Bold().FontColor(_primary);
            col.Item().PaddingTop(6).Row(row =>
            {
                if (logo != null) row.ConstantItem(60).Height(54).Image(logo).FitArea();
                row.RelativeItem().PaddingLeft(logo != null ? 10 : 0).Column(x =>
                {
                    x.Item().Text(b.Name).FontSize(13).Bold();
                    var addr = string.Join(", ", new[] { b.Address.Line1, b.Address.Line2, b.Address.City, b.Address.State, b.Address.Pincode }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    if (addr.Length > 0) x.Item().Text(addr);
                    var contact = string.Join("   ", new[] { b.Phone, b.Email }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    if (contact.Length > 0) x.Item().Text(contact);
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(1.2f).LineColor(_primary);
        });
    }

    private void Content(IContainer c)
    {
        var billed = bills.Sum(x => x.Totals.GrandTotal);
        var received = bills.Sum(x => x.AmountPaid);
        var due = bills.Sum(x => x.BalanceDue);

        c.Column(col =>
        {
            col.Spacing(10);
            col.Item().Row(row =>
            {
                row.RelativeItem().Border(0.6f).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(x =>
                {
                    x.Item().Text("Client").FontSize(8).FontColor(_primary).SemiBold();
                    x.Item().Text(client.Name).Bold().FontSize(11);
                    if (!string.IsNullOrWhiteSpace(city)) x.Item().Text(city);
                    if (!string.IsNullOrWhiteSpace(client.Phone)) x.Item().Text($"Mobile: {client.Phone}");
                });
                row.ConstantItem(10);
                row.RelativeItem().Border(0.6f).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(x =>
                {
                    x.Item().Text("Period").FontSize(8).FontColor(_primary).SemiBold();
                    x.Item().Text($"{from:dd-MM-yyyy} to {to:dd-MM-yyyy}").Bold().FontSize(11);
                    x.Item().Text($"Statement date: {DateTime.UtcNow.AddHours(5.5):dd-MM-yyyy}");
                });
            });

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(24); cd.RelativeColumn(1.3f); cd.RelativeColumn(1.6f);
                    cd.RelativeColumn(1.4f); cd.RelativeColumn(1.4f); cd.RelativeColumn(1.4f);
                });
                IContainer Head(IContainer x) => x.Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4);
                IContainer Cell(IContainer x) => x.Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4);
                t.Header(h =>
                {
                    h.Cell().Element(Head).Text("#").SemiBold();
                    h.Cell().Element(Head).Text("Date").SemiBold();
                    h.Cell().Element(Head).Text("Bill no.").SemiBold();
                    h.Cell().Element(Head).AlignRight().Text("Amount").SemiBold();
                    h.Cell().Element(Head).AlignRight().Text("Received").SemiBold();
                    h.Cell().Element(Head).AlignRight().Text("Balance").SemiBold();
                });
                var i = 1;
                foreach (var x in bills)
                {
                    t.Cell().Element(Cell).Text((i++).ToString());
                    t.Cell().Element(Cell).Text(x.InvoiceDate.ToString("dd-MM-yyyy"));
                    t.Cell().Element(Cell).Text(x.InvoiceNumber);
                    t.Cell().Element(Cell).AlignRight().Text(Rs(x.Totals.GrandTotal));
                    t.Cell().Element(Cell).AlignRight().Text(Rs(x.AmountPaid));
                    t.Cell().Element(Cell).AlignRight().Text(Rs(x.BalanceDue)).SemiBold();
                }
                if (bills.Count == 0)
                    t.Cell().ColumnSpan(6).Element(Cell).AlignCenter().Text("No bills in this period.").FontColor(Colors.Grey.Darken1);
                else
                {
                    t.Cell().ColumnSpan(3).Element(Cell).Text($"Total ({bills.Count} bill{(bills.Count == 1 ? "" : "s")})").Bold();
                    t.Cell().Element(Cell).AlignRight().Text(Rs(billed)).Bold();
                    t.Cell().Element(Cell).AlignRight().Text(Rs(received)).Bold();
                    t.Cell().Element(Cell).AlignRight().Text(Rs(due)).Bold();
                }
            });

            col.Item().AlignRight().Width(260).Column(x =>
            {
                void Line(string l, string v, bool strong = false)
                    => x.Item().PaddingVertical(2).Row(r =>
                    {
                        var a = r.RelativeItem().Text(l); var z = r.ConstantItem(110).AlignRight().Text(v);
                        if (strong) { a.Bold().FontSize(11).FontColor(_primary); z.Bold().FontSize(11).FontColor(_primary); }
                    });
                Line("Total billed", "Rs. " + Rs(billed));
                Line("Total received", "Rs. " + Rs(received));
                x.Item().PaddingTop(2).LineHorizontal(1).LineColor(_primary);
                Line("Balance due", "Rs. " + Rs(due), strong: true);
            });

            if (due > 0 && !string.IsNullOrWhiteSpace(b.Bank.AccountNumber))
                col.Item().ShowEntire().Border(1.2f).BorderColor(Colors.Black).Padding(8).DefaultTextStyle(s => s.Bold()).Column(x =>
                {
                    x.Item().Text("Please pay the balance to:").FontColor(_primary);
                    void L(string l, string v) { if (!string.IsNullOrWhiteSpace(v)) x.Item().Text($"{l}: {v}"); }
                    L("Account name", b.Bank.AccountName);
                    L("Account no.", b.Bank.AccountNumber);
                    L("IFSC", b.Bank.Ifsc);
                    L("Bank", string.Join(", ", new[] { b.Bank.BankName, b.Bank.Branch }.Where(s => !string.IsNullOrWhiteSpace(s))));
                    L("UPI ID", b.Bank.UpiId);
                });
        });
    }
}
