using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Enums;
using CookBot.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CookBot.Tests.AI;

/// <summary>
/// Phase 9 / Plan 09-04 / PROD-09 + PITFALL C3.
///
/// Validates the sentinel-prefix re-encryption pass in <see cref="DatabaseSeeder.SeedAsync"/>:
///  1. First boot — a plaintext AiApiKey row is encrypted in place; the resulting value passes
///     LooksLikeDataProtectionCiphertext and round-trips through Unprotect to the original plaintext.
///  2. Second boot — already-encrypted rows are a no-op (no double-encryption).
///  3. Independent round-trip check on the shared protector scope "AiApiKey.v1".
///
/// The IDataProtectionProvider used in tests is the in-memory provider returned by
/// AddDataProtection().SetApplicationName(...) — keys never need to outlive the process.
/// </summary>
public class SentinelPrefixMigrationTests : IDisposable
{
    private readonly CookBotDbContext _db;
    private readonly string _dbPath;
    private readonly IDataProtectionProvider _dpp;
    private readonly NoOpBackupService _backupService;
    private readonly JsonRecipeSerializer _serializer;
    private readonly string _contentRoot;

    public SentinelPrefixMigrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cookbot-sentinel-{Path.GetRandomFileName()}.db");
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite($"DataSource={_dbPath}")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName("FreelovesCookBot");
        var sp = services.BuildServiceProvider();
        _dpp = sp.GetRequiredService<IDataProtectionProvider>();

        _backupService = new NoOpBackupService();
        _serializer = new JsonRecipeSerializer();
        // ContentRootPath is only used by the ingredient-seed JSON loader; pointing at a
        // throwaway temp dir means LoadIngredientsFromSeedFile returns an empty list silently.
        _contentRoot = Path.Combine(Path.GetTempPath(), $"cookbot-sentinel-content-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(_contentRoot);
    }

    [Fact]
    public async Task FirstBoot_PlaintextRow_GetsReencrypted()
    {
        // Arrange — seed a user + profile with plaintext AiApiKey
        const string plaintext = "sk-ant-test-plaintext-key-12345";
        var user = new User
        {
            DisplayName = "Owner",
            IsCookBotAdmin = true,
            Profile = new UserProfile
            {
                ExperienceLevel = ExperienceLevel.Intermediate,
                UnitSystem = UnitSystem.Canadian,
                AiApiKey = plaintext,
            },
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Act — run the seeder
        await DatabaseSeeder.SeedAsync(_db, _backupService, _serializer, _dpp, NullLogger.Instance, _contentRoot);

        // Assert — reload the row and confirm encryption + round-trip
        var reloaded = await _db.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == user.Id);
        Assert.NotNull(reloaded.AiApiKey);
        Assert.True(DatabaseSeeder.LooksLikeDataProtectionCiphertext(reloaded.AiApiKey),
            $"row's AiApiKey should look like ciphertext after re-encryption pass, was: {reloaded.AiApiKey}");

        var protector = _dpp.CreateProtector("AiApiKey.v1");
        var decrypted = protector.Unprotect(reloaded.AiApiKey!);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public async Task SecondBoot_AlreadyEncryptedRow_IsNoOp()
    {
        // Arrange — encrypt then save the row directly so the first SeedAsync pass has nothing to do.
        const string plaintext = "sk-ant-already-encrypted-78901";
        var protector = _dpp.CreateProtector("AiApiKey.v1");
        var ciphertext = protector.Protect(plaintext);

        var user = new User
        {
            DisplayName = "PreEncrypted",
            IsCookBotAdmin = true,
            Profile = new UserProfile
            {
                ExperienceLevel = ExperienceLevel.Intermediate,
                UnitSystem = UnitSystem.Canadian,
                AiApiKey = ciphertext,
            },
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Act — boot twice
        await DatabaseSeeder.SeedAsync(_db, _backupService, _serializer, _dpp, NullLogger.Instance, _contentRoot);
        var afterFirst = await _db.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == user.Id);

        await DatabaseSeeder.SeedAsync(_db, _backupService, _serializer, _dpp, NullLogger.Instance, _contentRoot);
        var afterSecond = await _db.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == user.Id);

        // Assert — the ciphertext is byte-identical across boots (no double encryption)
        Assert.Equal(ciphertext, afterFirst.AiApiKey);
        Assert.Equal(ciphertext, afterSecond.AiApiKey);
        Assert.Equal(plaintext, protector.Unprotect(afterSecond.AiApiKey!));
    }

    [Fact]
    public void DataProtector_Unprotect_RoundTripsToOriginalPlaintext()
    {
        var protector = _dpp.CreateProtector("AiApiKey.v1");
        const string input = "any-string-can-be-protected";

        var protectedValue = protector.Protect(input);
        var roundTrip = protector.Unprotect(protectedValue);

        Assert.Equal(input, roundTrip);
        Assert.True(DatabaseSeeder.LooksLikeDataProtectionCiphertext(protectedValue));
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
    }

    /// <summary>
    /// Stand-in for IDatabaseBackupService — production copies cookbot.db to a .bak file, but tests
    /// run against a temp DB they own and would rather avoid the disk thrash.
    /// </summary>
    private sealed class NoOpBackupService : IDatabaseBackupService
    {
        public Task BackupBeforeMigrationAsync(string migrationName, CancellationToken ct) => Task.CompletedTask;
    }
}
