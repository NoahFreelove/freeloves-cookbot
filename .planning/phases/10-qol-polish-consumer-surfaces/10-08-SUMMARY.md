---
phase: 10-qol-polish-consumer-surfaces
plan: "08"
subsystem: ui
tags: [top-bar, slot-service, scoped, navigation-manager, idisposable, blazor, xunit, tdd]

# Dependency graph
requires:
  - phase: 10-qol-polish-consumer-surfaces
    provides: "Plan 10-02 (TopBar parameter [Parameter] RenderFragment? RightSlot already on TopBar.razor)"

provides:
  - "ICbTopBarService: public interface with RightSlot getter, OnChanged event, SetRightSlot and Clear"
  - "CbTopBarService: internal sealed scoped service, NavigationManager subscription + IDisposable"
  - "Program.cs: AddScoped<ICbTopBarService, CbTopBarService>() registration"
  - "CbTopBarServiceTests: four Fact tests covering all behavioral contracts"

affects:
  - 10-09  # MainLayout + RecipeView + RecipeEditor slot adoption

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "First Web-layer scoped service to subscribe to NavigationManager.LocationChanged in its constructor"
    - "IDisposable required for NavigationManager event unsubscription — prevents memory leaks on circuit disposal"
    - "Idempotent Clear() guard — no OnChanged event if RightSlot is already null"
    - "TDD RED/GREEN cycle with hand-rolled TestNavigationManager adapter for .NET 10 API shape"

key-files:
  created:
    - src/CookBot.Web/Services/ICbTopBarService.cs
    - src/CookBot.Web/Services/CbTopBarService.cs
    - tests/CookBot.Tests/Services/CbTopBarServiceTests.cs
  modified:
    - src/CookBot.Web/Program.cs

key-decisions:
  - "D-56: ICbTopBarService scoped service over CascadingValue/CascadingParameter — ROADMAP success criteria 4 literal compliance; event-driven for future LeftSlot expansion"
  - "D-57: auto-clear RightSlot on every NavigationManager.LocationChanged — no per-page boilerplate teardown required"
  - "W-06 adaptation: .NET 10.0.1 NotifyLocationChanged is single-bool signature; TestNavigationManager exposes a two-step wrapper (set Uri then notify) that preserves isInternalNavigation named-argument call sites"

patterns-established:
  - "NavigationManager subscription in constructor + IDisposable unsubscription: CbTopBarService is the canonical reference for any future Web-layer service needing lifecycle-tied navigation event handling"

requirements-completed: [POLISH-04]

# Metrics
duration: 15min
completed: 2026-05-17
---

# Phase 10 Plan 08: CbTopBarService Summary

**Scoped ICbTopBarService with NavigationManager auto-clear and IDisposable contract, backed by four TDD tests and registered in Program.cs**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-05-17T03:16:00Z
- **Completed:** 2026-05-17T03:31:56Z
- **Tasks:** 2 (TDD RED + GREEN)
- **Files modified:** 4 (3 created, 1 modified)

## Accomplishments

- `ICbTopBarService` interface delivered with all four members (RightSlot getter, OnChanged event, SetRightSlot, Clear)
- `CbTopBarService` implementation: constructor subscribes to `NavigationManager.LocationChanged`, auto-clears on every navigation (D-57), idempotent `Clear()` guard suppresses spurious events, `IDisposable` tears down the subscription preventing memory leaks (T-10-08-01)
- DI registration in `Program.cs` as Scoped (one per SignalR circuit)
- All four behavioral contracts verified by TDD tests: SetRightSlot raises OnChanged, LocationChanged auto-clears, Clear is idempotent on null state, Dispose unsubscribes from NavigationManager

## Task Commits

Each task was committed atomically:

1. **Task 1: Write CbTopBarServiceTests (RED)** - `5537189` (test)
2. **Task 2: Implement ICbTopBarService + CbTopBarService (GREEN) + register in Program.cs** - `bbe894e` (feat)

_TDD plan: test commit (RED gate) → feat commit (GREEN gate)_

## Files Created/Modified

