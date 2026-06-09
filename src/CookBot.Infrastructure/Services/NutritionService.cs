using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;
using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CookBot.Infrastructure.Services;

/// <summary>
/// Offline nutrition compute engine (NUTR-02/03/04 / Phase 15 / Plan 05).
/// Lives in Infrastructure so it can inject <see cref="CookBotDbContext"/> directly for
/// bulk-load CNF queries — mirrors the <see cref="RecipePhotoService"/> precedent.
///
/// <b>Compute path summary:</b>
/// 1. Token-intersection match over pre-normalized <c>CnfFood.NormalizedDescription</c>
///    (HIGH ≥ 0.80 / MEDIUM 0.50–0.79 / UNMATCHED &lt; 0.50).
/// 2. Volume → grams: CNF ConversionFactor first (closest-mL, ±20%, US-cup 0.9464 scale),
///    then <see cref="IngredientDensityProvider"/> fallback (confidence downgraded to MEDIUM),
///    then null (UNMATCHED) if no path.
/// 3. Mass units converted directly via IUnitConverter.
/// 4. Unmatched / no-density → null energy (never 0) — SC2/NUTR-04.
/// 5. Writes <see cref="RecipeNutritionCache"/> (upsert) with SHA-256 content hash.
///
/// <b>Hard invariants:</b>
/// - NEVER referenced from RecipeService (P7/SC1 — grep must return 0).
/// - NEVER writes to Recipe.CanonicalDocumentJson (P15).
/// </summary>
public class NutritionService : INutritionService
{
    private readonly CookBotDbContext _db;
    private readonly IRepository<Cookbook> _cookbookRepo;
    private readonly IngredientDensityProvider _densityProvider;
    private readonly IUnitConverter _unitConverter;
    private readonly JsonRecipeSerializer _serializer;
    private readonly ILogger<NutritionService> _logger;

    public NutritionService(
        CookBotDbContext db,
        IRepository<Cookbook> cookbookRepo,
        IngredientDensityProvider densityProvider,
        IUnitConverter unitConverter,
        JsonRecipeSerializer serializer,
        ILogger<NutritionService> logger)
    {
        _db = db;
        _cookbookRepo = cookbookRepo;
        _densityProvider = densityProvider;
        _unitConverter = unitConverter;
        _serializer = serializer;
        _logger = logger;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<RecipeNutritionCache?> GetCacheAsync(int recipeId, int userId)
    {
        await AssertOwnershipAsync(recipeId, userId);
        return await _db.RecipeNutritionCaches
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.RecipeId == recipeId);
    }

