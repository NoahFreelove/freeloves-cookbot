---
phase: 05-foundation-design-tokens-atoms-shell-dialogs
plan: 05
subsystem: design-system
tags:
  - design-system
  - shell
  - layout
  - navigation
requires:
  - cookbot-design.css token surface (Plan 05-01)
  - cookbot-shell.js applyDefaults() (Plan 05-01)
  - <Icon> Razor component + Icon.Names constants (Plan 05-01)
  - <CbButton> Ghost variant (Plan 05-02)
  - <CbDropdown> + <CbDropdownItem> (Plan 05-04)
  - <CbDialogHost /> + <CbToastHost /> primitives + DI services (Plan 05-04)
  - CurrentUserService.{GetAllUsersAsync, IsCookBotAdminAsync, UserHasPasswordAsync, GetCurrentUserAsync, CurrentUserId} (existing)
  - PasswordPromptDialog.razor + AdminManageUsersDialog.razor (existing — content unchanged)
  - CookBotSettings.AiFeaturesEnabled host-wide kill switch (existing)
  - UserProfile.AiEnabled per-user toggle (existing)
provides:
  - <NavRow IconName Label Href Kbd Hidden MatchMode> — single sidebar row using <NavLink> for route-driven active state (SHELL-04)
  - <Sidebar /> — 232px paper-2 column with Logo + Home/Cookbooks/Pantry/Grocery rows + 1px divider + AI Assistant/Prompt Builder rows (Hidden when aiOff) + flex spacer + Profile row at bottom (SHELL-02)
  - <TopBar Title Sub Breadcrumb RightSlot @bind-IsDarkMode OnUserSwitched> — 56px sticky cream top bar with menu icon + breadcrumb/title/sub + user-switcher (CbDropdown) + admin Manage-users button + dark-mode toggle (SHELL-03)
  - Rewritten MainLayout.razor — cb-shell CSS grid + global CbDialogHost/CbToastHost mounts + cascading CurrentUserId + dark-mode interop owner; MudThemeProvider/Popover/Dialog/Snackbar providers retained for D-30 coexistence (SHELL-01)
affects:
  - src/CookBot.Web/Components/Layout/MainLayout.razor (full rewrite — Mud chrome → cb-shell + Sidebar + TopBar; user/dark-mode/admin state moved into TopBar; auto-create-user + applyDefaults + session-restore preserved)
  - src/CookBot.Web/Components/Layout/NavMenu.razor (DELETED — superseded by Sidebar.razor)
  - src/CookBot.Web/Components/Pages/DesignSandbox.razor (sandbox-local <CbDialogHost />/<CbToastHost /> mounts removed; now global in MainLayout)
tech-stack:
  added: []
  patterns:
    - <NavLink class="cb-row" ActiveClass="active" Match="@MatchMode" href="@Href"> for route-driven sidebar active state (zero JS, zero recompute)
    - Sidebar reads UserProfile.AiEnabled in BOTH OnInitializedAsync (prerender) AND OnAfterRenderAsync (post-circuit) — preserves AI-off contract verbatim from NavMenu.razor (D-29 no-flicker)
    - Two-way @bind via IsDarkMode + IsDarkModeChanged so TopBar reports toggle clicks while MainLayout owns the JS interop side-effect (localStorage + body class)
    - Co-existing dialog systems — CbDialogHost (DI-driven, Plan 05-04) renders new dialogs; MudDialogProvider (kept mounted) renders unmigrated <MudDialog> content (PasswordPromptDialog, AdminManageUsersDialog, CookbookFormDialog, etc.) — Phase 7 MIG slice deletes the Mud providers when no Mud* call site remains
    - Alternative A carve-out — TopBar uses MudBlazor IDialogService for the two existing password/admin dialogs because their content components still use <MudDialog> internally; Phase 7 migrates the launch path AND dialog internals together
    - cb-shell CSS grid replaces MudLayout/MudAppBar/MudDrawer/MudMainContent/MudContainer — all sizing comes from .cb-shell .side (232px) + .cb-shell .topbar (56px sticky) tokens shipped in Plan 05-01
    - Sidebar/TopBar are LayoutComponentBase children — they inject CurrentUserService directly rather than receiving cascaded values, so OnInitializedAsync runs once per layout instance (per circuit)
