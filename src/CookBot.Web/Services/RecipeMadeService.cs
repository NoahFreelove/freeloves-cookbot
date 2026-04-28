using CookBot.Domain.Entities;
using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Web.Services;

/// <summary>
/// Persistence + access for the RecipeMade cooking log (Plan 07-09 Feature 2).
/// Closes FUTURE-Recently-Cooked from the v1.2 milestone summary — Home's
/// "Recently cooked" tile and RecipeView's made-count both read through here.
/// </summary>
public interface IRecipeMadeService
{
    Task<RecipeMade> LogMadeAsync(int recipeId, int userId, string? notes = null, CancellationToken ct = default);
    Task<int> GetMadeCountAsync(int recipeId, int userId, CancellationToken ct = default);
    Task<RecipeMade?> GetLastCookAsync(int recipeId, int userId, CancellationToken ct = default);
    Task<List<RecipeMade>> GetRecentForUserAsync(int userId, int take = 4, CancellationToken ct = default);
}

public class RecipeMadeService : IRecipeMadeService
{
    private readonly CookBotDbContext _db;

    public RecipeMadeService(CookBotDbContext db)
    {
        _db = db;
    }

    public async Task<RecipeMade> LogMadeAsync(int recipeId, int userId, string? notes = null, CancellationToken ct = default)
    {
        var canAccess = await _db.UserCanAccessRecipeAsync(recipeId, userId);
        if (!canAccess)
            throw new UnauthorizedAccessException("You do not have access to this recipe.");

        var trimmed = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (trimmed is { Length: > 1000 })
            trimmed = trimmed.Substring(0, 1000);

        var entry = new RecipeMade
        {
            RecipeId = recipeId,
            UserId = userId,
            Notes = trimmed,
            CompletedAt = DateTime.UtcNow,
        };
        _db.RecipeMades.Add(entry);
        await _db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<int> GetMadeCountAsync(int recipeId, int userId, CancellationToken ct = default)
    {
        // Count is per-user — the made-count surfaced in RecipeView is "your cooks"
        // not aggregate cooks across the household. Cheap to extend later if needed.
        return await _db.RecipeMades
            .AsNoTracking()
            .CountAsync(r => r.RecipeId == recipeId && r.UserId == userId, ct);
    }

    public async Task<RecipeMade?> GetLastCookAsync(int recipeId, int userId, CancellationToken ct = default)
    {
        return await _db.RecipeMades
            .AsNoTracking()
            .Where(r => r.RecipeId == recipeId && r.UserId == userId)
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<RecipeMade>> GetRecentForUserAsync(int userId, int take = 4, CancellationToken ct = default)
    {
        // Most-recent-first across all accessible recipes (owned OR shared cookbooks).
        return await _db.RecipeMades
            .AsNoTracking()
            .Include(r => r.Recipe)
            .Where(r => r.UserId == userId
                        && (r.Recipe.Cookbook.UserId == userId
                            || r.Recipe.Cookbook.Shares.Any(s => s.SharedWithUserId == userId)))
            .OrderByDescending(r => r.CompletedAt)
            .Take(take)
            .ToListAsync(ct);
    }
}
