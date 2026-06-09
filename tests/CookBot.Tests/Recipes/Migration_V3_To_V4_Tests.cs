using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using CookBot.Application.Recipes;
using CookBot.Domain.Recipes;

namespace CookBot.Tests.Recipes;

/// <summary>
/// Fixture-matrix tests for the V3->V4 upcaster (Phase 12, D-12-12, PITFALLS C7/P2, FORMAT-05).
/// Drives <see cref="Migration_V3_To_V4"/> through per-field fixture combinations and asserts:
/// <list type="bullet">
///   <item>All fixtures produce version=4 without throwing (D-12-12 independence contract, SC1, P2)</item>
///   <item>A v3 recipe with no new fields upcasts with all four new groups null/empty after deserialization</item>
///   <item>A v4 doc passed through the chain is identity (no double-upcasting)</item>
///   <item>A chain missing the 3->4 upcaster throws at construction (SC4 gap-detection)</item>
/// </list>
/// </summary>
public class Migration_V3_To_V4_Tests
{
    /// <summary>
    /// Constructs the full V1→V4 chain used for integration-style fixture assertions.
    /// </summary>
    private static RecipeUpcasterChain MakeChain() =>
        new(new IRecipeUpcaster[] { new Migration_V1_To_V2(), new Migration_V2_To_V3(), new Migration_V3_To_V4() });

    /// <summary>
    /// MemberData source: all v3-to-v4-*.json fixture files from the upcaster fixture directory.
    /// </summary>
    public static IEnumerable<object[]> V3ToV4Fixtures()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recipes", "upcaster");
        foreach (var path in Directory.GetFiles(dir, "v3-to-v4-*.json"))
        {
            yield return new object[] { Path.GetFileName(path), File.ReadAllText(path) };
        }
    }

    /// <summary>
    /// Per-fixture matrix: every v3 fixture upcasts to version=4 without throwing (D-12-12, PITFALLS C7/P2, SC1).
    /// </summary>
    [Theory]
    [MemberData(nameof(V3ToV4Fixtures))]
    public void Upcast_V3Fixture_ProducesVersion4(string fixtureName, string json)
    {
        var chain = MakeChain();
        var node = JsonNode.Parse(json)!;

        // Must not throw — each guard is independent (PITFALLS C7 / P2)
        var result = chain.UpcastToCurrent(node);

        Assert.True(result["version"]!.GetValue<int>() == 4, $"{fixtureName}: expected version=4 after upcast");
    }

    /// <summary>
    /// P2 / SC1: a v3 recipe with none of the four new field groups upcasts to v4 and all
    /// four groups are absent/empty/null after deserialization. Never throws even with partial fields.
    /// </summary>
    [Fact]
    public void Upcast_NoNewFields_NewFieldsAreNull()
    {
        var json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recipes", "upcaster", "v3-to-v4-no-fields.json"));
        var node = JsonNode.Parse(json)!;
        var result = MakeChain().UpcastToCurrent(node);

        Assert.Equal(4, result["version"]!.GetValue<int>());

        // Deserialize to typed record to verify field defaults.
        var doc = JsonSerializer.Deserialize<RecipeDocument>(result.ToJsonString())!;

        // equipment absent => empty list (never null)
        Assert.Empty(doc.Equipment);

        // provenance absent => null
        Assert.Null(doc.Provenance);

        // every content step: donenessCue absent => null
        foreach (var step in doc.Steps.OfType<ContentStep>())
        {
            Assert.Null(step.DonenessCue);
        }

        // every ingredient: substitutions absent => empty list
        foreach (var ing in doc.Ingredients)
        {
            Assert.Empty(ing.Substitutions);
        }
    }

    /// <summary>
    /// A recipe already at version=4 passes through the chain unchanged (identity pass).
    /// No upcaster fires because no upcaster's FromVersion matches 4.
    /// </summary>
    [Fact]
    public void Upcast_VersionAlreadyFour_IsIdentity()
    {
        var node = JsonNode.Parse(
            """{"version":4,"name":"Modern Recipe","ingredients":[],"steps":[]}""")!;
        var result = MakeChain().UpcastToCurrent(node);
        Assert.Equal(4, result["version"]!.GetValue<int>());
        Assert.Equal("Modern Recipe", result["name"]!.GetValue<string>());
    }

    /// <summary>
    /// SC4 / D-12-13: gap detection — a chain covering 1->2, 2->3 and 4->5 (skipping 3->4)
    /// must throw <see cref="InvalidOperationException"/> at construction.
    /// </summary>
    [Fact]
    public void ChainConstructor_ThrowsOnGap()
    {
        // Migration_V1_To_V2 covers 1->2; Migration_V2_To_V3 covers 2->3;
        // FakeUpcaster(4,5) covers 4->5 leaving a 3->4 gap.
        var fake4to5 = new FakeUpcaster(4, 5);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new RecipeUpcasterChain(new IRecipeUpcaster[]
            {
                new Migration_V1_To_V2(),
                new Migration_V2_To_V3(),
                fake4to5
            }));
        Assert.Contains("gap", ex.Message);
    }

    private sealed class FakeUpcaster : IRecipeUpcaster
    {
        public int FromVersion { get; }
        public int ToVersion { get; }
        public FakeUpcaster(int from, int to) { FromVersion = from; ToVersion = to; }
        public JsonNode Upcast(JsonNode input) => input;
    }
}
