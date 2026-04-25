# Phase 1: Canonical Format Foundation — Pattern Map

**Mapped:** 2026-04-25
**Files analyzed:** 36 (12 modified, 14 source created, 10 test/fixture created)
**Analogs found:** 36 / 36 (every new file has an in-tree analog or a research-cited verbatim shape)

This map turns each new/modified file in Phase 1 into a concrete "copy from this analog, mirror these excerpts" recipe so the planner can drop direct file/line references into PLAN.md actions. All excerpts are taken from real files in the current tree (or, when the research already prescribed the verbatim shape, from `01-RESEARCH.md` with the line range cited).

---

## File Classification

### Files this phase modifies

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs` | domain interface (DTO + contract) | request-response | self (only the file's own existing shape; preserved verbatim) | exact (no change to public surface) |
| `src/CookBot.Application/Services/RecipeFormatParser.cs` | application service (singleton parser) | transform (text → DTO) | self (rewrite-in-place; existing class structure is the analog) | exact |
| `src/CookBot.Application/Services/PromptBuilderService.cs` | application service (prompt assembly) | transform (template → string) | self (delete two literal blocks; replace with provider call) | exact |
| `src/CookBot.Application/Services/IngredientRefDetectionService.cs` | static helper | transform (text → ids) | self (delete one branch) | exact |
| `src/CookBot.Application/Services/RecipeStepTextFormatter.cs` | static helper | transform (text → HTML) | self (no behavior change; receives canonical text) | exact |
| `src/CookBot.Domain/Entities/RecipeStep.cs` | EF entity | CRUD | self (write-path retired for `IngredientRefs`; column stays) | exact |
| `src/CookBot.Domain/Entities/Recipe.cs` | EF entity | CRUD | self (add one nullable string property) | exact |
| `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` | EF DbContext | CRUD | self (no DbSet change; new column flows via `RecipeConfiguration`) | exact |
| `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs` | EF fluent config | mapping | self (add plain string property mapping; **NOT** `OwnsOne`) | exact |
| `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` | startup orchestration | batch | self (insert backup + backfill loops around existing `MigrateAsync` call) | exact |
| `src/CookBot.Application/DependencyInjection.cs` | DI registration | composition | self (add 6 new singleton lines) | exact |
| `src/CookBot.Infrastructure/DependencyInjection.cs` | DI registration | composition | self (add 1 singleton + 1 scoped line) | exact |
| `src/CookBot.Application/DTOs/CookbookTransferDtos.cs` | wire DTO | mapping | self (constant bump only: `1` → `2`) | exact |

### Files this phase creates

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `src/CookBot.Domain/Recipes/RecipeDocument.cs` | domain record (POCO + JSON attrs) | data | `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs` (`ParsedRecipe` shape) + `src/CookBot.Application/DTOs/CookbookTransferDtos.cs` (sealed record style) | role-match (records vs classes) |
| `src/CookBot.Domain/Recipes/StepNode.cs` | polymorphic abstract record | data | `01-RESEARCH.md:274-405` (Pattern 1) — research has verbatim canonical shape | exact (research-prescribed) |
| `src/CookBot.Domain/Recipes/IngredientEntry.cs` | domain record | data | `src/CookBot.Application/DTOs/CookbookTransferDtos.cs:30-37` (`CookbookTransferIngredient`) | exact (same shape, record-ified) |
| `src/CookBot.Domain/Recipes/TimerEntry.cs` | domain record | data | `src/CookBot.Application/DTOs/CookbookTransferDtos.cs:46-51` (`CookbookTransferTimer`) | exact |
| `src/CookBot.Application/Recipes/IRecipeSchemaDocumentationProvider.cs` | application interface | request-response | `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs:37-42` | role-match (interface in `Application/Recipes/` vs `Domain/Interfaces/`) |
| `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs` | singleton service | transform | `src/CookBot.Application/Services/IngredientResolver.cs` + `RecipeFormatParser.cs:21-32` (singleton ctor + `Lazy`-style cache pattern) | role-match |
| `src/CookBot.Application/Recipes/RecipeJsonSchemaProvider.cs` | singleton service | transform | `src/CookBot.Application/Services/RecipeFormatParser.cs:18-32` (singleton with cached state in ctor) + `01-RESEARCH.md:406-517` (Pattern 2 verbatim) | exact (research-prescribed) |
| `src/CookBot.Application/Recipes/RecipeValidator.cs` (+ `ValidationResult.cs`) | singleton service | transform | `src/CookBot.Application/Services/RecipeFormatParser.cs:143-186` (`TryParse` validation block) — same "errors-as-data" shape | role-match |
| `src/CookBot.Application/Recipes/IRecipeUpcaster.cs` | application interface | transform | `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs:37-42` | role-match |
| `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` | singleton service | transform | `01-RESEARCH.md:605-755` (Pattern 4) verbatim shape | exact (research-prescribed) |
| `src/CookBot.Application/Recipes/Migration_V1_To_V2.cs` | singleton (`IRecipeUpcaster`) | transform | `01-RESEARCH.md:605-755` (Pattern 4) verbatim | exact (research-prescribed) |
| `src/CookBot.Application/Recipes/JsonRecipeSerializer.cs` | singleton service | transform | `src/CookBot.Application/Services/RecipeFormatParser.cs:21-32, 105-141` (ctor with options + `Serialize` method) | role-match |
| `src/CookBot.Infrastructure/Data/IDatabaseBackupService.cs` + `DatabaseBackupService.cs` | infra service (`File.Copy`) | file-I/O | `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs:103-136` (file-read + path resolution from content root) + `01-RESEARCH.md:1056-1135` (Pattern 9 verbatim) | exact (research-prescribed) |
| `src/CookBot.Infrastructure/Data/Migrations/Helpers/LegacyRecipeProjector.cs` | one-shot helper (DELETE-AFTER-V1.1) | transform | `src/CookBot.Application/Services/RecipeFormatParser.cs:85-103` (entity → ParsedRecipe projection) + `01-RESEARCH.md:1137-1208` (Pattern 10 verbatim) | exact (research-prescribed) |
| `src/CookBot.Infrastructure/Migrations/<timestamp>_RecipeCanonicalDocument.cs` | EF migration | schema | `src/CookBot.Infrastructure/Migrations/20260416175214_AiApiKeyShares.cs:11-19` (`AddColumn` with INTEGER → swap for TEXT nullable) | exact |
| `tests/CookBot.Tests/Recipes/RecipeDocumentRoundTripTests.cs` | xUnit Theory + MemberData | filesystem-driven | `tests/CookBot.Tests/Services/UnitParserTests.cs:7-17` (`[Theory]` + `[InlineData]` shape) + `01-RESEARCH.md:1370-1404` (verbatim MemberData over `Directory.GetFiles`) | exact (research-prescribed) |
| `tests/CookBot.Tests/Recipes/RecipeValidatorTests.cs` | xUnit Fact tests | unit | `tests/CookBot.Tests/Services/IngredientRefDetectionServiceTests.cs` | exact |
| `tests/CookBot.Tests/Recipes/RecipeUpcasterTests.cs` | xUnit Fact + Theory | unit | `tests/CookBot.Tests/Services/UnitParserTests.cs` | exact |
| `tests/CookBot.Tests/Recipes/RecipeJsonSchemaProviderTests.cs` | xUnit Fact tests | unit | `tests/CookBot.Tests/Services/RecipeFormatParserTests.cs` | exact |
| `tests/CookBot.Tests/Recipes/ExtrasRoundTripTests.cs` | xUnit Fact tests | unit | `tests/CookBot.Tests/Services/RecipeFormatParserTests.cs` | exact |
| `tests/CookBot.Tests/Migration/CanonicalBackfillTests.cs` | xUnit + IDisposable + SQLite `:memory:` | DB integration | `tests/CookBot.Tests/Services/OwnershipTests.cs:10-22` (`IDisposable` + in-memory SQLite ctor) | exact |
| `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` | xUnit Fact (file equality) | filesystem | `01-RESEARCH.md:1420-1446` (verbatim) | exact (research-prescribed) |
| `tests/CookBot.Tests/Prompts/PromptDenylistTests.cs` | xUnit Theory (regex over source) | filesystem | `01-RESEARCH.md:1448-1472` (verbatim) | exact (research-prescribed) |
| `tests/CookBot.Tests/Fixtures/Recipes/v1-yaml/*.yaml`, `v1-json-export/*.json`, `v1-db-projections/*.json`, `v2-canonical/*.json` | test fixture data | data | `seeds/ingredients.json` (committed JSON fixture in repo) | role-match (no `tests/Fixtures/` exists today — this introduces the convention) |
| `tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt` | text fixture | data | (none — new convention) | no analog |

---

## Pattern Assignments

### `src/CookBot.Domain/Recipes/RecipeDocument.cs` (domain record, polymorphic data)

**Analogs:**
- Closest in-tree shape: `src/CookBot.Application/DTOs/CookbookTransferDtos.cs:4-11` (the JSON-friendly DTO root)
- Closest existing parser-DTO: `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs:3-12` (`ParsedRecipe`)
- Verbatim research shape: `.planning/phases/01-canonical-format-foundation/01-RESEARCH.md` "Pattern 1: Canonical record shape" (lines 274-405)

**Style baseline — sealed-record domain shape (from `CookbookTransferDtos.cs:4-11`):**

```csharp
namespace CookBot.Application.DTOs;

/// <summary>Portable cookbook file for backup and sharing (JSON).</summary>
public sealed class CookbookTransferDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string ExportedAt { get; set; } = "";
    public string SourceApp { get; set; } = "CookBot";
    public CookbookTransferCookbook Cookbook { get; set; } = new();
    public List<CookbookTransferRecipe> Recipes { get; set; } = new();
}
```

**What to mirror:**
- `namespace CookBot.Domain.Recipes;` + file-scoped namespace + blank line (CONVENTIONS §"File-Scoped Namespaces").
- One public type per file, file name = type name (CONVENTIONS §"Files").
- `public sealed record` (CONVENTIONS §"Records are used for tiny immutable shape carriers" — lines 21-23).
- Default required strings/lists inline: `string Name { get; init; } = "";`, `List<...> Steps { get; init; } = new();` (mirrors `Recipe.cs:7,11,16,17`).
- `int Version` at the top of the root per D-04.

**STJ-attribute additions (Pitfall H4 + D-02 + D-05) — research-prescribed verbatim:**

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ContentStep), "content")]
[JsonDerivedType(typeof(SectionStep), "section")]
public abstract record StepNode { /* base */ }

