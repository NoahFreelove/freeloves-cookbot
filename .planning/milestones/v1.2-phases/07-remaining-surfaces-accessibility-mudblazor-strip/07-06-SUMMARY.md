---
phase: 07-remaining-surfaces-accessibility-mudblazor-strip
plan: 06
subsystem: ui
tags: [accessibility, a11y, aria, focus-visible, wcag, blazor]

requires:
  - phase: 05-foundation-design-tokens-atoms-shell-dialogs
    provides: atom system + cookbot-design.css + CbDialog/CbToast/CbDropdown primitives
  - phase: 06-marquee-surfaces-home-cooking-mode-recipe-view-recipe-editor
    provides: Home / Cooking Mode / Recipe View / Recipe Editor surfaces
  - phase: 07-remaining-surfaces-accessibility-mudblazor-strip
    provides: Cookbooks / Pantry / Grocery / AI Chat / Prompt Builder / Profile surfaces (07-01..07-05)
provides:
  - Cross-cutting a11y audit (07-06-AUDIT.md) covering all 9 v1.2 surfaces
  - Unified :focus-visible 2px accent outline for buttons, sidebar rows, chips, dropdown items
  - AriaLabel parameter on CbInput / CbTextarea / CbSelect (call-site escape hatch when no label wraps)
  - role="switch" on CbToggle inner input
  - aria-labelledby wired on CbDialog
  - role="status" + aria-live="polite" on CbToastHost
  - role="progressbar" on CookingMode step rail
  - aria-label on every previously title-only icon-only button (TopBar, CookingMode, CookbookList, PantryView, AiChat)
  - aria-label on CookbookList + PantryView search inputs
affects: [07-07 final MudBlazor strip + verification, future v1.3 surfaces]

tech-stack:
  added: []
  patterns:
    - "Decorative SVG glyphs ship aria-hidden=true; host control supplies the accessible name (already in place for Icon — verified)"
    - "Form atoms expose AriaLabel parameter as a call-site escape hatch when surrounding context provides no implicit label"
    - "Toast host uses role=status + aria-live=polite for non-disruptive announcements"
    - "Step rail / progress bar surfaces use role=progressbar with aria-valuenow/min/max/label"

key-files:
  created:
    - .planning/phases/07-remaining-surfaces-accessibility-mudblazor-strip/07-06-AUDIT.md
  modified:
    - src/CookBot.Web/wwwroot/css/cookbot-design.css
    - src/CookBot.Web/Components/Atoms/CbInput.razor
    - src/CookBot.Web/Components/Atoms/CbTextarea.razor
    - src/CookBot.Web/Components/Atoms/CbSelect.razor
    - src/CookBot.Web/Components/Atoms/CbToggle.razor
    - src/CookBot.Web/Components/Dialogs/CbDialog.razor
    - src/CookBot.Web/Components/Dialogs/CbToastHost.razor
    - src/CookBot.Web/Components/Layout/TopBar.razor
    - src/CookBot.Web/Components/Pages/CookingMode.razor
    - src/CookBot.Web/Components/Pages/CookbookList.razor
    - src/CookBot.Web/Components/Pages/PantryView.razor
    - src/CookBot.Web/Components/Pages/AiChat.razor

key-decisions:
  - "CbToggle inner input gets role='switch' (per A11Y-03 explicit list), not just an unenhanced checkbox role"
  - "Form atoms add an AriaLabel parameter rather than auto-derive from Placeholder — placeholder is not an accessible name and we want call sites to be explicit"
  - "CbDialog aria-labelledby wires only when Header is non-null; bare-body dialogs degrade to role='dialog' aria-modal='true' alone (still more accessible than no role)"
  - "ink-4 token verified safe at AA — only used for placeholders/disabled/decorative; no body-text role"
  - "CookingMode dark-on-dark surface keeps its hardcoded rgba(255,255,255,...) overlays — semantic-token refactor deferred (v1.2 design intent is fixed-dark cooking shell regardless of user theme)"

