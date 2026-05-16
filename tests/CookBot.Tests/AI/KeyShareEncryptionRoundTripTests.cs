using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Enums;
using CookBot.Infrastructure.Data;
using CookBot.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CookBot.Tests.AI;

/// <summary>
/// Phase 9 / Plan 09-04 / PROD-11 + PITFALL C2.
///
/// The share semantic is "recipient can USE the owner's AI key without ever SEEING it." With
/// encryption-at-rest, that requires owner.Protect(scope) ↔ recipient.Unprotect(scope) using
/// the SAME shared scope. The C2 pitfall is silently regressing to a per-user scope, which
/// would make Unprotect throw on the recipient's resolve path.
///
/// This test wires the real DatabaseSeeder + the real AiApiKeyResolutionService against a
/// single in-memory IDataProtectionProvider, confirming the full round-trip.
/// </summary>
public class KeyShareEncryptionRoundTripTests : IDisposable
{
    private readonly CookBotDbContext _db;
    private readonly string _dbPath;
    private readonly IDataProtectionProvider _dpp;
    private readonly NoOpBackupService _backupService;
    private readonly JsonRecipeSerializer _serializer;
    private readonly string _contentRoot;

    public KeyShareEncryptionRoundTripTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cookbot-share-roundtrip-{Path.GetRandomFileName()}.db");
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite($"DataSource={_dbPath}")
            .Options;
        _db = new CookBotDbContext(options);
        // Migrate to match the production boot path; SeedAsync's MigrateAsync becomes a no-op.
        _db.Database.Migrate();

        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName("FreelovesCookBot");
        var sp = services.BuildServiceProvider();
        _dpp = sp.GetRequiredService<IDataProtectionProvider>();

        _backupService = new NoOpBackupService();
        _serializer = new JsonRecipeSerializer();
        _contentRoot = Path.Combine(Path.GetTempPath(), $"cookbot-share-content-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(_contentRoot);
    }

    [Fact]
    public async Task RecipientResolution_AfterReencryption_DecryptsToOriginalPlaintext()
    {
        // Arrange — owner with plaintext key, recipient with empty key, share row owner→recipient
        const string ownerPlaintext = "sk-ant-owner-shared-key-ABCDEF";
        var owner = new User
        {
            DisplayName = "Owner",
            IsCookBotAdmin = true,
            Profile = new UserProfile
            {
                ExperienceLevel = ExperienceLevel.Intermediate,
                UnitSystem = UnitSystem.Canadian,
                AiApiKey = ownerPlaintext,
                AiModel = "claude-sonnet-4-5-20250929",
                AiEnabled = true,
            },
        };
        var recipient = new User
        {
            DisplayName = "Recipient",
            Profile = new UserProfile
            {
                ExperienceLevel = ExperienceLevel.Beginner,
                UnitSystem = UnitSystem.Canadian,
                AiApiKey = null,
                AiEnabled = true,
            },
        };
        _db.Users.Add(owner);
        _db.Users.Add(recipient);
        await _db.SaveChangesAsync();

        _db.AiApiKeyShares.Add(new AiApiKeyShare
        {
            OwnerUserId = owner.Id,
            RecipientUserId = recipient.Id,
            CreatedAt = DateTime.UtcNow,
        });
        // Pin the preferred sharer so resolution doesn't depend on the lone-sharer fallback path.
        var recipientProfile = await _db.UserProfiles.FirstAsync(p => p.UserId == recipient.Id);
        recipientProfile.AiSharedKeyOwnerUserId = owner.Id;
        await _db.SaveChangesAsync();

        // Act 1 — run the seeder; the re-encryption pass turns owner's plaintext into ciphertext.
        await DatabaseSeeder.SeedAsync(_db, _backupService, _serializer, _dpp, NullLogger.Instance, _contentRoot);

        // Confirm the storage is now ciphertext, NOT plaintext.
        var ownerProfileAfter = await _db.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == owner.Id);
        Assert.True(DatabaseSeeder.LooksLikeDataProtectionCiphertext(ownerProfileAfter.AiApiKey),
            "owner row should be encrypted after SeedAsync");
        Assert.NotEqual(ownerPlaintext, ownerProfileAfter.AiApiKey);

        // Act 2 — recipient resolves their effective AI credentials. The shared-scope decrypt
        // must succeed and yield the owner's original plaintext.
        var resolver = new AiApiKeyResolutionService(_db, _dpp, NullLogger<AiApiKeyResolutionService>.Instance);
        var resolved = await resolver.ResolveAsync(recipient.Id);

        // Assert
        Assert.NotNull(resolved);
        Assert.Equal(ownerPlaintext, resolved!.ApiKey);
        Assert.Equal(owner.Id, resolved.SharedFromUserId);
        Assert.Equal("Owner", resolved.SharedFromDisplayName);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);
    }

    private sealed class NoOpBackupService : IDatabaseBackupService
    {
        public Task BackupBeforeMigrationAsync(string migrationName, CancellationToken ct) => Task.CompletedTask;
    }
}
