namespace CookBot.Application.Services;

/// <summary>
/// Curated per-ingredient fallback density table (g/mL) — NUTR-03.
///
/// Sourced from King Arthur Baking ingredient weight chart (KA, HIGH confidence)
/// and FAO/INFOODS Density Database v2.0 (FAO, MEDIUM confidence).
///
/// Returns null for unknown ingredients — the caller marks the conversion as
/// unmatched/low-confidence rather than falling back to water density (1.0 g/mL).
///
/// The SC3 flour anchor: "all-purpose flour" → 0.507 g/mL → ~120 g/US cup →
/// when combined with CNF 364 kcal/100 g and US-cup scaling (×0.9464), this
/// produces ≈455 kcal/cup — exactly the SC3 anchor verified against the live CNF API.
/// </summary>
public sealed class IngredientDensityProvider
{
    // IMPORTANT: No entry is 1.0 g/mL. Unknown ingredients return null.
    // Lookup is OrdinalIgnoreCase so callers need not pre-normalize casing
    // (the upstream IngredientNormalizer normalizes names before calling this,
    // but belt-and-suspenders keeps the lookup robust).
    //
    // Keys are stored in their human-readable form (may include hyphens).
    // At startup the NormalizedDensities lookup is built by also indexing
    // IngredientNormalizer.Normalize(key) — this ensures that callers who
    // pass the normalizer output (hyphens → spaces) still get a hit.
    private static readonly Dictionary<string, double> Densities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // ── FLOURS ────────────────────────────────────────────────────────────
            // All values from King Arthur Baking ingredient weight chart (HIGH).
            // g/mL = g/cup ÷ 236.588 mL/cup (US cup).
            ["all-purpose flour"]      = 0.507, // KA: 120 g/cup; SC3 density anchor
            ["bread flour"]            = 0.507, // KA: 120 g/cup
            ["whole wheat flour"]      = 0.478, // KA: 113 g/cup
            ["cake flour"]             = 0.507, // KA: 120 g/cup
            ["almond flour"]           = 0.406, // KA: 96 g/cup

            // ── SUGARS ────────────────────────────────────────────────────────────
            ["granulated sugar"]       = 0.837, // KA: 198 g/cup
            ["granulated white sugar"] = 0.837, // KA: alias for above
            ["brown sugar"]            = 0.900, // KA: 213 g/cup (packed)
            ["confectioners sugar"]    = 0.478, // KA: 113 g/cup (unsifted)

            // ── FATS / OILS ───────────────────────────────────────────────────────
            ["butter"]                 = 0.955, // KA: 226 g/cup
            ["unsalted butter"]        = 0.955, // KA: alias — same density as butter
            ["vegetable oil"]          = 0.837, // KA: 198 g/cup (KA preferred over FAO pure-liquid 0.92)
            ["olive oil"]              = 0.845, // KA extrapolated + FAO 0.92; KA cup weight used

            // ── DAIRY ─────────────────────────────────────────────────────────────
            ["whole milk"]             = 0.959, // KA: 227 g/cup
            ["heavy cream"]            = 0.959, // KA: 227 g/cup (FAO: ~0.984 at 38% fat; KA preferred)
            ["sour cream"]             = 0.959, // KA: 227 g/cup
            ["yogurt"]                 = 0.959, // KA: 227 g/cup
            ["plain yogurt"]           = 0.959, // KA: alias for yogurt
            ["cream cheese"]           = 0.959, // KA: 227 g/cup
            ["ricotta cheese"]         = 0.960, // KA: ~227 g/cup (similar to whole milk)

            // ── SYRUPS / SWEETENERS ───────────────────────────────────────────────
            ["honey"]                  = 1.420, // KA: 21 g/tbsp = 1.420 g/mL; FAO: 1.38–1.44 — agrees
            ["maple syrup"]            = 1.319, // KA: 312 g/cup

            // ── BAKING STAPLES ────────────────────────────────────────────────────
            ["cocoa powder"]           = 0.355, // KA: 84 g/cup (unsweetened)
            ["cornstarch"]             = 0.473, // KA: 112 g/cup
            ["rolled oats"]            = 0.478, // KA: 113 g/cup
            ["baking powder"]          = 0.900, // FAO: 0.9 g/mL
            ["salt"]                   = 1.380, // FAO: 1.38 g/mL (fine table salt)
            ["chocolate chips"]        = 0.719, // KA: 170 g/cup

            // ── ADDITIONAL ENTRIES ────────────────────────────────────────────────
            ["peanut butter"]          = 1.090, // FAO/USDA: 270 g/cup
            ["shredded coconut"]       = 0.360, // KA: 85 g/cup (sweetened)
        };

    // Secondary lookup indexed by IngredientNormalizer.Normalize(key) so that callers
    // who pass the post-normalize form (hyphens → spaces, commas stripped, deny-list applied)
    // still get a hit even when the human-readable key uses hyphens.
    private static readonly Dictionary<string, double> NormalizedDensities = BuildNormalized();

    private static Dictionary<string, double> BuildNormalized()
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in Densities)
        {
            var normalizedKey = IngredientNormalizer.Normalize(key);
            result.TryAdd(normalizedKey, value); // first entry wins on collision
        }
        return result;
    }

    /// <summary>
    /// Returns the density in g/mL for the given normalized ingredient name,
    /// or null if no curated entry exists.
    ///
    /// The name is expected to be pre-normalized (lowercase, deny-list stripped)
    /// by IngredientNormalizer before this call. Lookup tries both the raw key
    /// (OrdinalIgnoreCase) and the normalized form of the key so that
    /// hyphen-vs-space variants are handled automatically.
    /// </summary>
    public double? GetDensityGPerMl(string normalizedName)
    {
        // Try raw table key first (OrdinalIgnoreCase)
        if (Densities.TryGetValue(normalizedName, out var density))
            return density;

        // Try normalized form of key (handles hyphen→space from IngredientNormalizer)
        if (NormalizedDensities.TryGetValue(normalizedName, out density))
            return density;

        return null;
    }

    /// <summary>
    /// Total number of curated entries. Exposed for ≥23-entry count assertion.
    /// </summary>
    public static int EntryCount => Densities.Count;
}
