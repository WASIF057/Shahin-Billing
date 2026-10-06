using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Auth;
using ShahinBilling.Api.Dtos;
using ShahinBilling.Api.Infrastructure;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

/// <summary>Bring clients or items in from an Excel file. kind = "clients" or "items".</summary>
[Authorize(Roles = AppRoles.Owner)]
public class ImportController(ImportService service) : StaffApiControllerBase
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static string Kind(string kind) =>
        kind is "clients" or "items" ? kind : throw new NotFoundException("That kind of import");

    [HttpGet("{kind}/template")]
    public async Task<IActionResult> Template(string kind)
    {
        kind = Kind(kind);
        return File(await service.TemplateAsync(BusinessId, kind), Xlsx, $"import-{kind}-template.xlsx");
    }

    /// <summary>dryRun=true only checks the file and reports each row; dryRun=false saves the good rows.</summary>
    [HttpPost("{kind}")]
    [RequestSizeLimit(2_000_000)]
    public async Task<ImportResult> Import(string kind, IFormFile file, [FromQuery] bool dryRun = true)
    {
        kind = Kind(kind);
        if (file == null || file.Length == 0) throw new AppException("Choose an Excel file first.");
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) throw new AppException("Use an Excel file that ends in .xlsx.");

        await using var stream = file.OpenReadStream();
        var result = kind == "clients"
            ? await service.ImportClientsAsync(BusinessId, stream, dryRun)
            : await service.ImportItemsAsync(BusinessId, stream, dryRun);
        if (!dryRun)
            await Log("import." + kind, "Import", null, $"Imported {kind} from Excel: {result.Ok} added, {result.Skipped} skipped, {result.Errors} with problems");
        return result;
    }
}
