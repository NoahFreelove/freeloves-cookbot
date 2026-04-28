---
phase: 03-editor-ux-without-special-syntax
plan: "06"
subsystem: web-cooking-mode
tags: [blazor, cooking-mode, ingredient-highlight, link-resolution, gap-closure, wr-03, tdd]
dependency_graph:
  requires:
    - "03-03 (cooking-mode chip rendering via RecipeChipComposer)"
    - "01-02 (Phase 1 D-13 — IngredientRefs writes retired in RecipeService)"
    - "CookBot.Application/Recipes/IngredientLinkPatterns.cs (canonical regex)"
  provides:
    - "Working cooking-mode sidebar highlight driven by [name](#id) link resolution"
    - "CurrentStepRefIds() helper with per-step reference-equality cache"
  affects:
    - "src/CookBot.Web/Components/Pages/CookingMode.razor"
    - "src/CookBot.Application/AssemblyAttributes.cs"
    - "tests/CookBot.Tests/Web/CookingModeSidebarHighlightTests.cs"
tech_stack:
  added: []
  patterns:
    - "Per-step reference-equality cache (ReferenceEquals on RecipeStep instance) for Blazor render-loop safety"
    - "InternalsVisibleTo(CookBot.Tests) added to CookBot.Application for test access to internal IngredientLinkPatterns"
key_files:
  created:
    - tests/CookBot.Tests/Web/CookingModeSidebarHighlightTests.cs
  modified:
    - src/CookBot.Web/Components/Pages/CookingMode.razor
    - src/CookBot.Application/AssemblyAttributes.cs
decisions:
  - "Cache invalidation via ReferenceEquals(CurrentStep) — sufficient because Next/Previous navigation always assigns a different RecipeStep object instance from _navigableSteps; no identity confusion possible within a single cooking session"
  - "Doc comment on CurrentStepRefIds() does not repeat the exact dead-read pattern string to avoid false-positive grep gates"
  - "InternalsVisibleTo(CookBot.Tests) added to CookBot.Application AssemblyAttributes to allow tests to access the internal IngredientLinkPatterns class; this is the minimal blast-radius approach rather than making the class public"
metrics:
  duration: "~3 minutes"
  completed: "2026-04-27T01:13:00Z"
  tasks_completed: 1
  files_changed: 3
---

# Phase 3 Plan 6: Cooking-Mode Sidebar Highlight Fix (WR-03) Summary

**One-liner:** Replaced dead `CurrentStep.IngredientRefs.Contains` read with a cached `[name](#id)` parse via `IngredientLinkPatterns.Pattern`, closing Gap 3 (WR-03) so freshly-saved recipes show the correct sidebar highlight in cooking mode.

## What Was Done

### Gap Being Closed

Gap 3 (WR-03): `CookingMode.razor:146` was reading `CurrentStep.IngredientRefs.Contains(ri.RecipeLocalId)` to drive the ingredient sidebar highlight. Phase 1 Plan 02 (D-13) retired writes to `RecipeStep.IngredientRefs` in `RecipeService.CreateAsync`/`UpdateAsync`. As a result, every recipe saved after Phase 1 has an empty `IngredientRefs` list and the sidebar highlight never fires.

### Changes Made

**`src/CookBot.Web/Components/Pages/CookingMode.razor`**

Three localized changes, no other code touched:

1. **Using directives added** (lines 5–6):
   - `@using CookBot.Application.Recipes` — exposes the internal `IngredientLinkPatterns` class (allowed via `InternalsVisibleTo("CookBot.Web")` that was already in place)
   - `@using System.Text.RegularExpressions` — for `Match` type in the helper body

2. **Sidebar foreach fixed** (lines 144–153):
   - Added a `@{ var currentStepRefIds = CurrentStepRefIds(); }` block before the foreach
   - Replaced `CurrentStep.IngredientRefs.Contains(ri.RecipeLocalId)` with `currentStepRefIds.Contains(ri.RecipeLocalId)`
   - The `MudStack` `Style` binding that renders `var(--mud-palette-primary-lighten)` is unchanged — only the source variable changed

3. **`CurrentStepRefIds()` helper added** (lines 240–264):
   ```csharp
   private RecipeStep? _refIdsCacheStep;
   private HashSet<int> _refIdsCache = new();

   private HashSet<int> CurrentStepRefIds()
   {
       if (ReferenceEquals(_refIdsCacheStep, CurrentStep)) return _refIdsCache;
       var ids = new HashSet<int>();
       var text = CurrentStep.Text ?? string.Empty;
       foreach (Match m in IngredientLinkPatterns.Pattern.Matches(text))
       {
           if (int.TryParse(m.Groups[2].Value, out var id) && id > 0)
               ids.Add(id);
       }
       _refIdsCache = ids;
       _refIdsCacheStep = CurrentStep;
       return ids;
   }
   ```

**`src/CookBot.Application/AssemblyAttributes.cs`**

Added `[assembly: InternalsVisibleTo("CookBot.Tests")]` so tests can directly reference `IngredientLinkPatterns` (Rule 3 fix: required to make the TDD tests compile).

**`tests/CookBot.Tests/Web/CookingModeSidebarHighlightTests.cs`**

8 tests (TDD RED/GREEN) locking the parse behavior:
- `RefIds_StepWithTwoLinks_ReturnsBothIds` — basic case: `[Salt](#1) and [Pepper](#2)` → `{1, 2}`
- `RefIds_StepWithNoLinks_ReturnsEmptySet` — plain text produces no IDs
- `RefIds_NullOrEmptyText_ReturnsEmptySetWithoutThrowing` — Theory covering null/empty/whitespace
- `RefIds_CalledMultipleTimes_ReturnsSameResult` — idempotency: 5 calls with same text → same result
- `RefIds_DifferentText_ReturnsNewStepIds` — cache invalidates: step1 IDs ≠ step2 IDs
- `RefIds_ZeroOrNegativeId_IsRejected` — `#0` is rejected by the `id > 0` guard

