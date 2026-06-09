---
phase: 13-export-interoperability
plan: 01
subsystem: api
tags: [json-ld, schema-org, iso8601, recipe, serialization, security]

# Dependency graph
requires:
  - phase: 12-richer-format
    provides: RecipeDocument v4 with StepNode polymorphism, RecipeProvenance, IngredientEntry

provides:
  - JsonLdRecipeProjector.Project(RecipeDocument, absoluteImageUrl?) → Schema.org Recipe JSON-LD string
  - Iso8601DurationFormatter.ToIso8601Duration(int?) → "PT#H#M" | null
  - 11 unit + Verify golden-snapshot tests locking the JSON-LD shape

affects:
  - 13-export-interoperability (Plan 03 wires JsonLdRecipeProjector into RecipeView <head>)
  - 15-nutrition (Phase 15 wires nutrition.calories into the JSON-LD scaffold built here)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pure static Application-layer projector (RecipeDocument → string) with no DI/framework refs"
    - "STJ default (HTML-safe) encoder for raw MarkupString <script> output"
    - "Deterministic tag classification via curated allow-lists (never fabricate schema.org fields)"
    - "Dictionary<string,object> with explicit null-guards (WhenWritingNull does not apply to dict values)"
    - "Verify golden-file snapshot in tests/CookBot.Tests/Snapshots/ for JSON-LD shape regression"

key-files:
  created:
    - src/CookBot.Application/Recipes/Iso8601DurationFormatter.cs
    - src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs
    - tests/CookBot.Tests/Recipes/Iso8601DurationFormatterTests.cs
    - tests/CookBot.Tests/Recipes/JsonLdRecipeProjectorTests.cs
    - tests/CookBot.Tests/Snapshots/JsonLdRecipeProjectorTests.FullDocument_ProducesExpectedJsonLd.verified.txt
  modified: []

key-decisions:
  - "Used explicit null-guards in Dictionary<string,object> instead of relying on DefaultIgnoreCondition=WhenWritingNull (which only applies to typed object properties, not dictionary values)"
  - "HTML-safe STJ default encoder (no UnsafeRelaxedJsonEscaping) — escapes <,>,& to \\uXXXX so recipe content cannot break out of a <script> block"
  - "ALL tags always go to keywords; recipeCuisine/recipeCategory only on allow-list match, omitted otherwise — never fabricated"
  - "Iso8601DurationFormatter placed in CookBot.Application.Recipes namespace (separate file, reusable)"
  - "recipeYield emitted as int (not string) — valid Schema.org; simpler and consistent with Servings type"

patterns-established:
  - "Pure Application projector: public static class in CookBot.Application.Recipes, file-scoped namespace, zero framework refs"
  - "Tag allow-list lookup: case-insensitive match emits curated spelling, omitted on no-match"
  - "HowToSection/HowToStep hierarchy: SectionStep groups ContentSteps that follow it until next section"

requirements-completed: [INTEROP-01, INTEROP-02]

# Metrics
duration: 4min
completed: 2026-06-06
---

# Phase 13 Plan 01: JSON-LD Projector & ISO-8601 Duration Formatter Summary

**Pure Schema.org Recipe JSON-LD projector from RecipeDocument v4 using STJ HTML-safe encoder with curated tag allow-lists for category/cuisine classification**

## Performance

- **Duration:** 4 min
- **Started:** 2026-06-06T21:46:29Z
- **Completed:** 2026-06-06T21:50:41Z
- **Tasks:** 2 (both TDD)
- **Files modified:** 5 (all new)

## Accomplishments

- `Iso8601DurationFormatter.ToIso8601Duration` hand-rolled PT#H#M formatter (null/0 → omit; 30→PT30M, 60→PT1H, 90→PT1H30M, 125→PT2H5M) with 7 unit tests
- `JsonLdRecipeProjector.Project` pure static projector emitting structurally valid Schema.org Recipe JSON-LD with HowToSection/HowToStep nesting, all-tags-to-keywords, curated allow-list category/cuisine classification, and HTML-safe STJ encoding
- 11 tests including: golden Verify snapshot, `ScriptBreakout_IsEscaped`, `NeverEmitsAggregateRating`, `NoMatch_OmitsCategoryAndCuisine`, `Author_FromAuthorName`, and per-field assertions

## Task Commits

Each task was committed atomically:

1. **Task 1: Iso8601DurationFormatter** - `2fd31cf` (feat)
2. **Task 2: JsonLdRecipeProjector** - `d541c15` (feat)

## Files Created/Modified

