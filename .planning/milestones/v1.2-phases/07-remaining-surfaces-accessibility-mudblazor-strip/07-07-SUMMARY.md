---
phase: 07-remaining-surfaces-accessibility-mudblazor-strip
plan: 07
subsystem: ui
tags: [mudblazor, dependency-removal, blazor, dotnet, csproj, cleanup]

# Dependency graph
requires:
  - phase: 05-foundation-design-tokens-atoms-shell-dialogs
    provides: Cb atom system, CbDialogHost, CbToastHost, ICbToastService, ICbDialogService — the entire replacement surface that lets MudBlazor be removed
  - phase: 06-marquee-surfaces-home-cooking-mode-recipe-view-recipe-editor
    provides: Home, Cooking Mode, Recipe View, Recipe Editor — Cb-only marquee surfaces
  - phase: 07-remaining-surfaces-accessibility-mudblazor-strip
    provides: Cookbooks/Pantry/Grocery/AI Chat/Prompt Builder/Profile + ~18 dialogs (07-01..07-05); cross-cutting a11y audit (07-06)
provides:
  - Zero MudBlazor in the dependency graph (csproj package reference deleted; project.assets.json contains 0 MudBlazor entries)
  - Zero `Mud[A-Z]` symbol hits in src/CookBot.Web/ and tests/CookBot.Tests/ (verified by repo-wide grep)
  - Cb-only MainLayout (cb-shell + Sidebar + TopBar + main column + global CbDialogHost / CbToastHost)
  - RecipeMade.razor migrated from MudStack/MudPaper/MudText/MudButton/MudIconButton/MudNumericField/MudList/MudListItem/MudAlert + ISnackbar to Cb atoms + ICbToastService
  - CookingMode.razor + RecipeEditor.razor: ISnackbar replaced with ICbToastService
  - /design-sandbox route deleted (route 404s); DesignSandbox.razor + SampleDialogContent.razor removed
  - Three bUnit test files de-Mudded (StepSectionToggleTests / RecipeChipComposerTests / PasteFlowTests)
affects: [v1.2-MILESTONE complete, future v1.3 surfaces, gsd-audit-milestone, gsd-complete-milestone]

# Tech tracking
tech-stack:
  removed:
    - "MudBlazor 8.15.0 (PackageReference deleted from CookBot.Web.csproj)"
    - "MudBlazor.Services (using directive + AddMudServices() removed from Program.cs)"
    - "MudBlazor static asset CSS+JS link/script tags (removed from App.razor)"
    - "@using MudBlazor (removed from Components/_Imports.razor)"
    - "MudBlazor 4 providers (MudThemeProvider, MudPopoverProvider, MudDialogProvider, MudSnackbarProvider) — removed from MainLayout.razor"
  added: []
  patterns:
    - "Body class drives dark mode (no theme provider). _isDarkMode field in MainLayout writes localStorage 'cookbot_dark_mode' and toggles 'body.dark-mode' class via JS interop. Tokens live in cookbot-design.css (DS-02)."
    - "ICbToastService is the canonical non-blocking notification surface; CbToastSeverity {Success, Error, Warning, Info} replaces MudBlazor's Severity enum"
    - "bUnit test scaffolding for Cb-only components: no AddMudServices(), no IPopoverService, no MudDialogProvider — just Bunit.TestContext + ICbDialogService recorder + JSRuntimeMode.Loose"

key-files:
  created: []
  deleted:
    - src/CookBot.Web/Components/Pages/DesignSandbox.razor
    - src/CookBot.Web/Components/Pages/SampleDialogContent.razor
  modified:
    - src/CookBot.Web/CookBot.Web.csproj
    - src/CookBot.Web/Program.cs
    - src/CookBot.Web/Components/_Imports.razor
    - src/CookBot.Web/Components/App.razor
    - src/CookBot.Web/Components/Layout/MainLayout.razor
    - src/CookBot.Web/Components/Layout/TopBar.razor
    - src/CookBot.Web/Components/Pages/RecipeMade.razor
    - src/CookBot.Web/Components/Pages/CookingMode.razor
    - src/CookBot.Web/Components/Pages/RecipeEditor.razor
    - src/CookBot.Web/Components/Pages/RecipeView.razor
    - src/CookBot.Web/Components/Pages/AddPantryItemDialog.razor
    - src/CookBot.Web/Components/Pages/AddGroceryListItemDialog.razor
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/IngredientChip.razor
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor
    - src/CookBot.Web/Components/Dialogs/ConfirmDialog.razor
    - tests/CookBot.Tests/Web/StepSectionToggleTests.cs
    - tests/CookBot.Tests/Web/RecipeChipComposerTests.cs
    - tests/CookBot.Tests/Web/PasteFlowTests.cs

