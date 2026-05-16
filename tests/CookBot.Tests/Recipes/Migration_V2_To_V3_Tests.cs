using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using CookBot.Application.Recipes;

namespace CookBot.Tests.Recipes;

/// <summary>
/// Fixture-matrix tests for the V2->V3 upcaster (Phase 8, D-29, PITFALLS C7/M2, SCHEMA-04/SCHEMA-05).
/// Drives <see cref="Migration_V2_To_V3"/> through per-field fixture combinations and asserts:
/// <list type="bullet">
///   <item>All fixtures produce version=3 without throwing (D-29 independence contract)</item>
///   <item>Content steps with no temperature stay absent after upcasting (PITFALLS M2 — never zero-fill)</item>
///   <item>A v3 doc passed through the chain is identity (no double-upcasting)</item>
///   <item>A chain with a deliberate gap throws at construction (existing gap-detection regression guard)</item>
/// </list>
/// </summary>
public class Migration_V2_To_V3_Tests
{
    /// <summary>
    /// Constructs the full V1→V3 chain used for integration-style fixture assertions.
    /// </summary>
    private static RecipeUpcasterChain MakeChain() =>
        new(new IRecipeUpcaster[] { new Migration_V1_To_V2(), new Migration_V2_To_V3() });

    /// <summary>
    /// MemberData source: all v2-to-v3-*.json fixture files from the upcaster fixture directory.
    /// Pattern S1 (filesystem-driven fixture loading — see RecipeDocumentRoundTripTests).
    /// </summary>
    public static IEnumerable<object[]> V2ToV3Fixtures()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recipes", "upcaster");
        foreach (var path in Directory.GetFiles(dir, "v2-to-v3-*.json"))
        {
            yield return new object[] { Path.GetFileName(path), File.ReadAllText(path) };
        }
    }

    /// <summary>
    /// Per-fixture matrix: every v2 fixture upcasts to version=3 without throwing (D-29, PITFALLS C7).
    /// </summary>
    [Theory]
    [MemberData(nameof(V2ToV3Fixtures))]
    public void Upcast_V2Fixture_ProducesVersion3(string fixtureName, string json)
    {
        var chain = MakeChain();
        var node = JsonNode.Parse(json)!;

        // Must not throw — each guard is independent (PITFALLS C7)
        var result = chain.UpcastToCurrent(node);

        Assert.True(result["version"]!.GetValue<int>() == 3, $"{fixtureName}: expected version=3 after upcast");
    }

    /// <summary>
    /// PITFALLS C7 + M2: a v2 recipe with no temperature data upcasts to v3 and the
    /// content step's "temperature" key stays absent in the resulting JSON node.
    /// Temperature is NEVER zero-filled. (PITFALLS M2)
    /// </summary>
    [Fact]
    public void Upcast_NoTemperature_ContentStepTemperatureIsNull()
    {
        var json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recipes", "upcaster", "v2-to-v3-no-fields.json"));
        var node = JsonNode.Parse(json)!;
        var result = MakeChain().UpcastToCurrent(node);

        Assert.Equal(3, result["version"]!.GetValue<int>());

        // Walk steps and assert no "temperature" key was injected on content steps.
        // PITFALLS M2: absent temperature stays absent — never zero-filled.
        var steps = result["steps"]?.AsArray();
        Assert.NotNull(steps);
        foreach (var step in steps!.OfType<JsonObject>())
        {
            if (step["kind"]?.GetValue<string>() == "content")
            {
                // temperature key should be absent (null) — not set to a default value
                Assert.Null(step["temperature"]);
            }
        }
    }

    /// <summary>
    /// A recipe already at version=3 passes through the chain unchanged (identity pass).
    /// No upcaster fires because no upcaster's FromVersion matches 3.
    /// </summary>
    [Fact]
    public void Upcast_VersionAlreadyThree_IsIdentity()
    {
        var node = JsonNode.Parse(
            """{"version":3,"name":"Modern Recipe","ingredients":[],"steps":[]}""")!;
        var result = MakeChain().UpcastToCurrent(node);
        Assert.Equal(3, result["version"]!.GetValue<int>());
        Assert.Equal("Modern Recipe", result["name"]!.GetValue<string>());
    }

    /// <summary>
    /// Chain gap detection regression guard. A chain covering 1->2 and 3->4 (skipping 2->3)
    /// must throw <see cref="InvalidOperationException"/> at construction.
    /// Verifies the existing gap-detection in <see cref="RecipeUpcasterChain"/>'s constructor.
    /// </summary>
    [Fact]
    public void ChainConstructor_ThrowsOnGap()
    {
        // Migration_V1_To_V2 covers 1->2; FakeUpcaster(3,4) covers 3->4 leaving 2->3 gap.
        var fake3to4 = new FakeUpcaster(3, 4);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new RecipeUpcasterChain(new IRecipeUpcaster[] { new Migration_V1_To_V2(), fake3to4 }));
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
