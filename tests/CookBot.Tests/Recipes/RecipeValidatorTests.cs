using System.Linq;
using CookBot.Application.Recipes;
using CookBot.Domain.Recipes;

namespace CookBot.Tests.Recipes;

/// <summary>
/// FORMAT-07 / D-08 — verifies <see cref="RecipeValidator"/> returns a
/// <see cref="ValidationResult"/> data envelope and never throws (including on null input).
/// Covers each error code: REQUIRED, OUT_OF_RANGE, DUPLICATE_ID, DANGLING_REF.
/// </summary>
public class RecipeValidatorTests
{
    private readonly RecipeValidator _validator = new();

    [Fact]
    public void Validate_ValidDocument_NoErrors()
    {
        var doc = new RecipeDocument
        {
            Version = 2,
            Name = "X",
            Servings = 4,
            Ingredients = new IngredientEntry[]
            {
                new() { Id = 1, Name = "Salt" },
            },
            Steps = new StepNode[]
            {
                new ContentStep { Text = "Use [salt](#1)." },
            },
        };
        var r = _validator.Validate(doc);
        Assert.True(r.IsValid, $"Expected valid; got: {string.Join(", ", r.Errors.Select(e => $"{e.Path} {e.Code}"))}");
    }

    [Fact]
    public void Validate_EmptyName_ProducesRequiredError()
    {
        var doc = new RecipeDocument { Version = 2, Name = " ", Servings = 1 };
        var r = _validator.Validate(doc);
        Assert.Contains(r.Errors, e => e.Path == "/name" && e.Code == "REQUIRED");
    }

    [Fact]
    public void Validate_NonPositiveServings_ProducesOutOfRangeError()
    {
        var doc = new RecipeDocument { Version = 2, Name = "X", Servings = 0 };
        var r = _validator.Validate(doc);
        Assert.Contains(r.Errors, e => e.Path == "/servings" && e.Code == "OUT_OF_RANGE");
    }

    [Fact]
    public void Validate_DuplicateIngredientIds_ProducesDuplicateIdError()
    {
        var doc = new RecipeDocument
        {
            Version = 2,
            Name = "X",
            Servings = 1,
            Ingredients = new IngredientEntry[]
            {
                new() { Id = 1, Name = "A" },
                new() { Id = 1, Name = "B" },
            },
        };
        var r = _validator.Validate(doc);
        Assert.Contains(r.Errors, e => e.Code == "DUPLICATE_ID");
    }

    [Fact]
    public void Validate_DanglingIngredientRef_ProducesDanglingRefError()
    {
        var doc = new RecipeDocument
        {
            Version = 2,
            Name = "X",
            Servings = 1,
            Ingredients = new IngredientEntry[]
            {
                new() { Id = 1, Name = "A" },
            },
            Steps = new StepNode[]
            {
                new ContentStep { Text = "Use [missing](#99)." },
            },
        };
        var r = _validator.Validate(doc);
        Assert.Contains(r.Errors, e => e.Code == "DANGLING_REF");
    }

    [Fact]
    public void Validate_EmptySectionHeading_ProducesRequiredError()
    {
        var doc = new RecipeDocument
        {
            Version = 2,
            Name = "X",
            Servings = 1,
            Steps = new StepNode[]
            {
                new SectionStep { Heading = " " },
            },
        };
        var r = _validator.Validate(doc);
        Assert.Contains(r.Errors, e => e.Code == "REQUIRED" && e.Path.EndsWith("/heading"));
    }

    [Fact]
    public void Validate_NullDocument_DoesNotThrow()
    {
        var r = _validator.Validate(null!);
        Assert.NotNull(r);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Path == "/" && e.Code == "REQUIRED");
    }
}
