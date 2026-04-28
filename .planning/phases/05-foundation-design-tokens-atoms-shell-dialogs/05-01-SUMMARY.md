---
phase: 05-foundation-design-tokens-atoms-shell-dialogs
plan: 01
subsystem: design-system
tags:
  - design-system
  - css-tokens
  - icons
  - razor
requires: []
provides:
  - cookbot-design.css token surface (DS-01..DS-06)
  - Icon Razor component covering 36 outline glyphs (ATOM-07)
  - cookbot-shell.js JS interop (data-accent, data-density)
  - /design-sandbox verification surface with insertion-point sentinels
affects:
  - src/CookBot.Web/Components/App.razor
  - src/CookBot.Web/Components/_Imports.razor
tech-stack:
  added:
    - rsms.me/inter/inter.css (font, via CSS @import)
  patterns:
    - CSS custom-property-driven token system
    - data-accent / data-density attribute selectors on <html>
    - body.dark-mode class-based dark theme (preserved from existing toggle)
    - Single Razor component dispatching all icons via switch + MarkupString
key-files:
  created:
    - src/CookBot.Web/wwwroot/css/cookbot-design.css
    - src/CookBot.Web/Components/Atoms/Icon.razor
    - src/CookBot.Web/wwwroot/js/cookbot-shell.js
    - src/CookBot.Web/Components/Pages/DesignSandbox.razor
  modified:
    - src/CookBot.Web/Components/App.razor
    - src/CookBot.Web/Components/_Imports.razor
decisions:
  - Dark-mode block uses body.dark-mode selectors (not data-theme), matching existing toggle (D-04)
  - Icon dispatch uses switch + MarkupString to interleave <path> children inside <svg> (D-08)
  - applyDefaults is idempotent and called from sandbox first-render (D-05)
  - Inter loaded via @import inside cookbot-design.css; existing Google Fonts <link> in App.razor left untouched (load order: Mud → cookbot-design → app.css)
metrics:
  duration: ~10 min
  completed: 2026-04-27
requirements:
  - DS-01
  - DS-02
  - DS-03
  - DS-04
  - DS-05
  - DS-06
  - ATOM-07
---

# Phase 5 Plan 01: Design tokens, Icon component, sandbox route Summary

Foundation slice for the v1.2 redesign: ports the design-handoff token stylesheet into a single global CSS file with full dark-mode parity, ships the 36-glyph `<Icon>` Razor component, wires the data-accent / data-density JS interop, and stands up the `/design-sandbox` verification surface with insertion-point sentinels for plans 05-02..05-04. MudBlazor remains loaded — zero `Mud*` symbols introduced in any new code.

## What shipped

### Token stylesheet (`src/CookBot.Web/wwwroot/css/cookbot-design.css`)

Near-verbatim port of `.planning/design-handoff/project/styles.css` plus a dark-mode block. **51 CSS custom properties** total across `:root` + the dark and density/accent overrides.

Token categories shipped:

- **Surfaces** — `--cream`, `--cream-2`, `--paper`, `--paper-2`, `--line`, `--line-strong`
- **Ink** — `--ink`, `--ink-2`, `--ink-3`, `--ink-4`
- **Accent** — `--accent`, `--accent-soft`, `--accent-ink`
- **Status** — `--green`, `--green-soft`, `--warn`, `--warn-soft`
- **Type** — `--f-display`, `--f-body`, `--f-mono`
- **Radii** — `--t-radius`, `--t-radius-sm`
- **Density** — `--pad`, `--pad-sm`, `--gap`

Selectors:

- `:root { ... }` — light-mode defaults (orange accent, comfy density implicit)
- `[data-density="compact"]` — overrides `--pad`/`--pad-sm`/`--gap`
- `[data-accent="terracotta"]`, `[data-accent="sage"]` — override accent triad
- `body.dark-mode` — redefines every dark-relevant token + component overrides for `.cb`, `.cb-card`, `.cb-stat`, `.cb-shell .side`, `.cb-shell .topbar`, `.cb-btn` (+ ghost/accent/subtle), `.cb-row` (+ hover/active), `.cb-chip` (+ timer/ing/tag), `.cb-kbd`, `.cb-rule`, `.cb-ph`