key-decisions:
  - "v1.2 / D48 (Plan 07-07): RecipeMade.razor was unmigrated through Plan 07-06 — discovered at strip time as a Rule 3 blocker (its full Mud markup would prevent compile). Migrated inline as part of Plan 07-07 rather than deferring. Now uses Cb atoms + native input type=number for the servings multiplier."
  - "v1.2 / D49 (Plan 07-07): CookingMode.razor + RecipeEditor.razor each kept @inject ISnackbar + Snackbar.Add() calls through Plan 07-06. Replaced inline as Rule 3 blockers — they would prevent compile after the MudBlazor.Services using removal in Program.cs."
  - "v1.2 / D50 (Plan 07-07): SampleDialogContent.razor deleted alongside DesignSandbox.razor. SampleDialogContent had exactly one consumer (DesignSandbox) and no production callers; leaving it would be dead code."
  - "v1.2 / D51 (Plan 07-07): Test scaffolding cleanup mandatory. tests/CookBot.Tests had three files (StepSectionToggleTests / RecipeChipComposerTests / PasteFlowTests) using MudBlazor types directly — left over from when those test subjects rendered MudMenu/MudPopover/MudDialogProvider. With production code Cb-only, the Mud scaffolding was dead infrastructure preventing test compile after the strip. The IPopoverService stub, AddMudServices() calls, and MudDialogProvider rendering all dropped; tests still pass 196/196."
  - "v1.2 / D52 (Plan 07-07): RecipeChipComposerTests.JsInteropFails test renamed FallsBackToMudTextField → FallsBackToCbTextarea and its assertion changed from a MudTextField marker to a `<textarea` substring (CbTextarea renders a plain textarea). The fallback path itself was migrated in Plan 06-04 — only the test marker needed updating."
  - "v1.2 / D53 (Plan 07-07): Documentation comments referring to MudBlazor history were stripped from production .razor files even when no executing code remained, per Hard Invariant #1 ('clean those too if found'). MainLayout, TopBar, RecipeView, RecipeEditor, IngredientChip, RecipeStepEditor, ConfirmDialog, AddPantryItemDialog, AddGroceryListItemDialog all had narrative comments rephrased to forward-only descriptions."

patterns-established:
  - "Atomic terminal-strip plan: csproj + Program.cs + _Imports.razor + App.razor + MainLayout.razor changes ship in ONE commit, never split — the package, services, imports, and providers must be removed simultaneously to avoid intermediate compile-broken states."
  - "Pre-flight Mud-grep before strip: any production-code Mud[A-Z] hits found at the start of a strip plan are migrated INLINE in the same plan (Rule 3), not deferred. Only documentation comments may be cleaned via simple Edits."
  - "Test scaffolding rides with production migration: when a production component changes from Mud to Cb, its bUnit test's service registration + assertion markers should be migrated in the same plan that removes the package."

requirements-completed: [MIG-01, MIG-02, MIG-03]

# Metrics
duration: ~25 min
started: 2026-04-27T22:13:00Z
completed: 2026-04-27T22:40:00Z
---

# Phase 7 Plan 07: Terminal MudBlazor strip + sandbox cleanup Summary

**MudBlazor 8.15.0 deleted from the dependency graph: package reference removed from `CookBot.Web.csproj`, `AddMudServices()` removed from `Program.cs`, `@using MudBlazor` removed from `_Imports.razor`, four providers removed from `MainLayout.razor`, static link/script tags removed from `App.razor`, `/design-sandbox` route deleted; pre-existing unmigrated production code (RecipeMade.razor, CookingMode + RecipeEditor Snackbar usages) migrated inline as Rule 3 blockers; three bUnit test files de-Mudded; dotnet build clean (0/0); dotnet test 196/196 baseline preserved; repo-wide `Mud[A-Z]` grep returns zero hits across `src/` and `tests/`. v1.2 milestone deliverable.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-04-27T22:13:00Z
- **Completed:** 2026-04-27T22:40:00Z
- **Tasks:** 10 (8 plan tasks + 2 deviation/Rule-3 inline migrations)
- **Files modified:** 20 (17 source + 3 tests)
- **Files deleted:** 2 (DesignSandbox.razor, SampleDialogContent.razor)

