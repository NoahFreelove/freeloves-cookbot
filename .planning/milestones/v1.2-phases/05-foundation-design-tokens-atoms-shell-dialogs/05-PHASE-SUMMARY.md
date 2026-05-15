---
phase: 05-foundation-design-tokens-atoms-shell-dialogs
phase_number: 5
phase_name: Foundation — Design tokens, atoms, shell, dialogs
status: complete
plans_total: 5
plans_completed: 5
milestone: v1.2
milestone_name: UI Redesign
tags:
  - design-system
  - foundation
  - atoms
  - shell
  - dialogs
requirements_satisfied:
  - DS-01
  - DS-02
  - DS-03
  - DS-04
  - DS-05
  - DS-06
  - ATOM-01
  - ATOM-02
  - ATOM-03
  - ATOM-04
  - ATOM-05
  - ATOM-06
  - ATOM-07
  - ATOM-08
  - ATOM-09
  - ATOM-10
  - SHELL-01
  - SHELL-02
  - SHELL-03
  - SHELL-04
  - DIALOG-01
  - DIALOG-02
  - DIALOG-03
  - DIALOG-04
metrics:
  duration: ~30 min total wall-clock across 5 sessions
  completed: 2026-04-27
plans:
  - 05-01-PLAN.md / 05-01-SUMMARY.md — Design tokens + Icon component + sandbox route (DS-01..06, ATOM-07)
  - 05-02-PLAN.md / 05-02-SUMMARY.md — Display atoms (CbButton/Chip/Card/Stat/Eyebrow/Badge/StripedPlaceholder) (ATOM-01..06, ATOM-08)
  - 05-03-PLAN.md / 05-03-SUMMARY.md — Form atoms (CbToggle/Checkbox/Radio/Input/Textarea/Select/Option) (ATOM-09, ATOM-10)
  - 05-04-PLAN.md / 05-04-SUMMARY.md — Dialog/Toast/Dropdown primitives (CbDialog/CbDialogHost/CbToastHost/CbDropdown/CbDropdownItem + services + cb-dialog.js) (DIALOG-01..04)
  - 05-05-PLAN.md / 05-05-SUMMARY.md — Shell rewrite (Sidebar/TopBar/NavRow + MainLayout cb-shell grid + global host mounts) (SHELL-01..04)
---

# Phase 5: Foundation — Design tokens, atoms, shell, dialogs Summary

**Phase complete: 2026-04-27.** All 24 in-scope requirements satisfied (DS-01..06, ATOM-01..10, SHELL-01..04, DIALOG-01..04). The custom Razor component system is in place end-to-end and verified on `/design-sandbox`. Every existing route now renders inside the new design-handoff shell. MudBlazor coexists per D-30 — the package reference, AddMudServices() call, _Imports.razor using directive, and four MudBlazor providers (MudThemeProvider, MudPopoverProvider, MudDialogProvider, MudSnackbarProvider) all stay mounted in MainLayout to support the 32 unmigrated routable pages and ~14 unmigrated dialogs. Phase 6 migrates marquee surfaces (Home, Cooking Mode, Recipe View, Recipe Editor); Phase 7 migrates remaining surfaces and runs the terminal MIG slice that deletes MudBlazor. `dotnet build FreelovesCookBot.sln -c Debug` clean (0 warnings, 0 errors); `dotnet test --filter "Category!=RequiresApiKey"` 196/196 baseline preserved across every plan.

## What Phase 5 delivered

### Design tokens (Plan 05-01 — DS-01..06, ATOM-07)

- `src/CookBot.Web/wwwroot/css/cookbot-design.css` — single global stylesheet, near-verbatim port of `.planning/design-handoff/project/styles.css`.
- Token categories: surfaces (`--cream`, `--cream-2`, `--paper`, `--paper-2`, `--line`, `--line-strong`), ink (`--ink`, `--ink-2/3/4`), accent (`--accent`, `--accent-soft`, `--accent-ink`) with three variants (orange default, terracotta, sage) selectable via `data-accent` on `<html>`, density (`--pad`, `--pad-sm`, `--gap`) with `comfy`/`compact` modes via `data-density`, type families, radii.
- Dark-mode parity for every token via `body.dark-mode` selectors — preserves the existing `cookbot_dark_mode` localStorage toggle (D-04).
- `cb-*` class system (`.cb-btn`, `.cb-card`, `.cb-chip`, `.cb-row`, `.cb-stat`, `.cb-shell`, `.cb-ph`, `.cb-recipe-cap`, `.cb-rule`, `.cb-kbd`, `.eyebrow`, `.num`, `.mono`).
- Inter font loaded via `@import` from `rsms.me/inter/inter.css` (D-03).
- `<Icon>` Razor component dispatching all 36 outline icons from `.planning/design-handoff/project/icons.jsx` via switch + MarkupString (`Icon.Names.*` constants for typo safety).
- `cookbot-shell.js` JS interop module — `applyDefaults()` sets `<html data-accent="orange" data-density="comfy">` idempotently.
- `/design-sandbox` route created with INSERTION_POINT sentinels for atoms / forms / dialogs (replaced in subsequent plans).

