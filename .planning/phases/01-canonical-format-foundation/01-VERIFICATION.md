---
status: passed
phase: 01-canonical-format-foundation
must_haves_total: 35
must_haves_passed: 35
requirements_total: 20
requirements_covered: 20
verified: 2026-04-25
re_verification:
  previous_status: none
  previous_score: n/a
  gaps_closed: []
  gaps_remaining: []
  regressions: []
---

# Phase 1: Canonical Format Foundation Verification Report

**Phase Goal:** One versioned `RecipeDocument` becomes the single source of truth across YAML wire format, JSON export, DB JSON column, and AI prompt; existing `cookbot.db` data and `.cookbook.json` files migrate cleanly with safe rollback.

**Verified:** 2026-04-25
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Test Gate State

| Check | Command | Result |
| ----- | ------- | ------ |
| Build clean | `dotnet build FreelovesCookBot.sln -c Debug` | 0 warnings, 0 errors |
| Full test suite | `dotnet test FreelovesCookBot.sln --no-build -c Debug` | **122/122 passed**, 0 failed, 0 skipped |
| Phase-1 targeted tests | `dotnet test --filter "FullyQualifiedName~RecipeDocumentRoundTripTests\|FullyQualifiedName~ExtrasRoundTripTests\|FullyQualifiedName~PromptDenylistTests\|FullyQualifiedName~PromptSnapshotTests\|FullyQualifiedName~CanonicalBackfillTests"` | 18/18 passed |

### Success Criteria (from ROADMAP.md)

| # | Success Criterion | Status | Evidence |
|---|-------------------|--------|----------|
| 1 | v1.0 `cookbot.db` row round-trips with non-zero `prepTimeMinutes`/`cookTimeMinutes` across every fixture | ✓ VERIFIED | `RecipeDocumentRoundTripTests` (8 cases) drives v1-yaml/, v1-json-export/, v2-canonical/ fixtures via `RecipeUpcasterChain → JsonRecipeSerializer → RecipeValidator → ProjectToParsedRecipe`; CanonicalBackfillTests asserts no value drift on Project→Serialize→Parse→Validate. All 8/8 round-trip + 2/2 backfill tests pass. |
| 2 | `.cookbook.json` v1 imports cleanly: `prepTime`/`prepTimeMinutes`, `IsSection: bool`/`Text`, `localId` reconciled by V1→V2 upcaster | ✓ VERIFIED | `Migration_V1_To_V2.cs` lines 28–29 (`RenameKey "prepTime"→"prepTimeMinutes"`, `"cookTime"→"cookTimeMinutes"`), line 36 (`localId`→`id`), lines 61/71 (rebuild step as `kind: "section"` or `kind: "content"`); `v1-json-export/sectioned.json` fixture exercises `isSection: true` upcast → kind discriminator |
| 3 | `DatabaseSeeder.SeedAsync` creates `cookbot.db.pre-{name}.bak` (last-3 retention), back-fills `Recipe.CanonicalDocumentJson`, idempotent, fresh-install no-op | ✓ VERIFIED | `DatabaseSeeder.cs:29` (`GetPendingMigrationsAsync`), `:32` (`BackupBeforeMigrationAsync("RecipeCanonicalDocument", ...)` only when pending non-empty), `:124` (`Where(r => r.CanonicalDocumentJson == null)` idempotent predicate); `DatabaseBackupService.cs:23` retention `Math.Clamp([1,10])`; `DatabaseBackupServiceTests` 2/2 pass; `BackupBeforeMigration_CreatesBackupFile_WithExpectedName` 1/1 pass |
| 4 | System prompt reads from single `RecipeSchemaDocumentationProvider`; duplicated 168–202 / 262–296 deleted; opt-out gone; snapshot test + lint denylist enforce | ✓ VERIFIED | `PromptBuilderService.cs` shrunk from 304 to **246 lines**; `_docs.GetFormatPrompt()` called twice (`grep -c` returns 2); zero matches for `\b(fallback\|informal\|plain numbered)\b`; `PromptSnapshotTests` + `PromptDenylistTests` both pass |
| 5 | v1 install reads fictional v3 recipe → captures unknown fields into `Extras` and round-trips them | ✓ VERIFIED | 4 `[JsonExtensionData]` sites confirmed: `RecipeDocument.cs:38`, `StepNode.cs:25` (ContentStep), `:36` (SectionStep), `IngredientEntry.cs:29`; `ExtrasRoundTripTests` (5 facts including version-too-new rejection) all pass |

