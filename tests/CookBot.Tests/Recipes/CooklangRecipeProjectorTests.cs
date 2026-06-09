using CookBot.Application.Recipes;
using CookBot.Domain.Recipes;

namespace CookBot.Tests.Recipes;

// [UseVerify] is injected at assembly level by the Verify.Xunit MSBuild target — no class attribute needed.
public class CooklangRecipeProjectorTests
{
    /// <summary>
    /// Golden-file snapshot test: a fully-populated v4 RecipeDocument projects to the expected
    /// Cooklang .cook text. The snapshot MUST end with the trailing Substitution comment block.
    /// Review and commit the generated .verified.txt under Snapshots/ after first run.
    /// </summary>
    [Fact]
    public Task FullDocument_ProducesExpectedCooklang()
    {
        var doc = MakeFullDocument();
        return Verifier.Verify(CooklangRecipeProjector.Project(doc));
    }

    /// <summary>INTEROP-03 / §Pitfall 3: ingredients always use @name{amount%unit} braces form.</summary>
    [Fact]
    public void IngredientsAlwaysBraced()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Ingredients =
            [
                new IngredientEntry { Id = 1, Name = "all-purpose flour", Amount = 2.0, Unit = "cups" },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        Assert.Contains("@all-purpose flour{2%cups}", output, StringComparison.Ordinal);
        // Must NOT appear as bare "@all-purpose" without braces
        Assert.DoesNotContain("@all-purpose flour ", output, StringComparison.Ordinal);
    }

