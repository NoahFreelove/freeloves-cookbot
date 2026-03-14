using CookBot.Application.Services;

namespace CookBot.Tests.Services;

public class RecipeScalingServiceTests
{
    [Fact]
    public void ScaleAmount_DoublesServings_DoublesAmount()
    {
        var scaled = RecipeScalingService.ScaleAmount(2.0, originalServings: 4, targetServings: 8);
        Assert.Equal(4.0, scaled);
    }

    [Fact]
    public void ScaleAmount_HalvesServings_HalvesAmount()
    {
        var scaled = RecipeScalingService.ScaleAmount(2.0, originalServings: 4, targetServings: 2);
        Assert.Equal(1.0, scaled);
    }

    [Fact]
    public void FormatScaledAmount_ReturnsReadableFraction()
    {
        var display = RecipeScalingService.FormatScaledAmount(2.0, originalServings: 4, targetServings: 6);
        Assert.Equal("3", display); // 2 * (6/4) = 3
    }

    [Fact]
    public void FormatScaledAmount_FractionalResult()
    {
        var display = RecipeScalingService.FormatScaledAmount(1.0, originalServings: 4, targetServings: 6);
        Assert.Equal("1 1/2", display); // 1 * (6/4) = 1.5
    }
}
