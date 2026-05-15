---
phase: 07-remaining-surfaces-accessibility-mudblazor-strip
plan: 02
subsystem: ui
tags: [blazor, mudblazor-strip, pantry, dialogs, cb-atoms, ui-redesign, ai-off-contract]

# Dependency graph
requires:
  - phase: 05-foundation-design-tokens-atoms-shell-dialogs
    provides: CbDialog + CbDialogService + ICbToastService + atom set (CbCard, CbButton, CbInput, CbTextarea, CbSelect, CbOption, CbBadge, Icon)
  - phase: 07-remaining-surfaces-accessibility-mudblazor-strip / plan 01
    provides: ConfirmDialog primitive (used for delete-pantry confirm) + Cb-native dialog migration pattern
provides:
  - PantryView.razor rewritten against design-handoff/screens/pantry.jsx (PA-01..04)
  - 5 pantry dialog migrations to CbDialog slots + CbDialogService (AddPantryItemDialog, CreateSharedPantryDialog, ManagePantryMembersDialog, AiPopulatePantryDialog, AiStandardizePantryDialog)
  - AI-off contract verified for both pantry AI tools (populate + standardize) — buttons hidden when CookBotSettings.AiFeaturesEnabled || UserProfile.AiEnabled || effective AI credentials are missing
affects:
  - 07-03 (Grocery) — same dialog migration pattern; one fewer ISnackbar consumer
  - 07-07 (terminal MudBlazor strip) — five fewer Mud consumers; pantry surfaces fully Cb-native

# Tech tracking
tech-stack:
  added: []  # No new packages
  patterns:
    - Native HTML <input type="date"> as Cb-native replacement for MudDatePicker (no Cb date atom yet)
    - CbSelect over loaded ingredient list + companion CbInput "or new" field as a replacement for MudAutocomplete (no Cb autocomplete atom yet)
    - CbInput type="number" with culture-invariant double parsing as a replacement for MudNumericField
    - Multi-pantry pill switcher (CSS-only role="tablist"/aria-selected) as a replacement for MudTabs

key-files:
  modified:
    - src/CookBot.Web/Components/Pages/PantryView.razor
    - src/CookBot.Web/Components/Pages/AddPantryItemDialog.razor
    - src/CookBot.Web/Components/Pages/CreateSharedPantryDialog.razor
    - src/CookBot.Web/Components/Pages/ManagePantryMembersDialog.razor
    - src/CookBot.Web/Components/Pages/AiPopulatePantryDialog.razor
    - src/CookBot.Web/Components/Pages/AiStandardizePantryDialog.razor

key-decisions:
  - "MudAutocomplete (ingredient picker) replaced by CbSelect over the eagerly-loaded ingredient list, plus a companion CbInput 'or add new ingredient' field. The original MudAutocomplete supported free-text fall-through to create a new ingredient; the Cb-native pattern splits that into two explicit inputs (selection vs new-name). No Cb autocomplete atom is needed for this plan; FUTURE-* if desired."
  - "MudNumericField replaced by CbInput type='number' with culture-invariant double.TryParse + a HasAmount-derived disabled state on the unit CbSelect. This preserves the original 'leave blank to track only that you have it' UX without adding a Cb numeric atom."
  - "MudDatePicker replaced by native <input type='date'> styled with class='cb-input' (matches the .cb-input visual baseline). No Cb date atom is needed; modern browsers ship native date pickers."
  - "MudTabs (per-pantry switcher) replaced by a small inline pill row using role='tablist' + aria-selected; identical pattern to ShareCookbookDialog's segmented buttons in Plan 07-01. Active pill uses var(--cream-2); inactive is transparent ink-3."
  - "MudDataGrid (per-row table) replaced by manual CSS-grid rows inside a CbCard with Padding=0 and overflow:hidden. Row grid is 1fr 120px 110px 80px (item / qty / status / actions) per pantry.jsx and PA-04. Last-row border suppression matches the design handoff."
  - "Item status (in-stock / low / expiring / out) computed via a static StatusOf(PantryItem) helper. 'Out' = measured row with Amount<=0. 'Expiring' = ExpirationDate within 7 days (or already past). 'Low' = measured row below a per-unit heuristic threshold (kg/liter<0.5, g/ml<100, cup/pint/quart<1, pieces<2, etc.). The heuristic is intentionally conservative; smart pantry-match (FUTURE-13) will replace it with user-configurable thresholds."
  - "Cart-icon row action wired as a disabled affordance for now (title='Add to grocery list'). Grocery-list quick-add lives in Plan 07-03; wiring it here would create a circular dep. The trash icon (delete from pantry) is fully wired."
  - "ConfirmDialog (Plan 07-01) reused for delete-pantry confirm — replaces the previous IDialogService.ShowMessageBox call; Destructive=true so the confirm button uses the Accent variant."
  - "Owner-actions row (manage-members + delete-pantry) preserved exactly per the existing PantryView semantics; both moved into the Cb action-row pattern. Manage-members trash icons replace the MudIconButton 'Close' icon (visually clearer for 'remove member')."

