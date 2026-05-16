---
phase: 08-format-foundation
verified: 2026-05-16T06:00:00Z
status: passed
score: 5/5 must-haves verified
overrides_applied: 0
re_verification: false
---

# Phase 8: Format Foundation Verification Report

**Phase Goal:** The canonical `RecipeDocument` advances to v3 — all three new nullable fields (PhotoUrl, Description, per-step Temperature) exist in the type system, the upcaster chain, the EF entity columns, the AI schema contract, the YAML/JSON wire format, and the parser — and the four v1.1 format-cleanup carry-forwards ship: LegacyRecipeProjector deleted, TagsJson migrated to a relational RecipeTag table, prompt snapshot regression test in place, and README format section added.

**Verified:** 2026-05-16T06:00:00Z
**Status:** PASSED
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A v2 `.cookbook.json` imported after this phase upcasts to v3 with all three new fields null — no data loss, no throw | VERIFIED | `Migration_V2_To_V3.cs` implements `FromVersion=2,ToVersion=3` with three independent per-field no-op guards (D-29); 5-fixture Theory matrix covers all per-field combinations; `RecipeUpcasterChain.CurrentVersion=3` confirmed |
| 2 | `RecipeJsonSchemaProvider` emits a JSON schema that includes `photoUrl`, `description`, and step-level `temperature` — schema-assertion test (SCHEMA-11) passes | VERIFIED | `RecipeJsonSchemaProvider` uses `JsonSchemaExporter` auto-reflecting `RecipeDocument` — zero manual field additions; `SchemaAssertionTests.cs` has 3 Fact methods asserting all three fields; 247/247 tests pass |
| 3 | `RecipeFormatParserTests` cover all three new fields — round-trip fixtures for null, valid value, and all three temperature units (F/C/gas) all pass; no existing test deleted | VERIFIED | 5 new parser tests added covering null temperature, F/C/Gas `TryParse`, gas half-step 4.5, and `SerializeIndented` gas-glyph rendering; zero string-blob assertions (H11 audit passed); 3 v3-canonical fixture files added |
| 4 | `LegacyRecipeProjector` and `IRecipeProjector` files are deleted; `grep -r "LegacyRecipeProjector\|IRecipeProjector" src/` returns zero hits; startup null-canonical guard in `DatabaseSeeder.SeedAsync` fails loud | VERIFIED | Both source files deleted via `git rm`; grep in src/ returns zero hits; `DatabaseSeeder.SeedAsync` throws `InvalidOperationException` with row count + restore hint on null `CanonicalDocumentJson` rows; D-32 5-step sequence honored: guard commit `ac75fc4` precedes deletion commit `0625a86` |
| 5 | Home pantry-match dietary filtering can use SQL JOIN against `RecipeTag` rows — `TagsJson` superseded; prompt-snapshot test byte-stable; README "Recipe Format" section documents v3 | VERIFIED | `RecipeTag` relational table created with composite unique index `(RecipeId, Name)` + cascade-delete; all 6 production callsites switched to relational reads; `DropTagsJsonColumn` migration (20260516041718) drops the column; `Verify.Xunit 31.12.5` snapshot test wired with approved `.verified.txt`; README `## Recipe Format` section at line 44 with v3 YAML/JSON examples and V1→V2→V3 lineage |

**Score:** 5/5 truths verified

---

## Requirements Coverage

