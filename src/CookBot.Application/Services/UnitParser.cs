using CookBot.Domain.Enums;

namespace CookBot.Application.Services;

public static class UnitParser
{
    private static readonly Dictionary<string, MeasurementUnit> UnitMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Volume - Imperial
        ["tsp"] = MeasurementUnit.Teaspoon,
        ["teaspoon"] = MeasurementUnit.Teaspoon,
        ["teaspoons"] = MeasurementUnit.Teaspoon,
        ["tbsp"] = MeasurementUnit.Tablespoon,
        ["tablespoon"] = MeasurementUnit.Tablespoon,
        ["tablespoons"] = MeasurementUnit.Tablespoon,
        ["fl oz"] = MeasurementUnit.FluidOunce,
        ["fluid ounce"] = MeasurementUnit.FluidOunce,
        ["fluid ounces"] = MeasurementUnit.FluidOunce,
        ["cup"] = MeasurementUnit.Cup,
        ["cups"] = MeasurementUnit.Cup,
        ["c"] = MeasurementUnit.Cup,
        ["pint"] = MeasurementUnit.Pint,
        ["pints"] = MeasurementUnit.Pint,
        ["pt"] = MeasurementUnit.Pint,
        ["quart"] = MeasurementUnit.Quart,
        ["quarts"] = MeasurementUnit.Quart,
        ["qt"] = MeasurementUnit.Quart,
        ["gallon"] = MeasurementUnit.Gallon,
        ["gallons"] = MeasurementUnit.Gallon,
        ["gal"] = MeasurementUnit.Gallon,

        // Volume - Metric
        ["ml"] = MeasurementUnit.Milliliter,
        ["milliliter"] = MeasurementUnit.Milliliter,
        ["milliliters"] = MeasurementUnit.Milliliter,
        ["l"] = MeasurementUnit.Liter,
        ["liter"] = MeasurementUnit.Liter,
        ["liters"] = MeasurementUnit.Liter,
        ["litre"] = MeasurementUnit.Liter,
        ["litres"] = MeasurementUnit.Liter,

        // Weight - Imperial
        ["oz"] = MeasurementUnit.Ounce,
        ["ounce"] = MeasurementUnit.Ounce,
        ["ounces"] = MeasurementUnit.Ounce,
        ["lb"] = MeasurementUnit.Pound,
        ["lbs"] = MeasurementUnit.Pound,
        ["pound"] = MeasurementUnit.Pound,
        ["pounds"] = MeasurementUnit.Pound,

        // Weight - Metric
        ["g"] = MeasurementUnit.Gram,
        ["gram"] = MeasurementUnit.Gram,
        ["grams"] = MeasurementUnit.Gram,
        ["kg"] = MeasurementUnit.Kilogram,
        ["kilogram"] = MeasurementUnit.Kilogram,
        ["kilograms"] = MeasurementUnit.Kilogram,

        // Count
        ["piece"] = MeasurementUnit.Piece,
        ["pieces"] = MeasurementUnit.Piece,
        ["pcs"] = MeasurementUnit.Piece,
        ["dozen"] = MeasurementUnit.Dozen,

        // Other
        ["pinch"] = MeasurementUnit.Pinch,
        ["dash"] = MeasurementUnit.Dash,
        ["clove"] = MeasurementUnit.Clove,
        ["cloves"] = MeasurementUnit.Clove,
        ["bunch"] = MeasurementUnit.Bunch,
        ["can"] = MeasurementUnit.Can,
        ["cans"] = MeasurementUnit.Can,
        ["package"] = MeasurementUnit.Package,
        ["packages"] = MeasurementUnit.Package,
        ["pkg"] = MeasurementUnit.Package,
        ["slice"] = MeasurementUnit.Slice,
        ["slices"] = MeasurementUnit.Slice,
        ["stick"] = MeasurementUnit.Stick,
        ["sticks"] = MeasurementUnit.Stick,
    };

    public static MeasurementUnit Parse(string unit)
    {
        var trimmed = unit.Trim();
        if (UnitMap.TryGetValue(trimmed, out var result))
            return result;
        return MeasurementUnit.Piece;
    }

    public static string ToDisplayString(MeasurementUnit unit) => unit switch
    {
        MeasurementUnit.Teaspoon => "tsp",
        MeasurementUnit.Tablespoon => "tbsp",
        MeasurementUnit.FluidOunce => "fl oz",
        MeasurementUnit.Cup => "cups",
        MeasurementUnit.Pint => "pints",
        MeasurementUnit.Quart => "quarts",
        MeasurementUnit.Gallon => "gallons",
        MeasurementUnit.Milliliter => "mL",
        MeasurementUnit.Liter => "L",
        MeasurementUnit.Ounce => "oz",
        MeasurementUnit.Pound => "lbs",
        MeasurementUnit.Gram => "g",
        MeasurementUnit.Kilogram => "kg",
        MeasurementUnit.Piece => "pcs",
        MeasurementUnit.Dozen => "dozen",
        MeasurementUnit.Pinch => "pinch",
        MeasurementUnit.Dash => "dash",
        MeasurementUnit.Clove => "cloves",
        MeasurementUnit.Bunch => "bunch",
        MeasurementUnit.Can => "cans",
        MeasurementUnit.Package => "pkg",
        MeasurementUnit.Slice => "slices",
        MeasurementUnit.Stick => "sticks",
        _ => unit.ToString().ToLower()
    };
}