requirements-completed: [PA-01, PA-02, PA-03, PA-04]

# Metrics
duration: 6min
completed: 2026-04-27
---

# Phase 7 Plan 02: Pantry + pantry dialogs migration Summary

**PantryView.razor rewritten against Phase 5 atoms per the design-handoff pantry.jsx (top-bar actions w/ AI-off hide contract, sub-line "{N} items · last sync {ago}", 4-tile summary strip, search row + filter buttons, categorized stock cards w/ CbBadge status pills); 5 pantry dialogs migrated from MudDialog to CbDialogService — every migrated file is now Mud-free.**

## Performance

- **Duration:** ~6 min
- **Started:** 2026-04-27T21:30:10Z
- **Completed:** 2026-04-27T21:36:25Z
- **Tasks:** 3/3 complete (Task 1: PantryView rewrite; Task 2: 5 dialog migrations; Task 3: build + test + commit)
- **Files modified:** 6

## Accomplishments

- **PA-01 satisfied** — Top-bar action row: `Create shared pantry` + `AI standardize` + `AI populate` (last two hidden when `_aiPantryToolsVisible` is false; that flag combines `CookBotSettings.AiFeaturesEnabled`, `UserProfile.AiEnabled`, and effective credentials presence — same gate that the previous version used). `Add item` action uses `CbButton accent`. Sub-line emits `{N} items · last sync {ago}` from `_pantryItems.Count` + `Max(item.UpdatedAt)` + a `FormatAgo` helper that maps to "just now / Xm ago / Xh ago / Xd ago / MMM d" buckets.
- **PA-02 satisfied** — 4-tile summary strip rendered as a 4-col grid of `<CbCard Padding=16>` tiles. Each tile has an 8×36 colored vertical bar (green / warn / accent / ink-3), tabular value (26px, font-variant-numeric: tabular-nums, letter-spacing: -0.02em), and label (11.5px ink-3). Counts come from `RecomputeSummary()` which iterates `_pantryItems` and bins by `StatusOf`.
- **PA-03 satisfied** — Search row uses a 360px-max rounded `<input>` (with leading search icon) plus three filter `cb-btn ghost` buttons (All / Low only / Expiring) wired to `_filter` and `_searchText`. Search filters by `Ingredient.Name` Contains; the active filter pill gets `background: var(--cream-2)`.
- **PA-04 satisfied** — Stock cards grouped by `Ingredient.Category` (with a stable display order: Pantry → Grains → Canned → Spices → Condiments → Produce → Dairy → Meat → Seafood → Bakery → Frozen → Beverages → Snacks → Other). Each category renders an eyebrow row (h3 + count) + a `<CbCard Padding=0 overflow:hidden>` containing one row per item. Row grid is `1fr 120px 110px 80px` (name / quantity / `<CbBadge>` / cart-icon + trash-icon). Expiring rows tint with `rgba(194,65,12,0.04)` (accent-soft @ 4%). Status mapping → `CbBadgeStatus.InStock / Low / Expiring / Out`; expiring badges show a dynamic label ("expires in 3d" / "expires today" / "expired").
- **AI-off contract** — Both `AI standardize` and `AI populate` buttons are gated by `_aiPantryToolsVisible`. The dialog launchers also re-validate `_aiCredentials != null` and surface a Cb toast if credentials disappear between page load and click. Audited end-to-end: with `UserProfile.AiEnabled = false`, neither button renders and neither dialog can be opened from this page.
- **5 dialogs migrated** to `CbDialogService`. Each drops `<MudDialog>` and emits body + footer markup directly; `CbDialogHost` wraps in `<CbDialog>` slots. AI-related dialogs (`AiPopulatePantryDialog`, `AiStandardizePantryDialog`) preserve their AI integration logic verbatim — only the markup changed.
- **All call sites** for the migrated dialogs updated. `PantryView` now depends on `ICbDialogService` + `ICbToastService`; `IDialogService` and `ISnackbar` are gone from this page and from all five dialogs.

