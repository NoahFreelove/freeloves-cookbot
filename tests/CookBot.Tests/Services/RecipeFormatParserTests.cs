using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Interfaces;

namespace CookBot.Tests.Services;

public class RecipeFormatParserTests
{
    private static RecipeFormatParser CreateParser()
    {
        var chain = new RecipeUpcasterChain(new IRecipeUpcaster[] { new Migration_V1_To_V2() });
        return new RecipeFormatParser(chain, new JsonRecipeSerializer(), new RecipeValidator());
    }

    private readonly RecipeFormatParser _parser = CreateParser();

    [Fact]
    public void Parse_StructuredYamlWithSteps_ReturnsSteps()
    {
        var input = "---\nname: \"Test Recipe\"\nservings: 4\nprepTime: 10\ncookTime: 20\ntags: [easy, dinner]\ningredients:\n  - id: 1\n    name: \"flour\"\n    amount: 2\n    unit: \"cups\"\n  - id: 2\n    name: \"butter\"\n    amount: 1\n    unit: \"tbsp\"\nsteps:\n  - text: \"Mix [flour](#1) and [butter](#2).\"\n  - section: \"For the topping\"\n  - text: \"Bake for 25 minutes.\"\n---\n";

        var result = _parser.Parse(input);
        Assert.Equal("Test Recipe", result.Name);
        Assert.Equal(4, result.Servings);
        Assert.Equal(10, result.PrepTimeMinutes);
        Assert.Equal(20, result.CookTimeMinutes);
        Assert.Equal(2, result.Ingredients.Count);
        Assert.Equal(3, result.Steps.Count);
        Assert.Equal("Mix [flour](#1) and [butter](#2).", result.Steps[0].Text);
        Assert.False(result.Steps[0].IsSection);
        Assert.True(result.Steps[1].IsSection);
        Assert.Equal("For the topping", result.Steps[1].Text);
        Assert.Equal("Bake for 25 minutes.", result.Steps[2].Text);
    }

    [Fact]
    public void Parse_NumberedStepsInMarkdownBody_NotPromotedToSteps()
    {
        // Plan 01-02: the old "numbered-step markdown body fallback" is no longer part of
        // the canonical pipeline. Frontmatter is the single source of structured steps;
        // a recipe with no `steps:` key has no steps. The parser still succeeds because
        // RecipeValidator does not require non-empty steps.
        var input = "---\nname: \"Simple Recipe\"\nservings: 2\ningredients:\n  - id: 1\n    name: \"rice\"\n    amount: 1\n    unit: \"cup\"\n---\n\n1. Wash the rice.\n2. Cook for 20 minutes.\n3. Fluff with a fork.\n";

        var ok = _parser.TryParse(input, out var result, out var errors);
        Assert.True(ok, $"Expected parse to succeed; errors: {string.Join("; ", errors)}");
        Assert.NotNull(result);
        Assert.Empty(result!.Steps);
    }

