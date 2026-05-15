---
phase: 05-foundation-design-tokens-atoms-shell-dialogs
plan: 02
subsystem: design-system
tags:
  - design-system
  - atoms
  - razor
requires:
  - cookbot-design.css token surface (Plan 05-01)
  - Icon Razor component (Plan 05-01)
  - /design-sandbox route with ATOMS_INSERTION_POINT sentinel (Plan 05-01)
provides:
  - CbButton (4 variants, StartIcon/EndIcon, FullWidth, Disabled, Type, OnClick) — ATOM-01
  - CbChip (4 variants, Icon, Label/ChildContent) — ATOM-02
  - CbCard (Padding, Style, ChildContent) — ATOM-03
  - CbStat (Label, Value, Sub; tabular numerals) — ATOM-04
  - CbEyebrow (ChildContent) — ATOM-05
  - StripedPlaceholder (Width, Height, BorderRadius, Label, Style) — ATOM-06 / DS-06
  - CbBadge (4 status variants with default labels) — ATOM-08
  - cb-badge-* CSS rules in cookbot-design.css
  - Sandbox Atoms section demonstrating every variant
affects:
  - src/CookBot.Web/wwwroot/css/cookbot-design.css (CSS appended after dark-mode block)
  - src/CookBot.Web/Components/Pages/DesignSandbox.razor (ATOMS_INSERTION_POINT replaced)
tech-stack:
  added: []
  patterns:
    - Pure Razor components emitting cookbot-design.css classes — zero MudBlazor symbols (D-30)
    - Variant-as-enum nested inside the component class (e.g. CbButton.CbButtonVariant.Primary)
    - Icon-name string parameters (StartIcon/EndIcon/Icon) routed through <Icon Name="..." /> (D-10/D-11)
    - CSS class composition — CbBadge layers cb-badge / cb-badge-* on top of cb-chip geometry
key-files:
  created:
    - src/CookBot.Web/Components/Atoms/CbButton.razor
    - src/CookBot.Web/Components/Atoms/CbChip.razor
    - src/CookBot.Web/Components/Atoms/CbCard.razor
    - src/CookBot.Web/Components/Atoms/CbStat.razor
    - src/CookBot.Web/Components/Atoms/CbEyebrow.razor
    - src/CookBot.Web/Components/Atoms/CbBadge.razor
    - src/CookBot.Web/Components/Atoms/StripedPlaceholder.razor
  modified:
    - src/CookBot.Web/wwwroot/css/cookbot-design.css
    - src/CookBot.Web/Components/Pages/DesignSandbox.razor
decisions:
  - CbBadge uses cb-badge-* CSS classes (appended to cookbot-design.css) rather than inline styles, so dark-mode tints cascade automatically through --green-soft / --warn-soft / --accent-soft (D-18 acceptable variant)
  - Variant enums are nested inside the owning component class to keep the public surface scoped (e.g. CbButton.CbButtonVariant) — matches plan API examples
  - StripedPlaceholder.NormalizeDim accepts %, px, rem, em, vh, vw suffixes; bare numerics are treated as px so callers can pass "180" or "100%" interchangeably
  - CbBadge.Label is now optional with status-derived defaults ("in stock", "running low", "expiring", "out") per executor task spec
metrics:
  duration: ~2 min
  completed: 2026-04-27
requirements:
  - ATOM-01
  - ATOM-02
  - ATOM-03
  - ATOM-04
  - ATOM-05
  - ATOM-06
  - ATOM-08
  - DS-06
---

# Phase 5 Plan 02: Display atoms — CbButton/CbChip/CbCard/CbStat/CbEyebrow/CbBadge/StripedPlaceholder Summary

Seven custom Razor display atoms shipped, each rendering the existing `cb-*` classes from `cookbot-design.css` (Plan 05-01) — no inline styles for anything tokenized, no MudBlazor symbols in any new file. The `<!-- ATOMS_INSERTION_POINT -->` sentinel in `/design-sandbox` is replaced with a section that exercises every variant of every atom; the `FORMS_INSERTION_POINT` and `DIALOGS_INSERTION_POINT` sentinels are preserved for plans 05-03 and 05-04. `dotnet build` clean (0 warnings, 0 errors); `dotnet test --filter "Category!=RequiresApiKey"` baseline preserved (196/196 passed).

## What shipped

### `CbButton.razor` (ATOM-01 / D-10)

Four variants via `CbButton.CbButtonVariant`:

| Variant | Class            | Visual                                         |
| ------- | ---------------- | ---------------------------------------------- |
| Primary | `cb-btn`         | Cocoa fill (default — `--ink` bg, cream text) |
| Accent  | `cb-btn accent`  | Orange fill (`--accent` bg, white text)       |
| Ghost   | `cb-btn ghost`   | Transparent + line-strong border              |
| Subtle  | `cb-btn subtle`  | Light cocoa-tinted fill                       |