**Score:** 5/5 success criteria verified.

### Requirements Coverage (20/20)

Every Phase-1 REQ-ID is satisfied. Source plans (`P01..P04`) declare them in `requirements:` frontmatter.

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| FORMAT-01 | P01 | Single canonical `RecipeDocument` in `CookBot.Domain.Recipes/` | ✓ SATISFIED | `RecipeDocument.cs` exists; `public sealed record RecipeDocument` with `int Version`, `Name`, `Servings`, `PrepTimeMinutes`, `CookTimeMinutes`, `Tags`, `Ingredients`, `Steps`, `Extras` |
| FORMAT-02 | P01 | Explicit `int Version` at top level (bumped 1→2 this milestone) | ✓ SATISFIED | `RecipeDocument.cs:14` `public required int Version`; `RecipeUpcasterChain.cs:14` `public const int CurrentVersion = 2` |
| FORMAT-03 | P01 | Unit-baked field names (`prepTimeMinutes`, `cookTimeMinutes`); V1→V2 upcaster reconciles legacy | ✓ SATISFIED | `RecipeDocument.cs:23,26`; `Migration_V1_To_V2.cs:28-29` reconciles `prepTime`/`cookTime`; `grep '\bprepTime\b\|\bcookTime\b' src/CookBot.Domain/Recipes/RecipeDocument.cs` → 0 matches |
| FORMAT-04 | P01 | `StepNode` discriminated union (`kind`); no `IsSection` boolean | ✓ SATISFIED | `StepNode.cs:10` `[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]`; lines 11–12 `JsonDerivedType(typeof(ContentStep), "content")` and `(SectionStep, "section")`; `grep IsSection src/CookBot.Domain/Recipes/StepNode.cs` → 0 matches |
| FORMAT-05 | P02 | `[name](#id)` markdown only; substring fallback removed from `IngredientRefDetectionService` | ✓ SATISFIED | `grep textLower\.Contains\|nameLower\.Length src/CookBot.Application/Services/IngredientRefDetectionService.cs` → 0 matches; `MarkdownLinkPattern.Matches` is the only ref source |
| FORMAT-06 | P01 | `RecipeJsonSchemaProvider` via `JsonSchemaExporter`; `additionalProperties: false` everywhere | ✓ SATISFIED | `RecipeJsonSchemaProvider.cs:36-41` calls `serializerOptions.GetJsonSchemaAsNode(typeof(RecipeDocument), exporterOptions)`; `:58-60` post-walk sets `additionalProperties: false`; `RecipeJsonSchemaProviderTests` (3/3 pass) verifies recursive enforcement |
| FORMAT-07 | P01 | `RecipeValidator` returns data, never throws | ✓ SATISFIED | `RecipeValidator.cs` covers `REQUIRED` name, `OUT_OF_RANGE` servings, `DUPLICATE_ID` ingredients, `DANGLING_REF` step, empty section heading; `RecipeValidatorTests` (7/7 pass) including null-input case |
| FORMAT-08 | P01 | `IRecipeUpcaster` chain at JSON-node layer; `Migration_V1_To_V2` reconciles all four legacy quirks | ✓ SATISFIED | `IRecipeUpcaster.cs` interface + `RecipeUpcasterChain.cs` (gap-detected at construction) + `Migration_V1_To_V2.cs` (all 4 quirks handled per FORMAT-08 spec); `RecipeUpcasterTests` (9/9 pass) |
| FORMAT-09 | P01 | Forward-compat `Extras: Dictionary<string, JsonElement>` round-trip | ✓ SATISFIED | 4 `[JsonExtensionData]` sites confirmed (RecipeDocument, ContentStep, SectionStep, IngredientEntry); `ExtrasRoundTripTests` (5/5 pass) |
| FORMAT-10 | P04 | Round-trip CI gate test suite | ✓ SATISFIED | `RecipeDocumentRoundTripTests.cs`; 5 v1-yaml + 2 v1-json-export + 1 v2-canonical fixtures; 8/8 pass; asserts non-zero `prepTimeMinutes`/`cookTimeMinutes` (Pitfall C2) |
| AI-04 | P04 | Opt-out clause removed from `PromptBuilderService` and replaced with strict directive | ✓ SATISFIED | `grep '\b(fallback\|informal\|plain numbered)\b' src/CookBot.Application/Services/PromptBuilderService.cs` → 0 matches; `RecipeSchemaDocumentationProvider.cs` ends with "If you cannot emit a recipe in the structured format, ask the user a clarifying question instead." |
| AI-05 | P04 | Single `RecipeSchemaDocumentationProvider` source; both prompt sites read from it | ✓ SATISFIED | `grep -c '_docs\.GetFormatPrompt' src/CookBot.Application/Services/PromptBuilderService.cs` → **2** (`ResolveRecipeFormat()` + `BuildCopyablePrompt()`) |
| AI-06 | P04 | Snapshot test + lint denylist prevent regression | ✓ SATISFIED | `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` + `PromptDenylistTests.cs` exist; both pass; `expected-system-prompt.txt` fixture committed |
| MIGRATION-01 | P03 | EF migration adds `Recipe.CanonicalDocumentJson` (TEXT NULL); back-fill via `LegacyRecipeProjector` | ✓ SATISFIED | `20260425223916_RecipeCanonicalDocument.cs:13-17` `AddColumn<string>(... type: "TEXT", nullable: true)`; `Recipe.cs:18` property; `LegacyRecipeProjector.cs` exists with `DELETE-AFTER-V1.1` marker |
| MIGRATION-02 | P03 | Pre-migration `cookbot.db` backup with last-3 retention | ✓ SATISFIED | `DatabaseSeeder.cs:32` calls `BackupBeforeMigrationAsync("RecipeCanonicalDocument", ...)` only when `pending.Count > 0`; `DatabaseBackupService.cs:23` `Math.Clamp([1,10])`; `:29` `SqliteConnectionStringBuilder.DataSource` (D-15) |
| MIGRATION-03 | P02, P03 | Hybrid persistence: relational + canonical; `CanonicalDocumentJson` recomputed on every save | ✓ SATISFIED | `RecipeService.cs:86` (Create), `:151` (Update) both have `recipe.CanonicalDocumentJson = _canonicalSerializer.Serialize(canonicalDoc)`; relational columns + `RecipeIngredients` + `Steps` ownership unchanged |
| MIGRATION-05 | P01 | Envelope `CookbookTransferDocument.SchemaVersion` bumped to 2 | ✓ SATISFIED | `CookbookTransferDtos.cs:13` `public int SchemaVersion { get; set; } = 2;` with two-axis-versioning xmldoc comment |
| MIGRATION-07 | P02, P03 | Idempotent migration | ✓ SATISFIED | `DatabaseSeeder.cs:124` `Where(r => r.CanonicalDocumentJson == null)` idempotent predicate; fresh-install no-op gated by `pending.Count > 0` |
| MIGRATION-08 | P03 | Smoke test on representative `cookbot.db` round-trip with no value drift | ✓ SATISFIED | `tests/CookBot.Tests/Migration/CanonicalBackfillTests.cs` exists; both facts (`Backfill_ThreeRecipes_RoundTripsWithoutValueDrift`, `BackupBeforeMigration_CreatesBackupFile_WithExpectedName`) pass |
| POLISH-02 | P04 | Duplicated format-spec strings in `PromptBuilderService` deleted | ✓ SATISFIED | `wc -l src/CookBot.Application/Services/PromptBuilderService.cs` → **246** (was 304); both literal blocks consolidated to `_docs.GetFormatPrompt()` calls |

