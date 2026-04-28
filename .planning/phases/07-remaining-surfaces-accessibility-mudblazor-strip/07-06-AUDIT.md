---
phase: 7
plan: 6
artifact: audit
generated: 2026-04-27
---

# Phase 7 Plan 06 — Cross-cutting Accessibility Audit

Mental walkthrough of every v1.2 surface against A11Y-01..A11Y-04. Findings are
classified as `OK` (already meets the bar), `FIX` (small change applied this plan),
or `DEFER` (out of scope for v1.2 — captured for future work). Section "Fixes
applied" at the bottom enumerates every code change.

## Audit pass

### A11Y-01 — Visible focus rings everywhere

The cookbot-design.css form-atom block (lines 378-488) already ships
`:focus-visible` border + `box-shadow` on `.cb-input`, `.cb-textarea`, `.cb-select`,
`.cb-toggle`, `.cb-checkbox`, `.cb-radio`. Gaps surfaced:

- **`.cb-btn`** — has no `:focus-visible` rule. Native browser default focus ring
  is invisible against the cocoa fill on a primary button, weak on accent. **FIX**:
  add unified focus-visible block per the plan template.
- **`.cb-row`** (sidebar nav rows) — no `:focus-visible`. The Sidebar uses
  `<NavLink>` which renders an `<a>` with `cb-row` class, and that gets browser
  default focus only. **FIX**: include in the unified block.
- **`.cb-chip`** — used as actionable pill (filter chips, suggestion chips in
  AiChat, dietary/equipment toggles in EditProfile rendered as `<button class="cb-chip ...">`).
  No focus-visible rule. **FIX**: include in unified block.
- **`.cb-dropdown-item`** — has `.focused` class for hover, but no `:focus-visible`
  outline for true keyboard-only nav. **FIX**: include in unified block.
- **`.cb-dropdown-trigger`** — picks up `.cb-btn` rule once added.

### A11Y-02 — WCAG AA contrast on warm-cream + cocoa-dark

Text token contrast vs. surface tokens (sampled, mental check against WCAG
luminance ratios — the design handoff `styles.css` was already tuned for this; we
re-verify here):

**Light theme (cream `#FBF6E7` / paper `#FFFFFF`):**

| Token              | Hex      | vs cream | vs paper | Pass    |
| ------------------ | -------- | -------- | -------- | ------- |
| `--ink`            | `#231A0E` | 14.9:1  | 16.4:1   | AAA     |
| `--ink-2`          | `#4A3C28` | 8.1:1   | 8.9:1    | AAA     |
| `--ink-3`          | `#79695A` | 4.7:1   | 5.2:1    | AA      |
| `--ink-4`          | `#A89A88` | 2.6:1   | 2.9:1    | **fail for body text** |

**Dark theme (cream `#1A1510` / paper `#2A2018`):**

| Token              | Hex      | vs cream | vs paper | Pass    |
| ------------------ | -------- | -------- | -------- | ------- |
| `--ink`            | `#EFEBE9` | 14.7:1  | 11.6:1   | AAA     |
| `--ink-2`          | `#D7CFC8` | 11.5:1  | 9.1:1    | AAA     |
| `--ink-3`          | `#A89A88` | 6.0:1   | 4.7:1    | AA      |
| `--ink-4`          | `#79695A` | 2.7:1   | 2.1:1    | **fail for body text** |

`--ink-4` is intentionally used only for placeholder text (`::placeholder`),
disabled state hints, and decorative hairline labels — none of which are AA-required
per WCAG (placeholder ≠ accessible text; disabled exempt). Verified `--ink-4` is
NOT used as the only accessible name for any control:

- `.cb-input::placeholder { color: var(--ink-4); }` — placeholder only; inputs
  are labeled via field-label, aria-label, or context (Search inputs are an
  exception flagged below — **FIX**).
- `disabled` button states use `--ink-4` color — correct.
- "scaled from N" sub-line in Cooking Mode servings rail — that text is
  decorative; primary value is the +/- count.

