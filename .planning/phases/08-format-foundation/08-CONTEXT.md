# Phase 8: Format Foundation - Context

**Gathered:** 2026-05-15
**Status:** Ready for planning
**Mode:** discuss (4 gray areas surfaced; all 4 resolved by user)

<domain>
## Phase Boundary

Bump the canonical `RecipeDocument` from v2 to v3, landing three nullable additions (`PhotoUrl`, `Description`, per-step `Temperature`) through the entire stack — record type, upcaster chain, EF entity columns, AI JSON schema, YAML wire format, JSON export, and parser. Concurrently ship the four v1.1 format-cleanup carry-forwards: delete `LegacyRecipeProjector` (closing the `// DELETE-AFTER-V1.1` debt from Phase 1), migrate `Recipe.TagsJson` to a relational `RecipeTag` table, replace Phase 1's hand-rolled prompt snapshot test with `Verify.Xunit`, and add a README "Recipe Format" section.

**This phase delivers data layer + schema infrastructure.** UI surfacing of the three new fields (photo editor composite, Description subtitle, Temperature display in cooking mode) is **out of scope** — that lands in Phase 9 (photos surface) and Phase 10 (consumer features). The phase's user-visible signal is "v2 cookbooks import without data loss; AI now emits photoUrl/description/temperature when relevant."

**In scope:** SCHEMA-01..12, CLEAN-01..04 (16 requirements).

**Not in scope** (deferred to later phases — do not pull forward):
- Photo upload pipeline + paste-URL UI + `<img>` rendering → Phase 9 (PHOTO-01..14)
- Description subtitle in editor + lede in RecipeView → Phase 9 (rides with the photo composite)
- Temperature display in cooking mode + editor temperature picker → Phase 9 (rides with photo work)
- `IDataProtector` encrypt-at-rest + Docker + token telemetry → Phase 9 (PROD-*)
- Smart pantry-match + dietary filter (uses RecipeTag) + accent picker + Profile widgets → Phase 10 (QOL-*, POLISH-*)

</domain>

<decisions>
## Implementation Decisions

### TagsJson drop timing