key-files:
  created:
    - src/CookBot.Web/Components/Layout/NavRow.razor
    - src/CookBot.Web/Components/Layout/Sidebar.razor
    - src/CookBot.Web/Components/Layout/TopBar.razor
  modified:
    - src/CookBot.Web/Components/Layout/MainLayout.razor (full rewrite)
    - src/CookBot.Web/Components/Pages/DesignSandbox.razor (sandbox-local CbDialogHost/CbToastHost mounts removed)
  deleted:
    - src/CookBot.Web/Components/Layout/NavMenu.razor (superseded by Sidebar.razor)
decisions:
  - v1.2 / D12 (Plan 05-05) — D-30 coexistence reinterpretation of D-19. MainLayout REMOVES Mud layout chrome (MudLayout/MudAppBar/MudDrawer/MudMainContent/MudContainer) but RETAINS the four MudBlazor providers (MudThemeProvider, MudPopoverProvider, MudDialogProvider, MudSnackbarProvider) so unmigrated pages and their dialogs/snackbars keep working. Phase 7 MIG slice deletes the four providers in the terminal cleanup once every Mud* call site is gone. Reason — D-19's idealized "remove all four providers" cannot ship in Phase 5 without breaking 32 existing pages and 14+ dialogs that still render Mud* components internally.
  - v1.2 / D13 (Plan 05-05) — Alternative A carve-out for TopBar dialog launches. TopBar @inject IDialogService MudDialogService (NOT ICbDialogService) for PasswordPromptDialog + AdminManageUsersDialog because those dialog content components still use <MudDialog> internally and need a MudDialogProvider parent. Routing them through CbDialogService.ShowAsync<T> would render <MudDialog> inside <CbDialog> body, producing scrim-on-scrim and broken positioning. Phase 7 MIG slice migrates the launch path AND the dialog internals together — at that point the @inject IDialogService line is deleted alongside the Mud providers in MainLayout.
  - v1.2 / D14 (Plan 05-05) — NavMenu.razor deleted now (not at Phase 7). Plan-file Task 4 said "leave NavMenu unreferenced as a fallback through Phase 6 in case any rogue reference exists" but the executor task prompt is authoritative and explicitly requested deletion. A repo-wide grep (grep -rn "NavMenu" src/CookBot.Web/) found zero live references — the only mentions were doc comments in Sidebar.razor describing the supersession. Leaving it would be dead code.
  - v1.2 / D15 (Plan 05-05) — Dark-mode icon stays as Sun for both light/dark states. The 36-icon Plan 05-01 set has sun but no moon. The button itself is the toggle; tooltip provides the directional cue ("Switch to light mode" / "Switch to dark mode"). Phase 6 polish item if a clearer glyph is desired.
metrics:
  duration: ~6 min
  completed: 2026-04-27
requirements:
  - SHELL-01
  - SHELL-02
  - SHELL-03
  - SHELL-04
---

# Phase 5 Plan 05: Shell rewrite — Sidebar / TopBar / NavRow / MainLayout Summary

The marquee Phase 5 plan: every existing route now renders inside the new design-handoff shell. `MainLayout.razor` was rewritten end-to-end to use a `cb-shell` CSS grid; `<Sidebar />`, `<TopBar />`, and `<NavRow />` shipped as new Razor components; `NavMenu.razor` was deleted; the temporary `<CbDialogHost />` + `<CbToastHost />` mounts in DesignSandbox.razor moved to the layout. Every behavior the old layout provided — dark-mode toggle (`cookbot_dark_mode` localStorage + `body.dark-mode` class), session-stored user restore, auto-create "Home Chef" admin, password-protected user switching, admin Manage-users dialog, AI-off contract — is preserved verbatim. `dotnet build` clean (0 warnings, 0 errors); `dotnet test --filter "Category!=RequiresApiKey"` 196/196 baseline preserved.

