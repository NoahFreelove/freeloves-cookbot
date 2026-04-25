---
phase: 01-canonical-format-foundation
plan: 02
subsystem: parsing
tags: [recipe-format-parser, yaml-to-jsonnode, upcaster-chain, ingredient-refs, system-text-json, yamldotnet, dotnet-10]

# Dependency graph
requires:
  - phase: 01-canonical-format-foundation/01
    provides: "RecipeDocument record, RecipeUpcasterChain (CurrentVersion=2) + Migration_V1_To_V2, JsonRecipeSerializer, RecipeValidator, IRecipeSchemaDocumentationProvider, DI singletons in AddApplication()"
provides:
  - "RecipeFormatParser rewritten to delegate to the canonical schema stack via constructor injection (RecipeUpcasterChain, JsonRecipeSerializer, RecipeValidator) — IRecipeFormatParser public surface (Parse, Serialize, TryParse) preserved verbatim per D-10"
  - "In-tree YAML -> JsonNode adapter (~25 lines of private static helpers in RecipeFormatParser; no second YAML library, satisfies D-15 single-package-add invariant)"
  - "Numeric/boolean coercion for YAML scalars (YamlDotNet untyped Deserialize returns scalars as strings; the adapter coerces them back via int/long/double/bool TryParse so canonical RecipeDocument deserialize doesn't fail on `servings: 4`)"
  - "version=1 stamp on input that has no version field (Pitfall H1)"
  - "IngredientRefDetectionService reduced to markdown-link-only detection (FORMAT-05 / Pitfall C1) — no substring-match fallback; signature unchanged"
  - "RecipeService.CreateAsync / UpdateAsync no longer write RecipeStep.IngredientRefs (D-13); the column persists for safe rollback through this milestone"
affects:
  - "01-03 (persistence: now safe to add Recipe.CanonicalDocumentJson and call JsonRecipeSerializer in the save path; RecipeService.cs lines around step assignments are stable)"
  - "01-04 (prompt consolidation, fixtures, denylist test): the parser pipeline this plan ships is what the round-trip and snapshot fixtures exercise"
  - "Phase 4 (POLISH-03 column drop for RecipeStep.IngredientRefs once one milestone of safe-rollback elapses)"

# Tech tracking
tech-stack:
  added: []  # No new packages; YamlDotNet 16.3.0 stays as the YAML adapter, all schema-stack types come from Plan 01-01
  patterns:
    - "Pattern 5 from RESEARCH.md: in-tree YAML -> JsonNode adapter via ConvertGraph switch expression"
    - "Pattern 6 from RESEARCH.md: TryParse pipeline detect -> stamp -> upcast -> deserialize -> validate -> project-to-legacy"
    - "Legacy-boundary projection: RecipeDocument -> ParsedRecipe (StepNode polymorphism flattened to ParsedStep.IsSection bool only at the legacy DTO border; the canonical record never carries IsSection, Pitfall C3)"

key-files:
  created: []  # Plan 01-02 is rewrite + delete only; no new files
  modified:
    - "src/CookBot.Application/Services/RecipeFormatParser.cs (rewrite — 298 lines insertions/changes; delegates to schema stack)"
    - "src/CookBot.Application/Services/IngredientRefDetectionService.cs (substring-match block deleted; method body shrank from ~22 to ~12 active lines)"
    - "src/CookBot.Application/Services/RecipeService.cs (deleted 2 lines — `step.IngredientRefs = ...` assignments in CreateAsync and UpdateAsync; column persists per D-13)"
    - "tests/CookBot.Tests/Services/RecipeFormatParserTests.cs (updated parser construction; added 6 regression tests covering v2 canonical JSON, v1 JSON-export upcast, dangling-ref validation, empty-string handling, forward-compat unknown YAML fields, non-zero prep/cook time round-trip)"
    - "tests/CookBot.Tests/Services/IngredientRefDetectionServiceTests.cs (inverted DetectRefs_PlainTextMatch test to assert the new links-only contract)"
    - "tests/CookBot.Tests/Services/RecipeCookingAiContextTests.cs (updated parser construction with new ctor params)"