- `src/CookBot.Application/Recipes/Iso8601DurationFormatter.cs` — Pure formatter: int? minutes → "PT#H#M" | null
- `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs` — Pure projector: RecipeDocument → Schema.org Recipe JSON-LD string
- `tests/CookBot.Tests/Recipes/Iso8601DurationFormatterTests.cs` — 7 unit tests covering all edge cases
- `tests/CookBot.Tests/Recipes/JsonLdRecipeProjectorTests.cs` — 11 unit + Verify snapshot tests
- `tests/CookBot.Tests/Snapshots/JsonLdRecipeProjectorTests.FullDocument_ProducesExpectedJsonLd.verified.txt` — Committed golden snapshot

## Decisions Made

- **Dictionary null-guard pattern:** `DefaultIgnoreCondition = WhenWritingNull` applies to typed POCO properties, not `Dictionary<string, object>` values. Used explicit `if (x is not null) model["key"] = x` guards to ensure absent fields are omitted from the JSON-LD output. This was discovered during the GREEN phase and auto-fixed.
- **HTML-safe encoder confirmed:** Did not set `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (which `JsonRecipeSerializer._indented` uses). The STJ default encoder escapes `<`, `>`, `&` to `\uXXXX` form — safe inside a raw `<script>` MarkupString block. Test `ScriptBreakout_IsEscaped` verifies this.
- **Curated allow-lists in projector itself:** Both CUISINE and COURSE/CATEGORY allow-lists are `private static readonly` arrays in the projector. Case-insensitive match emits the curated spelling (e.g. tag "italian" → "Italian"). No fabrication.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] DefaultIgnoreCondition.WhenWritingNull does not apply to Dictionary<string, object> values**
- **Found during:** Task 2 (JsonLdRecipeProjector GREEN phase, first test run)
- **Issue:** `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` in `LdOptions` silences null properties on typed POCOs but does NOT suppress null entries in a `Dictionary<string, object?>`. Tests `Image_OmittedWhenNull`, `Author_FromAuthorName`, `NoMatch_OmitsCategoryAndCuisine`, and `Durations_NullMinutes_PropertyAbsent` all failed because null dictionary values were serialized as JSON `null` rather than being omitted.
- **Fix:** Switched model from `Dictionary<string, object?>` with null values to `Dictionary<string, object>` with explicit `if (x is not null) model["key"] = x` guards. Only non-null fields are added to the dictionary.
- **Files modified:** `src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs`
- **Verification:** All 11 tests pass; `Image_OmittedWhenNull` and `Author_FromAuthorName` confirm null fields are absent from output.
- **Committed in:** d541c15

---

**Total deviations:** 1 auto-fixed (Rule 1 - Bug)
**Impact on plan:** Essential fix for correctness — absent properties must be omitted, not serialized as null. No scope creep; all plan requirements met.

## Issues Encountered

None beyond the deviation above.

## Threat Surface Scan

No new surface introduced. The projector is a pure static function over already-authorized RecipeDocument (no new network endpoints, no auth paths, no file access, no schema changes). T-13-01 (XSS via `</script>`) is mitigated by confirmed HTML-safe STJ encoding (`ScriptBreakout_IsEscaped` test passes).

## Known Stubs

None — projector emits real data from RecipeDocument. No hardcoded placeholders.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- `JsonLdRecipeProjector.Project(doc, absoluteImageUrl)` is production-ready
- `Iso8601DurationFormatter.ToIso8601Duration` is production-ready
- Plan 02 (CooklangRecipeProjector) can proceed independently
- Plan 03 (RecipeView head wiring) can call `JsonLdRecipeProjector.Project(_doc, resolvedImageUrl)` and render via `<HeadContent><script type="application/ld+json">@((MarkupString)_jsonLd)</script></HeadContent>`
- Phase 15 can extend the projector to add `nutrition.calories` when NutritionService data is available

---
*Phase: 13-export-interoperability*
*Completed: 2026-06-06*

## Self-Check

Files:
- `[ -f src/CookBot.Application/Recipes/Iso8601DurationFormatter.cs ]` → FOUND
- `[ -f src/CookBot.Application/Recipes/JsonLdRecipeProjector.cs ]` → FOUND
- `[ -f tests/CookBot.Tests/Recipes/Iso8601DurationFormatterTests.cs ]` → FOUND
- `[ -f tests/CookBot.Tests/Recipes/JsonLdRecipeProjectorTests.cs ]` → FOUND
- `[ -f tests/CookBot.Tests/Snapshots/JsonLdRecipeProjectorTests.FullDocument_ProducesExpectedJsonLd.verified.txt ]` → FOUND

Commits:
- `2fd31cf` feat(13-01): Iso8601DurationFormatter → FOUND
- `d541c15` feat(13-01): JsonLdRecipeProjector → FOUND

Tests: 18/18 pass (7 duration + 11 projector)
Build: clean (0 errors, 0 warnings in production code)
No new NuGet packages.

## Self-Check: PASSED
