using System;
using System.Text.Json.Nodes;
using CookBot.Application.Recipes;

namespace CookBot.Tests.Recipes;

/// <summary>
/// FORMAT-09 / D-05 / Pitfall H2/H4 — verify <c>[JsonExtensionData] Extras</c> on each of
/// the four annotated record types (RecipeDocument root, ContentStep, SectionStep,
/// IngredientEntry) round-trips unknown JSON keys through serialize -> deserialize. Also
/// verifies the upcaster chain rejects a version newer than current (Pitfall H1).
/// </summary>
public class ExtrasRoundTripTests
{
    [Fact]
    public void UnknownTopLevelField_RoundTripsThroughExtras()
    {
        var input = """
            {"version":2,"name":"X","servings":1,"prepTimeMinutes":5,"cookTimeMinutes":10,"tags":[],"ingredients":[],"steps":[],"futureField":"hello"}
            """;
        var serializer = new JsonRecipeSerializer();
        var doc = serializer.Deserialize(input);
        var roundTripped = serializer.Serialize(doc);
        Assert.Contains("\"futureField\"", roundTripped);
        Assert.Contains("\"hello\"", roundTripped);
    }

    [Fact]
    public void UnknownContentStepField_RoundTripsThroughExtras()
    {
        var input = """
            {"version":2,"name":"X","servings":1,"ingredients":[],"steps":[{"kind":"content","text":"step","newPropX":"y"}]}
            """;
        var serializer = new JsonRecipeSerializer();
        var doc = serializer.Deserialize(input);
        var roundTripped = serializer.Serialize(doc);
        Assert.Contains("\"newPropX\"", roundTripped);
        Assert.Contains("\"y\"", roundTripped);
    }

    [Fact]
    public void UnknownSectionStepField_RoundTripsThroughExtras()
    {
        // D-05: [JsonExtensionData] Extras lives on SectionStep too.
        var input = """
            {"version":2,"name":"X","servings":1,"ingredients":[],"steps":[{"kind":"section","heading":"Wet","newPropY":"z"}]}
            """;
        var serializer = new JsonRecipeSerializer();
        var doc = serializer.Deserialize(input);
        var roundTripped = serializer.Serialize(doc);
        Assert.Contains("\"newPropY\"", roundTripped);
        Assert.Contains("\"z\"", roundTripped);
    }

    [Fact]
    public void UnknownIngredientEntryField_RoundTripsThroughExtras()
    {
        // D-05: [JsonExtensionData] Extras lives on IngredientEntry too.
        var input = """
            {"version":2,"name":"X","servings":1,"ingredients":[{"id":1,"name":"Salt","newPropZ":"w"}],"steps":[]}
            """;
        var serializer = new JsonRecipeSerializer();
        var doc = serializer.Deserialize(input);
        var roundTripped = serializer.Serialize(doc);
        Assert.Contains("\"newPropZ\"", roundTripped);
        Assert.Contains("\"w\"", roundTripped);
    }

    [Fact]
    public void VersionGreaterThanCurrent_ThrowsNewerThanCurrent()
    {
        var node = JsonNode.Parse("""{"version":999,"name":"X","servings":1,"ingredients":[],"steps":[]}""")!;
        var chain = new RecipeUpcasterChain(new IRecipeUpcaster[] { new Migration_V1_To_V2() });
        var ex = Assert.Throws<InvalidOperationException>(() => chain.UpcastToCurrent(node));
        Assert.Contains("newer than current", ex.Message);
    }
}
