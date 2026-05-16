using System.Text.Json.Nodes;
using CookBot.Application.Recipes;

namespace CookBot.Tests.Recipes;

/// <summary>
/// RED-gate tests for the V2->V3 upcaster wiring (D-29, D-30, SCHEMA-04, SCHEMA-05).
/// Asserts that Migration_V2_To_V3 exists, has correct version range, and that
/// RecipeUpcasterChain.CurrentVersion == 3.
/// </summary>
public class Migration_V2_To_V3_ChainTests
{
    [Fact]
    public void Migration_V2_To_V3_HasCorrectVersionRange()
    {
        var upcaster = new Migration_V2_To_V3();
        Assert.Equal(2, upcaster.FromVersion);
        Assert.Equal(3, upcaster.ToVersion);
    }

    [Fact]
    public void RecipeUpcasterChain_CurrentVersion_IsThree()
    {
        Assert.Equal(3, RecipeUpcasterChain.CurrentVersion);
    }

    [Fact]
    public void Migration_V2_To_V3_UpcastsVersionFieldToThree()
    {
        var node = JsonNode.Parse("""{"version":2,"name":"Test","ingredients":[],"steps":[]}""")!;
        var upcaster = new Migration_V2_To_V3();
        var result = upcaster.Upcast(node);
        Assert.Equal(3, result["version"]!.GetValue<int>());
    }

    [Fact]
    public void Chain_WithBothUpcasters_UpcastsV1ToV3()
    {
        var chain = new RecipeUpcasterChain(new IRecipeUpcaster[]
        {
            new Migration_V1_To_V2(),
            new Migration_V2_To_V3(),
        });
        var node = JsonNode.Parse("""{"version":1,"name":"Test","ingredients":[],"steps":[]}""")!;
        var result = chain.UpcastToCurrent(node);
        Assert.Equal(3, result["version"]!.GetValue<int>());
    }
}
