using CookBot.Application.DTOs;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace CookBot.Infrastructure.Data;

/// <summary>
/// Default <see cref="IDatabaseBackupService"/> implementation. Resolves the SQLite file path
/// via <see cref="SqliteConnectionStringBuilder.DataSource"/> (NOT regex/string-split — D-15),
/// copies it to <c>{stem}.pre-{migrationName}.bak</c>, and prunes older backups using
/// <c>LastWriteTimeUtc</c> ordering (descending). Retention is configurable via
/// <c>CookBotSettings.DatabaseBackupRetention</c>, clamped to [1, 10].
/// </summary>
public sealed class DatabaseBackupService : IDatabaseBackupService
{
    private readonly IConfiguration _config;
    private readonly int _retention;

    public DatabaseBackupService(IConfiguration config, IOptions<CookBotSettings> settings)
    {
        _config = config;
        _retention = Math.Clamp(settings.Value.DatabaseBackupRetention, 1, 10);
    }

    public Task BackupBeforeMigrationAsync(string migrationName, CancellationToken ct)
    {
        var connStr = _config.GetConnectionString("DefaultConnection") ?? "Data Source=cookbot.db";
        var builder = new SqliteConnectionStringBuilder(connStr);
        var dbPath = builder.DataSource;
        var fullPath = Path.GetFullPath(dbPath);

        if (!File.Exists(fullPath))
        {
            // Fresh install — no DB file yet, nothing to back up.
            return Task.CompletedTask;
        }

        var dir = Path.GetDirectoryName(fullPath)!;
        var stem = Path.GetFileName(fullPath);
        var backupName = $"{stem}.pre-{migrationName}.bak";
        var backupPath = Path.Combine(dir, backupName);

        File.Copy(fullPath, backupPath, overwrite: true);

        // Retention sweep: keep only the N most recent .pre-*.bak by LastWriteTimeUtc desc.
        var pattern = $"{stem}.pre-*.bak";
        var existing = Directory.GetFiles(dir, pattern)
            .Select(p => new FileInfo(p))
            .OrderByDescending(fi => fi.LastWriteTimeUtc)
            .ToList();
        foreach (var stale in existing.Skip(_retention))
        {
            try { stale.Delete(); }
            catch { /* non-fatal cleanup */ }
        }

        return Task.CompletedTask;
    }
}
