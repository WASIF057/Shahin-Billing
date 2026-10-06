using Microsoft.AspNetCore.Authorization;
using ShahinBilling.Api.Auth;
using Microsoft.AspNetCore.Mvc;
using ShahinBilling.Api.Services;

namespace ShahinBilling.Api.Controllers;

[Authorize(Roles = AppRoles.Owner)]
public class BackupController(BackupExporter exporter, IConfiguration config, IWebHostEnvironment env) : StaffApiControllerBase
{
    /// <summary>Everything for this business as one JSON file (passwords are never included).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var bytes = await exporter.BuildAsync(BusinessId);
        await Log("backup.downloaded", "Backup", null, "Downloaded a backup of all data");
        return File(bytes, "application/json", $"shahin-billing-backup-{DateTime.UtcNow:yyyyMMdd-HHmm}.json");
    }

    /// <summary>Whether the daily automatic backup is on, and when it last ran for this business.</summary>
    [HttpGet("status")]
    public object Status()
    {
        var enabled = BackupHostedService.IsEnabled(config);
        var last = enabled ? BackupHostedService.LatestFor(BackupHostedService.Folder(config, env), BusinessId) : null;
        return new { enabled, lastBackupAt = last, keep = BackupHostedService.Keep(config) };
    }
}
