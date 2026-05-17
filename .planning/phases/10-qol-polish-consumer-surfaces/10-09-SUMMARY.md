---
phase: 10-qol-polish-consumer-surfaces
plan: "09"
subsystem: ui
tags: [top-bar, slot-service, responsive-css, blazor, render-fragment, mainlayout, recipe-view, recipe-editor]

# Dependency graph
requires:
  - phase: 10-qol-polish-consumer-surfaces
    provides: "Plan 10-08 ICbTopBarService scoped service with SetRightSlot/Clear/OnChanged/RightSlot"
  - phase: 10-qol-polish-consumer-surfaces
    provides: "Plan 10-02 TopBar [Parameter] RenderFragment? RightSlot already on TopBar.razor"

provides:
  - "MainLayout: ICbTopBarService injection, OnChanged subscription in OnInitializedAsync, IDisposable Dispose unsubscription, RightSlot passthrough to TopBar"
  - "TopBar: topbar-right-slot wrapper div as stable CSS hook for D-59 media query"
  - "RecipeView: _topBarActions RenderFragment via @<text> markup-template (W-02), SetRightSlot in OnInitialized, inline fallback with class recipe-actions-inline-fallback"
  - "RecipeEditor: _topBarActions RenderFragment via @<text> markup-template (W-02), SetRightSlot in OnInitialized, inline fallback with class recipe-actions-inline-fallback"
  - "cookbot-design.css: @media (max-width: 720px) hides .topbar-right-slot; @media (min-width: 721px) hides .recipe-actions-inline-fallback"

affects:
  - 10-10  # RecipeEditor cookbook reparenting (adds CbSelect to editor header, orthogonal to this plan's action-row migration)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ICbTopBarService subscription in layout component: MainLayout subscribes in OnInitializedAsync and unsubscribes in Dispose — mirrors CbToastHost pattern"
    - "Razor @<text> markup-template RenderFragment construction (W-02): both RecipeView and RecipeEditor use identical @<text>...</text> assignment shape for _topBarActions"
    - "Dual-render pattern: same _topBarActions RenderFragment renders in TopBar.RightSlot (wide) AND inline fallback (narrow); single source of truth for action buttons"
    - "D-59 responsive toggle: CSS media queries at 720px boundary route rendering between slot and inline-fallback without JS"

key-files:
  created: []
  modified:
    - src/CookBot.Web/Components/Layout/MainLayout.razor
    - src/CookBot.Web/Components/Layout/TopBar.razor
    - src/CookBot.Web/Components/Pages/RecipeView.razor
    - src/CookBot.Web/Components/Pages/RecipeEditor.razor
    - src/CookBot.Web/wwwroot/css/cookbot-design.css

key-decisions:
  - "W-02 (per plan): @<text> markup-template construction confirmed for both files — @<text>...</text> compiles cleanly in .NET 10 Razor without fallback needed"
  - "Inline fallback wraps @_topBarActions (not duplicated button markup) — single source of truth for button behavior in both viewports"
  - "RecipeEditor inline fallback retains Back + breadcrumb (layout elements) and delegates action buttons to @_topBarActions; TopBar slot shows only the action buttons"
  - "IDisposable.Dispose() is a no-op in RecipeView and RecipeEditor — D-57 auto-clear on navigation removes the need for explicit Clear() calls"

patterns-established:
  - "Page → TopBar slot pattern: inject ICbTopBarService, implement IDisposable, construct _topBarActions via @<text> in OnInitialized, call SetRightSlot, wrap inline row with class=recipe-actions-inline-fallback"

requirements-completed: [POLISH-04]

# Metrics
duration: 3min
completed: 2026-05-17
---

# Phase 10 Plan 09: TopBar.RightSlot Wiring Summary

**MainLayout → TopBar slot pipeline live: RecipeView and RecipeEditor feed actions via ICbTopBarService with identical @<text> RenderFragment shape, responsive CSS toggles at 720px**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-05-17T03:36:40Z
- **Completed:** 2026-05-17T03:40:12Z
- **Tasks:** 3
- **Files modified:** 5 (0 created, 5 modified)

## Accomplishments

- MainLayout now injects `ICbTopBarService`, subscribes to `OnChanged` in `OnInitializedAsync`, passes `TopBarService.RightSlot` to `<TopBar RightSlot="..." />`, and unsubscribes in `Dispose` (T-10-09-01 memory-leak mitigation)
- TopBar wraps `@RightSlot` in `<div class="topbar-right-slot">` as the stable CSS hook for D-59 media-query hiding below 720px
- RecipeView and RecipeEditor both construct `_topBarActions` via the Razor `@<text>` markup-template form (W-02 committed shape, confirmed compiling in .NET 10), call `TopBarService.SetRightSlot` in `OnInitialized`, and retain their inline action rows wrapped with `class="recipe-actions-inline-fallback"` for narrow-viewport fallback
- Two CSS `@media` rules appended to `cookbot-design.css`: `.topbar-right-slot` hidden below 720px; `.recipe-actions-inline-fallback` hidden above 721px — slot/inline toggle with zero JavaScript

