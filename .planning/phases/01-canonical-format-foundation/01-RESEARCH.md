# Phase 1: Canonical Format Foundation - Research

**Researched:** 2026-04-25
**Domain:** .NET 10 / System.Text.Json polymorphic schema design + EF Core 10 SQLite migration mechanics + Anthropic Structured Outputs strict-mode JSON Schema requirements
**Confidence:** HIGH overall

> This research extends — does not duplicate — `.planning/research/SUMMARY.md` (build order), `.planning/research/STACK.md` (package selection), `.planning/research/ARCHITECTURE.md` (layering), and `.planning/research/PITFALLS.md` (C1–C7, H1–H10). The planner should treat those as the strategic backdrop and this file as the **code-level execution detail** for Phase 1's 20 requirements.

---

## Summary

Phase 1 establishes the `RecipeDocument` POCO record in `CookBot.Domain.Recipes` as the single source of truth for every recipe representation. Around it, the planner must build five collaborating singletons in `CookBot.Application.Recipes` (`RecipeJsonSchemaProvider`, `IRecipeSchemaDocumentationProvider` + impl, `RecipeValidator`, `RecipeUpcasterChain` + `Migration_V1_To_V2`, `JsonRecipeSerializer`), one `IDatabaseBackupService` in `CookBot.Infrastructure.Data`, one EF migration adding `Recipe.CanonicalDocumentJson`, and a `LegacyRecipeProjector` helper. The existing `IRecipeFormatParser` is rewritten to delegate to the new stack while keeping its public shape stable — every caller of `Parser.TryParse` (`PasteRawTextDialog`, `RecipeService.CreateFromTextAsync`, `AiChat.ExtractRecipeContent`, `RecipeCookingAiContext`) continues to compile unchanged.

The two key technical pivots — both verified — are: (1) `System.Text.Json.Schema.JsonSchemaExporter` in .NET 10 emits `anyOf` (not `oneOf`) for `[JsonPolymorphic]` types, which **is** supported by Anthropic strict-mode; (2) STJ does **not** emit `additionalProperties: false` by default, so `RecipeJsonSchemaProvider` must walk the produced `JsonNode` tree post-export and inject it on every object. Single new NuGet: `JsonSchema.Net 9.2.0`. No new YAML library; the V1→V2 path is YAML → `Dictionary<object, object>` via existing YamlDotNet 16.3.0 → `JsonNode` via in-tree adapter.

**Primary recommendation:** Land everything in build-step order (see `SUMMARY.md §3` step 1→6) but with these phase-specific additions: (a) the schema provider self-validates its own output via `JsonSchema.Net` at startup as a smoke test, (b) `IDatabaseBackupService.BackupBeforeMigrationAsync` uses `Microsoft.Data.Sqlite.SqliteConnectionStringBuilder.DataSource` to extract the file path (NOT string parsing), (c) the `DatabaseSeeder` insertion order is **`HasPendingMigrationsAsync` → `BackupBeforeMigrationAsync` → `MigrateAsync` → backfill loop** — backup conditional on pending work to avoid cold-start-with-no-migrations backups.

---

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FORMAT-01 | Canonical `RecipeDocument` record in `CookBot.Domain/Recipes/` | §"Canonical record shape" |
| FORMAT-02 | `int Version` at root, bumped 1→2 | §"Canonical record shape" + Anthropic schema requires top-level type literal `enum: [2]` for writes |
| FORMAT-03 | Quantity field names include units (`prepTimeMinutes` etc.) | §"Field naming + V1→V2 upcaster" |
| FORMAT-04 | `abstract record StepNode` + `ContentStep` + `SectionStep` via `[JsonPolymorphic]` | §"Polymorphic step union" + verified anyOf emission |
| FORMAT-05 | `[name](#id)` is the only ingredient-link source; substring fallback removed from `IngredientRefDetectionService` | §"Removing the substring fallback" (lines `IngredientRefDetectionService.cs:23-31`) |
| FORMAT-06 | `RecipeJsonSchemaProvider` via `JsonSchemaExporter` with `additionalProperties: false` post-processing | §"Schema provider implementation" |
| FORMAT-07 | `RecipeValidator` returns `ValidationResult` data, never throws | §"Validator + two-tier policy" |
| FORMAT-08 | `IRecipeUpcaster` chain at JSON-node layer; `Migration_V1_To_V2` reconciles divergences | §"Upcaster chain" |
| FORMAT-09 | Forward-compat `Extras` round-trip on `RecipeDocument`, `ContentStep`, `SectionStep`, `IngredientEntry` | §"Extras propagation through polymorphism" |
| FORMAT-10 | Round-trip test suite with 5+ fixtures | §"Fixture-driven test pattern" |
| AI-04 | Opt-out clause removed from `PromptBuilderService` | §"Prompt consolidation" (line `PromptBuilderService.cs:201`, `:295`) |
| AI-05 | `RecipeSchemaDocumentationProvider` consolidates the format spec | §"Prompt consolidation" + DI insertion shape |
| AI-06 | Snapshot test + lint denylist | §"Snapshot test pattern" + denylist regex |
| MIGRATION-01 | EF migration adds `Recipe.CanonicalDocumentJson`; `DatabaseSeeder` back-fills | §"Schema migration mechanics" + §"DatabaseSeeder ordering" |
| MIGRATION-02 | Pre-migration backup with last-3 retention | §"Backup service implementation" |
| MIGRATION-03 | Hybrid persistence preserved | §"Recipe entity changes" |
| MIGRATION-05 | `CookbookTransferDocument.SchemaVersion` bumped to 2; two-axis versioning documented | §"Envelope version bump" |
| MIGRATION-07 | Idempotent backfill (`WHERE CanonicalDocumentJson IS NULL`) | §"DatabaseSeeder ordering" |
| MIGRATION-08 | Smoke test: representative DB → project → serialize → parse → validate, no value drift | §"Smoke test pattern" |
| POLISH-02 | Delete duplicated format-spec literals at `:168-202` and `:262-296` | §"Prompt consolidation" |

---

## User Constraints (from CONTEXT.md)

### Locked Decisions

The 25 decisions D-01 through D-25 in `.planning/phases/01-canonical-format-foundation/01-CONTEXT.md` are locked. Highlights affecting research:

- **D-01:** New namespace `CookBot.Domain.Recipes` lives inside `CookBot.Domain` — no new project. Pure POCO records, no `<PackageReference>` additions to `CookBot.Domain.csproj`.
- **D-02:** `abstract record StepNode` + concrete `ContentStep(Text, Timers)` + `SectionStep(Heading)`. Discriminator property name `kind`, values `"content"` and `"section"`. **No `IsSection` boolean carried into the canonical record.**
- **D-03:** `prepTimeMinutes`, `cookTimeMinutes`, `ovenTempFahrenheit` (latter ships in Phase 4).
- **D-04:** `int Version` at top of root. Schema constrains writes to `enum: [2]`; reads accept any `int >= 1` and route through upcaster chain.
- **D-05:** `[JsonExtensionData] Dictionary<string, JsonElement> Extras` on `RecipeDocument`, `ContentStep`, `SectionStep`, `IngredientEntry`.
- **D-06:** `id` (renamed from `localId` in JSON exports) is the per-recipe local id, immutable, never user-visible. Substring-match fallback in `IngredientRefDetectionService` is **deleted** (FORMAT-05).
- **D-07:** `RecipeJsonSchemaProvider` is singleton; uses `JsonSchemaExporter.GetJsonSchemaAsNode`; post-walks the `JsonNode` setting `additionalProperties: false` on every object schema. Cached behind `Lazy<JsonNode>`.
- **D-09:** `IRecipeUpcaster` interface with `int FromVersion`, `int ToVersion`, `JsonNode Upcast(JsonNode input)`. `RecipeUpcasterChain` reads all `IRecipeUpcaster` registrations from DI (no reflection scanning), sorts by `FromVersion`. **`Migration_V1_To_V2` is the only concrete upcaster this milestone.**
- **D-12:** `Recipe.CanonicalDocumentJson: string?` is a plain EF string column, **NOT** `OwnsOne`/`OwnsMany`. Hybrid persistence — relational columns kept.
- **D-15:** `IDatabaseBackupService` with single method `BackupBeforeMigrationAsync(string migrationName, CancellationToken ct)`. Last-3 retention via `Directory.GetFiles(dir, "{name}.pre-*.bak")` ordered by `LastWriteTimeUtc` desc.
- **D-21:** Hand-rolled snapshot test, no Verify/ApprovalTests.
- **D-22:** Lint denylist via xUnit grep test on source file.
- **D-23/D-24:** Round-trip fixtures driven by `[Theory]` + `[MemberData]` + `Directory.GetFiles`.

### Claude's Discretion

Per CONTEXT.md `<decisions>` block, the planner has discretion over:
- File names within `CookBot.Domain/Recipes/` (one file per record vs grouped).
- `[Fact]` vs `[Theory]` choices for non-fixture tests.
- Where to place the upcaster-chain version-gap-startup-check (recommended yes).
- Whether `JsonRecipeSerializer` exposes both `Serialize` and `SerializeIndented` (recommended: indented for export, compact for DB column).
- Specific log levels for backup/migration events.
- Whether to add a thin `Recipe.TagsJson` helper this phase or wait (Phase 4 owns the full fix).

### Deferred Ideas (OUT OF SCOPE)

Per CONTEXT.md `<deferred>`:
- `Recipe.TagsJson` → relational table — Phase 4 (POLISH-04).
- Dropping `Recipe.IngredientRefs` column — Phase 4. **This phase stops writing it.**
- `AiChat.ExtractRecipeContent` deletion — Phase 2 (POLISH-01).
- `CookbookTransferService.Deserialize` upcaster routing — Phase 2 (MIGRATION-04). **Phase 1 only bumps the constant.**
- Old YAML pastes through upcaster chain in `IRecipeFormatParser` — Phase 2 (MIGRATION-06). **Phase 1 wires the chain in DI but the parser rewrite focuses on canonical record + parse + validate.**
- `RedactSecrets` chokepoint — Phase 2 (AI-07).
- `<recipe>` XML wrapping — Phase 2 (AI-08).
- Encrypt-at-rest for `UserProfile.AiApiKey` — FUTURE-01.
- MudBlazor 9.x, Cooklang as canonical — out of scope.

---

## Project Constraints (from CLAUDE.md)

These directives carry the same authority as locked decisions. Tasks that contradict them are non-compliant.

- **Don't introduce a second AI provider abstraction or pull `Microsoft.Extensions.AI` / official `Anthropic` NuGet.** The existing `HttpClient` in `AnthropicAiService` stays. (Affects Phase 2 mostly, but Phase 1 must not lay groundwork that contradicts this — e.g. don't shape `JsonRecipeSerializer` around an MEAI `IChatClient` consumer.)
- **Don't add `Newtonsoft.Json` / `NJsonSchema`.** App is 100% System.Text.Json. **Phase 1 adds exactly one package: `JsonSchema.Net`** (D-15 / `STACK.md §4`).
- **Don't add a `CookBot.Schemas` project.** `RecipeDocument` is pure POCO; lives in `CookBot.Domain/Recipes/` (D-01).
- **Don't auto-scale temperatures, prep times, or cook times.** Only `RecipeIngredient.Amount` scales. (Phase 4-relevant; Phase 1 doesn't introduce scaling but documentation in `RecipeSchemaDocumentationProvider` must not imply otherwise.)
- **Don't reintroduce a "free-form / numbered-list fallback" escape hatch in the AI prompt.** This is the opt-out clause AI-04 removes. The lint denylist (D-22) defends against regression.
- **Nullable reference types and implicit usings are enabled** in every project. New files use `#nullable enable` (project default) and rely on implicit usings.
- **Repositories vs DbContext direct access:** `DatabaseSeeder` already bypasses `IRepository<T>` and uses `CookBotDbContext` directly. The backfill loop follows that pattern.
- **Authorization** is enforced inside services (RecipeService, CookbookService), not middleware. Phase 1 doesn't change this; new DI-registered services are `Singleton` for pure ones (`IRecipeFormatParser` is already singleton — see `DependencyInjection.cs:11`).

