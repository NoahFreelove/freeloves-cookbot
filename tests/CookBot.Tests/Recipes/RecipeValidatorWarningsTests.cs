using CookBot.Application.Recipes;
using CookBot.Domain.Recipes;

namespace CookBot.Tests.Recipes;

/// <summary>
/// AI-SPEC §1b "Note on orphan ingredient detection and empty section detection"
/// — orphan ingredients and sections-without-content are surfaced as WARNINGS
/// (not errors), so they do not flip <see cref="ValidationResult.IsValid"/> and
/// do not trigger the repair loop in <see cref="CookBot.Application.AI.AiRecipeGenerator"/>.
/// Plan 02-05 Task 2.
/// </summary>
public class RecipeValidatorWarningsTests
{
    private readonly RecipeValidator _validator = new();

    [Fact]
    public void Validate_OrphanIngredient_AddsWarning_NoError()
    {
        var doc = new RecipeDocument
        {
            Version = 2,
            Name = "Test",
            Servings = 1,
            Ingredients = new IngredientEntry[]
            {
                new() { Id = 1, Name = "flour" },
                new() { Id = 2, Name = "salt" }   // orphan — never referenced
            },
            Steps = new StepNode[]
            {
                new ContentStep { Text = "Mix the [flour](#1)." }
            }
        };

        var result = _validator.Validate(doc);

        Assert.True(result.IsValid, "orphan ingredient must NOT cause validation failure");
        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings,
            w => w.Code == "OrphanIngredient" && w.Path.Contains("ingredients"));
    }

    [Fact]
    public void Validate_EmptySection_AddsWarning_NoError()
    {
        var doc = new RecipeDocument
        {
            Version = 2,
            Name = "Test",
            Servings = 1,
            Ingredients = new IngredientEntry[]
            {
                new() { Id = 1, Name = "flour" }
            },
            Steps = new StepNode[]
            {
                new SectionStep { Heading = "For the empty section" },
                new SectionStep { Heading = "For the cake" },
                new ContentStep { Text = "Mix the [flour](#1)." }
            }
        };

        var result = _validator.Validate(doc);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings,
            w => w.Code == "EmptySection" && w.Path.Contains("steps"));
    }

    [Fact]
    public void Validate_CleanRecipe_NoWarnings()
    {
        var doc = new RecipeDocument
        {
            Version = 2,
            Name = "Test",
            Servings = 1,
            Ingredients = new IngredientEntry[]
            {
                new() { Id = 1, Name = "flour" }
            },
            Steps = new StepNode[]
            {
                new SectionStep { Heading = "Mixing" },
                new ContentStep { Text = "Mix the [flour](#1)." }
            }
        };

        var result = _validator.Validate(doc);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }
}