### Display atoms (Plan 05-02 — ATOM-01..06, ATOM-08)

- `<CbButton>` — 4 variants (Primary cocoa, Accent orange, Ghost transparent+border, Subtle); 999px pill radius; StartIcon/EndIcon/FullWidth/Disabled/Type/OnClick parameters.
- `<CbChip>` — 4 variants (default, timer accent-soft, ing cream-2, tag transparent+border); Icon/Label/optional OnClick.
- `<CbCard>` — paper bg, 14px radius, line border, Padding/Style slots.
- `<CbStat>` — label + tabular-numeral 36px value + sub-text; min-height 124px.
- `<CbEyebrow>` — 11px uppercase letter-spaced 0.14em ink-3 weight 500.
- `<CbBadge>` — 4 status variants (in-stock green-soft, low warn-soft, expiring accent-soft, out gray) with status-derived default labels.
- `<StripedPlaceholder>` — diagonal stripes + dashed border + monospace caption (DS-06); accepts %, px, rem, em, vh, vw dimensions.
- All atoms emit `cb-*` CSS classes — zero MudBlazor symbols (D-30).

### Form atoms (Plan 05-03 — ATOM-09, ATOM-10)

- `<CbToggle>` — bool @bind-Value switch with Label and Disabled.
- `<CbCheckbox>` — bool @bind-Value with Label and Disabled.
- `<CbRadio<TValue>>` — Value parameter + @bind-CurrentValue group binding (matches Blazor's grouped-input idiom).
- `<CbInput>` — string @bind-Value text input with Type/Placeholder/Disabled/Style/DebounceOnInput.
- `<CbTextarea>` — string @bind-Value with Rows/Placeholder/Disabled.
- `<CbSelect<TValue>>` — generic select with @bind-Value, Placeholder, Disabled, ChildContent of `<CbOption<TValue>>` items via CascadingParameter coordination.
- Native `<input>` / `<select>` / `<textarea>` elements remain in DOM (display:none via CSS where visual is custom) — accessibility/keyboard preserved.
- `input:checked` sibling selectors drive custom-styled visuals (no JS for state).

### Dialog/Toast/Dropdown primitives (Plan 05-04 — DIALOG-01..04)

- `<CbDialog>` (presentational) — fixed-position scrim + centered card with line border + 14px radius + paper bg; MaxWidth (xs/sm/md/lg/xl), FullWidth, CloseOnEscape, CloseOnScrim, focus trap, multi-dialog stacking via Header/Body/Footer slots.
- `<CbDialogHost />` — DI-event-driven host. Subscribes to `ICbDialogService.OnRequest`; renders `_stack: List<HostEntry>` as `@foreach` of `<CbDialog @key="...">` instances; uses Blazor's `<DynamicComponent>` for type-erased dialog-content rendering with cascaded `CbDialogInstance` so content can self-close.
- `ICbDialogService` / `CbDialogService` (Scoped) — `ShowAsync<TDialog>(title, parameters, options)` returns `Task<CbDialogResult>`; mirrors MudBlazor `IDialogService.ShowAsync` shape closely so Phase 7 dialog migrations are mechanical.
- `<CbToastHost />` — max 3 stacked toasts with FIFO eviction + CTS cancel of evicted timer; 5s auto-fade; 4 severity tints (success/error/info/warning); InvokeAsync-marshaled for Singleton→Scoped boundary.
- `ICbToastService` / `CbToastService` (Singleton) — `Show(message, severity)`; same shape as `ISnackbar.Add(...)`.
- `<CbDropdown<TValue>>` + `<CbDropdownItem<TValue>>` — generic select-style dropdown with ESC + outside-click + item-select close; `[CascadingParameter]` parent coordination.
- `wwwroot/js/cb-dialog.js` — `window.cookbotDialog` with `trapFocus` / `releaseFocus` / `bindOutsideClick` / `unbindOutsideClick` (refcounted body scroll lock).
- 157 lines of CSS appended to cookbot-design.css for `.cb-scrim`, `.cb-dialog`, `.cb-dialog-{header/body/footer}`, `.cb-toast-host`, `.cb-toast`, `.cb-dropdown`, `.cb-dropdown-menu`, `.cb-dropdown-item`.

### Shell rewrite (Plan 05-05 — SHELL-01..04)

- `<NavRow IconName Label Href Kbd Hidden MatchMode>` — single sidebar row using `<NavLink class="cb-row" ActiveClass="active">` for route-driven active state.
- `<Sidebar />` — 232px paper-2 column with inline Logo (28px accent square + "CookBot" wordmark) + Home/Cookbooks/Pantry/Grocery rows + 1px divider + AI Assistant/Prompt Builder rows (Hidden when AiEnabled = false on user OR host) + flex spacer + Profile row at bottom. AI-off contract preserved from NavMenu via OnInitializedAsync + OnAfterRenderAsync read pattern.
- `<TopBar Title Sub Breadcrumb RightSlot @bind-IsDarkMode OnUserSwitched>` — 56px sticky cream top bar with menu icon + breadcrumb/title/sub + RightSlot + user-switcher (CbDropdown) + admin Manage-users (CbButton Ghost) + dark-mode toggle.
- MainLayout.razor REWRITTEN — replaces MudLayout/MudAppBar/MudDrawer/MudMainContent/MudContainer with cb-shell CSS grid + Sidebar + TopBar + main column; mounts CbDialogHost + CbToastHost globally; preserves dark-mode interop, session-storage user restore, auto-create "Home Chef" admin, default-user fallback, applyDefaults call, cascading CurrentUserId.
- NavMenu.razor DELETED (superseded by Sidebar).

## Architecture decisions accumulated across Phase 5

| ID | Plan | Decision |
| --- | --- | --- |
| D-01 | 05-01 | Tokens live in single global stylesheet at `src/CookBot.Web/wwwroot/css/cookbot-design.css` — not scoped CSS, not CSS-in-Razor |
| D-02 | 05-01 | Stylesheet is near-verbatim port of `.planning/design-handoff/project/styles.css`; Razor components emit `cb-*` classes |
| D-03 | 05-01 | Inter font loads from `rsms.me/inter/inter.css` via `@import`; self-hosting is FUTURE |
| D-04 | 05-01 | Dark-mode keeps existing `body.dark-mode` class approach (not data-theme); preserves `cookbot_dark_mode` localStorage toggle |
| D-05 | 05-01 | Accent variants (`data-accent`) + density modes (`data-density`) attach to `<html>` via `cookbot.applyDefaults()` JS interop |
| D-06 | 05-01 | Razor component prefix is `Cb` (matches `cb-*` CSS); shell-specific components unprefixed (Sidebar/TopBar/NavRow) |
| D-08 | 05-01 | Icon dispatch uses switch + MarkupString to interleave `<path>` children inside `<svg>` |
| D-10 | 05-02 | CbButton variants Primary/Accent/Ghost/Subtle; StartIcon/EndIcon route through `<Icon Name="...">` |
| D-11 | 05-02 | StartIcon/EndIcon/Icon are icon-name strings (not enum) — flexibility, easy to add icons later |
| D-18 | 05-02 | CbBadge uses `cb-badge-*` CSS classes for status tints — dark-mode cascades through `--green-soft` / `--warn-soft` / `--accent-soft` tokens |
| D-19 | 05-05 | MainLayout removes MudLayout/MudAppBar/MudDrawer/MudMainContent/MudContainer (replaced by cb-shell + Sidebar + TopBar + main) — *reinterpreted at execution time* (see D-12 below) |
| D-20 | 05-05 | Sidebar inside `<aside class="side">`; uses `.cb-row` CSS class via NavLink for active state |
| D-21 | 05-05 | TopBar inside `<header class="topbar">`; 56px sticky; user-switcher uses CbDropdown |
| D-22 | 05-05 | NavRow uses `<NavLink ActiveClass="active">` so the active class is route-driven (no JS, no manual matching) |
| D-23 | 05-04 | CbDialog presentational primitive with @bind-IsOpen + Header/Body/Footer slots; MaxWidth/FullWidth/CloseOnEscape/CloseOnScrim parameters |
| D-24 | 05-04 | CbDialogService is Scoped (per-circuit). `ShowAsync<TDialog>` is fire-and-forget invocation of `OnRequest` so awaited Tcs never blocks on host StateHasChanged. Missing-host fail-fast via InvalidOperationException |
| D-25 | 05-04 | CbToastService is Singleton (app-wide). CbToastHost evicts FIFO when count > 3 AND cancels evicted toast's CTS to avoid leaking timers |
| D-26 | 05-04 | CbDropdown uses native `<button>` Tab-order keyboard nav rather than roving-tabindex (simpler, accessible by default; arrow-key roving deferred to Phase 7 A11Y-01) |
| D-27 | 05-04 | `cookbotDialog.bindOutsideClick` defers attaching document mousedown listener via `setTimeout(0)` so the open-click doesn't immediately close it |
| D-29 | 05-05 | AI-off contract preservation — Sidebar reads `UserProfile.AiEnabled` in BOTH OnInitializedAsync (prerender) AND OnAfterRenderAsync (post-circuit) so first paint never flickers |
| D-30 | (cross-plan) | MudBlazor coexists through Phase 5 — package reference, AddMudServices, `@using MudBlazor` in _Imports, MudThemeProvider/Popover/Dialog/Snackbar mounts in MainLayout all preserved. New code imports zero `Mud*` symbols (TopBar carve-out below is the documented exception). Phase 7 MIG slice deletes |
| D-12 | 05-05 | D-30 coexistence reinterpretation of D-19 — MainLayout removes Mud layout chrome (MudLayout/MudAppBar/MudDrawer/MudMainContent/MudContainer) but RETAINS the four Mud providers so unmigrated pages and dialogs keep working. Phase 7 deletes the providers in the terminal cleanup |
| D-13 | 05-05 | Alternative A carve-out — TopBar @inject IDialogService MudDialogService for PasswordPromptDialog + AdminManageUsersDialog because those content components still use `<MudDialog>` internally. Phase 7 migrates launch path AND dialog internals together |
| D-14 | 05-05 | NavMenu.razor deleted now (not at Phase 7) — zero live references after MainLayout rewrite |
| D-15 | 05-05 | Dark-mode icon stays as Sun for both light/dark states. 36-icon set has sun but no moon. Tooltip provides directional cue. Phase 6 polish item |

## What ships into Phase 6/7

**Available for use in Phase 6/7 surface migrations:**

- 36 outline icons via `<Icon Name="..." Size="...">` (Plan 05-01)
- 7 display atoms (Plan 05-02)
- 7 form atoms (Plan 05-03)
- 4 dialog/toast/dropdown primitives + 2 DI services + cb-dialog.js (Plan 05-04)
- New shell — Sidebar / TopBar / MainLayout cb-shell grid (Plan 05-05)
- Token system: warm-cream surfaces, cocoa ink, dialed-back orange accent + sage/terracotta variants, comfy/compact density, dark-mode parity
- Global CbDialogHost + CbToastHost mounts in MainLayout — any new dialog content can `@inject ICbDialogService` and `await DialogService.ShowAsync<TDialog>(...)` from any component

**Verification surfaces:**

- `/design-sandbox` — exercises every token, every icon, every atom, every form atom, every dialog primitive in light + dark modes. Phase 7 deletes this route.

## Deletion targets accumulated for Phase 7 MIG cleanup

The terminal Phase 7 slice deletes:

| Item | Location | Why kept through Phase 5 |
| --- | --- | --- |
| `<MudThemeProvider>` mount | MainLayout.razor | Theme context for unmigrated Mud* surfaces |
| `<MudPopoverProvider>` mount | MainLayout.razor | Popover host for MudSelect/MudAutocomplete on unmigrated pages |
| `<MudDialogProvider>` mount | MainLayout.razor | Dialog host for `<MudDialog>` content (PasswordPromptDialog, AdminManageUsersDialog, CookbookFormDialog, AddPantryItemDialog, etc.) |
| `<MudSnackbarProvider>` mount | MainLayout.razor | Snackbar host for `ISnackbar.Add` call sites in 14+ pages |
| `_theme` field + Mud palette | MainLayout.razor | Drives MudThemeProvider |
| `@inject IDialogService MudDialogService` line + dialog launch paths | TopBar.razor | Alternative A — Phase 7 migrates dialog content to cb-* atoms then deletes |
| `AddMudServices()` call | Program.cs | Required for IDialogService / ISnackbar / theme service DI |
| `@using MudBlazor` | _Imports.razor | Once no Mud* symbol remains anywhere in the tree |
| `MudBlazor` + `MudBlazor.Services` packages | CookBot.Web.csproj | Final removal |
| `_content/MudBlazor/MudBlazor.min.css` + `MudBlazor.min.js` | App.razor | Final removal |
| `/design-sandbox` route + DesignSandbox.razor + SampleDialogContent.razor | Components/Pages/ | Verification surface — once Phase 6/7 surfaces ship, sandbox isn't needed |
| 32 unmigrated pages' `Mud*` internals | Components/Pages/*.razor | Phase 6 migrates marquee surfaces; Phase 7 migrates the rest |

## Phase 5 success criteria

1. **SC#1** — Every existing surface renders inside the new shell (232px sidebar with 4 nav rows + divider + AI rows + spacer + Profile + 56px sticky topbar with user-switcher dropdown + dark-mode toggle). `cookbot_dark_mode` localStorage toggle works in light AND dark themes. **SATISFIED** (Plan 05-05).
2. **SC#2** — Toggling AI off on Profile hides sidebar AI Assistant + Prompt Builder rows immediately (no reload). **SATISFIED** (Plan 05-05 — Sidebar reads UserProfile.AiEnabled in OnInitializedAsync + OnAfterRenderAsync; D-29).
3. **SC#3** — A `<CbDialog>` opened via `CbDialogService.ShowAsync<TDialog>` renders correctly inside the new shell. **SATISFIED** (Plans 05-04 + 05-05 — primitive shipped 04, mount globalized 05).
4. **SC#4** — `<CbDialog>` traps focus, closes on Escape, closes on scrim click, stacks correctly. CbToastService toasts appear bottom-right with severity tints. CbDropdown opens / closes on Escape / closes on outside-click. **SATISFIED** (Plan 05-04).
5. **SC#5** — `dotnet build` succeeds; `dotnet test` passes; MudBlazor package reference still loads (no MIG-01 in Phase 5); zero new code imports `Mud*` symbols UNLESS it's TopBar's intentional `IDialogService` injection (D-13 carve-out). **SATISFIED** across all 5 plans (build clean, tests 196/196 each plan).
6. **SC#6** — Application behavior identical to v1.1 Phase 2 completion EXCEPT the chrome (sidebar/topbar/main) which now matches the design handoff. **SATISFIED** (Plan 05-05 — every invariant from old MainLayout preserved verbatim).

## Phase 5 metrics

| Plan | Duration | Commits | Files Created | Files Modified | Files Deleted |
| --- | --- | --- | --- | --- | --- |
| 05-01 | ~10 min | ~3 | 4 (cookbot-design.css, Icon.razor, cookbot-shell.js, DesignSandbox.razor) | 2 (App.razor, _Imports.razor) | 0 |
| 05-02 | ~7 min | ~3 | 7 atoms (CbButton/Chip/Card/Stat/Eyebrow/Badge/StripedPlaceholder) | 2 (cookbot-design.css, DesignSandbox.razor) | 0 |
| 05-03 | ~6 min | ~4 | 7 form atoms (CbToggle/Checkbox/Radio/Input/Textarea/Select/Option) | 2 (cookbot-design.css, DesignSandbox.razor) | 0 |
| 05-04 | ~6 min | ~5 | 9 (CbDialogService, CbToastService, CbDialog, CbDialogHost, CbToastHost, CbDropdown, CbDropdownItem, SampleDialogContent, cb-dialog.js) | 5 (Program.cs, App.razor, _Imports.razor, cookbot-design.css, DesignSandbox.razor) | 0 |
| 05-05 | ~6 min | 4 | 3 (NavRow, Sidebar, TopBar) | 2 (MainLayout.razor full rewrite, DesignSandbox.razor) | 1 (NavMenu.razor) |
| **Total** | **~35 min** | **~19** | **30** | **13** | **1** |

Build clean (0 warnings, 0 errors) at every plan boundary. Tests at 196/196 baseline at every plan boundary.

## Next phase

**Phase 6** — Marquee surface migrations: Home, Cooking Mode, Recipe View, Recipe Editor (HOME-01..04, COOK-01..06, RV-01..05, ED-01..09 — 19 requirements). Each surface rebuilt against the cb-* atom system shipped in Phase 5; every Mud* internal replaced.

**Phase 7** — Remaining surfaces (CB, PA, GR, AIC, PB, PROF — 17 requirements) + cross-cutting A11Y audit (A11Y-01..04) + terminal MIG slice (MIG-01..03) — package removal, AddMudServices removal, _Imports.razor cleanup, MainLayout Mud-provider removal, sandbox route deletion.