## Task Commits

Each task was committed atomically:

1. **Task 1: Wire ICbTopBarService into MainLayout + add TopBar wrapper class** - `03fc75d` (feat)
2. **Task 2: Migrate RecipeView and RecipeEditor to use SetRightSlot via @<text> markup-template** - `27de5a3` (feat)
3. **Task 3: Add responsive media-query CSS rules for slot / inline-fallback toggle** - `ef2607f` (feat)

## Files Created/Modified

- `src/CookBot.Web/Components/Layout/MainLayout.razor` - Added `@inject ICbTopBarService TopBarService`, `@implements IDisposable`, `OnChanged` subscription in `OnInitializedAsync`, `HandleSlotChanged` method, `Dispose()` unsubscription, `RightSlot="@TopBarService.RightSlot"` on `<TopBar>`
- `src/CookBot.Web/Components/Layout/TopBar.razor` - Wrapped `@RightSlot` in `<div class="topbar-right-slot">` for stable CSS hook (no API changes)
- `src/CookBot.Web/Components/Pages/RecipeView.razor` - Added `ICbTopBarService` injection, `IDisposable`, `_topBarActions` field, `OnInitialized` with `@<text>` template (Edit/Share/Schedule/Cook this), `SetRightSlot` call, inline fallback wrapper with `class="recipe-actions-inline-fallback"`
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` - Added `ICbTopBarService` injection, `IDisposable`, `_topBarActions` field, `OnInitialized` with `@<text>` template (Paste raw text/Cancel/Save), `SetRightSlot` call, inline fallback wrapper with `class="recipe-actions-inline-fallback"`
- `src/CookBot.Web/wwwroot/css/cookbot-design.css` - Appended two `@media` rules for D-59 responsive slot/inline-fallback toggle at 720px boundary

## Decisions Made

- **W-02 confirmed:** `@<text>...</text>` Razor markup-template syntax compiles cleanly under .NET 10 — no fallback to RenderTreeBuilder needed. Both files use the identical construction shape per plan requirement.
- **Inline fallback delegates to @_topBarActions:** Rather than duplicating button markup, the inline fallback renders `@_topBarActions` — single source of truth for button behavior (identical rendering in both slot and fallback contexts).
- **RecipeEditor fallback layout:** The inline fallback div retains the Back button and breadcrumb (layout navigation elements) and renders `@_topBarActions` for the action buttons. The TopBar slot shows only the action buttons (Paste raw text, Cancel, Save). This means narrow-viewport users see the full header row; wide-viewport users see action buttons in TopBar only.
- **IDisposable.Dispose() no-op on pages:** D-57 auto-clear on navigation handles slot cleanup; page-side Dispose needs no `Clear()` call. The `@implements IDisposable` is present per plan requirement but Dispose body is empty.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- TopBar.RightSlot pipeline is fully operational: MainLayout feeds the slot, TopBar renders it, two pages consume it
- Pattern is established for any future page wanting to inject TopBar actions: inject `ICbTopBarService`, use `@<text>` template, call `SetRightSlot` in `OnInitialized`
- Plan 10-10 (cookbook reparenting CbSelect in RecipeEditor) can add its card orthogonally — this plan's action-row migration does not conflict with that plan's editor-header additions

## Known Stubs

None — this plan delivers complete, functional TopBar slot wiring with no stub patterns.

## Threat Surface Scan

No new threat surface beyond T-10-09-01 (mitigated by `IDisposable` + `Dispose` unsubscription in MainLayout). No new network endpoints, auth paths, file access patterns, or schema changes introduced.

## Self-Check: PASSED

- [x] `src/CookBot.Web/Components/Layout/MainLayout.razor` — exists and contains ICbTopBarService injection + subscription + Dispose
- [x] `src/CookBot.Web/Components/Layout/TopBar.razor` — exists and contains `topbar-right-slot` class
- [x] `src/CookBot.Web/Components/Pages/RecipeView.razor` — exists and contains `_topBarActions = @<text>` + `recipe-actions-inline-fallback`
- [x] `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — exists and contains `_topBarActions = @<text>` + `recipe-actions-inline-fallback`
- [x] `src/CookBot.Web/wwwroot/css/cookbot-design.css` — exists and contains both @media rules
- [x] Commit `03fc75d` — exists (Task 1: MainLayout + TopBar)
- [x] Commit `27de5a3` — exists (Task 2: RecipeView + RecipeEditor)
- [x] Commit `ef2607f` — exists (Task 3: CSS media rules)
- [x] `dotnet build FreelovesCookBot.sln` — 0 errors, 4 pre-existing warnings (EF1002 from test file)

---
*Phase: 10-qol-polish-consumer-surfaces*
*Completed: 2026-05-17*
