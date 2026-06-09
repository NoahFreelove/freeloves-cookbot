using CookBot.Application.Services;
using Xunit;

namespace CookBot.Tests.Nutrition;

/// <summary>
/// Unit tests for IngredientDensityProvider — NUTR-03 fallback density table.
/// Verifies ≥23-entry table, ≥20-ingredient density assertions (within ±0.01),
/// the no-water-density guard (SC3/P5), null-on-unknown, and case-insensitive lookup.
/// </summary>
public class IngredientDensityProviderTests
{
    private readonly IngredientDensityProvider _provider = new();

    // --- FLOURS ---

    [Fact]
    public void AllPurposeFlour_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("all-purpose flour");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.497, 0.517); // 0.507 ± 0.01
    }

    [Fact]
    public void AllPurposeFlour_IsNotWaterDensity_SC3Guard()
    {
        // SC3 guard: flour density must NOT be 1.0 g/mL (water).
        // Without this guard "1 cup flour" = 237 g = 862 kcal instead of ~455 kcal.
        var density = _provider.GetDensityGPerMl("all-purpose flour");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.45, 0.55); // [0.45, 0.55] — never 1.0
        Assert.NotEqual(1.0, density.Value);
    }

    [Fact]
    public void BreadFlour_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("bread flour");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.497, 0.517);
    }

    [Fact]
    public void WholeWheatFlour_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("whole wheat flour");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.468, 0.488); // 0.478 ± 0.01
    }

    [Fact]
    public void CakeFlour_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("cake flour");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.497, 0.517);
    }

    [Fact]
    public void AlmondFlour_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("almond flour");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.396, 0.416); // 0.406 ± 0.01
    }

    // --- SUGARS ---

    [Fact]
    public void GranulatedSugar_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("granulated sugar");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.827, 0.847); // 0.837 ± 0.01
    }

    [Fact]
    public void BrownSugar_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("brown sugar");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.890, 0.910); // 0.900 ± 0.01
    }

    [Fact]
    public void ConfectionersSugar_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("confectioners sugar");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.468, 0.488); // 0.478 ± 0.01
    }

    // --- FATS / OILS ---

    [Fact]
    public void Butter_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("butter");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.945, 0.965); // 0.955 ± 0.01
    }

    [Fact]
    public void VegetableOil_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("vegetable oil");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.827, 0.847); // 0.837 ± 0.01
    }

    [Fact]
    public void OliveOil_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("olive oil");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.835, 0.855); // 0.845 ± 0.01
    }

    // --- DAIRY ---

    [Fact]
    public void WholeMilk_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("whole milk");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.949, 0.969); // 0.959 ± 0.01
    }

    [Fact]
    public void HeavyCream_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("heavy cream");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.949, 0.969);
    }

    [Fact]
    public void SourCream_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("sour cream");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.949, 0.969);
    }

    [Fact]
    public void Yogurt_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("yogurt");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.949, 0.969);
    }

    // --- SYRUPS / SWEETENERS ---

    [Fact]
    public void Honey_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("honey");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 1.410, 1.430); // 1.420 ± 0.01
    }

    [Fact]
    public void MapleSyrup_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("maple syrup");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 1.309, 1.329); // 1.319 ± 0.01
    }

    // --- BAKING STAPLES ---

    [Fact]
    public void CocoaPowder_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("cocoa powder");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.345, 0.365); // 0.355 ± 0.01
    }

    [Fact]
    public void Cornstarch_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("cornstarch");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.463, 0.483); // 0.473 ± 0.01
    }

    [Fact]
    public void RolledOats_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("rolled oats");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.468, 0.488); // 0.478 ± 0.01
    }

    [Fact]
    public void BakingPowder_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("baking powder");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.890, 0.910); // 0.900 ± 0.01
    }

    [Fact]
    public void Salt_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("salt");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 1.370, 1.390); // 1.380 ± 0.01
    }

    [Fact]
    public void ChocolateChips_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("chocolate chips");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.709, 0.729); // 0.719 ± 0.01
    }

    // --- ADDITIONAL ENTRIES (≥20-ingredient coverage) ---

    [Fact]
    public void CreamCheese_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("cream cheese");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.949, 0.969); // 0.959 ± 0.01
    }

    [Fact]
    public void RicottaCheese_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("ricotta cheese");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.950, 0.970); // 0.960 ± 0.01
    }

    [Fact]
    public void PeanutButter_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("peanut butter");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 1.080, 1.100); // 1.09 ± 0.01
    }

    [Fact]
    public void ShreddedCoconut_ReturnsCorrectDensity()
    {
        var density = _provider.GetDensityGPerMl("shredded coconut");
        Assert.NotNull(density);
        Assert.InRange(density!.Value, 0.350, 0.370); // 0.360 ± 0.01
    }

    // --- GUARD CASES ---

    [Fact]
    public void UnknownIngredient_ReturnsNull()
    {
        var density = _provider.GetDensityGPerMl("unobtainium");
        Assert.Null(density);
    }

    [Fact]
    public void Lookup_IsCaseInsensitive()
    {
        // Both mixed-case and lower-case should resolve identically.
        var lower = _provider.GetDensityGPerMl("all-purpose flour");
        var mixed = _provider.GetDensityGPerMl("All-Purpose Flour");
        Assert.NotNull(lower);
        Assert.NotNull(mixed);
        Assert.Equal(lower!.Value, mixed!.Value);
    }

    [Fact]
    public void Table_HasAtLeast23Entries()
    {
        Assert.True(IngredientDensityProvider.EntryCount >= 23,
            $"Expected ≥23 density entries but found {IngredientDensityProvider.EntryCount}.");
    }
}
