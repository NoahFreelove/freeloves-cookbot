---
phase: 15-nutrition-offline-cnf-canadian-nutrient-file
plan: 07
subsystem: ui
tags: [blazor, nutrition, cnf, jsonld, schema.org, design-tokens, cb-atoms]

# Dependency graph
requires:
  - phase: 15-nutrition-offline-cnf-canadian-nutrient-file
    provides: INutritionService/NutritionService (Plans 05/06), RecipeNutritionCache entity, NutritionInfoDto, JsonLdRecipeProjector.Project(doc, image, nutrition?) signature
provides:
  - "5-state nutrition panel on RecipeView (not-calculated, calculating, calculated, stale, error)"
  - "CTA-only compute trigger (CalculateNutrition handler) wired to INutritionService.ComputeAsync"
  - "Per-serving/total segmented toggle with keyboard navigation (role=radiogroup)"
  - "Coverage list with unmatched -- / low-confidence ≈ + CNF description+FoodId display"
  - "Non-dismissable verbatim Health Canada disclaimer in all 5 panel states (NUTR-05/SC4)"
  - "JSON-LD nutrition rebuilt with NutritionInfoDto when cache is current (NUTR-06/SC5)"
  - "15-HUMAN-UAT.md with 15 browser-only verification items"
affects: [future nutrition surfaces, recipe JSON-LD consumers, Phase 15 UAT]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "5-state panel machine in Razor: error → null/calculating → cache"
    - "Prerender-safe nutrition: GetCacheAsync in interactive circuit only, never in OnParametersSetAsync"
    - "JSON-LD rebuilt after interactive load when NutritionInfoDto available (non-stale cache)"
    - "PerIngredientDisplayRow inner class for coverage list rendering (typed JSON deserialization)"
    - "role=radiogroup segmented toggle with @onkeydown arrow-key navigation"

key-files:
  created:
    - ".planning/phases/15-nutrition-offline-cnf-canadian-nutrient-file/15-HUMAN-UAT.md"
    - ".planning/phases/15-nutrition-offline-cnf-canadian-nutrient-file/15-07-SUMMARY.md"
  modified:
    - "src/CookBot.Web/Components/Pages/RecipeView.razor"

key-decisions:
  - "Icon.Names.Alert does not exist in the project's Icon atom — error banner uses plain text only (no icon)"
  - "JSON-LD is rebuilt twice: prerender path always passes null (nutrition unknown at prerender); interactive path passes NutritionInfoDto when cache is current"
  - "Coverage list defaults to showing only unmatched + MEDIUM rows; matched rows behind Show all toggle"
  - "CalculateNutrition handler rebuilds JSON-LD immediately after ComputeAsync succeeds (SC5 wired to CTA)"

patterns-established:
  - "Prerender-safe nutrition load: GetCacheAsync only in OnAfterRenderAsync (interactive circuit), never in OnParametersSetAsync"
  - "5-state null/error/else structure: error check first, then null||calculating, then cache states"

requirements-completed: [NUTR-04, NUTR-05, NUTR-06]

# Metrics
duration: 45min
completed: 2026-06-08
---

# Phase 15 Plan 07: Nutrition Panel UI Summary

**5-state nutrition panel on RecipeView with CTA-only compute, per-serving/total toggle, honest coverage list (-- unmatched / ≈ low-confidence + CNF description+FoodId), non-dismissable Health Canada disclaimer in all states, and JSON-LD nutrition wiring via NutritionInfoDto**

## Performance

- **Duration:** ~45 min
- **Started:** 2026-06-08
- **Completed:** 2026-06-08
- **Tasks:** 1 (+ checkpoint)
- **Files modified:** 1 (RecipeView.razor) + 1 created (15-HUMAN-UAT.md)

## Accomplishments

- Added 5-state nutrition panel to RecipeView inside `<section aria-label="Estimated nutrition">` at margin-top:48px below recipe-body-grid, using only Cb atoms and design tokens (no MudBlazor)
- Implemented CTA-only compute trigger (`CalculateNutrition`); nutrition never computes on load or save (SC1/P7)
- Wired JSON-LD to pass NutritionInfoDto when a non-stale cache exists, both in the interactive load path and immediately after CTA compute (SC5/NUTR-06)
- Verbatim non-dismissable disclaimer present in all 5 states via `role="note"` block (SC4/NUTR-05)
- Coverage list renders "--" for UNMATCHED (never "0"), "≈" prefix for MEDIUM confidence, CNF description+FoodId for matched rows (SC2/NUTR-04)
- 15-item human UAT checklist written to 15-HUMAN-UAT.md covering all required browser verifications

## Task Commits

1. **Task 1: 5-state nutrition panel + CTA + JSON-LD wiring** - `b38e6a1` (feat)
2. **Docs: human UAT checklist** - `4e674ed` (docs)

