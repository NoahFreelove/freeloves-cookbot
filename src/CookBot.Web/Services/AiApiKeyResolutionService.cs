using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Web.Services;

public sealed record EffectiveAiCredentials(
    string ApiKey,
    string? ModelId,
    int? SharedFromUserId,
    string? SharedFromDisplayName);

public class AiApiKeyResolutionService
{
    private readonly CookBotDbContext _db;

    public AiApiKeyResolutionService(CookBotDbContext db)
    {
        _db = db;
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
                profile.AiApiKey.Trim(),
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
            chosen.AiApiKey!.Trim(),
            chosen.AiModel,
            chosen.OwnerUserId,
            chosen.DisplayName);
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