- **D-26 (Area 1):** `TagsJson` column drops **within Phase 8** via a separate follow-up migration, NOT atomically with `AddRecipeTagTable`. Two migration sequence: (a) `AddRecipeTagTable` creates the relational table, runs the backfill from `TagsJson`, and switches all six callsites (`RecipeService.CreateAsync`/`UpdateAsync`, `RecipeEditor.razor` load path, `CookbookTransferService` export, `RecipeCookingAiContext`, `LegacyRecipeProjector`'s own read in case it executes during backfill) to read/write through the new table; (b) `DropTagsJsonColumn` lands after the callsite switchover + tests pass, with its own pre-migration backup. v1.3 ships with one source of truth for tags. Rationale: rollback granularity — if the backfill or any callsite migration is broken, reverting `DropTagsJsonColumn` alone restores the column without losing the relational data. (User answered "Same phase, second migration".)

### Schema additions

- **D-27 (Area 2):** `ContentStep.Temperature` value type is **`decimal`** (not `int` per draft REQUIREMENTS.md SCHEMA-03). Final shape: `record StepTemperature(decimal Value, TemperatureUnit Unit)` where `TemperatureUnit` is the existing `"F" | "C" | "gas"` enum. Rationale: UK gas-mark dial half-stops (gas 4½, 7½) are a real authoring concern; `int` would force authors to round. Per-unit validator rules: **F and C require whole-degree values** (validator error on fractional input — recipes never call for "bake at 350.5°F"); **gas accepts 0.5-step increments** (`Value % 0.5m == 0`, range 1.0–9.5). JSON Schema emitted to Anthropic: `"type": "number"` for `value`. Custom `JsonConverter<StepTemperature>` renders gas half-stops as "4½" / "7½" for human-readable JSON export only — wire YAML stores `temperature: { value: 4.5, unit: "gas" }`. (User answered "Change to `Value: decimal`".)
- **D-28:** `RecipeDocument.PhotoUrl: string?` and `RecipeDocument.Description: string?` shapes match REQUIREMENTS.md SCHEMA-01/02 exactly. No additional length validation at the canonical-record layer — EF column lengths (2048 / 4096) are the only enforcement; AI schema gets `"maxLength"` from `JsonSchemaExporter` only if a `[MaxLength]` attribute is applied (planner's call whether to add it).

### Upcaster shape

- **D-29:** `Migration_V2_To_V3` is a **single upcaster class** (not three composable mini-upcasters), matching Phase 1's `Migration_V1_To_V2` precedent. Internally, it null-coalesces per-field (NOT a single bundle-throw, per PITFALLS C7) — three independent `if (root["photoUrl"] is null) ...` style guards in the `Upcast(JsonNode)` body, each handling its field idempotently. One DI registration: `services.AddSingleton<IRecipeUpcaster, Migration_V2_To_V3>();`. Test coverage: one `[Theory]` driven by a fixture matrix that covers each combination of missing/present fields independently — gives the per-field test isolation without the per-class proliferation. (Claude's discretion — confirmed against Phase 1's pattern; user did not select this area.)
- **D-30:** `RecipeDocument.Version` constant bumps from `2` to `3`. JSON Schema `enum: [3]` (writes only). `RecipeUpcasterChain.CurrentVersion` bumps to `3`. Reads accept any `int >= 1` per Phase 1's D-04 contract — the chain handles V1→V2→V3 hop-by-hop.

### EF migrations

- **D-31 (Area 3):** Phase 8 ships **four EF migrations**, each with its own pre-migration backup file:
  1. `AddRecipePhotoUrlAndDescription` — two nullable string columns on `Recipe`. Combined into one migration because both are trivial nullable adds with no data motion. Backup: `cookbot.db.pre-AddRecipePhotoUrlAndDescription.bak`.
  2. `AddRecipeTagTable` — creates `RecipeTag(Id, RecipeId, Name)`, composite index on `(RecipeId, Name)`, FK to `Recipe(Id)` with cascade delete; embeds data backfill from `TagsJson` via raw SQL (or `migrationBuilder.Sql(...)`); same migration. Six callsite switchovers ship in the same atomic phase (separate commits, same phase). Backup: `cookbot.db.pre-AddRecipeTagTable.bak`.
  3. `DropTagsJsonColumn` — drops `Recipe.TagsJson` after callsite switchover + tests pass. Backup: `cookbot.db.pre-DropTagsJsonColumn.bak`.
  4. `AddPantryMatchIndexes` — composite indexes on `RecipeIngredient(RecipeId, IngredientId)` and `PantryItem(UserId, IngredientId)` for Phase 10 QOL-03 readiness (no functional change in Phase 8). Bundling here means Phase 10 can be a pure code-and-test phase with zero EF migrations. Backup: `cookbot.db.pre-AddPantryMatchIndexes.bak`.

  Sequence enforced via standard `dotnet ef migrations add` timestamp ordering. Each is forward-only; downgrade is unsupported.

### Format cleanup

- **D-32 (CLEAN-01):** `LegacyRecipeProjector` + `IRecipeProjector` deletion follows the exact 5-step sequence in REQUIREMENTS.md: (a) add startup null-canonical guard in `DatabaseSeeder.SeedAsync` (fail-loud if any row has null `CanonicalDocumentJson` — `throw new InvalidOperationException` with row count and remediation hint), (b) replace `_projector.Project(recipe)` in `RecipeService` with direct `RecipeDocument` construction from `ParsedRecipe`, (c) remove `IRecipeProjector` from `RecipeService` constructor, (d) remove `IRecipeProjector` DI registration, (e) delete the two source files. Order matters: the guard must be in place before the projector is removed, otherwise a startup with any pre-existing null row would fail silently downstream instead of loudly at boot.
- **D-33:** The null-canonical guard added in step (a) is **permanent**, not temporary. Unlike Phase 1's `// DELETE-AFTER-V1.1` projector marker (which was a known-throwaway), this guard is a structural invariant going forward — any future code path that creates a `Recipe` without writing `CanonicalDocumentJson` is a bug, and the guard is the load-bearing detection mechanism. No deletion marker.
- **D-34 (Area 4b):** `RecipeTag.Name` storage convention is **trim + preserve case**. Backfill from `TagsJson` trims whitespace but preserves original casing. New tag inserts: same — `Name = input.Trim()`. Composite UNIQUE index on `(RecipeId, Name)` is case-sensitive by default in SQLite — "Vegan" and "vegan" on the same recipe **coexist as two distinct tags**. Rationale: matches existing `TagsJson` freeform behavior (zero migration semantics change); least surprising for the user's existing data. Future dedup (case-insensitive) is a v1.4+ concern if it turns out to be a real problem.

### Prompt snapshot test

- **D-35 (Area 4a):** **Replace** Phase 1's hand-rolled `PromptSnapshotTests.cs` + `expected-system-prompt.txt` fixture with `Verify.Xunit 31.12.5`. New `PromptSnapshotTests.cs`:
  - Class decorated `[UsesVerify]`
  - `Verifier.DerivePathInfo` configured in `ModuleInitializer` to point at `tests/CookBot.Tests/Snapshots/`
  - Single fact: `await Verifier.Verify(promptBuilderService.BuildSystemPrompt(fixtureProfile))`
  - Verify owns the `.received.txt` / `.verified.txt` workflow; intentional changes accepted by moving `.received` → `.verified` (or via `dotnet verify-cli`)
  - The old `expected-system-prompt.txt` fixture is deleted in the same commit
- **D-36:** `PromptDenylistTests.cs` (Phase 1's D-22) **stays** — different purpose (source-scanning for opt-out clause regression) and gets extended with the SCHEMA-10 token list (`image`, `imageUrl`, `picture`, `summary`, `desc`, `temp`, `oven`). The denylist scan is structural (case-insensitive regex against source) and Verify wouldn't add value.

### README format documentation

- **D-37 (CLEAN-04):** README "Recipe Format" section is **inline in `README.md`**, not extracted to a separate `docs/recipe-format.md`. Rationale: PROJECT.md positions README as the single self-hoster touchpoint; v1.3 doesn't have a `docs/` directory established yet (introducing it here just for one format doc is overkill). Section contents: (a) one-paragraph description of `RecipeDocument` as the canonical format; (b) YAML wire example with all v3 fields populated; (c) JSON export example (same recipe); (d) V1→V2→V3 upcaster lineage bullet list with one-line description of what each migration handles; (e) explicit note that the format is internally-managed and the upcaster chain is forward-only. Section sits below the existing "Install" sections (which Phase 9 will write).

### Claude's Discretion

These were not gray areas the user weighed in on; the planner can make the calls during planning.

- File names within `CookBot.Domain/Recipes/` for the new `StepTemperature` record (likely standalone `StepTemperature.cs` in the same folder).
- Whether `Migration_V2_To_V3` and `Migration_V1_To_V2` get a shared base class (probably not — each is small enough that duplication beats coupling).
- Whether `StepTemperature` lives as a nested record inside `ContentStep` or as a sibling record (sibling is cleaner; Phase 1 made `TimerEntry` a sibling).
- Specific xUnit `[Fact]` vs `[Theory]` choices for non-fixture tests.
- The exact wording of the fail-loud null-canonical guard error message (planner picks tone consistent with Phase 1's `DatabaseSeeder` log style).
- Whether to add `[MaxLength(2048)]` / `[MaxLength(4096)]` attributes to `RecipeDocument.PhotoUrl` / `Description` for AI-schema propagation (recommended yes — `JsonSchemaExporter` honors these and surfaces `maxLength` to Anthropic, which improves AI compliance; small cost).
- Whether `JsonRecipeSerializer.SerializeIndented` / `Serialize` need a new overload for v3 (probably not — they take `RecipeDocument` and the type change is internal).
- Whether the `DropTagsJsonColumn` migration also drops the existing `HasDefaultValue("[]")` constraint (it must — EF handles this automatically when the column goes).
- Whether the prompt-snapshot test fixture profile gets one of the new SCHEMA-10 alias tokens injected to verify the denylist actually fires (recommended yes — one negative-path test makes the denylist self-verifying).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents (researcher, planner, executor) MUST read these before planning or implementing.**

### Project & Roadmap
- `.planning/PROJECT.md` — project context, validated capabilities, active scope, key decisions, constraints
- `.planning/REQUIREMENTS.md` §"Schema v3 + Upcaster (`SCHEMA-*`)" and §"Format cleanup (`CLEAN-*`)" — 16 REQ-IDs Phase 8 owns; each row spells out the canonical decision in remarkable detail
- `.planning/ROADMAP.md` §"Phase 8: Format Foundation" — phase goal, success criteria (5), dependency invariants (Phase 8 is the foundation for Phases 9 and 10)
- `.planning/STATE.md` §"Open questions" — TagsJson column drop timing (resolved here by D-26); sentinel-prefix detection (Phase 9 concern); token pricing (Phase 9 concern); pantry-match weights (Phase 10 concern)

### Research
- `.planning/research/SUMMARY.md` — synthesis routing layer; especially §"Phase 8: Format Foundation" (lines 146–166), §"Critical Pitfalls" #4 (Temperature null-fill — solved by D-29's null-coalescing upcaster), §"Reconciled Divergences" + §"Gaps to Address During Planning" (lines 257–263 — TagsJson drop timing called out by name; resolved by D-26)
- `.planning/research/STACK.md` — `Verify.Xunit 31.12.5` (the only new test package for Phase 8); confirms `JsonSchemaExporter` BCL and `JsonSchema.Net` already present from Phase 1
- `.planning/research/ARCHITECTURE.md` §"Migration_V2_To_V3" — trivial stamp upcaster; null-fills all three new fields; mirrors Phase 1's chain pattern
- `.planning/research/PITFALLS.md` C7 (V2→V3 null-fill not zero), C8 (schema assertion test must be FIRST), H11 (parser tests audited before any schema code merges)
- `.planning/research/FEATURES.md` §"Must have (P1)" — Description, PhotoUrl, per-step Temperature, LegacyRecipeProjector deletion, TagsJson → RecipeTag all P1

### Codebase
- `.planning/codebase/ARCHITECTURE.md` §"Recipe Format" — three-format situation resolved by Phase 1; Phase 8 extends the existing pattern
- `.planning/codebase/CONCERNS.md` §1–4 — file format inconsistencies (largely closed by Phase 1; Phase 8 closes the remaining `TagsJson` concern)
- `.planning/codebase/CONVENTIONS.md` — `#nullable enable` everywhere; xUnit Theory + MemberData for fixture-driven tests; singleton lifetimes for pure services

### Phase 1 Reference (load-bearing — Phase 8 extends Phase 1's chain)
- `.planning/phases/01-canonical-format-foundation/01-CONTEXT.md` — D-01 (POCO location), D-04 (version field placement), D-09 (upcaster chain shape), D-14 (LegacyRecipeProjector throwaway pattern — Phase 8 closes), D-15 (IDatabaseBackupService), D-19/D-20 (RecipeSchemaDocumentationProvider), D-21 (hand-rolled snapshot — Phase 8 replaces per D-35), D-22 (denylist test — Phase 8 extends per D-36)
- `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` — chain shape `Migration_V2_To_V3` plugs into
- `src/CookBot.Application/Recipes/Migration_V1_To_V2.cs` — direct precedent for `Migration_V2_To_V3` structure
- `src/CookBot.Domain/Recipes/RecipeDocument.cs` — v2 record that v3 extends
- `src/CookBot.Domain/Recipes/StepNode.cs` — `ContentStep` record that gains `Temperature: StepTemperature?`

### Source files this phase modifies (start here)
- `src/CookBot.Domain/Recipes/RecipeDocument.cs` — add `PhotoUrl: string?`, `Description: string?`; bump `Version` constant to 3
- `src/CookBot.Domain/Recipes/StepNode.cs` (or `ContentStep.cs` if split) — add `Temperature: StepTemperature?` to `ContentStep`
- `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` — bump `CurrentVersion` to 3
- `src/CookBot.Application/Recipes/RecipeJsonSchemaProvider.cs` — regenerates automatically via `JsonSchemaExporter`; verify schema asserts (SCHEMA-11)
- `src/CookBot.Application/Recipes/RecipeValidator.cs` — add per-unit Temperature validation (whole-degree for F/C; 0.5-step for gas; range 1.0–9.5 for gas)
- `src/CookBot.Application/Services/RecipeFormatParser.cs` — YAML/JSON read+write for the three new fields (SCHEMA-08)
- `src/CookBot.Application/Services/JsonRecipeSerializer.cs` — round-trip the three new fields in `Recipe.CanonicalDocumentJson` (SCHEMA-09)
- `src/CookBot.Application/Services/RecipeSchemaDocumentationProvider.cs` (or whichever Phase 1 file) — denylist tokens extended for SCHEMA-10 aliases
- `src/CookBot.Application/Services/PromptBuilderService.cs` — auto-picks up the new schema; no direct edits unless prompt prose mentions the new fields
- `src/CookBot.Application/Services/RecipeService.cs` — remove `IRecipeProjector` dependency (CLEAN-01); add tag relational read/write through the new table (CLEAN-02)
- `src/CookBot.Domain/Entities/Recipe.cs` — add `PhotoUrl: string?` (max 2048), `Description: string?` (max 4096); remove `TagsJson` after callsite switchover
- `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` — `Recipe` mapping for new columns; `RecipeTag` DbSet + configuration
- `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs` — new column lengths; remove `TagsJson` default
- `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` — add startup null-canonical guard (D-32 step a); remove `LegacyRecipeProjector` reference in backfill (which is now a no-op since all rows have CanonicalDocumentJson from Phase 1's milestone backfill)
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — line 420 TagsJson deserialize switches to `recipe.RecipeTags.Select(t => t.Name)` (CLEAN-02)
- `src/CookBot.Web/Services/CookbookTransferService.cs` — line 71 TagsJson deserialize switches to relational projection (CLEAN-02)
- `src/CookBot.Application/Services/RecipeCookingAiContext.cs` — line 19 TagsJson deserialize switches to relational projection (CLEAN-02)
- `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` — REPLACED with Verify-based implementation (D-35)
- `tests/CookBot.Tests/Prompts/PromptDenylistTests.cs` — extend regex to include `image|imageUrl|picture|summary|desc|temp|oven` (D-36 + SCHEMA-10)
- `README.md` — add "Recipe Format" section (CLEAN-04 + D-37)

### Source files this phase creates
- `src/CookBot.Domain/Recipes/StepTemperature.cs` — `record StepTemperature(decimal Value, TemperatureUnit Unit)` + `enum TemperatureUnit { F, C, Gas }`
- `src/CookBot.Application/Recipes/Migration_V2_To_V3.cs` — single class, null-coalescing per field (D-29)
- `src/CookBot.Application/Recipes/Converters/StepTemperatureJsonConverter.cs` — gas half-stop "4½" rendering (only for human-readable JSON if SerializeIndented)
- `src/CookBot.Domain/Entities/RecipeTag.cs` — `(Id, RecipeId, Name)` POCO
- `src/CookBot.Infrastructure/Data/Configurations/RecipeTagConfiguration.cs` — composite index on `(RecipeId, Name)`; FK to `Recipe` with cascade delete
- `src/CookBot.Infrastructure/Migrations/<timestamp>_AddRecipePhotoUrlAndDescription.cs`
- `src/CookBot.Infrastructure/Migrations/<timestamp>_AddRecipeTagTable.cs` (with embedded backfill SQL)
- `src/CookBot.Infrastructure/Migrations/<timestamp>_DropTagsJsonColumn.cs`
- `src/CookBot.Infrastructure/Migrations/<timestamp>_AddPantryMatchIndexes.cs`
- `tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt` — first Verify snapshot
- `tests/CookBot.Tests/ModuleInitializer.cs` — Verifier path configuration (if not already present)
- `tests/CookBot.Tests/Recipes/Migration_V2_To_V3_Tests.cs` — per-field fixture matrix
- `tests/CookBot.Tests/Recipes/SchemaAssertionTests.cs` — SCHEMA-11; FIRST test written, asserts `RecipeJsonSchemaProvider.GetSchema()` includes `photoUrl`, `description`, step-level `temperature`
- `tests/CookBot.Tests/Recipes/StepTemperatureTests.cs` — per-unit validator (F/C whole-degree, gas 0.5-step + 1.0–9.5 range)
- `tests/CookBot.Tests/Recipes/RecipeTagBackfillTests.cs` — trim + preserve case verified; coexistence of "Vegan" + "vegan" verified
- `tests/CookBot.Tests/Fixtures/Recipes/v2-canonical/*` (existing) → add `v3-canonical/*` with photoUrl/description/temperature populated
- `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-*.json` — fixture matrix for D-29's per-field test isolation

### Source files this phase deletes
- `src/CookBot.Application/Recipes/IRecipeProjector.cs` (CLEAN-01)
- `src/CookBot.Infrastructure/Data/Migrations/Helpers/LegacyRecipeProjector.cs` (CLEAN-01)
- `tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt` (D-35 — Verify replaces the hand-rolled fixture)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`RecipeUpcasterChain` + `Migration_V1_To_V2`** (`src/CookBot.Application/Recipes/`) — direct precedent. `Migration_V2_To_V3` follows the same shape: implements `IRecipeUpcaster`, registers in `AddApplication()`, returns mutated `JsonNode`. Null-coalescing per field is the load-bearing pattern (PITFALLS C7).
- **`RecipeJsonSchemaProvider`** (`src/CookBot.Application/Recipes/`) — adds nothing new; the schema regenerates automatically when `RecipeDocument` gains new properties. SCHEMA-07 is "no manual schema editing" by construction; SCHEMA-11 is the assertion that confirms it.
- **`IDatabaseBackupService.BackupBeforeMigrationAsync`** (`src/CookBot.Infrastructure/Data/`) — fires before every EF migration in `DatabaseSeeder.SeedAsync`. Phase 8's four migrations each produce a `.pre-{name}.bak` automatically; no new backup-service code needed.
- **`RecipeFormatParser`** (`src/CookBot.Application/Services/`) — already routes through `RecipeUpcasterChain`. Adding three nullable fields is a pure additive change; existing parsing of v2 docs through the chain handles v3 upgrade transparently.
- **`PromptDenylistTests.cs`** (`tests/CookBot.Tests/Prompts/`) — Phase 1's regex scanner of `PromptBuilderService.cs` source. SCHEMA-10 extension is a single regex-token addition: `\b(fallback|informal|plain numbered|image|imageUrl|picture|summary|desc|temp|oven)\b` (case-insensitive).
- **YamlDotNet 16.3.0** (existing `<PackageReference>`) — handles YAML write for new fields automatically (just maps property names).

### Established Patterns

- **DI registration via per-project extension** — `AddApplication()` (`src/CookBot.Application/DependencyInjection.cs`) registers `Migration_V2_To_V3`; `AddInfrastructure()` registers the `RecipeTagConfiguration` (auto-picked up by EF Core via `OnModelCreating`).
- **Singleton lifetimes for pure services** — `Migration_V2_To_V3`, `StepTemperatureJsonConverter` are singletons.
- **xUnit Theory + MemberData for fixture-driven tests** — used for round-trip tests; Phase 8's upcaster matrix follows the same pattern.
- **`#nullable enable` + implicit usings** — every new file.
- **EF migration naming** — PascalCase descriptive (`AddRecipePhotoUrlAndDescription`, `AddRecipeTagTable`, `DropTagsJsonColumn`, `AddPantryMatchIndexes`); auto-prefixed timestamp.
- **Forward-only migrations** — no `Down()` method body work; downgrade is unsupported per project policy.
- **Sequence-sensitive `DatabaseSeeder` boot order** — backup → migrate → seed → null-canonical guard. Phase 1's pattern preserved; D-32's guard is inserted post-seed.

### Integration Points

- **`Program.cs` composition root** — picks up `Migration_V2_To_V3` through `AddApplication()`; no changes to `Program.cs`.
- **`PromptBuilderService`** — no constructor changes (still consumes `IRecipeSchemaDocumentationProvider`); the new fields surface in the generated prompt automatically via `RecipeJsonSchemaProvider`.
- **`AiChat.razor` and `RecipeCookingAiContext`** — `RecipeCookingAiContext` line 19 (TagsJson read) switches to relational; AI Chat surface change is data-only (the structured response now includes `photoUrl`/`description`/`temperature` on relevant steps but UI consumption rides into Phase 9).
- **`CookbookTransferService`** — v2 cookbook exports/imports continue to round-trip through the upcaster chain. The envelope `SchemaVersion` (Phase 1 D-17 bumped to 2) does NOT bump here — only `RecipeDocument.Version` bumps. Phase 8 verifies a v2 cookbook (envelope `SchemaVersion=2`, recipes with `RecipeDocument.Version=2`) imports into a v1.3 install with recipes upcast to `Version=3` cleanly.
- **`RecipeService.CreateAsync` / `UpdateAsync`** — `IRecipeProjector` constructor dep removed; replaced with direct `RecipeDocument` construction from `ParsedRecipe` (CLEAN-01 step b). The hybrid persistence (relational columns + `CanonicalDocumentJson` snapshot) is preserved.
- **`RecipeEditor.razor`** — Phase 8 changes the tag read path (line 420) only. Photo input, Description field, Temperature picker are explicitly NOT added (UI hint: no).

</code_context>

<specifics>
## Specific Ideas

- **Gas half-stops as a real authoring concern** — user accepted the `decimal` complexity over `int` simplicity specifically to keep gas 4½ representable. This is a domain-driven design choice (UK home-cook convention is load-bearing) rather than a technical preference.
- **TagsJson coexistence (`Vegan` + `vegan` allowed)** — user explicitly accepted "least surprising for existing data" over case-insensitive dedup. Backfill must preserve original casing exactly.
- **Granular migrations over atomic V3 bump** — user picked rollback granularity over backup-file minimalism. The four-migration sequence is intentional: each step can be reverted independently.
- **Verify replaces hand-rolled** — user prefers Verify's diff workflow over the hand-rolled fixture-equality test. The `PromptDenylistTests` defense-in-depth stays.

</specifics>

<deferred>
## Deferred Ideas

Surfaced during analysis but not in scope for this phase:

- **UI surfacing of PhotoUrl** — Phase 9 (PHOTO-09 editor composite + PHOTO-10 RecipeView hero + PHOTO-11 Home tiles + PHOTO-12 AiChat + PHOTO-13 CookbookList).
- **UI surfacing of Description** — Phase 9 (rides into the editor composite alongside photo); editor surfaces as subtitle/lede; RecipeView surfaces under recipe title.
- **UI surfacing of Temperature** — Phase 9 (cooking mode step display) + editor temperature picker on each `ContentStep`.
- **Profile telemetry widget reading AiUsageLog rows** — Phase 10 (QOL/POLISH consumer surfaces).
- **Smart pantry-match using `RecipeTag` JOIN** — Phase 10 (QOL-02 dietary filter).
- **Case-insensitive tag dedup** — v1.4+ if duplicate-casing tags become a real authoring problem.
- **`SchemaVersion` envelope bump** — Phase 8 does NOT bump `CookbookTransferDocument.SchemaVersion`. Per Phase 1's D-17, envelope shape is unchanged; only per-recipe `RecipeDocument.Version` bumps to 3.
- **Drop `Recipe.IngredientRefs` column** — Phase 1's D-13 marked this for "Phase 4 / POLISH-03". POLISH-03 in v1.3 is the Moon glyph, not the column drop. Re-evaluate in v1.4: is `IngredientRefs` still being written? If not (Phase 1 stopped writing), the drop can land in any future schema migration. Not in Phase 8.
- **`DELETE-AFTER-V1.1` test assertion from Phase 1** — Phase 1's D-14 mentioned "an xUnit test that asserts the file still exists during the v1.1 milestone". Phase 8 deletes both the projector files AND that assertion test (if it shipped).

### Reviewed Todos (not folded)

(No pending todos in `.planning/STATE.md` or todo system to evaluate.)

</deferred>

---

*Phase: 08-format-foundation*
*Context gathered: 2026-05-15 (discuss mode — 4 gray areas surfaced, all 4 resolved by user)*
