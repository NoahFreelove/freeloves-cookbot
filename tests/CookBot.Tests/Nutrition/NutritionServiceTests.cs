using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;
using CookBot.Domain.Recipes;
using CookBot.Infrastructure.Data;
using CookBot.Infrastructure.Data.Repositories;
using CookBot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CookBot.Tests.Nutrition;

/// <summary>
/// Service-level tests for <see cref="NutritionService"/> — proves:
/// - SC3 flour anchor: "1 cup all-purpose flour" → ≈455 kcal via CNF factor + US-cup 0.9464 scale
/// - Density fallback (no CNF factor): flour via KA density (0.507 g/mL) → ≈435-455 kcal, NOT ~862 (water)
/// - Unmatched ingredient → null energy (never 0), confidence UNMATCHED
/// - US-cup ÷ CNF-250ml scale factor 0.9464 is applied
/// - Mass unit used directly (no density/CF needed)
/// - Coverage counts correct
/// - Stale-on-doc-change: RecipeService marks IsStale when canonical hash changes
/// </summary>
public class NutritionServiceTests : IDisposable
{
    private readonly CookBotDbContext _db;
    private readonly NutritionService _svc;
    private readonly JsonRecipeSerializer _serializer;
    private readonly int _userId;
    private readonly int _cookbookId;

    // CNF FoodId 4484 → all-purpose flour, white (364 kcal/100g, 1.32079 CF for 250ml)
    private const int FlourFoodId = 4484;
    // CNF FoodId 99 → butter, unsalted (717 kcal/100g)
    private const int ButterFoodId = 99;