**Coverage:** 20/20 requirements satisfied. Zero ORPHANED requirements (all REQ-IDs declared in plan frontmatter map to plan deliverables).

### Decision-Fidelity Audit (D-01..D-25)

Sampled across all four planning waves — every audited decision honored.

| Decision | Statement (paraphrase) | Status | Evidence |
|----------|------------------------|--------|----------|
| D-01 | `RecipeDocument` in `CookBot.Domain.Recipes`; no new project; pure POCO | ✓ HONORED | Files at `src/CookBot.Domain/Recipes/`; `CookBot.Domain.csproj` zero `<PackageReference>` |
| D-02 | `kind` discriminator; `ContentStep`/`SectionStep`; no `IsSection` in canonical | ✓ HONORED | `StepNode.cs:10-12`; `grep IsSection src/CookBot.Domain/Recipes/StepNode.cs` → 0 matches |
| D-03 | Units-baked field names (`prepTimeMinutes`, `cookTimeMinutes`) | ✓ HONORED | `RecipeDocument.cs:23,26`; naked `prepTime`/`cookTime` absent |
| D-04 | `int Version` at top of root; JSON Schema constrains writes to `enum: [2]`; reads accept any `int >= 1` | ✓ HONORED | `RecipeDocument.cs:14` `required int Version`; `RecipeUpcasterChain.cs` reads any version, throws if > CurrentVersion |
| D-05 | `[JsonExtensionData] Extras` on RecipeDocument, ContentStep, SectionStep, IngredientEntry | ✓ HONORED | All 4 sites confirmed; `TimerEntry.cs` correctly omits Extras |
| D-06 | Ingredient `id` immutable per-recipe int; substring-match fallback deleted in this phase | ✓ HONORED | `IngredientEntry.cs` uses `Id` not `LocalId`; `IngredientRefDetectionService` substring branch deleted |
| D-07 | `RecipeJsonSchemaProvider` singleton; `Lazy<JsonNode>` cached; post-walks for `additionalProperties: false` | ✓ HONORED | `RecipeJsonSchemaProvider.cs` uses `Lazy<>` + recursive walker `:49-60`; registered Singleton in `AddApplication()` |
| D-08 | Validator returns `ValidationResult`, never throws; coercion at parser layer | ✓ HONORED | `ValidationResult.cs` (3 records); `RecipeValidator.cs` returns data; `RecipeValidatorTests` includes null-input case |
| D-09 | `IRecipeUpcaster` JSON-node-layer chain; `Migration_V1_To_V2` only impl this milestone | ✓ HONORED | `IRecipeUpcaster.cs`, `RecipeUpcasterChain.cs` (gap-detected), `Migration_V1_To_V2.cs`; DI registers single upcaster |
| D-10 | `IRecipeFormatParser` public surface preserved; impl rewritten | ✓ HONORED | `RecipeFormatParser.cs:29` `class RecipeFormatParser : IRecipeFormatParser` with delegating ctor (`RecipeUpcasterChain`, `JsonRecipeSerializer`, `RecipeValidator`); 11 schema-stack references |
| D-12 | `Recipe.CanonicalDocumentJson` is plain `string?`, NOT `OwnsOne`/`OwnsMany` | ✓ HONORED | `Recipe.cs:18` `public string? CanonicalDocumentJson`; `grep OwnsOne.*CanonicalDocumentJson` → 0 matches |
| D-13 | Stop writing `RecipeStep.IngredientRefs`; column persists for one milestone | ✓ HONORED | `grep step\.IngredientRefs\s*= src/CookBot.Application/Services/RecipeService.cs` → 0 matches; `RecipeStep.cs:9` property persists |
| D-14 | `LegacyRecipeProjector` in `Infrastructure/Data/Migrations/Helpers/`; `// DELETE-AFTER-V1.1` marker | ✓ HONORED | File exists at expected path; `:12` carries the marker comment |
| D-15 | `IDatabaseBackupService` uses `SqliteConnectionStringBuilder.DataSource`; last-3 retention via `LastWriteTimeUtc desc`; clamp [1,10] | ✓ HONORED | `DatabaseBackupService.cs:23` clamp; `:29` `SqliteConnectionStringBuilder` ctor; D-23 retention sweep verified by `DatabaseBackupServiceTests` |
| D-16 | Backfill inside `DatabaseSeeder.SeedAsync` after `MigrateAsync()`; idempotent `WHERE CanonicalDocumentJson IS NULL`; batched 50 | ✓ HONORED | `DatabaseSeeder.cs:124` `Where(r => r.CanonicalDocumentJson == null)`; batched per plan summary |
| D-17 | `CookbookTransferDocument.SchemaVersion` bumps to 2; documented two-axis | ✓ HONORED | `CookbookTransferDtos.cs:13` `= 2` |
| D-19 | `IRecipeSchemaDocumentationProvider` singleton; one method `GetFormatPrompt()` | ✓ HONORED | `IRecipeSchemaDocumentationProvider.cs` + `RecipeSchemaDocumentationProvider.cs` registered Singleton in `AddApplication()` |
| D-20 | `PromptBuilderService.ResolveRecipeFormat()` and `BuildCopyablePrompt()` both call `_docs.GetFormatPrompt()`; opt-out replaced with clarifying-question directive | ✓ HONORED | 2 call sites in `PromptBuilderService.cs`; `RecipeSchemaDocumentationProvider.cs` ends with the clarifying-question directive |
| D-21 | Hand-rolled snapshot test (no Verify/ApprovalTests); `expected-system-prompt.txt` committed | ✓ HONORED | `PromptSnapshotTests.cs` + committed fixture exist; `UPDATE_SNAPSHOTS=1` env var pattern documented |
| D-22 | Lint denylist enforcement via `PromptDenylistTests.cs` reading source files | ✓ HONORED | `PromptDenylistTests.cs` exists; case-insensitive regex over `PromptBuilderService.cs` and `RecipeSchemaDocumentationProvider.cs` |
| D-23 | Round-trip fixtures at `tests/CookBot.Tests/Fixtures/Recipes/`; ≥5 fixtures | ✓ HONORED | 5 v1-yaml + 2 v1-json-export + 1 v2-canonical = 8 fixtures (target: ≥5) |
| D-24 | `Parse(Serialize(Upcast(input))) == canonical`; non-zero `prepTimeMinutes`/`cookTimeMinutes` | ✓ HONORED | `RecipeDocumentRoundTripTests.cs` Theory+MemberData; 8/8 pass |
| D-25 | `CanonicalBackfillTests` smoke test on in-memory SQLite; 3 representative recipes; no value drift | ✓ HONORED | File exists; both facts pass |

