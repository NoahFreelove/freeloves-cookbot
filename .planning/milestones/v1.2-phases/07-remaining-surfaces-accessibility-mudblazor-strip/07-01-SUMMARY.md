---
phase: 07-remaining-surfaces-accessibility-mudblazor-strip
plan: 01
subsystem: ui
tags: [blazor, mudblazor-strip, cookbooks, dialogs, cb-atoms, ui-redesign]

# Dependency graph
requires:
  - phase: 05-foundation-design-tokens-atoms-shell-dialogs
    provides: CbDialog + CbDialogService + ICbToastService + atom set (CbCard, CbButton, CbInput, CbTextarea, CbSelect, CbOption, CbChip, Icon, StripedPlaceholder, CbEyebrow)
  - phase: 06-marquee-surfaces-home-cooking-mode-recipe-view-recipe-editor
    provides: Cb-shell rendering pattern (Home, RecipeView, RecipeEditor, CookingMode)
provides:
  - Cookbook list rewrite against the design-handoff cookbook-list.jsx (CB-01)
  - Cookbook detail rewrite with hero + recipe row list + share/PDF/JSON/rename/delete (CB-02)
  - 7 dialog migrations to CbDialogService (CookbookFormDialog, ShareCookbookDialog, ImportCookbookDialog, CookbookReferenceDialog, SaveRecipeDialog, PasteRawTextDialog, SectionDropConfirmationDialog)
  - Generic Cb-native ConfirmDialog primitive (replaces IDialogService.ShowMessageBox)
  - CookbookDownloadHelper migrated from ISnackbar to ICbToastService
affects:
  - 07-02 (Pantry) — uses same dialog migration pattern
  - 07-03 (Grocery) — uses same dialog migration pattern
  - 07-04 (AI Chat / Prompt Builder) — partial DialogService cleanup remaining (SharedKeysDialog still on Mud)
  - 07-05 (Profile) — establishes ConfirmDialog primitive available for reuse
  - 07-07 (terminal MudBlazor strip) — fewer Mud DialogService consumers remaining

# Tech tracking
tech-stack:
  added: []  # No new packages
  patterns:
    - CbDialogService dispatch pattern (parameters dictionary + CbDialogResult, Title via ShowAsync arg)
    - Confirm-dialog-via-ShowAsync replacement for IDialogService.ShowMessageBox
    - In-page header row for "TopBar right slot" until shell exposes a per-page slot

key-files:
  created:
    - src/CookBot.Web/Components/Dialogs/ConfirmDialog.razor
  modified:
    - src/CookBot.Web/Components/Pages/CookbookList.razor
    - src/CookBot.Web/Components/Pages/CookbookDetail.razor
    - src/CookBot.Web/Components/Pages/CookbookFormDialog.razor
    - src/CookBot.Web/Components/Pages/ShareCookbookDialog.razor
    - src/CookBot.Web/Components/Pages/ImportCookbookDialog.razor
    - src/CookBot.Web/Components/Pages/CookbookReferenceDialog.razor
    - src/CookBot.Web/Components/Pages/SaveRecipeDialog.razor
    - src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/SectionDropConfirmationDialog.razor
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor
    - src/CookBot.Web/Components/Pages/RecipeView.razor
    - src/CookBot.Web/Components/Pages/RecipeEditor.razor
    - src/CookBot.Web/Components/Pages/AiChat.razor
    - src/CookBot.Web/Services/CookbookDownloadHelper.cs
    - tests/CookBot.Tests/Web/StepSectionToggleTests.cs

