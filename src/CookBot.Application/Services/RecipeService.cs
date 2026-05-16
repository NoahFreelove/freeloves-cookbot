using System.Text.Json;
using CookBot.Application.Recipes;
using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;
using CookBot.Domain.Recipes;

namespace CookBot.Application.Services;

public class RecipeService
{
    private readonly IRecipeFormatParser _parser;
    private readonly IRepository<Recipe> _recipeRepo;
    private readonly IRepository<Ingredient> _ingredientRepo;
    private readonly IRepository<Cookbook> _cookbookRepo;
    private readonly IRepository<RecipeTag> _recipeTagRepo;
    private readonly JsonRecipeSerializer _canonicalSerializer;

    public RecipeService(
        IRecipeFormatParser parser,
        IRepository<Recipe> recipeRepo,
        IRepository<Ingredient> ingredientRepo,
        IRepository<Cookbook> cookbookRepo,
        IRepository<RecipeTag> recipeTagRepo,
        JsonRecipeSerializer canonicalSerializer)
    {
        _parser = parser;
        _recipeRepo = recipeRepo;
        _ingredientRepo = ingredientRepo;
        _cookbookRepo = cookbookRepo;
        _recipeTagRepo = recipeTagRepo;
        _canonicalSerializer = canonicalSerializer;
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
            TagsJson = JsonSerializer.Serialize(parsed.Tags), // D-26 safety net: Plan 11 removes this line after DropTagsJsonColumn
        };

        // CLEAN-02 (Plan 08): dual-write relational RecipeTag rows alongside TagsJson safety net (D-26).
        // D-34: trim whitespace, preserve case ("Vegan"/"vegan" are distinct tags).
        // NOTE: Callers that READ tags via Recipe.Tags must .Include(r => r.Tags) on the Recipe query.
        // CreateAsync: new entity — Tags collection starts empty, Add works directly without Include.
        foreach (var name in parsed.Tags.Select(t => t.Trim()).Where(t => t.Length > 0))
        {
            recipe.Tags.Add(new RecipeTag { Name = name });
        }

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
                Timers = ps.IsSection
                    ? new()
                    : (ps.Timers ?? new()).Select(t => new StepTimer { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList(),
                // Plan 03-04 / EDITOR-03 final clause: explicit timer chips are the only
                // persisted source. The previous regex-based auto-write fallback (which
                // silently produced timer entries from step text like "Cook 25 minutes")
                // is removed — surfacing detections is now the inline-suggestion popover's
                // job; persistence requires the user to accept a chip.
                //
                // Plan 01-02 / D-13: writes to RecipeStep.IngredientRefs are retired this
                // milestone. The column persists for safe rollback; Phase 4 drops it.
                // Cooking-mode highlighting now resolves [name](#id) links at render time.
            };
            recipe.Steps.Add(step);
        }

        // MIGRATION-03 hybrid persistence: relational columns continue to be written;
        // canonical document JSON is recomputed on every save (Plan 01-03 / D-12).
        // CLEAN-01 (Plan 10 / D-32 step b): direct RecipeDocument construction from parsed.
        // NOTE: Callers that READ tags via Recipe.Tags must .Include(r => r.Tags) on the Recipe query.
        var canonicalDoc = new RecipeDocument
        {
            Version = RecipeUpcasterChain.CurrentVersion,
            Name = parsed.Name,
            Servings = parsed.Servings,
            PrepTimeMinutes = parsed.PrepTimeMinutes,
            CookTimeMinutes = parsed.CookTimeMinutes,
            PhotoUrl = parsed.PhotoUrl,
            Description = parsed.Description,
            Tags = recipe.Tags.Select(t => t.Name).ToList(),
            Ingredients = parsed.Ingredients.Select(i => new IngredientEntry { Id = i.LocalId, Name = i.Name, Amount = i.Amount, Unit = i.Unit, Note = i.Note }).ToList(),
            Steps = parsed.Steps.Select<ParsedStep, StepNode>(s => s.IsSection
                ? new SectionStep { Heading = s.Text }
                : new ContentStep
                {
                    Text = s.Text,
                    Timers = s.Timers?.Select(t => new TimerEntry { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList(),
                    Temperature = s.Temperature,
                }).ToList(),
        };
        recipe.CanonicalDocumentJson = _canonicalSerializer.Serialize(canonicalDoc);

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
        recipe.TagsJson = JsonSerializer.Serialize(parsed.Tags); // D-26 safety net: Plan 11 removes this line after DropTagsJsonColumn
        recipe.UpdatedAt = DateTime.UtcNow;

        // CLEAN-02 (Plan 08): dual-write relational RecipeTag rows alongside TagsJson safety net (D-26).
        // D-34: trim whitespace, preserve case. Clear existing tags first.
        // If Tags nav is loaded (via change tracker from caller's .Include(r => r.Tags)), Clear() issues
        // EF DELETE commands. For robustness, also explicitly delete via _recipeTagRepo.
        var existingTags = await _recipeTagRepo.FindAsync(t => t.RecipeId == recipe.Id);
        foreach (var tag in existingTags)
            await _recipeTagRepo.DeleteAsync(tag);

        recipe.Tags.Clear();
        foreach (var name in parsed.Tags.Select(t => t.Trim()).Where(t => t.Length > 0))
        {
            recipe.Tags.Add(new RecipeTag { Name = name });
        }

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
                Timers = ps.IsSection
                    ? new()
                    : (ps.Timers ?? new()).Select(t => new StepTimer { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList(),
                // Plan 03-04 / EDITOR-03 final clause: explicit timer chips only. See
                // CreateAsync above for the full rationale.
                //
                // Plan 01-02 / D-13: writes to RecipeStep.IngredientRefs are retired this
                // milestone. See comment in CreateAsync above.
            };
            recipe.Steps.Add(step);
        }

        // MIGRATION-03 hybrid persistence: recompute canonical document on every save.
        // CLEAN-01 (Plan 10 / D-32 step b): direct RecipeDocument construction from parsed.
        // NOTE: Callers that READ tags via Recipe.Tags must .Include(r => r.Tags) on the Recipe query.
        var canonicalDoc = new RecipeDocument
        {
            Version = RecipeUpcasterChain.CurrentVersion,
            Name = parsed.Name,
            Servings = parsed.Servings,
            PrepTimeMinutes = parsed.PrepTimeMinutes,
            CookTimeMinutes = parsed.CookTimeMinutes,
            PhotoUrl = parsed.PhotoUrl,
            Description = parsed.Description,
            Tags = recipe.Tags.Select(t => t.Name).ToList(),
            Ingredients = parsed.Ingredients.Select(i => new IngredientEntry { Id = i.LocalId, Name = i.Name, Amount = i.Amount, Unit = i.Unit, Note = i.Note }).ToList(),
            Steps = parsed.Steps.Select<ParsedStep, StepNode>(s => s.IsSection
                ? new SectionStep { Heading = s.Text }
                : new ContentStep
                {
                    Text = s.Text,
                    Timers = s.Timers?.Select(t => new TimerEntry { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList(),
                    Temperature = s.Temperature,
                }).ToList(),
        };
        recipe.CanonicalDocumentJson = _canonicalSerializer.Serialize(canonicalDoc);

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