- `src/CookBot.Web/Services/ICbTopBarService.cs` - Public interface with RightSlot/OnChanged/SetRightSlot/Clear (D-56, POLISH-04)
- `src/CookBot.Web/Services/CbTopBarService.cs` - Internal sealed implementation; NavigationManager subscription in ctor; IDisposable unsubscription; idempotent Clear(); auto-clear on LocationChanged (D-57)
- `src/CookBot.Web/Program.cs` - AddScoped<ICbTopBarService, CbTopBarService>() after ICbDialogService registration
- `tests/CookBot.Tests/Services/CbTopBarServiceTests.cs` - Four Fact tests with hand-rolled TestNavigationManager adapter

## Decisions Made

- **D-56** (per plan): ICbTopBarService scoped service over CascadingValue — honors ROADMAP success criteria 4 literal text
- **D-57** (per plan): Auto-clear on NavigationManager.LocationChanged — no per-page teardown boilerplate; pages re-set in OnInitializedAsync
- **W-06 adaptation** (deviation): .NET 10.0.1 ships `NotifyLocationChanged(bool isInternalNavigation)` with one parameter (URI comes from `NavigationManager.Uri` property); the plan's two-parameter override `(string uri, bool isInternalNavigation)` does not match the base signature. TestNavigationManager was adapted: it sets `Uri` directly then calls `base.NotifyLocationChanged(isInternalNavigation)`. Named-argument `isInternalNavigation:` is preserved at call sites per W-06 intent.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Adapted TestNavigationManager to actual .NET 10.0.1 NotifyLocationChanged signature**
- **Found during:** Task 1 (RED — initial build attempt)
- **Issue:** Plan specified `base.NotifyLocationChanged(string uri, bool isInternalNavigation)` (two parameters). The actual .NET 10.0.1 `NavigationManager` exposes `protected void NotifyLocationChanged(bool isInternalNavigation)` — one parameter; the URI is read from the `NavigationManager.Uri` property.
- **Fix:** `TestNavigationManager.NotifyLocationChanged(string uri, bool isInternalNavigation)` sets `Uri = uri` before calling `base.NotifyLocationChanged(isInternalNavigation)`. The public wrapper signature and named-argument call sites are preserved as specified.
- **Files modified:** `tests/CookBot.Tests/Services/CbTopBarServiceTests.cs`
- **Verification:** Build succeeds; all four tests pass GREEN; `grep "bool isInternalNavigation"` returns hits in test file
- **Committed in:** `5537189` (Task 1 RED commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 — API shape mismatch with actual runtime)
**Impact on plan:** Fix is strictly conformant — preserves all behavioral contracts and the W-06 named-argument intent. No scope change.

## Issues Encountered

None beyond the W-06 API shape deviation documented above.

## User Setup Required

None — no external service configuration required. DI registration is code-only.

## Next Phase Readiness

- `ICbTopBarService` is registered and ready for Plan 10-09 (MainLayout + RecipeView + RecipeEditor slot adoption)
- The service is the first to use NavigationManager subscription in a constructor — establishes the pattern for any future slot services (LeftSlot, CenterSlot) if needed in v1.4+
- All four behavioral tests pass; IDisposable contract is explicitly gated by test 4

## Known Stubs

None — this plan delivers a complete, production-ready service with no stub patterns.

## Threat Surface Scan

No new threat surface beyond T-10-08-01 (already in plan's threat model, mitigated by IDisposable). No new network endpoints, auth paths, or schema changes introduced.

## Self-Check: PASSED

- [x] `src/CookBot.Web/Services/ICbTopBarService.cs` — exists
- [x] `src/CookBot.Web/Services/CbTopBarService.cs` — exists
- [x] `tests/CookBot.Tests/Services/CbTopBarServiceTests.cs` — exists
- [x] Commit `5537189` — exists (RED: test file)
- [x] Commit `bbe894e` — exists (GREEN: implementation + registration)
- [x] `dotnet test --filter CbTopBarServiceTests` — 4/4 passed

---
*Phase: 10-qol-polish-consumer-surfaces*
*Completed: 2026-05-17*
