using System.Text.Json;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Tests.Nutrition;

/// <summary>
/// NUTR-01 / Phase 15 / Plan 15-03 — integration tests for <c>DatabaseSeeder.SeedCnfDataAsync</c>.
/// Uses an in-memory SQLite DB (EnsureCreated, no EF migrations) and a temp directory
/// for the seed JSON files, mirroring the RecipePhotoBackfillTests pattern.
///
/// Verifies:
///   (a) Rows load and persist correctly (food + conversion factor counts).
///   (b) NormalizedDescription is pre-computed (non-empty, lower-cased).
///   (c) Running the seed twice does NOT duplicate rows (idempotent guard).
///   (d) The flour anchor (FoodId 4484, "Grains, wheat flour, white, all purpose, ...") normalizes
///       to a string containing "flour" and "purpose" with prep/quality modifiers stripped.
///   (e) Macro values round-trip unchanged (verbatim — OGL-Canada T-15-05).
///   (f) Missing seed file → quiet return (T-15-06).
/// </summary>
public class CnfSeedLoadTests : IDisposable
{
    private readonly CookBotDbContext _db;
    private readonly string _tempRoot;

    // ── Fixture data ───────────────────────────────────────────────────────────

    // FoodId 4484 = the canonical flour anchor row (from the real seed).
    // "Grains, wheat flour, white, all purpose, enriched, calcium fortified"
    // EnergyKcalPer100g ≈ 364.
    private static readonly object FlourFoodRow = new
    {
        FoodId = 4484,
        FoodDescription = "Grains, wheat flour, white, all purpose, enriched, calcium fortified",
        FoodGroup = (string?)null,
        EnergyKcalPer100g = 364.0,
        ProteinGPer100g = 10.33,
        FatGPer100g = 0.98,
        CarbGPer100g = 76.31,
    };

    private static readonly object[] FixtureFoods =
    [
        FlourFoodRow,
        new
        {
            FoodId = 99,
            FoodDescription = "Chicken, broiler, chopped, diced, raw",
            FoodGroup = (string?)null,
            EnergyKcalPer100g = 120.0,
            ProteinGPer100g = 22.0,
            FatGPer100g = 3.5,
            CarbGPer100g = 0.0,
        },
    ];

    private static readonly object[] FixtureConversionFactors =
    [
        new { FoodId = 4484, MeasureDescription = "250ml", ConversionFactorValue = 1.32079 },
        new { FoodId = 4484, MeasureDescription = "15ml",  ConversionFactorValue = 0.07924 },
        new { FoodId = 99,   MeasureDescription = "125ml", ConversionFactorValue = 0.55 },
    ];

