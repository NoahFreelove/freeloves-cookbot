---
phase: 7
plan: 5
title: Profile + user/sharing dialogs migration
subsystem: ui-redesign
tags: [PROF-01, PROF-02, MIG, atoms, dialogs]
requirements: [PROF-01, PROF-02]
requires: [05-04, 05-05, 07-01, 07-04]
provides: [profile-rewrite, last-idialog-consumer-removed]
affects:
  - src/CookBot.Web/Components/Pages/EditProfile.razor
  - src/CookBot.Web/Components/Layout/AddUserDialog.razor
  - src/CookBot.Web/Components/Layout/AdminManageUsersDialog.razor
  - src/CookBot.Web/Components/Layout/PasswordPromptDialog.razor
  - src/CookBot.Web/Components/Pages/SharedKeysDialog.razor
  - src/CookBot.Web/Components/Layout/TopBar.razor
  - src/CookBot.Web/Components/Layout/MainLayout.razor
  - src/CookBot.Web/wwwroot/js/cookbot-shell.js
tech-stack:
  added: []
  patterns:
    - cookbot.setDensity / cookbot.getDensity → localStorage-backed density preference (Phase 5 D-05 ships here)
    - Density toggle in Profile, persisted across sessions, applied via JS interop on layout init
key-files:
  created: []
  modified:
    - src/CookBot.Web/Components/Pages/EditProfile.razor (full rewrite, ~470 → ~480 lines)
    - src/CookBot.Web/Components/Layout/AddUserDialog.razor (full rewrite, 22 lines)
    - src/CookBot.Web/Components/Layout/AdminManageUsersDialog.razor (full rewrite, ~115 lines)
    - src/CookBot.Web/Components/Layout/PasswordPromptDialog.razor (full rewrite, ~50 lines)
    - src/CookBot.Web/Components/Pages/SharedKeysDialog.razor (full rewrite, ~210 lines)
    - src/CookBot.Web/Components/Layout/TopBar.razor (drops @inject IDialogService, switches to CbDialogService)
    - src/CookBot.Web/Components/Layout/MainLayout.razor (comment-only: documents Mud-providers-without-consumers state)
    - src/CookBot.Web/wwwroot/js/cookbot-shell.js (extends setDensity + applyDefaults with localStorage)
decisions:
  - v1.2 / D43 (Plan 07-05) — Density storage via localStorage instead of UserProfile field. UserProfile.cs has no Density column; adding one now would need a migration solely for a UI-pref toggle. localStorage scoped per-browser matches modern dark-mode patterns (cookbot_dark_mode is already localStorage-backed). cookbot.setDensity / cookbot.getDensity / extended applyDefaults handle persistence.
  - v1.2 / D44 (Plan 07-05) — AdminManageUsersDialog reuses ConfirmDialog (Phase 7 / Plan 07-01 D-29) for the delete-user confirmation. Mirrors CookbookDetail's delete-cookbook + delete-recipe pattern; keeps a single Cb-native generic confirm primitive across the codebase.
  - v1.2 / D45 (Plan 07-05) — SharedKeysDialog inline alerts use ad-hoc `<div class="cb-card">` with severity-tinted backgrounds (var(--accent-soft) / var(--warn-soft)) instead of a dedicated CbAlert atom. Two existing migrated dialogs (AdminManageUsersDialog, EditProfile API-key card) use the same inline pattern; introducing an atom for three consumers would be premature. CbAlert is FUTURE if a fourth use case lands.
  - v1.2 / D46 (Plan 07-05) — Profile equipment + dietary multi-selects render as `<button class="cb-chip">` with aria-pressed (single-element toggle) instead of the previous MudChipSet. No new atom needed; chip pressed-state styling pulled from existing cb-chip variants (timer / ing for selected; tag for unselected).
  - v1.2 / D47 (Plan 07-05) — MainLayout's four MudBlazor providers (`<MudThemeProvider>` / `<MudPopoverProvider>` / `<MudDialogProvider>` / `<MudSnackbarProvider>`) are kept mounted through this plan even though no consumer remains in the Razor tree. Removing them here would entail also editing csproj + _Imports.razor + Program.cs + App.razor, which is the express scope of Plan 07-07 (per Phase 7 D-13). One atomic terminal strip is cleaner than splitting the cleanup across two plans.
