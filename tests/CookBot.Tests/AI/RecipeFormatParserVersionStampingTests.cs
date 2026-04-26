using CookBot.Application.Recipes;
using CookBot.Application.Services;

namespace CookBot.Tests.AI;

/// <summary>
/// MIGRATION-06 verification — YAML paste-in routes through the upcaster chain.
/// Phase 1 D-10 + H1 mitigation already wired this in <see cref="RecipeFormatParser.TryParse"/>;
/// this test locks the behavior so a regression that drops the version-stamping step
/// (RecipeFormatParser.cs:103-106) gets caught.
/// Plan 02-04 Task 2.
/// </summary>
public class RecipeFormatParserVersionStampingTests
{
    private static RecipeFormatParser BuildParser()
    {
        var chain = new RecipeUpcasterChain(new IRecipeUpcaster[] { new Migration_V1_To_V2() });
        return new RecipeFormatParser(chain, new JsonRecipeSerializer(), new RecipeValidator());
    }

    [Fact]
    public void TryParse_YamlWithoutVersion_IsStampedToV1AndUpcastedToCurrent()
    {
        // Legacy YAML — pre-v1.1 paste-in shape. No `version:` field at the top.
        // The legacy `prepTime` key is unknown to the canonical RecipeDocument; the only
        // way it survives as `PrepTimeMinutes` is if the parser stamped version=1
        // BEFORE the upcaster ran (so Migration_V1_To_V2's RenameKey step fired).
        const string legacyYaml = """
            ---
            name: Legacy Recipe
            servings: 4
            prepTime: 10
            cookTime: 20
            ingredients:
              - id: 1
                name: flour
                amount: 2
                unit: cup
            steps:
              - text: "Mix [flour](#1)."
            ---
            """;

        var parser = BuildParser();
        var ok = parser.TryParse(legacyYaml, out var parsed, out var errors);

        Assert.True(ok, $"Expected parse OK; errors: {string.Join("; ", errors)}");
        Assert.NotNull(parsed);
        // Indirect: if version-stamping did NOT happen, the upcaster would see no version,
        // the V1->V2 reconciliation would not run, and the parsed result would have null
        // PrepTimeMinutes (since the legacy `prepTime` key is unknown to the canonical
        // record without the upcaster's RenameKey step). This indirect-but-strong assertion
        // proves both the version-stamping AND the upcaster ran.
        Assert.NotNull(parsed!.PrepTimeMinutes);
        Assert.Equal(10, parsed.PrepTimeMinutes);
        Assert.NotNull(parsed.CookTimeMinutes);
        Assert.Equal(20, parsed.CookTimeMinutes);
    }

    [Fact]
    public void TryParse_YamlAlreadyV2_RoundTripsCleanly()
    {
        const string v2Yaml = """
            ---
            version: 2
            name: V2 Recipe
            servings: 2
            prepTimeMinutes: 5
            ingredients:
              - id: 1
                name: salt
                amount: 1
                unit: tsp
            steps:
              - kind: content
                text: "Add [salt](#1)."
            ---
            """;

        var parser = BuildParser();
        var ok = parser.TryParse(v2Yaml, out var parsed, out _);

        Assert.True(ok);
        Assert.NotNull(parsed);
        Assert.Equal(5, parsed!.PrepTimeMinutes);
    }
}