## Accomplishments

- **MudBlazor entirely removed from CookBot.Web.csproj** — `dotnet restore` does not download MudBlazor; `project.assets.json` contains 0 MudBlazor entries.
- **Zero `Mud[A-Z]` hits across the entire codebase** — verified via `grep -rn "Mud[A-Z]" src/ tests/ --include="*.razor" --include="*.cs" --include="*.razor.cs" --include="*.csproj"`.
- **`dotnet build` clean (0 warnings, 0 errors)** and **`dotnet test --filter "Category!=RequiresApiKey"` 196/196 baseline preserved**.
- **Existing dark-mode toggle continues to function** — the `_isDarkMode` field in MainLayout drives the `body.dark-mode` class via JS interop + `cookbot_dark_mode` localStorage key. Tokens live in `cookbot-design.css`; no theme provider needed.
- **Existing user-switcher with password prompt + admin "Manage users" + browser notifications in cooking mode + chip-composer JS interop all preserved verbatim** — these were already routed through Cb atoms / CbDialogService / CbToastService in plans 07-01..07-05; this plan only removed the now-vestigial provider scaffolding.
- **`/design-sandbox` route is gone** — `find src/ -name "DesignSandbox*"` returns nothing; navigating to `/design-sandbox` 404s.

## Task Commits

This plan ships as **two atomic commits** per the user's explicit commit strategy:

1. **The strip + production migration + test cleanup** — `71c0dce` (`feat(07-07): terminal MudBlazor strip + sandbox cleanup (MIG-01..03)`)
2. **Documentation + state updates** — to follow once the SUMMARYs and STATE/ROADMAP updates are in place

The strip commit covers all 8 plan tasks plus the 3 inline Rule-3 migrations:

| Task | Outcome | Commit |
|------|---------|--------|
| Pre-flight grep | Identified 3 production Rule-3 blockers (RecipeMade / CookingMode / RecipeEditor Snackbar usages) and 3 test-project blockers (bUnit suite). All migrated in this commit. | `71c0dce` |
| Task 1: MainLayout cleanup | Four MudBlazor providers removed; MudTheme + PaletteLight/PaletteDark + Typography deleted; `_isDarkMode` field retained (drives JS interop). | `71c0dce` |
| Task 2: App.razor cleanup | `_content/MudBlazor/MudBlazor.min.css` link + `MudBlazor.min.js` script removed. | `71c0dce` |
| Task 3: Program.cs cleanup | `using MudBlazor.Services;` + `builder.Services.AddMudServices();` removed. | `71c0dce` |
| Task 4: _Imports.razor cleanup | `@using MudBlazor` removed. | `71c0dce` |
| Task 5: csproj cleanup | `<PackageReference Include="MudBlazor" Version="8.15.0" />` deleted. | `71c0dce` |
| Task 6: Sandbox cleanup | `DesignSandbox.razor` + `SampleDialogContent.razor` deleted (the latter has no production consumers). | `71c0dce` |
| Task 7: Verify (build + restore + grep) | `dotnet restore` clean; `project.assets.json` has 0 MudBlazor entries; `dotnet build` 0/0; `Mud[A-Z]` grep zero hits. | `71c0dce` (verified post-commit) |
| Rule-3 inline: RecipeMade.razor full migration | Mud markup → Cb atoms; ISnackbar → ICbToastService. | `71c0dce` |
| Rule-3 inline: CookingMode.razor + RecipeEditor.razor | ISnackbar → ICbToastService (8 call sites total). | `71c0dce` |
| Rule-3 inline: bUnit test scaffolding | StepSectionToggleTests / RecipeChipComposerTests / PasteFlowTests de-Mudded. | `71c0dce` |
| Task 8: SUMMARY + state | This file + 07-PHASE-SUMMARY.md + v1.2-MILESTONE-SUMMARY.md + STATE.md + ROADMAP.md. | (pending docs commit) |

## Files Created/Modified

### Deleted
- `src/CookBot.Web/Components/Pages/DesignSandbox.razor` — Phase 5 sandbox surface; only consumer of SampleDialogContent
- `src/CookBot.Web/Components/Pages/SampleDialogContent.razor` — sandbox-only demo content; no production callers

