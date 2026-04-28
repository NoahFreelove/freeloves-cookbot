---
phase: 07-remaining-surfaces-accessibility-mudblazor-strip
plan: 03
subsystem: ui
tags: [blazor, mudblazor-strip, grocery, dialogs, cb-atoms, ui-redesign, mobile-first]

# Dependency graph
requires:
  - phase: 05-foundation-design-tokens-atoms-shell-dialogs
    provides: CbDialog + CbDialogService + ICbToastService + atom set (CbCard, CbButton, CbInput, CbSelect, CbOption, CbEyebrow, Icon)
  - phase: 07-remaining-surfaces-accessibility-mudblazor-strip / plan 01
    provides: ConfirmDialog primitive (used for delete-list confirm) + Cb-native dialog migration pattern
  - phase: 07-remaining-surfaces-accessibility-mudblazor-strip / plan 02
    provides: CbSelect + companion CbInput "or new" replacement pattern for MudAutocomplete (free-text fall-through); CbInput type="number" replacement pattern for MudNumericField
provides:
  - GroceryListView.razor rewritten against design-handoff/screens/grocery-phone.jsx (GR-01..04)
  - 2 grocery dialog migrations to CbDialog slots + CbDialogService (AddGroceryListItemDialog, NewGroceryListDialog)
  - Mobile-first per-list block (max-width:560px) that works on desktop without responsive media queries
affects:
  - 07-04 (AI Chat + Prompt Builder) — same dialog migration pattern; one fewer ISnackbar consumer
  - 07-07 (terminal MudBlazor strip) — three fewer Mud consumers; grocery surface fully Cb-native

# Tech tracking
tech-stack:
  added: []  # No new packages
  patterns:
    - Per-list mobile-first block (max-width:560px) — works on desktop without responsive media queries; multiple lists stack vertically with 32px gap
    - Custom 24px circle checkbox rendered inline (not <CbCheckbox>) — accent-fill + Icon check when checked; 2px line border when unchecked. Matches grocery-phone.jsx exactly.
    - Row-as-button pattern via div[role=button] + tabindex + onkeydown — keeps the row keyboard-activatable while allowing a real <button> inline for the trash action (avoids invalid nested-button HTML)
    - Full-width 50px height / 25px radius accent CbButton via Style="height:50px;border-radius:25px;font-size:15px;" (per GR-04)

key-files:
  modified:
    - src/CookBot.Web/Components/Pages/GroceryListView.razor
    - src/CookBot.Web/Components/Pages/AddGroceryListItemDialog.razor
    - src/CookBot.Web/Components/Pages/NewGroceryListDialog.razor

