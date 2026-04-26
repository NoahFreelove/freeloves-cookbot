# Phase 3: Editor UX Without Special Syntax - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in 03-CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-26
**Phase:** 03-editor-ux-without-special-syntax
**Areas discussed:** Chip composer interaction, Step / Section toggle UX, Timer-detection suggestion UX, Edge flows (paste, AI fallback, cooking mode, JS-interop fallback)

---

## Area Selection

| Option | Description | Selected |
|--------|-------------|----------|
| Chip composer interaction (Recommended) | The heart of the phase — `@`-trigger vs. button, chip click semantics, index display, markdown visibility | ✓ |
| Step / Section toggle UX (Recommended) | Per-step toggle control, what happens to text on toggle, what happens to attached timers/refs | ✓ |
| Timer-detection suggestion UX (Recommended) | How "Detected 25 min — convert?" surfaces; bulk vs. per-occurrence; where chip lives | ✓ |
| Edge flows (paste, AI fallback, cooking mode, JS-interop fallback) | The non-happy-path contract | ✓ |

**Note:** Ingredient reorder mechanism was not surfaced as a separate area — folded into Claude's Discretion.

---

## Chip composer interaction

### Insertion path

| Option | Description | Selected |
|--------|-------------|----------|
| @-trigger autocomplete only (Recommended) | Single primary path; Slack/Notion mental model | |
| "Insert ingredient" button only | More discoverable, slower for repeat use | |
| Both — @-trigger AND a button | Belt-and-suspenders; two paths must produce same chip | ✓ |
| Inline chip + structured-field hybrid | Separate "Ingredients used in this step" multi-select per step | |

**User's choice:** Both — @-trigger AND a button.

### Chip click behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Remove the chip (Recommended) | Click → unwraps chip back to underlying text; standard chip-input UX | |
| Open a small replace-popover | Click → swap or remove popover; richer interaction | ✓ |
| No-op; only × button removes | Most explicit, least magical | |

**User's choice:** Open a small replace-popover.

### Chip index display

| Option | Description | Selected |
|--------|-------------|----------|
| Name only — e.g. "Salt" (Recommended) | Cleanest visual; index lives in ingredients table | ✓ |
| Index + name — e.g. "#2 Salt" | More information-dense; reorder shifts chip text | |
| Name + tooltip with index on hover | Compromise option | |

**User's choice:** Name only — e.g. "Salt".

**Note:** This conflicts with REQUIREMENTS.md EDITOR-01 wording. Surfaced as a follow-up question.

### Markdown visibility

| Option | Description | Selected |
|--------|-------------|----------|
| Never — chip is the only representation (Recommended) | Paste Raw Text is the escape hatch | |
| Toggle per-step — "View as text" / "View as chips" | Per-step escape hatch | ✓ |
| Markdown shows on focus, chips on blur | Slack-style; defeats the milestone goal | |

**User's choice:** Toggle per-step — "View as text" / "View as chips".

### EDITOR-01 conflict resolution

| Option | Description | Selected |
|--------|-------------|----------|
| Amend EDITOR-01 — chip = name only is correct (Recommended) | Drop "user-facing index" clause from EDITOR-01 during plan-phase | ✓ |
| Revisit — chip should show index + name after all | Override previous answer | |
| Hybrid: name body + index badge | Honor EDITOR-01 spirit while keeping prior answer | |

**User's choice:** Amend EDITOR-01 — chip = name only is correct.

### Toggle scope

| Option | Description | Selected |
|--------|-------------|----------|
| Per-step, ephemeral — resets to chips on save/reload (Recommended) | UI-only state; no new DB column | ✓ |
| Per-step, persisted on the recipe | New field on RecipeStep or in Extras | |
| Recipe-level toggle in editor toolbar | Coarser; one toggle for all steps | |

**User's choice:** Per-step, ephemeral.

### Bad-ref handling on text-view → chip-view flip