| REQ-ID | Status | Evidence |
|--------|--------|----------|
| SCHEMA-01 | SATISFIED | `RecipeDocument.PhotoUrl: string?` with `[MaxLength(2048)]` at `src/CookBot.Domain/Recipes/RecipeDocument.cs:31` |
| SCHEMA-02 | SATISFIED | `RecipeDocument.Description: string?` with `[MaxLength(4096)]` at `src/CookBot.Domain/Recipes/RecipeDocument.cs:35` |
| SCHEMA-03 | SATISFIED | `StepTemperature` sealed record with `decimal Value` + `TemperatureUnit Unit`; `TemperatureUnit` enum (F/C/Gas); `ContentStep.Temperature: StepTemperature?` at `StepNode.cs:25` |
| SCHEMA-04 | SATISFIED | `RecipeUpcasterChain.CurrentVersion = 3`; `Migration_V2_To_V3` with `FromVersion=2, ToVersion=3`; DI registration in `DependencyInjection.cs`; three independent per-field null-coalescing guards per D-29/PITFALLS C7 |
| SCHEMA-05 | SATISFIED | `Recipe.PhotoUrl` entity column (max 2048) in `Recipe.cs`; `AddRecipePhotoUrlAndDescription` migration (20260516032653) with `maxLength:2048`; backup label derived dynamically per D-31 |
| SCHEMA-06 | SATISFIED | `Recipe.Description` entity column (max 4096) in `Recipe.cs`; same migration as SCHEMA-05 with `maxLength:4096` |
| SCHEMA-07 | SATISFIED | `RecipeJsonSchemaProvider` uses only `JsonSchemaExporter.GetJsonSchemaAsNode(typeof(RecipeDocument))` — zero manual field additions; `[MaxLength]` attributes propagate `maxLength` to AI schema automatically |
| SCHEMA-08 | SATISFIED | `RecipeFormatParser.Serialize()` emits `photoUrl`, `description`, and per-step `temperature: { value, unit }` in YAML wire format; `ProjectToParsedRecipe()` carries all three fields; `ParsedRecipe`/`ParsedStep` extended; `IRecipeFormatParser.cs` updated |
| SCHEMA-09 | SATISFIED | `JsonRecipeSerializer` wires `StepTemperatureJsonConverter` into `_indented`; gas half-stops render as "4½" in indented export; `_compact` retains ASCII-safe encoding for DB column; 3 v3-canonical fixture round-trip tests pass |
| SCHEMA-10 | SATISFIED | `PromptDenylistTests.Denylist` regex extended with `image\|imageUrl\|picture\|summary\|desc\|temp\|oven`; self-check Fact asserts `imageUrl` fires and `temperature` does NOT fire `\btemp\b`; XML doc `<summary>` tags removed from scanned files; "oven temperatures" changed to "baking temperatures" in PromptBuilderService.cs |
| SCHEMA-11 | SATISFIED | `SchemaAssertionTests.cs` with 3 Fact methods: `GetSchema_Includes_PhotoUrl_Description`, `GetSchema_StepTemperature_NullableShape`, `GetSchema_AdditionalPropertiesFalse_OnStepTemperatureSubschema` — all pass GREEN; committed before any v3 production code per PITFALLS C8 |
| SCHEMA-12 | SATISFIED | H11 audit completed: zero string-blob assertions found in `RecipeFormatParserTests`; audit header comment added; 5 new tests added covering null, F/C/Gas Theory, gas half-step 4.5, SerializeIndented glyph; all pass |
| CLEAN-01 | SATISFIED | D-32 5-step sequence completed: guard (`ac75fc4`) → projector replacement → param removal → DI removal → file deletion (`0625a86`); `IRecipeProjector.cs` and `LegacyRecipeProjector.cs` deleted; `grep -r "LegacyRecipeProjector\|IRecipeProjector" src/` returns 0 hits; null-canonical guard is permanent (D-33) |
| CLEAN-02 | SATISFIED | `RecipeTag(Id, RecipeId, Name)` POCO entity; `RecipeTagConfiguration` with composite unique index `(RecipeId, Name)` + FK cascade; `AddRecipeTagTable` migration (20260516034336) with `json_each` backfill SQL + `TRIM` + `ON CONFLICT DO NOTHING`; 5 callsites switched; `DropTagsJsonColumn` migration (20260516041718) drops column; dual-write period honored per D-26 sequencing |
| CLEAN-03 | SATISFIED | `Verify.Xunit 31.12.5` added to `CookBot.Tests.csproj`; `ModuleInitializer.cs` routes snapshots to `Snapshots/`; `PromptSnapshotTests.cs` replaced with Verify-based implementation; `PromptSnapshotTests.BuildSystemPrompt.verified.txt` committed; legacy `expected-system-prompt.txt` deleted |
| CLEAN-04 | SATISFIED | `## Recipe Format` section in `README.md` at line 44; five subsections per D-37: canonical description, YAML wire example (all v3 fields), JSON export example (gas "4½" rendering), V1→V2→V3 lineage bullets, internally-managed-format note; all canonical field names used (no alias leakage) |

---

## Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/CookBot.Domain/Recipes/StepTemperature.cs` | StepTemperature record + TemperatureUnit enum | VERIFIED | `sealed record StepTemperature` with `required decimal Value` + `required TemperatureUnit Unit`; enum F/C/Gas with `JsonStringEnumConverter<TemperatureUnit>` |
| `src/CookBot.Application/Recipes/Migration_V2_To_V3.cs` | Upcaster with three per-field guards | VERIFIED | `FromVersion=2, ToVersion=3`; three independent D-29 no-op guard blocks; stamps `version: 3`; M2 guard for ContentStep-only temperature |
| `src/CookBot.Application/Recipes/Converters/StepTemperatureJsonConverter.cs` | Gas half-stop "4½" rendering | VERIFIED | Write: gas half-stops → `"4½"` Unicode string; Read: accepts both object `{ value, unit }` and string forms; wired into `_indented` only |
| `src/CookBot.Domain/Entities/RecipeTag.cs` | Tag entity (Id, RecipeId, Name) | VERIFIED | POCO with 4 properties; Recipe navigation; no framework references |
| `src/CookBot.Infrastructure/Data/Configurations/RecipeTagConfiguration.cs` | Composite unique index + cascade FK | VERIFIED | `HasIndex(t => new { t.RecipeId, t.Name }).IsUnique()`; `OnDelete(DeleteBehavior.Cascade)` |
| `src/CookBot.Infrastructure/Migrations/20260516032653_AddRecipePhotoUrlAndDescription.cs` | PhotoUrl + Description columns | VERIFIED | Two `AddColumn<string>` calls; `maxLength:2048` and `maxLength:4096`; both `nullable:true` |
| `src/CookBot.Infrastructure/Migrations/20260516034336_AddRecipeTagTable.cs` | RecipeTag table + json_each backfill | VERIFIED | `CreateTable RecipeTags`; composite unique index; `json_each` backfill SQL with TRIM + ON CONFLICT DO NOTHING |
| `src/CookBot.Infrastructure/Migrations/20260516041718_DropTagsJsonColumn.cs` | Drop TagsJson column | VERIFIED | `DropColumn("TagsJson", "Recipes")` in Up(); symmetric AddColumn in Down() |
| `src/CookBot.Infrastructure/Migrations/20260516034227_AddPantryMatchIndexes.cs` | RecipeIngredients composite index | VERIFIED | `CreateIndex IX_RecipeIngredients_RecipeId_IngredientId`; PantryItems index already existed (pre-existing unique index satisfies requirement) |
| `tests/CookBot.Tests/Recipes/SchemaAssertionTests.cs` | 3 schema assertion Facts | VERIFIED | All 3 Facts pass GREEN: photoUrl/description presence, StepTemperature nullable shape, additionalProperties:false on subschema |
| `tests/CookBot.Tests/ModuleInitializer.cs` | Verifier.DerivePathInfo to Snapshots/ | VERIFIED | `[ModuleInitializer]` with `Verifier.DerivePathInfo` routing to `{projectDirectory}/Snapshots/` |
| `tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt` | Approved snapshot | VERIFIED | 48 lines; committed; no pending `.received.txt` |
| `README.md ## Recipe Format` | v3 YAML/JSON examples + lineage | VERIFIED | Section at line 44; all v3 fields; gas "4½" rendering; V1→V2→V3 migration bullets; forward-only note |
| ~~`src/CookBot.Application/Recipes/IRecipeProjector.cs`~~ | DELETED | VERIFIED | File absent; `ls` returns "No such file" |
| ~~`src/CookBot.Infrastructure/Data/Migrations/Helpers/LegacyRecipeProjector.cs`~~ | DELETED | VERIFIED | File absent; `ls` returns "No such file" |
| ~~`tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt`~~ | DELETED | VERIFIED | File absent; replaced by Verify snapshot |

---

## Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `RecipeDocument.PhotoUrl/Description` | `RecipeJsonSchemaProvider` schema | `JsonSchemaExporter` auto-reflection | WIRED | No manual additions; `[MaxLength]` attributes propagate `maxLength` to AI schema |
| `ContentStep.Temperature` | `RecipeFormatParser` | `TemperatureFrontmatter` inner class | WIRED | Parser reads/writes `temperature: { value, unit }` YAML; `ProjectToParsedRecipe` carries `Temperature` to `ParsedStep` |
| `ParsedRecipe.PhotoUrl/Description/Temperature` | `RecipeService.CreateAsync/UpdateAsync` | Direct `new RecipeDocument { ... }` construction | WIRED | All 3 fields assigned from `parsed.*` at lines 107-117 and 208-218 in `RecipeService.cs` |
| `RecipeTag` table | Application read paths | `.Include(r => r.Tags)` in callers | WIRED | Present in `RecipeEditor.razor:381`, `CookbookTransferService.cs:49`, `CookingMode.razor:668`; `RecipeCookingAiContext.cs:19-20` reads from `recipe.Tags` |
| `Migration_V2_To_V3` | `RecipeUpcasterChain` | `IRecipeUpcaster` + DI registration | WIRED | Singleton registration in `DependencyInjection.cs`; `FromVersion=2, ToVersion=3` confirmed |
| `PromptBuilderService` | `Verify.Xunit` snapshot | `Verifier.Verify(actual)` in `PromptSnapshotTests` | WIRED | `BuildSystemPrompt` Fact returns `Verifier.Verify(actual)`; `.verified.txt` approved and committed |
| `StepTemperatureJsonConverter` | `JsonRecipeSerializer._indented` | `_indented.Converters.Add(...)` | WIRED | Confirmed at `JsonRecipeSerializer.cs:38`; `_compact` is unaffected |
| null-canonical guard | `DatabaseSeeder.SeedAsync` | `context.Recipes.CountAsync(r => r.CanonicalDocumentJson == null)` | WIRED | Guard at `DatabaseSeeder.cs:44-49`; throws `InvalidOperationException` with restore hint |

