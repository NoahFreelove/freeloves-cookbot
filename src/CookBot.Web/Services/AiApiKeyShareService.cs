using CookBot.Domain.Entities;
using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Web.Services;

public class AiApiKeyShareService
{
    private readonly CookBotDbContext _db;

    public AiApiKeyShareService(CookBotDbContext db)
    {
        _db = db;
    }

    public sealed record ShareParty(int UserId, string DisplayName);

    public async Task<IReadOnlyList<ShareParty>> ListOutgoingAsync(int ownerUserId, CancellationToken cancellationToken = default)
    {
        return await _db.AiApiKeyShares.AsNoTracking()
            .Where(s => s.OwnerUserId == ownerUserId)
            .Join(_db.Users.AsNoTracking(), s => s.RecipientUserId, u => u.Id, (_, u) => u)
            .OrderBy(u => u.DisplayName)
            .Select(u => new ShareParty(u.Id, u.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShareParty>> ListIncomingAsync(int recipientUserId, CancellationToken cancellationToken = default)
    {
        return await _db.AiApiKeyShares.AsNoTracking()
            .Where(s => s.RecipientUserId == recipientUserId)
            .Join(_db.Users.AsNoTracking(), s => s.OwnerUserId, u => u.Id, (_, u) => u)
            .OrderBy(u => u.DisplayName)
            .Select(u => new ShareParty(u.Id, u.DisplayName))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Incoming shares whose owner currently has a non-empty API key (eligible for resolution).
    /// </summary>
    public async Task<IReadOnlyList<ShareParty>> ListIncomingWithUsableOwnerKeyAsync(int recipientUserId,
        CancellationToken cancellationToken = default)
    {
        return await (
                from s in _db.AiApiKeyShares.AsNoTracking()
                join p in _db.UserProfiles.AsNoTracking() on s.OwnerUserId equals p.UserId
                join u in _db.Users.AsNoTracking() on s.OwnerUserId equals u.Id
                where s.RecipientUserId == recipientUserId
                      && !string.IsNullOrWhiteSpace(p.AiApiKey)
                select u)
            .Distinct()
            .OrderBy(u => u.DisplayName)
            .Select(u => new ShareParty(u.Id, u.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> GrantAsync(int ownerUserId, int recipientUserId, CancellationToken cancellationToken = default)
    {
        if (ownerUserId == recipientUserId)
            return "Choose another user.";

        var ownerProfile = await _db.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == ownerUserId, cancellationToken);
        if (ownerProfile == null || string.IsNullOrWhiteSpace(ownerProfile.AiApiKey))
            return "Save your own Anthropic API key in Profile before sharing it.";

        var recipientExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == recipientUserId, cancellationToken);
        if (!recipientExists)
            return "That user does not exist.";

        var exists = await _db.AiApiKeyShares.AsNoTracking()
            .AnyAsync(s => s.OwnerUserId == ownerUserId && s.RecipientUserId == recipientUserId, cancellationToken);
        if (exists)
            return "You already share with this user.";

        _db.AiApiKeyShares.Add(new AiApiKeyShare
        {
            OwnerUserId = ownerUserId,
            RecipientUserId = recipientUserId,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);

        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == recipientUserId, cancellationToken);
        if (profile != null)
        {
            var shareCount = await _db.AiApiKeyShares.CountAsync(s => s.RecipientUserId == recipientUserId, cancellationToken);
            if (shareCount == 1)
                profile.AiSharedKeyOwnerUserId = ownerUserId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return null;
    }

    public async Task RevokeAsync(int ownerUserId, int recipientUserId, CancellationToken cancellationToken = default)
    {
        var share = await _db.AiApiKeyShares
            .FirstOrDefaultAsync(s => s.OwnerUserId == ownerUserId && s.RecipientUserId == recipientUserId, cancellationToken);
        if (share == null) return;

        _db.AiApiKeyShares.Remove(share);

        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == recipientUserId, cancellationToken);
        if (profile?.AiSharedKeyOwnerUserId == ownerUserId)
            profile.AiSharedKeyOwnerUserId = null;

        await _db.SaveChangesAsync(cancellationToken);

        if (profile == null) return;

        var remaining = await _db.AiApiKeyShares
            .Where(s => s.RecipientUserId == recipientUserId)
            .Select(s => s.OwnerUserId)
            .ToListAsync(cancellationToken);
        if (remaining.Count == 1)
        {
            profile.AiSharedKeyOwnerUserId = remaining[0];
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SetPreferredSharedOwnerAsync(int recipientUserId, int? ownerUserId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == recipientUserId, cancellationToken);
        if (profile == null) return;

        if (ownerUserId == null)
        {
            profile.AiSharedKeyOwnerUserId = null;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var valid = await _db.AiApiKeyShares.AsNoTracking()
            .AnyAsync(s => s.OwnerUserId == ownerUserId && s.RecipientUserId == recipientUserId, cancellationToken);
        if (!valid) return;

        profile.AiSharedKeyOwnerUserId = ownerUserId;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