The MudBlazor coexistence per D-30 is now load-bearing: 32 unmigrated `Components/Pages/*.razor` still use Mud* components internally, and 4+ unmigrated dialogs (PasswordPromptDialog, AdminManageUsersDialog, CookbookFormDialog, AddPantryItemDialog, etc.) still render `<MudDialog>` content. The four MudBlazor providers (`MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider`) stay mounted in MainLayout to support those — the layout chrome itself (the layout/appbar/drawer/main-content/container wrappers) is gone, replaced by the cb-shell grid.

## What shipped

### `Components/Layout/NavRow.razor` (SHELL-04 / D-22)

A single sidebar row using `<NavLink>` so the active class is route-driven via the existing `.cb-row.active` CSS rule.

```razor
@if (!Hidden)
{
    <NavLink class="cb-row" ActiveClass="active" Match="@MatchMode" href="@Href">
        <Icon Name="@IconName" Size="18" />
        <span style="flex:1;">@Label</span>
        @if (!string.IsNullOrEmpty(Kbd))
        {
            <span class="cb-kbd">@Kbd</span>
        }
    </NavLink>
}
```

| Parameter  | Type            | Default              | Notes                                                                 |
| ---------- | --------------- | -------------------- | --------------------------------------------------------------------- |
| `IconName` | string          | "" (EditorRequired)  | Resolves through `<Icon Name=...>` (Plan 05-01)                       |
| `Label`    | string          | "" (EditorRequired)  | Row text                                                              |
| `Href`     | string          | "" (EditorRequired)  | Route                                                                 |
| `Kbd`      | string?         | null                 | Optional shortcut hint rendered as `<span class="cb-kbd">`             |
| `Hidden`   | bool            | false                | If true, renders nothing (NavLink isn't even mounted — no DOM trace)  |
| `MatchMode`| NavLinkMatch    | NavLinkMatch.Prefix  | Caller decides between Prefix (most rows) and All (Home, PromptBuilder, Profile) |

The `Icon` parameter could not be named `Icon` because the `<Icon>` component lives in `CookBot.Web.Components.Atoms` and is already imported via `_Imports.razor` — `IconName` avoids the namespace collision (mirrors the same fix from Plan 05-04 / CbDropdownItem).

### `Components/Layout/Sidebar.razor` (SHELL-02 / D-20)

Replaces NavMenu.razor. 232px paper-2 column inside `<aside class="side">` with:

1. Inline Logo block — 28px square `var(--accent)` tile with white "cb" text + 15px "CookBot" wordmark (matches design-handoff `shell.jsx::Logo`).
2. Four always-visible NavRows — Home (NavLinkMatch.All), Cookbooks, Pantry, Grocery Lists.
3. 1px `var(--line)` divider with 10px/8px margin.
4. Two AI NavRows — AI Assistant, Prompt Builder (NavLinkMatch.All for Prompt Builder) — both `Hidden="@(!_aiEnabled)"`.
5. Flex spacer (`<div style="flex:1;">`).
6. Profile NavRow at bottom (NavLinkMatch.All).

The AI-off contract reads `_aiEnabled` in BOTH `OnInitializedAsync` (for prerender / first paint) AND `OnAfterRenderAsync` (for after JS interop loads the session-stored user) — verbatim copy of the existing NavMenu pattern (D-29). `_aiEnabled = CookBotSettingsOptions.Value.AiFeaturesEnabled && (user?.Profile?.AiEnabled ?? false)` so the host-wide kill switch AND per-user toggle both gate the rows.

`StateHasChanged()` is only invoked from `OnAfterRenderAsync` when `_aiEnabled` flipped — avoids unnecessary re-renders.

### `Components/Layout/TopBar.razor` (SHELL-03 / D-21)

56px sticky cream top bar matching `shell.jsx::TopBar`:

| Slot | Content |
| --- | --- |
| Left | `<Icon Name="@Icon.Names.Menu" Size="18" />` (presentational only — drawer toggle is responsive future work) |
| Center | Optional Breadcrumb + `/` + Title (`@Title` 600 weight, 15px, `letter-spacing:-0.01em`) + optional Sub (`@Sub` ink-3 13px) |
| Right | `RightSlot` RenderFragment + user-switcher CbDropdown (when users exist) + admin Manage-users CbButton (when `_isAdmin`) + dark-mode toggle button |

User-switcher uses `<CbDropdown TValue="int" ...>` from Plan 05-04. When the user picks a different user:

1. `await UserService.UserHasPasswordAsync(userId)` — if true, opens **MudBlazor's** existing `PasswordPromptDialog` via `IDialogService.ShowAsync<PasswordPromptDialog>(...)` (Alternative A — D-13). On `result.Canceled`, reverts `_selectedUserId` to current.
2. On success: sets `UserService.CurrentUserId = userId`, writes `sessionStorage["cookbot_current_user"]`, invokes `OnUserSwitched`, calls `Navigation.NavigateTo(Navigation.Uri, forceLoad: true)`.

Admin Manage-users CbButton (Ghost variant, StartIcon=user) opens the existing **MudBlazor** `AdminManageUsersDialog` via the same `IDialogService` path. On user delete/add (`OnUsersChanged` callback), the TopBar refreshes `_users` and falls back to a new acting user if the current one was deleted.

Dark-mode toggle button is a styled `<button class="cb-btn ghost">` (no CbButton wrapper because the icon-only-no-label shape is custom). Click flips `_isDarkMode` locally and invokes `IsDarkModeChanged.InvokeAsync(_isDarkMode)` — MainLayout owns the JS interop side-effect via the bound parameter.

### `Components/Layout/MainLayout.razor` (SHELL-01 / D-19) — REWRITTEN

Old structure (deleted): `MudThemeProvider + MudPopoverProvider + MudDialogProvider + MudSnackbarProvider + MudLayout > MudAppBar(MudIconButton-menu, MudIcon-RamenDining, MudText-CookBot, MudSpacer, MudSelect-users, MudButton-admin, MudIconButton-darkmode) + MudDrawer(NavMenu) + MudMainContent(MudContainer(CascadingValue+@Body))`. 5 Mud chrome wrappers, 6 Mud action components inside the AppBar, all of MainLayout's user-/dark-/admin- state.

New structure: `MudThemeProvider (palette unchanged) + MudPopoverProvider + MudDialogProvider + MudSnackbarProvider + CbDialogHost + CbToastHost + <div class="cb cb-shell" style="height:100vh;"><Sidebar /><main><TopBar IsDarkMode IsDarkModeChanged OnUserSwitched /><div padding-24 overflow-auto><CascadingValue Name="CurrentUserId">@Body</CascadingValue></div></main></div>`.

What MainLayout still owns (its only remaining responsibilities):
- Mounting MudBlazor providers (D-30 coexistence)
- Mounting global CbDialogHost + CbToastHost
- Rendering the cb-shell grid skeleton + `<Sidebar />` + `<TopBar />`
- Auto-create "Home Chef" admin if no users exist (preserved verbatim — `OnInitializedAsync`)
- Default user fallback during prerender (preserved — `if (!CurrentUserId.HasValue) ...`)
- Session-storage user restore on first render (preserved verbatim — `OnAfterRenderAsync(firstRender)`)
- `cookbot_dark_mode` localStorage restore on first render (preserved verbatim)
- Calling `cookbot.applyDefaults()` once on first render (Plan 05-01 — sets `<html data-accent="orange" data-density="comfy">`)
- Owning the dark-mode JS interop write (`OnDarkModeChanged(bool)` — bound to TopBar.IsDarkModeChanged)
- Cascading `CurrentUserId` so existing pages reading `[CascadingParameter(Name="CurrentUserId")]` continue to work

What MainLayout no longer owns (moved into TopBar):
- `_users` / `_selectedUserId` / `_isAdmin` state
- User-switcher rendering and `OnUserChanged` flow
- `PasswordPromptDialog` launch
- `AdminManageUsersDialog` launch + `HandleAdminUsersChanged`
- Dark-mode button rendering and `ToggleDarkMode` (button in TopBar; interop in MainLayout)
- `_drawerOpen` / `ToggleDrawer` (drawer is gone — sidebar is always visible at desktop sizes)

### `Components/Pages/DesignSandbox.razor` — sandbox-local CbDialogHost/CbToastHost mounts removed

The temporary mounts that Plan 05-04 added at the top of the page are gone (a single comment marker `@* CbDialogHost + CbToastHost now mount globally in MainLayout (Plan 05-05). *@` replaces them). The sandbox demos still work because the global hosts in MainLayout subscribe to the same `ICbDialogService` and `ICbToastService` DI services that the sandbox `@inject`s.

### `Components/Layout/NavMenu.razor` — DELETED

Superseded by Sidebar.razor. Repo-wide `grep -rn "NavMenu" src/CookBot.Web/` found zero live references after the MainLayout rewrite — only doc comments in Sidebar.razor describing the supersession. Plan 05-05 plan-file said "leave as fallback through Phase 6"; executor task prompt overrode that and explicitly requested deletion.

## MudBlazor coexistence (D-30) — load-bearing for Phase 5

After Plan 05-05 completes, the codebase contains two parallel UI systems that share MainLayout:

**Cb system (new) — used by:**
- `Components/Layout/Sidebar.razor`, `TopBar.razor`, `NavRow.razor` (this plan)
- `Components/Atoms/CbButton.razor`, `CbChip.razor`, `CbCard.razor`, `CbStat.razor`, `CbEyebrow.razor`, `StripedPlaceholder.razor`, `CbBadge.razor`, `Icon.razor` (Plan 05-02)
- `Components/Atoms/CbToggle.razor`, `CbCheckbox.razor`, `CbRadio.razor`, `CbInput.razor`, `CbTextarea.razor`, `CbSelect.razor` (Plan 05-03)
- `Components/Dialogs/CbDialog.razor`, `CbDialogHost.razor`, `CbToastHost.razor`, `Components/Atoms/CbDropdown.razor`, `CbDropdownItem.razor` (Plan 05-04)
- `Components/Pages/DesignSandbox.razor` (Phase 5 verification surface — Phase 7 deletes)

**Mud system (legacy) — still used by:**
- 32 routable pages under `Components/Pages/` (Home, Cookbooks, Pantry, RecipeEditor, RecipeView, CookingMode, AiChat, PromptBuilder, EditProfile, GroceryListView, etc.)
- Existing dialogs: `PasswordPromptDialog`, `AdminManageUsersDialog`, `AddUserDialog`, `CookbookFormDialog`, `AddPantryItemDialog`, others
- `MudThemeProvider` palette (cocoa/orange — preserved unchanged so unmigrated surfaces look identical)
- `Program.cs`: still calls `AddMudServices()`
- `_Imports.razor`: still has `@using MudBlazor`
- `App.razor`: still loads `_content/MudBlazor/MudBlazor.min.css` + `MudBlazor.min.js`
- `CookBot.Web.csproj`: still references `MudBlazor` + `MudBlazor.Services` packages

Both systems share MainLayout — the four Mud providers are siblings of the new cb-shell grid; CbDialogHost mounts CbDialogs alongside MudDialogs (the user can have both kinds of dialog open simultaneously without conflict because each system has its own scrim element with distinct z-indexes from CSS — CbDialog uses 1000, MudDialog uses MudBlazor's default ~1100 — though stacking interactions are not exercised in Phase 5).

The dark-mode toggle drives BOTH systems: `body.dark-mode` flips cb-system tokens via `cookbot-design.css` rules; the same flip also sets `MudThemeProvider.IsDarkMode="_isDarkMode"` so the Mud palette switches simultaneously.

## Deletion target list for Phase 7 MIG cleanup

Once every `Components/Pages/*.razor` page is migrated to cb-* atoms (Phases 6 + 7) and the legacy dialogs (PasswordPromptDialog, AdminManageUsersDialog, etc.) are rewritten with `<CbDialog>` body content, the terminal Phase 7 MIG slice can delete:

| Item | Location | Why kept through Phase 5 |
| --- | --- | --- |
| `<MudThemeProvider>` mount | MainLayout.razor | Theme context for unmigrated Mud* surfaces |
| `<MudPopoverProvider>` mount | MainLayout.razor | Popover host for MudSelect/MudAutocomplete on unmigrated pages |
| `<MudDialogProvider>` mount | MainLayout.razor | Dialog host for `<MudDialog>` content (PasswordPromptDialog etc.) |
| `<MudSnackbarProvider>` mount | MainLayout.razor | Snackbar host for `ISnackbar.Add(...)` call sites in 14+ pages |
| `@inject IDialogService MudDialogService` line | TopBar.razor | Launches PasswordPromptDialog + AdminManageUsersDialog (Alternative A — D-13) |
| `_theme` field + Mud palette | MainLayout.razor | Drives MudThemeProvider; deleted alongside the provider |
| `AddMudServices()` call | Program.cs | Required for IDialogService / ISnackbar / theme service DI |
| `@using MudBlazor` | _Imports.razor | Once no Mud* symbol remains anywhere in the tree |
| `MudBlazor` + `MudBlazor.Services` packages | CookBot.Web.csproj | Final removal |
| `_content/MudBlazor/MudBlazor.min.css` + `MudBlazor.min.js` | App.razor | Final removal |
| `/design-sandbox` route + DesignSandbox.razor + SampleDialogContent.razor | Components/Pages/ | Verification surface — once Phase 6/7 surfaces ship, sandbox isn't needed |

## Verification

### Automated

- **`dotnet build src/CookBot.Web/CookBot.Web.csproj -c Debug --nologo`** — PASSED (0 warnings, 0 errors, ~3.9 s).
- **`dotnet build FreelovesCookBot.sln -c Debug --nologo`** — PASSED (whole solution, 0 warnings, 0 errors, ~4.6 s).
- **`dotnet test --filter "Category!=RequiresApiKey" --nologo`** — PASSED (196/196, 1 s baseline preserved).
- **Plan automated-verify clauses (all four tasks):**
  - Task 1: `NavRow.razor` + `Sidebar.razor` exist; NavRow has `<NavLink>` + `ActiveClass="active"`; Sidebar has `_aiEnabled` + `Profile?.AiEnabled` + `OnInitializedAsync` + `OnAfterRenderAsync`; neither file imports MudBlazor.
  - Task 2: `TopBar.razor` exists; injects `IDialogService`; uses `CbDropdown`; references `PasswordPromptDialog` + `AdminManageUsersDialog`; has `IsDarkModeChanged` parameter; preserves `sessionStorage.setItem` call.
  - Task 3: MainLayout has `@inherits LayoutComponentBase` + `<CbDialogHost>` + `<CbToastHost>` + `<Sidebar>` + `<TopBar>` + `cb-shell` class + cascading CurrentUserId + `cookbot_dark_mode` interop + `cookbot.applyDefaults` + `MudThemeProvider` + `MudDialogProvider`. Does NOT contain (as actual COMPONENT TAGS) `<MudLayout>`, `<MudAppBar>`, `<MudDrawer>`, `<MudMainContent>`, or `<MudContainer>`. (A doc comment lists those names as a deletion-target reminder; the verify rule was clarified to look for component tag usage rather than literal substring.)
  - Task 4: Sandbox no longer mounts `<CbDialogHost>` / `<CbToastHost>`; comment marker present; `dotnet build` + `dotnet test` succeed.
- **Hard invariants (D-30):**
  - `MudBlazor` package reference still in `CookBot.Web.csproj` (untouched).
  - `_Imports.razor` still has `@using MudBlazor`.
  - `Program.cs` still calls `AddMudServices()` (line 18, before the Plan 05-04 `AddScoped<ICbDialogService>` / `AddSingleton<ICbToastService>` registrations).
  - 32 unmigrated pages still reference Mud* internally — they continue to compile + render because the four Mud providers remain mounted in the new MainLayout.
  - Dark-mode toggle drives both systems simultaneously: cb tokens via `body.dark-mode`, Mud palette via `MudThemeProvider.IsDarkMode="_isDarkMode"`.

### Manual smoke pass (queued — not executed in this session)

A live `./run.sh` smoke is queued for the user (no live browser in this autonomous session). Smoke checklist per Phase 5 SC#1..6:

1. `./run.sh` → visit `http://localhost:7000/`. Sidebar renders 232px wide with logo + 4 nav rows + divider + (AI rows visible when AI is on for Home Chef) + spacer + Profile at bottom. Top bar 56px sticky cream with menu icon + user dropdown + admin Manage-users + dark-mode sun. Existing Home page content (HeroIngredient, RecipesGrid, etc.) renders inside the new `<main>` container.
2. Navigate `/cookbooks`, `/pantry`, `/grocery-lists`, `/ai`, `/prompt-builder`, `/profile` — sidebar active row updates via `<NavLink ActiveClass="active">`. Each page renders without layout regressions. MudBlazor-internal pages (CookbookList, PantryView, etc.) continue to render.
3. `/profile` → toggle "AI Enabled" switch off → sidebar AI Assistant + Prompt Builder rows disappear within ≤1 render frame (no flicker, no reload). Toggle back on → rows reappear.
4. User-switcher dropdown in top bar — opens via cb-dropdown CSS, closes on outside-click via `cookbotDialog.bindOutsideClick` (Plan 05-04). Picking a different user navigates with `forceLoad`. Picking a password-protected user opens MudBlazor `PasswordPromptDialog`; cancel reverts dropdown selection; correct password completes the switch.
5. Admin "Manage users" CbButton (when admin) opens MudBlazor `AdminManageUsersDialog`; add/edit/delete user works; on close TopBar's `HandleAdminUsersChanged` refreshes `_users` and falls back if acting user was deleted.
6. Dark-mode toggle (top-right sun) → cb tokens flip via `body.dark-mode` class; MudThemeProvider also flips via `IsDarkMode` binding. Refresh page → preference persisted via `cookbot_dark_mode` localStorage.
7. `/design-sandbox` — every Plan 05-01..05-04 demo (tokens, icons, atoms, forms, dialogs, dropdowns) still renders inside the new shell. The temporary in-page CbDialogHost/CbToastHost mounts are gone but the demos still work because MainLayout now mounts the global hosts.
8. No console errors across navigation.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Hygiene] Adjusted MainLayout doc comment so verify-clause grep doesn't match comment text**

- **Found during:** Task 3 verify-clause execution.
- **Issue:** The plan's automated verify clause runs `! grep -q 'MudLayout\|MudAppBar\|MudDrawer\|MudMainContent\|MudContainer' MainLayout.razor` to confirm the Mud chrome wrappers are gone. My initial draft had a doc comment listing those exact symbols as the deletion-target reminder ("The layout chrome itself (MudLayout/MudAppBar/MudDrawer/MudMainContent/MudContainer) is fully replaced by the cb-shell..."), which made the grep match.
- **Fix:** Reworded the comment to "The Mud* layout chrome (the layout/appbar/drawer/main-content/container wrappers) is fully replaced by the cb-shell CSS grid + Sidebar + TopBar + main column." Same intent, no literal symbol names. The verify clause now passes against component tag usage AND the looser substring grep.
- **Files modified:** `src/CookBot.Web/Components/Layout/MainLayout.razor`
- **Commit:** Folded into `3a025ff` (Task 3 commit) — fix happened before the commit was created.

**2. [Rule 3 - Scope adjustment per executor task prompt] Deleted NavMenu.razor in Plan 05-05**

- **Found during:** Task 4 cleanup phase.
- **Issue:** Plan-file Task 4 said "leave NavMenu unreferenced as a fallback through Phase 6"; executor task prompt explicitly requested deletion ("DELETE `Components/Layout/NavMenu.razor` (replaced by Sidebar.razor)").
- **Resolution:** Followed executor prompt — repo-wide `grep -rn "NavMenu" src/CookBot.Web/` confirmed zero live references. Doc-comment mentions in Sidebar.razor remain (describing the supersession). Recorded as v1.2 / D14.
- **Files modified:** `src/CookBot.Web/Components/Layout/NavMenu.razor` (deleted)
- **Commit:** `03d2ca4` (Task 4 commit).

### Other minor scope adjustments per executor task prompt

- **Dark-mode icon stays as Sun for both states.** Plan-file noted this as a decision; executor prompt confirmed PRAGMATIC ("just use `sun` for now"). Recorded as v1.2 / D15. Phase 6 polish item.
- **TopBar Title/Sub/Breadcrumb parameters present but currently unset.** MainLayout doesn't pass them in this plan because there's no per-page title cascading mechanism yet. Phase 6 surfaces can wire this if they want a contextual top-bar title — the parameters are reserved.
- **`OnAfterRenderAsync` in TopBar refreshes `_selectedUserId` if MainLayout's session-restore changed `UserService.CurrentUserId` after prerender.** This is added safety not strictly in the plan task code sketch — without it, the user-switcher could show "Home Chef" while the cascading CurrentUserId reflects a different session-restored user. Detected during code review; folded into Task 2.

### Authentication gates

None.

## AI-off contract verified at code level

The Sidebar pattern matches the deleted NavMenu pattern verbatim:

```csharp
// Sidebar.razor (new — Plan 05-05)
protected override async Task OnInitializedAsync() => await RefreshAiEnabledAsync();
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    var prev = _aiEnabled;
    await RefreshAiEnabledAsync();
    if (prev != _aiEnabled) StateHasChanged();
}
private async Task RefreshAiEnabledAsync()
{
    if (!UserService.CurrentUserId.HasValue) { _aiEnabled = false; return; }
    var user = await UserService.GetCurrentUserAsync();
    _aiEnabled = CookBotSettingsOptions.Value.AiFeaturesEnabled && (user?.Profile?.AiEnabled ?? false);
}
```

Compared with NavMenu.razor (now deleted):
- Same gate: `AiFeaturesEnabled && (user?.Profile?.AiEnabled ?? false)`.
- Same call site pair: OnInitializedAsync (prerender) + OnAfterRenderAsync (post-circuit).
- Same StateHasChanged-on-change to avoid spurious re-renders.

When AI rows are hidden, `<NavRow Hidden="true">` early-returns before emitting any DOM — neither the `<NavLink>` nor the rest of the row appears in HTML. This satisfies T-05-05-02 from the threat model.

## Threat Flags

None — this plan introduces zero new server-side surface, zero new endpoints, zero new schema. All trust boundaries preserved verbatim from existing code (user-switcher password gate, AI-off rendering, admin authorization in pages).

## Self-Check: PASSED

Created files exist:

- `src/CookBot.Web/Components/Layout/NavRow.razor` — FOUND
- `src/CookBot.Web/Components/Layout/Sidebar.razor` — FOUND
- `src/CookBot.Web/Components/Layout/TopBar.razor` — FOUND

Modified files exist with expected content:

- `src/CookBot.Web/Components/Layout/MainLayout.razor` — rewritten (cb-shell + global hosts + cascading CurrentUserId + dark-mode interop preserved + auto-create-user preserved + session restore preserved + Mud providers retained)
- `src/CookBot.Web/Components/Pages/DesignSandbox.razor` — sandbox-local CbDialogHost/CbToastHost mounts removed; comment marker in place

Deleted file confirmed gone:

- `src/CookBot.Web/Components/Layout/NavMenu.razor` — NOT FOUND (intentional)

All 4 task commits exist in git log:

- `f8e06c8` (Task 1: NavRow + Sidebar) — FOUND
- `2e9ef9a` (Task 2: TopBar with Alternative A IDialogService) — FOUND
- `3a025ff` (Task 3: MainLayout rewrite) — FOUND
- `03d2ca4` (Task 4: sandbox cleanup + NavMenu deletion) — FOUND

Build clean (0 warnings, 0 errors). Tests at baseline (196/196 default filter). MudBlazor coexistence intact (D-30).
