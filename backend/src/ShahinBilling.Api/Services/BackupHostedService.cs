using MongoDB.Driver;
using ShahinBilling.Api.Data;

namespace ShahinBilling.Api.Services;

/// <summary>Saves a backup file of every business once a day, on the server, and keeps the latest 14 of each.
/// Settings: Backup:Enabled (default true), Backup:Folder (default "backups" next to the app), Backup:Keep (default 14).</summary>
public class BackupHostedService(
    MongoContext db, BackupExporter exporter, IConfiguration config, IWebHostEnvironment env, ILogger<BackupHostedService> log) : BackgroundService
{
    public static string Folder(IConfiguration config, IWebHostEnvironment env)
    {
        var f = config["Backup:Folder"];
        return string.IsNullOrWhiteSpace(f) ? Path.Combine(env.ContentRootPath, "backups") : f;
    }

    public static bool IsEnabled(IConfiguration config) => config.GetValue("Backup:Enabled", true);
    public static int Keep(IConfiguration config) => Math.Max(config.GetValue("Backup:Keep", 14), 1);

    /// <summary>When the newest backup file of this business was written (UTC), or null if there is none.</summary>
    public static DateTime? LatestFor(string folder, string businessId)
    {
        if (!Directory.Exists(folder)) return null;
        var files = new DirectoryInfo(folder).GetFiles($"backup-{businessId}-*.json");
        return files.Length == 0 ? null : files.Max(f => f.LastWriteTimeUtc);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!IsEnabled(config)) return;
        try { await Task.Delay(TimeSpan.FromSeconds(30), ct); } catch (OperationCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            try { await RunOnceAsync(); }
            catch (Exception ex) { log.LogWarning(ex, "Automatic backup failed"); }
            try { await Task.Delay(TimeSpan.FromHours(6), ct); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync()
    {
        var folder = Folder(config, env);
        Directory.CreateDirectory(folder);
        var ids = await db.Businesses.Find(_ => true).Project(b => b.Id).ToListAsync();
        foreach (var id in ids)
        {
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            if (Directory.GetFiles(folder, $"backup-{id}-{today}-*.json").Length == 0)
            {
                var bytes = await exporter.BuildAsync(id);
                await File.WriteAllBytesAsync(Path.Combine(folder, $"backup-{id}-{today}-{DateTime.UtcNow:HHmm}.json"), bytes);
                log.LogInformation("Backup saved for business {Id}", id);
            }
            // keep the newest N (file names sort by date)
            foreach (var old in Directory.GetFiles(folder, $"backup-{id}-*.json").OrderByDescending(f => f).Skip(Keep(config)))
                File.Delete(old);
        }
    }
}
