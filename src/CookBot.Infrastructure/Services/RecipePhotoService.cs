using CookBot.Application.DTOs;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;
using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CookBot.Infrastructure.Services;

/// <summary>
/// Gallery CRUD service: get, add, reorder, set-primary, delete, and update caption for
/// <see cref="RecipePhoto"/> rows. Owns the one-primary invariant and the server-side cap.
/// Lives in Infrastructure so it can inject <see cref="CookBotDbContext"/> directly for
/// bulk <c>ExecuteUpdateAsync</c>, <c>OrderBy</c>, and <c>Where</c> queries that the
/// generic <see cref="IRepository{T}"/> does not expose.
/// Every mutation except <see cref="UpdateCaptionAsync"/> calls
/// <see cref="RecipeService.SyncPrimaryPhotoUrlAsync"/> to keep <c>Recipe.PhotoUrl</c>
/// and <c>CanonicalDocumentJson</c> in sync (D-14-01 / P15).
/// </summary>
public class RecipePhotoService
{
    // Matches RecipePhotoConfiguration.cs:19-21 — the Url column max length.
    private const int MaxUrlLength = 2048;

    private readonly CookBotDbContext _db;
    private readonly IRepository<Cookbook> _cookbookRepo;
    private readonly RecipeService _recipeService;
    private readonly IRecipePhotoFileStorage _photoStorage;
    private readonly RecipePhotoUrlValidator _urlValidator;
    private readonly CookBotSettings _settings;
    private readonly ILogger<RecipePhotoService> _logger;