---

## Locked Decision Verification (D-26..D-37)

| Decision | Status | Evidence |
|----------|--------|----------|
| **D-26**: TagsJson drops within Phase 8, two-migration sequence | HONORED | `AddRecipeTagTable` (20260516034336) precedes `DropTagsJsonColumn` (20260516041718) in both timestamp order and git history; dual-write period in Plans 08→11 preserved rollback granularity |
| **D-27**: `ContentStep.Temperature` uses `decimal Value` (not int) | HONORED | `sealed record StepTemperature` has `required decimal Value`; F/C require whole-degree (validator rejects fractional); Gas accepts 0.5-step multiples in [1.0, 9.5] |
| **D-28**: `PhotoUrl` and `Description` max-lengths enforced in EF fluent API | HONORED | `HasMaxLength(2048)` and `HasMaxLength(4096)` in `RecipeConfiguration`; `[MaxLength]` attributes also on `RecipeDocument` for AI schema propagation |
| **D-29**: Single `Migration_V2_To_V3` class with three independent per-field guards | HONORED | Three separate `if (obj["X"] is null) { /* no-op */ }` blocks; no bundle-throw per PITFALLS C7 |
| **D-30**: `RecipeDocument.Version` constant and `CurrentVersion` both = 3 | HONORED | `RecipeUpcasterChain.CurrentVersion = 3` confirmed; `RecipeDocument.Version` is an instance property (not constant) holding the per-document version value |
| **D-31**: Four EF migrations with per-migration backup label | HONORED | All 4 migrations exist; `DatabaseSeeder` derives backup label from `pending[0].Split('_', 2)[1]` — each migration produces its own `.pre-{Name}.bak` |
| **D-32**: D-32 5-step LegacyRecipeProjector deletion order | HONORED | Guard commit `ac75fc4` (step a) appears earlier in git history than deletion commit `0625a86` (steps b-e); line numbers confirm: guard at position 14, deletion at position 13 (newest-first log) |
| **D-33**: null-canonical guard is permanent (no DELETE-AFTER marker) | HONORED | Guard in `DatabaseSeeder.SeedAsync` has no deletion comment; documented as "permanent structural invariant" in SUMMARY |
| **D-34**: `RecipeTag.Name` stored as trim + preserve-case | HONORED | Backfill SQL uses `TRIM(json_each.value)`; composite UNIQUE index `(RecipeId, Name)` is case-sensitive per SQLite default; "Vegan" + "vegan" coexist as distinct tags (verified by `RecipeTagBackfillTests`) |
| **D-35**: Replace hand-rolled `PromptSnapshotTests` + delete `expected-system-prompt.txt` | HONORED | `PromptSnapshotTests.cs` uses `Verifier.Verify(actual)`; `expected-system-prompt.txt` deleted; `ModuleInitializer.cs` routes to `Snapshots/`; `.verified.txt` committed |
| **D-36**: `PromptDenylistTests.cs` STAYS and is extended | HONORED | Denylist regex extended with 7 SCHEMA-10 alias tokens; `Denylist_FiresOn_AliasToken_InSyntheticInput` self-check Fact added |
| **D-37**: README "Recipe Format" section inline in `README.md` (not `docs/`) | HONORED | `## Recipe Format` section at `README.md:44`; all 5 D-37 subsections present; inline in existing README |

---

## Test Results

| Suite | Command | Result |
|-------|---------|--------|
| Non-API tests | `dotnet test --filter "Category!=RequiresApiKey"` | **247/247 PASS** (0 failed, 0 skipped) |

Test count evolution across the phase: 208 (start) → 208 (Plan 04) → 232 (Plan 05) → 246 (Plan 09) → 247 (Plan 08) → 248 (Plan 10) → 247 (Plan 11, one projector test removed) — final is **247/247**.

---

