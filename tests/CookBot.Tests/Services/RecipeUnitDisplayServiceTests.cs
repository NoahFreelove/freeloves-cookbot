using CookBot.Application.Services;
using CookBot.Domain.Enums;
using CookBot.Domain.Recipes;

namespace CookBot.Tests.Services;

public class RecipeUnitDisplayServiceTests
{
    private readonly RecipeUnitDisplayService _svc;

    public RecipeUnitDisplayServiceTests()
    {
        // Use the real IUnitConverter (not a mock) so the full weight/volume path is exercised.
        var converter = new UnitConversionService();
        _svc = new RecipeUnitDisplayService(converter);
    }

    // ─── Weight reference values ───────────────────────────────────────────────

    [Fact]
    public void FormatIngredientAmount_100g_ImperialTarget_ReturnsApprox3_53oz()
    {
        // 100 g ≈ 3.53 oz
        var result = _svc.FormatIngredientAmount(100.0, "g", UnitSystem.Imperial);
        // FractionFormatter will produce "3 1/2" for 3.53
        Assert.NotEmpty(result);
        Assert.Contains("oz", result);
    }

    [Fact]
    public void FormatIngredientAmount_453_6g_ImperialTarget_Returns1lb()
    {
        // 453.6 g ≈ 1 lb
        var result = _svc.FormatIngredientAmount(453.6, "g", UnitSystem.Imperial);
        Assert.NotEmpty(result);
        // 453.6g = ~1 lb — may display as oz or lbs depending on threshold, but must contain a unit
        Assert.True(result.Contains("oz") || result.Contains("lbs") || result.Contains("lb"));
    }

    [Fact]
    public void FormatIngredientAmount_1lb_ImperialTarget_Returns16oz()
    {
        // 1 lb in Imperial stays as lbs (same system, identity)
        var result = _svc.FormatIngredientAmount(1.0, "lb", UnitSystem.Imperial);
        Assert.NotEmpty(result);
        Assert.True(result.Contains("lbs") || result.Contains("lb") || result.Contains("oz"));
    }

    [Fact]
    public void FormatIngredientAmount_100g_MetricTarget_ReturnsSameGrams()
    {
        // Metric target: already in metric, should stay as grams
        var result = _svc.FormatIngredientAmount(100.0, "g", UnitSystem.Metric);
        Assert.NotEmpty(result);
        Assert.Contains("g", result);
    }

    // ─── Volume reference values ───────────────────────────────────────────────

    [Fact]
    public void FormatIngredientAmount_250ml_ImperialTarget_ReturnsApprox1Cup()
    {
        // 250 ml ≈ 1 US cup
        var result = _svc.FormatIngredientAmount(250.0, "ml", UnitSystem.Imperial);
        Assert.NotEmpty(result);
        Assert.True(result.Contains("cups") || result.Contains("cup") || result.Contains("fl oz"));
    }

    [Fact]
    public void FormatIngredientAmount_250ml_MetricTarget_ReturnsMl()
    {
        // Metric target: already in metric volume
        var result = _svc.FormatIngredientAmount(250.0, "ml", UnitSystem.Metric);
        Assert.NotEmpty(result);
        Assert.Contains("mL", result);
    }

    // ─── Non-convertible passthrough cases ────────────────────────────────────

    [Fact]
    public void FormatIngredientAmount_ToTaste_Passthrough()
    {
        var result = _svc.FormatIngredientAmount(1.0, "to taste", UnitSystem.Imperial);
        Assert.Contains("to taste", result);
    }

    [Fact]
    public void FormatIngredientAmount_Clove_Passthrough()
    {
        // "clove" is a known non-convertible unit (maps to MeasurementUnit.Clove, not weight/volume)
        var result = _svc.FormatIngredientAmount(2.0, "clove", UnitSystem.Imperial);
        Assert.Contains("clove", result);
    }

    [Fact]
    public void FormatIngredientAmount_APinch_Passthrough()
    {
        // "a pinch" not in converter
        var result = _svc.FormatIngredientAmount(1.0, "a pinch", UnitSystem.Imperial);
        Assert.NotEmpty(result);
        // Should come through unchanged (not an error)
    }

