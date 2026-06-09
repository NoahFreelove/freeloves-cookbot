# Phase 15: Nutrition (Offline CNF) — Pattern Map

**Mapped:** 2026-06-08
**Files analyzed:** 21 new/modified files
**Analogs found:** 18 / 21

---

## File Classification

| New / Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---------------------|------|-----------|----------------|---------------|
| `src/CookBot.Domain/Entities/CnfFood.cs` | model | CRUD | `src/CookBot.Domain/Entities/RecipePhoto.cs` | role-match |
| `src/CookBot.Domain/Entities/CnfConversionFactor.cs` | model | CRUD | `src/CookBot.Domain/Entities/RecipePhoto.cs` | role-match |
| `src/CookBot.Domain/Entities/RecipeNutritionCache.cs` | model | CRUD | `src/CookBot.Domain/Entities/Recipe.cs` (FK side: RecipePhoto) | role-match |
| `src/CookBot.Application/DTOs/NutritionInfoDto.cs` | DTO / value-object | transform | `src/CookBot.Application/Services/FractionFormatter.cs` (pure static output type) | partial-match |
| `src/CookBot.Application/Services/IngredientDensityProvider.cs` | service / utility | transform | `src/CookBot.Application/Services/UnitConversionService.cs` | role-match |
| `src/CookBot.Application/Services/IngredientNormalizer.cs` | utility | transform | `src/CookBot.Application/Services/IngredientResolver.cs` | exact |
| `src/CookBot.Application/Services/NutritionService.cs` | service | CRUD + transform | `src/CookBot.Infrastructure/Services/RecipePhotoService.cs` | role-match |
| `src/CookBot.Application/Services/INutritionService.cs` | interface | — | `src/CookBot.Application/Services/IPantryMatchService.cs` | role-match |
| `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs` (MODIFY) | projector | transform | self | exact |
| `src/CookBot.Application/DependencyInjection.cs` (MODIFY) | config | — | self | exact |
| `src/CookBot.Infrastructure/Data/Configurations/CnfFoodConfiguration.cs` | config | CRUD | `src/CookBot.Infrastructure/Data/Configurations/RecipePhotoConfiguration.cs` | exact |
| `src/CookBot.Infrastructure/Data/Configurations/CnfConversionFactorConfiguration.cs` | config | CRUD | `src/CookBot.Infrastructure/Data/Configurations/RecipeIngredientConfiguration.cs` | exact |
| `src/CookBot.Infrastructure/Data/Configurations/RecipeNutritionCacheConfiguration.cs` | config | CRUD | `src/CookBot.Infrastructure/Data/Configurations/RecipePhotoConfiguration.cs` | role-match |
| `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` (MODIFY) | config | — | self | exact |
| `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` (MODIFY) | seed / infra | batch | self | exact |
| `src/CookBot.Infrastructure/Migrations/{ts}_AddNutritionTables.cs` | migration | batch | `src/CookBot.Infrastructure/Migrations/20260607124611_AddRecipePhotosTable.cs` | exact |
| `src/CookBot.Application/Services/RecipeService.cs` (MODIFY — hash + stale-mark) | service | CRUD | self | exact |
| `src/CookBot.Web/Components/Pages/RecipeView.razor` (MODIFY) | component | request-response | self | exact |
| `seeds/nutrition/cnf_foods.json` | seed artifact | batch | `seeds/ingredients.json` | exact |
| `seeds/nutrition/cnf_conversion_factors.json` | seed artifact | batch | `seeds/ingredients.json` | exact |
| `tools/build-cnf-seed.py` | build script | batch | **NO ANALOG — greenfield** | none |
| `tests/CookBot.Tests/Nutrition/NutritionServiceTests.cs` | test | CRUD | `tests/CookBot.Tests/Migration/RecipePhotoBackfillTests.cs` | role-match |
| `tests/CookBot.Tests/Nutrition/JsonLdNutritionProjectorTests.cs` | test | transform | `tests/CookBot.Tests/Recipes/JsonLdRecipeProjectorTests.cs` | exact |

---

## Pattern Assignments

### `src/CookBot.Domain/Entities/CnfFood.cs` (model, CRUD)

**Analog:** `src/CookBot.Domain/Entities/RecipePhoto.cs`

**Entity declaration pattern** (RecipePhoto.cs:1-27):
```csharp
namespace CookBot.Domain.Entities;

/// <summary>
/// CNF food record (NUTR-01 / Phase 15 / D-15-02).
/// FoodId is the CNF FoodCode (user-visible code from the REST API, NOT the internal FoodID).
/// Values stored verbatim — OGL-Canada forbids modifying nutrient values.
/// </summary>
public class CnfFood
{
    public int FoodId { get; set; }           // PK — CNF FoodCode
    public string FoodDescription { get; set; } = string.Empty;
    public string? NormalizedDescription { get; set; } // pre-computed at seed load for runtime match
    public string? FoodGroup { get; set; }
    public double EnergyKcalPer100g { get; set; }
    public double ProteinGPer100g { get; set; }
    public double FatGPer100g { get; set; }
    public double CarbGPer100g { get; set; }

    public ICollection<CnfConversionFactor> ConversionFactors { get; set; } = [];
}
```

**Key conventions from RecipePhoto.cs:**
- `null!` nav props on FK side; `= []` collections on principal side
- No framework refs — pure POCO (RecipePhoto.cs:1 has no `using` directives)
- XML doc on the class, citing the phase/plan/req