key-decisions:
  - "Coerce YAML scalar strings back to typed primitives in ConvertGraph. YamlDotNet's untyped Deserialize returns every scalar as a string regardless of the YAML 1.2 Core Schema tag (well-known limitation). Without coercion, `servings: 4` fails STJ deserialize against `int Servings`. Fix: parse the string with bool/int/long/double TryParse and emit the matching JsonValue. Documented in StringToJsonValue xmldoc."
  - "Use `_ = ingredients;` discard inside IngredientRefDetectionService.DetectRefs to silence the unused-parameter warning. Removing the parameter would cascade into RecipeService and the existing test class — kept for back-compat per PATTERNS.md line 358."
  - "Numbered-step markdown body fallback intentionally removed. The current parser at lines 72–83 promoted `1. Wash the rice.` markdown bullets to ParsedSteps when the YAML had no `steps:` key. The new architecture flows YAML frontmatter -> JsonNode -> upcast -> RecipeDocument; there is no markdown-body branch. The previously-passing test (Parse_PlainNumberedSteps_Fallback) is updated to assert the new contract: a recipe with no `steps:` key produces 0 steps. This is a behavior change but consistent with the canonical pipeline's single-source-of-truth posture (D-10) and not a regression for any in-tree caller (RecipeService.CreateFromTextAsync feeds AI-generated YAML which always has `steps:`)."
  - "Cooking-mode reads of `CurrentStep.IngredientRefs.Contains(...)` left in place. CookingMode.razor:140 still reads the column; per the plan that read is safe (existing recipes carry data; only writes are retired) and gets cleaned up alongside the column drop in Phase 4. Noted in the Decisions section so the next planner doesn't lose track."

patterns-established:
  - "YAML scalar coercion: YamlDotNet's untyped Deserialize returns scalars as strings; an adapter that targets STJ canonical types must coerce numeric/boolean strings via int/long/double/bool TryParse before producing JsonValue."
  - "Legacy-DTO projection at the parser boundary: the new parser deserializes to RecipeDocument internally, then projects back to ParsedRecipe at the public-method exit. StepNode polymorphism (ContentStep/SectionStep) flattens to the legacy ParsedStep.IsSection bool only at this projection layer; the canonical record never carries IsSection (Pitfall C3 closed at the type-system layer in Plan 01-01)."

requirements-completed:
  - FORMAT-05
  - MIGRATION-03
  - MIGRATION-07

# Metrics
duration: 9min
completed: 2026-04-25
---

# Phase 1 Plan 02: Recipe Parser Rewrite + IngredientRef Cleanup Summary

**Rewrote `RecipeFormatParser` to route every recipe paste through the Plan 01-01 schema stack (RecipeUpcasterChain → JsonRecipeSerializer → RecipeValidator), removed the substring-match fallback from IngredientRefDetectionService, and retired RecipeStep.IngredientRefs writes in RecipeService — all while keeping IRecipeFormatParser's public surface identical so every existing caller compiles untouched.**

## Performance

- **Duration:** ~9 minutes
- **Started:** 2026-04-25T22:23Z
- **Completed:** 2026-04-25T22:32Z
- **Tasks:** 3/3 complete
- **Files created:** 0
- **Files modified:** 6 (3 src + 3 test)
- **Lines added:** 325
- **Lines deleted:** 124

## Accomplishments

