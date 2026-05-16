using System.Security.Cryptography;
using CookBot.Infrastructure.AI;
using CookBot.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CookBot.Web.Services;

public sealed record EffectiveAiCredentials(
    string ApiKey,
    string? ModelId,
    int? SharedFromUserId,
    string? SharedFromDisplayName);

public class AiApiKeyResolutionService
{
    private readonly CookBotDbContext _db;
    private readonly IDataProtector _protector;
    private readonly ILogger<AiApiKeyResolutionService> _logger;

    public AiApiKeyResolutionService(
        CookBotDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<AiApiKeyResolutionService> logger)
    {
        _db = db;
        // PROD-08 / PITFALL C2 (Phase 9 / Plan 09-04) — single shared scope. Owner.Protect(scope)
        // ↔ Recipient.Unprotect(scope) only succeeds when both sites use the same purpose
        // string. Per-user scopes would silently regress the share semantic.
        _protector = dataProtectionProvider.CreateProtector("AiApiKey.v1");
        _logger = logger;
    }

    /// <summary>
    /// Resolves the Anthropic API key and model for AI calls. Recipients never receive the key in the UI;
    /// this runs only on the server.
    /// </summary>
    public async Task<EffectiveAiCredentials?> ResolveAsync(int userId, CancellationToken cancellationToken = default)
    {
        await ClearStaleSharedKeyPreferenceAsync(userId, cancellationToken);

        var profile = await _db.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile == null) return null;

        if (!string.IsNullOrWhiteSpace(profile.AiApiKey))
        {
            return new EffectiveAiCredentials(
                DecryptIfNeeded(profile.AiApiKey).Trim(),
                profile.AiModel,
                SharedFromUserId: null,
                SharedFromDisplayName: null);
        }

        var shareOwners = await (
                from s in _db.AiApiKeyShares.AsNoTracking()
                join p in _db.UserProfiles.AsNoTracking() on s.OwnerUserId equals p.UserId
                join u in _db.Users.AsNoTracking() on s.OwnerUserId equals u.Id
                where s.RecipientUserId == userId && !string.IsNullOrWhiteSpace(p.AiApiKey)
                select new { s.OwnerUserId, p.AiApiKey, p.AiModel, u.DisplayName })
            .ToListAsync(cancellationToken);

        if (shareOwners.Count == 0) return null;

        var preferredId = profile.AiSharedKeyOwnerUserId;
        var chosen = preferredId.HasValue
            ? shareOwners.FirstOrDefault(x => x.OwnerUserId == preferredId.Value)
            : null;

        if (chosen == null && shareOwners.Count == 1)
            chosen = shareOwners[0];

        if (chosen == null) return null;

        return new EffectiveAiCredentials(
            DecryptIfNeeded(chosen.AiApiKey!).Trim(),
            chosen.AiModel,
            chosen.OwnerUserId,
            chosen.DisplayName);
    }

    /// <summary>
    /// PROD-08 + PITFALL C3/C4 — read-path decryption gate. Legacy plaintext rows fall through
    /// unchanged so the DatabaseSeeder migration pass can re-encrypt them on next boot; only
    /// values that already look like Data Protection ciphertext are Unprotect'd. On failure,
    /// the message is scrubbed via <see cref="SecretRedactor"/> before logging so neither the
    /// plaintext nor the ciphertext leaks to log sinks.
    /// </summary>
    private string DecryptIfNeeded(string stored)
    {
        if (!DatabaseSeeder.LooksLikeDataProtectionCiphertext(stored))
            return stored;
        try
        {
            return _protector.Unprotect(stored);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(SecretRedactor.Redact($"Failed to decrypt AI API key: {ex.Message}", stored));
            throw;
        }
    }

    /// <summary>
    /// If this user saves their own API key, a stored "preferred sharer" is meaningless and confuses the Shared keys UI.
    /// </summary>
    private async Task ClearStaleSharedKeyPreferenceAsync(int userId, CancellationToken cancellationToken)
    {
        await _db.UserProfiles
            .Where(p => p.UserId == userId
                        && p.AiSharedKeyOwnerUserId != null
                        && p.AiApiKey != null
                        && p.AiApiKey != "")
            .ExecuteUpdateAsync(
                s => s.SetProperty(p => p.AiSharedKeyOwnerUserId, (int?)null),
                cancellationToken);
    }
}