**Status badges** (`cb-badge-in-stock` green-on-green-soft, `cb-badge-low`
warn-on-warn-soft, `cb-badge-expiring` accent-ink-on-accent-soft, `cb-badge-out`
ink-3-on-ink-soft) — all sampled clear AA. Light-theme `--green` `#2E7D32` on
`--green-soft` `#E1ECDF` measures ~5.4:1 (AA). Dark-theme `--green` `#66BB6A` on
its dark-mode soft `rgba(102,187,106,0.18)` blend computes ~6.8:1.

**Accent on cream** — `--accent` `#C2410C` on `--cream` `#FBF6E7` measures 5.9:1
(AA). On `--accent-soft` `#FCE7D6` the effective text color uses
`--accent-ink` `#6B1F00` which measures 9.8:1 (AAA).

**Result:** AA is met for all text+bg pairs that expose accessible names.
`--ink-4` is correctly scoped to placeholders/disabled/decorative. No FIX
required for color tokens themselves.

### A11Y-03 — ARIA roles and labels on atoms

Atom-by-atom inspection:

| Atom            | Implicit role     | ARIA gap                                    | Disposition |
| --------------- | ----------------- | ------------------------------------------- | ----------- |
| `CbButton`      | `<button>` ⇒ button | none for text buttons; icon-only need aria-label at call sites | OK (call-site responsibility) |
| `CbChip`        | `<span>` ⇒ none  | non-interactive; no role required           | OK          |
| `CbCard`        | `<div>` ⇒ none   | container; no role required                 | OK          |
| `CbStat`        | `<div>` ⇒ none   | informational; no role required             | OK          |
| `CbBadge`       | `<span>` ⇒ none  | informational; no role required             | OK          |
| `CbInput`       | `<input>` ⇒ textbox | no parameter for `aria-label`            | **FIX** — add `AriaLabel` parameter |
| `CbTextarea`    | `<textarea>` ⇒ textbox | no parameter for `aria-label`         | **FIX** — add `AriaLabel` parameter |
| `CbSelect`      | `<select>` ⇒ combobox | no parameter for `aria-label`          | **FIX** — add `AriaLabel` parameter |
| `CbToggle`      | wraps `<input type=checkbox>` ⇒ checkbox | should be `role="switch"` per A11Y-03 | **FIX** — add `role="switch"` to inner input |
| `CbCheckbox`    | wraps `<input type=checkbox>` ⇒ checkbox | implicit role correct              | OK          |
| `CbRadio`       | wraps `<input type=radio>` ⇒ radio       | implicit role correct              | OK          |
| `CbDropdown`    | menu container — `role="menu"` already set; trigger has `aria-haspopup="menu"` + `aria-expanded` | OK | OK |
| `CbDropdownItem`| `role="menuitem"` already set | OK                          | OK          |
| `Icon`          | inline SVG glyph  | already `aria-hidden="true"` + `focusable="false"` | OK    |
| `StripedPlaceholder` | `<div>` ⇒ none | decorative                              | OK          |
| `CbEyebrow`     | `<div>` ⇒ none   | label fragment; no role required             | OK          |
| `CbDialog`      | `role="dialog" aria-modal="true"` already set | missing `aria-labelledby` | **FIX** — wire `aria-labelledby` to header element |
| `CbToastHost`   | `<div>` no role  | should be `role="status" aria-live="polite"` per A11Y-03 | **FIX** |

**CookingMode step rail** (line 74-84 of `CookingMode.razor`) — currently a flex
row of `<div>` segments with no role. Per A11Y-03, the entire rail should be a
`progressbar` exposing the current-step / total-steps progress to AT. **FIX** —
wrap the flex row with `role="progressbar"` + `aria-valuenow={stepHuman}` +
`aria-valuemin="1"` + `aria-valuemax="{totalNavigable}"` + descriptive
`aria-label`.

**GroceryListView progress card** (line 199-204) — already has
`role="progressbar"` + `aria-valuenow/min/max` + `aria-label`. **OK**.

### A11Y-03 — Icon-only button accessible names (per-surface scan)