    public CnfSeedLoadTests()
    {
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        // Write fixture seed files to a temp directory
        _tempRoot = Path.Combine(Path.GetTempPath(), $"cnf-seed-test-{Guid.NewGuid()}");
        var nutritionDir = Path.Combine(_tempRoot, "seeds", "nutrition");
        Directory.CreateDirectory(nutritionDir);

        File.WriteAllText(
            Path.Combine(nutritionDir, "cnf_foods.json"),
            JsonSerializer.Serialize(FixtureFoods));

        File.WriteAllText(
            Path.Combine(nutritionDir, "cnf_conversion_factors.json"),
            JsonSerializer.Serialize(FixtureConversionFactors));
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    // SeedCnfDataAsync is private — invoke via reflection to keep the seeder sealed.
    private static async Task CallSeedCnfDataAsync(CookBotDbContext db, string contentRootPath)
    {
        var method = typeof(DatabaseSeeder)
            .GetMethod("SeedCnfDataAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("SeedCnfDataAsync not found on DatabaseSeeder");

        await (Task)method.Invoke(null, [db, contentRootPath])!;
    }

    // contentRootPath for the seeder is expected one level ABOVE seeds/ (mirrors src/CookBot.Web)
    // The seeder does: Path.Combine(contentRootPath, "..", "seeds", "nutrition", ...)
    // So we pass _tempRoot + "/fake-web-root" so "../seeds/nutrition" resolves correctly.
    private string FakeContentRoot => Path.Combine(_tempRoot, "fake-web-root");

    // ─── (a) Rows load ────────────────────────────────────────────────────────

    [Fact]
    public async Task Rows_Load_FoodsAndConversionFactors()
    {
        Directory.CreateDirectory(FakeContentRoot);
        await CallSeedCnfDataAsync(_db, FakeContentRoot);

        var foodCount = await _db.CnfFoods.CountAsync();
        var cfCount   = await _db.CnfConversionFactors.CountAsync();

        Assert.Equal(2, foodCount);
        Assert.Equal(3, cfCount);
    }

    // ─── (b) NormalizedDescription is pre-computed ────────────────────────────

    [Fact]
    public async Task NormalizedDescription_IsNonEmptyAndLowerCase()
    {
        Directory.CreateDirectory(FakeContentRoot);
        await CallSeedCnfDataAsync(_db, FakeContentRoot);

        var foods = await _db.CnfFoods.AsNoTracking().ToListAsync();
        foreach (var food in foods)
        {
            Assert.NotNull(food.NormalizedDescription);
            Assert.NotEmpty(food.NormalizedDescription);
            // Must be all-lower (IngredientNormalizer.Normalize always lowercases)
            Assert.Equal(food.NormalizedDescription, food.NormalizedDescription.ToLowerInvariant());
        }
    }

    // ─── (c) Idempotent guard: second seed run is a no-op ────────────────────

    [Fact]
    public async Task IdempotentGuard_SecondSeedRun_DoesNotDuplicateRows()
    {
        Directory.CreateDirectory(FakeContentRoot);
        // First run
        await CallSeedCnfDataAsync(_db, FakeContentRoot);
        // Second run — should return immediately without inserting anything
        await CallSeedCnfDataAsync(_db, FakeContentRoot);

        var foodCount = await _db.CnfFoods.CountAsync();
        Assert.Equal(2, foodCount); // still 2, not 4
    }

    // ─── (d) Flour anchor normalizes correctly ────────────────────────────────

    [Fact]
    public async Task FlourAnchor_NormalizesWithFlourAndPurpose_PrepModifiersStripped()
    {
        Directory.CreateDirectory(FakeContentRoot);
        await CallSeedCnfDataAsync(_db, FakeContentRoot);

        var flour = await _db.CnfFoods
            .AsNoTracking()
            .SingleAsync(f => f.FoodId == 4484);

        // IngredientNormalizer strips commas and collapses spaces; "Grains" and "enriched"
        // are preserved; "all purpose" survives (not in deny-list).
        Assert.NotNull(flour.NormalizedDescription);
        Assert.Contains("flour", flour.NormalizedDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("purpose", flour.NormalizedDescription, StringComparison.OrdinalIgnoreCase);

        // Verify prep modifiers from the fixture chicken row are stripped
        var chicken = await _db.CnfFoods.AsNoTracking().SingleAsync(f => f.FoodId == 99);
        Assert.NotNull(chicken.NormalizedDescription);
        // "chopped" and "diced" are in the deny-list — must not appear
        Assert.DoesNotContain("chopped", chicken.NormalizedDescription, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("diced",   chicken.NormalizedDescription, StringComparison.OrdinalIgnoreCase);
        // "raw" is not in the deny-list — should still be present
        Assert.Contains("raw", chicken.NormalizedDescription, StringComparison.OrdinalIgnoreCase);
    }

    // ─── (e) Macro values round-trip verbatim ─────────────────────────────────

    [Fact]
    public async Task MacroValues_RoundTripVerbatim()
    {
        Directory.CreateDirectory(FakeContentRoot);
        await CallSeedCnfDataAsync(_db, FakeContentRoot);

        var flour = await _db.CnfFoods
            .AsNoTracking()
            .SingleAsync(f => f.FoodId == 4484);

        // OGL-Canada forbids modifying values — assert exact round-trip (T-15-05)
        Assert.Equal(364.0,  flour.EnergyKcalPer100g, precision: 6);
        Assert.Equal(10.33,  flour.ProteinGPer100g,   precision: 6);
        Assert.Equal(0.98,   flour.FatGPer100g,        precision: 6);
        Assert.Equal(76.31,  flour.CarbGPer100g,       precision: 6);

        // Conversion factor round-trip
        var cf = await _db.CnfConversionFactors
            .AsNoTracking()
            .FirstAsync(c => c.FoodId == 4484 && c.MeasureDescription == "250ml");
        Assert.Equal(1.32079, cf.ConversionFactorValue, precision: 6);
    }

    // ─── (e) Flour anchor EnergyKcalPer100g ≈ 364 ────────────────────────────

    [Fact]
    public async Task FlourAnchor_EnergyKcalPer100g_IsApprox364()
    {
        Directory.CreateDirectory(FakeContentRoot);
        await CallSeedCnfDataAsync(_db, FakeContentRoot);

        var flour = await _db.CnfFoods
            .AsNoTracking()
            .SingleAsync(f => f.FoodId == 4484);

        Assert.Equal(364.0, flour.EnergyKcalPer100g, precision: 0);
    }

    // ─── (f) Missing seed file → quiet return ─────────────────────────────────

    [Fact]
    public async Task MissingSeedFile_QuietReturn_NoDatabaseRows()
    {
        // Pass a content root where no seeds/nutrition/ directory exists
        var emptyRoot = Path.Combine(Path.GetTempPath(), $"cnf-empty-{Guid.NewGuid()}", "web");
        Directory.CreateDirectory(emptyRoot);
        try
        {
            // Must not throw — T-15-06 mitigation
            await CallSeedCnfDataAsync(_db, emptyRoot);
            var foodCount = await _db.CnfFoods.CountAsync();
            Assert.Equal(0, foodCount); // nothing inserted, no exception
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(emptyRoot)!, recursive: true);
        }
    }
}
