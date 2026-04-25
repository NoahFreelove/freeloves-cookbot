# Phase 1: Canonical Format Foundation - Context

**Gathered:** 2026-04-25
**Status:** Ready for planning
**Mode:** auto (recommended defaults selected; user can review and edit before planning)

<domain>
## Phase Boundary

Establish a single versioned `RecipeDocument` C# record as the source of truth for every recipe representation: YAML wire format (legacy paste-in only), JSON cookbook export, the SQLite-backed canonical document column, and the AI prompt's format spec. Build the parser/serializer/validator/upcaster scaffold around it. Migrate every recipe in `cookbot.db` and every legacy `.cookbook.json` v1 file into this format with a safe, idempotent backup-before-migrate flow.

**This phase delivers infrastructure.** The user-visible behavior change is "AI no longer has an opt-out clause and the format spec is no longer duplicated"; the larger payoff lands in Phases 2–4 which depend on this foundation.

**In scope:** FORMAT-01..10, AI-04..06, MIGRATION-01..03, MIGRATION-05, MIGRATION-07, MIGRATION-08, POLISH-02 (20 requirements).

**Not in scope** (deferred to later phases — do not pull forward):
- AI structured output (`output_config.format` wiring) → Phase 2
- Cookbook transfer integration through the upcaster chain (deserialize hot path) → Phase 2 (MIGRATION-04, MIGRATION-06)
- Chip composer / editor UX → Phase 3
- Per-step temperature field (FEATURE-V2) → Phase 4
- `Recipe.TagsJson` → relational `RecipeTag` table (POLISH-04) → Phase 4
- Encrypt-at-rest for `UserProfile.AiApiKey` → FUTURE-01

</domain>

<decisions>
## Implementation Decisions

### Schema & Domain Modeling

- **D-01:** `RecipeDocument` lives in `src/CookBot.Domain/Recipes/` as a new namespace (`CookBot.Domain.Recipes`). Pure POCO records with `[JsonPolymorphic]` attributes — no framework refs, consistent with `CookBot.Domain`'s zero-package-reference posture. **No new project** (rejects an isolated `CookBot.Schemas` project per `SUMMARY.md §6` anti-features).
- **D-02:** Step polymorphism uses C# `abstract record StepNode` with two concrete cases — `ContentStep(string Text, IReadOnlyList<TimerEntry>? Timers)` and `SectionStep(string Heading)`. STJ discriminator is `kind` (`[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]`); discriminator values are `"content"` and `"section"`. The boolean `IsSection` flag from the legacy entity model is **not** carried into the canonical record (closes Pitfall C3).
- **D-03:** Quantity field naming is units-baked: `prepTimeMinutes`, `cookTimeMinutes`, `ovenTempFahrenheit` (latter introduced in Phase 4). No naked `prepTime`/`cookTime` anywhere in the canonical record. The V1→V2 upcaster (D-09) reconciles legacy spellings.
- **D-04:** The `version` field sits at the **top of the root** `RecipeDocument` record, not nested. Type is `int`. JSON Schema constrains writes to `enum: [2]`; reads accept any `int >= 1` and route through the upcaster chain. This satisfies Anthropic Structured Outputs' need for a stable top-level discriminator and lets a v1 install detect "I don't know how to read this" cleanly.
- **D-05:** Forward-compat unknown fields are captured via `[JsonExtensionData] public Dictionary<string, JsonElement> Extras` on `RecipeDocument`, `ContentStep`, `SectionStep`, and `IngredientEntry`. STJ round-trips these automatically (closes Pitfall H4 — Extras propagation through edit/save).
- **D-06:** Ingredient ids in `[name](#id)` step links are the **per-recipe local id** (an `int`) — same semantics as today. Ids are immutable across edits (Pitfall related to ingredient reordering) and never user-visible. The substring-match fallback in `IngredientRefDetectionService` is **deleted** in this phase (FORMAT-05); link resolution is the only highlighting path going forward.

### Parser, Serializer, Schema Provider

