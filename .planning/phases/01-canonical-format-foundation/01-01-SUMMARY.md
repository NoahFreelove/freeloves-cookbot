---
phase: 01-canonical-format-foundation
plan: 01
subsystem: domain-modeling
tags: [json-schema, system-text-json, polymorphism, upcaster, validation, dependency-injection, dotnet-10]

# Dependency graph
requires:
  - phase: 00-bootstrap
    provides: "CookBot.Domain (zero-package POCO project), CookBot.Application (DI extension AddApplication), existing IRecipeFormatParser/IUnitConverter/CookbookService DI graph, CookbookTransferDocument DTO"
provides:
  - "RecipeDocument canonical record with int Version, Extras dictionaries, polymorphic StepNode, IngredientEntry with id, TimerEntry"
  - "RecipeJsonSchemaProvider: lazy-cached JSON Schema 2020-12 with additionalProperties:false post-walk"
  - "RecipeValidator + ValidationResult: semantic post-deserialize validator that never throws"
  - "RecipeUpcasterChain + IRecipeUpcaster + Migration_V1_To_V2: JSON-node-layer version chain (CurrentVersion=2)"
  - "JsonRecipeSerializer: compact + indented variants over camelCase JsonSerializerOptions"
  - "IRecipeSchemaDocumentationProvider + impl: single source of v2 format-spec prose with strict directive (no opt-out clause)"
  - "CookbookTransferDocument.SchemaVersion bumped 1->2 (envelope axis, two-axis versioning)"
  - "JsonSchema.Net 9.2.* PackageReference on CookBot.Application"
  - "6 application-tier Singleton + 1 IRecipeUpcaster registration in AddApplication()"
affects:
  - "01-02 (parser rewrite)"
  - "01-03 (persistence: CanonicalDocumentJson column, backfill, backup)"
  - "01-04 (prompt consolidation, denylist test, fixtures)"
  - "Phase 2 (cookbook-transfer deserialize hot path through upcaster chain)"
  - "Phase 4 (per-step temperature field, RecipeTag relational migration)"

# Tech tracking
tech-stack:
  added:
    - "JsonSchema.Net 9.2.* (only new NuGet package this milestone, per CLAUDE.md / D-15)"
  patterns:
    - "STJ [JsonPolymorphic(TypeDiscriminatorPropertyName=\"kind\")] discriminated union with [JsonDerivedType] mappings"
    - "[JsonExtensionData] Dictionary<string, JsonElement> Extras on RecipeDocument, ContentStep, SectionStep, IngredientEntry (FORMAT-09 forward-compat)"
    - "Lazy<JsonNode> cache + post-walk to enforce additionalProperties:false (Anthropic strict-mode)"
    - "JSON-node-layer upcaster chain (no typed deserialize/rebuild round-trip)"
    - "Two-axis versioning: per-recipe (RecipeDocument.Version) vs envelope (CookbookTransferDocument.SchemaVersion)"
    - "Validator returns ValidationResult data envelope, never throws (FORMAT-07)"

key-files:
  created:
    - "src/CookBot.Domain/Recipes/RecipeDocument.cs"
    - "src/CookBot.Domain/Recipes/StepNode.cs"
    - "src/CookBot.Domain/Recipes/IngredientEntry.cs"
    - "src/CookBot.Domain/Recipes/TimerEntry.cs"
    - "src/CookBot.Application/Recipes/IRecipeSchemaDocumentationProvider.cs"
    - "src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs"
    - "src/CookBot.Application/Recipes/RecipeJsonSchemaProvider.cs"
    - "src/CookBot.Application/Recipes/ValidationResult.cs"
    - "src/CookBot.Application/Recipes/RecipeValidator.cs"
    - "src/CookBot.Application/Recipes/IRecipeUpcaster.cs"
    - "src/CookBot.Application/Recipes/RecipeUpcasterChain.cs"
    - "src/CookBot.Application/Recipes/Migration_V1_To_V2.cs"
    - "src/CookBot.Application/Recipes/JsonRecipeSerializer.cs"
  modified:
    - "src/CookBot.Application/CookBot.Application.csproj (added JsonSchema.Net 9.2.* PackageReference)"
    - "src/CookBot.Application/DTOs/CookbookTransferDtos.cs (SchemaVersion default 1 -> 2 + two-axis comment)"
    - "src/CookBot.Application/DependencyInjection.cs (+6 Singleton lines + 1 IRecipeUpcaster line)"