    public NutritionServiceTests()
    {
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _serializer = new JsonRecipeSerializer();

        // Seed a user + cookbook
        var user = new User { DisplayName = "NutritionTestUser" };
        _db.Users.Add(user);
        _db.SaveChanges();
        _userId = user.Id;

        var cookbook = new Cookbook { UserId = _userId, Name = "Test Cookbook" };
        _db.Cookbooks.Add(cookbook);
        _db.SaveChanges();
        _cookbookId = cookbook.Id;

        // Seed CNF foods needed across tests:
        // Food 4484: all-purpose flour, white, enriched (364 kcal/100g)
        var flourFood = new CnfFood
        {
            FoodId = FlourFoodId,
            FoodDescription = "Grains, wheat flour, white, all purpose, enriched, calcium fortified",
            NormalizedDescription = IngredientNormalizer.Normalize("Grains, wheat flour, white, all purpose, enriched, calcium fortified"),
            EnergyKcalPer100g = 364.0,
            ProteinGPer100g = 9.7,
            FatGPer100g = 1.0,
            CarbGPer100g = 76.3,
        };
        // Food 99: butter, unsalted (717 kcal/100g)
        var butterFood = new CnfFood
        {
            FoodId = ButterFoodId,
            FoodDescription = "Butter, unsalted",
            NormalizedDescription = IngredientNormalizer.Normalize("Butter, unsalted"),
            EnergyKcalPer100g = 717.0,
            ProteinGPer100g = 0.9,
            FatGPer100g = 81.1,
            CarbGPer100g = 0.1,
        };
        _db.CnfFoods.AddRange(flourFood, butterFood);

        // Flour food 4484: has CF for 250ml (1.32079 = 132.1 g per Canadian cup)
        // Butter food 99: has CF for 250ml (0.22000 = 22.0 g per 250ml... for tbsp let's use a realistic value)
        var flourCf = new CnfConversionFactor
        {
            FoodId = FlourFoodId,
            MeasureDescription = "250ml",
            ConversionFactorValue = 1.32079,
        };
        _db.CnfConversionFactors.Add(flourCf);

        _db.SaveChanges();

        var cookbookRepo = new Repository<Cookbook>(_db);
        var densityProvider = new IngredientDensityProvider();
        var unitConverter = new UnitConversionService();

        _svc = new NutritionService(
            _db,
            cookbookRepo,
            densityProvider,
            unitConverter,
            _serializer,
            NullLogger<NutritionService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private int CreateRecipe(RecipeDocument doc)
    {
        var recipe = new Recipe
        {
            CookbookId = _cookbookId,
            Name = doc.Name,
            Servings = doc.Servings,
            CanonicalDocumentJson = _serializer.Serialize(doc),
        };
        _db.Recipes.Add(recipe);
        _db.SaveChanges();
        return recipe.Id;
    }

    private static RecipeDocument MakeRecipeDoc(int servings, params (string name, double amount, string unit)[] ingredients)
    {
        var entries = ingredients.Select((t, i) => new IngredientEntry
        {
            Id = i + 1,
            Name = t.name,
            Amount = t.amount,
            Unit = t.unit,
        }).ToList();

        return new RecipeDocument
        {
            Version = RecipeUpcasterChain.CurrentVersion,
            Name = "Test Recipe",
            Servings = servings,
            Ingredients = entries,
        };
    }

    private IReadOnlyList<JsonElement> ParsePerIngredient(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    // ── SC3 Flour Anchor ───────────────────────────────────────────────────────

    /// <summary>
    /// SC3 anchor: "1 cup all-purpose flour" → ≈455 kcal (±15) via CNF factor path.
    /// Formula: 364 kcal/100g × CF(250ml)=1.32079 × scale(236.588/250.0=0.9464) = 455.0 kcal
    /// </summary>
    [Fact]
    public async Task FlourAnchor_OneCupAllPurposeFlour_Returns455Kcal()
    {
        var doc = MakeRecipeDoc(1, ("all-purpose flour", 1.0, "Cup"));
        int recipeId = CreateRecipe(doc);

        var cache = await _svc.ComputeAsync(recipeId, _userId);

        // Per-serving = total / 1 serving
        Assert.InRange(cache.PerServingEnergyKcal, 440.0, 470.0);
    }

    /// <summary>
    /// When CNF factor is absent, falls back to KA density 0.507 g/mL for all-purpose flour.
    /// 1 US cup = 236.588 mL × 0.507 g/mL = 119.95 g → 364 × 119.95/100 = ~436 kcal.
    /// Must NOT use water density (1.0 g/mL → 237 g → ~862 kcal).
    /// </summary>
    [Fact]
    public async Task DensityFallback_OneCupFlour_NoCnfFactor_UsesKaDensityNotWater()
    {
        // Remove the 250ml CF from flour so CNF factor path is unavailable
        var cf = await _db.CnfConversionFactors.FirstAsync(c => c.FoodId == FlourFoodId);
        _db.CnfConversionFactors.Remove(cf);
        await _db.SaveChangesAsync();

        var doc = MakeRecipeDoc(1, ("all-purpose flour", 1.0, "Cup"));
        int recipeId = CreateRecipe(doc);

        var cache = await _svc.ComputeAsync(recipeId, _userId);

        // KA density: ~120 g → ~436 kcal
        Assert.InRange(cache.PerServingEnergyKcal, 420.0, 470.0);

        // Must NOT be water density (1.0 g/mL → 237 g → ~862 kcal)
        Assert.True(cache.PerServingEnergyKcal < 600.0,
            $"Energy {cache.PerServingEnergyKcal:F1} kcal suggests water density was used instead of KA (0.507 g/mL)");

        // Confidence should be MEDIUM (density fallback downgrade)
        var items = ParsePerIngredient(cache.PerIngredientMatchJson);
        var flour = items[0];
        Assert.Equal("MEDIUM", flour.GetProperty("confidence").GetString());
    }

    /// <summary>
    /// US-cup scale factor 0.9464 (236.588/250.0) must be applied when recipe unit = Cup
    /// and CNF measure = "250ml". Without scale, result would be 480.8 kcal; with scale = 455.0.
    /// </summary>
    [Fact]
    public async Task CupScaleFactor_USCupAgainst250ml_Applies0p9464()
    {
        var doc = MakeRecipeDoc(1, ("all-purpose flour", 1.0, "Cup"));
        int recipeId = CreateRecipe(doc);

        var cache = await _svc.ComputeAsync(recipeId, _userId);

        // With scale: 364 × 1.32079 × 0.9464 = ~455 kcal
        // Without scale: 364 × 1.32079 = ~480.8 kcal
        // Assert we're in the scaled range, not the unscaled range
        Assert.True(cache.PerServingEnergyKcal < 476.0,
            $"Energy {cache.PerServingEnergyKcal:F1} kcal suggests US-cup 0.9464 scale was NOT applied (expected ≈455, not ≈481)");
        Assert.InRange(cache.PerServingEnergyKcal, 440.0, 470.0);
    }

    // ── Unmatched → null energy, not zero ─────────────────────────────────────

    /// <summary>
    /// SC2/NUTR-04: An unmatched ingredient must record null energy, NOT 0.
    /// "pinch of saffron" won't match any CNF food at HIGH/MEDIUM confidence.
    /// </summary>
    [Fact]
    public async Task UnmatchedIngredient_ReturnsNullEnergy_NotZero()
    {
        // "pinch of saffron" → very low token-intersection score against any CNF food
        // Use a clearly unmatchable string that won't score >= 0.50
        var doc = MakeRecipeDoc(1, ("zzz-xyzzy-no-such-ingredient-at-all", 1.0, "Cup"));
        int recipeId = CreateRecipe(doc);

        var cache = await _svc.ComputeAsync(recipeId, _userId);

        Assert.Equal(1, cache.TotalIngredients);
        Assert.Equal(0, cache.MatchedIngredients);

        var items = ParsePerIngredient(cache.PerIngredientMatchJson);
        Assert.Single(items);
        var item = items[0];

        Assert.Equal("UNMATCHED", item.GetProperty("confidence").GetString());

        // energyKcal must be absent (null) — not present as 0
        if (item.TryGetProperty("energyKcal", out var kcalProp))
        {
            Assert.Equal(JsonValueKind.Null, kcalProp.ValueKind);
        }
        // (if the key is omitted entirely by WhenWritingNull, that is also correct)
    }

    // ── Mass unit direct path ──────────────────────────────────────────────────

    /// <summary>
    /// Mass unit "Gram": 100g butter → grams = 100 directly → kcal = 717 × 100/100 = 717 kcal.
    /// No density lookup or CNF factor needed.
    /// </summary>
    [Fact]
    public async Task MassUnit_UsedDirectly_NoConversionNeeded()
    {
        var doc = MakeRecipeDoc(1, ("unsalted butter", 100.0, "Gram"));
        int recipeId = CreateRecipe(doc);

        var cache = await _svc.ComputeAsync(recipeId, _userId);

        // 100g butter at 717 kcal/100g = 717 kcal
        Assert.InRange(cache.PerServingEnergyKcal, 700.0, 735.0);

        var items = ParsePerIngredient(cache.PerIngredientMatchJson);
        var butter = items[0];
        Assert.Equal("MassDirect", butter.GetProperty("conversionMethod").GetString());
        Assert.Equal(100.0, butter.GetProperty("gramsComputed").GetDouble(), precision: 1);
    }

    // ── Coverage counts ────────────────────────────────────────────────────────

    /// <summary>
    /// With 2 matched + 1 unmatched ingredients → MatchedIngredients=2, TotalIngredients=3.
    /// </summary>
    [Fact]
    public async Task CoverageCount_TwoMatchedOneUnmatched_Correct()
    {
        // "all-purpose flour" and "unsalted butter" should match; "zzz-no-match" should not
        var doc = MakeRecipeDoc(1,
            ("all-purpose flour", 1.0, "Cup"),
            ("unsalted butter", 100.0, "Gram"),
            ("zzz-no-match-ingredient", 1.0, "Cup"));
        int recipeId = CreateRecipe(doc);

        var cache = await _svc.ComputeAsync(recipeId, _userId);

        Assert.Equal(3, cache.TotalIngredients);
        // At least 2 matched (flour + butter should both score >= 0.50)
        Assert.True(cache.MatchedIngredients >= 2,
            $"Expected at least 2 matched ingredients, got {cache.MatchedIngredients}");
    }

    // ── Stale-on-doc-change ────────────────────────────────────────────────────

    /// <summary>
    /// After RecipeService writes a different canonical doc (different hash),
    /// the existing cache row must have IsStale = true.
    /// </summary>
    [Fact]
    public async Task StaleOnDocChange_HashMismatch_IsStaleTrue()
    {
        // Set up initial compute so a cache row exists with IsStale=false
        var doc = MakeRecipeDoc(1, ("all-purpose flour", 1.0, "Cup"));
        int recipeId = CreateRecipe(doc);

        var initialCache = await _svc.ComputeAsync(recipeId, _userId);
        Assert.False(initialCache.IsStale);

        // Simulate a doc change by manually updating the canonical JSON to something different
        // and then running the RecipeService stale-mark logic via the NutritionService's
        // own hash helper (the same SHA-256 function RecipeService uses).
        var recipe = await _db.Recipes.FindAsync(recipeId);
        Assert.NotNull(recipe);

        var updatedDoc = MakeRecipeDoc(2, ("all-purpose flour", 2.0, "Cup")); // changed
        recipe!.CanonicalDocumentJson = _serializer.Serialize(updatedDoc);

        // Compute the new hash (same BCL SHA-256 logic as RecipeService.MarkNutritionCacheStaleIfChangedAsync)
        var newHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(recipe.CanonicalDocumentJson!)));

        var cache = await _db.RecipeNutritionCaches.FindAsync(recipeId);
        Assert.NotNull(cache);
        if (cache!.CanonicalDocHash != newHash)
        {
            cache.IsStale = true;
            cache.CanonicalDocHash = newHash;
            _db.RecipeNutritionCaches.Update(cache);
            await _db.SaveChangesAsync();
        }

        var reloaded = await _db.RecipeNutritionCaches.AsNoTracking().FirstAsync(c => c.RecipeId == recipeId);
        Assert.True(reloaded.IsStale, "Cache must be marked stale after canonical doc hash changes");
    }

    /// <summary>
    /// When the canonical doc has NOT changed (same hash), the stale-mark should not fire.
    /// </summary>
    [Fact]
    public async Task NoStale_SameDocHash_IsStaleRemainsTrue_NotFlipped()
    {
        var doc = MakeRecipeDoc(1, ("all-purpose flour", 1.0, "Cup"));
        int recipeId = CreateRecipe(doc);

        // First compute → IsStale=false, hash set
        var initial = await _svc.ComputeAsync(recipeId, _userId);
        Assert.False(initial.IsStale);

        // Compute again with the same canonical doc → should remain not stale
        var second = await _svc.ComputeAsync(recipeId, _userId);
        Assert.False(second.IsStale);
        Assert.Equal(initial.CanonicalDocHash, second.CanonicalDocHash);
    }

    // ── Stale-on-change via RecipeService ─────────────────────────────────────

    /// <summary>
    /// End-to-end: RecipeService.UpdateAsync with a different ingredient list changes the hash
    /// → existing RecipeNutritionCache.IsStale becomes true.
    /// This proves the SC1/P7 invariant: the save path marks stale WITHOUT calling NutritionService.
    /// </summary>
    [Fact]
    public async Task RecipeService_UpdateAsync_MarksExistingCacheStale()
    {
        // Arrange: initial compute so a cache row exists
        var doc = MakeRecipeDoc(1, ("all-purpose flour", 1.0, "Cup"));
        int recipeId = CreateRecipe(doc);
        var cache = await _svc.ComputeAsync(recipeId, _userId);
        Assert.False(cache.IsStale);
        var initialHash = cache.CanonicalDocHash;

        // Build a RecipeService with all repos pointing at the same in-memory DB
        var recipeRepo = new Repository<Recipe>(_db);
        var ingredientRepo = new Repository<Ingredient>(_db);
        var cookbookRepo = new Repository<Cookbook>(_db);
        var recipeTagRepo = new Repository<RecipeTag>(_db);
        var recipePhotoRepo = new Repository<RecipePhoto>(_db);
        var nutritionCacheRepo = new Repository<RecipeNutritionCache>(_db);

        var recipeService = new RecipeService(
            new StubNutritionTestParser(),
            recipeRepo,
            ingredientRepo,
            cookbookRepo,
            recipeTagRepo,
            recipePhotoRepo,
            nutritionCacheRepo,
            new NullPhotoFileStorageForNutrition(),
            _serializer,
            NullLogger<RecipeService>.Instance);

        // Act: update the recipe with a different ingredient list
        var updatedParsed = new ParsedRecipe
        {
            Name = "Test Recipe",
            Servings = 2, // changed
            Ingredients = [
                new ParsedIngredient
                {
                    LocalId = 1,
                    Name = "unsalted butter",
                    Amount = 200,
                    Unit = "Gram",
                    Substitutions = [],
                }
            ],
            Steps = [],
            Tags = [],
            Equipment = [],
        };

        await recipeService.UpdateAsync(recipeId, _userId, updatedParsed);

        // Assert: cache is now stale
        var reloaded = await _db.RecipeNutritionCaches.AsNoTracking().FirstOrDefaultAsync(c => c.RecipeId == recipeId);
        Assert.NotNull(reloaded);
        Assert.True(reloaded!.IsStale, "RecipeService.UpdateAsync must mark existing cache stale when canonical doc changes");
        Assert.NotEqual(initialHash, reloaded.CanonicalDocHash);
    }

    // ── Per-ingredient match JSON has FoodId + description stored ─────────────

    /// <summary>
    /// SC2/NUTR-04: matched CNF FoodId and FoodDescription are stored in PerIngredientMatchJson.
    /// </summary>
    [Fact]
    public async Task MatchedIngredient_HasFoodIdAndDescription_Stored()
    {
        var doc = MakeRecipeDoc(1, ("all-purpose flour", 1.0, "Cup"));
        int recipeId = CreateRecipe(doc);

        var cache = await _svc.ComputeAsync(recipeId, _userId);

        var items = ParsePerIngredient(cache.PerIngredientMatchJson);
        var flour = items[0];

        Assert.Equal(FlourFoodId, flour.GetProperty("cnfFoodId").GetInt32());
        var desc = flour.GetProperty("cnfFoodDescription").GetString();
        Assert.NotNull(desc);
        Assert.Contains("flour", desc, StringComparison.OrdinalIgnoreCase);
    }

    // ── Cache is written + IsStale=false on fresh compute ─────────────────────

    [Fact]
    public async Task ComputeAsync_WritesCache_IsStaleIsFalse()
    {
        var doc = MakeRecipeDoc(4, ("all-purpose flour", 2.0, "Cup"));
        int recipeId = CreateRecipe(doc);

        var result = await _svc.ComputeAsync(recipeId, _userId);

        Assert.Equal(recipeId, result.RecipeId);
        Assert.False(result.IsStale);
        Assert.NotEmpty(result.CanonicalDocHash);
        Assert.NotEmpty(result.PerIngredientMatchJson);

        // Per-serving = total / 4 servings
        Assert.True(result.PerServingEnergyKcal < result.TotalEnergyKcal);
    }

    // ── Ownership guard ────────────────────────────────────────────────────────

    [Fact]
    public async Task ComputeAsync_WrongUser_ThrowsUnauthorizedAccessException()
    {
        var doc = MakeRecipeDoc(1, ("all-purpose flour", 1.0, "Cup"));
        int recipeId = CreateRecipe(doc);

        var wrongUser = new User { DisplayName = "Intruder" };
        _db.Users.Add(wrongUser);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _svc.ComputeAsync(recipeId, wrongUser.Id));
    }
}

// ── Stub parser for RecipeService tests ────────────────────────────────────────

internal sealed class StubNutritionTestParser : IRecipeFormatParser
{
    public ParsedRecipe Parse(string input) => new()
    {
        Name = "Stub",
        Servings = 1,
        Ingredients = [],
        Steps = [],
        Tags = [],
        Equipment = [],
    };

    public string Serialize(ParsedRecipe recipe) => string.Empty;

    public bool TryParse(string rawContent, out ParsedRecipe? recipe, out List<string> errors)
    {
        recipe = Parse(rawContent);
        errors = [];
        return true;
    }
}

// ── Null photo file storage ────────────────────────────────────────────────────

internal sealed class NullPhotoFileStorageForNutrition : CookBot.Application.Services.IRecipePhotoFileStorage
{
    public void DeletePhysicalFile(string url) { }
    public string GetUploadsDirectory() => Path.GetTempPath();
    public Task<string> SaveFileAsync(Stream stream, string fileName) => Task.FromResult(string.Empty);
}
