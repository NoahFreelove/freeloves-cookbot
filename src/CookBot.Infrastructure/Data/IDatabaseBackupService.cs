namespace CookBot.Infrastructure.Data;

/// <summary>
/// Pre-migration backup service. The SQLite file is copied next to itself with a
/// `.pre-{migrationName}.bak` suffix BEFORE EF applies pending migrations (D-15).
/// </summary>
public interface IDatabaseBackupService
{
    /// <summary>
    /// Backs up the SQLite database file before a migration is applied. No-op when the
    /// database file does not exist on disk (fresh install).
    /// </summary>
    Task BackupBeforeMigrationAsync(string migrationName, CancellationToken ct);
}
