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

    // ── D-27 per-unit Temperature validation (10 rows) ─────────────────────

    /// <summary>
    /// Builds a minimal single-step RecipeDocument with the given StepTemperature for testing.
    /// </summary>
    private static RecipeDocument BuildDocWithTemperature(StepTemperature temperature)
        => new()
        {
            Version = 2,
            Name = "Temp Test",
            Servings = 1,
            Steps = new StepNode[] { new ContentStep { Text = "Cook.", Temperature = temperature } },
        };

    [Theory]
    [InlineData("F", 350, false)]      // F whole-degree → valid
    [InlineData("F", 350.5, true)]     // F fractional → INVALID_TEMPERATURE
    [InlineData("C", 180, false)]      // C whole-degree → valid
    [InlineData("C", 180.5, true)]     // C fractional → INVALID_TEMPERATURE
    [InlineData("Gas", 4, false)]      // Gas whole-step in range → valid
    [InlineData("Gas", 4.5, false)]    // Gas 0.5-step in range → valid
    [InlineData("Gas", 0.5, true)]     // Gas below 1.0 → INVALID_TEMPERATURE
    [InlineData("Gas", 9.5, false)]    // Gas at ceiling → valid
    [InlineData("Gas", 10, true)]      // Gas above 9.5 → INVALID_TEMPERATURE
    [InlineData("Gas", 4.25, true)]    // Gas not 0.5-step → INVALID_TEMPERATURE
    public void Temperature_Validation_PerUnitRules(string unitStr, double valueDouble, bool expectInvalid)
    {
        var unit = unitStr switch
        {
            "F" => TemperatureUnit.F,
            "C" => TemperatureUnit.C,
            "Gas" => TemperatureUnit.Gas,
            _ => throw new ArgumentOutOfRangeException(nameof(unitStr)),
        };
        var doc = BuildDocWithTemperature(new StepTemperature { Value = (decimal)valueDouble, Unit = unit });
        var result = _validator.Validate(doc);

        if (expectInvalid)
        {
            Assert.True(
                result.Errors.Any(e => e.Code == "INVALID_TEMPERATURE"),
                $"Expected INVALID_TEMPERATURE for {unitStr}={valueDouble} but got: {string.Join(", ", result.Errors.Select(e => $"{e.Path} {e.Code}"))}");
        }
        else
        {
            Assert.True(result.IsValid,
                $"Expected valid for {unitStr}={valueDouble} but got errors: {string.Join(", ", result.Errors.Select(e => $"{e.Path} {e.Code} {e.Message}"))}");
        }
    }

    [Fact]
    public void Temperature_NullTemperature_IsValid()
    {
        // Temperature == null is valid; no INVALID_TEMPERATURE error produced
        var doc = new RecipeDocument
        {
            Version = 2,
            Name = "X",
            Servings = 1,
            Steps = new StepNode[] { new ContentStep { Text = "Cook.", Temperature = null } },
        };
        var result = _validator.Validate(doc);
        Assert.DoesNotContain(result.Errors, e => e.Code == "INVALID_TEMPERATURE");
    }
}