[JsonExtensionData]
public Dictionary<string, JsonElement>? Extras { get; init; }
```

(Source: `01-RESEARCH.md:1351-1358` "Polymorphic record declaration" + `:1360-1368` `[JsonExtensionData]` round-trip.)

**Why `record` not `class`:** the existing `Parsed*` types in `IRecipeFormatParser.cs:3-35` are mutable classes for legacy reasons; the new domain records are immutable per D-01. Use `init` setters, not `set`.

---

### `src/CookBot.Domain/Recipes/StepNode.cs` (+ ContentStep, SectionStep) (polymorphic abstract record)

**Analog:** `01-RESEARCH.md:1351-1358` (verbatim discriminator declaration). No close in-tree analog because the codebase has zero polymorphic JSON today.

**Pattern to mirror — exact research-prescribed shape:**

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ContentStep), "content")]
[JsonDerivedType(typeof(SectionStep), "section")]
public abstract record StepNode
{
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; init; }
}

public sealed record ContentStep : StepNode
{
    public string Text { get; init; } = "";
    public IReadOnlyList<TimerEntry>? Timers { get; init; }
}

public sealed record SectionStep : StepNode
{
    public string Heading { get; init; } = "";
}
```

**Critical AC (Pitfall C3, mapped at `01-RESEARCH.md:1264-1274`):** No `IsSection` boolean on the canonical record. `grep -E '\bIsSection\b|\bisSection\b' src/CookBot.Domain/Recipes/` must return zero matches.

---

### `src/CookBot.Domain/Recipes/IngredientEntry.cs` (domain record)

**Analog:** `src/CookBot.Application/DTOs/CookbookTransferDtos.cs:30-37`.

**Verbatim shape to mirror (style only — convert class→record, rename `LocalId`→`Id` per D-06):**

```csharp
public sealed class CookbookTransferIngredient
{
    public int LocalId { get; set; }
    public string Name { get; set; } = "";
    public double Amount { get; set; }
    public string Unit { get; set; } = "";
    public string? Note { get; set; }
}
```

**Translation for new file:**

```csharp
public sealed record IngredientEntry
{
    public int Id { get; init; }                 // renamed from LocalId per D-06
    public string Name { get; init; } = "";
    public double Amount { get; init; }
    public string Unit { get; init; } = "";
    public string? Note { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; init; }
}
```

---

### `src/CookBot.Domain/Recipes/TimerEntry.cs` (domain record)

**Analog:** `src/CookBot.Application/DTOs/CookbookTransferDtos.cs:46-51`.

**Verbatim shape to mirror:**

```csharp
public sealed class CookbookTransferTimer
{
    public int Duration { get; set; }
    public string Unit { get; set; } = "min";
    public string? Label { get; set; }
}
```

Convert to `sealed record` with `init` setters; same field names, same defaults. No `Extras` needed (D-05 lists `Extras` only on root, `IngredientEntry`, `ContentStep`, `SectionStep`).

---

### `src/CookBot.Application/Services/RecipeFormatParser.cs` (rewrite — singleton parser)

**Analog (rewrite-in-place):** the existing file at the same path. Preserve the public surface (`Parse`, `Serialize`, `TryParse`) per D-10.

**Imports pattern to keep (lines 1-6):**

```csharp
using System.Text.RegularExpressions;
using CookBot.Domain.Interfaces;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CookBot.Application.Services;
```

**Constructor pattern (lines 18-32) — keep the "build options once at ctor, store in readonly fields" shape, but inject the new dependencies instead of building Yaml options:**

```csharp
private readonly IDeserializer _deserializer;
private readonly ISerializer _serializer;

public RecipeFormatParser()
{
    _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();
    ...
}
```

**Mirror for the rewrite — inject the new chain (D-10 step list):**

```csharp
public RecipeFormatParser(
    RecipeUpcasterChain upcasterChain,
    JsonRecipeSerializer serializer,
    RecipeValidator validator)
{ ... }
```

(Constructor injection convention — see CONVENTIONS §"Constructor injection" lines 73-95 + `RecipeService.cs:14-24`.)

**`TryParse` shape to preserve (lines 143-186)** — error-list-as-data, never throw, validation-as-list-of-strings. The new implementation builds errors from `RecipeValidator.ValidationResult` instead of inline checks, but the **public signature stays identical**:

```csharp
public bool TryParse(string rawContent, out ParsedRecipe? recipe, out List<string> errors)
{
    errors = new List<string>();
    recipe = null;

    if (string.IsNullOrWhiteSpace(rawContent))
    {
        errors.Add("Recipe content is empty.");
        return false;
    }
    ...
    try
    {
        recipe = Parse(rawContent);
        ...
        return !errors.Any();
    }
    catch (Exception ex)
    {
        errors.Add($"Parse error: {ex.Message}");
        return false;
    }
}
```

**Steps to internalize (D-10):** detect YAML/JSON → YAML→JsonNode adapter (research Pattern 5 at `01-RESEARCH.md:756-804`) → stamp `version: 1` → `RecipeUpcasterChain.UpcastToCurrent` → `JsonSerializer.Deserialize<RecipeDocument>` → `RecipeValidator.Validate` → project to `ParsedRecipe` for back-compat.

**Projection back to `ParsedRecipe` (lines 85-103) is the analog for `RecipeDocument → ParsedRecipe`:**

```csharp
return new ParsedRecipe
{
    Name = frontmatter.Name ?? "Untitled Recipe",
    Servings = frontmatter.Servings,
    PrepTimeMinutes = frontmatter.PrepTime,
    CookTimeMinutes = frontmatter.CookTime,
    Tags = frontmatter.Tags ?? new List<string>(),
    Ingredients = (frontmatter.Ingredients ?? new List<IngredientFrontmatter>())
        .Select(i => new ParsedIngredient { ... }).ToList(),
    Steps = steps,
};
```

For the rewrite, the source becomes `RecipeDocument` and the rendering of `StepNode` polymorphism back to `ParsedStep.IsSection: bool` happens at this projection layer (legacy boundary).

---

### `src/CookBot.Application/Services/PromptBuilderService.cs` (modify — delete duplicated literal blocks)

**Analog:** self. Existing constructor uses no DI parameters; gain one per `01-CONTEXT.md` §"Integration Points":

```csharp
public PromptBuilderService(IRecipeSchemaDocumentationProvider docs) { ... }
```

**Delete the literal block at lines 168-202** (the entire `private string ResolveRecipeFormat()` body) and replace with:

```csharp
private string ResolveRecipeFormat() => _docs.GetFormatPrompt();
```

**Delete the literal block at lines 262-296** in `BuildCopyablePrompt` (the `## Recipe Format` block) and replace with `sb.AppendLine(_docs.GetFormatPrompt());`.

**Critical AC:** the opt-out clause at line 201 (`"If you can't follow this exact format, plain numbered steps are fine — the app will parse them."`) and line 295 (same string) must be removed from both call sites. The `PromptDenylistTests` (D-22) enforces this at test time — see denylist regex below.

**Scope hint (CONVENTIONS §"Lifetimes" line 181):** `PromptBuilderService` today is registered `Scoped` in `Infrastructure/DependencyInjection.cs:22`. Lifetime stays unchanged — the new `IRecipeSchemaDocumentationProvider` is a singleton and can safely be consumed by a scoped service.

---

### `src/CookBot.Application/Services/IngredientRefDetectionService.cs` (modify — delete substring-fallback branch)

**Analog:** self. Current full source:

```csharp
public static List<int> DetectRefs(string stepText, List<ParsedIngredient> ingredients)
{
    var refs = new HashSet<int>();

    // First: explicit markdown links [name](#id)
    foreach (Match match in MarkdownLinkPattern.Matches(stepText))
    {
        if (int.TryParse(match.Groups[2].Value, out var id))
            refs.Add(id);
    }

    // Second: plain text name matching (case-insensitive)         <-- DELETE
    var textLower = stepText.ToLowerInvariant();                    // <-- DELETE
    foreach (var ingredient in ingredients)                         // <-- DELETE
    {                                                               // <-- DELETE
        if (refs.Contains(ingredient.LocalId)) continue;            // <-- DELETE
        var nameLower = ingredient.Name.ToLowerInvariant();         // <-- DELETE
        if (nameLower.Length >= 3 && textLower.Contains(nameLower)) // <-- DELETE
            refs.Add(ingredient.LocalId);                           // <-- DELETE
    }                                                               // <-- DELETE

    return refs.OrderBy(x => x).ToList();
}
```

**Resulting shape after delete (lines 12-21 only — Pitfall C1 fix):**

```csharp
public static List<int> DetectRefs(string stepText, List<ParsedIngredient> ingredients)
{
    var refs = new HashSet<int>();
    foreach (Match match in MarkdownLinkPattern.Matches(stepText))
    {
        if (int.TryParse(match.Groups[2].Value, out var id))
            refs.Add(id);
    }
    return refs.OrderBy(x => x).ToList();
}
```

**Critical AC (Pitfall C1, mapped at `01-RESEARCH.md:1249-1252`):** `grep -nE 'textLower\.Contains|nameLower\.Length' src/CookBot.Application/Services/IngredientRefDetectionService.cs` returns zero matches.

**Note on the parameter:** `ingredients` becomes unused after the delete. Keep the parameter for back-compat with all existing callers (`RecipeService.CreateAsync` line 69, `RecipeService.UpdateAsync` line 129, `IngredientRefDetectionServiceTests.cs:16-44`); add a `_ = ingredients;` line or `[SuppressMessage]` if compiler warning surfaces.

---

### `src/CookBot.Domain/Entities/Recipe.cs` (modify — add nullable string property)

**Analog:** self. Current shape (lines 3-18 in full):

```csharp
public class Recipe
{
    public int Id { get; set; }
    public int CookbookId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Servings { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public string TagsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Cookbook Cookbook { get; set; } = null!;
    public List<RecipeStep> Steps { get; set; } = new();
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}
```

**Addition (D-12):** one property after `TagsJson`:

```csharp
public string? CanonicalDocumentJson { get; set; }
```

**Style mirror:** STRUCTURE.md "Naming Conventions" §"Properties" line 193 — JSON columns end in `Json`. Nullable string with no default (per D-12 "TEXT, nullable initially").

**Anti-pattern (research line 1214):** **Do NOT use `OwnsOne`** for this column. It's a plain string snapshot, not a relational projection.

---

### `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs` (modify — register the new column)

**Analog:** self. Current full source:

```csharp
public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(300).IsRequired();
        builder.Property(r => r.TagsJson).HasDefaultValue("[]");

        builder.OwnsMany(r => r.Steps, steps =>
        {
            steps.ToJson();
            steps.OwnsMany(s => s.Timers);
        });

        builder.HasMany(r => r.RecipeIngredients).WithOne(ri => ri.Recipe).HasForeignKey(ri => ri.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

**Addition pattern to mirror (line 13's plain `builder.Property` style — NOT line 15's `OwnsMany.ToJson` style):**

```csharp
builder.Property(r => r.CanonicalDocumentJson);  // plain string?, EF Core picks TEXT NULL automatically
```

**Why the simple form:** D-12. `OwnsMany.ToJson()` (line 15-19) is the right tool for relational-to-JSON projection of `Steps`; `CanonicalDocumentJson` is a snapshot we own the serialization of, so EF treats it as opaque.

---

### `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` (modify — wrap `MigrateAsync` with backup + backfill)

**Analog:** self. **Exact insertion point: line 20** (before `await context.Database.MigrateAsync();`).

**Current shape (lines 18-22):**

```csharp
public static async Task SeedAsync(CookBotDbContext context, string contentRootPath)
{
    await context.Database.MigrateAsync();

    if (await context.Users.AnyAsync())
```

**Required modifications (D-15 + D-16):**

1. **Promote `SeedAsync` from `static` to instance** OR keep static and pass `IDatabaseBackupService` + `LegacyRecipeProjector` + `JsonRecipeSerializer` as parameters. Recommended: change the signature to accept an `IServiceProvider` like other startup hooks; the planner has discretion (D-discretion list, line 84-87).

2. **Pre-`MigrateAsync` backup gate (D-15):**

```csharp
var pending = await context.Database.GetPendingMigrationsAsync();
if (pending.Any())
{
    await backupService.BackupBeforeMigrationAsync("RecipeCanonicalDocument", CancellationToken.None);
}
await context.Database.MigrateAsync();
```

3. **Post-`MigrateAsync` backfill (D-16):** insert immediately after `await context.Database.MigrateAsync();` and before `if (await context.Users.AnyAsync())`. Pattern mirrors the seed-loading loop at lines 71-84 (load → loop → batch save):

```csharp
// from existing seeder for the loop+save shape (lines 76-84):
foreach (var ingredient in ingredients)
{
    if (!existingNormalized.Contains(ingredient.NormalizedName))
    {
        context.Ingredients.Add(ingredient);
    }
}
await context.SaveChangesAsync();
```

**Translation for backfill (D-16, batches of 50):**

```csharp
const int batchSize = 50;
var pendingRecipes = await context.Recipes
    .Where(r => r.CanonicalDocumentJson == null)
    .Include(r => r.Steps)
    .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.Ingredient)
    .ToListAsync();

for (int i = 0; i < pendingRecipes.Count; i += batchSize)
{
    foreach (var recipe in pendingRecipes.Skip(i).Take(batchSize))
    {
        var doc = projector.Project(recipe);
        recipe.CanonicalDocumentJson = serializer.Serialize(doc);
    }
    await context.SaveChangesAsync();
}
```

**Rest of the file is untouched** — admin-ensure logic (lines 22-41), default-user creation (lines 43-86), `EnsureAtLeastOneCookBotAdminAsync`, `LoadIngredientsFromSeedFile` all stay as-is.

---

### `src/CookBot.Application/DependencyInjection.cs` (modify — add 6 singletons)

**Analog:** self. Current full shape (lines 9-19) — append-only modification:

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddSingleton<IRecipeFormatParser, RecipeFormatParser>();
    services.AddSingleton<IUnitConverter, UnitConversionService>();
    services.AddScoped<CookbookService>();
    services.AddScoped<RecipeService>();
    services.AddScoped<PantryService>();
    services.AddScoped<PantryAiPopulationService>();
    services.AddScoped<GroceryListService>();
    return services;
}
```

**Additions (research-prescribed verbatim at `01-RESEARCH.md:1405-1418`):**

```csharp
services.AddSingleton<IRecipeSchemaDocumentationProvider, RecipeSchemaDocumentationProvider>();
services.AddSingleton<RecipeJsonSchemaProvider>();
services.AddSingleton<RecipeValidator>();
services.AddSingleton<JsonRecipeSerializer>();
services.AddSingleton<IRecipeUpcaster, Migration_V1_To_V2>();
services.AddSingleton<RecipeUpcasterChain>();
```

**Lifetime rationale (CONVENTIONS §"Lifetimes" lines 179-183):** Singleton for stateless pure services exposed through interfaces. All six new types are stateless after construction (the `Lazy<JsonNode>` cache in `RecipeJsonSchemaProvider` is per-instance; singleton is correct).

---

### `src/CookBot.Infrastructure/DependencyInjection.cs` (modify — add 1 singleton + 1 scoped)

**Analog:** self. Current full shape (lines 15-26):

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<CookBotDbContext>(options =>
        options.UseSqlite(configuration.GetConnectionString("DefaultConnection") ?? "Data Source=cookbot.db"));

    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    services.AddScoped<IAiService, AnthropicAiService>();
    services.AddScoped<PromptBuilderService>();
    services.AddApplication();

    return services;
}
```

**Additions (research-prescribed verbatim at `01-RESEARCH.md:1415-1418`):**

```csharp
services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();
services.AddScoped<LegacyRecipeProjector>();
```

**Lifetime rationale:** `IDatabaseBackupService` is stateless file-I/O — singleton. `LegacyRecipeProjector` is `Scoped` (consumed once at startup inside the `DatabaseSeeder` scope; throwaway one-shot per D-14).

---

### `src/CookBot.Application/DTOs/CookbookTransferDtos.cs` (modify — bump constant)

**Analog:** self. Single-line change at line 6:

```csharp
public int SchemaVersion { get; set; } = 1;   // <-- becomes 2 (per D-17)
```

No other changes in this phase. Deserializer hot-path stays on the existing v1 path; Phase 2 owns MIGRATION-04/06.

---

### `src/CookBot.Application/Recipes/IRecipeSchemaDocumentationProvider.cs` (interface)

**Analog:** `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs:37-42`.

**Analog excerpt:**

```csharp
public interface IRecipeFormatParser
{
    ParsedRecipe Parse(string rawContent);
    string Serialize(ParsedRecipe recipe);
    bool TryParse(string rawContent, out ParsedRecipe? recipe, out List<string> errors);
}
```

**New file shape (single method per D-19):**

```csharp
namespace CookBot.Application.Recipes;

public interface IRecipeSchemaDocumentationProvider
{
    string GetFormatPrompt();
}
```

---

### `src/CookBot.Application/Recipes/RecipeJsonSchemaProvider.cs` (singleton, lazy cache)

**Analogs:**
- Singleton ctor + cached state: `src/CookBot.Application/Services/RecipeFormatParser.cs:18-32` (readonly fields built once at ctor).
- Verbatim research shape: `01-RESEARCH.md:406-517` (Pattern 2).

**Analog excerpt (`RecipeFormatParser.cs:18-32`):**

```csharp
private readonly IDeserializer _deserializer;
private readonly ISerializer _serializer;

public RecipeFormatParser()
{
    _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();
}
```

**Mirror — `Lazy<JsonNode>` cache pattern (D-07):**

```csharp
public sealed class RecipeJsonSchemaProvider
{
    private readonly Lazy<JsonNode> _schema;

    public RecipeJsonSchemaProvider()
    {
        _schema = new Lazy<JsonNode>(BuildSchema);
    }

    public JsonNode GetSchema() => _schema.Value;

    private static JsonNode BuildSchema()
    {
        var node = JsonSchemaExporter.GetJsonSchemaAsNode(
            JsonSerializerOptions.Default, typeof(RecipeDocument));
        WalkAndSetAdditionalPropertiesFalse(node);
        return node;
    }
    ...
}
```

(Pattern verbatim from `01-RESEARCH.md:1340-1349` for the `JsonSchemaExporter` call and `:406-517` for the walker.)

---

### `src/CookBot.Application/Recipes/RecipeValidator.cs` (+ ValidationResult)

**Analog (errors-as-data shape):** `src/CookBot.Application/Services/RecipeFormatParser.cs:143-186` — the existing `TryParse` already returns errors as a `List<string>` rather than throwing. `RecipeValidator` formalizes this with structured records.

**Analog excerpt (lines 143-186):**

```csharp
public bool TryParse(string rawContent, out ParsedRecipe? recipe, out List<string> errors)
{
    errors = new List<string>();
    recipe = null;
    ...
    try
    {
        recipe = Parse(rawContent);
        if (string.IsNullOrWhiteSpace(recipe.Name))
            errors.Add("Recipe name is required.");
        if (recipe.Servings <= 0)
            errors.Add("Servings must be greater than 0.");
        if (!recipe.Ingredients.Any())
            errors.Add("At least one ingredient is required.");
        var ids = recipe.Ingredients.Select(i => i.LocalId).ToList();
        if (ids.Count != ids.Distinct().Count())
            errors.Add("Ingredient IDs must be unique.");
        if (!recipe.Steps.Any())
            errors.Add("At least one step is required.");
        return !errors.Any();
    }
    catch (Exception ex)
    {
        errors.Add($"Parse error: {ex.Message}");
        return false;
    }
}
```

**Mirror — D-08 shape (errors AND warnings as records, never throws):**

```csharp
public sealed record ValidationError(string Path, string Code, string Message);
public sealed record ValidationWarning(string Path, string Code, string Message);