---

### `src/CookBot.Domain/Entities/CnfConversionFactor.cs` (model, CRUD)

**Analog:** `src/CookBot.Domain/Entities/RecipePhoto.cs`

**FK child pattern** (RecipePhoto.cs:10-27 — id + FK int + nav prop pattern):
```csharp
public class CnfConversionFactor
{
    public int Id { get; set; }               // surrogate PK
    public int FoodId { get; set; }           // FK → CnfFood.FoodId
    public string MeasureDescription { get; set; } = string.Empty; // e.g. "250ml", "15ml"
    public double ConversionFactorValue { get; set; }  // grams_in_measure / 100

    public CnfFood Food { get; set; } = null!;
}
```

---

### `src/CookBot.Domain/Entities/RecipeNutritionCache.cs` (model, CRUD)

**Analog:** `src/CookBot.Domain/Entities/Recipe.cs` (for the TEXT/JSON column pattern) + `RecipePhoto.cs` (for FK-to-Recipe + cascade pattern)

**1:1 FK + TEXT column pattern** (Recipe.cs:17 for `HasColumnType("TEXT")`, RecipePhoto.cs for FK cascade shape):
```csharp
public class RecipeNutritionCache
{
    public int RecipeId { get; set; }         // PK + FK → Recipe (1:1)
    public string CanonicalDocHash { get; set; } = string.Empty;
    public bool IsStale { get; set; }
    public double TotalEnergyKcal { get; set; }
    public double TotalProteinG { get; set; }
    public double TotalFatG { get; set; }
    public double TotalCarbG { get; set; }
    public int? Servings { get; set; }
    public double PerServingEnergyKcal { get; set; }
    public double PerServingProteinG { get; set; }
    public double PerServingFatG { get; set; }
    public double PerServingCarbG { get; set; }
    public int MatchedIngredients { get; set; }
    public int TotalIngredients { get; set; }
    /// <summary>JSON: [{name, cnfFoodId, cnfDesc, confidence, kcal, proteinG, fatG, carbG}]</summary>
    public string PerIngredientMatchJson { get; set; } = string.Empty;
    public DateTime ComputedAt { get; set; }

    public Recipe Recipe { get; set; } = null!;
}
```

---

### `src/CookBot.Application/DTOs/NutritionInfoDto.cs` (DTO, transform)

**Analog:** No direct DTO analog exists as a record — but the project uses `sealed record` for value objects in the Application layer. The closest is the anonymous DTO approach in RecipePhotoService.

**Sealed record pattern per RESEARCH.md and project conventions:**
```csharp
namespace CookBot.Application.DTOs;

/// <summary>
/// Per-serving nutrition value object passed to JsonLdRecipeProjector.Project (D-15-13 / NUTR-06).
/// Pure value type — no EF, no DI, no data-service access.
/// </summary>
public sealed record NutritionInfoDto(
    double CaloriesPerServing,
    double ProteinGPerServing,
    double FatGPerServing,
    double CarbGPerServing
);
```

---

### `src/CookBot.Application/Services/IngredientNormalizer.cs` (utility, transform)

**Analog:** `src/CookBot.Application/Services/IngredientResolver.cs` (EXACT match — same static normalize pattern)

**Core pattern** (IngredientResolver.cs:1-17 — full file):
```csharp
using System.Text.RegularExpressions;

namespace CookBot.Application.Services;

public static class IngredientNormalizer
{
    // Deny-list: prep/quality/instruction modifiers that do NOT change nutrition (D-15-05).
    // Keep: unsalted, salted, skinless, lowfat, low-fat, whole, light, heavy.
    private static readonly string[] DenyList = [
        "chopped", "minced", "diced", "sliced", "shredded", "grated", "ground",
        "sifted", "packed", "finely", "roughly", "freshly",
        "room-temperature", "room temperature", "cold", "warm",
        "good-quality", "good", "fine", "coarse", "large", "small", "medium", "ripe",
        "organic", "to taste", "optional", "divided", "for garnish", "plus more",
    ];

    public static string Normalize(string name)
    {
        // Mirror IngredientResolver.Normalize (IngredientResolver.cs:10-16)
        var lower = name.ToLowerInvariant().Trim();
        lower = Regex.Replace(lower, @"[-_]", " ");
        lower = Regex.Replace(lower, @"\s+", " ");
        // Strip deny-list tokens (full-word only, not substring)
        foreach (var token in DenyList)
            lower = Regex.Replace(lower, $@"\b{Regex.Escape(token)}\b", " ");
        lower = Regex.Replace(lower, @"\s+", " ").Trim();
        return lower;
    }
}
```

**Key:** `IngredientResolver.cs` (lines 10-16) shows the exact `ToLowerInvariant` + hyphen-strip + whitespace-collapse pipeline. `IngredientNormalizer` extends it with the deny-list loop using `\b` word-boundary anchors (not substring replace).

---

### `src/CookBot.Application/Services/IngredientDensityProvider.cs` (service/utility, transform)

**Analog:** `src/CookBot.Application/Services/UnitConversionService.cs` (hardcoded dictionary pattern + `IUnitConverter` interface shape)