- **D-07:** `RecipeJsonSchemaProvider` is a singleton service in `CookBot.Application/Recipes/`. Implementation: calls `JsonSchemaExporter.GetJsonSchemaAsNode(typeof(RecipeDocument), options)` once at first access, walks the resulting `JsonNode` setting `additionalProperties: false` on every object schema (Anthropic strict-mode requirement), caches the result behind a `Lazy<JsonNode>`. Exposes `GetSchema()` returning the cached node. No NuGet schema generator — `System.Text.Json.Schema.JsonSchemaExporter` is BCL on .NET 10.
- **D-08:** `RecipeValidator` (semantic post-parse check) lives in `CookBot.Application/Recipes/`. Returns a `ValidationResult` record with `IReadOnlyList<ValidationError> Errors` and `IReadOnlyList<ValidationWarning> Warnings`. Error/warning shape: `record ValidationError(string Path, string Code, string Message)`. **The validator never throws** (FORMAT-07). Coercion (e.g. `"30"` → `30`, `"vegetarian"` → `["vegetarian"]`) is done at the parser layer with a warning; semantic violations (duplicate ingredient ids, unresolved step links, section step with timers) are errors. (`SUMMARY.md` Q4 — two-tier validation policy.)
- **D-09:** Upcaster chain operates at the JSON-node layer. `IRecipeUpcaster` interface: `int FromVersion`, `int ToVersion`, `JsonNode Upcast(JsonNode input)`. `RecipeUpcasterChain` is a singleton that reads all `IRecipeUpcaster` registrations from DI, sorts by `FromVersion`, and applies them in sequence until the target version is reached. **Migration_V1_To_V2** is the only concrete upcaster this milestone (handles `prepTime`/`prepTimeMinutes`, `cookTime`/`cookTimeMinutes`, `IsSection: bool` + `Text` → `kind: "section", heading`, `localId` → `id`). DI registration: `services.AddSingleton<IRecipeUpcaster, Migration_V1_To_V2>();` — no library, no reflection.
- **D-10:** `IRecipeFormatParser` (existing in `CookBot.Domain/Interfaces/`) is rewritten to delegate to the new schema stack. Its public shape stays the same (`TryParse(content, out recipe, out errors)`) for back-compat with `PasteRawTextDialog.razor` and `RecipeService` callsites; internally it:
   1. detects YAML frontmatter vs JSON
   2. converts YAML → `JsonNode` (via existing YamlDotNet 16.3.0)
   3. stamps `version: 1` if missing
   4. routes through `RecipeUpcasterChain`
   5. deserializes to `RecipeDocument`
   6. runs `RecipeValidator`
   7. returns `ParsedRecipe` (the existing flat DTO) projected from `RecipeDocument`
