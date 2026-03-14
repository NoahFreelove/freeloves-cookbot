using CookBot.Application.Services;
using CookBot.Domain.Enums;

namespace CookBot.Tests.Services;

public class UnitParserTests
{
    [Theory]
    [InlineData("cups", MeasurementUnit.Cup)]
    [InlineData("tbsp", MeasurementUnit.Tablespoon)]
    [InlineData("g", MeasurementUnit.Gram)]
    [InlineData("mL", MeasurementUnit.Milliliter)]
    public void TryParse_KnownUnit_ReturnsEnum(string input, MeasurementUnit expected)
    {
        var result = UnitParser.TryParse(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("handful")]
    [InlineData("splash")]
    [InlineData("large clove")]
    [InlineData("")]
    public void TryParse_UnknownUnit_ReturnsNull(string input)
    {
        var result = UnitParser.TryParse(input);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("cups", "cups")]
    [InlineData("handful", "handful")]
    [InlineData("g", "g")]
    public void ToDisplayString_PassesThrough(string input, string expected)
    {
        var result = UnitParser.ToDisplayString(input);
        Assert.Equal(expected, result);
    }
}