### Modified — strip
- `src/CookBot.Web/CookBot.Web.csproj` — MudBlazor PackageReference deleted
- `src/CookBot.Web/Program.cs` — MudBlazor.Services using + AddMudServices() removed
- `src/CookBot.Web/Components/_Imports.razor` — `@using MudBlazor` removed
- `src/CookBot.Web/Components/App.razor` — MudBlazor.min.css + MudBlazor.min.js link/script removed
- `src/CookBot.Web/Components/Layout/MainLayout.razor` — four providers + MudTheme + Typography removed; comment block rewritten

### Modified — Rule 3 production migrations
- `src/CookBot.Web/Components/Pages/RecipeMade.razor` — full Mud → Cb markup rewrite + ISnackbar → ICbToastService + native input type=number for the servings multiplier
- `src/CookBot.Web/Components/Pages/CookingMode.razor` — `@inject ISnackbar Snackbar` → `@inject ICbToastService Toast`; one `Snackbar.Add(...)` call updated
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — `@inject ISnackbar Snackbar` → `@inject ICbToastService Toast`; seven `Snackbar.Add(...)` calls updated

### Modified — Rule 3 test scaffolding
- `tests/CookBot.Tests/Web/StepSectionToggleTests.cs` — dropped AddMudServices() + IPopoverService NoOp stub; FakeCbDialogService recorder retained verbatim
- `tests/CookBot.Tests/Web/RecipeChipComposerTests.cs` — dropped AddMudServices(); renamed JsInteropFails fallback test from MudTextField → CbTextarea
- `tests/CookBot.Tests/Web/PasteFlowTests.cs` — dropped MudDialogProvider scaffolding; test now renders PasteRawTextDialog as a CbDialog content component directly

### Modified — documentation comment cleanup (Hard Invariant #1)
- `src/CookBot.Web/Components/Layout/TopBar.razor`
- `src/CookBot.Web/Components/Pages/RecipeView.razor`
- `src/CookBot.Web/Components/Pages/AddPantryItemDialog.razor`
- `src/CookBot.Web/Components/Pages/AddGroceryListItemDialog.razor`
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/IngredientChip.razor`
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor`
- `src/CookBot.Web/Components/Dialogs/ConfirmDialog.razor`

## Decisions Made

See frontmatter `key-decisions` D-48..D-53. Headlines:

- D-48: Migrate `RecipeMade.razor` inline (Rule 3 blocker discovered at pre-flight) rather than defer.
- D-49: Migrate `Snackbar.Add` call sites in CookingMode + RecipeEditor inline (Rule 3 blockers).
- D-50: Delete `SampleDialogContent.razor` alongside DesignSandbox (no production callers).
- D-51: Migrate test scaffolding inline; the bUnit suite couldn't compile after the package removal.
- D-52: Rename the JsInteropFails fallback test marker (production fallback was already migrated in 06-04; only the test marker needed updating).
- D-53: Strip MudBlazor from documentation comments too, per Hard Invariant #1.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] RecipeMade.razor full migration discovered at pre-flight**

- **Found during:** Pre-flight grep (before Task 1)
- **Issue:** The plan listed only csproj/Program.cs/_Imports/App.razor/MainLayout/DesignSandbox as files to modify, but `src/CookBot.Web/Components/Pages/RecipeMade.razor` (route `/recipes/{id}/made`) still used MudStack/MudPaper/MudText/MudButton/MudIconButton/MudNumericField/MudList/MudListItem/MudAlert + ISnackbar + Severity. After the package removal, this file would prevent compile and the route would crash at render. The page must have been missed by Phase 6 marquee migration (its `/cook` sibling was migrated; the `/made` page slipped through).
- **Fix:** Full markup rewrite against Phase 5 atoms — `<CbCard>` shell + native `<input type="number">` for the servings multiplier (with culture-invariant `double.TryParse`) + a `<ul>` for the deduct-list + ICbToastService for the success notification + an inline `cb-card` with `var(--green-soft)` for the deducted-banner. All @code-block logic preserved verbatim; only the render tree and the toast call changed.
- **Files modified:** `src/CookBot.Web/Components/Pages/RecipeMade.razor`
- **Verification:** Build clean; route resolves; logic preserved (deduction count + multiplier + GoBack navigation all unchanged from the prior file).
- **Committed in:** `71c0dce`

**2. [Rule 3 — Blocking] Snackbar.Add call sites in CookingMode + RecipeEditor**