public sealed record ValidationResult(
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ValidationWarning> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed class RecipeValidator
{
    public ValidationResult Validate(RecipeDocument doc)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        if (string.IsNullOrWhiteSpace(doc.Name))
            errors.Add(new ValidationError("/name", "REQUIRED", "Recipe name is required."));
        if (doc.Servings <= 0)
            errors.Add(new ValidationError("/servings", "OUT_OF_RANGE", "Servings must be > 0."));
        // duplicate ingredient ids, unresolved [name](#id) links, section step with timers...

        return new ValidationResult(errors, warnings);
    }
}
```

Use `sealed record` (CONVENTIONS line 21-23). Public surface uses `IReadOnlyList<T>` (CONVENTIONS line 211).

---

### `src/CookBot.Application/Recipes/IRecipeUpcaster.cs` + `RecipeUpcasterChain.cs` + `Migration_V1_To_V2.cs`

**Analogs:** `01-RESEARCH.md:605-755` (Pattern 4 — full source provided). No close in-tree analog because the codebase has no document migration today.

**Interface shape (D-09 verbatim):**

```csharp
public interface IRecipeUpcaster
{
    int FromVersion { get; }
    int ToVersion { get; }
    JsonNode Upcast(JsonNode input);
}
```

**Chain shape (singleton fed by DI; sorts upcasters; applies in sequence):**

```csharp
public sealed class RecipeUpcasterChain
{
    public const int CurrentVersion = 2;

    private readonly IReadOnlyList<IRecipeUpcaster> _ordered;

    public RecipeUpcasterChain(IEnumerable<IRecipeUpcaster> upcasters)
    {
        _ordered = upcasters.OrderBy(u => u.FromVersion).ToList();
        // Optional: validate no version gaps (planner discretion per CONTEXT.md line 84)
    }

    public JsonNode UpcastToCurrent(JsonNode input)
    {
        var node = input;
        var version = node["version"]?.GetValue<int>() ?? 1;   // stamp v1 if missing (Pitfall H1)
        while (version < CurrentVersion)
        {
            var step = _ordered.FirstOrDefault(u => u.FromVersion == version)
                ?? throw new InvalidOperationException($"No upcaster from v{version}.");
            node = step.Upcast(node);
            version = step.ToVersion;
        }
        return node;
    }
}
```

**`Migration_V1_To_V2` rules (D-09 verbatim) — JSON-node-level rewrites only:**
- `prepTime` → `prepTimeMinutes` (Pitfall C2)
- `cookTime` → `cookTimeMinutes`
- `IsSection: true` + `Text: "X"` → `kind: "section", heading: "X"` (Pitfall C3)
- `localId` → `id` (D-06)
- Set `version: 2`

**DI registration pattern (open-generic-style enumeration consumed by chain):**

The chain ctor takes `IEnumerable<IRecipeUpcaster>` — the standard MS DI pattern for "list of all registered impls of interface T". Registering `services.AddSingleton<IRecipeUpcaster, Migration_V1_To_V2>();` is enough; future upcasters add a line.

---

### `src/CookBot.Application/Recipes/JsonRecipeSerializer.cs`

**Analog:** `src/CookBot.Application/Services/RecipeFormatParser.cs:21-32, 105-141` (cached options + `Serialize` method).

**Analog excerpt (lines 105-141 — `Serialize` shape):**

```csharp
public string Serialize(ParsedRecipe recipe)
{
    var frontmatter = new RecipeFrontmatter { ... };
    ...
    var yaml = _serializer.Serialize(frontmatter).TrimEnd();
    return $"---\n{yaml}\n---\n";
}
```

**Mirror for new file (D-discretion: planner picks `Serialize` only OR `Serialize` + `SerializeIndented` per CONTEXT.md line 85):**

```csharp
public sealed class JsonRecipeSerializer
{
    private readonly JsonSerializerOptions _compact;
    private readonly JsonSerializerOptions _indented;

    public JsonRecipeSerializer()
    {
        _compact = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        _indented = new JsonSerializerOptions(_compact) { WriteIndented = true };
    }

    public string Serialize(RecipeDocument doc) => JsonSerializer.Serialize(doc, _compact);
    public string SerializeIndented(RecipeDocument doc) => JsonSerializer.Serialize(doc, _indented);
    public RecipeDocument Deserialize(JsonNode node) => node.Deserialize<RecipeDocument>(_compact)!;
}
```

CONVENTIONS §"String Handling" line 207 — STJ uses `JsonNamingPolicy.CamelCase` for project-wide JSON. Mirrors `AnthropicAiService.cs:26`'s `SnakeCaseLower` pattern but for our internal camelCase.

---

### `src/CookBot.Infrastructure/Data/IDatabaseBackupService.cs` + `DatabaseBackupService.cs`

**Analogs:**
- File-I/O + path-resolution shape: `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs:103-136` (`Path.GetFullPath`, `File.Exists`, `File.ReadAllTextAsync` reading from a content-root-relative path).
- Verbatim research shape: `01-RESEARCH.md:1056-1135` (Pattern 9).

**Analog excerpt — file-IO + path resolution (`DatabaseSeeder.cs:103-114`):**

```csharp
private static async Task<List<Ingredient>> LoadIngredientsFromSeedFile(string contentRootPath)
{
    var seedPath = Path.GetFullPath(Path.Combine(contentRootPath, "..", "seeds", "ingredients.json"));

    if (!File.Exists(seedPath))
    {
        return [];
    }

    var json = await File.ReadAllTextAsync(seedPath);
    ...
}
```

**Mirror for new file (research Pattern 9 verbatim — interface + impl):**

```csharp
// IDatabaseBackupService.cs
namespace CookBot.Infrastructure.Data;

public interface IDatabaseBackupService
{
    Task BackupBeforeMigrationAsync(string migrationName, CancellationToken ct);
}
```

```csharp
// DatabaseBackupService.cs
public sealed class DatabaseBackupService : IDatabaseBackupService
{
    private readonly IConfiguration _config;
    private readonly int _retention;

    public DatabaseBackupService(IConfiguration config, IOptions<CookBotSettings> settings)
    {
        _config = config;
        _retention = 3;   // D-15 default; planner may surface CookBotSettings:DatabaseBackupRetention
    }