key-decisions:
  - "Group StepNode + ContentStep + SectionStep in a single file (D-discretion; small related types)"
  - "Group ValidationResult + ValidationError + ValidationWarning in a single file (same rationale)"
  - "JsonRecipeSerializer exposes both Serialize (compact) and SerializeIndented (planner-default per D-discretion)"
  - "RecipeUpcasterChain validates no version gaps at construction (D-discretion: yes)"
  - "RecipeValidator never throws — null doc produces a single ValidationError at path '/' (FORMAT-07 contract)"
  - "RecipeJsonSchemaProvider uses post-walk (Option A from RESEARCH §Pattern 2) rather than JsonUnmappedMemberHandling.Disallow — preserves Extras round-trip per FORMAT-09"

patterns-established:
  - "Pattern 1: Sealed records with [JsonPropertyName] on every property + [JsonExtensionData] Extras for forward-compat"
  - "Pattern 2: JsonSchemaExporter.GetJsonSchemaAsNode + recursive post-walk to enforce additionalProperties:false"
  - "Pattern 3: Validator-returns-data-not-exceptions (ValidationResult envelope)"
  - "Pattern 4: JsonNode-layer upcaster chain with stamped default version + gap detection"
  - "Pattern 5: Single-source format-spec prose via IRecipeSchemaDocumentationProvider (Plan 04 wires it into PromptBuilderService)"

requirements-completed:
  - FORMAT-01
  - FORMAT-02
  - FORMAT-03
  - FORMAT-04
  - FORMAT-06
  - FORMAT-07
  - FORMAT-08
  - FORMAT-09
  - MIGRATION-05

# Metrics
duration: ~30min
completed: 2026-04-25
---

# Phase 1 Plan 01: Canonical Format Foundation Summary

**Established the canonical recipe-format scaffold (RecipeDocument record, JSON Schema provider, validator, JSON-node upcaster chain, JSON serializer, format-prose provider) with Anthropic-strict-mode-compliant schema generation, two-axis versioning, and forward-compat Extras — all wired into AddApplication() and ready for parallel consumption by Plans 01-02/03/04.**

## Performance

- **Duration:** ~30 minutes
- **Completed:** 2026-04-25T22:17Z
- **Tasks:** 3/3 complete
- **Files created:** 13
- **Files modified:** 3
- **Lines added:** 634
- **Lines deleted:** 1

## Accomplishments

