using System.Text.Json;
using CookBot.Application.DTOs;
using CookBot.Domain.Entities;
using CookBot.Domain.Enums;
using CookBot.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace CookBot.Application.Services;

/// <summary>
/// Phase 10 / QOL-01..03 — Implements the D-44 pantry-match scoring algorithm:
/// <c>score = (matched / total) - RecencyPenaltyWeight * exp(-daysSinceCooked / RecencyHalfLifeDays)</c>.
/// Applies the D-45 AND-combined dietary filter (positive RecipeTag match + negative
/// IngredientCategory exclude) BEFORE scoring. Stable-sorted per PITFALL H8 as
/// (score desc, recipeId asc, recipe-name asc) to prevent reload-volatility.
/// </summary>
public class PantryMatchService : IPantryMatchService
{
    private readonly IRepository<Recipe> _recipeRepo;
    private readonly IRepository<UserProfile> _userProfileRepo;
    private readonly IRepository<RecipeIngredient> _recipeIngredientRepo;
    private readonly IRepository<Ingredient> _ingredientRepo;
    private readonly IRepository<RecipeTag> _recipeTagRepo;
    private readonly IRepository<CookbookShare> _cookbookShareRepo;
    private readonly IRecipeMadeService _recipeMade;
    private readonly PantryService _pantryService;
    private readonly PantryMatchOptions _opts;