    public Task BackupBeforeMigrationAsync(string migrationName, CancellationToken ct)
    {
        var connStr = _config.GetConnectionString("DefaultConnection") ?? "Data Source=cookbot.db";
        var builder = new SqliteConnectionStringBuilder(connStr);
        var dbPath = builder.DataSource;

        var fullPath = Path.GetFullPath(dbPath);
        if (!File.Exists(fullPath))
            return Task.CompletedTask;     // fresh install — nothing to back up

        var dir = Path.GetDirectoryName(fullPath)!;
        var stem = Path.GetFileName(fullPath);
        var backupName = $"{stem}.pre-{migrationName}.bak";
        var backupPath = Path.Combine(dir, backupName);

        File.Copy(fullPath, backupPath, overwrite: true);

        var pattern = $"{stem}.pre-*.bak";
        var existing = Directory.GetFiles(dir, pattern)
            .Select(p => new FileInfo(p))
            .OrderByDescending(fi => fi.LastWriteTimeUtc)
            .ToList();
        foreach (var stale in existing.Skip(_retention))
        {
            try { stale.Delete(); }
            catch { /* swallow — non-fatal */ }
        }

        return Task.CompletedTask;
    }
}
```

**Critical contract notes (research line 1126-1131):**
- Use `SqliteConnectionStringBuilder(connStr).DataSource`, NOT regex/`Split('=')`.
- `Microsoft.Data.Sqlite` is transitive via `Microsoft.EntityFrameworkCore.Sqlite` (no new package).
- `File.Copy` is correct because backup runs **before** `MigrateAsync` opens any connection.
- The catch-and-swallow on `Delete()` matches the "intentional silent fallback" pattern (CONVENTIONS §"Silent fallbacks" line 156-157).

---

### `src/CookBot.Infrastructure/Data/Migrations/Helpers/LegacyRecipeProjector.cs`

**Analogs:**
- Entity → DTO projection shape: `src/CookBot.Application/Services/RecipeFormatParser.cs:85-103` (existing relational-to-`ParsedRecipe` projection style).
- Verbatim research shape: `01-RESEARCH.md:1137-1208` (Pattern 10).

**Analog excerpt (`RecipeFormatParser.cs:85-103`):**

```csharp
return new ParsedRecipe
{
    Name = frontmatter.Name ?? "Untitled Recipe",
    Servings = frontmatter.Servings,
    PrepTimeMinutes = frontmatter.PrepTime,
    CookTimeMinutes = frontmatter.CookTime,
    Tags = frontmatter.Tags ?? new List<string>(),
    Ingredients = (frontmatter.Ingredients ?? new List<IngredientFrontmatter>())
        .Select(i => new ParsedIngredient { ... }).ToList(),
    Steps = steps,
};
```

**Mirror — research Pattern 10 verbatim (`01-RESEARCH.md:1137-1208`):**

```csharp
namespace CookBot.Infrastructure.Data.Migrations.Helpers;