    /// <inheritdoc/>
    public async Task<RecipeNutritionCache> ComputeAsync(int recipeId, int userId)
    {
        // (a) Assert ownership + load recipe
        await AssertOwnershipAsync(recipeId, userId);
        var recipe = await _db.Recipes.FindAsync(recipeId)
            ?? throw new InvalidOperationException("Recipe not found.");

        // WR-03: Require a non-empty canonical document so the hash input is identical to
        // the one RecipeService writes in MarkNutritionCacheStaleIfChangedAsync (which
        // also returns early on null/empty and never writes a hash for an empty canonical).
        // A recipe without a canonical doc cannot be nutritionally computed — the
        // synthesized-default path silently hashed "" while the stale-mark never wrote
        // that hash, making staleness comparison undefined.
        var canonicalJson = recipe.CanonicalDocumentJson;
        if (string.IsNullOrEmpty(canonicalJson))
            throw new InvalidOperationException(
                "Recipe has no canonical document — open it in the editor and save once to migrate, then recalculate nutrition.");

        var doc = _serializer.Deserialize(canonicalJson);

        int servings = Math.Max(doc.Servings, 1);

        // (b) Pre-load all CNF foods (ID + normalized description + 4 macros + CFs) once per compute.
        //     AsNoTracking for read-only bulk load — O(ingredients × 5,690) in-memory scoring.
        var allFoods = await _db.CnfFoods
            .AsNoTracking()
            .Include(f => f.ConversionFactors)
            .ToListAsync();

        // WR-02: Pre-tokenize each CNF food once (outside the per-ingredient loop) so the
        // inner loop does not rebuild a HashSet<string> per (ingredient × food) pair.
        // This reduces allocations from O(ingredients × foods) to O(foods) tokenizations.
        var indexedFoods = allFoods
            .Select(f =>
            {
                var desc = f.NormalizedDescription ?? IngredientNormalizer.Normalize(f.FoodDescription);
                var tokens = desc.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                return (Food: f, Tokens: tokens, Desc: desc);
            })
            .ToList();

        // WR-02: Hard cap on ingredient count to prevent a user-controlled recipe from
        // blocking the circuit thread with a multi-million-iteration scan.
        const int MaxIngredientsPerCompute = 200;
        var ingredientsToProcess = doc.Ingredients.Count <= MaxIngredientsPerCompute
            ? doc.Ingredients
            : doc.Ingredients.Take(MaxIngredientsPerCompute).ToList();

        // (c) Match + convert + scale each ingredient
        var perIngredient = new List<PerIngredientMatchRecord>();
        double totalKcal = 0, totalProteinG = 0, totalFatG = 0, totalCarbG = 0;
        int matchedCount = 0;

        foreach (var entry in ingredientsToProcess)
        {
            var record = MatchAndConvert(entry, indexedFoods);
            perIngredient.Add(record);

            if (record.Confidence != "UNMATCHED" && record.EnergyKcal.HasValue)
            {
                totalKcal    += record.EnergyKcal.Value;
                totalProteinG += record.ProteinG ?? 0;
                totalFatG    += record.FatG ?? 0;
                totalCarbG   += record.CarbG ?? 0;
                matchedCount++;
            }
        }

        // TotalIngredients reflects the full recipe list (including any beyond the cap),
        // so the coverage summary is honest about recipe size even if capped.
        int totalCount = doc.Ingredients.Count;

        // (d) Build per-ingredient JSON
        var matchJsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        var perIngredientJson = JsonSerializer.Serialize(perIngredient, matchJsonOptions);

        // (e) Compute content hash
        var hash = ComputeHash(canonicalJson);

        // (f) Upsert RecipeNutritionCache with concurrency-safe retry (WR-04).
        // The RowVersion concurrency token detects concurrent stale-mark writes from
        // RecipeService on a different DbContext.  On conflict, re-read the current row
        // and re-apply our compute results so the freshest data always wins.
        // A DbUpdateException (PK violation) can occur when two ComputeAsync calls both
        // observe no existing row and both try to Insert; handle by re-reading and updating.
        RecipeNutritionCache? existing = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                // Re-read on retry to get the current RowVersion tracked by this context.
                existing = await _db.RecipeNutritionCaches.FindAsync(recipeId);
                if (existing is not null)
                {
                    existing.CanonicalDocHash       = hash;
                    existing.IsStale                = false;
                    existing.TotalEnergyKcal        = totalKcal;
                    existing.TotalProteinG          = totalProteinG;
                    existing.TotalFatG              = totalFatG;
                    existing.TotalCarbG             = totalCarbG;
                    existing.Servings               = servings;  // WR-06: store clamped divisor
                    existing.PerServingEnergyKcal   = totalKcal / servings;
                    existing.PerServingProteinG     = totalProteinG / servings;
                    existing.PerServingFatG         = totalFatG / servings;
                    existing.PerServingCarbG        = totalCarbG / servings;
                    existing.MatchedIngredients     = matchedCount;
                    existing.TotalIngredients       = totalCount;
                    existing.PerIngredientMatchJson = perIngredientJson;
                    existing.ComputedAt             = DateTime.UtcNow;
                    existing.RowVersion             = Guid.NewGuid(); // refresh token
                }
                else
                {
                    existing = new RecipeNutritionCache
                    {
                        RecipeId                = recipeId,
                        CanonicalDocHash        = hash,
                        IsStale                 = false,
                        TotalEnergyKcal         = totalKcal,
                        TotalProteinG           = totalProteinG,
                        TotalFatG               = totalFatG,
                        TotalCarbG              = totalCarbG,
                        Servings                = servings,  // WR-06: store clamped divisor
                        PerServingEnergyKcal    = totalKcal / servings,
                        PerServingProteinG      = totalProteinG / servings,
                        PerServingFatG          = totalFatG / servings,
                        PerServingCarbG         = totalCarbG / servings,
                        MatchedIngredients      = matchedCount,
                        TotalIngredients        = totalCount,
                        PerIngredientMatchJson  = perIngredientJson,
                        ComputedAt              = DateTime.UtcNow,
                        RowVersion              = Guid.NewGuid(),
                    };
                    _db.RecipeNutritionCaches.Add(existing);
                }

                await _db.SaveChangesAsync();
                break; // success — exit retry loop
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                // Another writer (RecipeService stale-mark or a concurrent ComputeAsync)
                // modified the row between our FindAsync and SaveChangesAsync.
                // Detach the stale entry and retry from a fresh read.
                _logger.LogWarning(ex,
                    "Nutrition cache concurrency conflict for recipe {RecipeId}, attempt {Attempt}/3 — retrying.",
                    recipeId, attempt + 1);
                foreach (var entry in ex.Entries)
                    entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                existing = null;

                if (attempt == 2)
                    throw; // give up after 3 attempts
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
                when (ex.InnerException?.Message.Contains("UNIQUE constraint failed") == true ||
                      ex.InnerException?.Message.Contains("PRIMARY KEY") == true)
            {
                // Two concurrent ComputeAsync calls both saw no existing row and both tried
                // to Insert.  Detach and retry — the next attempt will find the row via FindAsync
                // and do an Update instead.
                _logger.LogWarning(ex,
                    "Nutrition cache duplicate-insert race for recipe {RecipeId}, attempt {Attempt}/3 — retrying.",
                    recipeId, attempt + 1);
                foreach (var entry in _db.ChangeTracker.Entries<RecipeNutritionCache>())
                    entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                existing = null;

                if (attempt == 2)
                    throw;
            }
        }

