using ClosedXML.Excel;
using FluentValidation;
using MongoDB.Driver;
using ShahinBilling.Api.Data;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Helpers;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Services;

/// <summary>Imports clients and items from an Excel file (.xlsx). The same rules as typing them in apply:
/// a "check" run (dryRun) reports every row without saving anything, and the real run saves only the rows that are fine.</summary>
public class ImportService(MongoContext db, ClientService clients, ItemService items, IValidator<Client> clientValidator, IValidator<Item> itemValidator)
{
    public const int MaxRows = 1000;
    private static readonly string[] Truthy = { "yes", "y", "true", "1", "x" };

    // ---------------------------------------------------------------- reading the sheet
    private sealed class Sheet
    {
        public IXLWorksheet Ws = null!;
        public Dictionary<string, int> Cols = new(StringComparer.OrdinalIgnoreCase);
        public int LastRow;

        /// <summary>The text of a column in a row, trimmed. Tries each header name given.</summary>
        public string Get(int row, params string[] names)
        {
            foreach (var n in names)
                if (Cols.TryGetValue(n, out var c)) return Ws.Cell(row, c).GetString().Trim();
            return "";
        }
        public bool Has(params string[] names) => names.Any(n => Cols.ContainsKey(n));
    }

    private static Sheet Open(Stream file)
    {
        XLWorkbook wb;
        try { wb = new XLWorkbook(file); }
        catch { throw new AppException("That file could not be read. Use an Excel file (.xlsx), ideally the template."); }
        var ws = wb.Worksheets.FirstOrDefault() ?? throw new AppException("The file has no sheets.");
        var s = new Sheet { Ws = ws, LastRow = ws.LastRowUsed()?.RowNumber() ?? 1 };
        foreach (var cell in ws.Row(1).CellsUsed()) s.Cols[cell.GetString().Trim().TrimEnd('*').Trim()] = cell.Address.ColumnNumber;
        if (s.LastRow - 1 > MaxRows) throw new AppException($"The file has more than {MaxRows} rows. Split it into smaller files.");
        return s;
    }

    private static bool BlankRow(Sheet s, int r) => s.Cols.Values.All(c => string.IsNullOrWhiteSpace(s.Ws.Cell(r, c).GetString()));
    private static string Err(IEnumerable<FluentValidation.Results.ValidationFailure> f) => string.Join(" ", f.Select(x => x.ErrorMessage).Distinct());

    private static ImportResult Done(List<ImportRow> rows, bool imported) =>
        new(rows.Count, rows.Count(r => r.Status == "Ok"), rows.Count(r => r.Status == "Skipped"), rows.Count(r => r.Status == "Error"), imported, rows);