    public RecipePhotoService(
        CookBotDbContext db,
        IRepository<Cookbook> cookbookRepo,
        RecipeService recipeService,
        IRecipePhotoFileStorage photoStorage,
        RecipePhotoUrlValidator urlValidator,
        IOptions<CookBotSettings> settings,
        ILogger<RecipePhotoService> logger)
    {
        _db = db;
        _cookbookRepo = cookbookRepo;
        _recipeService = recipeService;
        _photoStorage = photoStorage;
        _urlValidator = urlValidator;
        _settings = settings.Value;
        _logger = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>Returns all photos for the recipe ordered by <see cref="RecipePhoto.SortOrder"/>.</summary>
    public async Task<IReadOnlyList<RecipePhoto>> GetPhotosAsync(int recipeId, int userId)
    {
        await AssertOwnershipAsync(recipeId, userId);
        return await _db.RecipePhotos
            .Where(p => p.RecipeId == recipeId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Persists a new photo row.
    /// Enforces the server-side cap (clamped [1, 20]).
    /// The first photo added becomes primary automatically.
    /// Server-side URL validation: /uploads/ paths from the upload pipeline are
    /// allow-listed; all other URLs must pass the scheme allowlist (http/https only)
    /// and the 2048-char column limit (WR-02 / D-14-10 defense-in-depth).
    /// </summary>
    public async Task<RecipePhoto> AddPhotoAsync(int recipeId, string url, int userId, string? caption = null)
    {
        await AssertOwnershipAsync(recipeId, userId);

        // Server-side URL validation (WR-02): /uploads/ paths from LocalRecipePhotoStorage
        // are trusted as-is; all other URLs must pass the scheme allowlist.
        if (!url.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            if (url.Length > MaxUrlLength)
                throw new InvalidOperationException($"URL exceeds the maximum allowed length of {MaxUrlLength} characters.");

            if (!_urlValidator.TryValidate(url, out _, out var errorCode))
            {
                var message = errorCode switch
                {
                    "SCHEME_NOT_ALLOWED" => "Only http and https photo URLs are allowed.",
                    "PROTOCOL_RELATIVE_REJECTED" => "Only http and https photo URLs are allowed.",
                    _ => "The photo URL is not valid.",
                };
                throw new InvalidOperationException(message);
            }
        }

        var cap = Math.Clamp(_settings.MaxPhotosPerRecipe, 1, 20); // D-14-04-cap
        var count = await _db.RecipePhotos.CountAsync(p => p.RecipeId == recipeId);
        if (count >= cap)
            throw new InvalidOperationException($"Maximum {cap} photos per recipe.");

        var maxSort = await _db.RecipePhotos
            .Where(p => p.RecipeId == recipeId)
            .MaxAsync(p => (int?)p.SortOrder) ?? -1;

        var isFirst = count == 0;

        var photo = new RecipePhoto
        {
            RecipeId = recipeId,
            Url = url,
            Caption = caption?.Trim(),
            SortOrder = maxSort + 1,
            IsPrimary = isFirst,
        };
        _db.RecipePhotos.Add(photo);
        await _db.SaveChangesAsync();

        await _recipeService.SyncPrimaryPhotoUrlAsync(recipeId);

        return photo;
    }

    /// <summary>
    /// Sets the specified photo as primary and clears IsPrimary on all others for the recipe.
    /// Uses a bulk ExecuteUpdateAsync clear to prevent two-primary drift (RESEARCH Pitfall 3).
    /// The clear and set are wrapped in a single transaction (WR-03) so an interruption
    /// between the two writes cannot leave zero-primary state in the DB.
    /// </summary>
    public async Task SetPrimaryAsync(int recipeId, int photoId, int userId)
    {
        await AssertOwnershipAsync(recipeId, userId);

        var photo = await _db.RecipePhotos
            .FirstOrDefaultAsync(p => p.Id == photoId && p.RecipeId == recipeId)
            ?? throw new InvalidOperationException("Photo not found.");

        // Wrap clear+set in a single transaction so an interruption between the two
        // writes cannot leave the recipe with zero primary photos (WR-03).
        await using var tx = await _db.Database.BeginTransactionAsync();

        // Clear all IsPrimary for the recipe in one bulk update, then set the target.
        // ExecuteUpdateAsync bypasses the EF change tracker, so we must reload/detach
        // tracked entities to avoid the tracker re-applying stale IsPrimary=true values
        // on the next SaveChanges (RESEARCH Pitfall 3).
        await _db.RecipePhotos
            .Where(p => p.RecipeId == recipeId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsPrimary, false));

        // Detach all tracked RecipePhoto entities for this recipe so the change tracker
        // does not re-apply stale values when we SaveChanges below.
        var trackedPhotos = _db.ChangeTracker.Entries<RecipePhoto>()
            .Where(e => e.Entity.RecipeId == recipeId)
            .ToList();
        foreach (var entry in trackedPhotos)
            entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        // Re-attach and update only the target photo
        photo = await _db.RecipePhotos.FindAsync(photoId)
            ?? throw new InvalidOperationException("Photo not found after clear.");
        photo.IsPrimary = true;
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        await _recipeService.SyncPrimaryPhotoUrlAsync(recipeId);
    }

    /// <summary>
    /// Re-assigns <see cref="RecipePhoto.SortOrder"/> values based on the supplied
    /// ordered ID array (index = new SortOrder). Validates all IDs belong to the recipe.
    /// </summary>
    public async Task ReorderAsync(int recipeId, int[] orderedPhotoIds, int userId)
    {
        await AssertOwnershipAsync(recipeId, userId);

        var photos = await _db.RecipePhotos
            .Where(p => p.RecipeId == recipeId)
            .ToListAsync();

        // Validate every supplied ID belongs to this recipe
        var photoDict = photos.ToDictionary(p => p.Id);
        foreach (var id in orderedPhotoIds)
        {
            if (!photoDict.ContainsKey(id))
                throw new InvalidOperationException($"Photo {id} does not belong to recipe {recipeId}.");
        }

        // Assign new SortOrder (index position in supplied array)
        for (int i = 0; i < orderedPhotoIds.Length; i++)
        {
            photoDict[orderedPhotoIds[i]].SortOrder = i;
        }

        await _db.SaveChangesAsync();

        await _recipeService.SyncPrimaryPhotoUrlAsync(recipeId);
    }

    /// <summary>
    /// Deletes a single photo. Deletes the local file first (if a /uploads/ path).
    /// If the deleted photo was primary, promotes the lowest-SortOrder remaining photo.
    /// </summary>
    public async Task DeleteAsync(int recipeId, int photoId, int userId)
    {
        await AssertOwnershipAsync(recipeId, userId);

        var photo = await _db.RecipePhotos
            .FirstOrDefaultAsync(p => p.Id == photoId && p.RecipeId == recipeId)
            ?? throw new InvalidOperationException("Photo not found.");

        var wasPrimary = photo.IsPrimary;

        // Delete local file before removing the row (D-14-11)
        if (photo.Url.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            try
            {
                _photoStorage.DeletePhysicalFile(photo.Url);
            }
            catch (Exception ex)
            {
                // Non-fatal — log and continue
                _logger.LogWarning(ex, "Could not delete photo file {Url} during single-photo delete", photo.Url);
            }
        }

        _db.RecipePhotos.Remove(photo);
        await _db.SaveChangesAsync();

        // If we deleted the primary, promote the lowest-SortOrder remaining row (D-14-03).
        // The clear+promote pair is wrapped in a transaction (WR-03) so an interruption
        // between the two writes cannot leave the recipe with zero primary photos.
        if (wasPrimary)
        {
            var next = await _db.RecipePhotos
                .Where(p => p.RecipeId == recipeId)
                .OrderBy(p => p.SortOrder)
                .FirstOrDefaultAsync();

            if (next is not null)
            {
                var nextId = next.Id;

                await using var tx = await _db.Database.BeginTransactionAsync();

                // Clear all first (consistent with SetPrimaryAsync), then set winner.
                // ExecuteUpdateAsync bypasses the change tracker — detach first.
                await _db.RecipePhotos
                    .Where(p => p.RecipeId == recipeId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsPrimary, false));

                var trackedPhotos = _db.ChangeTracker.Entries<RecipePhoto>()
                    .Where(e => e.Entity.RecipeId == recipeId)
                    .ToList();
                foreach (var entry in trackedPhotos)
                    entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

                var winner = await _db.RecipePhotos.FindAsync(nextId);
                if (winner is null)
                {
                    // Concurrent delete: the candidate was removed between our query and fetch.
                    // Roll back and log; SyncPrimaryPhotoUrlAsync fallback will handle PhotoUrl.
                    _logger.LogWarning(
                        "Could not promote photo {NextId} for recipe {RecipeId}: photo was concurrently deleted.",
                        nextId, recipeId);
                    await tx.RollbackAsync();
                }
                else
                {
                    winner.IsPrimary = true;
                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
            }
        }

        await _recipeService.SyncPrimaryPhotoUrlAsync(recipeId);
    }

    /// <summary>
    /// Updates the caption on a single photo (trim + allow null).
    /// Does NOT re-sync PhotoUrl — captions are not mirrored.
    /// </summary>
    public async Task UpdateCaptionAsync(int recipeId, int photoId, string? caption, int userId)
    {
        await AssertOwnershipAsync(recipeId, userId);

        var photo = await _db.RecipePhotos
            .FirstOrDefaultAsync(p => p.Id == photoId && p.RecipeId == recipeId)
            ?? throw new InvalidOperationException("Photo not found.");

        photo.Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        await _db.SaveChangesAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Loads the recipe's cookbook and throws <see cref="UnauthorizedAccessException"/>
    /// when <paramref name="userId"/> is not the owner (verbatim from RecipeService).
    /// </summary>
    private async Task AssertOwnershipAsync(int recipeId, int userId)
    {
        var recipe = await _db.Recipes.FindAsync(recipeId)
            ?? throw new InvalidOperationException("Recipe not found.");

        var cookbook = await _cookbookRepo.GetByIdAsync(recipe.CookbookId)
            ?? throw new InvalidOperationException("Cookbook not found.");

        if (cookbook.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this cookbook.");
    }
}
