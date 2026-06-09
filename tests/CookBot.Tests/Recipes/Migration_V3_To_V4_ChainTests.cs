using System.Text.Json.Nodes;
using CookBot.Application.Recipes;

namespace CookBot.Tests.Recipes;

/// <summary>
/// RED-gate tests for the V3->V4 upcaster wiring (Phase 12, D-12-12, D-12-13, FORMAT-05).
/// Asserts that Migration_V3_To_V4 exists, has correct version range, and that
/// RecipeUpcasterChain.CurrentVersion == 4 (SC4).
/// </summary>
public class Migration_V3_To_V4_ChainTests
{
    [Fact]
    public void Migration_V3_To_V4_HasCorrectVersionRange()
    {
        var upcaster = new Migration_V3_To_V4();
        Assert.Equal(3, upcaster.FromVersion);
        Assert.Equal(4, upcaster.ToVersion);
    }

    [Fact]
    public void RecipeUpcasterChain_CurrentVersion_IsFour()
    {
        Assert.Equal(4, RecipeUpcasterChain.CurrentVersion);
    }

    [Fact]
    public void Migration_V3_To_V4_UpcastsVersionFieldToFour()
    {
        var node = JsonNode.Parse("""{"version":3,"name":"Test","ingredients":[],"steps":[]}""")!;
        var upcaster = new Migration_V3_To_V4();
        var result = upcaster.Upcast(node);
        Assert.Equal(4, result["version"]!.GetValue<int>());
    }

    [Fact]
    public void Chain_WithAllThreeUpcasters_UpcastsV1ToV4()
    {
        var chain = new RecipeUpcasterChain(new IRecipeUpcaster[]
        {
            new Migration_V1_To_V2(),
            new Migration_V2_To_V3(),
            new Migration_V3_To_V4(),
        });
        var node = JsonNode.Parse("""{"version":1,"name":"Test","ingredients":[],"steps":[]}""")!;
        var result = chain.UpcastToCurrent(node);
        Assert.Equal(4, result["version"]!.GetValue<int>());
    }
}