    [Fact]
    public void TryParse_InvalidYaml_ReturnsFalse()
    {
        var ok = _parser.TryParse("not yaml", out var recipe, out var errors);
        Assert.False(ok);
        Assert.NotEmpty(errors);
        Assert.Null(recipe);
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalseWithEmptyError()
    {
        var ok = _parser.TryParse("", out var recipe, out var errors);
        Assert.False(ok);
        Assert.Null(recipe);
        Assert.Contains("Recipe content is empty.", errors);
    }

    [Fact]
    public void Parse_FlexibleUnits_PreservedAsString()
    {
        var input = "---\nname: \"Flexible Units\"\nservings: 1\ningredients:\n  - id: 1\n    name: \"olive oil\"\n    amount: 1\n    unit: \"splash\"\nsteps:\n  - text: \"Add olive oil.\"\n---\n";

        var result = _parser.Parse(input);
        Assert.Equal("splash", result.Ingredients[0].Unit);
    }

    [Fact]
    public void TryParse_V2CanonicalJson_ReturnsTrue()
    {
        // v2 canonical JSON document — no version stamp needed (already at current version).
        var input = "{\"version\":2,\"name\":\"V2 Recipe\",\"servings\":2,\"prepTimeMinutes\":5,\"cookTimeMinutes\":15,\"tags\":[\"quick\"],\"ingredients\":[{\"id\":1,\"name\":\"egg\",\"amount\":2,\"unit\":\"each\"}],\"steps\":[{\"kind\":\"content\",\"text\":\"Crack [egg](#1).\"}]}";

        var ok = _parser.TryParse(input, out var recipe, out var errors);
        Assert.True(ok, $"Expected parse to succeed; errors: {string.Join("; ", errors)}");
        Assert.NotNull(recipe);
        Assert.Equal("V2 Recipe", recipe!.Name);
        Assert.Equal(5, recipe.PrepTimeMinutes);
        Assert.Equal(15, recipe.CookTimeMinutes);
        Assert.Single(recipe.Ingredients);
        Assert.Equal(1, recipe.Ingredients[0].LocalId);
        Assert.Single(recipe.Steps);
        Assert.False(recipe.Steps[0].IsSection);
    }

    [Fact]
    public void TryParse_V1JsonExport_UpcastsToV2Shape()
    {
        // v1 JSON export with prepTime/cookTime/isSection/localId — upcaster reconciles.
        var input = "{\"name\":\"V1 Export\",\"servings\":3,\"prepTime\":12,\"cookTime\":25,\"tags\":[],\"ingredients\":[{\"localId\":1,\"name\":\"sugar\",\"amount\":1,\"unit\":\"cup\"}],\"steps\":[{\"isSection\":true,\"text\":\"Sweet base\"},{\"text\":\"Mix [sugar](#1).\"}]}";

        var ok = _parser.TryParse(input, out var recipe, out var errors);
        Assert.True(ok, $"Expected parse to succeed; errors: {string.Join("; ", errors)}");
        Assert.NotNull(recipe);
        Assert.Equal(12, recipe!.PrepTimeMinutes);
        Assert.Equal(25, recipe.CookTimeMinutes);
        Assert.Equal(1, recipe.Ingredients[0].LocalId);
        Assert.Equal(2, recipe.Steps.Count);
        Assert.True(recipe.Steps[0].IsSection);
        Assert.Equal("Sweet base", recipe.Steps[0].Text);
        Assert.False(recipe.Steps[1].IsSection);
    }

    [Fact]
    public void TryParse_DanglingIngredientRef_ReturnsValidationError()
    {
        var input = "---\nname: \"Bad Refs\"\nservings: 1\ningredients:\n  - id: 1\n    name: \"flour\"\n    amount: 1\n    unit: \"cup\"\nsteps:\n  - text: \"Add [ginger](#99).\"\n---\n";

        var ok = _parser.TryParse(input, out var recipe, out var errors);
        Assert.False(ok);
        Assert.Null(recipe);
        Assert.Contains(errors, e => e.Contains("DANGLING_REF") || e.Contains("not in ingredients"));
    }

    [Fact]
    public void TryParse_YamlWithUnknownField_PreservedInExtras()
    {
        // FORMAT-09 forward-compat: unknown top-level YAML keys round-trip into the
        // RecipeDocument.Extras dictionary. Re-serializing through JsonRecipeSerializer
        // emits them again. The legacy ParsedRecipe doesn't carry Extras, but the parse
        // itself must succeed.
        var input = "---\nname: \"Forward Compat\"\nservings: 1\ncustomField: foo\ningredients:\n  - id: 1\n    name: \"water\"\n    amount: 1\n    unit: \"cup\"\nsteps:\n  - text: \"Drink the water.\"\n---\n";

        var ok = _parser.TryParse(input, out var recipe, out var errors);
        Assert.True(ok, $"Expected parse to succeed; errors: {string.Join("; ", errors)}");
        Assert.NotNull(recipe);
        Assert.Equal("Forward Compat", recipe!.Name);
    }

    [Fact]
    public void TryParse_NonZeroPrepCookTime_RoundTrips()
    {
        // Pitfall C2 — the v1 prepTime/cookTime keys must survive the upcaster and
        // emerge as PrepTimeMinutes/CookTimeMinutes with the original values intact.
        var input = "---\nname: \"Times\"\nservings: 1\nprepTime: 7\ncookTime: 42\ningredients:\n  - id: 1\n    name: \"x\"\n    amount: 1\n    unit: \"each\"\nsteps:\n  - text: \"Do x.\"\n---\n";

        var result = _parser.Parse(input);
        Assert.Equal(7, result.PrepTimeMinutes);
        Assert.Equal(42, result.CookTimeMinutes);
    }
}