## Task Commits

All work committed in a single atomic commit because the page rewrite and the 5-dialog migration are tightly coupled (the page is the call site for the dialogs).

1. **Tasks 1–3 (combined):** PantryView rewrite + 5 pantry dialogs + build + test → **`18507ea`** (feat)

## Files Modified

- `src/CookBot.Web/Components/Pages/PantryView.razor` — Full rewrite per pantry.jsx + PA-01..04. Multi-pantry switcher (personal + shared) preserved; manage-members + delete-pantry preserved on owned pantries; `ConfirmDialog` used for delete-pantry confirm.
- `src/CookBot.Web/Components/Pages/AddPantryItemDialog.razor` — Migrated to CbDialog slots. MudAutocomplete (ingredient) → `<CbSelect>` over loaded list + `<CbInput>` "or new ingredient" field. MudNumericField → `<CbInput Type="number">` with culture-invariant parsing. MudDatePicker → native `<input type="date">`.
- `src/CookBot.Web/Components/Pages/CreateSharedPantryDialog.razor` — Migrated. ISnackbar → ICbToastService.
- `src/CookBot.Web/Components/Pages/ManagePantryMembersDialog.razor` — Migrated. Member list redesigned as cream-2 tile rows w/ trash icon (clearer "remove" affordance than the original Close-icon). MudSelect → `<CbSelect>`. ISnackbar → ICbToastService.
- `src/CookBot.Web/Components/Pages/AiPopulatePantryDialog.razor` — Migrated. MudTextField (8-line) → `<CbTextarea Rows=8>`. ISnackbar → ICbToastService. AI integration logic (`PantryAi.PopulatePantryAsync(...)`) is byte-identical.
- `src/CookBot.Web/Components/Pages/AiStandardizePantryDialog.razor` — Migrated. MudAlert → Cb-native `<div role="alert">` with warn-soft tint. ISnackbar → ICbToastService. AI integration logic (`PantryAi.StandardizePantryAsync(...)`) is byte-identical.

## Verification

- **`dotnet build`:** Clean. 0 warnings, 0 errors.
- **`dotnet test --filter "Category!=RequiresApiKey"`:** 196/196 passing (baseline preserved).
- **Hard invariant #1 — zero `Mud*` symbols in PantryView + 5 pantry dialogs:** Verified via per-file `grep -E '<Mud|@inject Mud|MudBlazor\.|IMudDialog|ISnackbar\b|@inject IDialogService|@using MudBlazor\b'` — 0 matches in all 6 files. The remaining matches under the broader `Mud[A-Z]|MaxWidth\.` regex are false positives (`CbDialogResult`, `CbDialogMaxWidth.Sm`, and migration-history comments referencing the old Mud types they replaced).
- **Hard invariant #2 — AI buttons hidden when AI off:** Code review verified both buttons live behind `@if (_aiPantryToolsVisible)`; the flag is recomputed on every user-id transition by combining `CookBotSettings.AiFeaturesEnabled` + `UserProfile.AiEnabled` + `_aiCredentials != null`. No `_aiPantryToolsVisible = true` short-circuit anywhere.
- **Hard invariant #3 — AI integration logic preserved verbatim:** `AiPopulatePantryDialog.Submit()` and `AiStandardizePantryDialog.Submit()` call `PantryAi.PopulatePantryAsync(...)` and `PantryAi.StandardizePantryAsync(...)` with identical argument lists (`PantryId`, text or `CurrentItems`, `ApiKey`, `ModelId`, `Profile.UnitSystem`, `Profile.AiUnitExceptions`). Result handling (success/error branching, per-message toasts, dialog close on success) is structurally identical to the pre-migration version. Only the surface that displays results changed (ISnackbar → ICbToastService).
- **Hard invariant #4 — `dotnet test` baseline:** 196/196 passing, identical to Plan 07-01 baseline.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] Razor parse error on first build**

