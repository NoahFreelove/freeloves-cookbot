using System.Text.Json;
using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;

namespace CookBot.Application.Services;

public class RecipeService
{
    private readonly IRecipeFormatParser _parser;
    private readonly IRepository<Recipe> _recipeRepo;
    private readonly IRepository<Ingredient> _ingredientRepo;
    private readonly IRepository<Cookbook> _cookbookRepo;

    public RecipeService(
        IRecipeFormatParser parser,
        IRepository<Recipe> recipeRepo,
        IRepository<Ingredient> ingredientRepo,
        IRepository<Cookbook> cookbookRepo)
    {
        _parser = parser;
        _recipeRepo = recipeRepo;
        _ingredientRepo = ingredientRepo;
        _cookbookRepo = cookbookRepo;
    }

    public async Task<Recipe> CreateAsync(int cookbookId, int userId, ParsedRecipe parsed)
    {
        var cookbook = await _cookbookRepo.GetByIdAsync(cookbookId)
            ?? throw new InvalidOperationException("Cookbook not found.");

        if (cookbook.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this cookbook.");

        var recipe = new Recipe
        {
            CookbookId = cookbookId,
            Name = parsed.Name,
            Servings = parsed.Servings,
            PrepTimeMinutes = parsed.PrepTimeMinutes,
            CookTimeMinutes = parsed.CookTimeMinutes,
            TagsJson = JsonSerializer.Serialize(parsed.Tags),
        };

        foreach (var pi in parsed.Ingredients)
        {
            var ingredient = await ResolveIngredientAsync(pi.Name);
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                IngredientId = ingredient.Id,
                RecipeLocalId = pi.LocalId,
                Amount = pi.Amount,
                Unit = pi.Unit,
                Note = pi.Note,
            });
        }

        int order = 0;
        foreach (var ps in parsed.Steps)
        {
            var step = new RecipeStep
            {
                Order = order++,
                Text = ps.Text,
                IsSection = ps.IsSection,
                Timers = ps.IsSection ? new() :
                    (ps.Timers?.Any() == true
                        ? ps.Timers.Select(t => new StepTimer { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList()
                        : TimerDetectionService.DetectTimers(ps.Text)),
                // Plan 01-02 / D-13: writes to RecipeStep.IngredientRefs are retired this
                // milestone. The column persists for safe rollback; Phase 4 drops it.
                // Cooking-mode highlighting now resolves [name](#id) links at render time.
            };
            recipe.Steps.Add(step);
        }

        return await _recipeRepo.AddAsync(recipe);
    }

    public async Task<Recipe> CreateFromTextAsync(int cookbookId, int userId, string rawInput)
    {
        var parsed = _parser.Parse(rawInput);
        return await CreateAsync(cookbookId, userId, parsed);
    }

    public async Task<Recipe> UpdateAsync(int recipeId, int userId, ParsedRecipe parsed)
    {
        var recipe = await _recipeRepo.GetByIdAsync(recipeId)
            ?? throw new InvalidOperationException("Recipe not found.");

        var cookbook = await _cookbookRepo.GetByIdAsync(recipe.CookbookId)
            ?? throw new InvalidOperationException("Cookbook not found.");

        if (cookbook.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this cookbook.");

        recipe.Name = parsed.Name;
        recipe.Servings = parsed.Servings;
        recipe.PrepTimeMinutes = parsed.PrepTimeMinutes;
        recipe.CookTimeMinutes = parsed.CookTimeMinutes;
        recipe.TagsJson = JsonSerializer.Serialize(parsed.Tags);
        recipe.UpdatedAt = DateTime.UtcNow;

        recipe.RecipeIngredients.Clear();
        foreach (var pi in parsed.Ingredients)
        {
            var ingredient = await ResolveIngredientAsync(pi.Name);
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                RecipeId = recipe.Id,
                IngredientId = ingredient.Id,
                RecipeLocalId = pi.LocalId,
                Amount = pi.Amount,
                Unit = pi.Unit,
                Note = pi.Note,
            });
        }

        recipe.Steps.Clear();
        int order = 0;
        foreach (var ps in parsed.Steps)
        {
            var step = new RecipeStep
            {
                Order = order++,
                Text = ps.Text,
                IsSection = ps.IsSection,
                Timers = ps.IsSection ? new() :
                    (ps.Timers?.Any() == true
                        ? ps.Timers.Select(t => new StepTimer { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList()
                        : TimerDetectionService.DetectTimers(ps.Text)),
                // Plan 01-02 / D-13: writes to RecipeStep.IngredientRefs are retired this
                // milestone. See comment in CreateAsync above.
            };
            recipe.Steps.Add(step);
        }

        await _recipeRepo.UpdateAsync(recipe);
        return recipe;
    }

    public async Task DeleteAsync(int recipeId, int userId)
    {
        var recipe = await _recipeRepo.GetByIdAsync(recipeId)
            ?? throw new InvalidOperationException("Recipe not found.");

        var cookbook = await _cookbookRepo.GetByIdAsync(recipe.CookbookId)
            ?? throw new InvalidOperationException("Cookbook not found.");

        if (cookbook.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this cookbook.");

        await _recipeRepo.DeleteAsync(recipe);
    }

    private async Task<Ingredient> ResolveIngredientAsync(string name)
    {
        var normalized = IngredientResolver.Normalize(name);
        var existing = await _ingredientRepo.FindAsync(i => i.NormalizedName == normalized);
        if (existing.Any())
            return existing.First();

        return await _ingredientRepo.AddAsync(new Ingredient
        {
            Name = name,
            NormalizedName = normalized,
        });
    }
}