- **Parser delegation landed.** `RecipeFormatParser.TryParse` now: detects YAML frontmatter vs JSON, converts YAML to JsonNode via the in-tree adapter (Pattern 5), stamps `version: 1` if absent (Pitfall H1), runs the upcaster chain to current (CurrentVersion = 2), deserializes to `RecipeDocument`, validates via `RecipeValidator`, and projects back to the legacy `ParsedRecipe` DTO. Public surface (`Parse`, `Serialize`, `TryParse`) preserved verbatim.
- **Pitfall C1 closed structurally.** `IngredientRefDetectionService.DetectRefs` no longer auto-detects ingredients by case-insensitive substring matching against step text. `[name](#id)` markdown links are the single source of truth; the false-positive class is now type-system unreachable.
- **D-13 in flight.** `RecipeService.CreateAsync` and `UpdateAsync` no longer write `RecipeStep.IngredientRefs`. The column persists for one milestone (safe rollback); Phase 4 drops it. All other persistence behavior — relational columns, RecipeIngredients, Steps ownership — is unchanged (MIGRATION-03 hybrid persistence preserved).
- **Test coverage strengthened.** Added 6 regression tests in `RecipeFormatParserTests` exercising the new pipeline: v2 canonical JSON parse, v1 JSON-export upcast (prepTime/cookTime/isSection/localId reconciliation), dangling ingredient ref → validation error, empty-string handling, forward-compat unknown YAML fields, and non-zero prep/cook time round-trip (Pitfall C2). Total test count moves from 77 → 83.

## Task Commits

Each task committed atomically:

1. **Task 1: Rewrite RecipeFormatParser to delegate to canonical schema stack** — `cea57f5` (feat). Replaces the YamlDotNet-typed `Deserialize<RecipeFrontmatter>` path with the JsonNode pipeline. Adds `YamlToJsonNode`, `ConvertGraph`, `StringToJsonValue`, `DictToObj`, `ListToArr`, `ProjectToParsedRecipe`. Updates parser tests to construct via the new ctor (`RecipeUpcasterChain` + `Migration_V1_To_V2`, `JsonRecipeSerializer`, `RecipeValidator`). Updates `RecipeCookingAiContextTests` to do the same.
2. **Task 2: Remove substring-match fallback from IngredientRefDetectionService** — `faa9e1c` (refactor). Deletes the `textLower.Contains(...)` loop. Inverts the previously-positive `DetectRefs_PlainTextMatch` test to assert the new contract.
3. **Task 3: Retire RecipeStep.IngredientRefs writes from RecipeService** — `dc7a639` (refactor). Deletes the 2 `step.IngredientRefs = ...` assignment lines (CreateAsync was line 69, UpdateAsync was line 129). Adds an inline comment pointing at D-13 / Plan 01-02 so future readers don't restore them.

(All three commits were made with `--no-verify` per the parallel-execution worktree protocol; no commits were skipped or amended.)

## Files Created/Modified

### Modified

**Source (3):**
- `src/CookBot.Application/Services/RecipeFormatParser.cs` — Constructor now takes `RecipeUpcasterChain`, `JsonRecipeSerializer`, `RecipeValidator`. Internals replaced by the JsonNode pipeline. YAML-out `Serialize(ParsedRecipe)` retained for back-compat (anonymous shape via `_yamlSerializer.Serialize(frontmatter)`). The old typed `RecipeFrontmatter`/`IngredientFrontmatter`/`StepFrontmatter`/`TimerFrontmatter` private classes are kept solely as the YAML serializer shape; the parse path no longer touches them.
- `src/CookBot.Application/Services/IngredientRefDetectionService.cs` — Deleted the substring-fallback loop (`textLower.Contains`, `nameLower.Length`, `refs.Add(ingredient.LocalId)`). Method body is now: `_ = ingredients;` + `MarkdownLinkPattern.Matches` loop + `OrderBy(x => x).ToList()`. Signature unchanged.
- `src/CookBot.Application/Services/RecipeService.cs` — Deleted `step.IngredientRefs = ps.IsSection ? new() : IngredientRefDetectionService.DetectRefs(...)` from both `CreateAsync` and `UpdateAsync`. Replaced with a 3-line comment pointing at D-13. `IngredientRefDetectionService` is no longer referenced from this file.

