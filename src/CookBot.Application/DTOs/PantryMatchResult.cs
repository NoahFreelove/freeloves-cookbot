namespace CookBot.Application.DTOs;

/// <summary>
/// Phase 10 / QOL-01..03 — Application-layer result record returned by
/// <see cref="Services.IPantryMatchService.GetMatchesAsync"/>.
/// Mirrors the shape of <c>Home.razor.cs:470 HomePantryMatch</c> so that
/// the Home page swap (Plan 10-04) is mechanical: Home.razor.cs projects from
/// this record into the view-layer <c>HomePantryMatch</c> positional record.
/// </summary>
public sealed record PantryMatchResult(
    int RecipeId,
    string RecipeName,
    int MatchedCount,
    int TotalCount,
    double Score,
    string? PhotoUrl,
    string? FirstMissingIngredientName);
