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

    public bool IsVolume(string unit)
    {
        var parsed = UnitParser.TryParse(unit);
        return parsed.HasValue && VolumeToMl.ContainsKey(parsed.Value);
    }

    public bool IsWeight(string unit)
    {
        var parsed = UnitParser.TryParse(unit);
        return parsed.HasValue && WeightToGrams.ContainsKey(parsed.Value);
    }

    public bool CanConvert(string fromUnit, string toUnit)
    {
        var from = UnitParser.TryParse(fromUnit);
        var to = UnitParser.TryParse(toUnit);

        if (!from.HasValue || !to.HasValue)
            return false;

        if (from.Value == to.Value) return true;
        if (VolumeToMl.ContainsKey(from.Value) && VolumeToMl.ContainsKey(to.Value)) return true;
        if (WeightToGrams.ContainsKey(from.Value) && WeightToGrams.ContainsKey(to.Value)) return true;

        return false;
    }

    public double? Convert(double amount, string fromUnit, string toUnit)
    {
        var from = UnitParser.TryParse(fromUnit);
        var to = UnitParser.TryParse(toUnit);

        if (!from.HasValue || !to.HasValue)
            return null;

        if (from.Value == to.Value)
            return amount;

        if (VolumeToMl.ContainsKey(from.Value) && VolumeToMl.ContainsKey(to.Value))
        {
            var ml = amount * VolumeToMl[from.Value];
            return ml / VolumeToMl[to.Value];
        }

        if (WeightToGrams.ContainsKey(from.Value) && WeightToGrams.ContainsKey(to.Value))
        {
            var grams = amount * WeightToGrams[from.Value];
            return grams / WeightToGrams[to.Value];
        }

        return null;
    }
}