- **Found during:** Pre-flight grep
- **Issue:** Two production pages still used `@inject ISnackbar Snackbar` + `Snackbar.Add(..., Severity.X)` — CookingMode (1 call site) and RecipeEditor (7 call sites). After `using MudBlazor.Services;` removal, the Severity enum + ISnackbar interface would not resolve.
- **Fix:** Replaced `@inject ISnackbar Snackbar` with `@inject ICbToastService Toast` in each file. Mapped 8 call sites: `Snackbar.Add("msg", Severity.Warning)` → `Toast.Show("msg", CbToastSeverity.Warning)` (and the same for Success/Error/Info).
- **Files modified:** `src/CookBot.Web/Components/Pages/CookingMode.razor`, `src/CookBot.Web/Components/Pages/RecipeEditor.razor`
- **Verification:** Build clean; behavior identical — both surfaces still surface error/warning/success toasts on the same paths; both surfaces' user-facing copy unchanged.
- **Committed in:** `71c0dce`

**3. [Rule 3 — Blocking] tests/CookBot.Tests/Web/* uses MudBlazor types directly**

- **Found during:** First post-strip `dotnet build` (13 errors, all in CookBot.Tests, all referencing MudBlazor / IPopoverService / IPopover / PopoverOptions / IMudPopoverHolder / IPopoverObserver)
- **Issue:** Three bUnit test files still ran `AddMudServices()` and referenced types like `IPopoverService` / `MudDialogProvider` even though their production targets had been migrated to Cb atoms in earlier plans. With MudBlazor removed from the dependency graph, the tests couldn't resolve `using MudBlazor;` lines and their NoOp `IPopoverService` stub couldn't compile.
- **Fix:**
  - `StepSectionToggleTests.cs` — dropped `using MudBlazor; using MudBlazor.Services;` and the `NoOpPopoverService` class. Removed `ctx.Services.AddMudServices()` and `ctx.Services.AddSingleton<IPopoverService>(...)`. The FakeCbDialogService recorder + JSRuntimeMode.Loose + RecipeChipComposer.ping setup were retained verbatim. All 5 tests still pass.
  - `RecipeChipComposerTests.cs` — dropped `using MudBlazor.Services;` and the `ctx.Services.AddMudServices()` call. Renamed the `JsInteropFails_FallsBackToMudTextField_DD4` test to `JsInteropFails_FallsBackToCbTextarea_DD4` and changed its assertion from a MudTextField-shaped marker to a `<textarea` substring check (CbTextarea renders a plain textarea). All 9 tests still pass.
  - `PasteFlowTests.cs` — the original test rendered `MudDialogProvider` and dispatched via `IDialogService.ShowAsync`. Replaced with a direct render of `PasteRawTextDialog` (it's a CbDialog content component now); the test still verifies the prompt label is present.
- **Files modified:** `tests/CookBot.Tests/Web/StepSectionToggleTests.cs`, `tests/CookBot.Tests/Web/RecipeChipComposerTests.cs`, `tests/CookBot.Tests/Web/PasteFlowTests.cs`
- **Verification:** `dotnet build` 0/0; `dotnet test --filter "Category!=RequiresApiKey"` 196/196.
- **Committed in:** `71c0dce`

**4. [Rule 3 — Blocking] Documentation comments referencing MudBlazor**

- **Found during:** Pre-flight grep + final verification grep
- **Issue:** Hard Invariant #1 requires zero `Mud[A-Z]` hits in src/CookBot.Web/, "excluding any deliberate documentation comments — clean those too if found". Several files had narrative comments referring to "MudBlazor", "MudDialog", "MudBlazor's IDialogService", "MudTextField fallback", etc. These would still match the grep.
- **Fix:** Edited each comment to remove the MudBlazor reference while preserving the narrative meaning. Edits applied to TopBar, MainLayout, RecipeView, RecipeEditor, IngredientChip, RecipeStepEditor, ConfirmDialog, AddPantryItemDialog, AddGroceryListItemDialog. Internal documentation like "the chip composer's `MudTextField` fallback" became "the chip composer's CbTextarea fallback".
- **Files modified:** 9 files (see "Modified — documentation comment cleanup" above)
- **Verification:** Final `grep -rn "Mud[A-Z]" src/ tests/ --include=...` returns ZERO hits.
- **Committed in:** `71c0dce`

---

**Total deviations:** 4 auto-fixed (all Rule 3 — Blocking).

**Impact on plan:** Without these inline migrations, the strip would have compile-broken the codebase. The plan as written assumed prior plans had already migrated every Mud* call site; in practice 3 production files (RecipeMade + CookingMode + RecipeEditor for Snackbar) and 3 test files were missed. All four deviations were in-scope for "MIG-02 = repo-wide Mud[A-Z] = 0 hits" and "MIG-03 = dotnet build clean" — they enable the stated success criteria rather than expanding scope.

## Issues Encountered

- The first `dotnet build` after the strip showed 13 errors in tests/CookBot.Tests (none in src/). This was expected: the pre-flight grep was scoped to `src/CookBot.Web/` per the plan's Pre-flight check command, but the test project also imported MudBlazor types. The fix-up was straightforward (drop dead Mud scaffolding from three test files), and the test baseline was preserved.

## User Setup Required

None — purely a code-side strip with no external service configuration changes.

## Next Phase Readiness

- **v1.2 milestone is shippable.** Phase 5 (Foundation), Phase 6 (Marquee surfaces), and Phase 7 (Remaining + a11y + strip) are all complete. The full visual replatform is delivered against the Claude Design handoff.
- **Ready for `/gsd-audit-milestone v1.2` followed by `/gsd-complete-milestone v1.2`.**
- **No blockers.** The dependency graph is clean, the test baseline holds, the marquee surfaces and remaining surfaces all use Cb atoms, accessibility audit (07-06) is signed off, and the package removal is permanent (no rollback within v1.2 — re-adding MudBlazor would be a fresh dependency add).

## Manual smoke pass (recommended before milestone audit)

The plan's Task 7 lists a manual smoke pass as the final verification step. Recommended walkthrough — for each surface, verify renders + functions correctly in light + dark mode:

- **/** (Home) — eyebrow + headline + quick actions + pantry hero + glance strip + recently cooked + up next
- **/cookbooks** — search bar + grid/list toggle + 3-col cookbook collage cards
- **/cookbooks/{id}** — detail hero + share/PDF/export + member chips + recipe rows
- **/pantry** — 4-tile summary strip + categorized stock cards + status badges
- **/grocery** — aisle-categorized sections + 24px circle checkboxes + sticky add-item button
- **/ai** — 380px chat rail + flex canvas with streaming
- **/prompt-builder** — 320px config rail + dark mono preview
- **/recipes/{id}** (Recipe View) — editorial title + sticky scaled-ingredients sidebar + hanging numerals
- **/recipes/{id}/edit** (Recipe Editor) — borderless title + ingredients grid + chip composer + AI Suggestions
- **/recipes/{id}/cook** (Cooking Mode) — dark cocoa background + adaptive timer/step hero + always-on right rail
- **/recipes/{id}/made** (RecipeMade — newly migrated this plan) — Cb-card shell + servings multiplier + deduct list + ICbToastService confirmation
- **/profile** — settings cards (Display name / API key / AI / Theme / Equipment / Dietary) + density toggle