// DELETE-AFTER-V1.1 (per CONTEXT.md D-14 + Phase 4 POLISH-03)
public sealed class LegacyRecipeProjector
{
    public RecipeDocument Project(Recipe recipe)
    {
        var ingredients = recipe.RecipeIngredients
            .OrderBy(ri => ri.RecipeLocalId)
            .Select(ri => new IngredientEntry
            {
                Id = ri.RecipeLocalId,
                Name = ri.Ingredient.Name,
                Amount = ri.Amount,
                Unit = ri.Unit,
                Note = ri.Note,
            })
            .ToList();

        var steps = recipe.Steps
            .OrderBy(s => s.Order)
            .Select(s => s.IsSection
                ? (StepNode)new SectionStep { Heading = s.Text }
                : new ContentStep
                {
                    Text = s.Text,
                    Timers = s.Timers?.Count > 0
                        ? s.Timers.Select(t => new TimerEntry { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList()
                        : null,
                })
            .ToList();

        return new RecipeDocument
        {
            Version = RecipeUpcasterChain.CurrentVersion,
            Name = recipe.Name,
            ...
        };
    }

    private static IReadOnlyList<string> TryDeserializeTags(string tagsJson)
    {
        try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(tagsJson) ?? []; }
        catch { return []; }
    }
}
```

**Critical contract notes (research line 1208):** Projector does NOT consult `RecipeStep.IngredientRefs`. Markdown `[name](#id)` in `s.Text` is the only ref source — closes Pitfall C1.

---

### `src/CookBot.Infrastructure/Migrations/<timestamp>_RecipeCanonicalDocument.cs`

**Analog:** `src/CookBot.Infrastructure/Migrations/20260416175214_AiApiKeyShares.cs:11-19` (column-add-only migration).

**Analog excerpt (`AiApiKeyShares.cs:11-19`):**

```csharp
public partial class AiApiKeyShares : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AiSharedKeyOwnerUserId",
            table: "UserProfiles",
            type: "INTEGER",
            nullable: true);
        ...
    }
}
```

**Generation command (research line 1474-1480 + STRUCTURE.md line 201):**

```bash
dotnet ef migrations add RecipeCanonicalDocument \
    --project src/CookBot.Infrastructure \
    --startup-project src/CookBot.Web
```

**Mirror — generated `Up()` body should be exactly (research line 1484-1491):**

```csharp
migrationBuilder.AddColumn<string>(
    name: "CanonicalDocumentJson",
    table: "Recipes",
    type: "TEXT",
    nullable: true);
```

**`Down()` body (mirror `AiApiKeyShares.cs` Down style at lines 73-89):**

```csharp
migrationBuilder.DropColumn(name: "CanonicalDocumentJson", table: "Recipes");
```

**Critical AC (research line 1217 + Pitfall C4):** **Do NOT add `migrationBuilder.Sql(...)` for backfill.** The migration is column-add only; backfill happens in `DatabaseSeeder` (`File.Copy` backup → `MigrateAsync` → C# backfill loop).

**Style mirror:** existing migrations use `namespace CookBot.Infrastructure.Migrations` block-style (line 7-8 of `AiApiKeyShares.cs`) — that's auto-generated by `dotnet ef`; the convention is to leave it alone (CONVENTIONS line 46).

---

### `tests/CookBot.Tests/Recipes/RecipeDocumentRoundTripTests.cs` (Theory + MemberData over fixtures)

**Analogs:**
- Theory shape: `tests/CookBot.Tests/Services/UnitParserTests.cs:7-17`.
- MemberData + filesystem: `01-RESEARCH.md:1370-1404` (verbatim).

**Analog excerpt — Theory + InlineData (`UnitParserTests.cs:7-17`):**

```csharp
[Theory]
[InlineData("cups", MeasurementUnit.Cup)]
[InlineData("tbsp", MeasurementUnit.Tablespoon)]
[InlineData("g", MeasurementUnit.Gram)]
[InlineData("mL", MeasurementUnit.Milliliter)]
public void TryParse_KnownUnit_ReturnsEnum(string input, MeasurementUnit expected)
{
    var result = UnitParser.TryParse(input);
    Assert.Equal(expected, result);
}
```

**Mirror — MemberData + `Directory.GetFiles` (research verbatim at line 1370-1394):**

```csharp
public class RecipeDocumentRoundTripTests
{
    public static IEnumerable<object[]> V1YamlFixtures()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recipes", "v1-yaml");
        foreach (var path in Directory.GetFiles(dir, "*.yaml"))
            yield return new object[] { Path.GetFileName(path), File.ReadAllText(path) };
    }

    [Theory]
    [MemberData(nameof(V1YamlFixtures))]
    public void V1Yaml_ParsesAndRoundTrips(string fixtureName, string yamlText)
    {
        var parser = TestHost.GetParser();
        Assert.True(parser.TryParse(yamlText, out var parsed, out var errors),
            $"{fixtureName} failed to parse: {string.Join("; ", errors)}");
        Assert.NotNull(parsed);
        Assert.NotEqual(0, parsed!.PrepTimeMinutes ?? 0);
        Assert.NotEqual(0, parsed.CookTimeMinutes ?? 0);
    }
}
```

**Critical csproj addition (research line 1396-1403):**

```xml
<!-- in tests/CookBot.Tests/CookBot.Tests.csproj -->
<ItemGroup>
  <None Update="Fixtures\**\*.*" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

**Test-method naming mirror:** `MethodOrFeature_Scenario_ExpectedResult` with underscores (TESTING.md line 103-108).

---

### `tests/CookBot.Tests/Migration/CanonicalBackfillTests.cs` (in-memory SQLite + projector)

**Analog:** `tests/CookBot.Tests/Services/OwnershipTests.cs:10-22` (verbatim ctor pattern for in-memory SQLite).

**Analog excerpt:**

```csharp
public class OwnershipTests : IDisposable
{
    private readonly CookBotDbContext _db;

    public OwnershipTests()
    {
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task RecipeService_CreateAsync_ThrowsForWrongUser() { ... }

    public void Dispose() => _db.Dispose();
}
```

**Mirror for new file (D-25 — 3-recipe round-trip smoke test):**

```csharp
public class CanonicalBackfillTests : IDisposable
{
    private readonly CookBotDbContext _db;

    public CanonicalBackfillTests()
    {
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Backfill_ThreeRecipes_RoundTripsWithoutValueDrift()
    {
        // Arrange: seed 3 representative recipes via existing RecipeService (relational shape)
        // Act: run LegacyRecipeProjector → JsonRecipeSerializer.Serialize → JsonRecipeSerializer.Deserialize → RecipeValidator.Validate
        // Assert: every field round-trips, validation passes
    }

    public void Dispose() => _db.Dispose();
}
```

**Critical TESTING.md notes (lines 145-152):**
- `OpenConnection()` is required to keep the in-memory SQLite alive.
- `EnsureCreated()` (NOT `MigrateAsync()`) — schema from EF model directly.
- xUnit instantiates a fresh class per `[Fact]` — no cross-test state leakage.
- Compose concrete repositories via `new Repository<Recipe>(_db)` (`OwnershipTests.cs:37-42`).

---

### `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` (D-21)

**Analog:** `01-RESEARCH.md:1420-1446` (verbatim research shape — no in-tree analog because the codebase has no snapshot tests today).

**Mirror — verbatim research code:**

```csharp
public class PromptSnapshotTests
{
    [Fact]
    public void DefaultTemplate_AssembledPrompt_MatchesSnapshot()
    {
        var profile = TestHost.MakeProfile();
        var pantry = Array.Empty<PantryItem>();
        var svc = TestHost.GetPromptBuilderService();
        var actual = svc.ResolveTemplate(PromptBuilderService.DefaultTemplate, profile, pantry);

        var fixturePath = Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "Prompts", "expected-system-prompt.txt");

        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
        {
            File.WriteAllText(fixturePath, actual);
            return;
        }

        var expected = File.ReadAllText(fixturePath);
        Assert.Equal(expected, actual);
    }
}
```

**TestHost helper:** new file `tests/CookBot.Tests/TestHost.cs` with `GetPromptBuilderService()`, `GetParser()`, `MakeProfile()`, `FindRepoRoot()` static methods. Style mirrors `tests/CookBot.Tests/Services/OwnershipTests.cs` private nested helpers (TESTING.md §"Mocking" lines 161-175).

---

### `tests/CookBot.Tests/Prompts/PromptDenylistTests.cs` (D-22)

**Analog:** `01-RESEARCH.md:1448-1472` (verbatim).

**Mirror — verbatim research code:**

```csharp
public class PromptDenylistTests
{
    private static readonly Regex Denylist =
        new(@"\b(fallback|informal|plain numbered|If you can'?t follow)\b",
            RegexOptions.IgnoreCase);

    [Theory]
    [InlineData("src/CookBot.Application/Services/PromptBuilderService.cs")]
    [InlineData("src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs")]
    public void PromptSourceFiles_ContainNoOptOutPhrases(string relativePath)
    {
        var repoRoot = TestHost.FindRepoRoot();
        var full = Path.Combine(repoRoot, relativePath);
        var src = File.ReadAllText(full);
        var matches = Denylist.Matches(src).Select(m => m.Value).ToList();
        Assert.True(matches.Count == 0,
            $"Found opt-out phrases in {relativePath}: {string.Join(", ", matches)}");
    }
}
```

**`TestHost.FindRepoRoot()` walks up from `AppContext.BaseDirectory` looking for `FreelovesCookBot.sln` (research line 1472).**

---

### Test fixtures (`tests/CookBot.Tests/Fixtures/...`)

**Analog:** `seeds/ingredients.json` (committed JSON fixture loaded via `File.ReadAllTextAsync` from `ContentRootPath`). The new fixtures use the same "real text file in repo" convention but located at `AppContext.BaseDirectory` via the `<None Update Fixtures\**\*.*>` csproj line documented above.

**Fixture content sources (D-23):**
- `v1-yaml/`: drawn from existing seed cookbook recipes serialized via current `RecipeFormatParser.Serialize`.
- `v1-json-export/`: drawn from existing `CookbookTransferService.SerializeToUtf8Json` output.
- `v1-db-projections/`: produced by running `LegacyRecipeProjector` against seeded DB rows during a one-shot script.
- `v2-canonical/`: produced by running the upcaster chain against the v1 fixtures and serializing via `JsonRecipeSerializer.SerializeIndented`.

Minimum 5 fixtures per D-23. Naming: `simple.yaml`, `sectioned.yaml`, `multi-timer.yaml`, `ingredient-heavy.yaml`, `mixed-edge.yaml` (planner discretion).

---

## Shared Patterns

### Constructor injection + `_camelCase` readonly fields

**Source:** `src/CookBot.Application/Services/RecipeService.cs:7-24` (canonical example).
**Apply to:** every new singleton/scoped service in this phase (`RecipeJsonSchemaProvider`, `RecipeValidator`, `RecipeUpcasterChain`, `Migration_V1_To_V2`, `JsonRecipeSerializer`, `RecipeSchemaDocumentationProvider`, `DatabaseBackupService`, `LegacyRecipeProjector`).

```csharp
public class RecipeService
{
    private readonly IRecipeFormatParser _parser;
    private readonly IRepository<Recipe> _recipeRepo;
    private readonly IRepository<Ingredient> _ingredientRepo;
    private readonly IRepository<Cookbook> _cookbookRepo;

    public RecipeService(
        IRecipeFormatParser parser,
        IRepository<Recipe> recipeRepo,
        IRepository<Ingredient> ingredientRepo,
        IRepository<Cookbook> cookbookRepo)
    {
        _parser = parser;
        _recipeRepo = recipeRepo;
        _ingredientRepo = ingredientRepo;
        _cookbookRepo = cookbookRepo;
    }
}
```

CONVENTIONS lines 73-95: fields are `readonly`, prefixed `_camelCase`, assigned in ctor, never re-assigned.

---

### Errors as data (no throws on user input)

**Source:** `src/CookBot.Application/Services/RecipeFormatParser.cs:143-186` (`TryParse` returning `bool` + `out List<string> errors`).
**Apply to:** `RecipeValidator.Validate` (must return `ValidationResult`, never throw — D-08 explicit). The new parser's `TryParse` keeps the same shape.

**Throw-only-at-the-boundary contract** (CONVENTIONS lines 124-141):
- `InvalidOperationException` for "not found" / inconsistent state.
- `UnauthorizedAccessException` for ownership / permission failures (this phase doesn't add new authz surface).
- `FormatException` for malformed user input parsing in the inner `Parse` method only (`RecipeFormatParser.cs:38`).
- The outer `TryParse` catches and adds to `errors` (lines 181-185).

---

### File-scoped namespace + one public type per file

**Source:** every `.cs` file in `src/`, e.g. `src/CookBot.Domain/Entities/Recipe.cs:1`.
**Apply to:** every new file in `src/CookBot.Domain/Recipes/`, `src/CookBot.Application/Recipes/`, `src/CookBot.Infrastructure/Data/Migrations/Helpers/`.

```csharp
namespace CookBot.Domain.Recipes;

public sealed record RecipeDocument { ... }
```

**Exception:** `StepNode.cs` may group `StepNode` + `ContentStep` + `SectionStep` (a polymorphic union is a single concept) — the discretion list (CONTEXT.md line 82) explicitly allows this.

**Anti-pattern:** block-style `namespace { }` — only used by EF auto-generated migration files, never hand-written (CONVENTIONS line 46).

---

### Singleton lifetimes for stateless pure services

**Source:** `src/CookBot.Application/DependencyInjection.cs:11-12`.

```csharp
services.AddSingleton<IRecipeFormatParser, RecipeFormatParser>();
services.AddSingleton<IUnitConverter, UnitConversionService>();
```

**Apply to:** all 6 new services in `AddApplication()` and 1 new singleton in `AddInfrastructure()` (the `LegacyRecipeProjector` is the only `Scoped` one, because it's a one-shot DI consumer).

CONVENTIONS lines 179-183: Singleton for stateless pure helpers exposed through interfaces; Scoped for everything that touches DbContext or per-request state.

---

### Null-forgiving / null-coalescing patterns

**Source:**
- `src/CookBot.Application/Services/RecipeService.cs:28-29`: `?? throw new InvalidOperationException(...)`.
- `src/CookBot.Domain/Entities/Recipe.cs:15`: `public Cookbook Cookbook { get; set; } = null!;`.
- `src/CookBot.Domain/Entities/Recipe.cs:7`: `public string Name { get; set; } = string.Empty;`.

**Apply to:** every new domain record. Default required strings to `""`, lists to `new()`, optional nullables stay `null`.

---

### xUnit test class structure

**Source:** `tests/CookBot.Tests/Services/RecipeFormatParserTests.cs:6-9` (no constructor, plain `public class`, `[Fact]`/`[Theory]` per method).

```csharp
public class RecipeFormatParserTests
{
    private readonly RecipeFormatParser _parser = new();

    [Fact]
    public void Parse_StructuredYamlWithSteps_ReturnsSteps() { ... }
}
```

**For DB-backed tests:** add `IDisposable` and use the SQLite in-memory ctor pattern from `OwnershipTests.cs:10-22` (TESTING.md lines 119-152).

---

### Project structure mirror in tests

**Source:** TESTING.md lines 31-57.

| Source path | Test path |
|-------------|-----------|
| `src/CookBot.Domain/Recipes/*.cs` | `tests/CookBot.Tests/Recipes/*Tests.cs` |
| `src/CookBot.Application/Recipes/*.cs` | `tests/CookBot.Tests/Recipes/*Tests.cs` |
| `src/CookBot.Infrastructure/Data/Migrations/Helpers/LegacyRecipeProjector.cs` | `tests/CookBot.Tests/Migration/CanonicalBackfillTests.cs` |
| `src/CookBot.Application/Services/PromptBuilderService.cs` (modified) | `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` + `PromptDenylistTests.cs` |

The test folder structure mirrors **the kind of object under test**, not the namespace path of the source (TESTING.md line 57).

---

## No Analog Found

| File | Role | Reason | Fallback |
|------|------|--------|----------|
| `tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt` | text fixture | No `tests/Fixtures/` directory exists today; this introduces the convention | Use `seeds/ingredients.json` as a "committed text fixture in repo" precedent (STRUCTURE.md line 98-101). The csproj `<None Update Fixtures\**\*.* CopyToOutputDirectory>` line is the new convention (research line 1396-1403). |
| `src/CookBot.Domain/Recipes/StepNode.cs` polymorphic union | data | No polymorphic JSON in the codebase today; `[JsonPolymorphic]`/`[JsonDerivedType]` attrs are net-new | Use the verbatim research-prescribed shape at `01-RESEARCH.md:1351-1358` + `:274-405`. Microsoft Learn STJ polymorphism page is the secondary source [CITED in research]. |
| `tests/CookBot.Tests/TestHost.cs` (helper class) | test infra | No shared `TestHost`/`TestUtilities` class today; tests build state inline (TESTING.md line 186-200) | Build inline or as private nested static class first; promote to file only if 3+ tests depend on the same helper. Style: `OwnershipTests.cs:154-164` (private nested `StubRecipeFormatParser` is the in-tree precedent for sharing helpers across tests inside one file). |

---

## Metadata

**Analog search scope:**
- `src/CookBot.Domain/`
- `src/CookBot.Application/`
- `src/CookBot.Infrastructure/`
- `tests/CookBot.Tests/`
- `.planning/codebase/{ARCHITECTURE,STRUCTURE,CONVENTIONS,TESTING}.md`
- `.planning/phases/01-canonical-format-foundation/{01-CONTEXT,01-RESEARCH}.md`

**Files scanned:** 22 source files (read fully) + 6 codebase docs (selectively).

**Files explicitly read for excerpt extraction:**
- `src/CookBot.Application/Services/RecipeFormatParser.cs` (1-221)
- `src/CookBot.Application/Services/IngredientResolver.cs` (1-17)
- `src/CookBot.Application/Services/IngredientRefDetectionService.cs` (1-35)
- `src/CookBot.Application/Services/RecipeStepTextFormatter.cs` (1-65)
- `src/CookBot.Application/Services/RecipeService.cs` (1-165)
- `src/CookBot.Application/Services/PromptBuilderService.cs` (1-60, 160-300)
- `src/CookBot.Application/DependencyInjection.cs` (1-20)
- `src/CookBot.Application/DTOs/CookbookTransferDtos.cs` (1-51)
- `src/CookBot.Domain/Entities/Recipe.cs` (1-18)
- `src/CookBot.Domain/Entities/RecipeStep.cs` (1-10)
- `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs` (1-42)
- `src/CookBot.Infrastructure/Data/Repositories/Repository.cs` (1-43)
- `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` (1-137)
- `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` (1-29)
- `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs` (1-23)
- `src/CookBot.Infrastructure/DependencyInjection.cs` (1-27)
- `src/CookBot.Infrastructure/Migrations/20260416175214_AiApiKeyShares.cs` (1-91)
- `src/CookBot.Infrastructure/Migrations/20260416012530_AiEnabledDefaultFalse.cs` (1-36)
- `tests/CookBot.Tests/Services/RecipeFormatParserTests.cs` (1-55)
- `tests/CookBot.Tests/Services/UnitParserTests.cs` (1-40)
- `tests/CookBot.Tests/Services/IngredientRefDetectionServiceTests.cs` (1-48)
- `tests/CookBot.Tests/Services/OwnershipTests.cs` (1-80)
- `*.csproj` files (all 5 — for package + framework verification)

**Pattern extraction date:** 2026-04-25.