**Static dictionary + method pattern** (UnitConversionService.cs:9-28):
```csharp
namespace CookBot.Application.Services;

/// <summary>
/// Curated per-ingredient density fallback table (g/mL) for volume→mass conversion
/// when CNF has no Conversion Factor for the matched food/measure (D-15-07/09 / NUTR-03).
/// Sources: King Arthur Baking weight chart + FAO/INFOODS Density Database v2.0.
/// NOT a replacement for CNF factors — only activates for CNF-uncovered food/measure pairs.
/// </summary>
public class IngredientDensityProvider
{
    // g/mL values — KA = King Arthur Baking; FAO = FAO/INFOODS DB v2.0
    private static readonly Dictionary<string, double> _densities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["all-purpose flour"] = 0.507,   // KA HIGH
        ["bread flour"]       = 0.507,   // KA HIGH
        ["whole wheat flour"] = 0.478,   // KA HIGH
        // ... 20+ entries per RESEARCH.md §Research Target 3 table
    };

    /// <summary>
    /// Returns g/mL for the normalized ingredient name, or null when unknown.
    /// </summary>
    public double? GetDensityGPerMl(string normalizedName) =>
        _densities.TryGetValue(normalizedName, out var d) ? d : null;
}
```

**Key conventions from UnitConversionService.cs:**
- `private static readonly Dictionary<…>` with inline initialization
- `new(StringComparer.OrdinalIgnoreCase)` for case-insensitive key match (UnitConversionService uses enum keys; density provider uses string keys)
- Simple nullable return (like `Convert` returns `double?`)

---

### `src/CookBot.Application/Services/NutritionService.cs` + `INutritionService.cs` (service, CRUD + transform)

**Analog:** `src/CookBot.Infrastructure/Services/RecipePhotoService.cs` (auth check, DbContext injection, ownership guard, service-to-service coord)

**Constructor + ownership guard pattern** (RecipePhotoService.cs:22-51, 300-314):
```csharp
public class NutritionService : INutritionService
{
    private readonly CookBotDbContext _db;
    private readonly IngredientDensityProvider _densityProvider;
    private readonly IUnitConverter _unitConverter;
    private readonly ILogger<NutritionService> _logger;

    public NutritionService(
        CookBotDbContext db,
        IngredientDensityProvider densityProvider,
        IUnitConverter unitConverter,
        ILogger<NutritionService> logger)
    { ... }

    // Never called from RecipeService — only from an explicit user CTA (D-15-10 / P7).
    public async Task<RecipeNutritionCache> ComputeAsync(int recipeId, int userId)
    {
        // Mirror RecipePhotoService.AssertOwnershipAsync
        var recipe = await _db.Recipes.FindAsync(recipeId)
            ?? throw new InvalidOperationException("Recipe not found.");
        var cookbook = await _db.Cookbooks.FindAsync(recipe.CookbookId)
            ?? throw new InvalidOperationException("Cookbook not found.");
        if (cookbook.UserId != userId)
            throw new UnauthorizedAccessException("You do not own this cookbook.");
        ...
    }
}
```

**Query pattern** (RecipePhotoService.cs:53-63 — AsNoTracking, Where, OrderBy):
```csharp
public async Task<RecipeNutritionCache?> GetCacheAsync(int recipeId, int userId)
{
    await AssertOwnershipAsync(recipeId, userId);
    return await _db.RecipeNutritionCaches
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.RecipeId == recipeId);
}
```

**Interface pattern** (IPantryMatchService.cs as model):
```csharp
public interface INutritionService
{
    Task<RecipeNutritionCache?> GetCacheAsync(int recipeId, int userId);
    Task<RecipeNutritionCache> ComputeAsync(int recipeId, int userId);
    Task MarkStaleAsync(int recipeId, string newCanonicalHash);  // called by RecipeService
}
```

---

### `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs` (MODIFY — add optional `nutrition?` param)

**Analog:** self (exact — add one optional parameter following the existing `absoluteImageUrl` conditional pattern)

**Existing optional-param + conditional dict-entry pattern** (JsonLdRecipeProjector.cs:69, 131):
```csharp
// EXISTING signature (line 69):
public static string Project(RecipeDocument doc, string? absoluteImageUrl)

// NEW signature — add optional third param:
public static string Project(RecipeDocument doc, string? absoluteImageUrl, NutritionInfoDto? nutrition = null)

// Existing conditional entry pattern (line 131) — copy exactly:
if (absoluteImageUrl is not null)        model["image"] = absoluteImageUrl;
// ... existing entries unchanged ...
if (author is not null)                  model["author"] = author;

// NEW — add after "author" entry, before the final serialize:
if (nutrition is not null)
{
    model["nutrition"] = new Dictionary<string, string>
    {
        ["@type"]               = "NutritionInformation",
        ["calories"]            = $"{nutrition.CaloriesPerServing:0} calories",
        ["proteinContent"]      = $"{nutrition.ProteinGPerServing:0.#} g",
        ["carbohydrateContent"] = $"{nutrition.CarbGPerServing:0.#} g",
        ["fatContent"]          = $"{nutrition.FatGPerServing:0.#} g",
    };
}
// NEVER emit: aggregateRating, review, datePublished  (line 143 — keep unchanged)
```

**Critical — do NOT give the projector DI or data-service access.** The doc-comment (lines 8-15) states this invariant explicitly. `RecipeView` constructs `NutritionInfoDto` from `RecipeNutritionCache` and passes it in.