## Files Created/Modified

- `src/CookBot.Web/Components/Pages/RecipeView.razor` — Added @using CookBot.Application.DTOs, @inject INutritionService NutritionSvc; nutrition state fields; GetCacheAsync on interactive load; CalculateNutrition handler; JSON-LD rebuild with NutritionInfoDto; 5-state panel markup; PerIngredientDisplayRow inner class; HandleToggleKeyDown; ParsePerIngredientMatches helper
- `.planning/phases/15-nutrition-offline-cnf-canadian-nutrient-file/15-HUMAN-UAT.md` — 15-item browser-only verification checklist

## Decisions Made

- `Icon.Names.Alert` does not exist in the project's Icon atom set (checked against the `static class Names` constants). The error banner (State 5) uses plain text only with the role=status color styling rather than fabricating an icon reference.
- JSON-LD is rebuilt on two paths: (1) after interactive load if cache exists and is current, (2) immediately inside the CTA handler after successful compute. This ensures nutrition.calories appears in the JSON-LD as soon as compute completes without a page reload.
- Coverage list collapses to unmatched + MEDIUM rows by default, with a "Show all {n} matches" ghost button. This keeps the panel compact for recipes with many matched ingredients.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Icon.Names.Alert does not exist**
- **Found during:** Task 1 (build)
- **Issue:** The UI-SPEC and plan referenced `Icon.Names.Alert` for the State 5 error banner, but this constant does not exist in the project's `Icon.Names` class. Build error CS0117.
- **Fix:** Removed the icon call from the error banner. The banner text "Nutrition calculation failed — try again." is displayed without an icon. The role=status + error color token conveys the state visually.
- **Files modified:** src/CookBot.Web/Components/Pages/RecipeView.razor
- **Committed in:** b38e6a1 (Task 1 commit)

**2. [Rule 3 - Blocking] @{} block inside @else not valid Razor syntax**
- **Found during:** Task 1 (build)
- **Issue:** Used `@{` inside an `@else { ... }` block which is invalid Razor (RZ1010 error). Inside a Razor code block, C# variables are declared directly without the `@{}` wrapper.
- **Fix:** Removed the `@{` / `}` wrapper from the variable declarations inside the `@else` block; declared `var cache`, `var matches`, etc. directly.
- **Files modified:** src/CookBot.Web/Components/Pages/RecipeView.razor
- **Committed in:** b38e6a1 (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (1 bug, 1 blocking)
**Impact on plan:** Both auto-fixes necessary for build correctness. No scope creep. The missing Alert icon is a minor visual-only omission; the error state still communicates clearly via the red banner text and role=status.

## Issues Encountered

- Pre-existing NutritionService.cs warnings (CS8602 nullable dereference) were present before this plan — out of scope per SCOPE BOUNDARY rule; logged, not fixed.

## User Setup Required

**The dev server must be restarted** before running UAT so the `AddNutritionTables` EF migration is applied and the CNF seed (~5,690 foods) is loaded. Run `./run.sh`.

## Self-Check

Files exist:
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — FOUND (modified)
- `.planning/phases/15-nutrition-offline-cnf-canadian-nutrient-file/15-HUMAN-UAT.md` — FOUND (created)

Commits:
- b38e6a1 — FOUND (feat: 5-state nutrition panel)
- 4e674ed — FOUND (docs: human UAT checklist)

Disclaimer grep: count=1 — PASS

Build: succeeded — PASS

Tests: 548 passed, 0 failed (excluding RequiresApiKey tests) — PASS

No "Calories" heading: grep count=0 — PASS

"Estimated nutrition" heading: grep count=5 — PASS (heading appears in each state branch + section aria-label + disclaimer)

## Self-Check: PASSED

## Next Phase Readiness

- Nutrition panel fully implemented per 15-UI-SPEC.md 5-state contract
- INutritionService is injected and wired; ownership enforcement is in NutritionService (Plan 05)
- JSON-LD nutrition wiring complete (SC5/NUTR-06)
- Human browser verification required: restart dev server, confirm CNF seed loaded, verify all 15 UAT items
- After human UAT passes, Phase 15 is complete

## Known Stubs

None — all data is wired to live service calls. The panel shows State 1 when no nutrition has been computed, which is correct and not a stub.

## Threat Flags

No new network endpoints, auth paths, or schema changes introduced. CNF text is rendered via normal Razor interpolation (auto HTML-encoded), not MarkupString (T-15-16 mitigated). ComputeAsync ownership enforcement is in NutritionService (T-15-17 mitigated). No compute on render (T-15-18 mitigated).

---
*Phase: 15-nutrition-offline-cnf-canadian-nutrient-file*
*Completed: 2026-06-08*