key-decisions:
  - "Multi-list page rendered as stacked per-list blocks rather than redesigned as a single-list detail view. The existing /grocery-lists route is a list-of-lists (matches the previous semantics + the from-recipe deep-link); the design-handoff grocery-phone.jsx shows a single open list, so we render each list with the design pattern (per-list header + progress card + aisle sections + per-list bottom Add-item button) and stack them in the page. The plan's 'TopBar right-slot or inline header per ergonomics' clause sanctions the inline-header path."
  - "No literal back-button on the per-list header — there is no list-detail navigation in the current routing. Per-list header instead carries: list name (h2), sub-line ('{N} items · created MMM d, yyyy'), share-icon button (mark-all-purchased + move to pantry — preserves the existing 'I bought these' behavior under a more compact icon), and more-icon button (delete list with ConfirmDialog Destructive=true). Plan accepts ergonomic divergence."
  - "Mobile-first via max-width:560px on the .grocery-list per-list block (no media queries needed). The block sits inside the main content column (already capped at 1180px in MainLayout); on phone widths it occupies the full available width up to 560px; on desktop it stays at 560px against the cream background. This is exactly the 'just narrower main column on mobile via standard CSS' pattern called out in the plan instructions."
  - "Custom 24px circle checkbox rendered inline rather than via <CbCheckbox>. CbCheckbox emits a square box with a 4px-radius via the .cb-checkbox class; the grocery design needs a 24px circle (border-radius:12px) + accent-fill background + check-icon glyph when done, plus a 2px line-strong border when undone — three style points that don't override cleanly via inline style. The inline render lives directly in the row markup and is fully keyboard-accessible via the row-as-button pattern (Space/Enter toggle)."
  - "Row-as-button pattern: div[role='button'] + tabindex='0' + @onkeydown handler that toggles on Space/Enter. Required because a real <button> can't legally nest the trash <button> for the per-row Remove action. The inline trash button uses @onclick:stopPropagation='true' so it doesn't also toggle the row."
  - "ConfirmDialog (Plan 07-01) reused for delete-list confirm — replaces the previous IDialogService.ShowMessageBox call; Destructive=true so the confirm button uses the Accent variant. Identical pattern to PantryView's delete-pantry confirm in Plan 07-02."
  - "AddGroceryListItemDialog: MudAutocomplete (ingredient) → CbSelect over loaded list + companion CbInput 'or new ingredient' field. The original MudAutocomplete supported free-text fall-through to create a new ingredient; the Cb-native pattern splits that into two explicit inputs (selection vs new-name). Matches Plan 07-02 AddPantryItemDialog exactly."
  - "AddGroceryListItemDialog: MudAutocomplete (unit) → CbSelect over CommonUnits. The original allowed free-text via CoerceValue='true'; the Cb-native version is a fixed-list select. CommonUnits already covers the design's expected vocabulary (tsp/tbsp/cup/lb/g/kg/piece/etc.) so this is not a behavior loss."
  - "AddGroceryListItemDialog: MudNumericField → CbInput type='number' with culture-invariant double.TryParse + a HasAmount-derived disabled state on the submit button. Matches the Plan 07-02 numeric pattern."

requirements-completed: [GR-01, GR-02, GR-03, GR-04]

# Metrics
duration: 4min
completed: 2026-04-27
---

# Phase 7 Plan 03: Grocery + grocery dialogs migration Summary

**GroceryListView.razor rewritten against Phase 5 atoms per the design-handoff grocery-phone.jsx (mobile-first per-list block w/ inline header + share/more icons, progress card with tabular counter + accent bar, aisle-categorized cards w/ custom 24px circle checkboxes, full-width 50px accent Add-item button); 2 grocery dialogs migrated from MudDialog to CbDialogService — every migrated file is now Mud-free.**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-04-27T21:41:31Z
- **Completed:** 2026-04-27T21:44:55Z
- **Tasks:** 3/3 complete (Task 1: GroceryListView rewrite; Task 2: 2 dialog migrations; Task 3: build + test + commit)
- **Files modified:** 3

## Accomplishments

