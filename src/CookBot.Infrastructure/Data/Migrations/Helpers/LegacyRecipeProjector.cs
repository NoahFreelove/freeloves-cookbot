using System.Text.Json;
using CookBot.Application.Recipes;
using CookBot.Domain.Entities;
using CookBot.Domain.Recipes;

namespace CookBot.Infrastructure.Data.Migrations.Helpers;

/// <summary>
/// Projects a relational <see cref="Recipe"/> entity onto a canonical <see cref="RecipeDocument"/>
/// at <see cref="RecipeUpcasterChain.CurrentVersion"/> (D-14).
///
/// DELETE-AFTER-V1.1: this helper is throwaway. Phase 4 (POLISH-03) drops it together with
/// the legacy ingredient-refs column on the relational step entity.
/// </summary>
public sealed class LegacyRecipeProjector : IRecipeProjector
{
    public RecipeDocument Project(Recipe recipe)
    {
        var tags = TryDeserializeTags(recipe.TagsJson);

        var ingredients = recipe.RecipeIngredients
            .OrderBy(ri => ri.RecipeLocalId)
            .Select(ri => new IngredientEntry
            {
                Id = ri.RecipeLocalId,
                Name = ri.Ingredient?.Name ?? string.Empty,
                Amount = ri.Amount,
                Unit = ri.Unit,
                Note = ri.Note,
            })
            .ToList();

        var steps = recipe.Steps
            .OrderBy(s => s.Order)
            .Select<RecipeStep, StepNode>(s => s.IsSection
                ? new SectionStep { Heading = s.Text }
                : new ContentStep
                {
                    Text = s.Text,
                    Timers = s.Timers != null && s.Timers.Count > 0
                        ? s.Timers.Select(t => new TimerEntry
                        {
                            Duration = t.Duration,
                            Unit = t.Unit,
                            Label = t.Label,
                        }).ToList()
                        : null,
                })
            .ToList();

        return new RecipeDocument
        {
            Version = RecipeUpcasterChain.CurrentVersion,
            Name = recipe.Name,
            Servings = recipe.Servings,
            PrepTimeMinutes = recipe.PrepTimeMinutes,
            CookTimeMinutes = recipe.CookTimeMinutes,
            Tags = tags,
            Ingredients = ingredients,
            Steps = steps,
        };
    }

    private static IReadOnlyList<string> TryDeserializeTags(string tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(tagsJson) ?? []; }
        catch { return []; }
    }
}