**Golden test impact:** The existing `FullDocument_ProducesExpectedJsonLd` snapshot (`tests/CookBot.Tests/Snapshots/JsonLdRecipeProjectorTests.FullDocument_ProducesExpectedJsonLd.verified.txt`) must remain unchanged (nutrition absent → no `"nutrition"` key). Add a new golden test `FullDocumentWithNutrition_ProducesExpectedJsonLd` that passes a `NutritionInfoDto` and verifies the `NutritionInformation` block appears.

---

### `src/CookBot.Infrastructure/Data/Configurations/CnfFoodConfiguration.cs` (config, CRUD)

**Analog:** `src/CookBot.Infrastructure/Data/Configurations/RecipePhotoConfiguration.cs` (exact pattern — IEntityTypeConfiguration, discovered by `ApplyConfigurationsFromAssembly`)

**Full pattern** (RecipePhotoConfiguration.cs:1-42):
```csharp
using CookBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CookBot.Infrastructure.Data.Configurations;

/// <summary>
/// EF fluent configuration for <see cref="CnfFood"/> (NUTR-01 / Phase 15 / Plan 15-01).
/// Discovered automatically by ApplyConfigurationsFromAssembly — no change to CookBotDbContext.cs.
/// </summary>
public class CnfFoodConfiguration : IEntityTypeConfiguration<CnfFood>
{
    public void Configure(EntityTypeBuilder<CnfFood> builder)
    {
        builder.HasKey(f => f.FoodId);
        builder.Property(f => f.FoodDescription).HasMaxLength(300).IsRequired();
        builder.Property(f => f.NormalizedDescription).HasMaxLength(300);
        builder.Property(f => f.FoodGroup).HasMaxLength(100);
        // Index for name-matching queries
        builder.HasIndex(f => f.NormalizedDescription);

        builder.HasMany(f => f.ConversionFactors)
            .WithOne(cf => cf.Food)
            .HasForeignKey(cf => cf.FoodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**Key from RecipePhotoConfiguration.cs:**
- `builder.HasKey` on line 1 of Configure
- `HasMaxLength(...).IsRequired()` inline chain (lines 19-20)
- `HasDefaultValue` for optional boolean columns (line 29 — not needed here but shows the pattern)
- `HasIndex` for query-path columns (line 34)
- FK configured on child entity, not on parent (line 38-41 comment on RecipePhotoConfiguration.cs:37-41)

---

### `src/CookBot.Infrastructure/Data/Configurations/CnfConversionFactorConfiguration.cs` (config, CRUD)

**Analog:** `src/CookBot.Infrastructure/Data/Configurations/RecipeIngredientConfiguration.cs`

**Composite-index + FK pattern** (RecipeIngredientConfiguration.cs:1-24):
```csharp
public class CnfConversionFactorConfiguration : IEntityTypeConfiguration<CnfConversionFactor>
{
    public void Configure(EntityTypeBuilder<CnfConversionFactor> builder)
    {
        builder.HasKey(cf => cf.Id);
        // Index for GetFactors(foodId) query
        builder.HasIndex(cf => cf.FoodId);
        builder.Property(cf => cf.MeasureDescription).HasMaxLength(100).IsRequired();
        // FK configured on child side — CnfFoodConfiguration.cs configures the HasMany side
    }
}
```

---

### `src/CookBot.Infrastructure/Data/Configurations/RecipeNutritionCacheConfiguration.cs` (config, CRUD)

**Analog:** `src/CookBot.Infrastructure/Data/Configurations/RecipePhotoConfiguration.cs` (cascade FK) + `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs` (TEXT column pattern)

**1:1 FK + TEXT column pattern** (RecipeConfiguration.cs:14-16 for TEXT; RecipePhotoConfiguration.cs:37-41 for cascade):
```csharp
public class RecipeNutritionCacheConfiguration : IEntityTypeConfiguration<RecipeNutritionCache>
{
    public void Configure(EntityTypeBuilder<RecipeNutritionCache> builder)
    {
        builder.HasKey(c => c.RecipeId);

        // TEXT for the per-ingredient match JSON (mirrors Recipe.CanonicalDocumentJson — RecipeConfiguration.cs:14-16)
        builder.Property(c => c.PerIngredientMatchJson).HasColumnType("TEXT");
        builder.Property(c => c.CanonicalDocHash).HasMaxLength(64).IsRequired(); // SHA-256 hex = 64 chars

        // 1:1 cascade — mirrors RecipePhotoConfiguration.cs:37-41 shape but uses WithOne()
        builder.HasOne(c => c.Recipe)
            .WithOne()
            .HasForeignKey<RecipeNutritionCache>(c => c.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

### `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` (MODIFY — add 3 DbSets)

**Analog:** self (exact — add 3 DbSet lines following the GALLERY-01 comment pattern)

**DbSet addition pattern** (CookBotDbContext.cs:33-34):
```csharp
// GALLERY-01 / Phase 14 / Plan 14-01 — multi-photo gallery backing store.
public DbSet<RecipePhoto> RecipePhotos => Set<RecipePhoto>();

// NUTR-01 / Phase 15 / Plan 15-01 — CNF seed tables (read-only after seed) + nutrition cache.
public DbSet<CnfFood> CnfFoods => Set<CnfFood>();
public DbSet<CnfConversionFactor> CnfConversionFactors => Set<CnfConversionFactor>();
public DbSet<RecipeNutritionCache> RecipeNutritionCaches => Set<RecipeNutritionCache>();
```

`OnModelCreating` already calls `ApplyConfigurationsFromAssembly` (line 43) — no change needed there.

---

### `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` (MODIFY — add CNF seed load)

**Analog:** self — copy the `LoadIngredientsFromSeedFile` private method pattern exactly (lines 201-234)

**Seed-load pattern** (DatabaseSeeder.cs:201-234):
```csharp
// ── Existing ingredient seed load (lines 201-234) ──────────────────────────
private static async Task<List<Ingredient>> LoadIngredientsFromSeedFile(string contentRootPath)
{
    var seedPath = Path.GetFullPath(Path.Combine(contentRootPath, "..", "seeds", "ingredients.json"));
    if (!File.Exists(seedPath)) return [];
    var json = await File.ReadAllTextAsync(seedPath);
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var seedItems = JsonSerializer.Deserialize<List<SeedIngredient>>(json, options) ?? [];
    // ... transform + return
}
```

**New CNF seed methods (follow exactly the same shape):**
```csharp
private static async Task SeedCnfDataAsync(CookBotDbContext context, string contentRootPath)
{
    // Idempotent guard — mirrors the `context.Users.AnyAsync()` early-return at line 120
    if (await context.CnfFoods.AnyAsync()) return;

    var foodsPath = Path.GetFullPath(
        Path.Combine(contentRootPath, "..", "seeds", "nutrition", "cnf_foods.json"));
    if (!File.Exists(foodsPath)) return;

    var json = await File.ReadAllTextAsync(foodsPath);
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var rows = JsonSerializer.Deserialize<List<CnfFoodSeedRow>>(json, options) ?? [];

    foreach (var row in rows)
    {
        context.CnfFoods.Add(new CnfFood
        {
            FoodId              = row.FoodId,
            FoodDescription     = row.FoodDescription,
            NormalizedDescription = IngredientNormalizer.Normalize(row.FoodDescription),
            EnergyKcalPer100g   = row.EnergyKcalPer100g,
            ProteinGPer100g     = row.ProteinGPer100g,
            FatGPer100g         = row.FatGPer100g,
            CarbGPer100g        = row.CarbGPer100g,
        });
    }
    await context.SaveChangesAsync();

    // Load conversion factors in a second pass (after CnfFood PKs are committed)
    var cfPath = Path.GetFullPath(
        Path.Combine(contentRootPath, "..", "seeds", "nutrition", "cnf_conversion_factors.json"));
    if (!File.Exists(cfPath)) return;

    var cfJson = await File.ReadAllTextAsync(cfPath);
    var cfRows = JsonSerializer.Deserialize<List<CnfCfSeedRow>>(cfJson, options) ?? [];
    foreach (var cf in cfRows)
        context.CnfConversionFactors.Add(new CnfConversionFactor { ... });
    await context.SaveChangesAsync();
}
```

Call `SeedCnfDataAsync` from `SeedAsync` AFTER the `MigrateAsync()` call (line 60) and BEFORE the early-return for existing users (line 120), so it always runs on first startup regardless of whether a default user exists.

---

### `src/CookBot.Infrastructure/Migrations/{ts}_AddNutritionTables.cs` (migration, batch)

**Analog:** `src/CookBot.Infrastructure/Migrations/20260607124611_AddRecipePhotosTable.cs` (EXACT pattern)

**Migration Up() pattern** (AddRecipePhotosTable.cs:9-58):
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateTable(
        name: "CnfFoods",
        columns: table => new
        {
            FoodId              = table.Column<int>(type: "INTEGER", nullable: false),
            FoodDescription     = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
            NormalizedDescription = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
            FoodGroup           = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
            EnergyKcalPer100g   = table.Column<double>(type: "REAL", nullable: false),
            ProteinGPer100g     = table.Column<double>(type: "REAL", nullable: false),
            FatGPer100g         = table.Column<double>(type: "REAL", nullable: false),
            CarbGPer100g        = table.Column<double>(type: "REAL", nullable: false),
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_CnfFoods", x => x.FoodId);
        });
    // ... CreateTable for CnfConversionFactors and RecipeNutritionCaches ...
    // ... CreateIndex calls ...
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // Drop in reverse FK dependency order
    migrationBuilder.DropTable(name: "RecipeNutritionCaches");
    migrationBuilder.DropTable(name: "CnfConversionFactors");
    migrationBuilder.DropTable(name: "CnfFoods");
}
```

**Important:** Generate via `dotnet ef migrations add AddNutritionTables --project src/CookBot.Infrastructure --startup-project src/CookBot.Web`; do not hand-write the designer `.cs` snapshot. No backfill SQL is needed (these are new, empty-at-birth tables populated by the seeder).

---

### `src/CookBot.Application/Services/RecipeService.cs` (MODIFY — hash + stale-mark)

**Analog:** self — add SHA-256 hash computation and `RecipeNutritionCache.IsStale = true` after the `CanonicalDocumentJson` write, in both `CreateAsync` and `UpdateAsync`.

**Existing CanonicalDocumentJson write pattern** (RecipeService.cs:60-66):
```csharp
// EXISTING: canonical write on CreateAsync
var canonicalDoc = /* build from parsed */;
recipe.CanonicalDocumentJson = _canonicalSerializer.Serialize(canonicalDoc);
```

**NEW: add hash + stale-mark immediately after each CanonicalDocumentJson write:**
```csharp
// NUTR-02 / D-15-12 — compute content hash and mark existing nutrition cache stale.
// NEVER call NutritionService here (P7 guard: save must not block on nutrition).
var canonicalBytes = System.Text.Encoding.UTF8.GetBytes(recipe.CanonicalDocumentJson!);
var newHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(canonicalBytes));

var existingCache = await _db.RecipeNutritionCaches
    .FirstOrDefaultAsync(c => c.RecipeId == recipe.Id);
if (existingCache is not null && existingCache.CanonicalDocHash != newHash)
{
    existingCache.IsStale = true;
    existingCache.CanonicalDocHash = newHash;
    // SaveChangesAsync is called below — no extra round-trip needed
}
```

`System.Security.Cryptography.SHA256.HashData` is BCL (.NET 5+) — no new package.

---

### `src/CookBot.Application/DependencyInjection.cs` (MODIFY — register new services)

**Analog:** self (exact — follow the existing comment-per-phase pattern at lines 21-44)

**Registration pattern** (DependencyInjection.cs:14-44):
```csharp
// Phase 15 / NUTR-01..06 — offline CNF nutrition services.
// IngredientDensityProvider: stateless (hardcoded dict) → Singleton.
// IngredientNormalizer: static class, no registration needed.
// NutritionService: Scoped (depends on CookBotDbContext which is Scoped).
services.AddSingleton<IngredientDensityProvider>();
services.AddScoped<INutritionService, NutritionService>();
```

**NutritionService** injects `CookBotDbContext` directly (same pattern as `RecipePhotoService` in Infrastructure). Because `NutritionService` lives in `CookBot.Application`, it would need to take a DB abstraction or be moved to Infrastructure like `RecipePhotoService`. Follow the `RecipePhotoService` precedent: place `NutritionService.cs` in `src/CookBot.Infrastructure/Services/` and register from `src/CookBot.Infrastructure/DependencyInjection.cs`.

---

### `src/CookBot.Web/Components/Pages/RecipeView.razor` (MODIFY — nutrition panel + CTA)

**Analog:** self — follow three existing patterns exactly:

**1. Service injection pattern** (RecipeView.razor:12-22 — `@inject` directives):
```razor
@inject NutritionService NutritionSvc   // or INutritionService
```

**2. State fields pattern** (RecipeView.razor:416-455 — private field block in `@code`):
```csharp
// Phase 15 / NUTR-04 — nutrition panel state
private RecipeNutritionCache? _nutritionCache;
private bool _nutritionCalculating;
private bool _nutritionError;
private bool _showAllMatches;
private bool _nutritionPerServing = true; // default per-serving (D-15-15)
```

**3. JSON-LD call site** (RecipeView.razor:569 — the single call to `JsonLdRecipeProjector.Project`):
```csharp
// EXISTING (line 569):
_jsonLd = JsonLdRecipeProjector.Project(doc, absoluteImageUrl);

// MODIFIED (pass nutrition when cache exists and is current):
NutritionInfoDto? nutritionDto = null;
if (_nutritionCache is { IsStale: false })
{
    nutritionDto = new NutritionInfoDto(
        _nutritionCache.PerServingEnergyKcal,
        _nutritionCache.PerServingProteinG,
        _nutritionCache.PerServingFatG,
        _nutritionCache.PerServingCarbG);
}
_jsonLd = JsonLdRecipeProjector.Project(doc, absoluteImageUrl, nutritionDto);
```

**4. Panel placement** (RecipeView.razor:358 — after `</article>` close, per UI-SPEC):
```razor
@* Nutrition panel renders INSIDE <article class="recipe-article">,
   AFTER recipe-body-grid, at margin-top:48px (2xl) — UI-SPEC §Panel Placement *@
<section aria-label="Estimated nutrition" style="margin-top:48px;">
    @* 5-state machine: null cache → State 1; IsStale=true → State 3;
       calculating → State 4; error → State 5; IsStale=false → State 2 *@
</section>
```

**5. Disclaimer pattern** (RecipePhotoGalleryManager.razor:225-231 — exact DOM shape to copy):
```razor
@* ── Nutrition disclaimer — always visible, never conditional ──────── *@
<div role="note"
     aria-label="Nutrition data notice"
     style="font-size:12.5px;color:var(--ink-3);line-height:1.4;margin-top:16px;">
    Estimated nutrition — not suitable for medical dietary planning.
    Data: Health Canada, Canadian Nutrient File (2015).
</div>
```

**6. Stat tile pattern for macro numbers** (RecipeView.razor:113-133 — hero stat row reuse):
```razor
@* Hero stat row uses this exact num/eyebrow pattern — copy for macro tiles: *@
<div class="eyebrow" style="font-size:10px;">Energy</div>
<div class="num" style="font-size:28px;font-weight:600;margin-top:4px;color:var(--accent);">
    @(_nutritionPerServing ? _nutritionCache!.PerServingEnergyKcal.ToString("0") : _nutritionCache!.TotalEnergyKcal.ToString("0"))
    <span style="font-size:12.5px;color:var(--ink-3);margin-left:4px;">kcal</span>
</div>
```

**7. CbButton variant pattern** (RecipeView.razor:396-406 — `Variant=Accent` / `Variant=Ghost` usage):
```razor
@* State 1/4: primary Calculate CTA *@
<CbButton Variant="CbButton.CbButtonVariant.Accent" OnClick="CalculateNutrition">
    @if (_nutritionCalculating)
    {
        <span class="cb-pulse">Calculating…</span>
    }
    else
    {
        Calculate nutrition
    }
</CbButton>

@* State 3: stale recalculate CTA (Ghost, not Accent — D-15-12 / UI-SPEC §State 3) *@
<CbButton Variant="CbButton.CbButtonVariant.Ghost" OnClick="CalculateNutrition">
    Recalculate nutrition
</CbButton>
```

---

### `seeds/nutrition/cnf_foods.json` + `seeds/nutrition/cnf_conversion_factors.json` (seed artifact, batch)

**Analog:** `seeds/ingredients.json` (exact — JSON array, one object per row, camelCase or PascalCase keys per `PropertyNameCaseInsensitive = true`)

**File structure pattern** (ingredients.json shape + DatabaseSeeder.cs:210-211 `PropertyNameCaseInsensitive = true`):
```json
[
  {
    "FoodId": 4484,
    "FoodDescription": "Grains, wheat flour, white, all purpose, enriched, calcium fortified",
    "EnergyKcalPer100g": 364.0,
    "ProteinGPer100g": 10.33,
    "FatGPer100g": 0.98,
    "CarbGPer100g": 76.31
  },
  ...
]
```

```json
[
  {
    "FoodId": 4484,
    "MeasureDescription": "250ml",
    "ConversionFactorValue": 1.32079
  },
  ...
]
```

**Size:** ~680 KB + ~815 KB respectively (~30× the 51.7 KB `ingredients.json`) — within normal repo limits.

---

### `tools/build-cnf-seed.py` (build script, batch)

**No analog exists in the codebase.** This is a GREENFIELD one-time offline script. It calls three CNF REST API endpoints, merges the results, and writes `seeds/nutrition/cnf_foods.json` + `seeds/nutrition/cnf_conversion_factors.json`.

Reference shape from RESEARCH.md §Research Target 5:
- Fetch `https://food-nutrition.canada.ca/api/canadian-nutrient-file/food/?lang=en&type=json`
- Fetch `https://food-nutrition.canada.ca/api/canadian-nutrient-file/nutrientamount/?lang=en&type=json` — filter to NutrientNameIDs 203, 204, 205, 208
- Fetch `https://food-nutrition.canada.ca/api/canadian-nutrient-file/servingsize/?lang=en&type=json`
- Merge on `food_code` (NOT `food_id` — see PITFALL in RESEARCH.md §Anti-Patterns)
- Skip foods with no energy record (NutrientNameID 208 absent)
- Write two JSON files

The planner should describe this as a Python script committed to `tools/` with no runtime dependency in the .NET app.

---

### `tests/CookBot.Tests/Nutrition/NutritionServiceTests.cs` (test, CRUD + transform)

**Analog:** `tests/CookBot.Tests/Migration/RecipePhotoBackfillTests.cs` (in-memory SQLite EF setup + owned-DB-context pattern)

**In-memory SQLite test setup pattern** (RecipePhotoBackfillTests.cs:31-41):
```csharp
public NutritionServiceTests()
{
    var options = new DbContextOptionsBuilder<CookBotDbContext>()
        .UseSqlite("DataSource=:memory:")
        .Options;
    _db = new CookBotDbContext(options);
    _db.Database.OpenConnection();
    _db.Database.EnsureCreated();
}
```

**Required tests (CONTEXT.md Claude's Discretion + SC3):**

| Test | Assert |
|------|--------|
| `FlourAnchor_OneCupAllPurposeFlour_Returns455Kcal` | CNF factor path → 455 kcal ±1 |
| `DensityFallback_OneCupAllPurposeFlour_Returns455Kcal` | density path (when CF missing) → KA density → ≈435-455 kcal |
| `UnmatchedIngredient_ReturnsNullEnergy_NotZero` | confidence=UNMATCHED → energyKcal is null, NOT 0 |
| `LowConfidenceMatch_FlaggedWithApproxPrefix` | score 0.5–0.79 → Confidence.Medium |
| `MassUnit_UsedDirectly_NoConversionNeeded` | ingredient unit = gram → no density, no CF needed |
| `StaleOnDocChange_HashMismatch_IsStaleTrue` | hash change → IsStale=true |
| `NoStaleOnSameDoc_HashMatch_IsStaleNotSet` | same hash → IsStale unchanged |
| `CoverageCount_Correct` | 11 matched / 13 total → MatchedIngredients=11, TotalIngredients=13 |
| + ≥12 more density/conversion tests per CONTEXT.md D-15-08 | 20+ common ingredients |

---

### `tests/CookBot.Tests/Nutrition/JsonLdNutritionProjectorTests.cs` (test, transform)

**Analog:** `tests/CookBot.Tests/Recipes/JsonLdRecipeProjectorTests.cs` (EXACT — same static call pattern, same `JsonDocument.Parse` assertion style)

**Test pattern** (JsonLdRecipeProjectorTests.cs:22-33 — omit-when-null style):
```csharp
[Fact]
public void Nutrition_OmittedWhenNull()
{
    var doc = new RecipeDocument { Version = 4, Name = "Simple Soup" };
    var output = JsonLdRecipeProjector.Project(doc, absoluteImageUrl: null, nutrition: null);
    using var parsed = JsonDocument.Parse(output);
    Assert.False(parsed.RootElement.TryGetProperty("nutrition", out _),
        "nutrition should be absent when NutritionInfoDto is null");
}

[Fact]
public Task WithNutrition_ProducesExpectedJsonLd()
{
    var doc = MakeFullDocument(); // copy from JsonLdRecipeProjectorTests.MakeFullDocument()
    var nutrition = new NutritionInfoDto(455, 12.9, 1.2, 95.4);
    var actual = JsonLdRecipeProjector.Project(doc, "https://host/img.jpg", nutrition);
    return Verifier.Verify(actual); // golden snapshot in Snapshots/
}

[Fact]
public void NutritionValues_CorrectFormat()
{
    var doc = new RecipeDocument { Version = 4, Name = "Test Recipe", Servings = 4 };
    var nutrition = new NutritionInfoDto(CaloriesPerServing: 455.6, ProteinGPerServing: 12.9,
                                          FatGPerServing: 1.2, CarbGPerServing: 95.4);
    var output = JsonLdRecipeProjector.Project(doc, null, nutrition);
    using var parsed = JsonDocument.Parse(output);
    var n = parsed.RootElement.GetProperty("nutrition");
    Assert.Equal("NutritionInformation", n.GetProperty("@type").GetString());
    Assert.Equal("456 calories", n.GetProperty("calories").GetString()); // rounds to 0 dp
    Assert.Equal("12.9 g", n.GetProperty("proteinContent").GetString()); // 1 dp
}

// Regression: existing FullDocument_ProducesExpectedJsonLd golden must be UNCHANGED
// (nutrition absent → no "nutrition" key → snapshot byte-for-byte identical)
```

---

## Shared Patterns

### EF Configuration Discovery (apply to all 3 new configurations)
**Source:** `src/CookBot.Infrastructure/Data/CookBotDbContext.cs:43`
```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(CookBotDbContext).Assembly);
```
All three new `IEntityTypeConfiguration<T>` classes are auto-discovered. No change to `OnModelCreating`.

### Startup Migration + Seed Sequence
**Source:** `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs:50-60`
```csharp
var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
if (pending.Count > 0) { /* backup */ }
await context.Database.MigrateAsync();   // applies AddNutritionTables
// ... then SeedCnfDataAsync() runs (new) ...
// ... then existing user/ingredient seed logic ...
```

### Idempotent Seed Guard
**Source:** `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs:120`
```csharp
if (await context.Users.AnyAsync()) return; // ingredient guard pattern
// → CNF guard follows same shape:
if (await context.CnfFoods.AnyAsync()) return;
```

### Ownership Check
**Source:** `src/CookBot.Infrastructure/Services/RecipePhotoService.cs:300-314`
```csharp
private async Task AssertOwnershipAsync(int recipeId, int userId)
{
    var recipe = await _db.Recipes.FindAsync(recipeId)
        ?? throw new InvalidOperationException("Recipe not found.");
    var cookbook = await _cookbookRepo.GetByIdAsync(recipe.CookbookId)
        ?? throw new InvalidOperationException("Cookbook not found.");
    if (cookbook.UserId != userId)
        throw new UnauthorizedAccessException("You do not own this cookbook.");
}
```
Copy verbatim into `NutritionService`.

### `role="note"` Disclaimer DOM Block
**Source:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor:225-231`
```razor
<div role="note"
     aria-label="Copyright notice"
     style="font-size:11.5px;color:var(--ink-3);line-height:1.4;margin-top:8px;">
    Only add photos you have the right to use. ...
</div>
```
Phase 15 uses the same DOM shape with `aria-label="Nutrition data notice"`, `font-size:12.5px`, `margin-top:16px`, and the verbatim D-15-17 copy.

### `role="status" aria-live="polite"` Banner
**Source:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoGalleryManager.razor:173`, `:190` (status/progress inline banners)
```razor
<div role="status" aria-live="polite" style="font-size:12.5px;color:var(--ink-3);margin-top:8px;">
    ...
</div>
```
Used for stale-state banner (State 3) and error banner (State 5) in the nutrition panel.

### `.num` Tabular Numerals + Stat Tile
**Source:** `src/CookBot.Web/Components/Pages/RecipeView.razor:114-133` (hero stat row)
```razor
<div class="num" style="font-size:22px;font-weight:600;margin-top:4px;">
    @_doc.Servings
</div>
```
Nutrition macro tiles bump to `font-size:28px` (per UI-SPEC §Typography) but reuse the same `class="num"` for `font-variant-numeric: tabular-nums`.

### `cb-pulse` Busy Animation
**Source:** `cookbot-design.css:689` (referenced in UI-SPEC §State 4)
```razor
<CbButton Variant="CbButton.CbButtonVariant.Accent" Disabled=true>
    <span class="cb-pulse">Calculating…</span>
</CbButton>
```

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `tools/build-cnf-seed.py` | build script | batch | No Python/shell tools exist in the repo; pure offline one-time script; no runtime .NET analog |
| Ingredient→CNF matching algorithm inside `NutritionService` | algorithm | transform | No fuzzy/token-intersection name matcher exists anywhere in the codebase; closest is `IngredientResolver.Normalize` (only normalizes, does not score) |
| SHA-256 content-hash staleness in `RecipeService` | hash/invalidation | CRUD | No content-hash or staleness mechanism exists; BCL `SHA256.HashData` handles it without new packages |

---

## Metadata

**Analog search scope:** `src/CookBot.Domain/Entities/`, `src/CookBot.Infrastructure/Data/`, `src/CookBot.Infrastructure/Migrations/`, `src/CookBot.Application/Services/`, `src/CookBot.Application/Recipes/`, `src/CookBot.Web/Components/Pages/`, `tests/CookBot.Tests/`
**Files read:** 22
**Pattern extraction date:** 2026-06-08