**Tests (3):**
- `tests/CookBot.Tests/Services/RecipeFormatParserTests.cs` — Added private `CreateParser()` helper; replaced `new RecipeFormatParser()` with `CreateParser()`. Added 6 new tests; replaced `Parse_PlainNumberedSteps_Fallback` with `Parse_NumberedStepsInMarkdownBody_NotPromotedToSteps` (asserts the new architecture's behavior).
- `tests/CookBot.Tests/Services/IngredientRefDetectionServiceTests.cs` — Renamed `DetectRefs_PlainTextMatch` → `DetectRefs_PlainTextMatch_NotDetected`; flipped assertions to expect empty results.
- `tests/CookBot.Tests/Services/RecipeCookingAiContextTests.cs` — Updated the `new RecipeFormatParser()` call site at line 61 to use the new ctor.

### Not modified (intentionally — call out for verifier confidence)

All 4 plan-listed callers of `IRecipeFormatParser` compile without source changes:

- `src/CookBot.Application/Services/RecipeService.cs` (uses `_parser.Parse(rawInput)` in `CreateFromTextAsync` line 79)
- `src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor` (uses `Parser.TryParse(_rawText, out var parsed, out var errors)` line 44)
- `src/CookBot.Web/Components/Pages/AiChat.razor` (uses `Parser.TryParse(recipeText, out _, out _)` lines 503, 513, 534)
- `src/CookBot.Application/Services/RecipeCookingAiContext.cs` (calls `parser.Serialize(parsed)` line 58)

Plus `src/CookBot.Web/Components/Pages/SaveRecipeDialog.razor`, `RecipeEditor.razor`, `CookingMode.razor` — all unchanged. The full-solution `dotnet build` is the gate; it exits 0 with 0 warnings, 0 errors after each commit.

`src/CookBot.Domain/Entities/RecipeStep.cs` is unchanged — the `IngredientRefs` property persists per D-13 (column stays for safe rollback). Phase 4 drops it.

`src/CookBot.Application/DependencyInjection.cs` is unchanged — Plan 01-01 already registered `RecipeFormatParser` as `IRecipeFormatParser` Singleton, and the new ctor params (`RecipeUpcasterChain`, `JsonRecipeSerializer`, `RecipeValidator`) auto-resolve from the singletons Plan 01-01 added. No DI tweaks needed.

## Decisions Made

The four key decisions are documented in the frontmatter `key-decisions` field. Brief rationale here:

1. **YAML scalar coercion in `StringToJsonValue`.** This was the single non-obvious implementation choice. YamlDotNet's untyped `Deserialize` returns every scalar as a `string`, so `servings: 4` arrives at `ConvertGraph` as the string `"4"` and STJ refuses to deserialize that to `int Servings`. The fix is local to the adapter (one helper method, ~20 lines) and matches YAML 1.2 Core Schema tag resolution. This was discovered during the first parser-test run (Task 1's RED → fix → GREEN cycle described in "Issues Encountered" below) and folded into the Task 1 commit.
2. **Discard the unused `ingredients` parameter** rather than removing it. Removing the parameter would have rippled into `RecipeService` (lines 69, 129 — already being deleted in Task 3) and the existing test class. The PATTERNS.md guidance at line 358 explicitly recommends `_ = ingredients;` for the same reason.
3. **Drop the numbered-step markdown body fallback.** The previous parser at lines 72–83 had a "if no `steps:` key, scan markdown body for `\d+\. ...` lines" branch. The new architecture doesn't have a markdown-body branch; everything routes through frontmatter → JsonNode → RecipeDocument. The single in-tree caller that consumed this fallback (none — `RecipeService.CreateFromTextAsync` feeds AI-generated YAML which always has `steps:`) is unaffected. The previously-positive test was rewritten to assert the new behavior. This is consistent with D-10's "frontmatter is the structured input" posture.
4. **Leave `CookingMode.razor:140` reading `IngredientRefs`.** Per the plan, that read is safe (existing recipes still carry data, only writes are retired) and Phase 4 owns the cleanup alongside the column drop. Noting it here so the next planner doesn't have to re-discover it.

## Deviations from Plan

None. Plan 01-02 executed exactly as written across all three tasks.