---

## Architectural Responsibility Map

The Phase 1 capabilities map to the Clean/Onion tiers as follows. Every task assignment must respect these boundaries — anything misassigned (e.g. validator referencing EF) breaks the dependency direction.

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `RecipeDocument`, `StepNode`, `ContentStep`, `SectionStep`, `IngredientEntry`, `TimerEntry` records | `CookBot.Domain` | — | Pure POCOs with `[JsonPolymorphic]`/`[JsonExtensionData]` attributes from `System.Text.Json.Serialization`, which is BCL — no new `<PackageReference>` to `CookBot.Domain.csproj`. |
| `IRecipeSchemaDocumentationProvider`, `RecipeJsonSchemaProvider`, `RecipeValidator`, `IRecipeUpcaster`, `RecipeUpcasterChain`, `Migration_V1_To_V2`, `JsonRecipeSerializer` | `CookBot.Application` | — | Pure logic, no EF/HTTP. Registers via `AddApplication()` (`src/CookBot.Application/DependencyInjection.cs:9`). |
| `IRecipeFormatParser` (interface) | `CookBot.Domain` | — | Already lives there (`src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs`). Public shape preserved. |
| `RecipeFormatParser` (implementation, rewritten) | `CookBot.Application` | — | Already there (`src/CookBot.Application/Services/RecipeFormatParser.cs`). Rewrite delegates to `RecipeUpcasterChain` + `JsonRecipeSerializer` + `RecipeValidator`. |
| `PromptBuilderService` consolidation | `CookBot.Application` | — | Existing class at `src/CookBot.Application/Services/PromptBuilderService.cs`. Constructor gains `IRecipeSchemaDocumentationProvider` parameter. |
| `IDatabaseBackupService` + impl, `LegacyRecipeProjector` | `CookBot.Infrastructure` | — | Touches files (`File.Copy`) and the `Recipe` aggregate via `CookBotDbContext`. Must NOT live in `Application` — that would force a project reference inversion. |
| EF migration `<timestamp>_RecipeCanonicalDocument`, `RecipeConfiguration` change | `CookBot.Infrastructure` | — | Lives in `src/CookBot.Infrastructure/Migrations/`. `dotnet ef` command uses `--startup-project src/CookBot.Web`. |
| `DatabaseSeeder.SeedAsync` modifications | `CookBot.Infrastructure` | — | Existing file at `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs`. New backup + backfill steps wrap `MigrateAsync()`. |
| `CookbookTransferDocument.SchemaVersion = 2` | `CookBot.Application` | — | Single-line change to constant in `src/CookBot.Application/DTOs/CookbookTransferDtos.cs:6`. Deserialize hot path stays unchanged this phase. |
| Round-trip / migration / prompt tests | `CookBot.Tests` | references `Application`, `Infrastructure`, `Domain` | Existing project references already cover all three (`tests/CookBot.Tests/CookBot.Tests.csproj:22-26`). |
| Test fixtures (YAML, JSON, prompt expected) | `CookBot.Tests/Fixtures/` | — | Plain text files committed to repo. No xUnit `IClassFixture` needed. |

**Misassignment risks the planner should sanity-check:**
- A task that puts `RecipeJsonSchemaProvider` in `Domain` is wrong — schema generation needs `System.Text.Json.Schema.JsonSchemaExporter` (BCL, technically usable from Domain, but Domain has zero `<PackageReference>` and zero singleton-DI registrations; keep schema concerns in Application).
- A task that puts `IDatabaseBackupService` in `Application` is wrong — it needs `File.Copy` and a `SqliteConnectionStringBuilder`, neither of which fit Domain/Application's "no infrastructure" posture.
- A task that adds the EF migration via `dotnet ef migrations add ... --project src/CookBot.Application` is wrong — EF Design lives in `CookBot.Infrastructure` and `CookBot.Web` (`src/CookBot.Infrastructure/CookBot.Infrastructure.csproj:9`).

---

## Standard Stack

### Core (already in tree)

| Library | Version | Purpose | Why standard |
|---------|---------|---------|--------------|
| .NET 10 BCL | 10.0.107 (verified `dotnet --version`) | `System.Text.Json.Schema.JsonSchemaExporter` for FORMAT-06 | BCL — zero new packages [VERIFIED: Microsoft Learn `learn.microsoft.com/en-us/dotnet/api/system.text.json.schema.jsonschemaexporter`] |
| `System.Text.Json` | BCL .NET 10 | Polymorphic serialization, extension data, JsonNode tree | Already used everywhere; project is "100% STJ" per CLAUDE.md |
| `YamlDotNet` | 16.3.0 | YAML→`Dictionary<object,object>` for paste-in path | Already in `CookBot.Application.csproj:10`. **No upgrade.** |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.* | EF migration `RecipeCanonicalDocument` | Already in `CookBot.Infrastructure.csproj:13` |
| `Microsoft.Data.Sqlite` | transitive via EF Sqlite | `SqliteConnectionStringBuilder` for backup-service path resolution | Transitive — no new package |
| xUnit | 2.9.2 | Test framework | `tests/CookBot.Tests/CookBot.Tests.csproj:14` |

### New (single addition)

| Library | Version | Purpose | License |
|---------|---------|---------|---------|
| `JsonSchema.Net` | 9.2.0 | Runtime JSON Schema 2020-12 validation. Used in Phase 2 hot path; **registered in Phase 1 so `RecipeJsonSchemaProvider` can self-validate the schema it generates at startup as a smoke test.** | MIT [VERIFIED: nuget.org/packages/JsonSchema.Net, 9.2.0 published 2026-04-14] — GPL-3.0 compatible |

**Verified install command:**

```bash
dotnet add src/CookBot.Application package JsonSchema.Net --version 9.2.*
```

