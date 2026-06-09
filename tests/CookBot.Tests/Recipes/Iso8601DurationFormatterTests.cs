using CookBot.Application.Recipes;

namespace CookBot.Tests.Recipes;

public class Iso8601DurationFormatterTests
{
    [Fact]
    public void ToIso8601Duration_Null_ReturnsNull()
    {
        var result = Iso8601DurationFormatter.ToIso8601Duration(null);
        Assert.Null(result);
    }

    [Fact]
    public void ToIso8601Duration_Zero_ReturnsNull()
    {
        var result = Iso8601DurationFormatter.ToIso8601Duration(0);
        Assert.Null(result);
    }

    [Fact]
    public void ToIso8601Duration_Negative_ReturnsNull()
    {
        var result = Iso8601DurationFormatter.ToIso8601Duration(-5);
        Assert.Null(result);
    }

    [Fact]
    public void ToIso8601Duration_30_ReturnsPT30M()
    {
        var result = Iso8601DurationFormatter.ToIso8601Duration(30);
        Assert.Equal("PT30M", result);
    }

    [Fact]
    public void ToIso8601Duration_60_ReturnsPT1H()
    {
        var result = Iso8601DurationFormatter.ToIso8601Duration(60);
        Assert.Equal("PT1H", result);
    }

    [Fact]
    public void ToIso8601Duration_90_ReturnsPT1H30M()
    {
        var result = Iso8601DurationFormatter.ToIso8601Duration(90);
        Assert.Equal("PT1H30M", result);
    }

    [Fact]
    public void ToIso8601Duration_125_ReturnsPT2H5M()
    {
        var result = Iso8601DurationFormatter.ToIso8601Duration(125);
        Assert.Equal("PT2H5M", result);
    }

    /// <summary>
    /// WR-03: Hours are intentionally not rolled into days. 1500 minutes (25h) emits PT25H,
    /// not P1DT1H — schema.org / Google Rich Results accepts PT##H for &gt;24h values.
    /// This test pins the documented behaviour so a refactor cannot silently change it.
    /// </summary>
    [Fact]
    public void ToIso8601Duration_Over24Hours_EmitsPTHHForm()
    {
        // 1500 min = 25h exactly
        Assert.Equal("PT25H", Iso8601DurationFormatter.ToIso8601Duration(1500));
        // 1530 min = 25h30m
        Assert.Equal("PT25H30M", Iso8601DurationFormatter.ToIso8601Duration(1530));
    }
}