    /// <summary>SectionStep renders as == Heading == (SC2).</summary>
    [Fact]
    public void SectionHeading()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps = [new SectionStep { Heading = "Topping" }],
        };
        var output = CooklangRecipeProjector.Project(doc);
        Assert.Contains("== Topping ==", output, StringComparison.Ordinal);
    }

    /// <summary>TimerEntry renders as ~{Duration%Unit} (always braces form).</summary>
    [Fact]
    public void Timer()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps =
            [
                new ContentStep
                {
                    Text = "Bake until golden.",
                    Timers = [new TimerEntry { Duration = 5, Unit = "min" }],
                },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        Assert.Contains("~{5%min}", output, StringComparison.Ordinal);
    }

    /// <summary>Labeled timer renders as ~Label{Duration%Unit}.</summary>
    [Fact]
    public void Timer_WithLabel()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps =
            [
                new ContentStep
                {
                    Text = "Rest the dough.",
                    Timers = [new TimerEntry { Duration = 30, Unit = "min", Label = "rest" }],
                },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        Assert.Contains("~rest{30%min}", output, StringComparison.Ordinal);
    }

    /// <summary>Doneness cue and temperature emit as lines starting with "--".</summary>
    [Fact]
    public void DonenessTempAsComments()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps =
            [
                new ContentStep
                {
                    Text = "Bake the loaf.",
                    Temperature = new StepTemperature { Value = 375, Unit = TemperatureUnit.F },
                    DonenessCue = "golden brown",
                },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        // Temperature as -- comment
        Assert.Contains("-- 375°F", output, StringComparison.Ordinal);
        // Doneness cue as -- comment
        Assert.Contains("-- golden brown", output, StringComparison.Ordinal);
    }

    /// <summary>Celsius temperature renders with °C symbol.</summary>
    [Fact]
    public void Temperature_Celsius()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps =
            [
                new ContentStep
                {
                    Text = "Heat the oven.",
                    Temperature = new StepTemperature { Value = 180, Unit = TemperatureUnit.C },
                },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        Assert.Contains("-- 180°C", output, StringComparison.Ordinal);
    }

    /// <summary>Gas mark temperature renders with "Gas Mark" text.</summary>
    [Fact]
    public void Temperature_GasMark()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps =
            [
                new ContentStep
                {
                    Text = "Heat the oven.",
                    Temperature = new StepTemperature { Value = 4, Unit = TemperatureUnit.Gas },
                },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        Assert.Contains("-- 4Gas Mark", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// INTEROP-04 / D7 / T-13-03: Literal @, #, ~ in step prose must be stripped.
    /// The only @/#/~ in output come from structured ingredient/timer tokens.
    /// </summary>
    [Fact]
    public void ProseSanitized()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps =
            [
                new ContentStep { Text = "Cook @ 350 #1 ~5 min" },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);

        // The step prose line should not contain literal @, #, or ~ from the prose.
        // The step line is the non-empty, non->> and non--- line in the steps block.
        // All @ # ~ in output must be absent from the prose line.
        var lines = output.Split('\n');
        var proseLine = Array.Find(lines, l => l.Contains("350") || l.Contains("5 min"));
        Assert.NotNull(proseLine);
        // After sanitization, the prose should contain none of those special chars
        Assert.DoesNotContain("@", proseLine, StringComparison.Ordinal);
        Assert.DoesNotContain("#", proseLine, StringComparison.Ordinal);
        Assert.DoesNotContain("~", proseLine, StringComparison.Ordinal);
    }

    /// <summary>
    /// Recipe-level Equipment[] appears on a ">>" or "--" line, NOT as inline "#whisk".
    /// (D8: inline # is reserved for step-scoped cookware; recipe-level equipment uses metadata.)
    /// </summary>
    [Fact]
    public void EquipmentNotInlineCookware()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Equipment = ["whisk", "stand mixer"],
        };
        var output = CooklangRecipeProjector.Project(doc);

        // Must appear on a >> or -- line
        Assert.Contains("whisk", output, StringComparison.Ordinal);
        var hasMetadataLine = output.Contains("-- Equipment: whisk", StringComparison.Ordinal)
                           || output.Contains(">> equipment:", StringComparison.OrdinalIgnoreCase);
        Assert.True(hasMetadataLine, "equipment should be on a -- Equipment: or >> equipment: line");

        // Must NOT appear as inline #whisk
        Assert.DoesNotContain("#whisk", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Substitutions from IngredientEntry.Substitutions render as a trailing -- block.
    /// The exported .cook ends with `-- Substitution (butter): use margarine`.
    /// (Substitutions live ONLY on IngredientEntry — there is no per-step substitution model.)
    /// </summary>
    [Fact]
    public void SubstitutionPlacement()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Ingredients =
            [
                new IngredientEntry
                {
                    Id = 1,
                    Name = "butter",
                    Amount = 1.0,
                    Unit = "cup",
                    Substitutions =
                    [
                        new IngredientSubstitution { Note = "use margarine" },
                    ],
                },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        var trimmedEnd = output.TrimEnd();

        // The trailing block must be the last non-empty content.
        Assert.EndsWith("-- Substitution (butter): use margarine", trimmedEnd, StringComparison.Ordinal);
    }

    /// <summary>
    /// The full golden snapshot ends with the trailing Substitution comment block.
    /// </summary>
    [Fact]
    public void FullDocument_EndsWithSubstitutionBlock()
    {
        var doc = MakeFullDocument();
        var output = CooklangRecipeProjector.Project(doc);
        var trimmedEnd = output.TrimEnd();
        Assert.EndsWith("-- Substitution (butter): dairy-free option", trimmedEnd, StringComparison.Ordinal);
    }

    // ── WR-01 / WR-02: Grammar-complete sanitization tests (INTEROP-04) ──────────

    /// <summary>
    /// WR-01: A newline in step prose must be collapsed to a space — the prose must stay
    /// a single logical line in the .cook output (no injected comment/heading lines).
    /// </summary>
    [Fact]
    public void Prose_NewlineCollapsed()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps =
            [
                new ContentStep { Text = "Whisk eggs.\n-- secret: add salt" },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        // The injected "-- secret: add salt" must NOT appear as a separate Cooklang comment line.
        Assert.DoesNotContain("\n-- secret", output, StringComparison.Ordinal);
        // After newline collapse + structural-marker neutralization the prose is on one line.
        var lines = output.Split('\n');
        var proseLine = Array.Find(lines, l => l.Contains("Whisk eggs"));
        Assert.NotNull(proseLine);
        Assert.DoesNotContain("-- secret", proseLine, StringComparison.Ordinal);
    }

    /// <summary>
    /// WR-01: Prose containing "--" is neutralized (double-dash collapsed to single dash).
    /// </summary>
    [Fact]
    public void Prose_CommentMarkerNeutralized()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps =
            [
                new ContentStep { Text = "-- This would be a comment" },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        var lines = output.Split('\n');
        // The step prose line must not start with "--"
        var proseLine = Array.Find(lines, l => l.Contains("This would be a comment"));
        Assert.NotNull(proseLine);
        Assert.False(proseLine.TrimStart().StartsWith("--", StringComparison.Ordinal),
            "Prose must not emit as a Cooklang comment line");
    }

    /// <summary>
    /// WR-01: Prose containing ">>" (metadata marker) is neutralized.
    /// </summary>
    [Fact]
    public void Prose_MetadataMarkerNeutralized()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps =
            [
                new ContentStep { Text = ">> injected: metadata" },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        var lines = output.Split('\n');
        var proseLine = Array.Find(lines, l => l.Contains("injected"));
        Assert.NotNull(proseLine);
        Assert.False(proseLine.TrimStart().StartsWith(">>", StringComparison.Ordinal),
            "Prose must not emit as a Cooklang metadata line");
    }

    /// <summary>
    /// WR-01: Prose containing "==" (section heading marker) is neutralized.
    /// </summary>
    [Fact]
    public void Prose_SectionMarkerNeutralized()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps =
            [
                new ContentStep { Text = "== Dessert ==" },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        // Must not emit a valid section heading line
        Assert.DoesNotContain("== Dessert ==", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// WR-02: A "}" in an ingredient name must not close the braces token early.
    /// T-13-04: always-braces form is only safe if content is also sanitized.
    /// </summary>
    [Fact]
    public void IngredientName_CloseBraceStripped()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Ingredients =
            [
                new IngredientEntry { Id = 1, Name = "foo}bar", Amount = 1, Unit = "cup" },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        // The "}" in the name must be stripped; the token must be valid.
        Assert.Contains("@foobar{1%cup}", output, StringComparison.Ordinal);
        Assert.DoesNotContain("foo}bar", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// WR-02: A "%" in an ingredient name (e.g. "cream (8%) cheese") must not inject
    /// a spurious amount-unit separator inside the token.
    /// </summary>
    [Fact]
    public void IngredientName_PercentStripped()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Ingredients =
            [
                new IngredientEntry { Id = 1, Name = "cream (8%) cheese", Amount = 1, Unit = "cup" },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        // "%" in name must be stripped; the only % in the token is the amount/unit separator.
        Assert.Contains("@cream (8) cheese{1%cup}", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// WR-02: A newline in an ingredient name must be collapsed so it cannot inject
    /// a new output line.
    /// </summary>
    [Fact]
    public void IngredientName_NewlineCollapsed()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Ingredients =
            [
                new IngredientEntry { Id = 1, Name = "butter\ncream", Amount = 1, Unit = "cup" },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        // The ingredient line must be a single line, not split by a newline.
        Assert.Contains("@butter cream{1%cup}", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// WR-02: A "}" in a timer label must not close the braces token early.
    /// </summary>
    [Fact]
    public void TimerLabel_CloseBraceStripped()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps =
            [
                new ContentStep
                {
                    Text = "Rest.",
                    Timers = [new TimerEntry { Duration = 30, Unit = "min", Label = "rest}now" }],
                },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        // "}" in label must be stripped so token is not closed early.
        Assert.Contains("~restnow{30%min}", output, StringComparison.Ordinal);
        Assert.DoesNotContain("rest}now", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// WR-02: A "%" in a timer unit must not add a spurious amount separator.
    /// </summary>
    [Fact]
    public void TimerUnit_PercentStripped()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps =
            [
                new ContentStep
                {
                    Text = "Reduce.",
                    Timers = [new TimerEntry { Duration = 5, Unit = "min%s" }],
                },
            ],
        };
        var output = CooklangRecipeProjector.Project(doc);
        Assert.Contains("~{5%mins}", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// WR-02: A section heading containing "==" must not inject a nested heading structure.
    /// </summary>
    [Fact]
    public void SectionHeading_DoubleEqualStripped()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps = [new SectionStep { Heading = "Prep == now" }],
        };
        var output = CooklangRecipeProjector.Project(doc);
        // "==" in heading text is reduced to "=" by SanitizeToken (== → =).
        // Result: "== Prep = now ==" — the outer == are the projector's heading delimiters.
        Assert.Contains("== Prep = now ==", output, StringComparison.Ordinal);
        // Must not contain the raw "==" inside the heading text.
        Assert.DoesNotContain("Prep == now", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// WR-02: A newline in a section heading must not inject an extra output line.
    /// </summary>
    [Fact]
    public void SectionHeading_NewlineCollapsed()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Steps = [new SectionStep { Heading = "Prep\nExtra" }],
        };
        var output = CooklangRecipeProjector.Project(doc);
        // Newline must be collapsed; the heading must appear on one line.
        Assert.Contains("== Prep Extra ==", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// WR-02: A newline in equipment text must not inject an extra output line.
    /// </summary>
    [Fact]
    public void Equipment_NewlineCollapsed()
    {
        var doc = new RecipeDocument
        {
            Version = 4,
            Name = "Test",
            Equipment = ["stand\nmixer"],
        };
        var output = CooklangRecipeProjector.Project(doc);
        Assert.Contains("-- Equipment: stand mixer", output, StringComparison.Ordinal);
    }

    // ── Shared fixture ────────────────────────────────────────────────────────────

    private static RecipeDocument MakeFullDocument() => new()
    {
        Version = 4,
        Name = "Golden Pound Cake",
        Description = "A classic pound cake with a tender crumb.",
        Servings = 8,
        PrepTimeMinutes = 20,
        CookTimeMinutes = 60,
        Equipment = ["stand mixer", "bundt pan"],
        Provenance = new RecipeProvenance
        {
            SourceName = "Family Cookbook",
            AuthorName = "Grandma Rose",
            SourceUrl = "https://example.com/recipes/pound-cake",
        },
        Tags = ["Dessert", "baking"],
        Ingredients =
        [
            new IngredientEntry { Id = 1, Name = "all-purpose flour", Amount = 2, Unit = "cups" },
            new IngredientEntry { Id = 2, Name = "butter", Amount = 1, Unit = "cup",
                Substitutions = [new IngredientSubstitution { Note = "dairy-free option" }] },
            new IngredientEntry { Id = 3, Name = "sugar", Amount = 1.5, Unit = "cups" },
            new IngredientEntry { Id = 4, Name = "eggs", Amount = 4, Unit = "" },
            new IngredientEntry { Id = 5, Name = "vanilla extract", Amount = 0.5, Unit = "tsp" },
        ],
        Steps =
        [
            new SectionStep { Heading = "Cream Butter" },
            new ContentStep
            {
                Text = "Beat [butter](#2) and [sugar](#3) together until light and fluffy.",
                Temperature = new StepTemperature { Value = 72, Unit = TemperatureUnit.F },
                DonenessCue = "pale and fluffy",
            },
            new SectionStep { Heading = "Combine" },
            new ContentStep
            {
                Text = "Add [eggs](#4) one at a time, then [vanilla extract](#5).",
            },
            new ContentStep
            {
                Text = "Fold in [all-purpose flour](#1) gradually.",
                Timers = [new TimerEntry { Duration = 2, Unit = "min", Label = "mix" }],
            },
            new SectionStep { Heading = "Bake" },
            new ContentStep
            {
                Text = "Pour into prepared bundt pan and bake.",
                Temperature = new StepTemperature { Value = 350, Unit = TemperatureUnit.F },
                Timers = [new TimerEntry { Duration = 60, Unit = "min" }],
                DonenessCue = "toothpick comes out clean",
            },
        ],
    };
}