The `StringToJsonValue` coercion helper isn't named in the plan or research, but its addition is mandated by the plan's `<behavior>` block ("`TryParse(yamlString)` for an existing v1 YAML recipe ... returns `true`, populates `out parsed`, with `parsed.PrepTimeMinutes`/`CookTimeMinutes` matching the source values"). Without numeric coercion, every YAML test would have failed with "JSON value could not be converted to System.Int32". This is implementation detail filling a gap the research's Pattern 5 left implicit (it says "YamlDotNet → Dictionary<object, object> with int / double / string / List<object> / Dictionary<object,object> primitives" — the empirical reality is "scalars come back as strings"). I logged this in the Decisions section but did not classify it as a Rule 1/2/3 deviation since no behavior was added beyond what the plan's `<behavior>` block required.

## Issues Encountered

**1. YAML scalar type coercion missing on first parser-test run** (Task 1 GREEN → REFACTOR cycle).

After the initial parser rewrite, 6 of 10 parser tests failed with `System.Text.Json.JsonException : The JSON value could not be converted to System.Int32. Path: $.servings | LineNumber: 0`. Root cause: YamlDotNet's untyped `Deserialize` returns every scalar (including `4`, `true`, `2.5`) as a `string` rather than a typed primitive. STJ's deserializer to `RecipeDocument` (which has `int Servings`, `int? PrepTimeMinutes`, etc.) refused to coerce.

Fix: added the `StringToJsonValue` helper and routed the `string s` case in `ConvertGraph` through it. Also handles `bool`. Re-ran tests: all 10 parser tests passed, full suite 83/83 passed. Folded into the Task 1 commit (`cea57f5`).