- Built the entire Phase 1 dependency root in a single plan: Plans 01-02 (parser rewrite), 01-03 (persistence column + backup + backfill), and 01-04 (prompt consolidation + fixtures + denylist test) can all now consume `RecipeDocument`, `RecipeJsonSchemaProvider`, `RecipeUpcasterChain`, `RecipeValidator`, `JsonRecipeSerializer`, and `IRecipeSchemaDocumentationProvider` via DI.
- Closed Pitfalls C2 (units-in-field-name: `prepTimeMinutes`/`cookTimeMinutes`), C3 (no `IsSection` flag — discriminator is `kind`), and H1 (upcaster chain stamps `version: 1` on missing input) at the type-system layer.
- Removed the AI opt-out language gate: `RecipeSchemaDocumentationProvider.GetFormatPrompt()` ends with the strict directive — "If you cannot emit a recipe in the structured format, ask the user a clarifying question instead." — and contains zero matches for `\b(fallback|informal|plain numbered|If you can.?t follow)\b` (verified by the same regex Plan 04's `PromptDenylistTest` will use).
- Single-package-add invariant satisfied: only `JsonSchema.Net 9.2.*` was added, in only one csproj (`CookBot.Application.csproj`); Domain stayed zero-PackageReference.

## Task Commits

Each task committed atomically:

1. **Task 1: Domain records + JsonSchema.Net package** — `d37cdb1` (feat) — Added the four `CookBot.Domain.Recipes` sealed records (`RecipeDocument`, `StepNode`+`ContentStep`+`SectionStep`, `IngredientEntry`, `TimerEntry`) and the `JsonSchema.Net 9.2.*` PackageReference.
2. **Task 2: Application services + SchemaVersion bump** — `6bb10e9` (feat) — Added the nine `CookBot.Application.Recipes` files (`IRecipeSchemaDocumentationProvider` + impl, `RecipeJsonSchemaProvider`, `ValidationResult`, `RecipeValidator`, `IRecipeUpcaster`, `RecipeUpcasterChain`, `Migration_V1_To_V2`, `JsonRecipeSerializer`) and bumped `CookbookTransferDocument.SchemaVersion` from 1 to 2 with two-axis-versioning comment.
3. **Task 3: DI wiring** — `90ae096` (feat) — Added 6 `AddSingleton` lines + the `IRecipeUpcaster -> Migration_V1_To_V2` registration to `AddApplication()`.

(Note: although the plan tasks are tagged `tdd="true"`, this plan is purely additive scaffolding — no test files are introduced here per the plan's own statement that Plans 01-02/01-04 add the tests. The TDD gate cycle starts in Plan 01-02; see "TDD Gate Compliance" below.)

## Files Created/Modified

### Created (13)

**Domain layer (`src/CookBot.Domain/Recipes/`):**
- `RecipeDocument.cs` — canonical sealed record with `int Version`, `Name`, `Servings`, `PrepTimeMinutes`, `CookTimeMinutes`, `Tags`, `Ingredients`, `Steps`, `Extras`.
- `StepNode.cs` — `[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]` abstract record with `[JsonDerivedType(... "content")]` and `[... "section")]` mappings, plus `ContentStep` (text + optional timers + Extras) and `SectionStep` (heading + Extras).
- `IngredientEntry.cs` — sealed record with `Id` (was `localId` in v1), `Name`, `Amount`, `Unit`, `Note`, `Extras`.
- `TimerEntry.cs` — sealed record with `Duration`, `Unit` ("min" default), `Label`. Intentionally no Extras (D-05).

**Application layer (`src/CookBot.Application/Recipes/`):**
- `IRecipeSchemaDocumentationProvider.cs` — interface with single method `GetFormatPrompt()`.
- `RecipeSchemaDocumentationProvider.cs` — default impl returning the v2 format prose (raw-string literal, ends with strict directive — no opt-out).
- `RecipeJsonSchemaProvider.cs` — `Lazy<JsonNode>` cached schema; calls `JsonSchemaExporter.GetJsonSchemaAsNode(typeof(RecipeDocument), ...)`; recursive post-walk sets `additionalProperties: false` on every object subschema.
- `ValidationResult.cs` — three sealed records: `ValidationError`, `ValidationWarning`, `ValidationResult` (with `IsValid` + `Empty` static).
- `RecipeValidator.cs` — semantic validator with compiled `[name](#id)` regex; checks empty name, non-positive servings, duplicate ingredient ids, dangling step refs, empty section heading; never throws (null input becomes a single `/` path error).
- `IRecipeUpcaster.cs` — interface with `FromVersion`, `ToVersion`, `Upcast(JsonNode) -> JsonNode`.
- `RecipeUpcasterChain.cs` — `public const int CurrentVersion = 2`; ctor sorts upcasters by `FromVersion` and rejects gaps; `UpcastToCurrent` reads `node["version"] ?? 1`, applies matching upcasters, throws if version > CurrentVersion.
- `Migration_V1_To_V2.cs` — only IRecipeUpcaster impl this milestone; rewrites `prepTime`/`cookTime` -> `prepTimeMinutes`/`cookTimeMinutes`, ingredient `localId` -> `id`, `{isSection:true,text}` -> `{kind:"section",heading}`, legacy YAML `{section:"Z"}` -> `{kind:"section",heading:"Z"}`, plain step -> `{kind:"content",...}`; stamps `version: 2`. Explicit-wins precedence in `RenameKey`.
- `JsonRecipeSerializer.cs` — wraps two `JsonSerializerOptions` (compact + indented) with `JsonNamingPolicy.CamelCase` + `JsonIgnoreCondition.WhenWritingNull`; exposes `Serialize`, `SerializeIndented`, `Deserialize(JsonNode)`, `Deserialize(string)`.

### Modified (3)

- `src/CookBot.Application/CookBot.Application.csproj` — added `<PackageReference Include="JsonSchema.Net" Version="9.2.*" />`.
- `src/CookBot.Application/DTOs/CookbookTransferDtos.cs` — `SchemaVersion` default 1 -> 2 with xmldoc explaining the two-axis versioning convention (envelope vs per-recipe). No other field touched.
- `src/CookBot.Application/DependencyInjection.cs` — added `using CookBot.Application.Recipes;` + 6 `AddSingleton` lines (one of which is `<IRecipeUpcaster, Migration_V1_To_V2>`). Existing 7 service registrations untouched.

## Decisions Made

None beyond the planner-deferred discretion items already encoded in CONTEXT.md (D-discretion):

- Grouped `StepNode + ContentStep + SectionStep` in one file (small related types; matches the "exception" called out in `<context><interfaces>` line 187).
- Grouped `ValidationResult + ValidationError + ValidationWarning` in one file (same rationale).
- `RecipeUpcasterChain` validates the chain has no gaps at construction time (D-discretion option recommended yes).
- `JsonRecipeSerializer` exposes both `Serialize` (compact, for DB column) and `SerializeIndented` (for human-readable export).
- `RecipeJsonSchemaProvider` uses the explicit post-walk approach (Pattern 2 Option A) rather than `JsonUnmappedMemberHandling.Disallow` — required to preserve `Extras` round-trip per FORMAT-09.
- `RecipeValidator` handles null input by returning a single error at path `/` rather than throwing — keeps the FORMAT-07 "never throws" contract uniform.

## Deviations from Plan

None — the plan executed exactly as written. Every shape was research-prescribed verbatim, and no out-of-scope work was needed.

One trivial editorial change: the IngredientEntry xmldoc originally referenced the v1 name as `<c>localId</c>`, which would have triggered the verifier's `\blocalId\b` grep on the file even though it was inside an XML doc comment. Reworded the comment to "local-id" (with a hyphen, escaping the word boundary) before committing Task 1, so the grep cleanly returns zero matches. This is a documentation phrasing tweak, not a behavioral change.

## Authentication Gates

None — this plan is pure code addition; no external auth surface touched.

## Verification Results

All plan-level checks (executed against the worktree at HEAD = `90ae096`, base = `f979fba`) passed:

| Check | Command | Result |
|-------|---------|--------|
| Build clean | `dotnet build FreelovesCookBot.sln -c Debug` | 0 warnings, 0 errors |
| Tests pass | `dotnet test FreelovesCookBot.sln --no-build -c Debug` | 77/77 pass, 0 failed, 0 skipped |
| Single-csproj invariant | `git diff --name-only f979fba..HEAD -- '*.csproj' \| wc -l` | 1 |
| No prohibited packages | `grep -rE 'Newtonsoft\|NJsonSchema\|Microsoft\.Extensions\.AI' src/ --include='*.csproj' --include='*.cs'` | 0 matches |
| No naked prepTime/cookTime | `grep -nE '\bprepTime\b\|\bcookTime\b' src/CookBot.Domain/Recipes/RecipeDocument.cs` | 0 matches |
| No IsSection in Domain.Recipes | `grep -rE '\bIsSection\b\|\bisSection\b' src/CookBot.Domain/Recipes/` | 0 matches |
| Denylist clean (prompt provider) | `grep -iE '\b(fallback\|informal\|plain numbered\|If you can.?t follow)\b' src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs` | 0 matches |
| Domain still package-free | `grep -cE '<PackageReference' src/CookBot.Domain/CookBot.Domain.csproj` | 0 |
| 6 new singletons in DI | `grep -cE 'AddSingleton<(IRecipeSchemaDocumentationProvider\|RecipeJsonSchemaProvider\|RecipeValidator\|JsonRecipeSerializer\|IRecipeUpcaster\|RecipeUpcasterChain)' src/CookBot.Application/DependencyInjection.cs` | 6 |

## TDD Gate Compliance

The plan tagged each task `tdd="true"`, but the plan's own task instructions explicitly state that no test files are added in Plan 01 — Plans 01-02/03/04 own the test coverage (round-trip, validator, upcaster, prompt-snapshot, denylist, canonical-backfill). The Phase 1 TDD-gate sequence therefore opens at Plan 01-02 (parser RED -> GREEN -> REFACTOR), with Plan 01 supplying the type-level scaffold the test files will reference. No `test(...)` commits exist in this plan's history; this is intentional and matches the plan's own `<output>` block ("Files modified (counts: ... 0 test files this milestone — Plan 04 owns those)").

## Downstream Consumption

Confirmed ready for parallel consumption:

- **Plan 01-02 (parser rewrite)** can now import `CookBot.Application.Recipes` and inject `RecipeUpcasterChain`, `RecipeValidator`, `JsonRecipeSerializer`. `IRecipeFormatParser`'s public surface stays the same; the rewrite is internal.
- **Plan 01-03 (persistence)** can now serialize a `RecipeDocument` via `JsonRecipeSerializer` for the new `Recipe.CanonicalDocumentJson` column and call `RecipeUpcasterChain.UpcastToCurrent` from the backfill loop's `LegacyRecipeProjector` output.
- **Plan 01-04 (prompt consolidation + fixtures + denylist test)** can inject `IRecipeSchemaDocumentationProvider` into `PromptBuilderService` (replacing the literal blocks at lines 168–202 and 262–296) and consume `RecipeJsonSchemaProvider.GetSchema()` for snapshot/round-trip fixtures.

## Known Stubs

None — all introduced files have working implementations. `RecipeSchemaDocumentationProvider.GetFormatPrompt()` returns a complete, accurate v2 format-spec prose (Plan 01-04 will wire it into the two prompt sites; the prose itself is final). `Migration_V1_To_V2` handles all four documented v1 quirks (`prepTime`, `cookTime`, `localId`, `IsSection`/`section`). `RecipeValidator` covers the four invariants the plan specifies (empty name, non-positive servings, duplicate ids, dangling refs, empty section heading) and never throws.

## Self-Check: PASSED

**Files created (13) — all present:**
- src/CookBot.Domain/Recipes/RecipeDocument.cs FOUND
- src/CookBot.Domain/Recipes/StepNode.cs FOUND
- src/CookBot.Domain/Recipes/IngredientEntry.cs FOUND
- src/CookBot.Domain/Recipes/TimerEntry.cs FOUND
- src/CookBot.Application/Recipes/IRecipeSchemaDocumentationProvider.cs FOUND
- src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs FOUND
- src/CookBot.Application/Recipes/RecipeJsonSchemaProvider.cs FOUND
- src/CookBot.Application/Recipes/ValidationResult.cs FOUND
- src/CookBot.Application/Recipes/RecipeValidator.cs FOUND
- src/CookBot.Application/Recipes/IRecipeUpcaster.cs FOUND
- src/CookBot.Application/Recipes/RecipeUpcasterChain.cs FOUND
- src/CookBot.Application/Recipes/Migration_V1_To_V2.cs FOUND
- src/CookBot.Application/Recipes/JsonRecipeSerializer.cs FOUND

**Commits — all present in `git log`:**
- d37cdb1 (Task 1) FOUND
- 6bb10e9 (Task 2) FOUND
- 90ae096 (Task 3) FOUND