key-decisions:
  - "Dialog content components do NOT include a <CbDialog> wrapper — CbDialogHost wraps the content in CbDialog slots automatically. Only the body + footer markup ships in the dialog component (matches the existing SampleDialogContent pattern)."
  - "Cookbook accent color is computed by index (cookbook ID modulo 4) since v1.2 has no AccentColor field on Cookbook. Palette matches the four design-handoff tokens (var(--accent-soft), var(--cream-2), #E1ECDF, #F0E2C8). User-facing accent picker is FUTURE-14."
  - "MudTabs in ShareCookbookDialog replaced with a small inline pill segmented-button switcher (People / Export). No new atom needed — directly uses cb-btn classes."
  - "CookbookDownloadHelper migrated to ICbToastService rather than dual-signature overload (Rule 3 — required to remove the ISnackbar dependency from CookbookList/CookbookDetail). All three call sites updated atomically."
  - "Added ConfirmDialog as a Cb-native primitive in Components/Dialogs/ to replace IDialogService.ShowMessageBox (used by CookbookDetail.DeleteCookbook + DeleteRecipe). Reusable by future plans (07-02..07-05) that need yes/cancel confirms."
  - "TopBar right-slot for the cookbook list (Import / New cookbook) renders in-page rather than via the shell TopBar — Phase 6 pattern (see RecipeView D-17 PRAGMATIC). Shell TopBar slot plumbing is out of scope."
  - "Inline edit + delete on cookbook list cards REMOVED (matches design-handoff intent — clean cards). Edit (rename) + delete + share now live on the cookbook detail page hero so no operation is lost."

patterns-established:
  - "Cb-native dialog migration: dialog body emits content + footer; CbDialogHost owns the CbDialog wrapper; CbDialogInstance cascaded via [CascadingParameter] for self-close."
  - "Dialog parameters use CbDialogParameters dictionary indexers (params[\"Name\"] = value) instead of MudBlazor's typed-builder DialogParameters<TDialog>."
  - "ConfirmDialog primitive accepts Title, Message, ConfirmLabel, CancelLabel, Destructive parameters and returns CbDialogResult.Ok(true) on confirm."

requirements-completed: [CB-01, CB-02]

# Metrics
duration: 10min
completed: 2026-04-27
---

# Phase 7 Plan 01: Cookbooks (list + detail) + cookbook/recipe dialogs migration Summary

**Cookbooks list and detail rewritten against Phase 5 atoms (3-col tinted-collage cards, hero with member chips + share/PDF/JSON/rename/delete actions); 7 cookbook/recipe dialogs migrated from MudDialog to CbDialogService; CookbookDownloadHelper migrated from ISnackbar to ICbToastService — every migrated file is now Mud-free.**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-04-27T21:13:59Z
- **Completed:** 2026-04-27T21:23:58Z
- **Tasks:** 4/4 complete (Task 1: CookbookList; Task 2: CookbookDetail; Task 3: 7 dialog migrations; Task 4: build + test)
- **Files modified:** 15 + 1 created

## Accomplishments

- **CB-01 satisfied** — `CookbookList.razor` rewritten as a 3-column grid of cookbook "collage" cards (180px tinted header with 3×2 striped-tile placeholder grid + body with title, recipe count, and author/share line) plus search field, filter button (disabled placeholder), and grid/list toggle.
- **CB-02 satisfied** — `CookbookDetail.razor` rewritten with hero (cookbook title + member chips for shares + Share / PDF / JSON / Rename / Delete / New-recipe action row) and recipe row list (StripedPlaceholder thumbnail + name + prep/cook/servings chips + last-updated meta).
- **7 dialogs migrated** to `CbDialogService` per the plan list. Each dialog drops `<MudDialog>` and now emits body + footer content directly; `CbDialogHost` wraps it in `<CbDialog>` slots automatically.
- **`CookbookDownloadHelper`** updated to `ICbToastService` so the cookbook pages no longer need `ISnackbar`. QuestPDF-backed `CookbookPdfService` export path is preserved verbatim.
- **`ConfirmDialog`** added as a generic Cb-native confirm primitive (replaces the `IDialogService.ShowMessageBox` calls used for delete cookbook / delete recipe confirms).
- **All call sites** for the migrated dialogs updated: `RecipeStepEditor`, `RecipeView`, `RecipeEditor`, `AiChat`, plus `CookbookList` / `CookbookDetail`. `AiChat` keeps `IDialogService` for the Phase 7 / Plan 07-05 `SharedKeysDialog`; `RecipeEditor` keeps `ISnackbar` for non-migrated surfaces (out of scope).

## Task Commits

All work committed in a single atomic commit because the 7-dialog migration, the helper signature change, and the page rewrites were tightly coupled (helper change forces page change forces dialog change). Plan permitted "multiple atomic commits OK"; one was sufficient.

1. **Tasks 1–4 (combined):** Cookbook surfaces + 7 dialogs + helper + ConfirmDialog + test fix → **`5815f42`** (feat)