- **D-11:** YAML stays as a paste-in input format only. `JsonRecipeSerializer` becomes the canonical serializer (used for `Recipe.CanonicalDocumentJson` and the AI prompt's example). `YamlRecipeSerializer` is kept *only* for output to the legacy "Paste Raw Text" preview shape if needed; not the export path. (`SUMMARY.md §1` — JSON-as-canonical decision.)

### Persistence & Migration

- **D-12:** `Recipe` gets a new column `CanonicalDocumentJson: string?` (TEXT, nullable initially). Mapped via standard EF Core string property — **NOT** `OwnsOne`/`OwnsMany`, because the column holds the projected JSON snapshot, not a relational projection. Indexed queries (`CookbookList.razor` filters/sorts) keep using the existing relational columns; `CanonicalDocumentJson` is the export/AI/import authority and is recomputed on every save.
- **D-13:** `Recipe.IngredientRefs: List<int>` (the derived field on `RecipeStep`) **stops being written** during this phase but the column stays for one milestone for safe rollback. `RecipeStepTextFormatter` resolves links from `[name](#id)` markdown directly. The column is dropped in Phase 4 alongside `LegacyRecipeProjector` (POLISH-03 territory).
- **D-14:** `LegacyRecipeProjector` lives in `CookBot.Infrastructure/Data/Migrations/Helpers/` (NEW folder). It reads relational columns and emits a `RecipeDocument` at current `Version`. **One-shot, throwaway** — marked with a `// DELETE-AFTER-V1.1` comment and an xUnit test that asserts the file still exists during the v1.1 milestone. Phase 4 deletes both the helper and the assertion.
- **D-15:** Pre-migration backup mechanism: `IDatabaseBackupService` in `CookBot.Infrastructure/Data/`. Single method: `BackupBeforeMigrationAsync(string migrationName, CancellationToken ct)`. Implementation: locate the SQLite file from the connection string, copy it to `{name}.pre-{migrationName}.bak` next to the original via `File.Copy`. Last-3 retention via `Directory.GetFiles(dir, "{name}.pre-*.bak")` ordered by `LastWriteTimeUtc` descending; delete tail. Configurable via `CookBotSettings:DatabaseBackupRetention` (default 3, min 1, max 10). `DatabaseSeeder.SeedAsync` calls this **before** `MigrateAsync()` whenever the pending migration list is non-empty. Skips silently on a fresh install (no DB file yet).
- **D-16:** Backfill happens inside `DatabaseSeeder.SeedAsync` after `MigrateAsync()` completes. Idempotent SQL: `WHERE CanonicalDocumentJson IS NULL`. Uses `LegacyRecipeProjector` (D-14) per row, serializes via `JsonRecipeSerializer`, batches writes in groups of 50 to bound memory. On a fresh install with zero recipes → no-op (MIGRATION-07).
- **D-17:** `CookbookTransferDocument.SchemaVersion` bumps to **2** in this phase. The two version axes are documented in code comments above each:
   - `CookbookTransferDocument.SchemaVersion` — envelope shape (cookbook metadata + recipes array). Bumps when envelope changes.
   - `RecipeDocument.Version` — per-recipe shape. Bumps when recipe schema evolves.
- **D-18:** EF migration name is `<timestamp>_RecipeCanonicalDocument`. Generated via `dotnet ef migrations add` from inside `src/CookBot.Web` (per existing convention — `Microsoft.EntityFrameworkCore.Design` is referenced there). Forward-only, idempotent.

### Prompt Consolidation & Anti-regression

- **D-19:** `IRecipeSchemaDocumentationProvider` is a new singleton interface in `CookBot.Application/Recipes/`. Single method: `string GetFormatPrompt()`. Implementation: assembles the prose description (capabilities, examples, do's/don'ts) **once** from the canonical `RecipeDocument` record + `RecipeJsonSchemaProvider` schema. Returns a stable string for both AI prompt sites.
- **D-20:** `PromptBuilderService.ResolveRecipeFormat()` and `BuildCopyablePrompt()` both delete their literal-string format specs (lines 168–202 and 262–296) and call `IRecipeSchemaDocumentationProvider.GetFormatPrompt()` instead. The opt-out clause ("If you can't follow this exact format, plain numbered steps are fine — the app will parse them.") is removed from both call sites. Replacement directive (in the new provider's prose): "If you cannot emit a recipe in the structured format, ask the user a clarifying question instead." (FORMAT spec lives in `RecipeSchemaDocumentationProvider`; this is the surrounding instruction language, owned by `PromptBuilderService` template strings.)
- **D-21:** Snapshot test for the assembled system prompt: hand-rolled, no external library. `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` calls `PromptBuilderService.ResolveTemplate` with a fixture profile and asserts the output equals the contents of `tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt`. Fixture file is committed to the repo. When the fixture changes intentionally, the test author re-generates it manually (visible diff in PR). Avoids `Verify` / `ApprovalTests` — the prompt is small, stable, and the diff tooling overhead isn't justified for one milestone.
- **D-22:** Lint denylist enforcement: `tests/CookBot.Tests/Prompts/PromptDenylistTest.cs` reads the source of `PromptBuilderService.cs` (and `RecipeSchemaDocumentationProvider.cs`) at test time and fails if any case-insensitive match for `\b(fallback|informal|plain numbered)\b` is found inside string literals. This catches the most common opt-out-clause regression patterns. No external linter.

### Testing & Fixtures

- **D-23:** Round-trip test fixtures live at `tests/CookBot.Tests/Fixtures/Recipes/` with subdirs `v1-yaml/`, `v1-json-export/`, `v1-db-projections/`, `v2-canonical/`. Each fixture is a real example file (drawn from existing seed cookbook recipes + author-provided examples). Minimum 5 fixtures covering: simple recipe, sectioned recipe, multi-timer recipe, ingredient-heavy recipe (10+ ingredients), edge-case recipe (mixed `text:` and `section:` with quirks).
- **D-24:** Round-trip property: for every fixture, `Parse(Serialize(Upcast(input))) == canonical` with deep-equality on `RecipeDocument`, and `prepTimeMinutes`/`cookTimeMinutes` are non-zero where the source had time values. Implemented as xUnit `[Theory]` + `[MemberData]` driven from `Directory.GetFiles`.
- **D-25:** Smoke test (MIGRATION-08): `tests/CookBot.Tests/Migration/CanonicalBackfillTests.cs` builds an in-memory SQLite DB, seeds it with 3 representative recipes via the existing `RecipeService` (relational shape), runs `LegacyRecipeProjector` against the rows, deserializes the output, and asserts: every recipe round-trips through `Project → Serialize → Parse → ValidateSemantically` with no value drift on any field. Does **not** test the actual EF migration application — that's covered by the existing `DatabaseSeeder` smoke test patterns.

### Claude's Discretion

These were not gray areas the user needed to weigh in on; the planner can make the calls during planning.

- File names within `CookBot.Domain/Recipes/` (e.g. one file per record vs grouped by concept).
- Specific xUnit `[Fact]` vs `[Theory]` choices for non-fixture tests.
- Whether `RecipeUpcasterChain` validates that the registered chain has no version gaps at startup (recommended yes; planner can decide where to put the check).
- Whether `JsonRecipeSerializer` exposes `Serialize(RecipeDocument)` only or also `SerializeIndented(RecipeDocument)` for human-readable export (planner can default to indented for export, compact for DB column).
- Specific log levels for backup/migration events (Information vs Warning).
- Whether the existing `Recipe.TagsJson` parsing call sites (CONCERNS §3) get a temporary helper this phase or wait for the Phase 4 relational migration. Phase 4 owns the full fix; planner may add a thin helper if the schema work creates new call sites.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents (researcher, planner, executor) MUST read these before planning or implementing.**

### Project & Roadmap
- `.planning/PROJECT.md` — project context, validated capabilities, active scope, key decisions, constraints
- `.planning/REQUIREMENTS.md` — 46 REQ-IDs across 6 categories; Phase 1 owns FORMAT-01..10, AI-04..06, MIGRATION-01..03/05/07/08, POLISH-02 (20 reqs)
- `.planning/ROADMAP.md` §"Phase 1: Canonical Format Foundation" — phase goal, success criteria, dependency invariants

### Research
- `.planning/research/SUMMARY.md` — synthesis routing layer; especially §1 (Headline insight: Anthropic Structured Outputs forces JSON canonical), §2 (Stack additions), §3 (Build Order, steps 1–6 for this phase), §7 (Critical pitfalls C1–C7)
- `.planning/research/STACK.md` §"Recommended Stack Additions" — `JsonSchema.Net` 9.2.x is the only new package (lines 80–99); `JsonSchemaExporter` is BCL (lines 63–78); reject Newtonsoft alternatives
- `.planning/research/ARCHITECTURE.md` — deep architectural plan; especially §4 "Migration Chain Pattern" and §6 "Schema Provider"
- `.planning/research/PITFALLS.md` C1–C7 — top critical pitfalls anchored to phase mapping; C1 (IngredientRefs migration silence), C2 (field-rename ambiguity), C3 (`IsSection` re-implementation), C4 (destructive auto-migration without backups) all land in this phase
- `.planning/research/FEATURES.md` §"Goal 2 — Canonical format" — table-stakes features for the format

### Codebase
- `.planning/codebase/ARCHITECTURE.md` §"Recipe Format" — the existing three-format situation (YAML/JSON-export/DB owned-entity)
- `.planning/codebase/CONCERNS.md` §1–4 — file format inconsistencies; this phase resolves them
- `.planning/codebase/CONCERNS.md` §13 — duplicated format spec in `PromptBuilderService.cs:168-202` and `:262-296`
- `.planning/codebase/STRUCTURE.md` — directory layout (where `CookBot.Domain/Recipes/` slots in)
- `.planning/codebase/STACK.md` §"Frameworks" — confirms .NET 10, EF Core 10, MudBlazor 8.15 baseline; no upgrades in this phase
- `.planning/codebase/CONVENTIONS.md` — C# style, nullable refs enabled, async patterns, error handling

### Source files this phase modifies (start here)
- `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs` — public contract preserved; implementation rewritten in Application layer
- `src/CookBot.Application/Services/RecipeFormatParser.cs` — major rewrite; delegates to schema stack
- `src/CookBot.Application/Services/PromptBuilderService.cs` — lines 168–202 and 262–296 deleted; both call sites switch to `IRecipeSchemaDocumentationProvider`
- `src/CookBot.Application/Services/IngredientRefDetectionService.cs` — substring-match fallback removed
- `src/CookBot.Application/Services/RecipeStepTextFormatter.cs` — link resolution becomes the only path
- `src/CookBot.Domain/Entities/RecipeStep.cs` — `IngredientRefs` write path retired (column stays)
- `src/CookBot.Domain/Entities/Recipe.cs` — adds `CanonicalDocumentJson: string?`
- `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` — `Recipe` mapping picks up the new column
- `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` — pre-migration backup + backfill loop
- `src/CookBot.Infrastructure/Migrations/` — new EF migration `<timestamp>_RecipeCanonicalDocument`
- `src/CookBot.Application/DTOs/CookbookTransferDtos.cs` — `SchemaVersion` constant bumps to 2 (deserializer back-compat lands in Phase 2)

### Source files this phase creates
- `src/CookBot.Domain/Recipes/RecipeDocument.cs`
- `src/CookBot.Domain/Recipes/StepNode.cs` (+ ContentStep, SectionStep)
- `src/CookBot.Domain/Recipes/IngredientEntry.cs`
- `src/CookBot.Domain/Recipes/TimerEntry.cs`
- `src/CookBot.Application/Recipes/IRecipeSchemaDocumentationProvider.cs` + impl
- `src/CookBot.Application/Recipes/RecipeJsonSchemaProvider.cs`
- `src/CookBot.Application/Recipes/RecipeValidator.cs` (+ ValidationResult, ValidationError)
- `src/CookBot.Application/Recipes/IRecipeUpcaster.cs` + `RecipeUpcasterChain.cs` + `Migration_V1_To_V2.cs`
- `src/CookBot.Application/Recipes/JsonRecipeSerializer.cs`
- `src/CookBot.Infrastructure/Data/IDatabaseBackupService.cs` + impl
- `src/CookBot.Infrastructure/Data/Migrations/Helpers/LegacyRecipeProjector.cs`
- `tests/CookBot.Tests/Recipes/*` (round-trip, validator, upcaster tests)
- `tests/CookBot.Tests/Migration/CanonicalBackfillTests.cs`
- `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` + `PromptDenylistTest.cs`
- `tests/CookBot.Tests/Fixtures/Recipes/v1-yaml/*`, `v1-json-export/*`, `v1-db-projections/*`, `v2-canonical/*`
- `tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`IRecipeFormatParser`** (`src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs`) — public surface stays; only the implementation moves to delegate to the new schema stack. All callers (`RecipeService.CreateAsync`, `PasteRawTextDialog.razor`, `AiChat.ExtractRecipeContent` until Phase 2 deletes it, `RecipeCookingAiContext`) keep working without changes.
- **YamlDotNet 16.3.0** (existing `<PackageReference>` in `CookBot.Application.csproj`) — kept as the YAML→JsonNode adapter for paste-in. No new YAML library.
- **System.Text.Json** (BCL) — already the project's only JSON layer. `JsonSchemaExporter` ships in .NET 10 BCL, so no NuGet addition for schema generation.
- **`OwnsMany(...).ToJson()` precedent** in `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs:15-19` — confirms STJ-backed JSON columns work in this codebase. The new `CanonicalDocumentJson` is a plain string column (simpler — we own the serialization).
- **`DatabaseSeeder.SeedAsync`** (`src/CookBot.Infrastructure/Data/DatabaseSeeder.cs`) — already runs `MigrateAsync()` at startup. Insert backup + backfill steps without changing the broader seeding logic (admin user, default cookbook, ingredient seed).

### Established Patterns

- **DI registration via per-project extension** — `AddApplication()` (`src/CookBot.Application/DependencyInjection.cs`) and `AddInfrastructure(IConfiguration)` (`src/CookBot.Infrastructure/DependencyInjection.cs`). New singletons (`IRecipeSchemaDocumentationProvider`, `RecipeJsonSchemaProvider`, `RecipeUpcasterChain`, `RecipeValidator`, `IRecipeUpcaster` impls) register in `AddApplication()`. `IDatabaseBackupService` registers in `AddInfrastructure()`.
- **Singleton lifetimes for pure services** — `IRecipeFormatParser` is already singleton; new parsing/validation/schema services follow.
- **xUnit Theory + MemberData for fixture-driven tests** — already used in `tests/CookBot.Tests/`; round-trip and upcaster tests adopt the same pattern.
- **Nullable reference types and implicit usings everywhere** — every new file uses `#nullable enable` (project default) and avoids redundant usings.
- **Repositories bypassed for DbContext when richer queries are needed** — `DatabaseSeeder` uses `CookBotDbContext` directly rather than `IRepository<Recipe>` for the backfill loop; consistent with existing seeder patterns.

### Integration Points

- **`Program.cs` composition root** — picks up new singletons through `AddApplication()` / `AddInfrastructure()`; no changes needed in `Program.cs` itself.
- **`PromptBuilderService` constructor** — gains an `IRecipeSchemaDocumentationProvider` constructor parameter; existing DI registration is auto-fixed.
- **`AiChat.razor` and `RecipeCookingAiContext`** — read the AI system prompt unchanged; the format-spec consolidation is invisible to them. (Phase 2 will refactor `AiChat.ExtractRecipeContent` to use structured-output results.)
- **`CookbookTransferService`** — consumes the new schema during Phase 2 (MIGRATION-04). Phase 1 only bumps the envelope `SchemaVersion` constant; the deserialize hot path stays on the existing v1 deserializer until Phase 2 wires the upcaster chain.
- **`RecipeService.CreateAsync` / `UpdateAsync`** — saves get a new "compute and store `CanonicalDocumentJson`" step (Project current relational state → `RecipeDocument` → serialize → set column). The relational columns continue to be written as before (hybrid persistence).
- **`Migrations/CookBotDbContextModelSnapshot.cs`** — auto-updated by `dotnet ef migrations add`; no hand-edits needed.

</code_context>

<specifics>
## Specific Ideas

(No user-provided "I want it like X" references — auto-mode discussion picked recommended defaults from research and codebase scout.)

If the user reviews this CONTEXT.md and wants to adjust any decision, the most likely revision targets are:

- **D-04** — `version` field placement: top-level integer is the recommended default; alternatives are nested under a `meta` object or as a JSON Schema `$schema` URI.
- **D-15** — Backup retention default: 3 backups is conservative; could be 5 or even "keep all" for solo self-hosters where disk is cheap.
- **D-17** — Bumping `CookbookTransferDocument.SchemaVersion` to 2 in Phase 1: the alternative is bumping it in Phase 2 (when the deserializer hot path actually changes). Bumping early is recommended because the canonical document version is what changes; the envelope shape is unchanged but the version axis lets us track "this export was produced by a v2-aware install."
- **D-21** — Snapshot framework: hand-rolled is the recommended default; if the test author finds the maintenance burden unpleasant, switching to `Verify` is a one-package addition with no other code impact.
- **D-23** — Fixture count: 5 is the floor; the planner is encouraged to add more if Phase 1 surfaces edge cases (especially around `text:` + `section:` ambiguity, Unicode in step text, large ingredient lists).

</specifics>

<deferred>
## Deferred Ideas

Surfaced during synthesis or codebase scout but not in scope for this phase:

- **`Recipe.TagsJson` → relational `RecipeTag` table** — Phase 4 (POLISH-04). The string-based tag column stays untouched here; existing call sites continue to deserialize on read.
- **Dropping `Recipe.IngredientRefs` column** — Phase 4 (POLISH-03 territory). This phase stops *writing* it but the column persists for one milestone for safe rollback.
- **`AiChat.ExtractRecipeContent` deletion** — Phase 2 (POLISH-01). The three-tier extractor stays during Phase 1 because Phase 2 owns the structured-output replacement.
- **CookbookTransferService deserializer routing through upcaster** — Phase 2 (MIGRATION-04). Phase 1 only changes the envelope `SchemaVersion` constant.
- **Old YAML pastes routing through upcaster chain in `IRecipeFormatParser`** — Phase 2 (MIGRATION-06). Phase 1 wires the upcaster chain in DI but the `IRecipeFormatParser` rewrite focuses on the canonical record + parse + validate; the legacy YAML routing path is owned by Phase 2.
- **AI system-prompt redaction chokepoint (`RedactSecrets`)** — Phase 2 (AI-07). Phase 1 does not touch error/log surfaces.
- **`<recipe>...</recipe>` XML wrapping for prompt-injection defense** — Phase 2 (AI-08).
- **One-time per-sharer cookbook-import consent banner** — Phase 2 (AI-09).
- **Encrypt-at-rest for `UserProfile.AiApiKey`** — FUTURE-01 (separate security milestone).
- **MudBlazor 9.x upgrade** — out of scope (anti-feature in `SUMMARY.md §6`); FUTURE-10.
- **Cooklang as canonical format** — out of scope (anti-feature); FUTURE-11 considers Cooklang as a one-way export target only.

### Reviewed Todos (not folded)

(No pending todos in `.planning/STATE.md` or todo system to evaluate — fresh project init.)

</deferred>

---

*Phase: 01-canonical-format-foundation*
*Context gathered: 2026-04-25 (auto mode — recommended defaults selected)*
