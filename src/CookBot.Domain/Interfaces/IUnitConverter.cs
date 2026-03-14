namespace CookBot.Domain.Interfaces;

public interface IUnitConverter
{
    bool CanConvert(string fromUnit, string toUnit);
    double? Convert(double amount, string fromUnit, string toUnit);
    bool IsVolume(string unit);
    bool IsWeight(string unit);
}