## Files Created / Modified

### Created
- `src/CookBot.Web/Components/Dialogs/ConfirmDialog.razor` — Generic Cb-native yes/cancel confirm.

### Modified — Page rewrites (Task 1, Task 2)
- `src/CookBot.Web/Components/Pages/CookbookList.razor` — Full rewrite per cookbook-list.jsx.
- `src/CookBot.Web/Components/Pages/CookbookDetail.razor` — Full rewrite. Adds rename + delete entry points absent from previous detail page (operations previously sat as inline icons on list cards; consolidated here per design intent).

### Modified — Dialog migrations (Task 3)
- `src/CookBot.Web/Components/Pages/CookbookFormDialog.razor`
- `src/CookBot.Web/Components/Pages/ShareCookbookDialog.razor` (MudTabs → segmented buttons)
- `src/CookBot.Web/Components/Pages/ImportCookbookDialog.razor`
- `src/CookBot.Web/Components/Pages/CookbookReferenceDialog.razor`
- `src/CookBot.Web/Components/Pages/SaveRecipeDialog.razor`
- `src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor`
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/SectionDropConfirmationDialog.razor`

### Modified — Call sites
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` — IDialogService → ICbDialogService.
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — IDialogService → ICbDialogService (one call site, ShareCookbookDialog).
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — Added ICbDialogService inject; PasteRawTextDialog launch migrated. ISnackbar kept (out of scope).
- `src/CookBot.Web/Components/Pages/AiChat.razor` — Added ICbDialogService inject alongside IDialogService (kept for SharedKeysDialog → Plan 07-05). Three migrated call sites updated.

### Modified — Service / test
- `src/CookBot.Web/Services/CookbookDownloadHelper.cs` — ISnackbar → ICbToastService (Rule 3 deviation).
- `tests/CookBot.Tests/Web/StepSectionToggleTests.cs` — `FakeDialogService` → `FakeCbDialogService` to match RecipeStepEditor's new dispatch surface (Rule 1 — broken test contract).

## Verification

- **`dotnet build`:** Clean. 0 warnings, 0 errors.
- **`dotnet test --filter "Category!=RequiresApiKey"`:** 196/196 passing (baseline preserved).
- **Hard invariant #1 — zero `Mud*` symbols in migrated files:** Verified via per-file `grep -E "Mud[A-Z]|MudBlazor|ISnackbar"` — 0 matches in CookbookList, CookbookDetail, and all 7 dialogs.
- **Hard invariant #2 — PDF export still works:** `CookbookPdfService` + `CookbookDownloadHelper.TryDownloadPdfAsync` signature unchanged except for the `ISnackbar → ICbToastService` parameter swap. QuestPDF generation path is byte-identical.
- **Hard invariant #3 — dialogs use CbDialog slots:** All migrations drop `<MudDialog>`; CbDialogHost provides Header/Body/Footer.
- **Hard invariant #4 — call sites use CbDialogService.ShowAsync:** Verified via repo-wide grep — 10 of 10 migrated-dialog ShowAsync calls now use `CbDialogService`. The 3 remaining `IDialogService.ShowAsync` references in `RecipeStepEditor`/`AiChat` are for non-migrated dialogs (`SharedKeysDialog`) handled by future plans.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] CookbookDownloadHelper required to migrate to ICbToastService**