| Option | Description | Selected |
|--------|-------------|----------|
| Render as red error chip; let user fix it (Recommended) | Replace-popover lets user fix; warning surfaced; save allowed | ✓ |
| Refuse to flip until user fixes it | Strict; toggle disabled with tooltip | |
| Render as plain (un-tokenized) text | Leave [name](#id) literal | |

**User's choice:** Render as red error chip.

---

## Step / Section toggle UX

### Toggle control

| Option | Description | Selected |
|--------|-------------|----------|
| MudToggleGroup (segmented control) per step (Recommended) | `[Step | Section]` segmented control per row | ✓ |
| MudSwitch labeled "Section header?" | Compact but less discoverable | |
| Keep separate "Add Step" + "Add Section Header" buttons | Today's pattern; no mid-edit conversion | |
| Icon-button toggle (paragraph/heading icons) | Compact; relies on icon recognition | |

**User's choice:** MudToggleGroup.

### Step → Section text behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse text as section heading (Recommended) | Copy text into heading field; no data loss | ✓ |
| Discard text — show confirmation dialog | Forces explicit choice | |
| Keep text invisibly; restore on toggle back | Reversible but invisible state | |

**User's choice:** Reuse text as section heading.

### Section step + existing timers/refs

| Option | Description | Selected |
|--------|-------------|----------|
| Hide and clear them on Section toggle (Recommended) | Confirmation dialog if non-empty; silent if empty | ✓ |
| Block the toggle if timers/ingredient refs exist | Force explicit cleanup first | |
| Stash data and restore on Section → Step | Hidden round-trip state | |

**User's choice:** Hide and clear (with confirmation dialog when non-empty).

---

## Timer-detection suggestion UX

### Suggestion surface

| Option | Description | Selected |
|--------|-------------|----------|
| Inline highlight + click-to-convert (Recommended) | Dotted underline on detected substring; click → popover Yes/No | ✓ |
| End-of-step suggestion strip | Strip below textarea with click-to-convert chips | |
| Banner above the step + bulk action | MudAlert with "Convert all" | |
| Snackbar / toast on detection | Lightweight but easy to miss | |

**User's choice:** Inline highlight + click-to-convert.

### Bulk action

| Option | Description | Selected |
|--------|-------------|----------|
| Per-occurrence only, no bulk action (Recommended) | Each detection gets its own Yes/No | ✓ |
| Per-occurrence + step-level "Convert all" | Per-step bulk button | |
| Recipe-level "Convert all detected timers" | Top-of-editor bulk button | |

**User's choice:** Per-occurrence only.

### Accepted timer chip rendering

| Option | Description | Selected |
|--------|-------------|----------|
| Chip below step text; click to edit duration/unit/label (Recommended) | Chip strip below textarea; popover edit; original text stays | ✓ |
| Chip replaces detected substring inline | Mixes timer chips with ingredient chips in step body | |
| Chip in separate "Timers for this step" field | Dedicated sub-section per step | |

**User's choice:** Chip below step text with click-to-edit popover.

---

## Edge flows: paste, AI fallback, cooking mode, JS-interop fallback

### Paste flow

| Option | Description | Selected |
|--------|-------------|----------|
| Pass-through: parse, close, dump into chip editor with banners (Recommended) | Minimal dialog; chip editor populates with warnings | ✓ |
| Preview-and-confirm wizard inside the dialog | Two-stage; safer for mistakes | |
| Dialog itself becomes the chip editor | No separate dialog | |

**User's choice:** Pass-through.

### Phase 2 "Edit and save anyway" flow

| Option | Description | Selected |
|--------|-------------|----------|
| Same flow — reuse the paste-error banner and chip editor (Recommended) | Single code path, single mental model | ✓ |
| Distinct AI-failure UI inside the editor | Separate banner + "Discard and retry" button | |

**User's choice:** Same flow.

### Cooking-mode chip interactivity

| Option | Description | Selected |
|--------|-------------|----------|
| Read-only chips; non-clickable, same visual as editor (Recommended) | Visual parity; no editing surface | |
| Clickable ingredient chips that scroll the ingredients sidebar | Adds value; introduces JS-interop in cooking mode | ✓ |
| Hover-only highlighting (existing behavior) | Keep today's pattern | |

**User's choice:** Clickable ingredient chips that scroll the ingredients sidebar.

### JS-interop fallback (EDITOR-07)

| Option | Description | Selected |
|--------|-------------|----------|
| Plain MudTextField textarea fallback for step text (Recommended) | Raw [name](#id) editing; Save still works | ✓ |
| Degraded chip rendering: chips render but not interactive | Two-pane editing; awkward partition | |
| Hard fail with error banner blocking save | Conflicts with Phase 2 fallback contract | |

**User's choice:** Plain MudTextField textarea fallback.

---

## Claude's Discretion

Areas not surfaced for user input — planner picks:

- Ingredient reorder mechanism (drag handles vs. arrow buttons vs. both)
- Timer regex broadening (fractional, ranges, multi-segment; word-form numbers possibly deferred)
- Specific keyboard semantics inside the chip composer (Tab/Shift+Tab/Backspace/Arrow)
- axe-core / accessibility test mechanism (manual smoke checklist recommended)
- Replace-popover internals (`MudPopover` vs `MudMenu`)
- Confirmation-dialog framework for Step→Section drop (`MudDialog`)
- Inline-highlight CSS approach (`<span>` wraps via formatter vs. JS DOM mutation)
- Component extraction (`RecipeStepEditor.razor`, `RecipeChipComposer.razor`, `IngredientChip.razor`, `TimerChip.razor`)
- File layout under `src/CookBot.Web/Components/Pages/RecipeEditor/`

## Deferred Ideas

- Timer regex word-form numbers ("ten minutes") — backlog if planner judges out of scope
- Per-step temperature field — Phase 4
- `Recipe.TagsJson` → relational `RecipeTag` — Phase 4
- `LegacyRecipeProjector` deletion + `Recipe.IngredientRefs` column drop — Phase 4
- `README.md` "Recipe Format" section — Phase 4
- MudBlazor 9.x upgrade — FUTURE-10
- Drag-and-drop for step reordering — discretion / not core
- Encrypt-at-rest for `UserProfile.AiApiKey` — FUTURE-01
- Per-sharer cookbook-import consent banner — FUTURE-12
