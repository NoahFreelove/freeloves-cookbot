---
phase: 03-editor-ux-without-special-syntax
plan: "07"
subsystem: refactor
tags: [regex-consolidation, dead-code-removal, gap-closure, single-source-of-truth]

# Dependency graph
requires:
  - phase: 03-editor-ux-without-special-syntax
    provides: "IngredientLinkPatterns.Pattern canonical regex (Plan 03-01)"
provides:
  - "IngredientRefDetectionService deleted — duplicate MarkdownLinkPattern (+1 quantifier) gone"
  - "IngredientRefDetectionServiceTests deleted — 3 [Fact]s covering the deleted service removed"
  - "Gap 4 (IN-01) closed: exactly one [name](#id) regex definition in production codebase"
affects:
  - "Any future work touching ingredient ref detection — must use IngredientLinkPatterns.Pattern"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Single source of truth: IngredientLinkPatterns.Pattern is the only [name](#id) regex"

key-files:
  created: []
  modified: []

key-decisions:
  - "Delete dead service outright rather than redirecting — no callers exist, deletion is cleanest mitigation"
  - "EF column RecipeStep.IngredientRefs deliberately NOT touched — POLISH-03 / Phase 4 scope"

patterns-established:
  - "IngredientLinkPatterns.Pattern: sole canonical [name](#id) regex; no duplication permitted"

requirements-completed: [EDITOR-04, EDITOR-06]

# Metrics
duration: 10min
completed: 2026-04-27
---

# Phase 03 Plan 07: Delete IngredientRefDetectionService (Gap 4 IN-01) Summary

**Duplicate MarkdownLinkPattern (+1 quantifier vs canonical *0) eliminated by deleting the dead IngredientRefDetectionService and its 3 tests, leaving IngredientLinkPatterns.Pattern as the sole [name](#id) regex**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-04-27T01:00:00Z
- **Completed:** 2026-04-27T01:10:20Z
- **Tasks:** 1
- **Files modified:** 2 (both deleted)

## Accomplishments

- Pre-flight grep confirmed exactly 5 references to `IngredientRefDetectionService` — all in the two files being deleted, zero production callers in `src/` outside the service file itself
- Deleted `IngredientRefDetectionService.cs` (29 lines) — the service containing the divergent `MarkdownLinkPattern` regex with `+` (1+ chars) vs the canonical `*` (0+ chars)
- Deleted `IngredientRefDetectionServiceTests.cs` (50 lines) — the 3 [Fact] test class that only exercised the deleted service
- Solution builds green (0 warnings, 0 errors); test suite passes at 182 (down 3 from baseline 185, exactly as expected)
- `IngredientLinkPatterns.Pattern` in `src/CookBot.Application/Recipes/IngredientLinkPatterns.cs` remains untouched — single source of truth confirmed

## Pre-flight Evidence

```
grep -rn "IngredientRefDetectionService" src/ tests/
```

Output (exactly 5 lines):
```
src/CookBot.Application/Services/IngredientRefDetectionService.cs:6:public static class IngredientRefDetectionService
tests/CookBot.Tests/Services/IngredientRefDetectionServiceTests.cs:6:public class IngredientRefDetectionServiceTests
tests/CookBot.Tests/Services/IngredientRefDetectionServiceTests.cs:16:        var refs = IngredientRefDetectionService.DetectRefs(
tests/CookBot.Tests/Services/IngredientRefDetectionServiceTests.cs:34:        var refs = IngredientRefDetectionService.DetectRefs(
tests/CookBot.Tests/Services/IngredientRefDetectionServiceTests.cs:46:        var refs = IngredientRefDetectionService.DetectRefs(
```

Zero references in any `RecipeService`, `Program.cs`, DI registration, or other production file. Deletion premise validated.

## Task Commits

1. **Task 1: Delete IngredientRefDetectionService and its tests** - `8383fad` (refactor)

**Plan metadata:** (docs commit below)

## Files Created/Modified

- `src/CookBot.Application/Services/IngredientRefDetectionService.cs` — DELETED (29 lines)
- `tests/CookBot.Tests/Services/IngredientRefDetectionServiceTests.cs` — DELETED (50 lines)

## Decisions Made

- **Delete outright:** No callers exist (Phase 1 D-13 retired the `RecipeService` call site when `IngredientRefs` writes were dropped). Redirecting to the canonical `IngredientLinkPatterns.Pattern` was not necessary — no production path needed it. Deletion is the cleanest closure.
- **EF column left intact:** `RecipeStep.IngredientRefs` is a database column managed by EF migrations. It is already write-empty per Phase 1 D-13. Dropping it is POLISH-03 in Phase 4 — out of scope for this gap-closure plan.
- **Regex divergence was the risk:** The deleted `MarkdownLinkPattern` used `[^\]]+` (one or more chars) while the canonical uses `[^\]]*` (zero or more chars). A future caller reviving the service would consume the wrong regex for ingredient names that could theoretically be empty strings in some edge case. Deletion eliminates the vector entirely.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. The pre-flight grep output matched exactly the 5 expected lines. Build and test suite passed on the first attempt after deletion.

## Known Stubs

None. This plan only deletes code — no UI rendering or data stubs introduced.

## Threat Flags

None. This plan deletes a dead service — no new external surface, auth paths, file access patterns, or schema changes introduced. T-03P07-01 (latent regex divergence) is fully mitigated by deletion.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Gap 4 (IN-01) closed: `IngredientLinkPatterns.Pattern` is confirmed as the only `[name](#id)` regex in production code
- The `MarkdownLinkPattern` name no longer appears anywhere in `src/`
- `RecipeStep.IngredientRefs` EF column remains — scheduled for Phase 4 POLISH-03 drop
- All parallel wave 1 plans (03-05, 03-06, 03-07) share the same test baseline; final merged test count may differ from 182 if other plans add tests

---
*Phase: 03-editor-ux-without-special-syntax*
*Completed: 2026-04-27*