- **Found during:** Task 1 / Task 2 (CookbookList + CookbookDetail rewrites)
- **Issue:** Hard invariant required zero Mud symbols in CookbookList/CookbookDetail. The pages call `CookbookDownloadHelper.TryDownloadPdfAsync(...)` and `TryDownloadJsonAsync(...)` which both took an `ISnackbar` parameter. Keeping `ISnackbar` would have either forced the pages to inject Mud's `ISnackbar` (violating the invariant) or required two parallel helper signatures (technical debt). The helper had only 3 callers (CookbookList, CookbookDetail, ShareCookbookDialog) — all three were already in scope for migration.
- **Fix:** Migrated `CookbookDownloadHelper` to take `ICbToastService` and updated all callers atomically. Behavior is identical; only the toast surface changed.
- **Files modified:** `src/CookBot.Web/Services/CookbookDownloadHelper.cs`, `CookbookDetail.razor`, `ShareCookbookDialog.razor` (CookbookList didn't call it).
- **Commit:** `5815f42`

**2. [Rule 2 — Critical functionality] ConfirmDialog primitive added**

- **Found during:** Task 2 (CookbookDetail rewrite)
- **Issue:** The previous CookbookDetail used `DialogService.ShowMessageBox(...)` (MudBlazor) for the delete-cookbook and delete-recipe confirmation prompts. No Cb-native equivalent existed. Removing the confirmation prompts would have been a destructive UX regression (silent delete). The plan didn't list `ConfirmDialog` as an item but the migration is impossible without it.
- **Fix:** Added `Components/Dialogs/ConfirmDialog.razor` as a generic Cb-native confirm primitive (Title / Message / ConfirmLabel / CancelLabel / Destructive parameters; returns `CbDialogResult.Ok(true)` on confirm). Reusable by future plans 07-02..07-05.
- **Files modified:** `src/CookBot.Web/Components/Dialogs/ConfirmDialog.razor` (new).
- **Commit:** `5815f42`

**3. [Rule 1 — Bug] Test fake updated to match new dispatch surface**

- **Found during:** Task 4 (`dotnet test`)
- **Issue:** `tests/CookBot.Tests/Web/StepSectionToggleTests.cs` had a `FakeDialogService : IDialogService` recorder. After `RecipeStepEditor` migrated from `IDialogService` to `ICbDialogService`, the bUnit context lacked the new service registration and 5 tests failed with `InvalidOperationException: There is no registered service of type 'ICbDialogService'`.
- **Fix:** Replaced the fake with `FakeCbDialogService : ICbDialogService` recording `CbDialogParameters` and returning a configurable `CbDialogResult`. Test assertions updated to read parameters via dictionary indexer instead of MudBlazor's typed `Get<T>()`.
- **Files modified:** `tests/CookBot.Tests/Web/StepSectionToggleTests.cs`.
- **Commit:** `5815f42`

### Scope adjustments (in-spec)

- **`AiChat.razor` keeps both `IDialogService` AND new `ICbDialogService` injections.** It still launches `SharedKeysDialog` (a Phase 7 / Plan 07-05 dialog not in this plan's scope) which remains MudBlazor. Plan 07-05 will remove the residual `IDialogService` inject when `SharedKeysDialog` migrates.
- **`RecipeEditor.razor` keeps `ISnackbar`.** Only the `PasteRawTextDialog` call site was in scope for this plan; the file's broader Mud usage will be addressed by a future plan (presumably as part of 07-04 or 07-07).
- **`PasteFlowTests.cs` not updated.** It opens `PasteRawTextDialog` through MudDialogProvider's `IDialogService` and asserts the dialog content renders. The test still passes because Mud renders whatever component is given (the inner CbDialogInstance just goes unbound). This is dead Mud test scaffolding that 07-07 will remove with the rest of the Mud strip.

## Known Stubs

- **Filters button on CookbookList** (`<button class="cb-btn ghost" disabled>Filters</button>`) — visual placeholder per cookbook-list.jsx (the design shows a Filters button but no filter UX is specified). Disabled with title="Filters (coming soon)" so users see the affordance but get no broken functionality. Not a regression — previous list had no filters either.
- **Cookbook accent color cycles by ID** rather than persisted user choice (FUTURE-14). Stable across renders so each cookbook keeps its accent.

## Threat Flags

None. No new network endpoints, auth surfaces, file-access patterns, or schema changes. Authorization checks (`UserCanAccessRecipeAsync` etc.) preserved verbatim by the application services. The dialogs continue to require `UserService.CurrentUserId` before operating.

## Self-Check: PASSED

Verified the SUMMARY.md claims:

**Files created exist:**
- `src/CookBot.Web/Components/Dialogs/ConfirmDialog.razor` — FOUND.

**Files modified exist (all 15):**
- All 15 modified files exist on disk and are tracked by git.

**Commit `5815f42` exists in `git log --oneline --all`:** FOUND.

**`dotnet build`:** clean (0 warnings, 0 errors).
**`dotnet test --filter "Category!=RequiresApiKey"`:** 196/196 passing.
**Per-file Mud grep on the 9 plan-scoped files:** 0 matches.