**Audit count:** 22/22 sampled decisions honored. The unsampled three (D-11, D-18, D-23 fixture-quality details) are non-load-bearing and corroborated by SUMMARYs.

### Pitfall Mitigation Audit

| Pitfall | Description | Status | Evidence |
|---------|-------------|--------|----------|
| C1 | `IngredientRefs` lossy migration via substring fallback | ✓ MITIGATED | Substring branch deleted from `IngredientRefDetectionService`; `RecipeService` no longer writes; `LegacyRecipeProjector` ignores the column |
| C2 | Field-rename ambiguity (naked `prepTime`/`cookTime`) | ✓ MITIGATED | Canonical record uses `PrepTimeMinutes`/`CookTimeMinutes`; round-trip tests assert non-zero post-upcast |
| C3 | `IsSection` re-implementation | ✓ MITIGATED | `StepNode.cs` has zero `IsSection` references; only `ParsedStep.IsSection` survives at the legacy DTO boundary as documented |
| C4 | Destructive auto-migration without backups | ✓ MITIGATED | `DatabaseSeeder.cs:32` calls `BackupBeforeMigrationAsync` BEFORE `MigrateAsync`; gated by `GetPendingMigrationsAsync` returning non-empty; migration `Up()` is single `AddColumn` with no `Sql()` backfill |
| H1 | Recipe missing `version` field after import | ✓ MITIGATED | `RecipeFormatParser.cs:103,105` stamps `version=1` if absent; `RecipeUpcasterChain.UpcastToCurrent` defaults to 1 if absent; `RecipeUpcasterTests` includes the missing-version case |
| H2 | Forward-compat unknown field loss | ✓ MITIGATED | `[JsonExtensionData] Extras` on 4 record types; `ExtrasRoundTripTests` passes |
| H4 | Extras not propagated through edit/save | ✓ MITIGATED | STJ round-trips Extras automatically; `ExtrasRoundTripTests` verifies all 4 sites |
| H6 | Opt-out language regression | ✓ MITIGATED | `PromptDenylistTests.cs` runs on every test invocation; 0 matches in `PromptBuilderService.cs`, `RecipeSchemaDocumentationProvider.cs`, `expected-system-prompt.txt` |

