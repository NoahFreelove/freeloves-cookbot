using CookBot.Domain.Enums;

namespace CookBot.Domain.Interfaces;

public interface IUnitConverter
{
    bool CanConvert(MeasurementUnit from, MeasurementUnit to);
    double Convert(double amount, MeasurementUnit from, MeasurementUnit to);
    MeasurementUnit GetBaseUnit(MeasurementUnit unit);
    bool IsVolume(MeasurementUnit unit);
    bool IsWeight(MeasurementUnit unit);
}
