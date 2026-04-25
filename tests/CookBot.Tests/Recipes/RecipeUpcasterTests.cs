using System;
using System.Text.Json.Nodes;
using CookBot.Application.Recipes;

namespace CookBot.Tests.Recipes;

/// <summary>
/// Pitfall H1 / D-09 — verifies the JSON-node-layer upcaster chain dispatches versions
/// correctly (absent stamps to v1, v1 -> v2 by Migration_V1_To_V2, v2 identity, v999
/// rejected) and exercises the four documented v1 quirks Migration_V1_To_V2 reconciles.
/// Also asserts the chain validates no version gaps at construction.
/// </summary>
public class RecipeUpcasterTests
{
    private static RecipeUpcasterChain MakeChain() =>
        new(new IRecipeUpcaster[] { new Migration_V1_To_V2() });

    [Fact]
    public void UpcastToCurrent_VersionAbsent_StampsV1AndUpcastsToV2()
    {
        var node = JsonNode.Parse("""{"name":"X","ingredients":[],"steps":[]}""")!;
        var result = MakeChain().UpcastToCurrent(node);
        Assert.Equal(2, result["version"]!.GetValue<int>());
    }

    [Fact]
    public void UpcastToCurrent_VersionExplicit1_UpcastsToV2()
    {
        var node = JsonNode.Parse("""{"version":1,"name":"X","ingredients":[],"steps":[]}""")!;
        var result = MakeChain().UpcastToCurrent(node);
        Assert.Equal(2, result["version"]!.GetValue<int>());
    }

    [Fact]
    public void UpcastToCurrent_VersionAlready2_IsIdentity()
    {
        var node = JsonNode.Parse("""{"version":2,"name":"X","ingredients":[],"steps":[]}""")!;
        var result = MakeChain().UpcastToCurrent(node);
        Assert.Equal(2, result["version"]!.GetValue<int>());
        Assert.Equal("X", result["name"]!.GetValue<string>());
    }

    [Fact]
    public void UpcastToCurrent_VersionGreaterThanCurrent_Throws()
    {
        var node = JsonNode.Parse("""{"version":999,"name":"X","ingredients":[],"steps":[]}""")!;
        var ex = Assert.Throws<InvalidOperationException>(() => MakeChain().UpcastToCurrent(node));
        Assert.Contains("newer than current", ex.Message);
    }

    [Fact]
    public void Migration_V1_To_V2_RenamesPrepTimeAndCookTime()
    {
        var node = JsonNode.Parse(
            """{"version":1,"prepTime":5,"cookTime":12,"name":"X","ingredients":[],"steps":[]}""")!;
        var result = new Migration_V1_To_V2().Upcast(node).AsObject();
        Assert.False(result.ContainsKey("prepTime"));
        Assert.False(result.ContainsKey("cookTime"));
        Assert.Equal(5, result["prepTimeMinutes"]!.GetValue<int>());
        Assert.Equal(12, result["cookTimeMinutes"]!.GetValue<int>());
    }

    [Fact]
    public void Migration_V1_To_V2_RebuildsSectionStepFromIsSectionTrue()
    {
        var node = JsonNode.Parse(
            """{"version":1,"name":"X","ingredients":[],"steps":[{"isSection":true,"text":"Wet"}]}""")!;
        var result = new Migration_V1_To_V2().Upcast(node).AsObject();
        var step = result["steps"]!.AsArray()[0]!.AsObject();
        Assert.Equal("section", step["kind"]!.GetValue<string>());
        Assert.Equal("Wet", step["heading"]!.GetValue<string>());
        Assert.False(step.ContainsKey("isSection"));
    }

    [Fact]
    public void Migration_V1_To_V2_RebuildsContentStepKeepsTimers()
    {
        var node = JsonNode.Parse(
            """{"version":1,"name":"X","ingredients":[],"steps":[{"isSection":false,"text":"Mix","timers":[{"duration":3,"unit":"min"}]}]}""")!;
        var result = new Migration_V1_To_V2().Upcast(node).AsObject();
        var step = result["steps"]!.AsArray()[0]!.AsObject();
        Assert.Equal("content", step["kind"]!.GetValue<string>());
        Assert.Equal("Mix", step["text"]!.GetValue<string>());
        Assert.False(step.ContainsKey("isSection"));
        Assert.NotNull(step["timers"]);
    }

    [Fact]
    public void Migration_V1_To_V2_RenamesLocalIdToId()
    {
        var node = JsonNode.Parse(
            """{"version":1,"name":"X","ingredients":[{"localId":1,"name":"Salt"}],"steps":[]}""")!;
        var result = new Migration_V1_To_V2().Upcast(node).AsObject();
        var ing = result["ingredients"]!.AsArray()[0]!.AsObject();
        Assert.False(ing.ContainsKey("localId"));
        Assert.Equal(1, ing["id"]!.GetValue<int>());
    }

    [Fact]
    public void RecipeUpcasterChain_GapInVersions_ThrowsAtConstruction()
    {
        // Migration_V1_To_V2 covers 1->2; an extra fake covering 3->4 leaves a 2->3 gap.
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