metrics:
  duration: ~12 minutes (executor wall-clock)
  tasks_completed: 3/3
  files_modified: 8
  files_created: 0
  tests: 196 passed / 0 failed (baseline)
  build: clean (0 warnings, 0 errors)
  date: 2026-04-27
---

# Phase 7 Plan 5: Profile + user/sharing dialogs migration — Summary

## One-liner

Last `IDialogService` consumer removed: EditProfile rewrite + 4 profile/sharing dialogs migrated to `<CbDialog>` slots (`AddUserDialog`, `PasswordPromptDialog`, `AdminManageUsersDialog`, `SharedKeysDialog`); TopBar switches to `CbDialogService`; density toggle ships on Profile via localStorage + `cookbot.setDensity` JS interop.

## What was built

### Task 1 — EditProfile.razor rewrite (commit `515e52d`)

Full rewrite against Phase 5 atoms; settings split into eight `<CbCard>` cards:

1. **Display name** — `<CbInput>` + `<CbButton Save>`. Edit is staged in `_displayNameEdit`; Save commits to `User.DisplayName` and toasts.
2. **Account password** — `<CbInput Type="password">` + Set/Change/Remove buttons. PBKDF2 hash flow preserved verbatim from previous file (`CurrentUserService.HashPassword` static helper).
3. **AI features toggle** — `<CbToggle>` bound to `UserProfile.AiEnabled`. Toggling persists immediately and triggers `Navigation.NavigateTo(forceLoad: true)` so `Sidebar.OnAfterRenderAsync` re-evaluates the AI-off contract from a fresh circuit (sidebar Assistant + Prompt Builder rows hide/show; Pantry AI buttons; Home AI cards). Server-disabled (CookBotSettings.AiFeaturesEnabled=false) renders the explanatory paragraph without the toggle.
4. **API key + Model + Shared keys** (visible when AI on) — `<CbInput Type="password">` for API key, `<CbSelect>` for model with Fetch-models button, "Save AI settings" + "Manage shared keys" buttons. Inline alerts (cb-card with var(--accent-soft) / var(--warn-soft) backgrounds) for shared-key states (using-shared-key-only, incoming-shares-but-no-usable-key, pending-share-choice).
5. **Cooking preferences** (visible when AI on) — `<CbSelect>` for ExperienceLevel, three `<CbRadio>` for UnitSystem, `<CbTextarea>` for AiUnitExceptions. All wired to debounced 450ms autosave (preserved from previous file).
6. **Theme & density** — `<CbToggle>` bound to local `_isCompactDensity` state. On toggle, calls JS interop `cookbot.setDensity(...)` which writes to `localStorage.cookbot_density` AND sets `<html data-density="...">`. On firstRender, reads `cookbot.getDensity()` to seed the toggle. `cookbot.applyDefaults` (called from MainLayout firstRender) extends to read localStorage and apply data-density before any UI paints.
7. **Kitchen tools** — 38-item list rendered as `<button class="cb-chip ...">` with `aria-pressed`. Selected items use `cb-chip timer` (accent-soft); unselected use `cb-chip tag` (transparent + line border). Toggle adds/removes from `_kitchenTools` HashSet and persists via debounced save.
8. **Dietary preferences** — same chip-toggle pattern over 4 items; selected uses `cb-chip ing` (cream-2).

ZERO Mud* components in the file. ICbToastService replaces ISnackbar; CbDialogService replaces IDialogService for SharedKeysDialog launch.

### Task 2 — Dialog migrations + TopBar switch (commit `bf9bc2b`)

**AddUserDialog.razor** (Layout/) — 22-line content component. CbInput for the name field; Cancel/Add CbButton row. Returns `CbDialogResult.Ok(_name.Trim())` on Add, `Cancel()` on dismiss. Uses `[CascadingParameter] CbDialogInstance` per the D-27 pattern.

**PasswordPromptDialog.razor** (Layout/) — 50-line content component. CbInput type=password; inline error message ("Incorrect password.") shown below the input on a failed verify; the dialog stays open for retry. Submit calls `UserService.VerifyPasswordAsync` and `Close(Ok(true))` on success.