- **Found during:** Task 3 (`dotnet build`)
- **Issue:** Inside the `@if (_activePantry is { } pantry) { ... }` block, I initially wrote `@{ var visibleItems = ApplyFilters(...).ToList(); }` to compute the per-render filtered + grouped lists. Razor reports `RZ1010 Unexpected "{" after "@" character` because we're already inside a code block (the outer `@if`); inside an `@if {...}` you write C# directly without the `@{}` transition.
- **Fix:** Replaced `@{ … }` with bare `var x = …;` lines inside the `@if`. Replaced inner `@foreach` and `@for` calls (also implicitly inside the `@if`) with bare `foreach`/`for` where applicable. Established that the inner `@for` loop CAN keep its `@` because it's at a position where the parser is ambiguous between markup and code; bare `for` works equivalently and matches Plan 07-01's CookbookDetail pattern.
- **Files modified:** `src/CookBot.Web/Components/Pages/PantryView.razor`
- **Commit:** `18507ea`

### Scope adjustments (in-spec)

- **Cart icon button (per-row "Add to grocery list") wired as a disabled visual affordance** — actual grocery-list quick-add wiring lives in Plan 07-03; doing it here would create a circular dep. The icon renders with `cursor:not-allowed` + `title="Add to grocery list"` + `disabled` so users see the affordance without broken behavior.

## Known Stubs

- **Per-row cart-icon button (Add to grocery list)** — disabled affordance, not wired. Resolved in Plan 07-03 (Grocery).
- **Item-status thresholds** — heuristic in `IsLowAmount(amount, unit)` is conservative (kg<0.5, cup<1, pieces<2, etc.). Smart pantry-match with user-configurable thresholds is FUTURE-13.
- **"Sweep expired" / "auto-deduct on cook"** — not in design-handoff; NOT a stub for this plan, just calling out scope.

## Threat Flags

None. No new network endpoints, auth surfaces, file-access patterns, or schema changes. Authorization checks (`PantryService.TryDeleteOwnedPantryAsync`, `PantryService.AddOrUpdateAsync`, `PantryService.AddMemberAsync`/`RemoveMemberAsync`) preserved verbatim by the application service. AI calls (`PantryAi.PopulatePantryAsync` / `StandardizePantryAsync`) use the existing per-user `EffectiveAiCredentials` (resolved via `AiApiKeyResolutionService`) — same trust boundary as before.

## Self-Check: PASSED

Verified the SUMMARY.md claims:

**Files modified exist (all 6):**
- `src/CookBot.Web/Components/Pages/PantryView.razor` — FOUND
- `src/CookBot.Web/Components/Pages/AddPantryItemDialog.razor` — FOUND
- `src/CookBot.Web/Components/Pages/CreateSharedPantryDialog.razor` — FOUND
- `src/CookBot.Web/Components/Pages/ManagePantryMembersDialog.razor` — FOUND
- `src/CookBot.Web/Components/Pages/AiPopulatePantryDialog.razor` — FOUND
- `src/CookBot.Web/Components/Pages/AiStandardizePantryDialog.razor` — FOUND

**Commit `18507ea` exists in `git log --oneline --all`:** FOUND.

**`dotnet build`:** clean (0 warnings, 0 errors).
**`dotnet test --filter "Category!=RequiresApiKey"`:** 196/196 passing.
**Per-file Mud grep on the 6 plan-scoped files:** 0 matches under the strict pattern.
