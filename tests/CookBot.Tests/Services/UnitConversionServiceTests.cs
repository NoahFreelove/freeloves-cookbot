using CookBot.Application.Services;

namespace CookBot.Tests.Services;

public class UnitConversionServiceTests
{
    private readonly UnitConversionService _svc = new();

    [Fact]
    public void Convert_CupsToMl_ReturnsCorrectValue()
    {
        var result = _svc.Convert(1.0, "cups", "mL");
        Assert.NotNull(result);
        Assert.InRange(result.Value, 236.0, 237.0);
    }

    [Fact]
    public void Convert_UnknownFromUnit_ReturnsNull()
    {
        var result = _svc.Convert(1.0, "handful", "cups");
        Assert.Null(result);
    }

    [Fact]
    public void Convert_UnknownToUnit_ReturnsNull()
    {
        var result = _svc.Convert(1.0, "cups", "splash");
        Assert.Null(result);
    }

    [Fact]
    public void Convert_VolumeToWeight_ReturnsNull()
    {
        var result = _svc.Convert(1.0, "cups", "g");
        Assert.Null(result);
    }

    [Fact]
    public void CanConvert_SameKnownType_ReturnsTrue()
    {
        Assert.True(_svc.CanConvert("cups", "mL"));
    }

    [Fact]
    public void CanConvert_UnknownUnit_ReturnsFalse()
    {
        Assert.False(_svc.CanConvert("handful", "cups"));
    }
}
