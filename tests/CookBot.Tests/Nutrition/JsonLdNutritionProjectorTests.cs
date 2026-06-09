using System.Text.Json;
using CookBot.Application.DTOs;
using CookBot.Application.Recipes;
using CookBot.Domain.Recipes;

namespace CookBot.Tests.Nutrition;

// [UseVerify] is injected at assembly level by the Verify.Xunit MSBuild target — no class attribute needed.

/// <summary>
/// Tests that JsonLdRecipeProjector correctly emits (or omits) the Schema.org NutritionInformation
/// object based on whether a NutritionInfoDto is supplied (NUTR-06 / SC5 / D-15-13 / Phase 15).
/// The existing Phase 13 golden snapshot (JsonLdRecipeProjectorTests.FullDocument_ProducesExpectedJsonLd)
/// must remain byte-for-byte unchanged — verifying the nutrition-absent path is a regression lock.
/// </summary>
public class JsonLdNutritionProjectorTests
{
    /// <summary>
    /// SC5: When nutrition is null (or not supplied), the "nutrition" key must be absent from
    /// the JSON-LD output. This is the absent-nutrition path regression guard.
    /// </summary>
    [Fact]
    public void Nutrition_OmittedWhenNull()
    {
        var doc = MakeMinimalDocument();
        var output = JsonLdRecipeProjector.Project(doc, null, null);
        using var parsed = JsonDocument.Parse(output);
        Assert.False(
            parsed.RootElement.TryGetProperty("nutrition", out _),
            "nutrition key must be absent when NutritionInfoDto is null (SC5)");
    }

    /// <summary>
    /// NUTR-06: When a NutritionInfoDto is supplied, the JSON-LD must contain a
    /// NutritionInformation object with the correct Schema.org keys and per-serving values.
    /// </summary>
    [Fact]
    public void WithNutrition_IncludesNutritionInformation()
    {
        var doc = MakeMinimalDocument();
        var nutrition = new NutritionInfoDto(
            CaloriesPerServing: 455,
            ProteinGPerServing: 12.9,
            FatGPerServing: 1.2,
            CarbGPerServing: 95.4);

        var output = JsonLdRecipeProjector.Project(doc, "https://host/img.jpg", nutrition);
        using var parsed = JsonDocument.Parse(output);

        Assert.True(
            parsed.RootElement.TryGetProperty("nutrition", out var nutritionEl),
            "nutrition key must be present when NutritionInfoDto is supplied");

        Assert.Equal("NutritionInformation", nutritionEl.GetProperty("@type").GetString());
        Assert.Equal("455 calories", nutritionEl.GetProperty("calories").GetString());
        Assert.Equal("12.9 g", nutritionEl.GetProperty("proteinContent").GetString());
        Assert.Equal("95.4 g", nutritionEl.GetProperty("carbohydrateContent").GetString());
        Assert.Equal("1.2 g", nutritionEl.GetProperty("fatContent").GetString());
    }

    /// <summary>
    /// Rounding: calories must be 0 decimal places; macros must be 1 decimal place.
    /// 455.6 kcal → "456 calories"; 12.94 g protein → "12.9 g".
    /// </summary>
    [Fact]
    public void NutritionCalories_RoundsToWholeNumber()
    {
        var doc = MakeMinimalDocument();
        var nutrition = new NutritionInfoDto(
            CaloriesPerServing: 455.6,
            ProteinGPerServing: 12.94,
            FatGPerServing: 1.25,
            CarbGPerServing: 95.44);

        var output = JsonLdRecipeProjector.Project(doc, null, nutrition);
        using var parsed = JsonDocument.Parse(output);

        var nutritionEl = parsed.RootElement.GetProperty("nutrition");
        Assert.Equal("456 calories", nutritionEl.GetProperty("calories").GetString());
        Assert.Equal("12.9 g", nutritionEl.GetProperty("proteinContent").GetString());
        Assert.Equal("95.4 g", nutritionEl.GetProperty("carbohydrateContent").GetString());
        Assert.Equal("1.3 g", nutritionEl.GetProperty("fatContent").GetString());
    }

    /// <summary>
    /// Regression guard: the Phase 13 baseline golden snapshot
    /// (JsonLdRecipeProjectorTests.FullDocument_ProducesExpectedJsonLd) continues to pass
    /// byte-for-byte with the nutrition-absent call. This plan must NOT break it.
    /// This test verifies the same document + call site and asserts no "nutrition" key appears.
    /// </summary>
    [Fact]
    public void Baseline_NutritionAbsentGoldenUnchanged()
    {
        // The same document and call as JsonLdRecipeProjectorTests.FullDocument_ProducesExpectedJsonLd
        // (2-arg call, nutrition absent) — assert that the output does NOT contain "nutrition".
        var doc = MakeFullDocument();
        var output = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: "https://host/img.jpg");
        using var parsed = JsonDocument.Parse(output);
        Assert.False(
            parsed.RootElement.TryGetProperty("nutrition", out _),
            "nutrition key must be absent in the nutrition-absent (Phase 13 baseline) call path");
    }

    /// <summary>
    /// Golden snapshot: a fully-populated document WITH NutritionInfoDto produces the expected
    /// JSON-LD shape including the NutritionInformation block. Review and commit the generated
    /// .verified.txt under Snapshots/ after first run.
    /// </summary>
    [Fact]
    public Task FullDocumentWithNutrition_ProducesExpectedJsonLd()
    {
        var doc = MakeFullDocument();
        var nutrition = new NutritionInfoDto(
            CaloriesPerServing: 455,
            ProteinGPerServing: 12.9,
            FatGPerServing: 1.2,
            CarbGPerServing: 95.4);

        var actual = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: "https://host/img.jpg", nutrition: nutrition);
        return Verifier.Verify(actual);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static RecipeDocument MakeMinimalDocument() => new()
    {
        Version = 4,
        Name = "Simple Soup",
        Servings = 4,
    };

    /// <summary>
    /// Mirror of JsonLdRecipeProjectorTests.MakeFullDocument — same recipe so the
    /// Baseline regression guard verifies the same output as the Phase 13 golden.
    /// </summary>
    private static RecipeDocument MakeFullDocument() => new()
    {
        Version = 4,
        Name = "Classic Chocolate Cake",
        Description = "A rich and moist chocolate cake.",
        Servings = 8,
        PrepTimeMinutes = 30,
        CookTimeMinutes = 45,
        Tags = ["Dessert", "Italian", "weeknight", "baking"],
        Provenance = new RecipeProvenance { AuthorName = "Chef Maria", SourceName = "Family Recipes" },
        Ingredients =
        [
            new IngredientEntry { Id = 1, Name = "all-purpose flour", Amount = 2.0, Unit = "cups" },
            new IngredientEntry { Id = 2, Name = "cocoa powder", Amount = 0.75, Unit = "cup", Note = "unsweetened" },
            new IngredientEntry { Id = 3, Name = "sugar", Amount = 1.5, Unit = "cups" },
        ],
        Steps =
        [
            new SectionStep { Heading = "Make the Batter" },
            new ContentStep { Text = "Mix [flour](#1) and [cocoa powder](#2) together." },
            new ContentStep { Text = "Add [sugar](#3) and mix until combined.", DonenessCue = "smooth batter" },
            new SectionStep { Heading = "Bake" },
            new ContentStep { Text = "Pour into pan and bake at 350°F for 45 minutes." },
        ],
    };
}