**AdminManageUsersDialog.razor** (Layout/) — 115-line content component. Lists users via cb-card rows with a trash-icon ghost button per row (admin-only; disabled with tooltip when not). Footer hosts "Add user" (admin-only, launches AddUserDialog via CbDialogService) and "Close" CbButton. Delete confirmation goes through `<ConfirmDialog>` (Phase 7 / Plan 07-01 D-29) with Destructive=true. Toasts via ICbToastService for all four AdminDeleteUserResult cases. The non-admin-warning alert renders inline as a cb-card with var(--cream-2).

**SharedKeysDialog.razor** (Pages/) — 210-line content component, the heaviest of the four. Three sections:
- **Share your key with** — `<CbSelect TValue=int>` of eligible recipients + Add CbButton; current outgoing shares listed as cb-card rows with ghost-button "Revoke" per row.
- **API access others share with you** — branched rendering for five mutually-exclusive states (no incoming, has-own-key + incoming, incoming-without-usable-key, currently-using-share, pending-multi-share-choice). Inline cb-card alerts in accent-soft / warn-soft tints.
- **Preferred owner picker** — `<CbSelect>` + Save choice CbButton, shown when multiple usable sharers exist.

Toasts via ICbToastService. The `_addRecipientId == 0` placeholder pattern is preserved (uniform TValue=int across CbSelect + CbOption).

**TopBar.razor** — drops `@inject IDialogService MudDialogService` (the Phase 5 D-13 Alternative A carve-out). Both `OnUserChanged` (PasswordPromptDialog launch) and `ShowAdminManageUsersDialog` switch from `MudDialogService.ShowAsync<T>(...)` + `DialogParameters<T>` + MudBlazor `DialogOptions` to `CbDialogService.ShowAsync<T>(...)` + `CbDialogParameters` (string-keyed) + `CbDialogOptions` record. Behavior preserved: cancel → revert dropdown selection; success → forceLoad navigation.

**MainLayout.razor** — comment-only update describing the Mud-providers-without-consumers state through to Plan 07-07. The four `<MudThemeProvider>` / `<MudPopoverProvider>` / `<MudDialogProvider>` / `<MudSnackbarProvider>` mounts stay (D-47); their removal is bundled with csproj + Program.cs + _Imports.razor + App.razor stripping in 07-07 for atomicity.

### Task 3 — Build verify + commit

`dotnet build CookBot.Web.csproj` — clean (0 warnings, 0 errors).
`dotnet test --filter "Category!=RequiresApiKey"` — 196 passed / 0 failed (baseline preserved).

## Acceptance criteria satisfied

| Criterion | Status |
|-----------|--------|
| PROF-01 (profile settings cards rewritten against atoms) | Done |
| PROF-02 (profile/user/sharing dialogs migrated to <CbDialog>) | Done |
| AI-toggle drives sidebar AI row hiding immediately | Done (forceLoad → Sidebar.OnAfterRenderAsync re-checks) |
| Density toggle persists across sessions | Done (localStorage + cookbot.applyDefaults reads on init) |
| Zero Mud* in EditProfile + 4 dialogs + TopBar | Done (verified by grep) |
| Last IDialogService consumer removed | Done (TopBar.razor) |
| dotnet build clean | Done (0/0) |
| dotnet test baseline | Done (196/196) |

## Hard invariants verified

1. EditProfile rewritten with all six required setting cards plus account-password retention — done.
2. AI features toggle continues to drive `UserProfile.AiEnabled`; toggling triggers a forceLoad reload so Sidebar's existing `OnAfterRenderAsync` AI re-check fires from a fresh circuit (Phase 5 contract intact).
3. Density toggle persists across sessions via localStorage; `cookbot.applyDefaults` reads it before first paint so reload preserves the choice.
4. PasswordPromptDialog + AdminManageUsersDialog migrated to `<CbDialog>` and called via `CbDialogService` from TopBar.
5. AddUserDialog (called from AdminManageUsersDialog) + SharedKeysDialog (called from EditProfile and AiChat — AiChat call site already migrated in Plan 07-04) both migrated to `<CbDialog>` and `CbDialogService`.
6. TopBar's `@inject IDialogService MudDialogService` removed (Phase 5 D-13 Alternative A carve-out).
7. Zero `Mud*` components in plan-scoped files. (Comment-only mentions in TopBar/MainLayout describing the migration history are NOT live code; verified via per-file grep above.)
8. `dotnet build` clean; `dotnet test` baseline preserved at 196/196.