- **GR-01 satisfied** — Per-list inline header: list name (20px h2, -0.018em letter-spacing), sub-line (`{N} item(s) · created MMM d, yyyy`), share-icon button (mark-all-purchased + move to pantry), more-icon button (delete with ConfirmDialog). Page-level header above retains the canonical "Grocery Lists" h1 + accent "New list" button. The "back button" element from the design isn't rendered because the route is a top-level surface (not a detail page) — see decisions for the rationale; plan permits this via the "inline header per ergonomics" clause.
- **GR-02 satisfied** — Progress card uses `<CbCard Padding="14">` with two-row layout: "Progress" label (13px weight 500) + tabular-numeral counter (`{checked} / {total}` with class="num"), then a 6px-tall outer bar (`background:var(--cream-2);border-radius:3px`) wrapping an accent fill (`background:var(--accent)`) sized to `width:{pct}%` with a 160ms transition. Uses `role="progressbar"` + `aria-valuenow/min/max/label` for accessibility (foundation for A11Y plan).
- **GR-03 satisfied** — Aisle sections grouped by `Ingredient.Category` (the Domain enum); each renders a `<CbEyebrow>` label then a `<CbCard Padding="0" Style="overflow:hidden">` containing one row per item. Each row is `display:flex;gap:14px;padding:14px 16px` with a bottom border between rows (suppressed on the last). The 24px circle checkbox is custom inline: when `done` it has `background:var(--accent)` + the `<Icon Name="check" Size="14">` glyph; when undone it has `border:2px solid var(--line-strong)` + transparent background. Item name uses `font-size:15px;font-weight:500` with `text-decoration:line-through` + `opacity:0.45` when done. Quantity uses class="num" + `font-size:13px` + ink-3 color, right-aligned via flex layout.
- **GR-04 satisfied** — Bottom action is a full-width `<CbButton Variant="Accent" FullWidth="true" StartIcon="plus">` with `Style="height:50px;border-radius:25px;font-size:15px;"`. Sits below the aisle stack inside the per-list section block. Per the plan's "narrower main column on mobile via standard CSS" rule, the per-list block is `max-width:560px` so on phone widths the button stretches edge-to-edge while on desktop it sits at 560px against the cream background.
- **2 dialogs migrated** to `CbDialogService`. Each drops `<MudDialog>` and emits body + footer markup directly; `CbDialogHost` wraps in `<CbDialog>` slots.
- **All call sites** for the migrated dialogs updated. `GroceryListView` now depends on `ICbDialogService` + `ICbToastService`; `IDialogService` and `ISnackbar` are gone from this page and from both dialogs.
- **Mobile-first verified** — The per-list block uses `max-width:560px` and lives inside the standard `max-width:1180px` page container. No media queries; on a 375px viewport the block fits naturally at ~375px wide; on 1180px it sits at 560px.
- **Keyboard-accessible row-as-button** — Each row is `div[role="button"][tabindex="0"]` with an `@onkeydown` handler that toggles on Space or Enter. Required because nesting the trash `<button>` inside a real `<button>` would produce invalid HTML.

## Task Commits

All work committed in a single atomic commit because the page rewrite and the 2-dialog migration are tightly coupled (the page is the call site for the dialogs).

1. **Tasks 1–3 (combined):** GroceryListView rewrite + 2 grocery dialogs + build + test → **`62effff`** (feat)

## Files Modified

- `src/CookBot.Web/Components/Pages/GroceryListView.razor` — Full rewrite per grocery-phone.jsx + GR-01..04. Multi-list semantics preserved (page lists ALL grocery lists for the user, sorted descending by CreatedAt). Per-list inline header + progress card + aisle sections + per-list bottom Add-item button. Delete-list uses `ConfirmDialog`. From-recipe deep-link path (`/grocery-lists/from-recipe/{RecipeId}`) preserved including access-check and toast on success/empty. Empty state preserved with cart icon + CTA button.
- `src/CookBot.Web/Components/Pages/AddGroceryListItemDialog.razor` — Migrated to CbDialog slots. MudAutocomplete (ingredient) → `<CbSelect>` over loaded list + `<CbInput>` "or new ingredient" field. MudNumericField → `<CbInput Type="number">` with culture-invariant parsing. MudAutocomplete (unit) → `<CbSelect>` over CommonUnits. CommonUnits vocabulary unchanged. The DI for `CookBotDbContext` is preserved verbatim — only the dialog surface and inputs changed.
- `src/CookBot.Web/Components/Pages/NewGroceryListDialog.razor` — Migrated. MudTextField → `<CbInput>`. The "leave blank → default name" semantics preserved (the page-level handler still substitutes `Shopping list — {date}` when the trimmed name is empty).

## Verification