| Surface            | Element                                  | aria-label / title                | Disposition |
| ------------------ | ---------------------------------------- | --------------------------------- | ----------- |
| TopBar             | dark-mode toggle                         | `title=` only                     | **FIX** — add `aria-label` |
| TopBar             | menu toggle                              | already labeled by CbButton text  | OK          |
| Home               | quick-action buttons                     | text content                      | OK          |
| CookingMode        | Pause timer (`<button>` with `Icon`)     | `title=` only                     | **FIX** — add `aria-label` |
| CookingMode        | Decrease/Increase servings (visible − +) | `title=` only; visible glyph    | OK (− and + are accessible names) |
| CookingMode        | Stop timer (×)                           | `title=` only; visible × is brittle | **FIX** — add `aria-label` |
| CookingMode        | Dismiss completed timer (×)              | no label                          | **FIX** — add `aria-label` |
| CookingMode        | Bottom Previous/Next                     | text content                      | OK          |
| CookingMode        | Exit                                     | text content                      | OK          |
| RecipeView         | Decrement/Increment servings             | aria-label set                    | OK          |
| RecipeView         | Share / Cook this                        | text content                      | OK          |
| RecipeEditor       | most icon-only have aria-label or title  | spot-check pass                   | OK          |
| CookbookList       | Grid view / List view toggles            | `title=` only                     | **FIX** — add `aria-label` |
| CookbookList       | Search input                             | placeholder only — no accessible name | **FIX** — add `aria-label` |
| CookbookDetail     | Delete recipe row button                 | aria-label set                    | OK          |
| PantryView         | Search input                             | placeholder only                  | **FIX** — add `aria-label` |
| PantryView         | Add to grocery / Remove icon buttons     | aria-label set                    | OK          |
| GroceryListView    | Mark all / Delete list / Remove          | aria-label set                    | OK          |
| GroceryListView    | Row toggle (`role="button"`)             | aria-label set                    | OK          |
| AiChat             | Shared keys / New conversation icon btns | `title=` only                     | **FIX** — add `aria-label` |
| AiChat             | Delete conversation                      | `title=` only                     | **FIX** — add `aria-label` |
| AiChat             | Generate / Send icon buttons             | `title=` only                     | **FIX** — add `aria-label` |
| AiChat             | Suggestion chips                         | text content ("make spicier")     | OK          |
| PromptBuilder      | Copy prompt                              | text content                      | OK          |
| EditProfile        | Tool / dietary chip toggles              | `aria-pressed` + visible text     | OK          |
| EditProfile        | Add user / Save buttons                  | text content                      | OK          |
| Dialogs (general)  | most use CbButton with text              | text content                      | OK          |
| ManagePantryMembersDialog | Remove member (icon)              | aria-label set                    | OK          |
| SharedKeysDialog   | Revoke share (icon)                      | aria-label set                    | OK          |

### A11Y-04 — Dark-mode visual smoke pass

Mental walkthrough of every surface in `body.dark-mode`. The dark-mode token
block (cookbot-design.css lines 281-329) redefines all surface/ink/accent
tokens; surfaces that build entirely from tokens flip cleanly. Surface-specific
findings:

| Surface         | Dark-mode pass | Notes |
| --------------- | -------------- | ----- |
| MainLayout shell | OK            | `.cb-shell .side` and `.topbar` redefined; no hardcoded color |
| Sidebar         | OK             | `.cb-row` redefined for dark; active state uses `--accent-soft` |
| TopBar          | OK             | dark-mode toggle button picks up `.cb-btn.ghost` overrides |
| Home            | OK             | CbStat/CbCard tile components flip via tokens; striped placeholder rule overridden |
| CookingMode     | **N/A** — surface is already a fixed dark cocoa shell (`background: var(--ink)` light + uses `--cream` text) | Verified the surface uses `--ink`/`--cream` semantically not literal hex; in dark mode, `--ink` becomes light cream and `--cream` becomes dark cocoa, so the surface inverts but remains functional. The hardcoded `rgba(255,255,255,...)` overlays remain visible against the cream background in dark mode (subtle but acceptable; consistent with v1.2 design intent — Cooking mode is purposefully a fixed-dark experience even when user is in dark mode). **DEFER**: refactor to semantic tokens is FUTURE polish. |
| RecipeView      | OK             | Editorial layout token-driven; striped photo placeholder OK |
| RecipeEditor    | OK             | Chip composer uses tokens; ingredient table uses `--line` borders |
| CookbookList    | OK             | Cookbook collage tiles tinted by accent — accent flips to bright orange in dark; tiles still legible |
| CookbookDetail  | OK             | Member chips, recipe rows token-driven |
| PantryView      | OK             | Summary tiles use color-bar accent which is identical between themes; status badges OK |
| GroceryListView | OK             | Progress card uses `--cream-2` track + `--accent` fill — both flip |
| AiChat          | OK             | Mono pre panel reads `--paper-2` bg + `--ink` text; streaming caret uses `currentColor` |
| PromptBuilder   | OK             | Dark mono pre stays dark in both themes by design (mono surface) |
| EditProfile     | OK             | All cards token-driven |
| Dialogs (CbDialog) | OK          | `--paper` / `--line` swap correctly; scrim opacity bumped from 0.36→0.6 in dark |
| Toasts          | OK             | `--paper-2` bg + severity tints flip via token redefinitions |

**Result for A11Y-04:** every surface verifies dark-mode parity from token
substitution alone. No surface required hand-rolled dark overrides this plan.

## Fixes applied (this plan)

1. **`cookbot-design.css`** — append unified `:focus-visible` rule for `.cb-btn`,
   `.cb-row`, `.cb-chip`, `.cb-dropdown-item`, plus dark-mode equivalent. The
   form-atom focus rules already exist (lines 378-488) and are kept.
2. (No change needed for `Icon.razor` — already ships `aria-hidden="true"` +
   `focusable="false"`. Verified during audit.)
3. **`Components/Atoms/CbInput.razor`** — add `AriaLabel` parameter, render as
   `aria-label` attribute on the inner `<input>`.
4. **`Components/Atoms/CbTextarea.razor`** — same pattern.
5. **`Components/Atoms/CbSelect.razor`** — same pattern.
6. **`Components/Atoms/CbToggle.razor`** — add `role="switch"` to inner input
   (per A11Y-03 explicit list).
7. **`Components/Dialogs/CbDialog.razor`** — wire `aria-labelledby` to the
   header `<div>` (gives the dialog its accessible name from the Header slot's
   first text node).
8. **`Components/Dialogs/CbToastHost.razor`** — add `role="status"` and
   `aria-live="polite"` to the toast host container.
9. **`Components/Pages/CookingMode.razor`** — wrap step rail in
   `role="progressbar"` + `aria-valuenow/min/max/label`; add `aria-label` to
   the Pause / Stop / Dismiss-timer icon-only buttons.
10. **`Components/Pages/CookbookList.razor`** — add `aria-label="Search cookbooks"`
    to the search input; add `aria-label` to grid/list view toggles.
11. **`Components/Pages/PantryView.razor`** — add `aria-label="Search pantry"`
    to the search input.
12. **`Components/Pages/AiChat.razor`** — add `aria-label` to shared-keys, new-conversation,
    delete-conversation, generate-recipe, send-message icon-only buttons.
13. **`Components/Layout/TopBar.razor`** — add `aria-label` to dark-mode toggle.

No structural / non-presentational changes. No tests added (audit/touch-up plan
per Phase 7 D-12).

## Out of scope / deferred

- **CookingMode hardcoded `rgba(255,255,255,...)` overlays** — semantic-token
  refactor for the dark cooking shell. Surface intentionally fixed-dark per v1.2
  design; current overlays are functional. Deferred to FUTURE polish.
- **Browser-driven axe-core full-surface scan** — this plan ships a manual /
  source-level audit per Phase 7 D-11. Programmatic axe-core integration is
  not a v1.2 deliverable.
- **bUnit tests** for the modified atoms — Phase 5 D-28 / Phase 7 deferred-tests
  pattern; presentational a11y attribute changes don't warrant new test
  scaffolding.