## Deviations from Plan

None — plan executed exactly as written, with one mechanical addition:

**[Rule 2 — Critical correctness] Extended `cookbot.applyDefaults` to read localStorage on init.** Plan task 1 said "Density toggle persists across sessions … apply via cookbot.setDensity(...) JS interop." The toggle alone applies the data-density attribute on click but does NOT survive reload — `cookbot.applyDefaults` (called once on first render from MainLayout) sets the default attribute only when none is present, but doesn't read localStorage. Without this, every reload reverts to `comfy` regardless of the user's choice. Extending applyDefaults to consult `localStorage.cookbot_density` is required for "persists across sessions" to be true. Same shape as the existing `cookbot_dark_mode` pattern in MainLayout. Files modified: `wwwroot/js/cookbot-shell.js`. Captured in commit `515e52d`.

## Known Stubs

None. Density preference flows end-to-end (toggle → JS interop → localStorage → applyDefaults on next visit → data-density attribute → CSS rules in cookbot-design.css already handle compact mode).

## Post-Plan Phase 7 status

| Plan | Status |
|------|--------|
| 07-01 Cookbooks + 7 dialogs | Shipped |
| 07-02 Pantry + 5 dialogs | Shipped |
| 07-03 Grocery + 2 dialogs | Shipped |
| 07-04 AI Chat + Prompt Builder | Shipped |
| **07-05 Profile + 4 dialogs (this plan)** | **Shipped** |
| 07-06 Accessibility audit + small fixes | Pending |
| 07-07 Terminal MudBlazor strip | Pending |

After this plan, the **only** remaining MudBlazor coupling in the Razor tree is:
- Four providers in `MainLayout.razor` (no consumers — kept mounted for atomic strip in 07-07)
- `@using MudBlazor` in `_Imports.razor` (kept until 07-07)
- `MudBlazor` + `MudBlazor.Services` packages in `CookBot.Web.csproj` (kept until 07-07)
- `AddMudServices()` in `Program.cs` (kept until 07-07)
- MudBlazor static link tags in `App.razor` (kept until 07-07)
- `/design-sandbox` route (kept until 07-07)
- A handful of pages outside this plan's scope still use `Mud*` (RecipeView, RecipeEditor, RecipeStepEditor, IngredientChip, CookingMode, RecipeMade, AddPantryItemDialog, AddGroceryListItemDialog) — these are migrated in Plan 07-06 (a11y audit catches Mud usages as part of repo-wide grep) or were already migrated in prior plans and only have residual comment mentions.

Plan 07-06 (a11y audit) runs next; Plan 07-07 (terminal strip) MUST run last.

## Self-Check: PASSED

- File `src/CookBot.Web/Components/Pages/EditProfile.razor` — FOUND
- File `src/CookBot.Web/Components/Layout/AddUserDialog.razor` — FOUND
- File `src/CookBot.Web/Components/Layout/AdminManageUsersDialog.razor` — FOUND
- File `src/CookBot.Web/Components/Layout/PasswordPromptDialog.razor` — FOUND
- File `src/CookBot.Web/Components/Pages/SharedKeysDialog.razor` — FOUND
- File `src/CookBot.Web/Components/Layout/TopBar.razor` — FOUND
- File `src/CookBot.Web/Components/Layout/MainLayout.razor` — FOUND
- File `src/CookBot.Web/wwwroot/js/cookbot-shell.js` — FOUND
- Commit `515e52d` (Task 1) — FOUND
- Commit `bf9bc2b` (Task 2) — FOUND
- Build: 0 warnings, 0 errors
- Tests: 196 passed / 0 failed
- Zero `Mud*` live code in plan-scoped files (TopBar matches are inside @* ... *@ migration commentary; MainLayout providers retained per D-47 for Plan 07-07 atomic strip)
