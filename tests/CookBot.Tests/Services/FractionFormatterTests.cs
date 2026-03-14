using CookBot.Application.Services;

namespace CookBot.Tests.Services;

public class FractionFormatterTests
{
    [Theory]
    [InlineData(1.0, "1")]
    [InlineData(0.5, "1/2")]
    [InlineData(0.333, "1/3")]
    [InlineData(0.25, "1/4")]
    [InlineData(0.75, "3/4")]
    [InlineData(1.5, "1 1/2")]
    [InlineData(1.333, "1 1/3")]
    [InlineData(2.0, "2")]
    [InlineData(0.125, "1/8")]
    [InlineData(0.666, "2/3")]
    public void Format_ReturnsReadableFraction(double value, string expected)
    {
        Assert.Equal(expected, FractionFormatter.Format(value));
    }

    [Fact]
    public void Format_OddValue_ReturnsDecimal()
    {
        var result = FractionFormatter.Format(1.137);
        Assert.Equal("1.14", result);
    }
}
