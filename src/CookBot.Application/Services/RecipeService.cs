using System.Text.Json;
using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;

namespace CookBot.Application.Services;

public class RecipeService
{
    private readonly IRecipeFormatParser _parser;
    private readonly IRepository<Recipe> _recipeRepo;
    private readonly IRepository<Ingredient> _ingredientRepo;

    public RecipeService(
        IRecipeFormatParser parser,
        IRepository<Recipe> recipeRepo,
        IRepository<Ingredient> ingredientRepo)
    {
        _parser = parser;
        _recipeRepo = recipeRepo;
        _ingredientRepo = ingredientRepo;
    }

    public async Task<Recipe> CreateFromRawAsync(int cookbookId, string rawContent)
    {
        var parsed = _parser.Parse(rawContent);
        var recipe = new Recipe
        {
            CookbookId = cookbookId,
            Name = parsed.Name,
            RawContent = rawContent,
            MarkdownBody = parsed.MarkdownBody,
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
                Unit = UnitParser.Parse(pi.Unit),
                Note = pi.Note,
            });
        }

        return await _recipeRepo.AddAsync(recipe);
    }

    public async Task<Recipe> UpdateFromRawAsync(int recipeId, string rawContent)
    {
        var recipe = await _recipeRepo.GetByIdAsync(recipeId)
            ?? throw new InvalidOperationException("Recipe not found.");

        var parsed = _parser.Parse(rawContent);
        recipe.Name = parsed.Name;
        recipe.RawContent = rawContent;
        recipe.MarkdownBody = parsed.MarkdownBody;
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
                Unit = UnitParser.Parse(pi.Unit),
                Note = pi.Note,
            });
        }

        await _recipeRepo.UpdateAsync(recipe);
        return recipe;
    }

    public async Task DeleteAsync(Recipe recipe) =>
        await _recipeRepo.DeleteAsync(recipe);

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
