using CookBot.Application.DTOs;
using CookBot.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace CookBot.Tests.Migration;

/// <summary>
/// Verifies the configurable retention contract for <see cref="DatabaseBackupService"/>:
/// settings-driven retention is read from <see cref="CookBotSettings.DatabaseBackupRetention"/>
/// and clamped to [1, 10] before the file-system sweep runs.
/// </summary>
public class DatabaseBackupServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;

    public DatabaseBackupServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "cookbot-backup-tests-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "cookbot.db");
        File.WriteAllText(_dbPath, "fake sqlite content");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private DatabaseBackupService BuildService(int retention)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}"
            })
            .Build();
        var settings = Options.Create(new CookBotSettings { DatabaseBackupRetention = retention });
        return new DatabaseBackupService(config, settings);
    }

    [Fact]
    public async Task RetentionFromSettings_IsRead()
    {
        // Pre-create 8 fake .pre-*.bak files with descending mtimes.
        for (int i = 0; i < 8; i++)
        {
            var f = Path.Combine(_tempDir, $"cookbot.db.pre-Old{i}.bak");
            File.WriteAllText(f, $"backup{i}");
            File.SetLastWriteTimeUtc(f, DateTime.UtcNow.AddMinutes(-i - 10));
        }

        var svc = BuildService(retention: 5);
        await svc.BackupBeforeMigrationAsync("NewMigration", CancellationToken.None);

        // After the call: 8 pre-existing + 1 just-created = 9. Retention=5 keeps the 5 newest by mtime;
        // the just-created `.pre-NewMigration.bak` is the freshest, so it survives.
        var bakFiles = Directory.GetFiles(_tempDir, "cookbot.db.pre-*.bak");
        Assert.Equal(5, bakFiles.Length);
    }

    [Fact]
    public async Task RetentionClamp_BelowMin_UsesOne()
    {
        var svc = BuildService(retention: 0); // below min — service must clamp to 1
        await svc.BackupBeforeMigrationAsync("M1", CancellationToken.None);
        var bakFiles = Directory.GetFiles(_tempDir, "cookbot.db.pre-*.bak");
        Assert.Single(bakFiles);
    }
}