`JsonSchema.Net` 9.2.0 targets `net8.0` and `netstandard2.0` primary, computed for `net9.0`/`net10.0` [CITED: nuget.org/packages/JsonSchema.Net]. Dependency: only `JsonPointer.Net >= 7.0.1`. Main namespaces: `Json.Schema`, `Json.Schema.Generation` (we don't use generation since `JsonSchemaExporter` is BCL).

**Version verification:** `JsonSchemaExporter` itself ships in `System.Text.Json` v11.0.0-preview.3.26207.106 inside the .NET 10 SDK [CITED: Microsoft Learn JsonSchemaExporter Class page, Package field]. Available without a NuGet add since the SDK already includes `System.Text.Json` in `Microsoft.NETCore.App` shared framework.

### Alternatives Considered (and Rejected)

| Instead of | Could Use | Why Not |
|------------|-----------|---------|
| `JsonSchemaExporter` (BCL) | `Corvus.JsonSchema`, `NJsonSchema`, `JsonSchema.Net.Generation` | Adds package; we get it free in BCL. `JsonSchema.Net.Generation` would force `[Required]`/`[JsonRequired]` semantics that fight `[JsonExtensionData]`. |
| In-tree YAML→Dict→JsonNode adapter | `YamlDotNet.System.Text.Json` 1.7.1 | Adds a 2nd package and conflicts with D-15 (one-package rule). The in-tree adapter is ~20 lines, see §"YAML → JsonNode" below. |
| `File.Copy` for backups | `SqliteConnection.BackupDatabase(dest)` | `BackupDatabase` is the right tool when the DB has open connections; ours doesn't (backup runs **before** `MigrateAsync` opens the connection). `File.Copy` is simpler and `BackupDatabase` blocks writers anyway [CITED: dotnet/efcore#13834]. |
| Hand-rolled snapshot test | `Verify` or `ApprovalTests` | Per D-21: 1 string equality + 1 fixture file is small enough to not justify a new package. |

---

## Architecture Patterns

### System Architecture (data flow through Phase 1 components)

```
                       ┌──────────────────────────────────┐
                       │ RecipeDocument (POCO record)     │
                       │   CookBot.Domain.Recipes         │
                       │   - int Version                  │
                       │   - Extras: Dict<string, JsonEl> │
                       │   - StepNode polymorphic union   │
                       └─────────────┬────────────────────┘
                                     │ source for projections
        ┌────────────────────────────┼────────────────────────────────┐
        ▼                            ▼                                ▼
  ┌──────────────┐          ┌────────────────────┐          ┌─────────────────────┐
  │ JsonRecipe-  │          │ RecipeJsonSchema-  │          │ RecipeSchemaDocu-   │
  │ Serializer   │          │ Provider           │          │ mentationProvider   │
  │ (Serialize/  │          │ (JsonSchemaExporter│          │ (.GetFormatPrompt()) │
  │  Deserialize)│          │  + walk node tree  │          │ — used by both AI   │
  └──────┬───────┘          │  setting addProps  │          │   prompt sites      │
         │                  │  false everywhere) │          └──────────┬──────────┘
         │                  └─────────┬──────────┘                     │
         │                            │                                │
         ▼                            ▼                                ▼
  ┌──────────────┐          ┌────────────────────┐          ┌─────────────────────┐
  │ Recipe.      │          │ JSON Schema 2020-12│          │ PromptBuilder-      │
  │ Canonical-   │          │ — fed to:          │          │ Service.            │
  │ DocumentJson │          │  - JsonSchema.Net  │          │ ResolveRecipeFormat │
  │ (DB column)  │          │    runtime validate│          │ + BuildCopyable-    │
  └──────────────┘          │  - Phase 2 wires   │          │ Prompt              │
                            │    to Anthropic    │          │ (lines 168-202 and  │
                            │    output_config   │          │  262-296 DELETED)   │
                            └────────────────────┘          └─────────────────────┘

  inbound (parse path):
    YAML/JSON text ─► Detect format ─► (YAML→JsonNode adapter | JsonNode.Parse)
                  ─► Stamp version=1 if absent ─► RecipeUpcasterChain.UpcastToCurrent
                  ─► Migration_V1_To_V2 (rewrites JSON-level fields)
                  ─► JsonSerializer.Deserialize<RecipeDocument>
                  ─► RecipeValidator.Validate (returns errors as data)
                  ─► ParsedRecipe (back-compat projection for legacy callers)

  startup path (DatabaseSeeder.SeedAsync):
    HasPendingMigrationsAsync? ─yes─► IDatabaseBackupService.BackupBefore-
                                       MigrationAsync("RecipeCanonicalDocument")
                                       (File.Copy + last-3 retention)
                                ─► MigrateAsync()
                                ─► WHERE CanonicalDocumentJson IS NULL
                                ─► LegacyRecipeProjector.Project(recipe)
                                ─► JsonRecipeSerializer.Serialize
                                ─► SaveChangesAsync (batched in 50)
```

### Recommended Project Structure (additions only — files this phase creates)

```
src/CookBot.Domain/Recipes/                                    # NEW namespace
├── RecipeDocument.cs                                          # NEW — root record
├── StepNode.cs                                                # NEW — abstract record + ContentStep + SectionStep
├── IngredientEntry.cs                                         # NEW — record
└── TimerEntry.cs                                              # NEW — record

src/CookBot.Application/Recipes/                               # NEW folder
├── IRecipeSchemaDocumentationProvider.cs                      # NEW interface
├── RecipeSchemaDocumentationProvider.cs                       # NEW impl
├── RecipeJsonSchemaProvider.cs                                # NEW — singleton, Lazy<JsonNode>
├── RecipeValidator.cs                                         # NEW — returns ValidationResult
├── ValidationResult.cs                                        # NEW (or grouped in Validator file)
├── IRecipeUpcaster.cs                                         # NEW interface
├── RecipeUpcasterChain.cs                                     # NEW — DI-fed list, sorted
├── Migration_V1_To_V2.cs                                      # NEW — only concrete upcaster
└── JsonRecipeSerializer.cs                                    # NEW — wraps JsonSerializerOptions

src/CookBot.Infrastructure/Data/
├── IDatabaseBackupService.cs                                  # NEW interface
├── DatabaseBackupService.cs                                   # NEW impl
└── Migrations/Helpers/LegacyRecipeProjector.cs                # NEW — DELETE-AFTER-V1.1

src/CookBot.Infrastructure/Migrations/
└── <timestamp>_RecipeCanonicalDocument.cs                     # NEW — generated

tests/CookBot.Tests/
├── Recipes/                                                   # NEW folder
│   ├── RecipeDocumentRoundTripTests.cs                        # [Theory] + [MemberData] + Directory.GetFiles
│   ├── RecipeValidatorTests.cs
│   ├── RecipeUpcasterTests.cs
│   ├── RecipeJsonSchemaProviderTests.cs                       # asserts addProps:false everywhere
│   └── ExtrasRoundTripTests.cs                                # FORMAT-09 / Pitfall H4
├── Migration/CanonicalBackfillTests.cs                        # MIGRATION-08
├── Prompts/
│   ├── PromptSnapshotTests.cs                                 # AI-06 D-21
│   └── PromptDenylistTests.cs                                 # AI-06 D-22
└── Fixtures/
    ├── Recipes/v1-yaml/*.yaml
    ├── Recipes/v1-json-export/*.json
    ├── Recipes/v1-db-projections/*.json
    ├── Recipes/v2-canonical/*.json
    └── Prompts/expected-system-prompt.txt
```

### Pattern 1: Canonical record shape

**Source:** D-01 through D-06; verified against [CITED: learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism] for discriminator semantics.

```csharp
// src/CookBot.Domain/Recipes/RecipeDocument.cs
namespace CookBot.Domain.Recipes;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record RecipeDocument
{
    [JsonPropertyName("version")]
    public required int Version { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("servings")]
    public int Servings { get; init; } = 1;

    [JsonPropertyName("prepTimeMinutes")]
    public int? PrepTimeMinutes { get; init; }

    [JsonPropertyName("cookTimeMinutes")]
    public int? CookTimeMinutes { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    [JsonPropertyName("ingredients")]
    public IReadOnlyList<IngredientEntry> Ingredients { get; init; } = [];

    [JsonPropertyName("steps")]
    public IReadOnlyList<StepNode> Steps { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extras { get; init; } = new();
}
```

```csharp
// src/CookBot.Domain/Recipes/StepNode.cs
namespace CookBot.Domain.Recipes;

using System.Text.Json;
using System.Text.Json.Serialization;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ContentStep), typeDiscriminator: "content")]
[JsonDerivedType(typeof(SectionStep), typeDiscriminator: "section")]
public abstract record StepNode;

public sealed record ContentStep : StepNode
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("timers")]
    public IReadOnlyList<TimerEntry>? Timers { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extras { get; init; } = new();
}

public sealed record SectionStep : StepNode
{
    [JsonPropertyName("heading")]
    public required string Heading { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extras { get; init; } = new();
}
```

```csharp
// src/CookBot.Domain/Recipes/IngredientEntry.cs
namespace CookBot.Domain.Recipes;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record IngredientEntry
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }       // per-recipe local id; was `localId` in v1

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("amount")]
    public double Amount { get; init; }

    [JsonPropertyName("unit")]
    public string Unit { get; init; } = "";

    [JsonPropertyName("note")]
    public string? Note { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extras { get; init; } = new();
}
```

```csharp
// src/CookBot.Domain/Recipes/TimerEntry.cs
namespace CookBot.Domain.Recipes;

using System.Text.Json.Serialization;

public sealed record TimerEntry
{
    [JsonPropertyName("duration")]
    public required int Duration { get; init; }

    [JsonPropertyName("unit")]
    public string Unit { get; init; } = "min";

    [JsonPropertyName("label")]
    public string? Label { get; init; }
}
```

**Discriminator semantics verified [CITED: learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism]:**
- The discriminator is emitted **first** in the JSON object (grouped with metadata properties like `$id`, `$ref`).
- The `[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]` overrides the default `$type` to use `kind`.
- Discriminator values are arbitrary strings — `"content"` / `"section"` are valid.
- Round-tripping requires the type discriminator (deserializing `WeatherForecastWithCity` JSON without `$type` would just produce `WeatherForecastBase`).
- `[JsonExtensionData]` on derived types **does** capture unknown fields scoped to that derived shape — confirmed working in STJ since .NET 7.
- Note: AllowOutOfOrderMetadataProperties may be needed if external tools place the discriminator mid-object. STJ's own writer always emits it first, so we don't need to set this — but Phase 2's Anthropic-emitted JSON might. Defer that to Phase 2.

### Pattern 2: Schema provider implementation

**Source:** D-07; verified against [CITED: learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/extract-schema] and [CITED: platform.claude.com/docs/en/build-with-claude/structured-outputs].

```csharp
// src/CookBot.Application/Recipes/RecipeJsonSchemaProvider.cs
namespace CookBot.Application.Recipes;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using CookBot.Domain.Recipes;

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
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
        };
        var node = serializerOptions.GetJsonSchemaAsNode(typeof(RecipeDocument), exporterOptions);
        SetAdditionalPropertiesFalse(node);
        return node;
    }

    /// <summary>Walks every object schema node in the tree and sets additionalProperties: false.
    /// Required by Anthropic Structured Outputs strict mode — STJ does not emit this by default.</summary>
    private static void SetAdditionalPropertiesFalse(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            // STJ emits {"type": "object"} or {"type": ["object", "null"]} on object schemas.
            if (HasObjectType(obj))
            {
                if (obj["additionalProperties"] is null)
                    obj["additionalProperties"] = false;
            }
            foreach (var kvp in obj.ToList())
                SetAdditionalPropertiesFalse(kvp.Value);
        }
        else if (node is JsonArray arr)
        {
            foreach (var child in arr)
                SetAdditionalPropertiesFalse(child);
        }
    }

    private static bool HasObjectType(JsonObject obj)
    {
        if (obj["type"] is JsonValue v && v.TryGetValue<string>(out var s))
            return s == "object";
        if (obj["type"] is JsonArray a)
            return a.Any(x => x is JsonValue v2 && v2.TryGetValue<string>(out var s2) && s2 == "object");
        // anyOf branches and properties dictionaries also contain object subschemas without type at top level
        return obj.ContainsKey("properties");
    }
}
```

**Verified facts driving this code [CITED: learn.microsoft.com/.../extract-schema]:**
- API entry: `JsonSerializerOptions.GetJsonSchemaAsNode(Type, JsonSchemaExporterOptions?)`. Returns `JsonNode`.
- `TreatNullObliviousAsNonNullable = true` matches the project's `<Nullable>enable</Nullable>` posture (every non-`?` reference type is non-nullable).
- Without the option, the exporter emits `"type": ["object", "null"]` for the root — confusing for Anthropic strict mode. With it, root is `"type": "object"`.
- The exporter does **NOT** emit `additionalProperties: false` by default (the Microsoft Learn example only shows it appearing when `JsonUnmappedMemberHandling.Disallow` is set on `JsonSerializerOptions`). Either approach works:
  - Option A (preferred — the post-walk shown above): explicitly walk the tree. Visible, testable.
  - Option B (alternative): set `UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow` on the `JsonSerializerOptions`. This emits `additionalProperties: false` automatically — **but** it also rejects unknown members at deserialization, which contradicts FORMAT-09 (`Extras` round-trip). **Pick Option A.**

**Polymorphic discriminator output [CITED: GitHub `dotnet/runtime/src/libraries/System.Text.Json/src/System/Text/Json/Schema/JsonSchemaExporter.cs`, verified via WebFetch]:**
- The exporter emits `"anyOf": [...]` for `[JsonPolymorphic]` types — **NOT** `oneOf`.
- This matters because Anthropic strict mode supports `anyOf` and `allOf` but **does NOT support `oneOf`** [CITED: platform.claude.com/docs/en/build-with-claude/structured-outputs]. We get the right shape for free.
- Each branch's discriminator property is added with a `"const"` constraint matching that derived type's discriminator value.
- When the discriminator property exists on the base, it appears in the base properties list; if all derived types share one it's marked required.

**Anthropic strict-mode constraints to verify in the schema [CITED: platform.claude.com docs]:**
- ✅ `additionalProperties: false` on every object — handled by the post-walk.
- ✅ `anyOf` is allowed (not `oneOf`) — STJ already emits `anyOf`.
- ❌ Numerical constraints (`minimum`, `maximum`, `multipleOf`) — **don't add them**.
- ❌ String constraints (`minLength`, `maxLength`, `pattern`) — **don't add them**.
- ❌ Recursive schemas — `RecipeDocument` is acyclic, fine.
- ❌ External `$ref` — the exporter uses internal `$ref`/`$defs` for repeated types, which IS allowed.
- ✅ `enum` (strings/numbers/bools/nulls only) — fine for Version's `enum: [2]` constraint.
- ✅ `const` — fine for the discriminator constraint that STJ's anyOf branches emit.
- ✅ Array `minItems` only with values 0 or 1 — STJ doesn't emit minItems.
- The `version` field constraint to `enum: [2]` for *writes* (Anthropic schema) but *reads* accept any int — handled by sending the schema only on Phase 2's structured-output requests; the in-memory deserializer accepts any int.

**Schema self-validation at startup (recommended addition):**

Register `JsonSchema.Net` so the provider's tests can validate sample documents against the schema. The provider itself can run a one-time self-check:

```csharp
// in RecipeJsonSchemaProvider constructor (or factory method)
// Optional smoke test — fail fast if schema generation broke
var sample = new RecipeDocument { Version = 2, Name = "smoke-test", Steps = [], Ingredients = [] };
var sampleJson = JsonNode.Parse(JsonSerializer.Serialize(sample))!;
var jsonSchema = Json.Schema.JsonSchema.FromText(_schema.Value.ToJsonString());
var result = jsonSchema.Evaluate(sampleJson);
if (!result.IsValid) throw new InvalidOperationException("RecipeJsonSchemaProvider self-check failed: " + result);
```

(Discretion item: the planner can decide to make this conditional on Debug builds or run-once at startup via DI.)

### Pattern 3: Validator + two-tier policy

**Source:** D-08, FORMAT-07, Pitfall H9 from `PITFALLS.md`.

```csharp
// src/CookBot.Application/Recipes/ValidationResult.cs
namespace CookBot.Application.Recipes;

public sealed record ValidationResult(
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ValidationWarning> Warnings)
{
    public bool IsValid => Errors.Count == 0;
    public static ValidationResult Empty { get; } = new([], []);
}

public sealed record ValidationError(string Path, string Code, string Message);
public sealed record ValidationWarning(string Path, string Code, string Message);
```

```csharp
// src/CookBot.Application/Recipes/RecipeValidator.cs
namespace CookBot.Application.Recipes;

using System.Text.RegularExpressions;
using CookBot.Domain.Recipes;

public sealed class RecipeValidator
{
    // Same pattern as IngredientRefDetectionService.MarkdownLinkPattern
    private static readonly Regex IngredientLink = new(
        @"\[([^\]]+)\]\(#(\d+)\)", RegexOptions.Compiled);

    public ValidationResult Validate(RecipeDocument doc)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();   // currently unused at this layer; parser produces warnings

        if (string.IsNullOrWhiteSpace(doc.Name))
            errors.Add(new("name", "required", "Recipe name is required."));
        if (doc.Servings <= 0)
            errors.Add(new("servings", "positive", "Servings must be > 0."));

        // Ingredient id uniqueness (closes Pitfall C2/C3 + ParsedRecipe.Ids existing rule at RecipeFormatParser.cs:172)
        var ids = doc.Ingredients.Select(i => i.Id).ToList();
        if (ids.Count != ids.Distinct().Count())
            errors.Add(new("ingredients", "uniqueId", "Ingredient ids must be unique within a recipe."));

        // Step rules
        for (int i = 0; i < doc.Steps.Count; i++)
        {
            var step = doc.Steps[i];
            switch (step)
            {
                case ContentStep content:
                    foreach (Match m in IngredientLink.Matches(content.Text))
                    {
                        if (!int.TryParse(m.Groups[2].Value, out var refId) || !ids.Contains(refId))
                            errors.Add(new($"steps[{i}].text", "danglingRef",
                                $"Step references ingredient #{m.Groups[2].Value} which is not in ingredients."));
                    }
                    break;

                case SectionStep section:
                    if (string.IsNullOrWhiteSpace(section.Heading))
                        errors.Add(new($"steps[{i}].heading", "required", "Section heading is required."));
                    // Enforced by the type system: SectionStep has no Timers, no Text, no IngredientRefs.
                    // Closes Pitfall C3.
                    break;
            }
        }

        return new ValidationResult(errors, warnings);
    }
}
```

**Two-tier policy (D-08):**

| Tier | Where | Behavior |
|------|-------|----------|
| Schema-strict | `JsonSchema.Net` runtime check + Anthropic constrained decoding (Phase 2) | Storage + AI output gate |
| Lenient parser-level coercion | Inside `RecipeFormatParser.TryParse` rewrite | Pre-deserialize: stamp version, run upcaster (handles `prepTime` → `prepTimeMinutes`, `IsSection: true` + `Text` → `kind: section, heading`, `localId` → `id`). Coercion is logged as a `ValidationWarning`. |
| Semantic (this validator) | After deserialize | Errors only for things the type system can't catch: dangling refs, duplicate ids, empty section heading. **Never throws** (FORMAT-07). |

The parser layer is responsible for coercion. The validator (this class) is for invariants that must hold post-deserialize.

### Pattern 4: Upcaster chain

**Source:** D-09, FORMAT-08, `ARCHITECTURE.md §"Pattern 2"`.

```csharp
// src/CookBot.Application/Recipes/IRecipeUpcaster.cs
namespace CookBot.Application.Recipes;

using System.Text.Json.Nodes;

public interface IRecipeUpcaster
{
    int FromVersion { get; }
    int ToVersion { get; }
    JsonNode Upcast(JsonNode input);
}
```

```csharp
// src/CookBot.Application/Recipes/RecipeUpcasterChain.cs
namespace CookBot.Application.Recipes;

using System.Text.Json.Nodes;

public sealed class RecipeUpcasterChain
{
    public const int CurrentVersion = 2;

    private readonly IReadOnlyList<IRecipeUpcaster> _upcasters;

    public RecipeUpcasterChain(IEnumerable<IRecipeUpcaster> upcasters)
    {
        // Sort by FromVersion; reject gaps at construction (Claude's discretion item — recommended yes per CONTEXT.md)
        _upcasters = upcasters.OrderBy(u => u.FromVersion).ToList();
        for (int i = 0; i < _upcasters.Count - 1; i++)
        {
            if (_upcasters[i].ToVersion != _upcasters[i + 1].FromVersion)
                throw new InvalidOperationException(
                    $"Upcaster chain has a gap: {_upcasters[i].ToVersion} → {_upcasters[i + 1].FromVersion}");
        }
    }

    public JsonNode UpcastToCurrent(JsonNode input)
    {
        var node = input;
        var version = node["version"]?.GetValue<int>() ?? 1;
        foreach (var upcaster in _upcasters)
        {
            if (version == CurrentVersion) break;
            if (upcaster.FromVersion != version) continue;
            node = upcaster.Upcast(node);
            version = upcaster.ToVersion;
        }
        if (version > CurrentVersion)
            throw new InvalidOperationException(
                $"Recipe version {version} is newer than current ({CurrentVersion}). Update the app.");
        return node;
    }
}
```

```csharp
// src/CookBot.Application/Recipes/Migration_V1_To_V2.cs
namespace CookBot.Application.Recipes;

using System.Text.Json.Nodes;

public sealed class Migration_V1_To_V2 : IRecipeUpcaster
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public JsonNode Upcast(JsonNode input)
    {
        var obj = input.AsObject();

        // 1. prepTime / cookTime → prepTimeMinutes / cookTimeMinutes (Pitfall C2)
        RenameKey(obj, "prepTime", "prepTimeMinutes");
        RenameKey(obj, "cookTime", "cookTimeMinutes");

        // 2. ingredients[].localId → ingredients[].id (Concerns §2)
        if (obj["ingredients"] is JsonArray ings)
            foreach (var ing in ings.OfType<JsonObject>())
                RenameKey(ing, "localId", "id");

        // 3. steps: { isSection: true, text: "X" } → { kind: "section", heading: "X" }
        //          { isSection: false, text: "Y", timers: [...] } → { kind: "content", text: "Y", timers: [...] }
        //          { section: "Z" } (legacy YAML shape) → { kind: "section", heading: "Z" }
        //          { text: "W" } → { kind: "content", text: "W" }
        if (obj["steps"] is JsonArray steps)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] is not JsonObject step) continue;
                var isSection = step["isSection"]?.GetValue<bool>() == true
                                || step["section"] is not null;

                if (isSection)
                {
                    var heading = step["section"]?.GetValue<string>()
                                  ?? step["text"]?.GetValue<string>()
                                  ?? string.Empty;
                    var rebuilt = new JsonObject
                    {
                        ["kind"] = "section",
                        ["heading"] = heading,
                    };
                    steps[i] = rebuilt;
                }
                else
                {
                    step.Remove("isSection");
                    step.Remove("section");
                    // Keep text and timers; insert kind discriminator FIRST per STJ convention.
                    var rebuilt = new JsonObject { ["kind"] = "content" };
                    foreach (var kvp in step.ToList())
                    {
                        rebuilt[kvp.Key] = kvp.Value!.DeepClone();
                    }
                    steps[i] = rebuilt;
                }
            }
        }

        obj["version"] = 2;
        return obj;
    }

    private static void RenameKey(JsonObject obj, string from, string to)
    {
        if (!obj.ContainsKey(from)) return;
        if (obj.ContainsKey(to)) { obj.Remove(from); return; }    // explicit-wins precedence
        var value = obj[from]!.DeepClone();
        obj.Remove(from);
        obj[to] = value;
    }
}
```

**DI registration (`AddApplication()` in `src/CookBot.Application/DependencyInjection.cs`):**

```csharp
services.AddSingleton<IRecipeUpcaster, Migration_V1_To_V2>();
services.AddSingleton<RecipeUpcasterChain>();
services.AddSingleton<IRecipeSchemaDocumentationProvider, RecipeSchemaDocumentationProvider>();
services.AddSingleton<RecipeJsonSchemaProvider>();
services.AddSingleton<RecipeValidator>();
services.AddSingleton<JsonRecipeSerializer>();
// Existing line stays: IRecipeFormatParser → RecipeFormatParser (rewritten internally)
```

### Pattern 5: YAML → JsonNode adapter (no new package)

**Why not `YamlDotNet.System.Text.Json`?** Adds a 2nd new package, contradicts D-15. The in-tree adapter is small.

```csharp
// Inside RecipeFormatParser (private static)
private static readonly IDeserializer YamlReader = new DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .IgnoreUnmatchedProperties()         // Pitfall H2 — forward-compat
    .Build();

private static JsonNode YamlToJsonNode(string yamlContent)
{
    // YamlDotNet → Dictionary<object, object> with int / double / string / List<object> / Dictionary<object,object> primitives
    var graph = YamlReader.Deserialize(yamlContent);
    return ConvertGraph(graph) ?? new JsonObject();
}

private static JsonNode? ConvertGraph(object? value) => value switch
{
    null                                       => null,
    string s                                   => JsonValue.Create(s),
    bool b                                     => JsonValue.Create(b),
    int i                                      => JsonValue.Create(i),
    long l                                     => JsonValue.Create(l),
    double d                                   => JsonValue.Create(d),
    IDictionary<object, object?> dict          => DictToObj(dict),
    IList<object?> list                        => ListToArr(list),
    _                                          => JsonValue.Create(value.ToString())
};

private static JsonObject DictToObj(IDictionary<object, object?> dict)
{
    var obj = new JsonObject();
    foreach (var kvp in dict)
        obj[kvp.Key.ToString()!] = ConvertGraph(kvp.Value);
    return obj;
}

private static JsonArray ListToArr(IList<object?> list)
{
    var arr = new JsonArray();
    foreach (var item in list) arr.Add(ConvertGraph(item));
    return arr;
}
```

YamlDotNet's untyped deserializer returns the standard `Dictionary<object, object>` shape (verified via [CITED: YamlDotNet docs `aaubry/YamlDotNet/issues/332`]). The adapter is ~25 lines and lives as `private static` helpers inside `RecipeFormatParser`. No package addition.

### Pattern 6: IRecipeFormatParser rewrite (D-10)

The new parser keeps the public surface intact — `TryParse(string, out ParsedRecipe?, out List<string>)` continues to work for all existing callers (`RecipeService.CreateFromTextAsync` line 79, `PasteRawTextDialog.razor`, `AiChat.ExtractRecipeContent`, `RecipeCookingAiContext`). Internally it routes through the new stack:

```csharp
public bool TryParse(string rawContent, out ParsedRecipe? recipe, out List<string> errors)
{
    errors = new();
    recipe = null;

    if (string.IsNullOrWhiteSpace(rawContent))
    {
        errors.Add("Recipe content is empty.");
        return false;
    }

    try
    {
        // 1. Detect format
        JsonNode node;
        var trimmed = rawContent.TrimStart();
        if (trimmed.StartsWith("---"))
        {
            // YAML frontmatter — extract YAML body, convert to JsonNode
            var match = FrontmatterRegex.Match(trimmed);
            if (!match.Success) { errors.Add("Missing YAML frontmatter delimiters."); return false; }
            node = YamlToJsonNode(match.Groups[1].Value);
        }
        else
        {
            // Raw JSON
            node = JsonNode.Parse(rawContent) ?? throw new FormatException("Empty JSON.");
        }

        // 2. Stamp version=1 if absent
        node["version"] ??= 1;

        // 3. Upcast to current
        var upcasted = _upcasterChain.UpcastToCurrent(node);

        // 4. Deserialize to RecipeDocument
        var doc = upcasted.Deserialize<RecipeDocument>(_jsonOptions)!;

        // 5. Validate semantically
        var result = _validator.Validate(doc);
        if (!result.IsValid)
        {
            errors.AddRange(result.Errors.Select(e => $"{e.Path}: {e.Message}"));
            return false;
        }

        // 6. Project to ParsedRecipe (back-compat)
        recipe = ProjectToParsedRecipe(doc);
        return true;
    }
    catch (Exception ex)
    {
        errors.Add($"Parse error: {ex.Message}");
        return false;
    }
}

private static ParsedRecipe ProjectToParsedRecipe(RecipeDocument doc) => new()
{
    Name = doc.Name,
    Servings = doc.Servings,
    PrepTimeMinutes = doc.PrepTimeMinutes,
    CookTimeMinutes = doc.CookTimeMinutes,
    Tags = doc.Tags.ToList(),
    Ingredients = doc.Ingredients.Select(i => new ParsedIngredient
    {
        LocalId = i.Id, Name = i.Name, Amount = i.Amount, Unit = i.Unit, Note = i.Note
    }).ToList(),
    Steps = doc.Steps.Select(s => s switch
    {
        ContentStep c => new ParsedStep
        {
            Text = c.Text,
            IsSection = false,
            Timers = c.Timers?.Select(t => new ParsedTimer
            {
                Duration = t.Duration, Unit = t.Unit, Label = t.Label
            }).ToList()
        },
        SectionStep sec => new ParsedStep { Text = sec.Heading, IsSection = true },
        _ => throw new InvalidOperationException()
    }).ToList()
};
```

**Constructor change:** `RecipeFormatParser(RecipeUpcasterChain chain, RecipeValidator validator, JsonRecipeSerializer serializer)`. DI auto-resolves these singletons. Existing `IRecipeFormatParser` consumers don't see the change.

### Pattern 7: Prompt consolidation (D-19, D-20)

**Existing constructor at `src/CookBot.Application/Services/PromptBuilderService.cs`:**

The current `PromptBuilderService` has **no constructor** — it's a default-constructible `public class`. Registered as `Scoped` at `src/CookBot.Infrastructure/DependencyInjection.cs:22`. (Note: this contradicts the comment in CONTEXT.md `<code_context>` saying singleton; verified the actual registration is `AddScoped`. Recommend keeping it scoped to avoid changing existing consumers — the new `IRecipeSchemaDocumentationProvider` is singleton, which is fine to inject into a scoped service.)

**Insertion shape:**

```csharp
public class PromptBuilderService
{
    private readonly IRecipeSchemaDocumentationProvider _formatDocs;

    public PromptBuilderService(IRecipeSchemaDocumentationProvider formatDocs)
    {
        _formatDocs = formatDocs;
    }
    // ... rest unchanged ...

    private string ResolveRecipeFormat() => _formatDocs.GetFormatPrompt();
    // ResolveRecipeFormat() body deleted — was lines 168-202.

    public string BuildCopyablePrompt(...)
    {
        // ... unchanged through line 261 ...
        sb.AppendLine("## Recipe Format");
        sb.AppendLine();
        sb.AppendLine(_formatDocs.GetFormatPrompt());
        sb.AppendLine();
        // Lines 262-296 (raw YAML example string) DELETED.
        // ... rest unchanged ...
    }
}
```

**`IRecipeSchemaDocumentationProvider` shape:**

```csharp
// src/CookBot.Application/Recipes/IRecipeSchemaDocumentationProvider.cs
namespace CookBot.Application.Recipes;

public interface IRecipeSchemaDocumentationProvider
{
    /// <summary>The single-source format spec embedded in both AI system prompts.</summary>
    string GetFormatPrompt();
}
```

**Prose content guidance for the impl** (D-20 explicitly removes the opt-out clause):

The replacement directive should be: *"If you cannot emit a recipe in the structured format, ask the user a clarifying question instead."* The prose should describe the JSON shape via a literal example (still hand-written) but **derived from the same `RecipeDocument` shape** as the schema. The prose is essentially the YAML example currently at `PromptBuilderService.cs:170-198` translated to v2 conventions:

```
When providing a recipe, emit a fenced code block with this exact JSON shape:

```recipe
{
  "version": 2,
  "name": "Recipe Name",
  "servings": 4,
  "prepTimeMinutes": 15,
  "cookTimeMinutes": 30,
  "tags": ["tag1", "tag2"],
  "ingredients": [
    { "id": 1, "name": "ingredient name", "amount": 2, "unit": "cups" },
    { "id": 2, "name": "another ingredient", "amount": 1, "unit": "tbsp", "note": "optional note" }
  ],
  "steps": [
    { "kind": "content", "text": "Step instruction with [ingredient name](#1)." },
    { "kind": "section", "heading": "Section header" },
    { "kind": "content", "text": "Bake for 25 minutes.",
      "timers": [{ "duration": 25, "unit": "min", "label": "bake" }] }
  ]
}
```

Use [ingredient name](#id) markdown links in step text to reference ingredients by their per-recipe id.
Steps come in two kinds: "content" (with text and optional timers) or "section" (with a heading only).
Timers carry a duration (int), a unit ("min" / "hr" / "sec"), and an optional label.

If you cannot emit a recipe in the structured format, ask the user a clarifying question instead.
```

(The above is illustrative — the planner can refine the wording. The **mandatory deletes** are the two literal blocks at `PromptBuilderService.cs:168-202` and `:262-296`, and both opt-out clauses at `:201` and `:295`.)

### Pattern 8: DatabaseSeeder ordering

**Source:** D-15, D-16, MIGRATION-01/02/07.

Current `DatabaseSeeder.SeedAsync` flow at `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs:18-86`:

```
SeedAsync(context, contentRootPath):
    await context.Database.MigrateAsync();       // line 20
    if (any users) { ensure pantries; ensure admin; return; }  // lines 22-41
    seed default user, pantry, cookbook, ingredients
    SaveChangesAsync
```

**New shape:**

```
SeedAsync(context, backupSvc, projector, serializer, contentRootPath):
    // -- step 1: backup before migrate (NEW) --
    var hasPending = (await context.Database.GetPendingMigrationsAsync()).Any();
    if (hasPending)
        await backupSvc.BackupBeforeMigrationAsync("RecipeCanonicalDocument", default);

    // -- step 2: migrate (EXISTING line 20) --
    await context.Database.MigrateAsync();

    // -- step 3: backfill (NEW) --
    await BackfillCanonicalDocumentAsync(context, projector, serializer);

    // -- step 4: existing seeding logic (lines 22-86) --
    if (any users) { ensure pantries; ensure admin; return; }
    // ... default user etc ...
```

**Key points:**

1. **Conditional backup:** `GetPendingMigrationsAsync()` returns the migrations not yet applied. If empty, no backup. This avoids backing up `cookbot.db` on every cold-start when nothing has changed. [VERIFIED: EF Core 10 docs and `Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<DatabaseFacade>.GetPendingMigrationsAsync` is the standard API.]
2. **Backup runs before migrate, NOT before seed.** The reason: schema migrations are the destructive step. Seed-data adjustments (default user, ingredients) do not warrant a `.bak` file.
3. **Backfill runs after migrate but before the rest of the seed:** the `WHERE CanonicalDocumentJson IS NULL` predicate handles re-runs — fresh installs (zero recipes) produce zero updates (MIGRATION-07).
4. **Backfill DI:** the seeder gets two new constructor-style services. **But seeder is `public static`!** The cleanest fix is to add explicit parameters to `SeedAsync` and update the single call site at `src/CookBot.Web/Program.cs:42`. (Alternative: convert the seeder to an instance class registered in DI. The static-with-extra-params approach matches the existing convention; recommend that.)

**Backfill loop pattern:**

```csharp
private static async Task BackfillCanonicalDocumentAsync(
    CookBotDbContext db, LegacyRecipeProjector projector, JsonRecipeSerializer serializer)
{
    const int batchSize = 50;
    int total = 0;
    while (true)
    {
        var batch = await db.Recipes
            .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.Ingredient)
            .Include(r => r.Steps)
            .Where(r => r.CanonicalDocumentJson == null)
            .Take(batchSize)
            .ToListAsync();

        if (batch.Count == 0) break;

        foreach (var recipe in batch)
        {
            var doc = projector.Project(recipe);
            recipe.CanonicalDocumentJson = serializer.Serialize(doc);  // compact, single-line
        }
        await db.SaveChangesAsync();
        total += batch.Count;
    }
    // Log total via ILogger if injected; otherwise silent.
}
```

`Recipe.Steps` uses `OwnsMany(...).ToJson()` (`RecipeConfiguration.cs:15-19`), so `.Include(r => r.Steps)` is sufficient — owned-JSON loads with the parent. `RecipeIngredients` is a separate relational table that needs its own `Include`.

### Pattern 9: Backup service implementation

**Source:** D-15.

```csharp
// src/CookBot.Infrastructure/Data/IDatabaseBackupService.cs
namespace CookBot.Infrastructure.Data;

public interface IDatabaseBackupService
{
    Task BackupBeforeMigrationAsync(string migrationName, CancellationToken ct);
}
```

```csharp
// src/CookBot.Infrastructure/Data/DatabaseBackupService.cs
namespace CookBot.Infrastructure.Data;

using CookBot.Application.DTOs;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

public sealed class DatabaseBackupService : IDatabaseBackupService
{
    private readonly IConfiguration _config;
    private readonly int _retention;

    public DatabaseBackupService(IConfiguration config, IOptions<CookBotSettings> settings)
    {
        _config = config;
        // Discretion: settings.Value.DatabaseBackupRetention if added; for now use D-15 default of 3.
        _retention = 3;
    }

    public Task BackupBeforeMigrationAsync(string migrationName, CancellationToken ct)
    {
        var connStr = _config.GetConnectionString("DefaultConnection") ?? "Data Source=cookbot.db";
        var builder = new SqliteConnectionStringBuilder(connStr);
        var dbPath = builder.DataSource;

        // Resolve relative paths against the working directory (matches existing `cookbot.db` next to Program.cs)
        var fullPath = Path.GetFullPath(dbPath);
        if (!File.Exists(fullPath))
            return Task.CompletedTask;     // fresh install — nothing to back up

        var dir = Path.GetDirectoryName(fullPath)!;
        var stem = Path.GetFileName(fullPath);
        var backupName = $"{stem}.pre-{migrationName}.bak";
        var backupPath = Path.Combine(dir, backupName);

        File.Copy(fullPath, backupPath, overwrite: true);

        // Last-N retention via mtime
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

**Key facts:**

- `SqliteConnectionStringBuilder.DataSource` is the right API to extract the file path [VERIFIED: learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqliteconnectionstringbuilder.datasource]. **Don't string-parse the connection string.**
- `Microsoft.Data.Sqlite` is transitively available in `CookBot.Infrastructure` via `Microsoft.EntityFrameworkCore.Sqlite`. No new package.
- `File.Copy` is correct here because the backup runs **before** `MigrateAsync` opens any connection. `SqliteConnection.BackupDatabase` is the right tool for live-DB backup but is overkill for our pre-migrate flow [CITED: `dotnet/efcore` issue #13834 — `BackupDatabase` blocks writers anyway].
- Retention by `LastWriteTimeUtc` desc + skip(N) handles "keep last 3" cleanly.
- DI registration in `AddInfrastructure(IConfiguration)`:
  ```csharp
  services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();
  ```

### Pattern 10: LegacyRecipeProjector

**Source:** D-14.

```csharp
// src/CookBot.Infrastructure/Data/Migrations/Helpers/LegacyRecipeProjector.cs
namespace CookBot.Infrastructure.Data.Migrations.Helpers;

using CookBot.Domain.Entities;
using CookBot.Domain.Recipes;

// DELETE-AFTER-V1.1 (per CONTEXT.md D-14 + Phase 4 POLISH-03)
public sealed class LegacyRecipeProjector
{
    private static readonly System.Text.RegularExpressions.Regex IngredientLink = new(
        @"\[([^\]]+)\]\(#(\d+)\)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    public RecipeDocument Project(Recipe recipe)
    {
        var tags = TryDeserializeTags(recipe.TagsJson);

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
                        ? s.Timers.Select(t => new TimerEntry
                          {
                              Duration = t.Duration, Unit = t.Unit, Label = t.Label
                          }).ToList()
                        : null,
                })
            .ToList();

        return new RecipeDocument
        {
            Version = RecipeUpcasterChain.CurrentVersion,
            Name = recipe.Name,
            Servings = recipe.Servings,
            PrepTimeMinutes = recipe.PrepTimeMinutes,
            CookTimeMinutes = recipe.CookTimeMinutes,
            Tags = tags,
            Ingredients = ingredients,
            Steps = steps,
        };
    }

    private static IReadOnlyList<string> TryDeserializeTags(string tagsJson)
    {
        try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(tagsJson) ?? []; }
        catch { return []; }
    }
}
```

**Note on `IngredientRefs`:** Per D-13, this phase **stops writing** `RecipeStep.IngredientRefs` but leaves the column. The projector does NOT consult `IngredientRefs` — it uses `[name](#id)` markdown links in step text as the only ref source. This closes Pitfall C1.

### Anti-Patterns to Avoid (Phase 1 specific)

- **Anti-pattern: keeping the `IsSection` boolean in `RecipeDocument`.** Pitfall C3. The discriminated union enforces "section steps have no timers" at the type level; a flag-based design re-introduces the footgun.
- **Anti-pattern: silently ignoring missing `version` field instead of stamping `1`.** Pitfall H1. The parser MUST stamp `version: 1` when absent — that's how the upcaster chain knows what to do.
- **Anti-pattern: `OwnsOne` for `CanonicalDocumentJson`.** D-12 explicitly forbids this — the column holds a projected snapshot, not a relational projection. EF should treat it as a plain `string?`.
- **Anti-pattern: deleting `RecipeStep.IngredientRefs` column this phase.** D-13 keeps the column for one milestone for safe rollback. Phase 4 owns the removal.
- **Anti-pattern: backing up `cookbot.db` on every startup.** D-15 says "whenever the pending migration list is non-empty". The `GetPendingMigrationsAsync()` check is the gate.
- **Anti-pattern: `MigrationBuilder.Sql(...)` mutating recipe JSON.** Pitfall C4. The migration adds a column; backfill happens in C# inside `DatabaseSeeder`.

---

## Don't Hand-Roll

| Problem | Don't build | Use instead | Why |
|---------|-------------|-------------|-----|
| JSON Schema generation from C# types | Reflection-based schema walker | `JsonSchemaExporter.GetJsonSchemaAsNode` (BCL) | Honors `[JsonPropertyName]`, `[JsonRequired]`, nullability, `[JsonPolymorphic]`/`[JsonDerivedType]` correctly — same exporter that powers ASP.NET Core OpenAPI. |
| JSON Schema runtime validation | `JsonElement` walker matching schema by hand | `JsonSchema.Net` 9.2.0 | Draft 2020-12 support (matches what the exporter emits), STJ-native (no `JObject`), MIT, used by `Microsoft.Extensions.AI`. |
| Polymorphic discriminator emission | Custom `JsonConverter<StepNode>` reading/writing `kind` | `[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]` + `[JsonDerivedType]` | Built-in since .NET 7. Round-trips correctly for both serialize and deserialize. The exporter generates the right `anyOf` schema automatically. |
| Capturing unknown JSON fields | Custom `JsonConverter` with property-bag rewriting | `[JsonExtensionData] Dictionary<string, JsonElement>` | Built-in. Round-trips through edit/save. Verified to work on derived types as well as the base. |
| YAML→JSON conversion | A second YAML library | YamlDotNet 16.3.0 untyped `Deserialize(yaml)` → in-tree `ConvertGraph` adapter (~25 lines) | We're already on YamlDotNet 16.3.0; the adapter is mechanical. Adding `YamlDotNet.System.Text.Json` would violate D-15 (one-package rule). |
| SQLite file-path extraction from connection string | Regex / `Split('=')` | `SqliteConnectionStringBuilder(connStr).DataSource` | Microsoft-provided parser handles all the edge cases (quoting, semicolons, escapes). |
| Snapshot diffing | `Verify` / `ApprovalTests` package | Hand-rolled file-equality + optional `UPDATE_SNAPSHOTS=1` env var | D-21. The prompt is small + stable; library overhead unjustified. |
| Source linting for forbidden words | Roslyn analyzer | xUnit `[Fact]` reading the source file at test time and applying a regex denylist | D-22. Single test, single regex, runs with `dotnet test`. |
| Schema migration framework for documents | Liquibase-style | Hand-written `IRecipeUpcaster` chain (D-09) | One transition (V1→V2) this phase; a library is heavier than the problem. Pattern: Marten/event-sourcing upcasters. |

**Key insight:** Phase 1 has heavy "JSON schema" / "polymorphism" / "migration" terminology that often invites pulling in `NJsonSchema`, `Newtonsoft.Json.Schema`, or schema-migration tooling. The .NET 10 BCL covers schema generation; one MIT package covers runtime validation; the rest is a few hundred lines of in-tree code. This is the cheapest correct stack.

---

## Common Pitfalls (Phase 1 mapping)

Each pitfall is anchored to either a `PITFALLS.md` entry, a CONCERNS.md entry, or a code line. The planner should map each to a specific task or `<acceptance_criteria>`.

### Pitfall C1: `IngredientRefs` lossy migration

**Source:** `PITFALLS.md:13-26`. Maps to FORMAT-05.

**What goes wrong:** Today `RecipeStep.IngredientRefs` (`Domain/Entities/RecipeStep.cs:9`) is recomputed on save by `IngredientRefDetectionService.DetectRefs` (`Application/Services/IngredientRefDetectionService.cs:23-31`). The substring-match fallback at line 24-30 has false positives ("salt" matching "asalted"). When this phase removes the substring fallback, link resolution becomes the only highlighting source — but only if the canonical document round-trips its `[name](#id)` markdown faithfully.

**Mapped task / AC:**
- TASK: delete the substring-match block in `IngredientRefDetectionService.cs` (lines 23-31). The method becomes a thin wrapper around `MarkdownLinkPattern.Matches`.
- AC: `grep -nE 'textLower\.Contains|nameLower\.Length' src/CookBot.Application/Services/IngredientRefDetectionService.cs` returns zero matches.
- AC: every fixture in `tests/CookBot.Tests/Fixtures/Recipes/v1-yaml/` round-trips with the same set of detected refs as `[name](#id)` patterns it contains (no extras, no missing).

### Pitfall C2: `prepTime` vs `prepTimeMinutes`

**Source:** `PITFALLS.md:28-42`. Maps to FORMAT-03 and FORMAT-08.

**What goes wrong:** YAML uses `prepTime` (`PromptBuilderService.cs:175`, `RecipeFormatParser.cs:192-193`). JSON export uses `PrepTimeMinutes` (`CookbookTransferDtos.cs:23-24`). DB uses `Recipe.PrepTimeMinutes` (`Recipe.cs:9-10`). A naive deserialization of either format with the other's key name produces zero.

**Mapped task / AC:**
- TASK: `Migration_V1_To_V2.RenameKey(obj, "prepTime", "prepTimeMinutes")` and same for `cookTime`.
- AC: `[Theory] [MemberData(...)]` test with each `tests/Fixtures/Recipes/v1-yaml/*.yaml` and `v1-json-export/*.json` fixture: parsed result has non-zero `PrepTimeMinutes` AND non-zero `CookTimeMinutes` for any fixture whose source carries those values.

### Pitfall C3: `IsSection` re-implemented as flag

**Source:** `PITFALLS.md:44-56`. Maps to FORMAT-04.

**What goes wrong:** A naive consolidation might keep `IsSection: bool` in the canonical record. That re-creates the footgun: a section step can acquire `Timers` because the type system doesn't forbid it.

**Mapped task / AC:**
- TASK: `StepNode` is `abstract record`. `ContentStep` has `Text` + optional `Timers`. `SectionStep` has `Heading` only.
- AC: `grep -E '\bIsSection\b|\bisSection\b' src/CookBot.Domain/Recipes/` returns zero matches.
- AC: round-trip test: `{ "kind": "section", "heading": "X", "timers": [...] }` JSON either fails schema validation or has `timers` ignored (captured in `Extras` if `[JsonExtensionData]` is set). Either way, `SectionStep.Timers` cannot exist.

### Pitfall C4: Destructive auto-migration without backup

**Source:** `PITFALLS.md:58-72`. Maps to MIGRATION-02.

**What goes wrong:** EF `MigrateAsync` runs at startup. Without a `.bak` file, a buggy migration is irrecoverable.

**Mapped task / AC:**
- TASK: `IDatabaseBackupService.BackupBeforeMigrationAsync` runs before `MigrateAsync` when `GetPendingMigrationsAsync` is non-empty.
- AC: integration test starting from a copy of the test `cookbot.db` confirms `cookbot.db.pre-RecipeCanonicalDocument.bak` exists after seed, and identical bytes to the original DB.
- AC: re-running the seed when no migrations are pending creates **no new backup** (idempotency).
- AC: 4th run when 3 backups exist: `Directory.GetFiles(dir, "cookbot.db.pre-*.bak")` returns exactly 3 entries, with the oldest deleted.

### Pitfall H1: Version field added but never read

**Source:** `PITFALLS.md:130-144`. Maps to FORMAT-02 + FORMAT-08.

**What goes wrong:** The team adds `version: 1` and 6 months later writes V2 — but the parser doesn't dispatch on `version`. There's no migration spine.

**Mapped task / AC:**
- TASK: `RecipeUpcasterChain.UpcastToCurrent` reads `node["version"]` and dispatches.
- AC: `grep -nE 'node\[\\?"version\\?"\]' src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` finds at least one read.
- AC: test with a v1 fixture (no `version` field), v1 fixture (explicit `version: 1`), v2 fixture (`version: 2`), v999 fixture (`version: 999`):
  - v0/v1 → upcasts to v2
  - v2 → identity
  - v999 → throws `InvalidOperationException` with "newer than current" in message

### Pitfall H2: Forward-incompat (reject unknown fields)

**Source:** `PITFALLS.md:146-160`. Maps to FORMAT-09.

**What goes wrong:** A v1 install reads a v2 file with `ovenTempFahrenheit` (Phase 4 field). Strict deserializer throws.

**Mapped task / AC:**
- TASK: `[JsonExtensionData] Dictionary<string, JsonElement> Extras` on `RecipeDocument`, `ContentStep`, `SectionStep`, `IngredientEntry`. **Verify the data round-trips through serialize → DB column → deserialize.**
- TASK: YamlDotNet `DeserializerBuilder().IgnoreUnmatchedProperties()` in `RecipeFormatParser` (already there at line 25 — keep it).
- AC: `tests/CookBot.Tests/Recipes/ExtrasRoundTripTests.cs`: input JSON `{"version":3,"name":"X","ingredients":[],"steps":[],"futureField":"hello"}` → upcast (V1→V2 still applies even though source is v3? — clamp logic: if source version > current, throw; if equal, skip; the test should use `version: 2` with an extra field) → deserialize → re-serialize → assert `futureField: "hello"` is present in output.
- AC (specifically D-04 read tolerance): a v3 recipe (`version: 3`) is rejected by the upcaster chain with a clear error, **but** unknown fields within a v2 recipe round-trip via Extras.

### Pitfall H6: Opt-out clause re-creeps in

**Source:** `PITFALLS.md:211-225`. Maps to AI-04 + AI-06.

**What goes wrong:** Six weeks after shipping AI-04, someone adds "or numbered steps work too" back to the system prompt because Haiku occasionally fails.

**Mapped task / AC:**
- TASK: `tests/CookBot.Tests/Prompts/PromptDenylistTests.cs` (D-22): reads `src/CookBot.Application/Services/PromptBuilderService.cs` and `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs` source as text. For each, assert no case-insensitive match for the regex `\b(fallback|informal|plain numbered)\b` inside string literals (or simpler: in the entire file, since the words don't appear in normal C# code).
- TASK: `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` (D-21): builds the assembled system prompt with a fixture profile, asserts `Assert.Equal` against `tests/Fixtures/Prompts/expected-system-prompt.txt`. Optional `UPDATE_SNAPSHOTS=1` env var rewrites the file.
- AC: `grep -nE "(plain numbered|If you can'?t follow|fallback|informal)" src/CookBot.Application/Services/PromptBuilderService.cs` returns zero matches.
- AC: `grep -nE "(plain numbered|If you can'?t follow|fallback|informal)" src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs` returns zero matches.

### Additional pitfalls relevant to Phase 1 work

| Pitfall | Source | Phase 1 mapping |
|---------|--------|-----------------|
| L5 — Migration breaks on fresh install | `PITFALLS.md:451-457` | MIGRATION-07. Test: empty DB, run seed, assert no errors and `Recipes` table empty. |
| L3 — Schema version constants drift | `PITFALLS.md:435-439` | Document `RecipeUpcasterChain.CurrentVersion` (per-recipe) and `CookbookTransferDocument.SchemaVersion` (envelope) in code comments. D-17. |
| L2 — Stale AI conversation references v1 format | `PITFALLS.md:427-433` | Phase 2 owns this (POLISH-06). Phase 1 just notes it. |
| H4 — Editor loses Extras | `PITFALLS.md:178-192` | Phase 3 owns the editor. Phase 1's job is making sure `Extras` survives the JSON round-trip. |

---

## Code Examples

Verified patterns from official sources, ready for the planner to copy into task descriptions verbatim.

### JsonSchemaExporter call
```csharp
// Source: learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/extract-schema
var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var exporterOptions = new JsonSchemaExporterOptions
{
    TreatNullObliviousAsNonNullable = true,
};
JsonNode schema = serializerOptions.GetJsonSchemaAsNode(typeof(RecipeDocument), exporterOptions);
```

### Polymorphic record declaration
```csharp
// Source: learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ContentStep), typeDiscriminator: "content")]
[JsonDerivedType(typeof(SectionStep), typeDiscriminator: "section")]
public abstract record StepNode;
```

### `[JsonExtensionData]` round-trip
```csharp
// Source: learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/handle-overflow
public sealed record ContentStep : StepNode
{
    [JsonPropertyName("text")] public required string Text { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement> Extras { get; init; } = new();
}
```

### xUnit `[Theory]` + `[MemberData]` over filesystem fixtures
```csharp
// tests/CookBot.Tests/Recipes/RecipeDocumentRoundTripTests.cs
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

**Critical:** the fixture files must be marked `<Content CopyToOutputDirectory="PreserveNewest" />` in `CookBot.Tests.csproj` (or the equivalent `<None Update>` block) so `AppContext.BaseDirectory/Fixtures/...` resolves at test runtime. Pattern follows existing `seeds/ingredients.json` — but those are loaded at `ContentRootPath`, not `BaseDirectory`. The simplest setup:

```xml
<!-- in tests/CookBot.Tests/CookBot.Tests.csproj -->
<ItemGroup>
  <None Update="Fixtures\**\*.*" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### DI registration deltas
```csharp
// src/CookBot.Application/DependencyInjection.cs - additions to AddApplication()
services.AddSingleton<IRecipeSchemaDocumentationProvider, RecipeSchemaDocumentationProvider>();
services.AddSingleton<RecipeJsonSchemaProvider>();
services.AddSingleton<RecipeValidator>();
services.AddSingleton<JsonRecipeSerializer>();
services.AddSingleton<IRecipeUpcaster, Migration_V1_To_V2>();
services.AddSingleton<RecipeUpcasterChain>();

// src/CookBot.Infrastructure/DependencyInjection.cs - additions to AddInfrastructure()
services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();
services.AddScoped<LegacyRecipeProjector>();   // throwaway one-shot, scoped is fine
```

### Snapshot test pattern (D-21)
```csharp
// tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs
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

### Lint denylist test pattern (D-22)
```csharp
// tests/CookBot.Tests/Prompts/PromptDenylistTests.cs
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

`TestHost.FindRepoRoot()` walks up from `AppContext.BaseDirectory` looking for `FreelovesCookBot.sln`. The file-read pattern is verbatim from the convention — no Roslyn analyzer needed.

### EF migration generation command
```bash
# From the repo root
dotnet ef migrations add RecipeCanonicalDocument \
    --project src/CookBot.Infrastructure \
    --startup-project src/CookBot.Web
```

This is the existing convention per `.planning/codebase/STRUCTURE.md:201`. `Microsoft.EntityFrameworkCore.Design` lives in both `CookBot.Web` (`CookBot.Web.csproj`) and `CookBot.Infrastructure` (`CookBot.Infrastructure.csproj:9`). The migration .cs file lands at `src/CookBot.Infrastructure/Migrations/<UTC>_RecipeCanonicalDocument.cs`. The Designer.cs and `CookBotDbContextModelSnapshot.cs` are auto-updated.

The migration's `Up()` body should be just:
```csharp
migrationBuilder.AddColumn<string>(
    name: "CanonicalDocumentJson",
    table: "Recipes",
    type: "TEXT",
    nullable: true);
```

Per existing convention (e.g. `20260416175214_AiApiKeyShares.cs:14-19`). No `Sql(...)` call. Backfill happens in `DatabaseSeeder`.

---

## Runtime State Inventory

> Phase 1 introduces a column rename surface (`localId` → `id`, `prepTime` → `prepTimeMinutes`, `IsSection: bool` + `Text` → `kind: section, heading`) **inside the canonical document JSON**, but the relational columns keep their existing names (`Recipe.PrepTimeMinutes`, `RecipeIngredient.RecipeLocalId`, `RecipeStep.IsSection`). This is **not a global string rename** — it's a serialization-shape change. The runtime state below is therefore narrower than for a true rebrand.

| Category | Items | Action |
|----------|-------|--------|
| **Stored data** | `cookbot.db.Recipes.CanonicalDocumentJson` (NEW column, nullable) — backfilled at `DatabaseSeeder` time. `cookbot.db.Recipes.Steps` JSON column (existing OwnsMany) — **unchanged**. `cookbot.db.Recipes.PrepTimeMinutes` etc. relational columns — **unchanged**. `Recipe.TagsJson` — **unchanged this phase** (Phase 4 owns POLISH-04). | Backfill via `LegacyRecipeProjector` once per recipe; idempotent on re-run. |
| **Live service config** | None — the app has no external services (Datadog, n8n, etc.) per `.planning/codebase/STACK.md`. The Anthropic API key is per-user in `cookbot.db`, but the schema work doesn't touch it. | None. |
| **OS-registered state** | None — `run.sh` is the deploy story; no Windows Task Scheduler, no systemd unit, no pm2 process names. | None. |
| **Secrets and env vars** | None affected. `CookBot:AnthropicApiKey` (env: `CookBot__AnthropicApiKey`) is unchanged. Connection-string env-var `ConnectionStrings:DefaultConnection` is unchanged. | None. |
| **Build artifacts / installed packages** | New NuGet `JsonSchema.Net` 9.2.* lands in `src/CookBot.Application/CookBot.Application.csproj`. Existing `bin/` / `obj/` directories rebuild from a clean `dotnet restore`. | Standard `dotnet restore` after merging. |
| **`.cookbook.json` files in the wild** | These have `SchemaVersion: 1` envelope. **Phase 1 only bumps the constant for new exports** (D-17). The `Deserialize` hot path stays on the v1 deserializer until Phase 2 (MIGRATION-04). Old files keep importing via the existing path. | Document the version axis in code comments above `CookbookTransferDocument.SchemaVersion`. |
| **Anthropic system prompts in user templates** | `UserProfile.AiSystemPromptTemplate` may contain `{{recipe_format}}` token. `PromptBuilderService.ResolveTemplate` substitutes it. After Phase 1, the substituted text comes from `RecipeSchemaDocumentationProvider` (v2 shape) instead of the literal at `:170-198` (v1 shape). User templates are not migrated — the token is dynamic. | None — token substitution is dynamic; user templates auto-pick up the new format text on next prompt build. |
| **AI conversation history** | `AiConversation.MessagesJson` contains assistant outputs in v1 YAML format. **Phase 2 owns this** (POLISH-06). Phase 1's job: don't break loading old conversations. The MessagesJson is not parsed by Phase 1 code. | None this phase. |

**Nothing destructive in this phase.** The only state mutation is `Recipe.CanonicalDocumentJson` getting populated (NULL → JSON string). Pre-migration `.bak` is the rollback path.

---

## Environment Availability

| Dependency | Required by | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All compilation | ✓ | 10.0.107 (verified `dotnet --version`) | — |
| `dotnet ef` CLI | Generating the EF migration | ✓ (transitively via `Microsoft.EntityFrameworkCore.Design` in `CookBot.Web` and `CookBot.Infrastructure` csproj) | 10.* | If missing globally: `dotnet tool install --global dotnet-ef --version 10.*` |
| SQLite (file-only) | Runtime persistence | ✓ (transitive via `Microsoft.EntityFrameworkCore.Sqlite` 10.*) | bundled | — |
| `JsonSchema.Net` 9.2.0 | Phase 1 self-validation + Phase 2 runtime validation | ✗ — **must add** | 9.2.0 | None — single new package per D-15 |
| YamlDotNet 16.3.0 | YAML→JsonNode adapter | ✓ | 16.3.0 (`CookBot.Application.csproj:10`) | — |
| xUnit 2.9.2 | Test framework | ✓ | 2.9.2 | — |
| `dotnet test` runner | Running denylist + snapshot + round-trip tests | ✓ | bundled with SDK | — |

**Missing dependencies with no fallback:** none — `JsonSchema.Net` is the single addition.

**Missing dependencies with fallback:** none.

**Smoke command for the planner to use as a Wave 0 gate:**
```bash
cd /home/noah/Desktop/projects/freeloves-cookbot
dotnet add src/CookBot.Application package JsonSchema.Net --version 9.2.*
dotnet build
dotnet test
```
A clean run before any Phase 1 task lands ensures the baseline is healthy.

---

## State of the Art

| Old Approach (current code) | Current Approach (Phase 1 target) | Why changed | Impact |
|--------------|------------------|--------------|--------|
| Three independent serializers (YAML at `RecipeFormatParser.cs`, JSON DTOs at `CookbookTransferDtos.cs`, owned-JSON via `RecipeConfiguration.cs:15-19`) | One `RecipeDocument` POCO; everything else projects from it | CONCERNS §1-4: drift across formats. SUMMARY.md §1 headline. | Eliminates the "three competing shapes" debt. |
| `IsSection: bool` + `Text` per step | `[JsonPolymorphic]` discriminated union (`ContentStep` / `SectionStep`) | Pitfall C3 — flag-based encoding allows section steps to acquire timers. | Type system enforces invariant. |
| `prepTime` (YAML) / `prepTimeMinutes` (JSON) / `PrepTimeMinutes` (DB) | Single canonical name `prepTimeMinutes` everywhere | Pitfall C2 — silent zero-out on round-trip. | Unified key set; V1→V2 upcaster handles legacy reads. |
| Format spec duplicated at `PromptBuilderService.cs:170-198` and `:267-291` | Single `IRecipeSchemaDocumentationProvider.GetFormatPrompt()` | CONCERNS §13. POLISH-02. | One source for both AI sites; snapshot test locks it. |
| Opt-out clause at `:201` and `:295` | Strict directive to ask clarifying question instead | AI-04 / Pitfall H6. | AI conformance becomes the only path. |
| Substring-match ingredient ref fallback at `IngredientRefDetectionService.cs:23-31` | `[name](#id)` markdown links only | FORMAT-05 / Pitfall C1. | Eliminates false-positive highlights. |
| EF migrations auto-apply with no backup | `IDatabaseBackupService.BackupBeforeMigrationAsync` runs first when pending | Pitfall C4 / MIGRATION-02. | User data has a recovery path. |
| `Recipe.IngredientRefs` written on every save (`RecipeService.cs:69, 129`) | Stops being written this phase; column persists for one milestone | D-13. Phase 4 drops the column. | Safe rollback window; eventual cleanup. |
| `CookbookTransferDocument.SchemaVersion = 1` (no per-recipe version axis) | Envelope `SchemaVersion = 2`; per-recipe `Version: 2` in canonical doc | D-17 / MIGRATION-05 / Pitfall H3. | Two-axis versioning prevents the mixed-version cookbook footgun. |

**Deprecated/outdated:**
- The `RecipeFormatParser`'s YAML-only pathway is no longer the canonical wire format — JSON is. YAML stays as paste-in input only.
- `RecipeFrontmatter.PrepTime` (the private DTO at `RecipeFormatParser.cs:192`) — internally retired once the rewrite delegates to the upcaster chain.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The user's machine has `dotnet ef` accessible via `dotnet ef migrations add ...` (transitively via the Web project's `Microsoft.EntityFrameworkCore.Design` reference) | Pattern 8, Environment Availability | If missing globally, the planner's "generate migration" task fails until `dotnet tool install --global dotnet-ef` is run. Low risk — `Microsoft.EntityFrameworkCore.Design` 10.* is referenced in both the Web and Infrastructure csproj. |
| A2 | `PromptBuilderService` is `Scoped` (per actual registration at `CookBot.Infrastructure/DependencyInjection.cs:22`), not `Singleton` as CONTEXT.md `<code_context>` describes | Pattern 7 | Constructor injection of `IRecipeSchemaDocumentationProvider` (singleton) into a scoped service is fine. If the planner mistakenly upgrades `PromptBuilderService` to singleton, no functional break — but it's wasted churn. |
| A3 | The default `JsonSerializerOptions` for `JsonRecipeSerializer` should match `JsonSerializerDefaults.Web` (camelCase, case-insensitive deserialize) — matching `AnthropicAiService.JsonOptions` style at `AnthropicAiService.cs:23-27` | Pattern 6, schema provider | If non-Web defaults are used, JSON keys come out PascalCase, breaking the schema contract. Mitigation: explicit `[JsonPropertyName]` on every record property (already shown). |
| A4 | Anthropic structured-outputs strict-mode does not support `oneOf` but does support `anyOf` — and STJ's `JsonSchemaExporter` emits `anyOf` for `[JsonPolymorphic]` types | Pattern 1, schema provider | Verified via [CITED: platform.claude.com docs] and [CITED: GitHub `dotnet/runtime` source]. Confidence HIGH. If wrong, Phase 2 wiring breaks — but Phase 1 only generates the schema; the breakage would surface in Phase 2 acceptance. |
| A5 | `CookBotSettings.DatabaseBackupRetention` does NOT yet exist in `CookBotSettings.cs` (it's only in CONTEXT.md as a "configurable via" note) | Pattern 9 (backup service) | Hardcoding `_retention = 3` matches D-15's default and the user can add the setting later as a Phase 1 follow-up. Low risk. |
| A6 | YamlDotNet 16.3.0's untyped `Deserialize(yaml)` returns the `Dictionary<object, object?>` shape with `int`/`double`/`string`/`List<object>`/`Dictionary<object,object>` primitives | Pattern 5 (YAML→JsonNode adapter) | Verified by [CITED: aaubry/YamlDotNet#332]. The adapter's `ConvertGraph` switch handles all 5 cases. If a fixture contains a YAML feature outside this set (e.g. anchors+aliases), the fallback `_ => JsonValue.Create(value.ToString())` catches it but loses fidelity — fixtures should not exercise YAML anchors. |
| A7 | `AppContext.BaseDirectory` resolves to the test bin directory at runtime, and `<None Update="Fixtures\**\*.*" CopyToOutputDirectory="PreserveNewest" />` is the right csproj idiom for fixture files | Pattern, Code examples | Standard .NET pattern. If wrong, the fixture-driven tests fail with file-not-found and the planner has a concrete error to debug. Low risk. |

---

## Open Questions (RESOLVED)

1. **Should `RecipeJsonSchemaProvider` emit a Draft 2020-12 `$schema` URI?**
   - What we know: STJ's `JsonSchemaExporter` emits a 2020-12-compatible structure but doesn't add a `$schema` property. Anthropic strict mode neither requires nor rejects `$schema`.
   - What's unclear: whether `JsonSchema.Net` consumers expect the `$schema` URI for draft routing.
   - RESOLVED: omit `$schema` (matches Anthropic example payload). `JsonSchema.Net` defaults to 2020-12 evaluation when none is set.

2. **Should the `Recipe.CanonicalDocumentJson` column be tightened to NOT NULL after backfill?**
   - What we know: D-12 says "TEXT, nullable initially". The phrase "initially" implies a Phase 4 follow-up.
   - What's unclear: whether to schedule that tightening now or just leave it nullable forever.
   - RESOLVED: leave nullable forever. Indexed queries don't filter on it; the only consumers (export, AI prompt example) check for null and recompute on demand. A NOT NULL constraint adds zero value but creates a future migration burden.

3. **Should `RecipeUpcasterChain` validate the schema of the upcasted node before deserialize?**
   - What we know: `JsonSchema.Net` is registered. The upcaster chain emits `JsonNode` that should match the v2 schema.
   - What's unclear: whether running `jsonSchema.Evaluate(upcasted)` adds value or just slows things down.
   - RESOLVED: do **not** validate in the chain. The downstream `RecipeValidator` covers semantic checks; STJ deserialize covers structural shape. Schema-validation is reserved for Phase 2's AI-output gate.

4. **What's the right fixture for the first migration test (MIGRATION-08)?**
   - What we know: D-25 says use `RecipeService` to seed 3 representative recipes via the existing relational shape, then run `LegacyRecipeProjector`. Does NOT exercise the actual migration.
   - What's unclear: should the test also exercise `IDatabaseBackupService` end-to-end?
   - RESOLVED: yes — a separate integration test in `CanonicalBackfillTests.cs` should also verify the backup file lands on disk with the expected name. Plan 03 Task 5 implements this as a required `[Fact]` (W3 (a)).
---

## Sources

### Primary (HIGH confidence)

- [Microsoft Learn — JsonSchemaExporter Class (.NET 10)](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.schema.jsonschemaexporter?view=net-10.0) — API signature, returns `JsonNode`, options bag.
- [Microsoft Learn — JSON schema exporter (System.Text.Json)](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/extract-schema) — `TreatNullObliviousAsNonNullable`, `TransformSchemaNode`, `JsonUnmappedMemberHandling.Disallow` interaction.
- [Microsoft Learn — Polymorphic serialization (System.Text.Json)](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism) — `[JsonPolymorphic]`, `[JsonDerivedType]`, discriminator emission position.
- [Microsoft Learn — Handle overflow JSON / `[JsonExtensionData]`](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/handle-overflow) — round-trip semantics, `Dictionary<string, JsonElement>` shape.
- [Anthropic Structured Outputs (GA docs)](https://platform.claude.com/docs/en/build-with-claude/structured-outputs) — `additionalProperties: false` requirement, supported keywords (`anyOf` yes, `oneOf` no).
- [GitHub `dotnet/runtime` JsonSchemaExporter source](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Text.Json/src/System/Text/Json/Schema/JsonSchemaExporter.cs) — verified emits `anyOf` for `[JsonPolymorphic]` types (not `oneOf`).
- [NuGet — JsonSchema.Net 9.2.0](https://www.nuget.org/packages/JsonSchema.Net) — package metadata, license MIT, target frameworks, single dependency `JsonPointer.Net >= 7.0.1`.
- [Microsoft Learn — SqliteConnectionStringBuilder.DataSource](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqliteconnectionstringbuilder.datasource) — verified API for connection-string path extraction.
- Codebase audit: `.planning/codebase/ARCHITECTURE.md`, `STACK.md`, `STRUCTURE.md`, `CONVENTIONS.md`, `TESTING.md`, `CONCERNS.md`.
- Phase context: `.planning/phases/01-canonical-format-foundation/01-CONTEXT.md` — D-01..D-25.

### Secondary (MEDIUM confidence)

- [Microsoft.Data.Sqlite — Online backup](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup) — `BackupDatabase` API; informs the choice of `File.Copy` over `BackupDatabase` for pre-migrate flow.
- [GitHub `dotnet/efcore` issue #13834](https://github.com/dotnet/efcore/issues/13834) — `BackupDatabase` blocks writers, motivating the `File.Copy` choice for our offline-pre-migrate flow.
- [endjin — JSON Schema Patterns: Polymorphism with discriminator](https://endjin.com/blog/2024/05/json-schema-patterns-dotnet-polymorphism-with-discriminator-properties) — independent corroboration of `anyOf` + `const` discriminator pattern.
- [aaubry/YamlDotNet#332](https://github.com/aaubry/YamlDotNet/issues/332) — untyped `Dictionary<object,object>` deserialize shape.

### Tertiary (LOW confidence — flagged for validation)

- [Snippets Ltd — Structured Outputs with Claude: validation and retry loops](https://snippets.ltd/blog/structured-outputs-with-claude-json-schemas-validation-retry-loops) — referenced in ARCHITECTURE.md for Phase 2; not used directly here.

---

## Metadata

**Confidence breakdown:**
- Standard stack (single new package, BCL exporter): HIGH — verified directly against nuget.org and Microsoft Learn.
- Architecture (record shape, upcaster, validator, backup service): HIGH — all interfaces and shapes validated against existing codebase patterns and STJ docs.
- Pitfalls mapping (C1–C7, H1–H6, L5): HIGH — every pitfall maps to a concrete grep-verifiable AC anchored to existing code lines.
- Anthropic schema constraints (Phase 2 forward-look): HIGH — verified against current Anthropic docs (April 2026); planner can use these as Phase 2 input without re-research.
- EF Core 10 + SQLite migration mechanics on `Recipe.CanonicalDocumentJson` column: HIGH — pattern matches existing migration `20260416175214_AiApiKeyShares` exactly.
- DatabaseSeeder ordering (`HasPendingMigrationsAsync` → backup → migrate → backfill): HIGH — uses standard EF Core 10 API.

**Research date:** 2026-04-25
**Valid until:** 2026-05-25 (30 days; .NET 10 BCL and Anthropic structured-outputs API are stable; revisit if `JsonSchema.Net` ships a 9.3+ with breaking changes)
