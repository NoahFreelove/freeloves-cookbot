using System.Text;
using System.Text.Json;
using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;

namespace CookBot.Application.Services;

/// <summary>
/// Builds prompts for AI assistance during cooking mode (full recipe + highlighted step).
/// </summary>
public static class RecipeCookingAiContext
{
    public static ParsedRecipe ToParsedRecipe(Recipe recipe, int targetServings)
    {
        var baseServings = recipe.Servings > 0 ? recipe.Servings : 1;
        targetServings = Math.Max(1, targetServings);

        var tags = JsonSerializer.Deserialize<List<string>>(recipe.TagsJson ?? "[]") ?? new();
        var ingredients = recipe.RecipeIngredients
            .OrderBy(ri => ri.RecipeLocalId)
            .Select(ri => new ParsedIngredient
            {
                LocalId = ri.RecipeLocalId,
                Name = ri.Ingredient.Name,
                Amount = RecipeScalingService.ScaleAmount(ri.Amount, baseServings, targetServings),
                Unit = ri.Unit,
                Note = ri.Note,
            })
            .ToList();

        var steps = recipe.Steps
            .OrderBy(s => s.Order)
            .Select(s => new ParsedStep { Text = s.Text, IsSection = s.IsSection })
            .ToList();

        return new ParsedRecipe
        {
            Name = recipe.Name,
            Servings = targetServings,
            PrepTimeMinutes = recipe.PrepTimeMinutes,
            CookTimeMinutes = recipe.CookTimeMinutes,
            Tags = tags,
            Ingredients = ingredients,
            Steps = steps,
        };
    }

    public static string BuildUserMessage(
        Recipe recipe,
        int targetServings,
        int currentNavigableIndex,
        IReadOnlyList<RecipeStep> navigableSteps,
        string? currentSectionHeader,
        string question,
        IRecipeFormatParser parser)
    {
        var parsed = ToParsedRecipe(recipe, targetServings);
        var yaml = parser.Serialize(parsed).Trim();
        var step = navigableSteps[currentNavigableIndex];
        var stepHuman = currentNavigableIndex + 1;

        var refs = new StringBuilder();
        foreach (var id in step.IngredientRefs)
        {
            var ri = recipe.RecipeIngredients.FirstOrDefault(x => x.RecipeLocalId == id);
            if (ri != null)
                refs.AppendLine($"  - id {id}: {ri.Ingredient.Name}");
            else
                refs.AppendLine($"  - id {id}: (unknown)");
        }

        var refsBlock = refs.Length > 0 ? refs.ToString().TrimEnd() : "  (none — no ingredient links detected in this step text)";

        var baseServings = recipe.Servings > 0 ? recipe.Servings : 1;
        return $"""
            The user is in **Cooking Mode** in CookBot. Ingredient amounts in the YAML below are scaled for **{targetServings} servings** (the recipe was originally written for {baseServings} servings).

            ## CURRENT STEP (prioritize this)
            - **Instruction step** {stepHuman} of {navigableSteps.Count} (section headers are not counted).
            - **Section heading:** {(string.IsNullOrEmpty(currentSectionHeader) ? "(none)" : currentSectionHeader)}
            - **Instruction text:** {step.Text}

            **Ingredient IDs referenced in this step** (see YAML `ingredients` for names and amounts):
            {refsBlock}

            ## FULL RECIPE (CookBot YAML; amounts already scaled)
            ```recipe
            {yaml}
            ```

            ## USER QUESTION
            {question.Trim()}
            """;
    }
}