patterns-established:
  - "Audit-then-fix in a single plan: AUDIT.md captures findings; SUMMARY.md captures outcomes; commit applies both"
  - "Icon-only button pattern: aria-label is mandatory; title= remains as a tooltip but is not an accessible-name fallback"

requirements-completed: [A11Y-01, A11Y-02, A11Y-03, A11Y-04]

duration: 4min
completed: 2026-04-27
---

# Phase 7 Plan 06: Cross-cutting accessibility audit + small fixes Summary

**Walked all 9 v1.2 surfaces against WCAG AA / focus-rings / ARIA-roles / dark-mode parity; landed targeted fixes (focus-visible CSS, AriaLabel atom params, role=switch / role=status / role=progressbar / aria-labelledby, icon-only aria-labels across TopBar/CookingMode/CookbookList/PantryView/AiChat) without introducing structural changes.**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-04-27T22:21:03Z
- **Completed:** 2026-04-27 (same session)
- **Tasks:** 3/3 (audit, fixes, build verify)
- **Files modified:** 12 + 1 created (AUDIT.md)

## Accomplishments

- Completed 07-06-AUDIT.md walkthrough: 9 surfaces, ARIA gap matrix, WCAG AA contrast table for both themes, dark-mode smoke pass per surface.
- Applied 13 targeted code changes covering A11Y-01 (focus rings), A11Y-03 (ARIA roles + accessible names on icon-only controls + form atoms).
- Confirmed A11Y-02 (contrast) and A11Y-04 (dark-mode parity) by inspection — no code changes needed; tokens were already tuned in Phase 5.
- Preserved baseline test results: 196 passing / 6 pre-existing failures (out of scope: PromptInjectionResistanceTests have been red since before this plan and are tracked separately).

## Task Commits

1. **Task 1+2+3 (audit + fixes + verify):** `c7e4caf` (feat) — single atomic commit per plan acceptance criteria. Includes 07-06-AUDIT.md plus all 12 source/style files.

## Files Created/Modified

**Created**
- `.planning/phases/07-remaining-surfaces-accessibility-mudblazor-strip/07-06-AUDIT.md` — full per-surface audit walkthrough, contrast table, dark-mode smoke pass, deferred-items log.

**Modified**
- `src/CookBot.Web/wwwroot/css/cookbot-design.css` — appended unified `:focus-visible` block for `.cb-btn`, `.cb-row`, `.cb-chip`, `.cb-dropdown-item` (+ dark-mode parity rule).
- `src/CookBot.Web/Components/Atoms/CbInput.razor` — added `AriaLabel` parameter; rendered as `aria-label` on the inner `<input>`.
- `src/CookBot.Web/Components/Atoms/CbTextarea.razor` — same pattern for textarea.
- `src/CookBot.Web/Components/Atoms/CbSelect.razor` — same pattern for select.
- `src/CookBot.Web/Components/Atoms/CbToggle.razor` — added `role="switch"` to the hidden native checkbox.
- `src/CookBot.Web/Components/Dialogs/CbDialog.razor` — added `_headerId` field and wired `aria-labelledby="@_headerId"` on the dialog when Header is rendered.
- `src/CookBot.Web/Components/Dialogs/CbToastHost.razor` — added `role="status"` + `aria-live="polite"` + `aria-atomic="false"` to the toast host container.
- `src/CookBot.Web/Components/Layout/TopBar.razor` — added `aria-label` + `aria-pressed` on the dark-mode toggle button.
- `src/CookBot.Web/Components/Pages/CookingMode.razor` — wrapped step rail in `role="progressbar"` with valuenow/min/max/label; added `aria-label` on Pause-stop, Stop-timer, Dismiss-completed-timer, Decrement/Increment-servings icon-only buttons.
- `src/CookBot.Web/Components/Pages/CookbookList.razor` — added `aria-label="Search cookbooks"` to the search input and `aria-label="Grid view"` / `"List view"` to the layout-mode toggle buttons.
- `src/CookBot.Web/Components/Pages/PantryView.razor` — added `aria-label="Search pantry"` to the search input.
- `src/CookBot.Web/Components/Pages/AiChat.razor` — added `aria-label` to Shared-API-keys, New-conversation, Delete-conversation, Generate-recipe, Send-message icon-only buttons.

