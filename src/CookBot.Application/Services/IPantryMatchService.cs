using CookBot.Application.DTOs;

namespace CookBot.Application.Services;

/// <summary>
/// Phase 10 / QOL-01..03 — Application-layer contract for ranking pantry-match recipes.
/// Returns recipes ordered by D-44's linear-decay score (coverage − recency-penalty),
/// filtered by D-45's AND-combined dietary rules, and stable-sorted per PITFALL H8 as
/// (score desc, recipeId asc, recipe-name asc) to prevent reload-volatility on equal scores.
/// </summary>
public interface IPantryMatchService
{
    /// <summary>
    /// Returns the top-N pantry-match recipes for the given user, ranked by score.
    /// <para>
    /// Returns an empty list when no recipes survive the dietary filter or meet the
    /// minimum coverage ratio. The user's <c>DietaryPreferences</c> are read inside
    /// the service — the caller does not pass them. <paramref name="ct"/> follows the
    /// standard ASP.NET Core / Blazor Server cancellation pattern (pass
    /// <c>CancellationToken.None</c> from tests; pass the page's circuit token from
    /// page code-behind).
    /// </para>
    /// </summary>
    /// <param name="userId">The ID of the user whose pantry and dietary preferences govern the results.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<PantryMatchResult>> GetMatchesAsync(int userId, CancellationToken ct = default);
}
