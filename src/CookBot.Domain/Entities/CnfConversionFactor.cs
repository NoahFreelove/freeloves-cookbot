namespace CookBot.Domain.Entities;

/// <summary>
/// CNF household-measure → gram conversion factor for a specific food (NUTR-01 / Phase 15 / D-15-02).
/// <para>
/// <b>ConversionFactorValue</b> is the CNF-published multiplier: grams_in_named_measure / 100.
/// Multiplying a food's per-100 g nutrient value by <c>ConversionFactorValue</c> yields the
/// nutrient content for the named measure (e.g. "250 mL cup").
/// Values stored <b>verbatim</b> — OGL-Canada forbids modifying nutrient values.
/// </para>
/// </summary>
public class CnfConversionFactor
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>Foreign key → <see cref="CnfFood.FoodId"/>.</summary>
    public int FoodId { get; set; }

    /// <summary>Human-readable measure label as published in the CNF (e.g. "250mL", "15mL", "1 cup").</summary>
    public string MeasureDescription { get; set; } = string.Empty;

    /// <summary>
    /// CNF conversion factor value: grams_in_measure / 100.
    /// Multiply a per-100 g nutrient value by this factor to get the nutrient in the named measure.
    /// Stored verbatim from CNF (OGL-Canada).
    /// </summary>
    public double ConversionFactorValue { get; set; }

    /// <summary>Navigation property back to the parent <see cref="CnfFood"/>.</summary>
    public CnfFood Food { get; set; } = null!;
}
