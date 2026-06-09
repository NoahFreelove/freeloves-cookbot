using System.Text.Json;
using CookBot.Application.Recipes;
using CookBot.Domain.Recipes;

namespace CookBot.Tests.Recipes;

// [UseVerify] is injected at assembly level by the Verify.Xunit MSBuild target — no class attribute needed.
public class JsonLdRecipeProjectorTests
{
    /// <summary>
    /// Golden-file snapshot test: a fully-populated v4 RecipeDocument projects to the expected JSON-LD shape.
    /// Review and commit the generated .verified.txt under Snapshots/ after first run.
    /// </summary>
    [Fact]
    public Task FullDocument_ProducesExpectedJsonLd()
    {
        var doc = MakeFullDocument();
        var actual = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: "https://host/img.jpg");
        return Verifier.Verify(actual);
    }

    [Fact]
    public void Image_OmittedWhenNull()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Simple Soup",
        };
        var output = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: null);
        using var parsed = JsonDocument.Parse(output);
        Assert.False(parsed.RootElement.TryGetProperty("image", out _), "image should be absent when absoluteImageUrl is null");
    }

    [Fact]
    public void NeverEmitsAggregateRating()
    {
        var doc = MakeFullDocument();
        var output = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: "https://host/img.jpg");
        Assert.DoesNotContain("aggregateRating", output, StringComparison.Ordinal);
        Assert.DoesNotContain("review", output, StringComparison.Ordinal);
        Assert.DoesNotContain("datePublished", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Durations_AreIso8601()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Timed Recipe",
            PrepTimeMinutes = 30,
            CookTimeMinutes = 90,
        };
        var output = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: null);
        using var parsed = JsonDocument.Parse(output);

        Assert.Equal("PT30M", parsed.RootElement.GetProperty("prepTime").GetString());
        Assert.Equal("PT1H30M", parsed.RootElement.GetProperty("cookTime").GetString());
        Assert.Equal("PT2H", parsed.RootElement.GetProperty("totalTime").GetString());
    }

    [Fact]
    public void Durations_NullMinutes_PropertyAbsent()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "No Time Recipe",
            PrepTimeMinutes = null,
            CookTimeMinutes = null,
        };
        var output = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: null);
        using var parsed = JsonDocument.Parse(output);

        Assert.False(parsed.RootElement.TryGetProperty("prepTime", out _), "prepTime should be absent when null");
        Assert.False(parsed.RootElement.TryGetProperty("cookTime", out _), "cookTime should be absent when null");
        Assert.False(parsed.RootElement.TryGetProperty("totalTime", out _), "totalTime should be absent when both null");
    }

    [Fact]
    public void ScriptBreakout_IsEscaped()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Mom's <best> & \"great\" cake </script>",
        };
        var output = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: null);

        // Must parse as valid JSON
        using var parsed = JsonDocument.Parse(output);

        // The raw string must not contain unescaped </script>
        Assert.DoesNotContain("</script>", output, StringComparison.OrdinalIgnoreCase);
        // Must contain escaped form
        Assert.Contains("\\u003C", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tags_AllBecomeKeywords()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Quick Weeknight Meal",
            Tags = ["weeknight", "quick"],
        };
        var output = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: null);
        using var parsed = JsonDocument.Parse(output);

        var keywords = parsed.RootElement.GetProperty("keywords").GetString();
        Assert.NotNull(keywords);
        Assert.Contains("weeknight", keywords, StringComparison.Ordinal);
        Assert.Contains("quick", keywords, StringComparison.Ordinal);
    }

    [Fact]
    public void Cuisine_FromAllowList()
    {
        // Exact case match
        var docExact = new RecipeDocument
        {
            Version = 4,
            Name = "Italian Pasta",
            Tags = ["Italian", "weeknight"],
        };
        var outputExact = JsonLdRecipeProjector.Project(docExact, absoluteImageUrl: null);
        using var parsedExact = JsonDocument.Parse(outputExact);
        Assert.Equal("Italian", parsedExact.RootElement.GetProperty("recipeCuisine").GetString());

        // Lowercase should also match and emit the curated spelling
        var docLower = new RecipeDocument
        {
            Version = 4,
            Name = "Italian Pasta",
            Tags = ["italian", "weeknight"],
        };
        var outputLower = JsonLdRecipeProjector.Project(docLower, absoluteImageUrl: null);
        using var parsedLower = JsonDocument.Parse(outputLower);
        Assert.Equal("Italian", parsedLower.RootElement.GetProperty("recipeCuisine").GetString());
    }

    [Fact]
    public void Category_FromAllowList()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Chocolate Cake",
            Tags = ["Dessert", "sweet"],
        };
        var output = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: null);
        using var parsed = JsonDocument.Parse(output);
        Assert.Equal("Dessert", parsed.RootElement.GetProperty("recipeCategory").GetString());
    }

    [Fact]
    public void NoMatch_OmitsCategoryAndCuisine()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Weeknight Meal",
            Tags = ["weeknight"],
        };
        var output = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: null);
        using var parsed = JsonDocument.Parse(output);

        // Neither category nor cuisine when no tag matches allow-list
        Assert.False(parsed.RootElement.TryGetProperty("recipeCategory", out _), "recipeCategory should be absent for unmatched tags");
        Assert.False(parsed.RootElement.TryGetProperty("recipeCuisine", out _), "recipeCuisine should be absent for unmatched tags");

        // But keywords should still include the tag
        var keywords = parsed.RootElement.GetProperty("keywords").GetString();
        Assert.NotNull(keywords);
        Assert.Contains("weeknight", keywords, StringComparison.Ordinal);
    }

    [Fact]
    public void Author_FromAuthorName()
    {
        // AuthorName present
        var docWithAuthor = new RecipeDocument
        {
            Version = 4,
            Name = "Jane's Recipe",
            Provenance = new RecipeProvenance { AuthorName = "Jane" },
        };
        var outputWithAuthor = JsonLdRecipeProjector.Project(docWithAuthor, absoluteImageUrl: null);
        using var parsedWithAuthor = JsonDocument.Parse(outputWithAuthor);
        var author = parsedWithAuthor.RootElement.GetProperty("author");
        Assert.Equal("Person", author.GetProperty("@type").GetString());
        Assert.Equal("Jane", author.GetProperty("name").GetString());

        // AuthorName null - no author key
        var docNoAuthor = new RecipeDocument
        {
            Version = 4,
            Name = "Unknown Recipe",
            Provenance = new RecipeProvenance { AuthorName = null },
        };
        var outputNoAuthor = JsonLdRecipeProjector.Project(docNoAuthor, absoluteImageUrl: null);
        using var parsedNoAuthor = JsonDocument.Parse(outputNoAuthor);
        Assert.False(parsedNoAuthor.RootElement.TryGetProperty("author", out _), "author should be absent when AuthorName is null");
    }

    /// <summary>
    /// WR-06: A trailing SectionStep with no following ContentStep must NOT emit an empty
    /// HowToSection (itemListElement = []). An empty section is meaningless and some
    /// validators warn on it.
    /// </summary>
    [Fact]
    public void TrailingEmptySection_IsOmitted()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps =
            [
                new SectionStep { Heading = "Intro" },
                new ContentStep { Text = "Prep the ingredients." },
                // Trailing SectionStep with no following ContentStep — must not emit HowToSection
                new SectionStep { Heading = "Trailing Empty Section" },
            ],
        };
        var output = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: null);
        using var parsed = JsonDocument.Parse(output);
        var instructions = parsed.RootElement.GetProperty("recipeInstructions");
        // Must contain only the non-empty section ("Intro")
        foreach (var item in instructions.EnumerateArray())
        {
            if (item.TryGetProperty("@type", out var typeEl) &&
                typeEl.GetString() == "HowToSection")
            {
                var name = item.GetProperty("name").GetString();
                Assert.NotEqual("Trailing Empty Section", name);
                // Also ensure itemListElement is non-empty for the sections that ARE emitted
                var items = item.GetProperty("itemListElement");
                Assert.True(items.GetArrayLength() > 0, "No HowToSection with empty itemListElement should be emitted");
            }
        }
    }

    /// <summary>
    /// WR-06: Two consecutive SectionSteps (the first is empty) — only the second section
    /// that has content should be emitted. The empty first section must be omitted.
    /// </summary>
    [Fact]
    public void ConsecutiveSections_EmptyFirstOmitted()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps =
            [
                new SectionStep { Heading = "Empty Section" },
                // Immediately followed by another section — "Empty Section" has no steps
                new SectionStep { Heading = "Real Section" },
                new ContentStep { Text = "Do the thing." },
            ],
        };
        var output = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: null);
        using var parsed = JsonDocument.Parse(output);
        var instructions = parsed.RootElement.GetProperty("recipeInstructions");
        var sectionNames = instructions.EnumerateArray()
            .Where(el => el.TryGetProperty("@type", out var t) && t.GetString() == "HowToSection")
            .Select(el => el.GetProperty("name").GetString())
            .ToList();
        // "Empty Section" must NOT appear
        Assert.DoesNotContain("Empty Section", sectionNames);
        // "Real Section" must appear
        Assert.Contains("Real Section", sectionNames);
    }

    /// <summary>
    /// WR-04: A unit-less ingredient (empty Unit) must emit "4 eggs" not "4  eggs".
    /// The old interpolation produced a double space when Unit was empty.
    /// </summary>
    [Fact]
    public void UnitlessIngredient_NoDoubleSpace()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Ingredients =
            [
                new IngredientEntry { Id = 1, Name = "eggs", Amount = 4, Unit = "" },
            ],
        };
        var output = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: null);
        using var parsed = JsonDocument.Parse(output);
        var ingredients = parsed.RootElement.GetProperty("recipeIngredient");
        var eggLine = ingredients.EnumerateArray().First().GetString()!;
        // Must be "4 eggs" — no double space.
        Assert.Equal("4 eggs", eggLine);
        Assert.DoesNotContain("  ", eggLine, StringComparison.Ordinal);
    }

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
