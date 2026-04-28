using CookBot.Domain.Entities;
using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Web.Services;

/// <summary>
/// Persistence + access for per-user scheduled recipes (Plan 07-09 Feature 1).
/// Reminders/notifications are out of scope; this is the data backbone for the
/// Home "Up next" card and the RecipeView "Schedule" affordance.
///
/// Lives in CookBot.Web.Services (alongside AiApiKeyShareService) because the
/// Application layer is repository-only and doesn't reference Infrastructure.
/// EF Include + AsNoTracking are needed for the read paths.
/// </summary>
public interface IScheduledRecipeService
{
    Task<List<ScheduledRecipe>> GetUpcomingAsync(int userId, int take = 3, CancellationToken ct = default);
    Task<ScheduledRecipe> ScheduleAsync(int recipeId, int userId, DateTime scheduledFor, string? notes = null, CancellationToken ct = default);
    Task UnscheduleAsync(int scheduledRecipeId, int userId, CancellationToken ct = default);
}

public class ScheduledRecipeService : IScheduledRecipeService
{
    private readonly CookBotDbContext _db;

    public ScheduledRecipeService(CookBotDbContext db)
    {
        _db = db;
    }

    public async Task<List<ScheduledRecipe>> GetUpcomingAsync(int userId, int take = 3, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.ScheduledRecipes
            .AsNoTracking()
            .Include(s => s.Recipe)
            .Where(s => s.UserId == userId && s.ScheduledFor >= now)
            .OrderBy(s => s.ScheduledFor)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<ScheduledRecipe> ScheduleAsync(int recipeId, int userId, DateTime scheduledFor, string? notes = null, CancellationToken ct = default)
    {
        // Authz — same predicate the rest of the app uses (owns the cookbook OR is
        // a share recipient). Mirrors UserCanAccessRecipeAsync in spirit.
        var canAccess = await _db.Recipes
            .AnyAsync(r => r.Id == recipeId && (
                r.Cookbook.UserId == userId ||
                r.Cookbook.Shares.Any(s => s.SharedWithUserId == userId)), ct);
        if (!canAccess)
            throw new UnauthorizedAccessException("You do not have access to this recipe.");

        // Normalize to UTC. <input type="datetime-local"> emits a wall-clock string;
        // ScheduleRecipeDialog converts that to UTC before calling ScheduleAsync.
        if (scheduledFor.Kind == DateTimeKind.Unspecified)
            scheduledFor = DateTime.SpecifyKind(scheduledFor, DateTimeKind.Utc);
        else if (scheduledFor.Kind == DateTimeKind.Local)
            scheduledFor = scheduledFor.ToUniversalTime();

        var trimmedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (trimmedNotes is { Length: > 500 })
            trimmedNotes = trimmedNotes.Substring(0, 500);

        var entry = new ScheduledRecipe
        {
            RecipeId = recipeId,
            UserId = userId,
            ScheduledFor = scheduledFor,
            Notes = trimmedNotes,
            CreatedAt = DateTime.UtcNow,
        };
        _db.ScheduledRecipes.Add(entry);
        await _db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task UnscheduleAsync(int scheduledRecipeId, int userId, CancellationToken ct = default)
    {
        var entry = await _db.ScheduledRecipes
            .FirstOrDefaultAsync(s => s.Id == scheduledRecipeId && s.UserId == userId, ct);
        if (entry == null) return;
        _db.ScheduledRecipes.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }
}