Public parameters: `Variant`, `StartIcon` (string — icon name), `EndIcon` (string), `FullWidth` (bool), `Disabled` (bool), `Type` (string, default `"button"`), `OnClick` (`EventCallback`), `ChildContent` (`RenderFragment`), `Style` (string, optional appended). `StartIcon` / `EndIcon` resolve through `<Icon Name="@StartIcon" Size="14" />`. `FullWidth=true` adds `width:100%;justify-content:center;` to the inline style.

### `CbChip.razor` (ATOM-02 / D-11)

Four variants via `CbChip.CbChipVariant`: `Default`, `Timer`, `Ing`, `Tag`. Class mapping: `cb-chip` / `cb-chip timer` / `cb-chip ing` / `cb-chip tag`. Optional `Icon` (string — rendered at Size 12) and `Label` OR `ChildContent`. ChildContent wins when both are set.

### `CbCard.razor` (ATOM-03 / D-12)

`<div class="cb-card" style="padding:{Padding}px;{Style}">` wrapping `ChildContent`. Default `Padding=22`. Optional `Style` slot for layout overrides (e.g. `grid-column: 1 / -1`).

### `CbStat.razor` (ATOM-04 / D-13)

Renders the `.cb-stat` tile: a tabular-numeral value (`<div class="v num">`) plus a `.l` label, with an optional sub-line (`Sub`). `Value` is a string so callers can format ("128", "47", "42 / 50") freely. `Label` and `Value` are `[EditorRequired]`.

### `CbEyebrow.razor` (ATOM-05 / D-14)

`<div class="eyebrow">{ChildContent}</div>` — uppercase + tracking + ink-3 color via the existing `.eyebrow` class.

### `StripedPlaceholder.razor` (ATOM-06 / DS-06 / D-15)

Renders `.cb-ph` diagonal-stripe + dashed-border tile. Width / Height accept `"100%"`, `"180px"`, or bare numeric `"180"` (treated as px). Default `BorderRadius=10` matches the handoff `Stripe` helper. Default `Label="photo"`.

### `CbBadge.razor` (ATOM-08 / D-18)

Four `CbBadge.CbBadgeStatus` values: `InStock`, `Low`, `Expiring`, `Out`. Builds on `.cb-chip` geometry; status-specific tint via the new `.cb-badge-*` CSS classes:

| Status   | bg                       | color           | Default label |
| -------- | ------------------------ | --------------- | ------------- |
| InStock  | `--green-soft`           | `--green`       | "in stock"    |
| Low      | `--warn-soft`            | `--warn`        | "running low" |
| Expiring | `--accent-soft`          | `--accent-ink`  | "expiring"    |
| Out      | `rgba(60,42,18,0.05)`    | `--ink-3`       | "out"         |

`Label` is optional — when omitted, the status-derived default is used. Dark-mode is automatic for in-stock / low / expiring (the soft tokens are redefined in the existing `body.dark-mode` block); a single explicit override handles the Out variant for dark mode.

### CSS appended to `cookbot-design.css`

Seven CSS rules added at the very end of the file (after the existing `body.dark-mode .cb-ph::after` rule):

```css
.cb-chip.cb-badge { font-weight: 600; }
.cb-chip.cb-badge.cb-badge-in-stock { background: var(--green-soft); color: var(--green); }
.cb-chip.cb-badge.cb-badge-low      { background: var(--warn-soft); color: var(--warn); }
.cb-chip.cb-badge.cb-badge-expiring { background: var(--accent-soft); color: var(--accent-ink); }
.cb-chip.cb-badge.cb-badge-out      { background: rgba(60,42,18,0.05); color: var(--ink-3); }

body.dark-mode .cb-chip.cb-badge.cb-badge-out {
  background: rgba(239,235,233,0.06);
  color: var(--ink-3);
}
```

### Sandbox demo section

`<!-- ATOMS_INSERTION_POINT -->` replaced with six `<section>` blocks (each labelled with a `<CbEyebrow>`):

1. **CbButton** — all four variants + StartIcon (Plus, Share) + a Save/ArrowR pair + a disabled Primary + a full-width Accent
2. **CbChip** — all four variants (Timer chip carries the Clock icon + "25 min"; Ing chip carries Pantry icon + "3 cups flour")
3. **CbCard + CbEyebrow** — two cards in a grid (default padding vs Padding=12) each containing an eyebrow + title + body
4. **CbStat** — four tiles in a grid (Recipes 128 +3 this week / Cookbooks 7 / Pantry 47 / Grocery 12)
5. **StripedPlaceholder** — one tile at Width=100% / Height=180 / Label=hero photo
6. **CbBadge** — all four statuses (Expiring overrides default with "expires Friday")

The `FORMS_INSERTION_POINT` and `DIALOGS_INSERTION_POINT` sentinels remain in the file at exactly their previous positions — Plans 05-03 and 05-04 will replace them.

## Verification