        return existing!;
    }

    // ── Core matching + conversion ─────────────────────────────────────────────

    /// <summary>
    /// Match one ingredient entry to a CNF food, convert amount to grams,
    /// scale macros, and return a per-ingredient record.
    /// Unmatched → confidence UNMATCHED, null energy (never 0).
    /// Low-confidence density path → confidence MEDIUM, "≈" marker.
    /// </summary>
    /// <param name="entry">The ingredient entry to match.</param>
    /// <param name="indexedFoods">Pre-tokenized CNF food index (WR-02 — built once per compute, not per call).</param>
    private PerIngredientMatchRecord MatchAndConvert(
        CookBot.Domain.Recipes.IngredientEntry entry,
        List<(CnfFood Food, HashSet<string> Tokens, string Desc)> indexedFoods)
    {
        var normalizedName = IngredientNormalizer.Normalize(entry.Name);
        var recipeTokens = normalizedName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Distinct()
            .ToArray();

        // ── Step 1: Token-intersection match (F1 score) ───────────────────────
        // WR-01: Use F1 (harmonic mean of precision and recall) so a 1-token
        // ingredient matching 1-of-N CNF tokens scores proportionally to N,
        // not 1.0 (which the old recall-only formula produced for single tokens).
        // Precision = matchCount / cnfTokenCount  (how much of the CNF food the
        //             recipe name covers — low when CNF name is long/specific).
        // Recall    = matchCount / recipeTokens.Length  (how much of the recipe
        //             ingredient name is found in the CNF description).
        // F1        = 2 * P * R / (P + R)  — balanced, never trivially 1.0 for
        //             a single-token query against a 10-token CNF description.
        CnfFood? bestFood = null;
        string? bestDesc = null;
        double bestScore = 0;

        foreach (var (food, cnfTokens, desc) in indexedFoods)
        {
            int matchCount = recipeTokens.Count(t => cnfTokens.Contains(t));
            double recall    = recipeTokens.Length == 0 ? 0 : (double)matchCount / recipeTokens.Length;
            double precision = cnfTokens.Count == 0 ? 0 : (double)matchCount / cnfTokens.Count;
            double score     = (precision + recall) == 0 ? 0 : 2 * precision * recall / (precision + recall);

            if (score > bestScore ||
                (score == bestScore && bestFood is not null &&
                 Math.Abs(desc.Length - normalizedName.Length) < Math.Abs(bestDesc!.Length - normalizedName.Length)))
            {
                bestScore = score;
                bestFood = food;
                bestDesc = desc;
            }
        }

        // ── Step 2: Confidence tier ──────────────────────────────────────────
        string confidence;
        if (bestFood is null || bestScore < 0.50)
            confidence = "UNMATCHED";
        else if (bestScore >= 0.80)
            confidence = "HIGH";
        else
            confidence = "MEDIUM";

        if (confidence == "UNMATCHED")
        {
            return new PerIngredientMatchRecord
            {
                Name           = entry.Name,
                NormalizedName = normalizedName,
                Confidence     = "UNMATCHED",
            };
        }

        // ── Step 3: Convert amount → grams ───────────────────────────────────
        double? grams = null;
        string? conversionMethod = null;
        string? measureUsed = null;
        string finalConfidence = confidence;

        if (_unitConverter.IsWeight(entry.Unit))
        {
            // Mass unit: convert directly to grams
            grams = _unitConverter.Convert(entry.Amount, entry.Unit, "Gram");
            if (grams.HasValue)
            {
                conversionMethod = "MassDirect";
                measureUsed      = entry.Unit;
            }
        }
        else if (_unitConverter.IsVolume(entry.Unit))
        {
            // Volume unit: try CNF factor first
            var recipeMl = _unitConverter.Convert(entry.Amount, entry.Unit, "Milliliter");
            if (recipeMl.HasValue)
            {
                grams = TryGetGramsFromCnfFactor(bestFood.ConversionFactors, recipeMl.Value, out var mUsed);
                if (grams.HasValue)
                {
                    conversionMethod = "CnfFactor";
                    measureUsed      = mUsed;
                    // confidence stays as matched (HIGH or MEDIUM)
                }
                else
                {
                    // CNF factor failed — try curated density fallback
                    var density = _densityProvider.GetDensityGPerMl(normalizedName);
                    if (density.HasValue)
                    {
                        grams            = density.Value * recipeMl.Value;
                        conversionMethod = "DensityFallback";
                        measureUsed      = entry.Unit;
                        // Downgrade confidence to at most MEDIUM (SC2/NUTR-03 P5)
                        finalConfidence = "MEDIUM";
                    }
                    // else: no CNF factor AND no density → grams stays null → UNMATCHED-for-conversion
                }
            }
        }
        // else: unit is neither weight nor volume (e.g. piece, count) → grams stays null

        if (!grams.HasValue)
        {
            // Could not convert to grams — record match info but null energy (never 0)
            return new PerIngredientMatchRecord
            {
                Name               = entry.Name,
                NormalizedName     = normalizedName,
                CnfFoodId          = bestFood.FoodId,
                CnfFoodDescription = bestFood.FoodDescription,
                Confidence         = "UNMATCHED",
                ConversionMethod   = null,
                MeasureUsed        = null,
                GramsComputed      = null,
                EnergyKcal         = null,
                ProteinG           = null,
                FatG               = null,
                CarbG              = null,
            };
        }

        // ── Step 4: Scale macros (per 100 g) ─────────────────────────────────
        double scaleFactor = grams.Value / 100.0;
        double kcal   = bestFood.EnergyKcalPer100g  * scaleFactor;
        double protein = bestFood.ProteinGPer100g   * scaleFactor;
        double fat     = bestFood.FatGPer100g       * scaleFactor;
        double carb    = bestFood.CarbGPer100g      * scaleFactor;

        return new PerIngredientMatchRecord
        {
            Name               = entry.Name,
            NormalizedName     = normalizedName,
            CnfFoodId          = bestFood.FoodId,
            CnfFoodDescription = bestFood.FoodDescription,
            Confidence         = finalConfidence,
            ConversionMethod   = conversionMethod,
            MeasureUsed        = measureUsed,
            GramsComputed      = grams,
            EnergyKcal         = kcal,
            ProteinG           = protein,
            FatG               = fat,
            CarbG              = carb,
        };
    }

    /// <summary>
    /// Finds the CNF ConversionFactor whose parsed mL is closest to
    /// <paramref name="recipeMl"/> within a ±20% tolerance band,
    /// applies recipe_mL/cnf_mL scale, and returns grams.
    /// Returns null when no CF is within tolerance.
    /// </summary>
    private static double? TryGetGramsFromCnfFactor(
        IEnumerable<CnfConversionFactor> factors,
        double recipeMl,
        out string? measureUsed)
    {
        measureUsed = null;

        var candidates = factors
            .Select(f => new { Factor = f, CnfMl = ParseMlFromMeasureDescription(f.MeasureDescription) })
            .Where(x => x.CnfMl > 0)
            .OrderBy(x => Math.Abs(x.CnfMl - recipeMl))
            .ToList();

        if (candidates.Count == 0)
            return null;

        var best = candidates[0];
        double tolerance = best.CnfMl * 0.20; // ±20%
        if (Math.Abs(best.CnfMl - recipeMl) > tolerance)
            return null;

        // grams = CF × (recipe_mL / cnf_mL) × 100
        // This automatically applies the US-cup 0.9464 scale (236.588 / 250.0 = 0.9464)
        // when recipe unit is Cup and CNF measure is "250ml".
        double scale = recipeMl / best.CnfMl;
        double grams = best.Factor.ConversionFactorValue * scale * 100.0;
        measureUsed = best.Factor.MeasureDescription;
        return grams;
    }

    /// <summary>
    /// Parses a mL value from a CNF MeasureDescription string (e.g. "250ml", "15mL").
    /// Returns 0 if no numeric mL value is found.
    /// </summary>
    private static double ParseMlFromMeasureDescription(string measure)
    {
        var m = Regex.Match(measure, @"^(\d+(?:\.\d+)?)\s*ml", RegexOptions.IgnoreCase);
        return m.Success ? double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
    }

    /// <summary>
    /// Computes the SHA-256 hex digest of the canonical JSON string.
    /// Matches the hash computed by RecipeService (same input, same BCL call).
    /// </summary>
    internal static string ComputeHash(string canonicalJson)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
    }

    // ── Ownership guard (verbatim from RecipePhotoService) ────────────────────

    /// <summary>
    /// Loads the recipe's cookbook and throws <see cref="UnauthorizedAccessException"/>
    /// when <paramref name="userId"/> is not the owner (verbatim from RecipePhotoService).
    /// </summary>
    private async Task AssertOwnershipAsync(int recipeId, int userId)
    {
        var recipe = await _db.Recipes.FindAsync(recipeId)
            ?? throw new InvalidOperationException("Recipe not found.");

        var cookbook = await _cookbookRepo.GetByIdAsync(recipe.CookbookId)
            ?? throw new InvalidOperationException("Cookbook not found.");

        if (cookbook.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this cookbook.");
    }

    // ── Inner DTO for per-ingredient JSON (serialized to PerIngredientMatchJson) ──

    private sealed class PerIngredientMatchRecord
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("normalizedName")]
        public string NormalizedName { get; set; } = string.Empty;

        [JsonPropertyName("cnfFoodId")]
        public int? CnfFoodId { get; set; }

        [JsonPropertyName("cnfFoodDescription")]
        public string? CnfFoodDescription { get; set; }

        [JsonPropertyName("confidence")]
        public string Confidence { get; set; } = string.Empty;

        [JsonPropertyName("conversionMethod")]
        public string? ConversionMethod { get; set; }

        [JsonPropertyName("measureUsed")]
        public string? MeasureUsed { get; set; }

        [JsonPropertyName("gramsComputed")]
        public double? GramsComputed { get; set; }

        [JsonPropertyName("energyKcal")]
        public double? EnergyKcal { get; set; }

        [JsonPropertyName("proteinG")]
        public double? ProteinG { get; set; }

        [JsonPropertyName("fatG")]
        public double? FatG { get; set; }

        [JsonPropertyName("carbG")]
        public double? CarbG { get; set; }
    }
}
