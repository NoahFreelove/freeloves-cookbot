using CookBot.Domain.Entities;

namespace CookBot.Application.Services;

/// <summary>
/// Offline nutrition compute service (NUTR-02/03/04 / Phase 15 / Plan 05).
/// <para>
/// <b>Hard invariant (P7/SC1):</b> This interface is NEVER imported or called from
/// <c>RecipeService</c>. The save path must not block on nutrition. Only explicit
/// user CTAs (Plan 07 RecipeView) call <see cref="ComputeAsync"/>.
/// </para>
/// </summary>
public interface INutritionService
{
    /// <summary>
    /// Returns the cached nutrition row for <paramref name="recipeId"/> (may be stale),
    /// or null if nutrition has never been computed.
    /// Enforces ownership: throws <see cref="UnauthorizedAccessException"/> if
    /// <paramref name="userId"/> does not own the recipe.
    /// </summary>
    Task<RecipeNutritionCache?> GetCacheAsync(int recipeId, int userId);

    /// <summary>
    /// Computes offline nutrition for every ingredient in the recipe, writes
    /// (or updates) the <see cref="RecipeNutritionCache"/> row, and returns it.
    /// Enforces ownership: throws <see cref="UnauthorizedAccessException"/> if
    /// <paramref name="userId"/> does not own the recipe.
    /// </summary>
    Task<RecipeNutritionCache> ComputeAsync(int recipeId, int userId);
}