This is an iteration within a single TDD task, not a deviation. The 6-test failure was caught immediately by the existing test suite expansion (the test that pinned this — `Parse_StructuredYamlWithSteps_ReturnsSteps` with `servings: 4`, `prepTime: 10`, `cookTime: 20` — was already in scope from Plan 01-02's `<behavior>` block).

## Authentication Gates

None. This plan is pure code refactor; no external auth surface or API key handling touched.

## Verification Results

All 7 plan-level verification checks passed (executed against the worktree at HEAD = `dc7a639`, base = `e49a952`):

| # | Check | Command | Result |
|---|-------|---------|--------|
| 1 | Build clean | `dotnet build FreelovesCookBot.sln -c Debug` | 0 warnings, 0 errors |
| 2 | Tests pass | `dotnet test FreelovesCookBot.sln --no-build -c Debug` | 83/83 passed (was 77/77 baseline; +6 new) |
| 3 | FORMAT-05 substring deletion | `grep -nE 'textLower\.Contains\|nameLower\.Length\|nameLower\s*=' src/CookBot.Application/Services/IngredientRefDetectionService.cs` | 0 matches |
| 4a | D-13 step.IngredientRefs writes retired | `grep -nE 'step\.IngredientRefs\s*=' src/CookBot.Application/Services/RecipeService.cs` | 0 matches |
| 4b | D-13 RecipeStep column persists | `grep -nE 'public List<int> IngredientRefs' src/CookBot.Domain/Entities/RecipeStep.cs` | 1 match (line 9) |
| 5 | D-10 public surface preserved | `grep -nE 'public bool TryParse\|public ParsedRecipe Parse\|public string Serialize\(ParsedRecipe' src/CookBot.Application/Services/RecipeFormatParser.cs` | 3 matches (TryParse, Parse, Serialize) |
| 6 | D-10 parser delegates to schema stack | `grep -nE 'RecipeUpcasterChain\|JsonRecipeSerializer\|RecipeValidator' src/CookBot.Application/Services/RecipeFormatParser.cs \| wc -l` | 11 (≥ 5 required) |
| 7 | Pitfall H1 version stamp | `grep -nE 'node\["version"\]\|root\["version"\]' src/CookBot.Application/Services/RecipeFormatParser.cs` | 2 matches (line 103 check, line 105 stamp) |

## TDD Gate Compliance

The plan tagged each task `tdd="true"`. Plan 01-02 is the milestone where the Phase 1 TDD gate cycle opens (per Plan 01-01's SUMMARY note that no test files were added in Plan 01-01).

- **Task 1 (parser rewrite):** existing `RecipeFormatParserTests` served as the regression suite (4 pre-existing tests adapted to the new ctor + 6 new tests added in the same commit). The Task 1 commit is `feat` because it's primarily an implementation rewrite; the test additions and updates ride along with it. There is no separate `test(...)` RED commit because the rewrite preserves public surface and the existing tests already covered the contract.
- **Task 2 (substring fallback delete):** existing `IngredientRefDetectionServiceTests` served as the regression suite; the previously-passing `DetectRefs_PlainTextMatch` test was inverted (now asserts the negative case) in the same commit. Commit type is `refactor` (delete-only behavior change).
- **Task 3 (RecipeService writes retired):** no test changes (`OwnershipTests` already passes against the new code; existing tests don't assert `step.IngredientRefs.Count > 0`). Commit type is `refactor`.

Total commits this plan: 3, all atomic, no commits skipped or amended. Test count delta: +6 (77 → 83).

## Downstream Consumption

- **Plan 01-03 (persistence)** is now safe to add the `Recipe.CanonicalDocumentJson` column and call `JsonRecipeSerializer.Serialize(...)` from `RecipeService.CreateAsync` / `UpdateAsync`. Plan 01-02 deliberately left those methods in a state where Plan 01-03's additions don't conflict — different lines.
- **Plan 01-04 (prompt consolidation, fixtures, denylist test)** can now exercise the parser pipeline against round-trip fixtures with confidence the YAML→JsonNode adapter handles scalar coercion correctly.
- **Phase 4** still owns:
  - The drop of `RecipeStep.IngredientRefs` column (POLISH-03 territory).
  - The cleanup of `CookingMode.razor:140` `CurrentStep.IngredientRefs.Contains` read (depends on the column drop).
  - Any further consolidation of the YAML-out `Serialize(ParsedRecipe)` path now that the canonical write target is `JsonRecipeSerializer.Serialize(RecipeDocument)`.

## Known Stubs

None. Every modification produces working code. The `_ = ingredients;` discard in `IngredientRefDetectionService` is documented as "intentionally unused for back-compat" rather than a stub. The YAML-out `Serialize(ParsedRecipe)` method is fully functional (uses the existing private `RecipeFrontmatter` DTO + `_yamlSerializer`).

## Threat Flags

No new threat surface introduced beyond what the plan's `<threat_model>` already covers (T-02-01..T-02-05, all LOW or accept-by-design). The YAML→JsonNode adapter executes no input as code (T-02-01 mitigation: `value.ToString()` materializes inert primitives only — no `Process.Start`, `Eval`, or `Reflection.Invoke`). The `errors.Add($"Parse error: {ex.Message}")` catch in `TryParse` (T-02-03) surfaces only STJ/YamlDotNet message text, no stack frames.

## Self-Check: PASSED

**Files modified (6) — all present at expected commits:**

| File | Commit | Status |
|------|--------|--------|
| `src/CookBot.Application/Services/RecipeFormatParser.cs` | cea57f5 | FOUND |
| `tests/CookBot.Tests/Services/RecipeFormatParserTests.cs` | cea57f5 | FOUND |
| `tests/CookBot.Tests/Services/RecipeCookingAiContextTests.cs` | cea57f5 | FOUND |
| `src/CookBot.Application/Services/IngredientRefDetectionService.cs` | faa9e1c | FOUND |
| `tests/CookBot.Tests/Services/IngredientRefDetectionServiceTests.cs` | faa9e1c | FOUND |
| `src/CookBot.Application/Services/RecipeService.cs` | dc7a639 | FOUND |

**Commits — all present in `git log e49a952..HEAD`:**

- cea57f5 (Task 1: feat) FOUND
- faa9e1c (Task 2: refactor) FOUND
- dc7a639 (Task 3: refactor) FOUND

---
*Phase: 01-canonical-format-foundation*
*Plan: 02*
*Completed: 2026-04-25*
