namespace CookBot.Domain.Entities;

/// <summary>
/// Per-recipe computed-nutrition cache (NUTR-02 / Phase 15 / D-15-11).
/// <para>
/// <b>Hard invariant:</b> This entity is the ONLY home for computed nutrition data and is
/// NEVER serialized into <c>CanonicalDocumentJson</c>. Nutrition is a display-only
/// enrichment layer; <c>RecipeService</c> must not write to this table. Write-ownership
/// belongs exclusively to <c>NutritionService</c> (Plan 05).
/// </para>
/// <para>
/// Keyed by <c>RecipeId</c> (1:1 with <see cref="Recipe"/>). The <c>CanonicalDocHash</c>
/// (SHA-256 hex of the snapshot that was used to compute) drives staleness: when
/// the recipe's <c>CanonicalDocumentJson</c> changes, <c>IsStale</c> is set to true
/// and the panel shows a "recipe changed — recalculate" affordance (D-15-12).
/// </para>
/// </summary>
public class RecipeNutritionCache
{
    /// <summary>Primary key and foreign key → <see cref="Recipe.Id"/> (1:1 relationship).</summary>
    public int RecipeId { get; set; }

    /// <summary>SHA-256 hex digest of <c>CanonicalDocumentJson</c> at compute time. Used for staleness detection.</summary>
    public string CanonicalDocHash { get; set; } = string.Empty;

    /// <summary>
    /// True when the recipe's canonical doc has changed since the last compute.
    /// The panel shows a "recipe changed — recalculate" affordance when stale.
    /// </summary>
    public bool IsStale { get; set; }

    // ── Total values (entire recipe) ─────────────────────────────────────────

    /// <summary>Total energy for the whole recipe (kcal).</summary>
    public double TotalEnergyKcal { get; set; }

    /// <summary>Total protein for the whole recipe (g).</summary>
    public double TotalProteinG { get; set; }

    /// <summary>Total fat for the whole recipe (g).</summary>
    public double TotalFatG { get; set; }

    /// <summary>Total carbohydrate for the whole recipe (g).</summary>
    public double TotalCarbG { get; set; }

    /// <summary>Snapshot of <c>RecipeDocument.Servings</c> at compute time. Used as the per-serving divisor.</summary>
    public int? Servings { get; set; }

    // ── Per-serving values ───────────────────────────────────────────────────

    /// <summary>Energy per serving (kcal). Zero when <see cref="Servings"/> is null or zero.</summary>
    public double PerServingEnergyKcal { get; set; }

    /// <summary>Protein per serving (g).</summary>
    public double PerServingProteinG { get; set; }

    /// <summary>Fat per serving (g).</summary>
    public double PerServingFatG { get; set; }

    /// <summary>Carbohydrate per serving (g).</summary>
    public double PerServingCarbG { get; set; }

    // ── Coverage ─────────────────────────────────────────────────────────────

    /// <summary>Number of ingredients successfully matched to a CNF food.</summary>
    public int MatchedIngredients { get; set; }

    /// <summary>Total number of ingredients in the recipe at compute time.</summary>
    public int TotalIngredients { get; set; }

    /// <summary>
    /// JSON array of per-ingredient match results (TEXT column).
    /// Schema: <c>[{ name, cnfFoodId, cnfDesc, confidence, kcal, proteinG, fatG, carbG }]</c>.
    /// Unmatched ingredients have <c>cnfFoodId = null</c> and <c>confidence = "unmatched"</c>.
    /// NEVER written into <c>CanonicalDocumentJson</c> — lives only in this cache table.
    /// </summary>
    public string PerIngredientMatchJson { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the most recent successful compute.</summary>
    public DateTime ComputedAt { get; set; }

    /// <summary>
    /// EF concurrency token (WR-04). Refreshed (new Guid) on every write so concurrent
    /// saves on different DbContext instances detect the conflict and throw
    /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>.
    /// Stored as a TEXT column; SQLite-compatible (no native row-version type required).
    /// </summary>
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    /// <summary>Navigation property back to the parent <see cref="Recipe"/>.</summary>
    public Recipe Recipe { get; set; } = null!;
}