    // ---------------------------------------------------------------- clients
    public async Task<ImportResult> ImportClientsAsync(string businessId, Stream file, bool dryRun)
    {
        var s = Open(file);
        if (!s.Has("Name")) throw new AppException("Row 1 needs a \u201cName\u201d column. Download the template to see the layout.");

        var existing = await db.Clients.Find(c => c.BusinessId == businessId).ToListAsync();
        var names = new HashSet<string>(existing.Select(c => c.Name.Trim()), StringComparer.OrdinalIgnoreCase);
        var gstins = new HashSet<string>(existing.Where(c => c.Gstin.Length > 0).Select(c => c.Gstin), StringComparer.OrdinalIgnoreCase);

        var rows = new List<ImportRow>();
        for (var r = 2; r <= s.LastRow; r++)
        {
            if (BlankRow(s, r)) continue;
            var name = s.Get(r, "Name", "Client", "Client Name");
            try
            {
                if (name.Length == 0) throw new AppException("The name is empty.");
                var gstin = s.Get(r, "GSTIN").ToUpperInvariant().Replace(" ", "");
                var stateText = s.Get(r, "State");
                var stateCode = ResolveState(stateText, gstin);

                var cities = s.Get(r, "Cities", "City").Split(new[] { ',', ';', '\n', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var client = new Client
                {
                    Name = name, Gstin = gstin, Pan = s.Get(r, "PAN").ToUpperInvariant(), Phone = s.Get(r, "Mobile", "Phone", "Mobile Number"),
                    Email = s.Get(r, "Email"), Cities = cities, Notes = s.Get(r, "Notes"),
                    BilledWithoutGst = Truthy.Contains(s.Get(r, "Billed without GST", "Without GST").ToLowerInvariant()),
                    BillingAddress = new Address { StateCode = stateCode, State = StateCodes.NameOf(stateCode), City = cities.FirstOrDefault() ?? "" },
                    ShippingSameAsBilling = true, IsActive = true,
                };

                var check = await clientValidator.ValidateAsync(client);
                if (!check.IsValid) throw new AppException(Err(check.Errors));

                if (names.Contains(name) || (gstin.Length > 0 && gstins.Contains(gstin)))
                { rows.Add(new ImportRow(r, "Skipped", "A client with this name or GSTIN already exists.", name)); continue; }
                names.Add(name); if (gstin.Length > 0) gstins.Add(gstin);

                if (!dryRun) await clients.CreateAsync(businessId, client);
                rows.Add(new ImportRow(r, "Ok", dryRun ? "Will be added." : "Added.", $"{name}{(cities.Count > 0 ? " · " + string.Join(", ", cities) : "")}"));
            }
            catch (AppException ex) { rows.Add(new ImportRow(r, "Error", ex.Message, name)); }
        }
        return Done(rows, !dryRun);
    }

    /// <summary>A state typed as a name ("Karnataka") or a code ("29"). Falls back to the GSTIN's first two digits.</summary>
    public static string ResolveState(string text, string gstin)
    {
        if (text.Length > 0)
        {
            var code = text.PadLeft(2, '0');
            if (StateCodes.All.ContainsKey(code)) return code;
            var hit = StateCodes.All.FirstOrDefault(kv => kv.Value.Equals(text, StringComparison.OrdinalIgnoreCase));
            if (hit.Key != null) return hit.Key;
            throw new AppException($"Unknown state \u201c{text}\u201d. Use the state's name (for example Karnataka) or its 2-digit code.");
        }
        var fromGstin = StateCodes.FromGstin(gstin);
        if (fromGstin.Length > 0 && StateCodes.All.ContainsKey(fromGstin)) return fromGstin;
        throw new AppException("The state is needed (or a valid GSTIN to take it from).");
    }

    // ---------------------------------------------------------------- items
    public async Task<ImportResult> ImportItemsAsync(string businessId, Stream file, bool dryRun)
    {
        var s = Open(file);
        if (!s.Has("Type")) throw new AppException("Row 1 needs a \u201cType\u201d column. Download the template to see the layout.");

        var types = await db.ProductTypes.Find(t => t.BusinessId == businessId).ToListAsync();
        var existing = await db.Items.Find(i => i.BusinessId == businessId).ToListAsync();
        static string Key(string typeId, string variant, string cloth, string colour, string size) => $"{typeId}|{variant}|{cloth}|{colour}|{size}".ToLowerInvariant();
        var seen = new HashSet<string>(existing.Where(i => i.TypeId.Length > 0).Select(i => Key(i.TypeId, i.Variant, i.Cloth, i.Colour, i.Size)));

        var rows = new List<ImportRow>();
        for (var r = 2; r <= s.LastRow; r++)
        {
            if (BlankRow(s, r)) continue;
            var typeText = s.Get(r, "Type");
            try
            {
                if (typeText.Length == 0) throw new AppException("The type is empty.");
                var type = types.FirstOrDefault(t => t.Name.Equals(typeText, StringComparison.OrdinalIgnoreCase))
                           ?? throw new AppException($"The type \u201c{typeText}\u201d isn't set up. Add it in Item setup first.");

                var variant = Pick(s.Get(r, "Variant"), type.UsesVariants ? type.Variants : null, "variant", type.Name, noneAllowedMessage: $"{type.Name} has no variants.");
                var cloth = Pick(s.Get(r, "Cloth"), type.ClothTypes.Count > 0 ? type.ClothTypes : null, "cloth", type.Name, noneAllowedMessage: $"{type.Name} has no cloth types.");
                // Colours belong to a cloth: they must be one of the colours of the cloth chosen on this row
                var coloursOfCloth = type.ClothColours.FirstOrDefault(c => c.Cloth.Equals(cloth, StringComparison.OrdinalIgnoreCase))?.Colours;
                var colour = Pick(s.Get(r, "Colour", "Color"), coloursOfCloth is { Count: > 0 } ? coloursOfCloth : null, "colour",
                    cloth.Length > 0 ? $"{type.Name} ({cloth})" : type.Name,
                    noneAllowedMessage: cloth.Length > 0 ? $"{cloth} has no colours." : "Choose the cloth first: colours belong to a cloth.");
                var size = Pick(s.Get(r, "Size"), type.Sizes.Count > 0 ? type.Sizes : null, "size", type.Name, noneAllowedMessage: $"{type.Name} has no sizes.");

                var rate = ParseNumber(s.Get(r, "Default rate", "Rate"), "default rate", required: true)!.Value;
                var gst = ParseNumber(s.Get(r, "GST %", "GST", "GST rate"), "GST %", required: false) ?? 18m;
                var unit = s.Get(r, "Unit");

                var item = new Item
                {
                    TypeId = type.Id, Name = type.Name, Variant = variant, Cloth = cloth, Colour = colour, Size = size,
                    Unit = unit.Length > 0 ? unit : "PCS", GstRate = gst, DefaultRate = rate, IsActive = true,
                };
                var check = await itemValidator.ValidateAsync(item);
                if (!check.IsValid) throw new AppException(Err(check.Errors));

                var label = string.Join(" · ", new[] { type.Name, variant, cloth, colour, size }.Where(x => x.Length > 0));
                var key = Key(type.Id, variant, cloth, colour, size);
                if (!seen.Add(key)) { rows.Add(new ImportRow(r, "Skipped", "This item already exists.", label)); continue; }

                if (!dryRun) await items.CreateAsync(businessId, item);
                rows.Add(new ImportRow(r, "Ok", dryRun ? "Will be added." : "Added.", $"{label} · Rs. {Money.FormatIndian(rate)}"));
            }
            catch (AppException ex) { rows.Add(new ImportRow(r, "Error", ex.Message, typeText)); }
        }
        return Done(rows, !dryRun);
    }

    /// <summary>A typed value must be one of the type's choices (matched ignoring capitals; the stored spelling is used). Empty is fine.</summary>
    public static string Pick(string value, IReadOnlyList<string>? options, string label, string typeName, string noneAllowedMessage)
    {
        if (value.Length == 0) return "";
        if (options == null) throw new AppException(noneAllowedMessage + " Leave the " + label + " empty.");
        return options.FirstOrDefault(o => o.Equals(value, StringComparison.OrdinalIgnoreCase))
               ?? throw new AppException($"\u201c{value}\u201d isn't a {label} of {typeName}. Add it in Item setup first. Choices: {string.Join(", ", options)}.");
    }

    public static decimal? ParseNumber(string text, string label, bool required)
    {
        if (text.Length == 0) return required ? throw new AppException($"The {label} is empty.") : null;
        var clean = text.Replace(",", "").Replace("\u20b9", "").Replace("Rs.", "", StringComparison.OrdinalIgnoreCase).Replace("%", "").Trim();
        if (!decimal.TryParse(clean, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v))
            throw new AppException($"The {label} \u201c{text}\u201d is not a number.");
        return v;
    }

    // ---------------------------------------------------------------- templates
    public async Task<byte[]> TemplateAsync(string businessId, string kind)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(kind == "clients" ? "Clients" : "Items");
        string[] head; string[][] samples;
        if (kind == "clients")
        {
            head = new[] { "Name*", "GSTIN", "PAN", "State", "Mobile", "Email", "Cities", "Billed without GST", "Notes" };
            samples = new[]
            {
                new[] { "Sathyanatha Cloth Store", "29ABLPP6364N1ZP", "ABLPP6364N", "Karnataka", "9845000000", "store@example.com", "Koppa, Bhrammawara", "No", "" },
                new[] { "Sample Traders", "", "", "Karnataka", "9900000000", "", "Udupi", "Yes", "Cash customer" },
            };
        }
        else
        {
            head = new[] { "Type*", "Variant", "Cloth", "Colour", "Size", "Unit", "GST %", "Default rate*" };
            var t = (await db.ProductTypes.Find(x => x.BusinessId == businessId).SortBy(x => x.Name).ToListAsync()).FirstOrDefault();
            samples = new[]
            {
                new[] { t?.Name ?? "Bed", t != null && t.UsesVariants ? t.Variants.FirstOrDefault() ?? "" : "", t?.ClothTypes.FirstOrDefault() ?? "", t?.ClothColours.FirstOrDefault()?.Colours.FirstOrDefault() ?? "", t?.Sizes.FirstOrDefault() ?? "", "PCS", "18", "400" },
            };
        }
        for (var i = 0; i < head.Length; i++) ws.Cell(1, i + 1).Value = head[i];
        for (var r = 0; r < samples.Length; r++)
            for (var c = 0; c < samples[r].Length; c++) ws.Cell(r + 2, c + 1).Value = samples[r][c];
        var h = ws.Row(1);
        h.Style.Font.Bold = true;
        h.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        var note = wb.Worksheets.Add("How to fill this");
        var lines = kind == "clients"
            ? new[]
              {
                  "Columns marked * are required. Delete the example rows before importing your own.",
                  "State: the state's name (Karnataka) or its 2-digit code. If you leave it empty, it is taken from a valid GSTIN.",
                  "Cities: the cities this client has stores in, separated by commas.",
                  "Billed without GST: Yes or No. Yes makes new bills for this client start as Non-GST bills.",
                  "A client whose name or GSTIN already exists is skipped.",
              }
            : new[]
              {
                  "Columns marked * are required. Delete the example row before importing your own.",
                  "Type must already exist in Item setup. Variant, Cloth, Colour and Size must be choices of that type, and a Colour must be one of the colours of the Cloth on the same row (capitals do not matter).",
                  "Unit defaults to PCS and GST % to 18 when left empty. Default rate is before GST.",
                  "An item with the same type, variant, cloth, colour and size that already exists is skipped.",
              };
        for (var i = 0; i < lines.Length; i++) note.Cell(i + 1, 1).Value = lines[i];
        note.Column(1).Width = 110;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
