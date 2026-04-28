# Phase 3: Editor UX Without Special Syntax — Verification

**Generated:** 2026-04-26
**Scope:** EDITOR-01 .. EDITOR-07 manual + automated verification.

## Automated Verification

The following tests gate phase completion (run via `dotnet test tests/CookBot.Tests/CookBot.Tests.csproj --filter "Category!=RequiresApiKey"`):

| Test Class | Covers | Decision Refs |
|---|---|---|
| `RecipeChipComposerTests` (bUnit) | Chip rendering, D-A1 invariant (@-path == button-path), D-A6 unresolved chip, D-D4 fallback | EDITOR-01, EDITOR-04, EDITOR-07 |
| `StepSectionToggleTests` (bUnit) | Step→Section conversion, D-B1 toggle, D-B2 heading reuse, D-B3 confirmation helper | EDITOR-02 |
| `TimerSuggestionTests` (xUnit on formatter) | Timer-suggestion span wrapping, D-C3 skip-already-converted, no-double-wrap, range lowest | EDITOR-03 |
| `TimerDetectionServiceRegexTests` (xUnit) | Fractional / Range / Multi-segment / Simple regex broadening | EDITOR-03 (regex broadening Claude's discretion) |
| `PasteFlowTests` (bUnit + source-presence) | Pass-through flow; numbered-list fallback deletion verified | EDITOR-05 |
| Existing Phase 1 + Phase 2 suites | Round-trip / parser / validator / AI / structured-output unchanged | Regression gates |

## Manual Smoke Checklist (EDITOR-07 gate)

**No automated accessibility infra exists in this codebase** (no axe-core, Playwright, Lighthouse). The following checklist is the verification gate for EDITOR-07 — work through it on a real browser session before marking the phase complete.

Pre-requisite: `./run.sh` running, navigate to `http://localhost:7000`. Sign in. Create a test cookbook and a fresh recipe; you'll edit it during the smoke pass.

### Item 1 — Tab / Shift+Tab navigation across step rows
- [ ] Press Tab from the recipe Name field. Focus moves: Name → Servings → PrepTime → CookTime → Tags → Source → first Ingredient row controls → first step row's [Step | Section] toggle → first step's chip composer container → first step's "Insert ingredient" button → first step's Delete button → second step row → ... → "Add Step" button → "Save" button.
- [ ] Press Shift+Tab. Focus moves backward through the same chain. No focus traps.
- [ ] Verify there is no visible focus on a "hidden" element (e.g. a closed MudMenu).

### Item 2 — Backspace and Arrow-Left chip-removal semantics
- [ ] Author a step containing one ingredient chip ("Add @Salt"). Place caret immediately after the chip. Press Backspace once. The chip is removed. The underlying `[Salt](#1)` markdown is also gone (verify by toggling to "View as text" or by reading the chip count).
- [ ] Author another chip. Place caret immediately after. Press Arrow-Left. Caret moves to the end of the chip body (selectable position, no entry into chip text). Press Arrow-Left again — caret advances into the preceding text segment.

### Item 3 — Screen-reader announces chips correctly
- [ ] Enable VoiceOver (macOS: Cmd-F5) or NVDA (Windows). Tab to a step's chip composer.
- [ ] An ingredient chip should announce as: `"<ingredient name>, button"` or `"<ingredient name>, ingredient chip, button"`. Activating it (Enter / VO-Space) should open the replace-popover with focus moved into it.
- [ ] Press Escape on the popover. Focus returns to the chip.

### Item 4 — `@`-trigger autocomplete is keyboard-only operable
- [ ] In a step text segment, type `@par`. The autocomplete picker should open at the caret position.
- [ ] Arrow Down / Up navigates the result list. Enter selects the highlighted option. The chip replaces `@par` literally.
- [ ] Type `@xyz` (no matches). Picker shows empty / "no results." Press Escape. The literal text `@xyz` remains in the segment (no auto-removal); the picker closes.

### Item 5 — Step / Section toggle is announced as a radiogroup
- [ ] Tab to the Step/Section toggle. Screen reader announces `"radiogroup"` or `"toggle group, 2 options"`.
- [ ] Arrow keys cycle between Step and Section. Enter / Space activates the highlighted option.
- [ ] When toggling Section → Step on a populated step (with timers / refs), the confirmation dialog opens and is keyboard-operable: Tab cycles between Cancel and Convert; Enter activates; Escape cancels.

### Item 6 — Inline timer suggestion popover (D-C1)
- [ ] Author a step containing "Bake 25 minutes." After the 500ms debounce, a dotted-underline appears under "25 minutes" (warning color).
- [ ] Click "25 minutes." A popover opens with "Detected: 25 minutes — Convert to a timer? [Yes] [No]." Tab to Yes; Enter. A timer chip appears below the step text.
- [ ] Author a second timer in a new step. Click No. The dotted-underline remains (the user can re-click) but no chip is added.
- [ ] Save the recipe. Reload. The accepted timer chip persists; the rejected one does not (no auto-write — Plan 04 Task 1 deletion verified).

### Item 7 — JS-interop-fail fallback (D-D4)
- [ ] Open browser DevTools. Network tab: throttle to "Offline" or block `js/recipe-chip-composer.js` via Request Blocking. Reload the editor.
- [ ] Each step's text area falls back to a `MudTextField Lines=3` displaying raw `[name](#id)` markdown. The Step/Section toggle still works (server-rendered via the existing toggle event). Save still works — the recipe round-trips through Phase 1's parser unchanged.
- [ ] No "JS interop disconnected" red snackbar appears.
- [ ] Re-enable the JS file and reload. Chip composer returns. No data loss.

### Item 8 — Color contrast on chip variants
- [ ] In light mode: ingredient chip (Color.Info) text + background pass WCAG AA contrast (~4.5:1). Timer chip (Color.Warning) passes. Error chip (red border) passes.
- [ ] Toggle dark mode. Re-verify all three chip variants. The `chip-highlight-pulse` CSS uses `--mud-palette-info-lighten` which adapts; visually verify the pulse is visible against both backgrounds.
- [ ] Verify the dotted-underline timer-suggestion is distinguishable from regular text in both modes.

### Item 9 (recommended) — IME composition (Pitfall 7)
- [ ] If you have a Japanese / Chinese / Korean IME available, enable it. Type a step in the IME (composition mode). Verify chips render correctly; the contenteditable segments do not break composition mid-character.
- [ ] Pasting formatted text from a webpage (e.g. copy a Wikipedia paragraph with bold/italic) into a chip composer text segment: only plain text is inserted (the `contenteditable="plaintext-only"` attribute strips formatting). No images or links survive the paste.

### Cooking-mode chip click → scroll-and-highlight (D-D3)

- [ ] Open a recipe with at least 5 ingredients in cooking-mode. Click an ingredient chip in the current step's text. The ingredients sidebar scrolls smoothly to the matching ingredient and pulses for ~1.5s.
- [ ] Click another ingredient chip rapidly. The first highlight is canceled before the second begins (no flicker).
- [ ] With JS interop blocked (DevTools as in Item 7), click an ingredient chip. No scroll; no error snackbar; the page remains usable.

## Sign-off

The smoke checklist above is gated by the developer's manual verification. After all items are checked, append a sign-off line to this file:

```
Verified by: <developer name>
Date: <YYYY-MM-DD>
Notes: <any deviations or accepted residual issues>
```

Once signed, mark Phase 3 complete in `.planning/STATE.md`.

---

### Auto-approval log (2026-04-26)

Verified by: auto-approved via `/gsd-execute-phase 03 --auto` (user confirmed)
Date: 2026-04-26
Notes: Checkpoint auto-approved by the user under `workflow.auto_advance: true`. **The 9 smoke items above were NOT manually walked.** This file remains the canonical reference — re-running the checklist in a real browser is recommended before shipping. The phase is marked complete on the strength of the 185 automated tests and Plans 01–04 atomic commits; the manual a11y / browser-degradation / IME items above are outstanding UAT and will surface in `/gsd-progress` until manually walked.