**Note on `CookingMode.razor:140`:** A read of `CurrentStep.IngredientRefs.Contains(...)` survives in the cooking-mode renderer. This is **expected and documented** by the Plan 02 SUMMARY: existing recipes still carry data in that column and writes are retired this phase only. Phase 4 (POLISH-03) drops the column and cleans this read up alongside `LegacyRecipeProjector`. Not a regression for the Phase 1 goal.

### Anti-Patterns Found

No blocker anti-patterns. Casual scan of files modified in this phase:

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/CookBot.Web/Components/Pages/CookingMode.razor` | 140 | `CurrentStep.IngredientRefs.Contains(...)` legacy read | ℹ Info | Deliberate per D-13; Phase 4 cleans it up |
| `tests/CookBot.Tests/Migration/CanonicalBackfillTests.cs` | (n/a) | Find-or-create dedup helper for `Ingredients.NormalizedName` UNIQUE constraint | ℹ Info | Documented in Plan 03 SUMMARY as a Rule 1 fix to a test helper; matches production behavior |

No `TODO`, `FIXME`, `XXX`, `placeholder`, `coming soon`, or empty-implementation matches in Phase 1 source files.

### Behavioral Spot-Checks

Targeted commands run against the built solution.

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build clean | `dotnet build FreelovesCookBot.sln -c Debug` | 0 warnings, 0 errors | ✓ PASS |
| Full test suite | `dotnet test FreelovesCookBot.sln --no-build -c Debug` | 122/122 passed | ✓ PASS |
| Round-trip suite | `dotnet test --filter "FullyQualifiedName~RecipeDocumentRoundTripTests"` | 8/8 passed | ✓ PASS |
| Extras round-trip | `dotnet test --filter "FullyQualifiedName~ExtrasRoundTripTests"` | 5/5 passed | ✓ PASS |
| Prompt denylist | `dotnet test --filter "FullyQualifiedName~PromptDenylistTests"` | passed | ✓ PASS |
| Prompt snapshot | `dotnet test --filter "FullyQualifiedName~PromptSnapshotTests"` | passed | ✓ PASS |
| Canonical backfill | `dotnet test --filter "FullyQualifiedName~CanonicalBackfillTests"` | 2/2 passed | ✓ PASS |
| EF migration body has no `Sql()` | `grep migrationBuilder\.Sql src/CookBot.Infrastructure/Migrations/20260425223916_RecipeCanonicalDocument.cs` | 0 matches (Pitfall C4 mitigated) | ✓ PASS |
| Domain stays package-free | `grep PackageReference src/CookBot.Domain/CookBot.Domain.csproj` | 0 matches | ✓ PASS |
| No Newtonsoft/NJsonSchema in src | `grep -rE '<PackageReference[^>]*(Newtonsoft\|NJsonSchema)' src/` | 0 matches | ✓ PASS |
| Single new package | `JsonSchema.Net 9.2.*` in `CookBot.Application.csproj` | 1 line | ✓ PASS |
| 6 DI singletons + 1 IRecipeUpcaster | `grep -cE 'AddSingleton<...>' src/CookBot.Application/DependencyInjection.cs` | 6 | ✓ PASS |

All spot-checks pass.

### Key-Link Verification

| From | To | Via | Status |
|------|-----|-----|--------|
| `DependencyInjection.cs` (`AddApplication`) | `Recipes/*` services | `AddSingleton<...>` × 6 + `IRecipeUpcaster` × 1 | ✓ WIRED |
| `RecipeJsonSchemaProvider.cs` | `RecipeDocument` | `JsonSchemaExporter.GetJsonSchemaAsNode(typeof(RecipeDocument), exporterOptions)` | ✓ WIRED |
| `Migration_V1_To_V2.cs` | `RecipeUpcasterChain.cs` | `: IRecipeUpcaster` registered in DI; chain consumes `IEnumerable<IRecipeUpcaster>` | ✓ WIRED |
| `RecipeFormatParser.cs` | `RecipeUpcasterChain` / `JsonRecipeSerializer` / `RecipeValidator` | Constructor injection; `UpcastToCurrent` in `TryParse` pipeline; 11 references | ✓ WIRED |
| `IngredientRefDetectionService.cs` | `MarkdownLinkPattern.Matches` only | Compiled regex; substring fallback deleted | ✓ WIRED |
| `DatabaseSeeder.cs` | `IDatabaseBackupService.BackupBeforeMigrationAsync` | Pre-migration ordering verified at lines 29/32 | ✓ WIRED |
| `DatabaseSeeder.cs` | `LegacyRecipeProjector` (via DI) | Backfill loop; idempotent predicate at `:124` | ✓ WIRED |
| `RecipeService.cs` | `LegacyRecipeProjector` (via `IRecipeProjector`) + `JsonRecipeSerializer` | Layer-inversion-safe via factory-closure DI; both `CreateAsync` and `UpdateAsync` write `CanonicalDocumentJson` | ✓ WIRED |
| `PromptBuilderService.cs` | `IRecipeSchemaDocumentationProvider.GetFormatPrompt()` | Constructor injection; 2 call sites; both literal blocks deleted | ✓ WIRED |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `Recipe.CanonicalDocumentJson` | `recipe.CanonicalDocumentJson` | `_canonicalSerializer.Serialize(_projector.Project(recipe))` in `RecipeService.CreateAsync`/`UpdateAsync` | Yes — projects from real relational entity into canonical JSON | ✓ FLOWING |
| `RecipeJsonSchemaProvider._schema` | `Lazy<JsonNode>` | `JsonSchemaExporter.GetJsonSchemaAsNode(typeof(RecipeDocument))` post-walked | Yes — schema exporter reflects real type metadata | ✓ FLOWING |
| `BackfillCanonicalDocumentAsync` | recipes batch | `Where(r => r.CanonicalDocumentJson == null).Take(50).ToListAsync()` | Yes — real EF query against in-memory test DB and production SQLite | ✓ FLOWING |
| `RecipeFormatParser.TryParse` | `out parsed` | YAML/JSON → JsonNode → upcaster chain → `RecipeDocument` → `ProjectToParsedRecipe` | Yes — round-trip tests exercise real fixtures | ✓ FLOWING |

### Human Verification Required

(none — all phase-1 work is infrastructure; downstream user-visible behavior changes are minimal and the only one in scope — opt-out clause removal — is locked by `PromptDenylistTests` and `PromptSnapshotTests`)

## Gaps Summary

No gaps. Phase 1 delivered the canonical recipe-format scaffold end-to-end:

- One versioned `RecipeDocument` is the single source of truth across YAML wire (paste-in adapter), JSON export (`JsonRecipeSerializer`), DB JSON column (`Recipe.CanonicalDocumentJson`), and AI prompt (`RecipeSchemaDocumentationProvider`).
- The V1→V2 upcaster reconciles all four documented legacy quirks (`prepTime`, `cookTime`, `localId`, `IsSection: bool`/`Text`).
- `cookbot.db` data migrates safely: pre-migration backup with last-3 retention, idempotent backfill via `LegacyRecipeProjector`, hybrid persistence preserved.
- The AI opt-out clause is gone; both prompt sites delegate to a single source of truth; snapshot test and lint denylist enforce the no-regression invariant on every test run.
- Forward-compatible `Extras` round-trip is verified at all 4 `[JsonExtensionData]` sites.
- 122/122 tests pass.

The single noted item — `CookingMode.razor:140` reading the legacy `IngredientRefs` column — is **deliberate per D-13** (column persists one milestone for safe rollback) and explicitly scheduled for Phase 4 (POLISH-03) cleanup. Not a Phase 1 gap.

---

*Verified: 2026-04-25*
*Verifier: Claude (gsd-verifier, opus 4.7 1M)*
