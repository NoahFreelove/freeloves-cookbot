---
phase: 15-nutrition-offline-cnf-canadian-nutrient-file
plan: "06"
subsystem: application/json-ld
tags: [json-ld, schema-org, nutrition, tdd]
dependency_graph:
  requires: ["15-02"]
  provides: ["15-07"]
  affects: ["src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs"]
tech_stack:
  added: []
  patterns: ["optional-param-default-null", "conditional-dict-entry", "verify-golden-snapshot"]
key_files:
  created:
    - tests/CookBot.Tests/Nutrition/JsonLdNutritionProjectorTests.cs
    - tests/CookBot.Tests/Snapshots/JsonLdNutritionProjectorTests.FullDocumentWithNutrition_ProducesExpectedJsonLd.verified.txt
  modified:
    - src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs
decisions:
  - "Optional third param with default null keeps all existing 2-arg call sites compiling with no changes"
  - "Copied received.txt bytes directly to verified.txt to ensure exact byte match (degree sign is HTML-escaped as \\u00B0 by the LdOptions encoder)"
metrics:
  duration: "~10 minutes"
  completed: "2026-06-08T03:18:00Z"
  tasks_completed: 2
  files_changed: 3
---

# Phase 15 Plan 06: JsonLd Nutrition Projection Summary

Schema.org NutritionInformation added to JSON-LD output as an optional, per-serving block emitted only when a `NutritionInfoDto` is supplied; nutrition-absent path is byte-identical to the Phase 13 baseline (regression-locked).

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | Add optional NutritionInfoDto? param to JsonLdRecipeProjector.Project | 873ef63 | JsonLdRecipeProjector.cs |
| 2 | Present/absent nutrition projector tests + golden regression (RED) | f99f7f4 | JsonLdNutritionProjectorTests.cs |
| 2 | Present/absent nutrition projector tests + golden regression (GREEN) | 5eba871 | *.verified.txt |

## What Was Built

**`JsonLdRecipeProjector.Project`** (signature change):
```csharp
public static string Project(RecipeDocument doc, string? absoluteImageUrl, NutritionInfoDto? nutrition = null)
```
When `nutrition` is non-null, adds:
```json
"nutrition": {
  "@type": "NutritionInformation",
  "calories": "455 calories",
  "proteinContent": "12.9 g",
  "carbohydrateContent": "95.4 g",
  "fatContent": "1.2 g"
}
```
Formatting: calories at 0 decimal places (`{N:0} calories`); macros at 1 decimal place (`{N:0.#} g`). The projector stays a pure static function — no DI, no data-service access; HTML-safe LdOptions encoder unchanged.

**Test file** `tests/CookBot.Tests/Nutrition/JsonLdNutritionProjectorTests.cs` — 5 tests:
- `Nutrition_OmittedWhenNull` — SC5: no `nutrition` key when DTO is null
- `WithNutrition_IncludesNutritionInformation` — NUTR-06: correct Schema.org keys + formatted values
- `NutritionCalories_RoundsToWholeNumber` — 0 dp calories, 1 dp macros
- `Baseline_NutritionAbsentGoldenUnchanged` — Phase 13 regression guard (behavioral assertion)
- `FullDocumentWithNutrition_ProducesExpectedJsonLd` — golden snapshot (new `.verified.txt`)

## TDD Gate Compliance

| Gate | Commit | Status |
|------|--------|--------|
| RED — failing test committed before .verified.txt | f99f7f4 | PASS |
| GREEN — implementation already in place (Task 1); .verified.txt committed | 5eba871 | PASS |
| REFACTOR | n/a — no cleanup needed | PASS |

## Deviations from Plan

**None** — plan executed exactly as written.

One minor implementation note: when copying the received output to `verified.txt`, the `°` character appears as `°` in the file (HTML-safe encoding from `LdOptions`). The bytes were copied exactly from the test runner's `received.txt` to ensure the golden assertion passes.

## Security Review (Threat Model)

| Threat ID | Disposition | Verified |
|-----------|-------------|---------|
| T-15-14 (XSS / JSON-LD injection) | mitigate | LdOptions HTML-safe encoder unchanged; nutrition values are numeric-formatted strings with no free text; `ScriptBreakout_IsEscaped` test still passes |
| T-15-15 (projector self-fetching) | mitigate | Projector remains pure static; no DI, no data-service, no CanonicalDocumentJson access — enforced by doc-comment and confirmed by code review |

## Test Results

- All 19 JsonLd tests pass (14 existing + 5 new)
- Phase 13 golden (`JsonLdRecipeProjectorTests.FullDocument_ProducesExpectedJsonLd.verified.txt`) byte-identical — confirmed via `git diff` (no diff)
- Full non-AI suite: 536 passed, 0 failed
- 6 pre-existing AI fixture test failures (require live API) are unrelated to this plan

## Known Stubs

None.

## Self-Check: PASSED

- [x] `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs` — modified, exists
- [x] `tests/CookBot.Tests/Nutrition/JsonLdNutritionProjectorTests.cs` — created, exists
- [x] `tests/CookBot.Tests/Snapshots/JsonLdNutritionProjectorTests.FullDocumentWithNutrition_ProducesExpectedJsonLd.verified.txt` — created, exists
- [x] Commit 873ef63 exists (Task 1 projector change)
- [x] Commit f99f7f4 exists (Task 2 RED — test file)
- [x] Commit 5eba871 exists (Task 2 GREEN — golden snapshot)
- [x] Phase 13 `JsonLdRecipeProjectorTests.FullDocument_ProducesExpectedJsonLd.verified.txt` unmodified