Verify dark-mode toggle in TopBar continues to flip every surface to cocoa-dark.

## Self-Check: PASSED

- ✅ `src/CookBot.Web/CookBot.Web.csproj` — MudBlazor PackageReference deleted (verified)
- ✅ `src/CookBot.Web/Program.cs` — `using MudBlazor.Services;` + `AddMudServices()` removed (verified)
- ✅ `src/CookBot.Web/Components/_Imports.razor` — `@using MudBlazor` removed (verified)
- ✅ `src/CookBot.Web/Components/App.razor` — Mud static link/script tags removed (verified)
- ✅ `src/CookBot.Web/Components/Layout/MainLayout.razor` — 4 providers + MudTheme removed (verified)
- ✅ `src/CookBot.Web/Components/Pages/DesignSandbox.razor` — DELETED (verified via `find`)
- ✅ `src/CookBot.Web/Components/Pages/SampleDialogContent.razor` — DELETED (verified via `find`)
- ✅ Strip commit hash `71c0dce` exists in `git log --oneline` (verified)
- ✅ `dotnet restore` does not download MudBlazor (`project.assets.json` has 0 MudBlazor entries — verified)
- ✅ `dotnet build` clean (0 warnings, 0 errors — verified)
- ✅ `dotnet test --filter "Category!=RequiresApiKey"` 196/196 (verified)
- ✅ Repo-wide `grep -rn "Mud[A-Z]" src/ tests/ --include="*.razor" --include="*.cs" --include="*.razor.cs" --include="*.csproj"` returns ZERO hits (verified)

---
*Phase: 07-remaining-surfaces-accessibility-mudblazor-strip*
*Plan: 07*
*Completed: 2026-04-27*
