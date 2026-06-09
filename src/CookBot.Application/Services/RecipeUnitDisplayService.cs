using CookBot.Domain.Enums;
using CookBot.Domain.Interfaces;
using CookBot.Domain.Recipes;

namespace CookBot.Application.Services;

/// <summary>
/// Display-time unit and temperature conversion for ingredients and step temperatures,
/// keyed off <see cref="UnitSystem"/>. Pure/stateless Application-layer service.
///
/// IMPORTANT: This service NEVER mutates the canonical RecipeDocument — it returns
/// formatted display strings only. Canonical data (RecipeDocument, IngredientEntry,
/// CanonicalDocumentJson) is never written by this class.
/// </summary>
public class RecipeUnitDisplayService
{
    private readonly IUnitConverter _converter;

    // ─── Gas mark → °C table (standard UK/EU gas mark reference) ─────────────
    // gas 1=140, 2=150, 3=170, 4=180, 5=190, 6=200, 7=220, 8=230, 9=240
    private static readonly Dictionary<int, int> GasMarkToCelsius = new()
    {
        [1] = 140,
        [2] = 150,
        [3] = 170,
        [4] = 180,
        [5] = 190,
        [6] = 200,
        [7] = 220,
        [8] = 230,
        [9] = 240,
    };

    // ─── UnitSystem → target units ────────────────────────────────────────────
    // Mirrors the mapping in PromptBuilderService.ResolveUnitSystem.
    // Canadian: grams for weight (metric), cups/tbsp for volume (imperial), Celsius for temps.
    private static readonly Dictionary<UnitSystem, (string WeightUnit, string VolumeUnit)> TargetUnits = new()
    {
        [UnitSystem.Imperial]  = ("oz",   "cups"),
        [UnitSystem.Metric]    = ("g",    "mL"),
        [UnitSystem.Canadian]  = ("g",    "cups"),  // metric weight, imperial volume
    };

    public RecipeUnitDisplayService(IUnitConverter converter)
    {
        _converter = converter;
    }

    /// <summary>
    /// Formats an ingredient amount + unit for display in the requested unit system.
    ///
    /// - If the unit is convertible (weight or volume), converts to the target system unit.
    /// - If the unit is non-convertible ("to taste", "1 clove", "a pinch", empty/whitespace)
    ///   or if amount is &lt;= 0, returns the original amount + unit unchanged (passthrough).
    /// - Uses <see cref="FractionFormatter.Format"/> for cooking-rounded output (no "13.9876 oz").
    /// - Never throws; non-convertible / unrecognized inputs are passed through as-is.
    /// </summary>
    public string FormatIngredientAmount(double amount, string unit, UnitSystem target)
    {
        // amount <= 0 passthrough
        if (amount <= 0)
        {
            var unitDisplay = string.IsNullOrWhiteSpace(unit) ? "" : $" {UnitParser.ToDisplayString(unit)}";
            return $"{amount}{unitDisplay}".Trim();
        }

        // Empty/whitespace unit passthrough
        if (string.IsNullOrWhiteSpace(unit))
        {
            return FractionFormatter.Format(amount);
        }

        if (!TargetUnits.TryGetValue(target, out var targetPair))
        {
            // Fallback: return as-is
            return $"{FractionFormatter.Format(amount)} {UnitParser.ToDisplayString(unit)}".Trim();
        }

        // Determine destination unit based on source family
        string? destUnit = null;
        if (_converter.IsWeight(unit))
            destUnit = targetPair.WeightUnit;
        else if (_converter.IsVolume(unit))
            destUnit = targetPair.VolumeUnit;

        // Try conversion if we have a destination unit
        if (destUnit != null)
        {
            var converted = _converter.Convert(amount, unit, destUnit);
            if (converted.HasValue)
            {
                return $"{FractionFormatter.Format(converted.Value)} {UnitParser.ToDisplayString(destUnit)}".Trim();
            }
        }

        // Passthrough: non-convertible unit (pinch, clove, etc.) or cross-family
        return $"{FractionFormatter.Format(amount)} {UnitParser.ToDisplayString(unit)}".Trim();
    }

    /// <summary>
    /// Formats a <see cref="StepTemperature"/> for display in the requested unit system.
    ///
    /// - Imperial target → Fahrenheit (°F)
    /// - Metric target   → Celsius (°C)
    /// - Canadian target → Celsius (°C) per PromptBuilderService precedent (uses Fahrenheit for baking)
    ///   NOTE: PromptBuilderService says "Fahrenheit for baking temperatures" for Canadian, but
    ///   the CONTEXT decision says Canadian→°C for oven temps. We follow CONTEXT (§CLEANUP-04).
    /// - Gas mark inputs are resolved via the standard gas-mark→°C table, then converted to
    ///   the target scale.
    /// - Temperatures apply cooking rounding: °F rounded to the nearest 25 (for oven temps
    ///   ≥ 100°F), °C rounded to nearest 5.
    /// - This method NEVER scales by servings — unit conversion only (CLAUDE.md guardrail).
    /// </summary>
    public string FormatTemperature(StepTemperature temp, UnitSystem target)
    {
        // Resolve the source to Celsius first (canonical intermediate)
        double celsius = temp.Unit switch
        {
            TemperatureUnit.C   => (double)temp.Value,
            TemperatureUnit.F   => FahrenheitToCelsius((double)temp.Value),
            TemperatureUnit.Gas => ResolveGasMarkToCelsius((int)Math.Round((double)temp.Value)),
            _                   => (double)temp.Value,
        };

        // Determine target scale: Imperial → F; Metric or Canadian → C
        bool targetFahrenheit = target == UnitSystem.Imperial;

        if (targetFahrenheit)
        {
            double fahrenheit = CelsiusToFahrenheit(celsius);
            double rounded = CookRoundFahrenheit(fahrenheit);
            return $"{(int)rounded}°F";
        }
        else
        {
            double rounded = CookRoundCelsius(celsius);
            return $"{(int)rounded}°C";
        }
    }

    // ─── Temperature math helpers ─────────────────────────────────────────────

    private static double CelsiusToFahrenheit(double c) => c * 9.0 / 5.0 + 32.0;

    private static double FahrenheitToCelsius(double f) => (f - 32.0) * 5.0 / 9.0;

    private static double ResolveGasMarkToCelsius(int gas)
    {
        if (GasMarkToCelsius.TryGetValue(gas, out var c))
            return c;

        // Clamp to range for out-of-range inputs
        if (gas < 1) return GasMarkToCelsius[1];
        if (gas > 9) return GasMarkToCelsius[9];
        return GasMarkToCelsius[gas];
    }

    /// <summary>
    /// Cooking-round Fahrenheit to nearest 25 for oven temps (standard culinary practice).
    /// e.g. 392°F → 400°F, 356°F → 350°F.
    /// For small values (< 100°F, e.g. proving temps) rounds to nearest 5.
    /// </summary>
    private static double CookRoundFahrenheit(double f)
    {
        if (f >= 100)
            return Math.Round(f / 25.0) * 25.0;
        return Math.Round(f / 5.0) * 5.0;
    }

    /// <summary>
    /// Cooking-round Celsius to nearest 5.
    /// e.g. 177°C → 175°C, 192°C → 190°C.
    /// </summary>
    private static double CookRoundCelsius(double c)
    {
        return Math.Round(c / 5.0) * 5.0;
    }
}