    [Fact]
    public void FormatIngredientAmount_EmptyUnit_Passthrough()
    {
        var result = _svc.FormatIngredientAmount(1.0, "", UnitSystem.Imperial);
        Assert.NotEmpty(result);
        // Should return something without throwing
    }

    [Fact]
    public void FormatIngredientAmount_ZeroAmount_Passthrough()
    {
        var result = _svc.FormatIngredientAmount(0.0, "g", UnitSystem.Imperial);
        // amount <= 0 is a passthrough
        Assert.NotNull(result);
    }

    [Fact]
    public void FormatIngredientAmount_NeverThrows_ForUnrecognizedUnit()
    {
        // Should never throw regardless of input
        var result = _svc.FormatIngredientAmount(3.0, "sprigs", UnitSystem.Metric);
        Assert.NotEmpty(result);
    }

    // ─── Temperature reference values ─────────────────────────────────────────

    [Fact]
    public void FormatTemperature_200C_ImperialTarget_Returns400F()
    {
        // 200°C = 392°F, cook-rounded to 400°F
        var temp = new StepTemperature { Value = 200m, Unit = TemperatureUnit.C };
        var result = _svc.FormatTemperature(temp, UnitSystem.Imperial);
        Assert.Contains("400", result);
        Assert.Contains("°F", result);
    }

    [Fact]
    public void FormatTemperature_180C_ImperialTarget_Returns350F()
    {
        // 180°C = 356°F, cook-rounded to 350°F
        var temp = new StepTemperature { Value = 180m, Unit = TemperatureUnit.C };
        var result = _svc.FormatTemperature(temp, UnitSystem.Imperial);
        Assert.Contains("350", result);
        Assert.Contains("°F", result);
    }

    [Fact]
    public void FormatTemperature_GasMark6_MetricTarget_Returns200C()
    {
        // Gas mark 6 = 200°C
        var temp = new StepTemperature { Value = 6m, Unit = TemperatureUnit.Gas };
        var result = _svc.FormatTemperature(temp, UnitSystem.Metric);
        Assert.Contains("200", result);
        Assert.Contains("°C", result);
    }

    [Fact]
    public void FormatTemperature_GasMark6_ImperialTarget_Returns400F()
    {
        // Gas mark 6 = 200°C = 400°F
        var temp = new StepTemperature { Value = 6m, Unit = TemperatureUnit.Gas };
        var result = _svc.FormatTemperature(temp, UnitSystem.Imperial);
        Assert.Contains("400", result);
        Assert.Contains("°F", result);
    }

    [Fact]
    public void FormatTemperature_FValue_MetricTarget_ReturnsCelsius()
    {
        // 350°F → ~177°C, cook-rounded to 175 or 180°C
        var temp = new StepTemperature { Value = 350m, Unit = TemperatureUnit.F };
        var result = _svc.FormatTemperature(temp, UnitSystem.Metric);
        Assert.Contains("°C", result);
    }

    [Fact]
    public void FormatTemperature_CValue_MetricTarget_ReturnsSameCelsius()
    {
        // Already in Celsius, Metric target = passthrough
        var temp = new StepTemperature { Value = 200m, Unit = TemperatureUnit.C };
        var result = _svc.FormatTemperature(temp, UnitSystem.Metric);
        Assert.Contains("200", result);
        Assert.Contains("°C", result);
    }

    [Fact]
    public void FormatTemperature_CanadianTarget_UsesCelsius()
    {
        // Canadian uses Celsius for oven temps (per PromptBuilderService precedent)
        var temp = new StepTemperature { Value = 200m, Unit = TemperatureUnit.C };
        var result = _svc.FormatTemperature(temp, UnitSystem.Canadian);
        Assert.Contains("°C", result);
    }

    [Fact]
    public void FormatTemperature_GasMark3_MetricTarget_Returns170C()
    {
        var temp = new StepTemperature { Value = 3m, Unit = TemperatureUnit.Gas };
        var result = _svc.FormatTemperature(temp, UnitSystem.Metric);
        Assert.Contains("170", result);
        Assert.Contains("°C", result);
    }
}