## Cache Invalidation Strategy

`ReferenceEquals(_refIdsCacheStep, CurrentStep)` is the cache key. This suffices because:

- `CurrentStep` is defined as `_navigableSteps[_currentStepIndex]`
- `_navigableSteps` is built once per recipe load from `_recipe.Steps.Where(s => !s.IsSection).ToList()`
- `NextStep()` increments `_currentStepIndex`; `PreviousStep()` decrements it
- The new index yields a different `RecipeStep` object from the list — reference equality detects the change immediately
- `OnParametersSetAsync` resets `_navigableSteps = new()` on recipe change, which also invalidates the cache (new list, new object references)
- No mutation of `CurrentStep.Text` occurs during cooking mode — so the same step can safely return the cached set for the entire duration on that step

This avoids re-parsing `CurrentStep.Text` on every sidebar iteration (which runs once per ingredient per render tick) while guaranteeing freshness after navigation.

## Acceptance Criteria Verification

| Check | Command | Result |
|-------|---------|--------|
| Build clean | `dotnet build FreelovesCookBot.sln` | 0 warnings, 0 errors |
| Canonical regex wired | `grep -q "IngredientLinkPatterns.Pattern" CookingMode.razor` | PASS |
| Helper present | `grep -q "CurrentStepRefIds" CookingMode.razor` | PASS |
| Dead read removed | `! grep -qE "CurrentStep\.IngredientRefs\.Contains" CookingMode.razor` | PASS |
| Using directive present | `grep -q "@using CookBot.Application.Recipes" CookingMode.razor` | PASS |
| No new IngredientRefs writes | `grep -n "IngredientRefs" RecipeService.cs` | PASS (comments only) |
| Full test suite | `dotnet test --filter "Category!=RequiresApiKey" --no-build` | 193/193 passed (8 new + 185 baseline) |

## Commits

| Hash | Type | Description |
|------|------|-------------|
| `1452e2f` | `test(03-06)` | RED: add CookingModeSidebarHighlightTests (8 tests) + InternalsVisibleTo(CookBot.Tests) |
| `9deebb5` | `feat(03-06)` | GREEN: implement CurrentStepRefIds() helper; replace dead IngredientRefs.Contains read |

## TDD Gate Compliance

- RED gate commit: `1452e2f` (test)
- GREEN gate commit: `9deebb5` (feat)
- REFACTOR: not needed — implementation is clean as written

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added InternalsVisibleTo("CookBot.Tests") to CookBot.Application**
- **Found during:** Task 1 RED phase
- **Issue:** `IngredientLinkPatterns` is `internal` and `AssemblyAttributes.cs` only granted access to `CookBot.Web`. The test file referencing `IngredientLinkPatterns.Pattern` directly failed to compile with CS0122.
- **Fix:** Added `[assembly: InternalsVisibleTo("CookBot.Tests")]` to `src/CookBot.Application/AssemblyAttributes.cs`
- **Files modified:** `src/CookBot.Application/AssemblyAttributes.cs`
- **Commit:** `1452e2f`

**2. [Rule 1 - Bug] Updated doc comment to avoid false-positive grep gate**
- **Found during:** Task 1 acceptance check
- **Issue:** The XML doc comment on `CurrentStepRefIds()` contained the literal string `CurrentStep.IngredientRefs.Contains` (the exact pattern the acceptance criterion greps for). The `! grep -qE` check was failing because the comment matched.
- **Fix:** Reworded the doc comment to `"replaces the dead IngredientRefs list read"` without repeating the exact method-call string.
- **Files modified:** `src/CookBot.Web/Components/Pages/CookingMode.razor`
- **Commit:** `9deebb5`

## Outstanding Manual UAT

Per plan and `03-GOAL-VERIFICATION.md` Human Verification #3:

**Cooking-mode sidebar highlight on freshly-saved recipe** — After saving a new recipe that includes chips referencing 3 ingredients (e.g., steps that contain `[Salt](#1)`, `[Pepper](#2)`, `[Butter](#3)` chips), open cooking mode at step 1. Verify:
- The 3 referenced ingredients in the sidebar receive the `primary-lighten` background highlight
- Non-referenced ingredients do not receive the highlight
- Navigating to a different step updates the highlight correctly

This is surface-in-03-08 outstanding UAT (visual CSS property; only verifiable in a real browser circuit with a freshly-saved Phase-1 recipe).

## Threat Surface Scan

No new network endpoints, auth paths, file access patterns, or schema changes introduced. The only new surface is `IngredientLinkPatterns.Pattern.Matches(CurrentStep.Text)` which was already analyzed in the plan's threat model (T-03P06-01: linear-time regex on bounded input — accepted; T-03P06-02: `int.TryParse` + `id > 0` guard — mitigated as implemented; T-03P06-03: information disclosure — accepted, data is already authorized).

## Self-Check: PASSED

| Item | Status |
|------|--------|
| `src/CookBot.Web/Components/Pages/CookingMode.razor` | FOUND |
| `tests/CookBot.Tests/Web/CookingModeSidebarHighlightTests.cs` | FOUND |
| `src/CookBot.Application/AssemblyAttributes.cs` | FOUND |
| RED commit `1452e2f` | FOUND |
| GREEN commit `9deebb5` | FOUND |
