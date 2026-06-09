namespace CookBot.Domain.Entities;

/// <summary>
/// CNF food record (NUTR-01 / Phase 15 / D-15-02).
/// <para>
/// <b>FoodId</b> is the CNF FoodCode (user-visible code from the REST API, NOT the internal FoodID).
/// Values are stored <b>verbatim</b> — OGL-Canada forbids modifying nutrient values;
/// column/food subsetting is allowed but values must remain unchanged.
/// </para>
/// </summary>
public class CnfFood
{
    /// <summary>Primary key — CNF FoodCode (not the internal FoodID).</summary>
    public int FoodId { get; set; }

    /// <summary>Full English food description as published in the CNF.</summary>
    public string FoodDescription { get; set; } = string.Empty;

    /// <summary>
    /// Pre-computed normalized description for runtime match (populated at seed load by
    /// <c>IngredientNormalizer.Normalize</c>). Null until the seeder back-fills it (Plan 03).
    /// </summary>
    public string? NormalizedDescription { get; set; }

    /// <summary>CNF food group name (optional). Null when not present in the seed.</summary>
    public string? FoodGroup { get; set; }

    /// <summary>Energy per 100 g (kcal). Stored verbatim from CNF (OGL-Canada).</summary>
    public double EnergyKcalPer100g { get; set; }

    /// <summary>Protein per 100 g (g). Stored verbatim from CNF (OGL-Canada).</summary>
    public double ProteinGPer100g { get; set; }

    /// <summary>Total fat per 100 g (g). Stored verbatim from CNF (OGL-Canada).</summary>
    public double FatGPer100g { get; set; }

    /// <summary>Carbohydrate per 100 g (g). Stored verbatim from CNF (OGL-Canada).</summary>
    public double CarbGPer100g { get; set; }

    /// <summary>Household-measure conversion factors for this food (FK children).</summary>
    public ICollection<CnfConversionFactor> ConversionFactors { get; set; } = [];
}
