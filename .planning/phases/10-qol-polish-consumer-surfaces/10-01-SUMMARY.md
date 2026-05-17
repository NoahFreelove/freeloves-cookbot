---
phase: 10-qol-polish-consumer-surfaces
plan: "01"
subsystem: application-layer
tags: [layering, refactor, application-layer, interface-move]
dependency_graph:
  requires: []
  provides: [IRecipeMadeService-in-application-layer]
  affects: [plan-10-03-PantryMatchService]
tech_stack:
  added: []
  patterns: [interface-move-keep-implementation, global-razor-import]
key_files:
  created:
    - src/CookBot.Application/Services/IRecipeMadeService.cs
  modified:
    - src/CookBot.Web/Services/RecipeMadeService.cs
    - src/CookBot.Web/Program.cs
    - src/CookBot.Web/Components/_Imports.razor
decisions:
  - "Moved IRecipeMadeService interface to CookBot.Application.Services (Path A from PATTERNS.md Layering Note 1); implementation stays in Web layer"
  - "Added @using CookBot.Application.Services to _Imports.razor (global razor import) rather than per-file; covers all 3 razor consumers in one edit"
  - "Copied all 4 method signatures verbatim from source file (plan body listed 3; GetMadeCountAsync was also present and required for compilation)"
metrics:
  duration: "~5 minutes"
  completed: "2026-05-16"
  tasks_completed: 3
  tasks_total: 3
  files_created: 1
  files_modified: 3
---

# Phase 10 Plan 01: IRecipeMadeService Interface Move Summary

Move `IRecipeMadeService` from `CookBot.Web.Services` to `CookBot.Application.Services` so the Phase 10 `PantryMatchService` (Application-layer) can depend on the recency-debounce abstraction without inverting the Clean/Onion dependency direction.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create IRecipeMadeService.cs in Application layer | 3a4668c | src/CookBot.Application/Services/IRecipeMadeService.cs (created) |
| 2 | Remove duplicate interface from RecipeMadeService.cs and add Application using | 02ffa23 | src/CookBot.Web/Services/RecipeMadeService.cs |
| 3 | Verify build + all IRecipeMadeService consumers resolve | 228c182 | src/CookBot.Web/Program.cs, src/CookBot.Web/Components/_Imports.razor |

## What Was Built

`IRecipeMadeService` interface relocated to `CookBot.Application.Services` namespace. All existing consumers (Home.razor.cs, RecipeView.razor, CookingMode.razor, Program.cs) continue to resolve through the moved interface — no callsite logic changed, only `using` directives added. DI registration `AddScoped<IRecipeMadeService, RecipeMadeService>()` in Program.cs is unchanged.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Interface had 4 methods, plan body listed 3**

- **Found during:** Task 1
- **Issue:** The plan's interface excerpt showed 3 methods (`LogMadeAsync`, `GetLastCookAsync`, `GetRecentForUserAsync`) but the actual source file also contains `GetMadeCountAsync`. Omitting it would cause a compilation error since `RecipeMadeService` implements all 4 methods.
- **Fix:** Copied all 4 method signatures verbatim from the source file as instructed by the task action ("copy the method signatures verbatim from RecipeMadeService.cs:12-18").
- **Files modified:** src/CookBot.Application/Services/IRecipeMadeService.cs
- **Commit:** 3a4668c

**2. [Rule 3 - Blocking fix] _Imports.razor needed @using for razor consumers**

- **Found during:** Task 3 (build verification)
- **Issue:** `dotnet build` revealed that razor files (`RecipeView.razor`, `CookingMode.razor`) using `@inject IRecipeMadeService` lose access when the interface moves out of `CookBot.Web.Services`. The plan called for adding `using` directives to failing files — adding to `_Imports.razor` covers all razor consumers in one edit rather than 3 individual files.
- **Fix:** Added `@using CookBot.Application.Services` to `src/CookBot.Web/Components/_Imports.razor` (global import).
- **Files modified:** src/CookBot.Web/Components/_Imports.razor
- **Commit:** 228c182

## Known Stubs

None. This is a pure refactor — no UI surfaces, no data flows, no stubs.

## Threat Flags

None. This is a pure type-system refactor with no runtime behavior change. The threat register disposition (T-10-01-01: accept) remains accurate — same concrete resolves, same authz logic in `RecipeMadeService.LogMadeAsync` is untouched.

## Self-Check

- [x] `src/CookBot.Application/Services/IRecipeMadeService.cs` exists
- [x] `src/CookBot.Web/Services/RecipeMadeService.cs` — no `public interface IRecipeMadeService` block, has `using CookBot.Application.Services`
- [x] `src/CookBot.Web/Program.cs` — has `using CookBot.Application.Services`, `AddScoped<IRecipeMadeService, RecipeMadeService>()` unchanged
- [x] `src/CookBot.Web/Components/_Imports.razor` — has `@using CookBot.Application.Services`
- [x] Commits 3a4668c, 02ffa23, 228c182 exist in git log
- [x] `dotnet build` exits 0, 0 errors, 4 pre-existing warnings only (EF1002 in RecipeTagBackfillTests.cs — pre-existing, out of scope)

## Self-Check: PASSED