- **`dotnet build src/CookBot.Web/CookBot.Web.csproj -c Debug --nologo`** — PASSED (0 warnings, 0 errors, 3.83 s).
- **`dotnet test --filter "Category!=RequiresApiKey" --nologo`** — PASSED (196/196, 1 s). Pre-existing `RequiresApiKey`-gated AI fixture tests are untouched (ANTHROPIC_API_KEY environment baseline).
- **Plan automated-verify clauses** — PASSED:
  - Task 1: `CbButton.razor`, `CbCard.razor`, `CbEyebrow.razor` exist; CbButton uses `cb-btn` class and exports `CbButtonVariant`; none of the three import MudBlazor.
  - Task 2: `CbChip.razor`, `CbStat.razor`, `StripedPlaceholder.razor`, `CbBadge.razor` exist; all four `cb-badge-*` rules present in `cookbot-design.css`; none of the four import MudBlazor.
  - Task 3: All seven atom tags appear in `DesignSandbox.razor`; `FORMS_INSERTION_POINT` and `DIALOGS_INSERTION_POINT` sentinels preserved; `dotnet build` succeeds.
- **Hard invariants:**
  - Zero `Mud*` symbols in `src/CookBot.Web/Components/Atoms/` (`grep -rn "Mud[A-Z]" src/CookBot.Web/Components/Atoms/` → no matches).
  - `MainLayout.razor` not modified (Plan 05-05 owns that rewrite).
  - Atoms emit existing `cb-*` classes; only inline styles are layout/dimension passthrough (`Style` slot, `padding`, `width`/`height` on the placeholder, `width:100%` on FullWidth buttons) — no tokenized values inlined.

## Manual smoke pass

Not executed in this session (no live browser). The automated checks plus a clean build are the gate the plan's Task 3 requires; the live light/dark visual smoke is a downstream user-driven check available immediately after this commit lands. To smoke:

1. `./run.sh`
2. Navigate to `http://localhost:7000/design-sandbox`
3. Scroll to the **Atoms** section.
4. Confirm 4 button variants render with correct fills (cocoa, orange, transparent+border, light-gray); StartIcon `+` glyph appears on Accent; EndIcon `→` appears on the Save & continue button; the disabled button is dimmed and not interactive; the full-width button stretches across its container.
5. Confirm 4 chip variants render (default cocoa-soft, timer accent-soft, ing cream-2, tag transparent + border).
6. Confirm CbStat values render in tabular-numeral font (squarer "8" / "1" shapes); 128 is paired with the "+3 this week" sub-line.
7. Confirm StripedPlaceholder shows diagonal-stripe pattern + dashed border + uppercase mono "hero photo" label, 180px tall, full-width.
8. Confirm CbBadge variants — green "in stock", warm orange "running low", soft accent "expires Friday", gray "out".
9. Toggle dark mode → every variant re-renders with dark counterparts; badges inherit dark soft-tone tints automatically (only the Out variant has an explicit dark rule, which is loaded).
10. Verify the **Forms** and **Dialogs / Toasts / Dropdown** section headers + sentinels are still present below the Atoms section so Plans 05-03/05-04 can extend them.

## MudBlazor coexistence (D-30)

- `MudBlazor` package reference still present in `CookBot.Web.csproj` (untouched).
- `@using MudBlazor` still in `_Imports.razor`.
- `_content/MudBlazor/MudBlazor.min.css` + `MudBlazor.min.js` still referenced from `App.razor`.
- The current `MainLayout.razor` (still MudBlazor-based) wraps `/design-sandbox`. Plan 05-05 owns the shell rewrite.

## Deviations from Plan

### Auto-fixed Issues

None — Tasks 1, 2, and 3 executed exactly as specified in the plan and the executor task prompt.

### Minor scope adjustments per executor task prompt

The executor task prompt specified the four CbStat tiles as **Recipes 128 (+3 this week) / Cookbooks 7 / Pantry 47 / Grocery 12** — slightly different from the plan's Task 3 sketch (which listed Pantry items 42 / Grocery 6 with extra subs). I followed the executor prompt's labels because the plan file's sketch was illustrative; the executor prompt is the latest direction. No requirement is affected (ATOM-04 is satisfied by any valid stat-tile demo).

The executor task prompt also called out CbBadge labels "in stock" / "running low" / "expiring" / "out" with `Label` allowed to default from the status. I made `Label` optional in `CbBadge` and added status-derived defaults — this is a parameter-surface superset of the plan's spec (which had `Label` as `EditorRequired`). Plan 05-04+ surfaces can still pass `Label="expires Friday"` to override; the sandbox demonstrates both modes (one of the four uses an explicit override).

## Authentication gates

None.

## Self-Check: PASSED

All 7 atom files exist:

- `src/CookBot.Web/Components/Atoms/CbButton.razor` — FOUND
- `src/CookBot.Web/Components/Atoms/CbChip.razor` — FOUND
- `src/CookBot.Web/Components/Atoms/CbCard.razor` — FOUND
- `src/CookBot.Web/Components/Atoms/CbStat.razor` — FOUND
- `src/CookBot.Web/Components/Atoms/CbEyebrow.razor` — FOUND
- `src/CookBot.Web/Components/Atoms/CbBadge.razor` — FOUND
- `src/CookBot.Web/Components/Atoms/StripedPlaceholder.razor` — FOUND

Build clean. Tests at baseline (196/196 default filter). Plan-level commit hash recorded after this file is staged.