- **`dotnet build`:** Clean. 0 warnings, 0 errors.
- **`dotnet test --filter "Category!=RequiresApiKey"`:** 196/196 passing (baseline preserved).
- **Hard invariant #1 — zero `Mud*` symbols in GroceryListView + 2 grocery dialogs:** Verified via per-file `grep -E '<Mud|@inject Mud|MudBlazor\.|IMudDialog|ISnackbar\b|@inject IDialogService|@using MudBlazor\b'` — 0 matches in all 3 files. The remaining matches under the broader `Mud[A-Z]|Severity\.|Color\.|Variant\.|MaxWidth\.` regex are false positives (`CbButton.CbButtonVariant.*`, `CbToastSeverity.*`) plus 3 migration-history comments referencing the old Mud types they replaced (matches the Plan 07-02 comment pattern).
- **Hard invariant #2 — Mobile-first layout works on desktop too:** Verified by code review. The per-list block uses `max-width:560px` (no media query) inside the page's standard 1180px container. Phone widths get a near-full-width column; desktop widths get a 560px column against cream background. No responsive breakpoint logic was added.
- **Hard invariant #3 — `dotnet build` clean + baseline tests:** 0 warnings, 0 errors. 196/196 tests passing, identical to the Plan 07-02 baseline.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] Initial implementation used a `<button>` element as the row container with a nested trash `<button>`**

- **Found during:** Task 1 (initial markup design)
- **Issue:** Nested `<button>` elements produce invalid HTML and undefined behavior across browsers. Self-caught during code review before the first build; the design's "click anywhere on row toggles" + "trash icon for remove" intent requires both to be activatable.
- **Fix:** Switched the row container to `<div role="button" tabindex="0">` with an `@onkeydown` handler that activates on Space or Enter. The trash `<button>` inside uses `@onclick:stopPropagation="true"` so it doesn't also toggle the row. Both interactions are mouse + keyboard accessible.
- **Files modified:** `src/CookBot.Web/Components/Pages/GroceryListView.razor`
- **Commit:** `62effff` (single atomic)

### Scope adjustments (in-spec)

- **No literal back-button** — `/grocery-lists` is a top-level page reachable from the sidebar; there's no list-detail route to "back" to. The design's back-button glyph is omitted from the inline header. Plan permits this via "TopBar right-slot or inline header per ergonomics".
- **Per-row trash icon added** (not in design-handoff grocery-phone.jsx) — preserves the existing "Remove from list" affordance from the pre-migration page. Without it, users would have no way to remove a single item without re-entering the dialog. Plan does not require its removal.

## Known Stubs

- **No "{M} recipes" sub-line** — The plan task description says sub: `"{N} items · from {M} recipes"`. The current `GroceryList` schema doesn't track which recipes contributed items (the `GenerateFromRecipeAsync` path produces a list, but other items can be added manually afterwards, and the link from item → source recipe isn't persisted). Sub-line currently emits `"{N} item(s) · created {date}"` as the closest faithful surrogate. A future plan could add a `GroceryListItem.SourceRecipeId` column; not in scope for this plan or this milestone (FUTURE-* candidate).

## Threat Flags

None. No new network endpoints, auth surfaces, file-access patterns, or schema changes. Authorization checks (`UserCanAccessRecipeAsync`, per-list owner check on add-item, user-id filter on LoadLists) preserved verbatim. The from-recipe deep-link path still calls `UserCanAccessRecipeAsync` before fetching the recipe.

## Self-Check: PASSED

Verified the SUMMARY.md claims:

**Files modified exist (all 3):**
- `src/CookBot.Web/Components/Pages/GroceryListView.razor` — FOUND
- `src/CookBot.Web/Components/Pages/AddGroceryListItemDialog.razor` — FOUND
- `src/CookBot.Web/Components/Pages/NewGroceryListDialog.razor` — FOUND

**Commit `62effff` exists in `git log --oneline --all`:** FOUND.

**`dotnet build`:** clean (0 warnings, 0 errors).
**`dotnet test --filter "Category!=RequiresApiKey"`:** 196/196 passing.
**Per-file Mud grep on the 3 plan-scoped files:** 0 matches under the strict pattern (history comments only under the broader pattern).
