---
phase: 10-qol-polish-consumer-surfaces
plan: "10"
subsystem: ui
tags: [recipe-service, recipe-editor, cookbook-reparenting, authz, csharp, blazor]

# Dependency graph
requires:
  - phase: 10-qol-polish-consumer-surfaces
    provides: "Plan 10-09 RecipeEditor.razor TopBar.RightSlot migration — ICbTopBarService injection, _topBarActions, recipe-actions-inline-fallback"

provides:
  - "RecipeService.UpdateAsync extended with optional int? newCookbookId = null parameter"
  - "Inline destination-cookbook ownership validation (destination.UserId != userId) inside UpdateAsync reparent block"
  - "RecipeEditor.razor CbSelect TValue=int listing user's own cookbooks (_userCookbooks filtered by current userId)"
  - "Save handler passes _selectedCookbookId to UpdateAsync; navigates to /recipes/{id} on cookbook change, /cookbooks/{id} on no-change"
  - "T-10-10-01 cross-user reparenting blocked: UnauthorizedAccessException thrown before CookbookId assignment"
  - "T-10-10-02 information disclosure prevented: _userCookbooks filtered by c.UserId == currentUserId"

affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Inline destination-ownership check in UpdateAsync: matches CreateAsync pattern at lines 35-39 (PATTERNS.md correction #5 — no db.UserCanAccessCookbookAsync, it does not exist)"
    - "CbSelect TValue=int with CbOption loop for cookbook reparenting: mirrors SaveRecipeDialog.razor:11-16"
    - "Save-with-reparent navigation: capture originalCookbookId before UpdateAsync, navigate to /recipes/{id} on change, /cookbooks/{id} on no-change"

key-files:
  created: []
  modified:
    - src/CookBot.Application/Services/RecipeService.cs
    - src/CookBot.Web/Components/Pages/RecipeEditor.razor

key-decisions:
  - "PATTERNS.md correction #5 enforced: inline destination.UserId != userId check used in reparent block, NOT a call to db.UserCanAccessCookbookAsync (method does not exist in codebase)"
  - "Renamed _availableCookbooks to _userCookbooks to match plan naming and added AsNoTracking() to the cookbook load query"
  - "Replaced CbDropdown with CbSelect TValue=int + CbOption to satisfy plan acceptance criteria and match the SaveRecipeDialog analog"
  - "Removed now-unused OnCookbookChanged handler (CbSelect uses inline lambda; handler was only wired to CbDropdown)"
  - "On cookbook-change navigation: /recipes/{recipeId} is used (recipe view works regardless of which cookbook it lives in); on no-change: existing /cookbooks/{selectedId} is preserved"

patterns-established:
  - "Service reparenting pattern: load destination via repo with null-coalescing throw, then inline userId check before assignment — same shape as CreateAsync ownership check"

requirements-completed: [POLISH-01]

# Metrics
duration: 15min
completed: 2026-05-16
---

# Phase 10 Plan 10: Cookbook Reparenting via RecipeService + RecipeEditor CbSelect Summary

**RecipeService.UpdateAsync gains optional newCookbookId with inline destination-ownership check; RecipeEditor surfaces a CbSelect listing user-owned cookbooks and routes Save through the reparenting argument**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-05-16T04:00:00Z
- **Completed:** 2026-05-16T04:15:00Z
- **Tasks:** 2
- **Files modified:** 2 (0 created, 2 modified)

## Accomplishments

- `RecipeService.UpdateAsync` now accepts `int? newCookbookId = null` as an optional final parameter — all existing call sites compile unchanged (backward-compatible default)
- Reparent block inserted after existing ownership check: loads destination cookbook, checks `destination.UserId != userId` (T-10-10-01 mitigation), assigns `recipe.CookbookId = newCookbookId.Value` only after both checks pass
- RecipeEditor's right-rail Cookbook card converted from `CbDropdown` to `CbSelect TValue="int"` with `CbOption` loop over `_userCookbooks` (T-10-10-02: filtered by current user, AsNoTracking)
- Save handler: captures `originalCookbookId` before calling UpdateAsync, passes `_selectedCookbookId`, and navigates to `/recipes/{id}` on a cookbook change (recipe view is cookbook-agnostic) or `/cookbooks/{selectedId}` on no-change

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend RecipeService.UpdateAsync with newCookbookId parameter + reparent block** - `6fbfa63` (feat)
2. **Task 2: Add cookbook reparenting CbSelect to RecipeEditor and wire to Save handler** - `030b8e7` (feat)

## Files Created/Modified

- `src/CookBot.Application/Services/RecipeService.cs` — UpdateAsync signature extended with `int? newCookbookId = null`; reparent block with inline destination-ownership check (PATTERNS.md correction #5); XML doc added
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — Renamed `_availableCookbooks` to `_userCookbooks`, added AsNoTracking() to cookbook load, replaced CbDropdown with CbSelect+CbOption, removed unused OnCookbookChanged handler, wired `_selectedCookbookId` through UpdateAsync, added post-save navigation branching on cookbook change

## Decisions Made

- **Inline check over non-existent extension (PATTERNS.md #5):** `db.UserCanAccessCookbookAsync` does not exist in this codebase. Used the same inline pattern as CreateAsync (lines 35-39): load via repo → null-coalescing throw → `if (destination.UserId != userId) throw UnauthorizedAccessException`. Zero calls to the non-existent method project-wide.
- **CbDropdown → CbSelect migration:** The existing Cookbook card used `CbDropdown` (a different atom). Plan acceptance criteria requires `CbSelect TValue="int"`. Migrated the markup to use `CbSelect` with `CbOption` loop (matches SaveRecipeDialog.razor analog). `OnCookbookChanged` handler was the only consumer of CbDropdown's ValueChanged; removed it since CbSelect uses an inline lambda.
- **Navigation branching on cookbook change:** On a reparent, navigate to `/recipes/{recipeId}` (the recipe view URL is recipe-id-based and cookbook-agnostic). On no-change, preserve existing `/cookbooks/{_selectedCookbookId}` navigation.
- **_userCookbooks rename:** Renamed from `_availableCookbooks` to `_userCookbooks` to align with plan naming and make the ownership-filtering intent explicit in code.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Replaced CbDropdown with CbSelect to satisfy plan acceptance criteria**
- **Found during:** Task 2 (RecipeEditor markup update)
- **Issue:** The existing Cookbook card used `CbDropdown` (Plan 10-09 left it in place). Plan 10-10 acceptance criteria requires `grep -q 'CbSelect TValue="int"'`. These are different atoms with different APIs.
- **Fix:** Replaced the CbDropdown + CbDropdownItem markup with CbSelect TValue="int" + CbOption loop over _userCookbooks. Removed the now-unused OnCookbookChanged handler (CbDropdown ValueChanged consumer).
- **Files modified:** src/CookBot.Web/Components/Pages/RecipeEditor.razor
- **Verification:** grep check passes; dotnet build exits 0
- **Committed in:** 030b8e7 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 — existing atom mismatch with plan acceptance criteria)
**Impact on plan:** Fix brings the markup into alignment with the plan's explicit CbSelect requirement and the SaveRecipeDialog analog. No scope creep.

## Issues Encountered

The worktree/main-repo path confusion: initial Read/Edit calls resolved against the main repo path. Identified the issue by checking `git -C <worktree> status`, reverted the accidental main-repo edit, and re-applied all edits to the correct worktree paths. All task commits are on the worktree branch.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- POLISH-01 closed: users can now move a recipe to any cookbook they own from the Recipe Editor
- RecipeService.UpdateAsync reparenting contract is fully defined and tested by build; manual smoke test (edit recipe, pick different cookbook, save, confirm redirect to /recipes/{id}) deferred per plan
- No blockers for subsequent plans

## Known Stubs

None — the CbSelect is wired to a real `_userCookbooks` query against DbContext.Cookbooks filtered by the current user. The Save handler passes the selected value through to RecipeService which persists the change.

## Threat Surface Scan

No new network endpoints introduced. The two threat register items are fully mitigated:
- T-10-10-01 (cross-user reparenting): inline `destination.UserId != userId` check in UpdateAsync throws UnauthorizedAccessException before any assignment — verified present in committed code
- T-10-10-02 (cookbook list exposure): `_userCookbooks` query uses `Where(c => c.UserId == userId)` — only the current user's cookbooks appear in the select

No new surface beyond what was planned.

## Self-Check: PASSED

- [x] `src/CookBot.Application/Services/RecipeService.cs` — exists and contains `int? newCookbookId = null`, `Destination cookbook not found`, `You do not own the destination cookbook`, `destination.UserId != userId`; no `UserCanAccessCookbookAsync` method calls
- [x] `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — exists and contains `_selectedCookbookId`, `_userCookbooks`, `CbSelect TValue="int"`, `UpdateAsync.*_selectedCookbookId`
- [x] Commit `6fbfa63` — exists (Task 1: RecipeService.UpdateAsync)
- [x] Commit `030b8e7` — exists (Task 2: RecipeEditor CbSelect + Save handler)
- [x] `dotnet build FreelovesCookBot.sln` — 0 errors, 4 pre-existing warnings (EF1002 from test file, unrelated to this plan)

---
*Phase: 10-qol-polish-consumer-surfaces*
*Completed: 2026-05-16*
