using CookBot.Application.DTOs;
using CookBot.Application.Recipes;
using CookBot.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace CookBot.Tests.Migration;

/// <summary>
/// MIGRATION-08 backup-file integration check covering RESEARCH Open Q4.
///
/// Note: The projector round-trip test was removed in Plan 10 (CLEAN-01)
/// when the projector was deleted as part of D-32 step e.
/// </summary>
public class CanonicalBackfillTests
{
    [Fact]
    public async Task BackupBeforeMigration_CreatesBackupFile_WithExpectedName()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "cookbot-backup-int-" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var dbPath = Path.Combine(tempDir, "cookbot.db");
            File.WriteAllText(dbPath, "preexisting db content");

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={dbPath}"
                })
                .Build();
            var settings = Options.Create(new CookBotSettings { DatabaseBackupRetention = 3 });
            var svc = new DatabaseBackupService(config, settings);

            await svc.BackupBeforeMigrationAsync("RecipeCanonicalDocument", CancellationToken.None);

            var expected = Path.Combine(tempDir, "cookbot.db.pre-RecipeCanonicalDocument.bak");
            Assert.True(File.Exists(expected), $"expected backup at {expected}");
            Assert.Equal("preexisting db content", File.ReadAllText(expected));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