## Acceptance criteria — gate-check

- [x] **A11Y-01**: visible 2px accent focus rings on every actionable element (buttons, sidebar rows, chips, dropdown items, form atoms, dialog primitives) — verified via cookbot-design.css unified rule + form-atom rules already in place.
- [x] **A11Y-02**: WCAG AA contrast verified on warm-cream + cocoa-dark for `--ink`, `--ink-2`, `--ink-3`, accent variants, and status badges. `--ink-4` is correctly scoped to placeholder/disabled/decorative roles only — documented in AUDIT.md contrast tables.
- [x] **A11Y-03**: ARIA roles audited atom-by-atom and surface-by-surface; gaps closed (role=switch on toggle, role=status on toast host, role=progressbar on cooking step rail, aria-labelledby on dialog, AriaLabel parameter on form atoms, aria-label on every previously unlabeled icon-only button).
- [x] **A11Y-04**: Dark-mode smoke pass walkthrough recorded for all 9 surfaces in AUDIT.md. All surfaces flip cleanly via token redefinitions; no surface required hand-rolled dark overrides this plan. CookingMode's intentional fixed-dark shell is preserved with deferred-polish notes.
- [x] `dotnet build` clean (0 warnings, 0 errors).
- [x] `dotnet test` baseline preserved: 196 passing (matches pre-plan baseline). 6 PromptInjectionResistanceTests remain failing — pre-existing red state, out of scope per Plan 07-06 deviation rules (SCOPE BOUNDARY: only auto-fix issues directly caused by current task changes).
- [x] 07-06-AUDIT.md documents the audit walkthrough + every applied fix.

## Deviations from Plan

**None — plan executed exactly as written.**

The plan template suggested a single CSS block touching `.cb-btn`, `.cb-input`, `.cb-select`, `.cb-textarea`, `.cb-row`. Form-atom focus rings were already shipped in Phase 5 with a stronger treatment (border-color + box-shadow) than a simple outline; the new block contributes the missing button/row/chip/dropdown-item rules without disturbing the existing form rules. This is consistent with the plan ("likely already partial — verify and extend") and is documented in AUDIT.md.

The Icon component was found to already ship `aria-hidden="true"` + `focusable="false"` (Phase 5); the plan's mention of decorative icon labelling is therefore a no-op and is documented as OK in the AUDIT atom-by-atom table.

## Deferred / out-of-scope

- **CookingMode fixed-dark shell semantic-token refactor** — `rgba(255,255,255,...)` overlays remain hardcoded; v1.2 design intent is a fixed-dark cooking experience regardless of user theme. Refactor to semantic tokens captured in AUDIT.md as FUTURE polish.
- **Programmatic axe-core scan in CI** — A11Y-04 phase verification is a manual walkthrough per Phase 7 D-11; CI integration is not a v1.2 deliverable.
- **bUnit tests for atom a11y attribute changes** — Phase 5 D-28 deferred-tests pattern; presentational attribute additions don't warrant new test scaffolding.
- **Pre-existing PromptInjectionResistanceTests failures (6)** — red state predates this plan; out of scope per SCOPE BOUNDARY rule. Tracked for separate remediation.

## Self-Check: PASSED

- `.planning/phases/07-remaining-surfaces-accessibility-mudblazor-strip/07-06-AUDIT.md`: FOUND
- Commit `c7e4caf`: FOUND in `git log` (HEAD).
- All 12 modified files match `git show --stat c7e4caf` output.
- `dotnet build` clean (0 errors / 0 warnings).
- `dotnet test`: 196 passing — matches pre-plan baseline.
