using CookBot.Domain.Enums;
using CookBot.Domain.Interfaces;

namespace CookBot.Application.Services;

public class UnitConversionService : IUnitConverter
{
    // Conversion factors to base units (mL for volume, grams for weight)
    private static readonly Dictionary<MeasurementUnit, double> VolumeToMl = new()
    {
        [MeasurementUnit.Teaspoon] = 4.929,
        [MeasurementUnit.Tablespoon] = 14.787,
        [MeasurementUnit.FluidOunce] = 29.574,
        [MeasurementUnit.Cup] = 236.588,
        [MeasurementUnit.Pint] = 473.176,
        [MeasurementUnit.Quart] = 946.353,
        [MeasurementUnit.Gallon] = 3785.41,
        [MeasurementUnit.Milliliter] = 1.0,
        [MeasurementUnit.Liter] = 1000.0,
    };

    private static readonly Dictionary<MeasurementUnit, double> WeightToGrams = new()
    {
        [MeasurementUnit.Ounce] = 28.3495,
        [MeasurementUnit.Pound] = 453.592,
        [MeasurementUnit.Gram] = 1.0,
        [MeasurementUnit.Kilogram] = 1000.0,
    };

    public bool IsVolume(MeasurementUnit unit) => VolumeToMl.ContainsKey(unit);
    public bool IsWeight(MeasurementUnit unit) => WeightToGrams.ContainsKey(unit);

    public bool CanConvert(MeasurementUnit from, MeasurementUnit to)
    {
        if (from == to) return true;
        if (IsVolume(from) && IsVolume(to)) return true;
        if (IsWeight(from) && IsWeight(to)) return true;
        return false;
    }

    public double Convert(double amount, MeasurementUnit from, MeasurementUnit to)
    {
        if (from == to) return amount;

        if (IsVolume(from) && IsVolume(to))
        {
            var ml = amount * VolumeToMl[from];
            return ml / VolumeToMl[to];
        }

        if (IsWeight(from) && IsWeight(to))
        {
            var grams = amount * WeightToGrams[from];
            return grams / WeightToGrams[to];
        }

        throw new InvalidOperationException($"Cannot convert between {from} and {to}.");
    }

    public MeasurementUnit GetBaseUnit(MeasurementUnit unit)
    {
        if (IsVolume(unit)) return MeasurementUnit.Milliliter;
        if (IsWeight(unit)) return MeasurementUnit.Gram;
        return unit;
    }
}
