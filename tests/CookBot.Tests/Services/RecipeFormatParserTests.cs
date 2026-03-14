using CookBot.Application.Services;
using CookBot.Domain.Interfaces;

namespace CookBot.Tests.Services;

public class RecipeFormatParserTests
{
    private readonly RecipeFormatParser _parser = new();

    [Fact]
    public void Parse_StructuredYamlWithSteps_ReturnsSteps()
    {
        var input = "---\nname: \"Test Recipe\"\nservings: 4\nprepTime: 10\ncookTime: 20\ntags: [easy, dinner]\ningredients:\n  - id: 1\n    name: \"flour\"\n    amount: 2\n    unit: \"cups\"\n  - id: 2\n    name: \"butter\"\n    amount: 1\n    unit: \"tbsp\"\nsteps:\n  - text: \"Mix [flour](#1) and [butter](#2).\"\n  - section: \"For the topping\"\n  - text: \"Bake for 25 minutes.\"\n---\n";

        var result = _parser.Parse(input);
        Assert.Equal("Test Recipe", result.Name);
        Assert.Equal(4, result.Servings);
        Assert.Equal(2, result.Ingredients.Count);
        Assert.Equal(3, result.Steps.Count);
        Assert.Equal("Mix [flour](#1) and [butter](#2).", result.Steps[0].Text);
        Assert.False(result.Steps[0].IsSection);
        Assert.True(result.Steps[1].IsSection);
        Assert.Equal("For the topping", result.Steps[1].Text);
        Assert.Equal("Bake for 25 minutes.", result.Steps[2].Text);
    }

    [Fact]
    public void Parse_PlainNumberedSteps_Fallback()
    {
        var input = "---\nname: \"Simple Recipe\"\nservings: 2\ningredients:\n  - id: 1\n    name: \"rice\"\n    amount: 1\n    unit: \"cup\"\n---\n\n1. Wash the rice.\n2. Cook for 20 minutes.\n3. Fluff with a fork.\n";

        var result = _parser.Parse(input);
        Assert.Equal(3, result.Steps.Count);
        Assert.Equal("Wash the rice.", result.Steps[0].Text);
        Assert.Equal("Cook for 20 minutes.", result.Steps[1].Text);
        Assert.Equal("Fluff with a fork.", result.Steps[2].Text);
    }

    [Fact]
    public void TryParse_InvalidYaml_ReturnsFalse()
    {
        var ok = _parser.TryParse("not yaml", out var recipe, out var errors);
        Assert.False(ok);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Parse_FlexibleUnits_PreservedAsString()
    {
        var input = "---\nname: \"Flexible Units\"\nservings: 1\ningredients:\n  - id: 1\n    name: \"olive oil\"\n    amount: 1\n    unit: \"splash\"\nsteps:\n  - text: \"Add olive oil.\"\n---\n";

        var result = _parser.Parse(input);
        Assert.Equal("splash", result.Ingredients[0].Unit);
    }
}