## Data-Flow Trace (Level 4)

The three v3 fields render dynamic data through the full stack:

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `RecipeService.CreateAsync` | `parsed.PhotoUrl`, `parsed.Description`, step `Temperature` | `RecipeFormatParser.TryParse` → `ParsedRecipe` | Yes — from canonical JSON document via parser | FLOWING |
| `RecipeTagBackfillTests` | `RecipeTag` rows from `json_each` backfill | `migrationBuilder.Sql` embedded in `AddRecipeTagTable` | Yes — reads `TagsJson` column and inserts relational rows | FLOWING |
| `PromptSnapshotTests` | `BuildSystemPrompt` string | `PromptBuilderService` with `TestHost.GetPromptBuilderService()` | Yes — real service call, no mocks | FLOWING |
| `RecipeJsonSchemaProvider` | JSON schema with `photoUrl`, `description`, `temperature` | `JsonSchemaExporter.GetJsonSchemaAsNode(typeof(RecipeDocument))` | Yes — reflects actual C# type including `[MaxLength]` attributes | FLOWING |

---

## Anti-Patterns Found

| File | Pattern | Severity | Assessment |
|------|---------|----------|------------|
| None found | — | — | No TBD/FIXME/XXX/TODO/HACK/PLACEHOLDER markers in phase-modified files; no empty implementations; no stubs in production code paths |

Notable: One `TODO`-adjacent comment in `DatabaseSeeder.cs` line is "// Step 1:" style enumeration — not a debt marker.

---

## Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Test suite (non-API) passes | `dotnet test --filter "Category!=RequiresApiKey"` | 247/247 PASS | PASS |
| `IRecipeProjector` absent from src/ | `grep -r "LegacyRecipeProjector\|IRecipeProjector" src/` | 0 hits | PASS |
| `TagsJson` absent from src/ (excl. Migrations/) | `grep -rn "TagsJson" src/ --include="*.cs" \| grep -v Migrations/` | 1 hit (doc comment in `RecipeTag.cs` mentioning legacy name) | PASS — comment only, no functional reference |
| `RecipeJsonSchemaProvider` has no manual field additions | `grep -n "photoUrl\|description\|temperature" RecipeJsonSchemaProvider.cs` | 0 hits | PASS |
| All 4 EF migrations exist with correct timestamps | `ls Migrations/*.cs \| grep -E "AddRecipePhotoUrl\|AddRecipeTagTable\|DropTagsJson\|AddPantryMatch"` | 4 migration files + 4 Designer files | PASS |
| D-32 guard commit before deletion commit | `git log --oneline \| grep -n "ac75fc4\|0625a86"` | guard at line 14 (older), deletion at line 13 (newer) | PASS |
| D-26 AddRecipeTagTable before DropTagsJsonColumn | Timestamp comparison: 034336 < 041718 | Correct order | PASS |

---

## Human Verification Required

None — all observable truths can be verified programmatically. Phase 8 has no UI deliverables (`UI hint: no` in ROADMAP.md).

---

## Gaps Summary

No gaps found. All 16 requirements (SCHEMA-01..12, CLEAN-01..04) are satisfied. All 5 success criteria are met. All 12 locked decisions (D-26..D-37) are honored. The test suite passes 247/247.

**One notable deviation that was handled correctly:** Plan 12 (AddPantryMatchIndexes) discovered that `PantryItem` uses `PantryId` (not `UserId`) and that `PantryItemConfiguration` already created an equivalent unique composite index — the migration therefore creates only the `RecipeIngredients` index. The Phase 10 QOL-03 performance requirement is still fully satisfied, and the deviation was appropriately documented and handled.

---

## Recommendation

**READY TO SHIP.** Phase 8 goal is fully achieved. The codebase evidence confirms:

1. `RecipeDocument` is at v3 in the type system, upcaster chain, EF columns, AI schema, YAML/JSON wire format, and parser.
2. `LegacyRecipeProjector` and `IRecipeProjector` are completely gone from the codebase.
3. `RecipeTag` relational table is the sole source of truth for recipe tags; `TagsJson` column dropped.
4. Verify.Xunit snapshot test replaces the hand-rolled fixture; byte-stable across runs.
5. README "Recipe Format" section documents the canonical v3 format with worked examples.
6. 247/247 tests pass.

Phase 9 (Photos + Prod-Ready Infrastructure) can proceed — its declared dependencies (`PhotoUrl` field on `RecipeDocument v3` and `RecipeTag` table) are both in place.

---

*Verified: 2026-05-16T06:00:00Z*
*Verifier: Claude (gsd-verifier)*
