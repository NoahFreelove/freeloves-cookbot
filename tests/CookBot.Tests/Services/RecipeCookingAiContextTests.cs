using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;

namespace CookBot.Tests.Services;

public class RecipeCookingAiContextTests
{
    [Fact]
    public void ToParsedRecipe_ScalesAmounts_ToTargetServings()
    {
        var recipe = new Recipe
        {
            Name = "Test",
            Servings = 4,
            TagsJson = "[]",
            RecipeIngredients =
            {
                new RecipeIngredient
                {
                    RecipeLocalId = 1,
                    Amount = 2,
                    Unit = "cup",
                    Ingredient = new Ingredient { Name = "flour" },
                },
            },
            Steps = { new RecipeStep { Order = 0, Text = "Mix", IsSection = false } },
        };

        var parsed = RecipeCookingAiContext.ToParsedRecipe(recipe, 8);
        Assert.Equal(8, parsed.Servings);
        Assert.Single(parsed.Ingredients);
        Assert.Equal(4, parsed.Ingredients[0].Amount);
    }

    [Fact]
    public void BuildUserMessage_IncludesStepAndYaml()
    {
        var recipe = new Recipe
        {
            Name = "Cookies",
            Servings = 2,
            TagsJson = "[]",
            RecipeIngredients =
            {
                new RecipeIngredient
                {
                    RecipeLocalId = 1,
                    Amount = 1,
                    Unit = "cup",
                    Ingredient = new Ingredient { Name = "sugar" },
                },
            },
            Steps =
            {
                new RecipeStep { Order = 0, Text = "Preheat", IsSection = false, IngredientRefs = new List<int>() },
                new RecipeStep { Order = 1, Text = "Fold in [sugar](#1).", IsSection = false, IngredientRefs = new List<int> { 1 } },
            },
        };

        IRecipeFormatParser parser = new RecipeFormatParser();
        var msg = RecipeCookingAiContext.BuildUserMessage(
            recipe,
            targetServings: 2,
            currentNavigableIndex: 1,
            navigableSteps: recipe.Steps.Where(s => !s.IsSection).OrderBy(s => s.Order).ToList(),
            currentSectionHeader: null,
            question: "How gentle?",
            parser);

        Assert.Contains("CURRENT STEP", msg);
        Assert.Contains("Fold in", msg);
        Assert.Contains("2 of 2", msg);
        Assert.Contains("How gentle?", msg);
        Assert.Contains("sugar", msg);
        Assert.Contains("```recipe", msg);
    }
}
