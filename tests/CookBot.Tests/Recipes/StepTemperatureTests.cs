using System.Text.Json;
using CookBot.Domain.Recipes;

namespace CookBot.Tests.Recipes;

/// <summary>
/// Verifies StepTemperature round-trips cleanly through System.Text.Json and
/// that the canonical wire format from D-27 deserializes correctly.
/// Validator-level per-unit rules (F/C whole-degree, gas 0.5-step) are covered
/// in RecipeValidatorTests when Plan 03 adds temperature validation.
/// </summary>
public class StepTemperatureTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Theory]
    [InlineData(350.0, TemperatureUnit.F)]
    [InlineData(180.0, TemperatureUnit.C)]
    [InlineData(4.5, TemperatureUnit.Gas)]
    [InlineData(9.5, TemperatureUnit.Gas)]
    [InlineData(1.0, TemperatureUnit.Gas)]
    public void RoundTrip_PreservesValueAndUnit(double value, TemperatureUnit expectedUnit)
    {
        var temp = new StepTemperature { Value = (decimal)value, Unit = expectedUnit };
        var json = JsonSerializer.Serialize(temp);
        var deserialized = JsonSerializer.Deserialize<StepTemperature>(json);

        Assert.NotNull(deserialized);
        Assert.Equal((decimal)value, deserialized.Value);
        Assert.Equal(expectedUnit, deserialized.Unit);
    }

    [Theory]
    [InlineData("""{"value":4.5,"unit":"gas"}""", 4.5, TemperatureUnit.Gas)]
    [InlineData("""{"value":350,"unit":"F"}""", 350.0, TemperatureUnit.F)]
    [InlineData("""{"value":180,"unit":"C"}""", 180.0, TemperatureUnit.C)]
    [InlineData("""{"value":9.5,"unit":"gas"}""", 9.5, TemperatureUnit.Gas)]
    public void Deserialize_FromCanonicalWireFormat_Succeeds(string json, double expectedValue, TemperatureUnit expectedUnit)
    {
        var deserialized = JsonSerializer.Deserialize<StepTemperature>(json);

        Assert.NotNull(deserialized);
        Assert.Equal((decimal)expectedValue, deserialized.Value);
        Assert.Equal(expectedUnit, deserialized.Unit);
    }

    [Theory]
    [InlineData(350.0, TemperatureUnit.F)]
    [InlineData(180.0, TemperatureUnit.C)]
    [InlineData(4.5, TemperatureUnit.Gas)]
    public void Equals_SameValueAndUnit_IsTrue(double value, TemperatureUnit unit)
    {
        var a = new StepTemperature { Value = (decimal)value, Unit = unit };
        var b = new StepTemperature { Value = (decimal)value, Unit = unit };

        Assert.Equal(a, b);
    }

    [Fact]
    public void Deserialize_FractionalFahrenheit_AcceptsWithoutThrow()
    {
        // Type itself is lenient — validator (Plan 03) enforces whole-degree rule for F/C
        const string json = """{"value":350.5,"unit":"F"}""";
        var deserialized = JsonSerializer.Deserialize<StepTemperature>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(350.5m, deserialized.Value);
        Assert.Equal(TemperatureUnit.F, deserialized.Unit);
    }
}