    /// <summary>
    /// D-47 (PATTERNS.md correction #4) — hardcoded diet→excluded-IngredientCategory map.
    /// Uses ONLY real enum values from <see cref="IngredientCategory"/> (14 values:
    /// Produce, Dairy, Meat, Seafood, Bakery, Pantry, Frozen, Spices, Condiments,
    /// Beverages, Grains, Canned, Snacks, Other).
    /// Poultry, Fish, and Eggs are NOT in the enum — absent from this map.
    /// Unknown diet labels skip the negative filter; positive RecipeTag match still applies.
    /// </summary>
    private static readonly Dictionary<string, IngredientCategory[]> DietExcludeMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["vegan"]       = [IngredientCategory.Meat, IngredientCategory.Seafood, IngredientCategory.Dairy],
            ["vegetarian"]  = [IngredientCategory.Meat, IngredientCategory.Seafood],
            ["dairy-free"]  = [IngredientCategory.Dairy],
            ["gluten-free"] = [IngredientCategory.Grains, IngredientCategory.Bakery],
        };

    public PantryMatchService(
        IRepository<Recipe> recipeRepo,
        IRepository<UserProfile> userProfileRepo,
        IRepository<RecipeIngredient> recipeIngredientRepo,
        IRepository<Ingredient> ingredientRepo,
        IRepository<RecipeTag> recipeTagRepo,
        IRepository<CookbookShare> cookbookShareRepo,
        IRecipeMadeService recipeMade,
        PantryService pantryService,
        IOptions<PantryMatchOptions> opts)
    {
        _recipeRepo = recipeRepo;
        _userProfileRepo = userProfileRepo;
        _recipeIngredientRepo = recipeIngredientRepo;
        _ingredientRepo = ingredientRepo;
        _recipeTagRepo = recipeTagRepo;
        _cookbookShareRepo = cookbookShareRepo;
        _recipeMade = recipeMade;
        _pantryService = pantryService;
        _opts = opts.Value;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PantryMatchResult>> GetMatchesAsync(int userId, CancellationToken ct = default)
    {
        // 1. Load accessible pantry items — canonical accessor (owned + member pantries)
        var pantryItems = await _pantryService.GetAllUserAccessibleItemsAsync(userId);
        var pantryIngredientIds = pantryItems.Select(p => p.IngredientId).ToHashSet();

        // Short-circuit: no pantry items → nothing can match
        if (pantryIngredientIds.Count == 0)
            return Array.Empty<PantryMatchResult>();

        // 2. Load user profile; parse dietary preferences (canonical pattern from
        //    PromptBuilderService.cs:156-161 — DietaryPreferencesJson is a JSON array string)
        var profileList = await _userProfileRepo.FindAsync(p => p.UserId == userId);
        var profile = profileList.FirstOrDefault();
        var diets = profile is null
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(profile.DietaryPreferencesJson) ?? new();

        // 3. Load candidate recipes for this user (owned + shared via cookbook).
        //    Authz predicate from PATTERNS.md §"Authz pattern" (RecipeMadeService.cs:75-77 analog):
        //    EF Core translates this predicate to SQL JOINs — navigation properties on the
        //    returned Recipe entities will be EMPTY (no Include). Related data is loaded below.
        var recipes = await _recipeRepo.FindAsync(
            r => r.Cookbook.UserId == userId
              || r.Cookbook.Shares.Any(s => s.SharedWithUserId == userId));

        if (recipes.Count == 0)
            return Array.Empty<PantryMatchResult>();

        var recipeIds = recipes.Select(r => r.Id).ToHashSet();

        // 4. Load RecipeIngredients for all candidate recipes (one round-trip, join in memory)
        var allRecipeIngredients = await _recipeIngredientRepo.FindAsync(
            ri => recipeIds.Contains(ri.RecipeId));

        // Group by recipeId for fast lookup
        var ingredientsByRecipe = allRecipeIngredients
            .GroupBy(ri => ri.RecipeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 5. Load Ingredients by ID set for category lookup (diet filter)
        var allIngredientIds = allRecipeIngredients.Select(ri => ri.IngredientId).ToHashSet();
        var allIngredients = await _ingredientRepo.FindAsync(i => allIngredientIds.Contains(i.Id));
        var ingredientById = allIngredients.ToDictionary(i => i.Id);

        // 6. Load RecipeTags for all candidate recipes (positive-tag diet filter)
        var allTags = await _recipeTagRepo.FindAsync(t => recipeIds.Contains(t.RecipeId));
        var tagsByRecipe = allTags
            .GroupBy(t => t.RecipeId)
            .ToDictionary(g => g.Key, g => g.Select(t => t.Name).ToList());

        // 7. Dietary filter (D-45 AND-combined) — runs BEFORE scoring
        //    For each diet preference: (a) positive RecipeTag match AND (b) negative category exclude
        var surviving = recipes.ToList();
        foreach (var pref in diets)
        {
            // (a) Positive RecipeTag match — recipe must have a tag matching this diet label
            surviving = surviving.Where(r =>
            {
                var tags = tagsByRecipe.GetValueOrDefault(r.Id) ?? new List<string>();
                return tags.Any(t => string.Equals(t, pref, StringComparison.OrdinalIgnoreCase));
            }).ToList();

            // (b) Negative IngredientCategory exclude — recipe must have no ingredient in excluded categories
            //     If diet label not in map → skip negative filter (unknown label)
            if (DietExcludeMap.TryGetValue(pref, out var excludedCategories))
            {
                surviving = surviving.Where(r =>
                {
                    var ris = ingredientsByRecipe.GetValueOrDefault(r.Id) ?? new List<RecipeIngredient>();
                    return !ris.Any(ri =>
                        ingredientById.TryGetValue(ri.IngredientId, out var ing)
                        && excludedCategories.Contains(ing.Category));
                }).ToList();
            }
        }

        // 8. Score each surviving recipe
        var scoredResults = new List<(double Score, int RecipeId, string RecipeName, int MatchedCount, int TotalCount, string? PhotoUrl, string? FirstMissingIngredientName)>();

        foreach (var recipe in surviving)
        {
            var ris = ingredientsByRecipe.GetValueOrDefault(recipe.Id) ?? new List<RecipeIngredient>();
            var total = ris.Count;

            // Recipes with no ingredients cannot be scored meaningfully — skip
            if (total == 0)
                continue;

            var matched = ris.Count(ri => pantryIngredientIds.Contains(ri.IngredientId));
            var coverage = (double)matched / total;

            // Drop recipes below the minimum coverage ratio
            if (coverage < _opts.MinCoverageRatio)
                continue;

            // Recency penalty (D-44 formula):
            //   if never cooked: penalty term = 0, score = coverage
            //   if cooked: score = coverage - RecencyPenaltyWeight * exp(-daysSinceCooked / RecencyHalfLifeDays)
            var lastCook = await _recipeMade.GetLastCookAsync(recipe.Id, userId, ct);
            double score;
            if (lastCook is null)
            {
                score = coverage;
            }
            else
            {
                // Clamp to zero so clock-skewed future timestamps don't flip the penalty
                // term into a bonus (exp(positive) > 1 would oversubtract).
                var daysSinceCooked = Math.Max(0.0, (DateTime.UtcNow - lastCook.CompletedAt).TotalDays);
                score = coverage - _opts.RecencyPenaltyWeight
                    * Math.Exp(-daysSinceCooked / _opts.EffectiveHalfLifeDays);
            }

            // First missing ingredient (ordered by natural RI order — Id ascending)
            var firstMissing = ris
                .OrderBy(ri => ri.Id)
                .FirstOrDefault(ri => !pantryIngredientIds.Contains(ri.IngredientId));
            string? firstMissingName = null;
            if (firstMissing != null && ingredientById.TryGetValue(firstMissing.IngredientId, out var missingIng))
                firstMissingName = missingIng.Name;

            scoredResults.Add((score, recipe.Id, recipe.Name, matched, total, recipe.PhotoUrl, firstMissingName));
        }

        // 9. Stable sort per D-44 / PITFALL H8: (score desc, recipeId asc, recipeName asc)
        return scoredResults
            .OrderByDescending(t => t.Score)
            .ThenBy(t => t.RecipeId)
            .ThenBy(t => t.RecipeName, StringComparer.OrdinalIgnoreCase)
            .Take(_opts.ResultCount)
            .Select(t => new PantryMatchResult(
                t.RecipeId,
                t.RecipeName,
                t.MatchedCount,
                t.TotalCount,
                t.Score,
                t.PhotoUrl,
                t.FirstMissingIngredientName))
            .ToList();
    }
}