Component classes ported verbatim: `.cb`, `.cb h1..h4`, `.cb p`, `.cb button`, `.cb .num`, `.cb .mono`, `.cb .eyebrow`, `.cb-btn` (+ ghost/accent/subtle), `.cb-card`, `.cb-ph` (+ `::after`), `.cb-chip` (+ timer/ing/tag), `.cb-rule`, `.icon`, `.cb-row` (+ hover/active), `.cb-shell` (+ side/topbar), `.cb-stat` (+ v/l), `.cb-recipe-cap`, `.cb-kbd`.

Inter loads via `@import url('https://rsms.me/inter/inter.css')` at top of file (D-03).

### Icon component (`src/CookBot.Web/Components/Atoms/Icon.razor`)

Single Razor component covering all 36 outline glyphs from `.planning/design-handoff/project/icons.jsx`, dispatched via `switch` expression keyed on the `Name` parameter. Path data ported verbatim. Uses `MarkupString` to render the inner `<path>`/`<rect>`/`<circle>` children inside the parent `<svg>` cleanly.

API:

- `Name` (string, required) — one of the 36 glyph names
- `Size` (int, default 18) — width/height in px
- `Style` (string?, optional) — extra inline style appended to the default `flex-shrink:0;vertical-align:middle;`

Public `Icon.Names` static class exposes a `public const string` for every glyph (36 constants — matches the 36 names below).

The 36 verified icon names: `home`, `book`, `pantry`, `cart`, `spark`, `prompt`, `user`, `menu`, `search`, `plus`, `check`, `clock`, `flame`, `pause`, `play`, `arrowR`, `arrowL`, `bell`, `sun`, `share`, `download`, `copy`, `pencil`, `more`, `trash`, `scale`, `bolt`, `filter`, `grid`, `list`, `chevD`, `chevR`, `flag`, `send`, `save`, `link`.

Filled-glyph variants (`play`, `more` circles, `list` bullet circles) carry per-element `fill="currentColor"` exactly as in the JSX source. Unknown names render an inline `?` text node and write a one-line `Console.Error` warning (dev signal, not user-visible).

`@using CookBot.Web.Components.Atoms` added to `_Imports.razor` after the existing `@using CookBot.Web.Components.Layout` line; `@using MudBlazor` left in place (D-30 coexistence).

### JS interop (`src/CookBot.Web/wwwroot/js/cookbot-shell.js`)

Three functions on `window.cookbot`:

- `setAccent(name)` — allowed: `orange | terracotta | sage`; unknown → defaults to `orange`. Writes `data-accent` on `<html>`.
- `setDensity(mode)` — allowed: `comfy | compact`; unknown → defaults to `comfy`. Writes `data-density` on `<html>`.
- `applyDefaults()` — idempotent. Sets `data-accent="orange"` and `data-density="comfy"` only if not already present.

Deliberately does NOT touch dark-mode state — the existing `cookbot_dark_mode` localStorage toggle in `MainLayout.razor` (toggling `body.dark-mode`) remains the single source of truth (D-04).

### App.razor wiring

- Added `<link rel="stylesheet" href="css/cookbot-design.css" />` in head, immediately AFTER the existing MudBlazor stylesheet (so cookbot tokens override Mud where they cascade).
- Added `<script src="js/cookbot-shell.js"></script>` at the end of the body script list (after `recipe-chip-composer.js`).
- Existing MudBlazor CSS, MudBlazor JS, Google Fonts Inter `<link>`, app.css, scoped styles, ImportMap, favicon, HeadOutlet — all preserved untouched.

### Design sandbox (`/design-sandbox`)

`Components/Pages/DesignSandbox.razor` mounted under the existing `MainLayout` (which is still the MudBlazor-based layout — Plan 05-05 swaps that). Renders:

- Header — page title + eyebrow note "Phase 5 verification surface — DELETED in Phase 7"
- **Tokens** section — 15 swatches across the surface/ink/accent/green/warn token families, each rendering a 48px color preview tied to `var(--token-name)` so flipping `body.dark-mode` re-paints the entire grid.
- **Icons (36)** section — 36 tiles, each rendering `<Icon Name="@name" Size="24" />` plus the glyph name in mono.
- Three insertion-point sentinels for downstream plans:
  - `<!-- ATOMS_INSERTION_POINT -->` (Plan 05-02)
  - `<!-- FORMS_INSERTION_POINT -->` (Plan 05-03)
  - `<!-- DIALOGS_INSERTION_POINT -->` (Plan 05-04)
- Calls `cookbot.applyDefaults()` from `OnAfterRenderAsync(firstRender)` so the `<html>` element picks up `data-accent="orange"` and `data-density="comfy"` on first paint.

Route is `/design-sandbox` — direct nav only, not added to the sidebar (D-27).

## Verification

- `dotnet build src/CookBot.Web/CookBot.Web.csproj -c Debug` — **PASSED** (0 warnings, 0 errors, 7.83s).
- `dotnet test --filter "Category!=RequiresApiKey"` — **PASSED** (196/196). The 6 `RequiresApiKey`-gated AI fixture tests fail without `ANTHROPIC_API_KEY` set — pre-existing environment baseline, no production code in this plan touches AI generation.
- All plan automated-verify clauses (Tasks 1-5) — **PASSED**:
  - Task 1: 51 custom properties (≥25 required), Inter import, body.dark-mode block, terracotta/sage accents, compact density.
  - Task 2: 36 `public const string` constants in `Icon.Names`, `_Imports.razor` adds Atoms namespace, MudBlazor using preserved.
  - Task 3: setAccent / setDensity / applyDefaults all present.
  - Task 4: cookbot-design.css linked, cookbot-shell.js scripted, MudBlazor.min.css and MudBlazor.min.js still referenced.
  - Task 5: `@page "/design-sandbox"`, all three insertion-point sentinels, `cookbot.applyDefaults` invocation present.
- Self-check (file existence): all 6 target files FOUND.

## Manual smoke pass

Not executed in this session (no live browser). The automated checks plus a clean build are the verification gate the plan's Task 6 requires; the live light/dark visual smoke is a downstream user-driven check available immediately after this commit lands. To smoke:

1. `./run.sh`
2. Navigate to `http://localhost:7000/design-sandbox`
3. Confirm 15 token swatches render with distinct cream / paper / ink / accent / green / warn colors.
4. Confirm 36 icon tiles render — none show `?`.
5. Toggle dark mode via the existing top-bar button. Tokens should flip; swatches re-paint dark cocoa for cream / light cream for ink / warmer accent.
6. DevTools → `<html>` has `data-accent="orange" data-density="comfy"`.
7. `cookbot.setAccent("terracotta")` shifts the accent swatch to brown-red; `cookbot.setDensity("compact")` updates `--pad` (visible effect lands in Plan 05-02).

## MudBlazor coexistence (D-30)

- `MudBlazor` package reference still present in `CookBot.Web.csproj` (untouched).
- `@using MudBlazor` still in `_Imports.razor`.
- `_content/MudBlazor/MudBlazor.min.css` + `MudBlazor.min.js` still referenced from `App.razor`.
- `MainLayout.razor` and `NavMenu.razor` not modified — the existing `<MudThemeProvider>` / `<MudLayout>` shell still wraps every page including `/design-sandbox`. The new tokens override Mud-painted surfaces only where the `.cb` class is applied (sandbox content div).

## Deviations from Plan

None. Plan executed as written.

The plan's Task 2 action sketch suggested per-branch Razor `<text>` children carrying `MarkupString` per icon. I chose the structurally simpler form — a single `(MarkupString)PathFor(Name)` rendered inside the `<svg>` parent, with `PathFor` being a `static string` switch returning the inner SVG markup as a string. Same effect, fewer Razor inversions, easier to read; explicitly within the plan's "if a cleaner pattern emerges, prefer it — but DO NOT block on cleverness; correctness first" allowance.

## Authentication gates

None.

## Self-Check: PASSED

All 6 target files exist. Build succeeded with 0 warnings. Test suite preserved (196/196 in default filter; 6 pre-existing `RequiresApiKey`-gated failures unchanged by this plan and unrelated to its scope).
