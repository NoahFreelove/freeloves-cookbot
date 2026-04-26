# Phase 3: Editor UX Without Special Syntax - Context

**Gathered:** 2026-04-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Replace the manual `[name](#id)` markdown burden in `RecipeEditor.razor` (and adjacent surfaces — `PasteRawTextDialog.razor`, `CookingMode.razor`) with a chip-aware composer built on `MudAutocomplete<Ingredient>` + `MudChipSet<T>`. Close the `text:`/`section:` mutual-exclusivity footgun (CONCERNS §6) with an explicit per-step Step/Section toggle. Close the silent-rewrite footgun (CONCERNS §7) by surfacing detected timer durations as suggestion-only chips — never auto-rewriting step text on save. Cooking mode renders the same chip representation; underlying ingredient highlighting resolves from `[name](#id)` link parsing exclusively (Phase 1 D-13).

**This phase delivers a UX surface change.** No new schema fields, no AI conformance work, no migration mechanics. The canonical record is unchanged from Phase 1; the chip composer is a pure view-layer tokenization over the same text-backed string the parser sees.

**In scope (7 reqs):** EDITOR-01, EDITOR-02, EDITOR-03, EDITOR-04, EDITOR-05, EDITOR-06, EDITOR-07.

**Not in scope (deferred — do not pull forward):**
- Per-step temperature field (`OvenTempFahrenheit`) and any new schema field — Phase 4 (FEATURE-V2-01..05).
- `Recipe.TagsJson` → relational `RecipeTag` — Phase 4 (POLISH-04).
- `LegacyRecipeProjector` deletion + `Recipe.IngredientRefs` column drop — Phase 4 (POLISH-03 territory).
- AI structured output / repair loop / prompt injection wrapping — Phase 2 (already shipped).
- Encrypt-at-rest for `UserProfile.AiApiKey` — FUTURE-01.
- MudBlazor 9.x upgrade — FUTURE-10. (Composer is built on MudBlazor 8.15.)

**Parallel-safety with Phase 2:** Phase 2 shipped on 2026-04-26 (before Phase 3 starts), so the original parallel-safe constraint is moot; no coordination needed. Phase 2's "Edit and save anyway" path lands users in today's textarea editor — when Phase 3 ships, the same hand-off automatically routes through the chip composer (D-D2).

</domain>

<decisions>
## Implementation Decisions

### A. Chip Composer Interaction Model (EDITOR-01)

- **D-A1:** **Two insertion paths, single chip output.** Users can insert an ingredient reference by either:
  1. Typing `@` in the step text — opens a `MudAutocomplete<Ingredient>` overlay anchored to the caret; selecting an item replaces `@partial` with a chip.
  2. Clicking a per-step "Insert ingredient" button — opens the same `MudAutocomplete<Ingredient>` in a popover; selection inserts a chip at the current caret position.
  Both paths produce an identical chip representation backed by the same `[name](#id)` string. Test invariant: "chip from `@`-path" == "chip from button-path" given the same selected ingredient.

- **D-A2:** **Click-to-replace popover on chip.** Clicking the body of an existing ingredient chip opens a small replace-popover with two affordances: (a) re-run autocomplete to swap to a different ingredient, (b) remove the chip entirely. A small `×` icon on the chip's right edge is also a one-click remove. Backspace from the position immediately after a chip removes it (standard chip-input keyboard behavior, EDITOR-07).

- **D-A3:** **Chip displays name only — no index on chip body.** The chip text is just the ingredient name (e.g. `Salt`). The user-facing index (`#2 Salt`) lives in the Ingredients section table at the top of the editor and is what users read for human-readable instructions; it does NOT appear inside step-text chips. Reorder of ingredients does NOT visually flicker chip text — the immutable `id` is what serializes (Phase 1 D-06), and the chip name is a name-lookup, not an index-lookup.

- **D-A4:** **`[name](#id)` markdown is hidden by default; per-step "View as text / View as chips" toggle is the escape hatch.** Each step row has a small toggle that flips between (a) chip rendering (default) and (b) raw markdown editing in a `MudTextField`. Toggle state is **ephemeral / UI-only** — not persisted on `RecipeStep`, not stored in `Extras`, resets to chip view on save and reload. No new DB column. Users who want to see/edit the raw markdown can flip per-step as needed; everyone else never sees the syntax.

- **D-A5 (REQUIREMENTS edit needed):** **EDITOR-01 wording must be amended during plan-phase.** REQUIREMENTS.md currently reads `chips render with the user-facing index while the underlying string keeps the immutable id` — this conflicts with D-A3. The amendment: drop the "user-facing index" clause from EDITOR-01. New wording (proposal for the planner to lock during plan-phase):
  > **EDITOR-01:** `RecipeEditor.razor`'s step textarea is replaced with a chip-aware composer built on `MudAutocomplete<Ingredient>` + `MudChipSet<T>`. Typing `@` or clicking an "Insert ingredient" affordance opens autocomplete over recipe ingredients; selecting one inserts a chip showing the ingredient name. The underlying string keeps `[name](#id)` markdown invisibly; the immutable `id` is what serializes.
  This is a P0 docs change in the phase plan — flag it as a task, not Claude's Discretion.

- **D-A6:** **Text-view → chip-view flip with unresolved `[name](#id)`** (i.e. an `id` that doesn't match any current recipe ingredient): renders as an **error chip** (red border, error icon, name as typed). Clicking the error chip opens the same replace-popover (D-A2) so the user can pick a real ingredient or delete the chip. Save is **allowed** with unresolved chips, but the validator surfaces an `OrphanIngredient`-style warning (Phase 2 Plan 5 already added this validator warning on the canonical document) and the editor displays a save-time banner listing the affected steps. The recipe round-trips; the user is nagged but not blocked.

### B. Step / Section Toggle (EDITOR-02)

- **D-B1:** **`MudToggleGroup` (`[Step | Section]`) per step row.** Each step row carries a small segmented control with two `MudToggleItem` values — `Step` and `Section`. Clear visual state, single-click to switch. Replaces today's two-button "Add Step / Add Section Header" pattern at the bottom of the steps list — there is now ONE "Add Step" button; new rows default to `Step` kind and the user toggles to `Section` if needed. (Today's `_steps.Add(new ParsedStep { ... IsSection = true })` path collapses into a single add + post-toggle.)

- **D-B2:** **`Step → Section` toggle reuses the existing step text as the section heading.** When a user flips a populated `Step` row to `Section`, the current step text is copied into the section heading field; the user can then edit the heading. No silent data loss for the text. (Phase 1 D-02 canonical: `SectionStep(string Heading)` is the only field on a section step, so this is the only place the original text could land.)

- **D-B3:** **Section steps clear timers and ingredient refs; confirmation dialog if non-empty.** Toggling `Step → Section` drops any associated `Timers` array and any `[name](#id)` references in the (now-hidden) text — the canonical `SectionStep` has no place to store them. Behavior:
  - If the step has zero timers and zero ingredient refs: toggle silently.
  - If the step has any timers or ingredient refs: show a `MudDialog` confirmation: *"Convert to a section header? This will discard {N} timer(s) and {M} ingredient reference(s) on this step."* with `[Cancel] [Convert]` buttons. Cancel reverts the toggle; Convert proceeds and the discards apply.
  This honors EDITOR-02's "closing the `text:`/`section:` mutual-exclusivity footgun" — section steps can no longer carry timer/ingredient state by accident.

### C. Timer-Detection Suggestion UX (EDITOR-03)

- **D-C1:** **Inline highlight + click-to-convert popover, per-occurrence.** Detected timer-duration substrings in step text are visually marked with a subtle dotted underline (`text-decoration: underline dotted` styling, Color.Warning hue). Clicking the underlined substring opens a small popover anchored to the substring: *"Detected: 25 minutes — Convert to a timer? [Yes / No]"*. Per-occurrence locality — the user sees exactly which substring is being offered for conversion. Detection runs on debounced step-text edits (the existing `Immediate="true" DebounceInterval="500"` pattern in `RecipeEditor.razor:148` is the right cadence).

- **D-C2:** **Per-occurrence Yes/No only — no "Convert all" bulk affordance, no recipe-wide bulk button.** Each detected duration gets its own conversion choice. This matches EDITOR-03's spirit (*"explicit timer chips are the only persisted source"*) and forces deliberate per-conversion decisions. Most recipes have 1–3 timers; the bulk affordance saves little real time and dilutes the deliberateness.

- **D-C3:** **Accepted timer chip renders in a chip strip below the step textarea; click to edit.** Accepting a conversion drops a `MudChip<TimerEntry>` (Color.Warning, Timer icon) into a chip strip that lives directly below the step's text area (the same visual region today's `RecipeEditor.razor:151-167` uses for detected timer chips, but now: persisted, explicit, never auto-written). Clicking the chip opens a popover with three fields:
  - Duration: `MudNumericField<int>`
  - Unit: `MudSelect<TimerUnit>` (`min` / `sec` / `hr`)
  - Label: `MudTextField<string>` (optional, e.g. "simmer")
  Click the chip's `×` icon to remove. Reorder of timer chips on a step is via drag-handle (Claude's discretion: arrow buttons are also fine — see discretion list below).
  The original detected substring (e.g. *"25 minutes"*) **stays as plain text in the step body** — the chip is the persisted source of truth, and the human-readable substring is just context. Re-detection on edit does NOT offer to re-convert a duration that's already been accepted into an explicit timer chip on the same step (avoid suggestion fatigue).

### D. Edge Flows: Paste, AI Fallback, Cooking Mode, JS-Interop Fallback (EDITOR-05, EDITOR-06, EDITOR-07)

- **D-D1 (EDITOR-05 — Paste):** **Pass-through dialog: parse, close, dump into chip editor with inline error banners.** `PasteRawTextDialog.razor` stays minimal:
  1. User pastes raw text into the dialog's textarea.
  2. Dialog calls `IRecipeFormatParser.TryParse` (which Phase 1 D-10 wired through the schema stack — YAML→JsonNode→stamp version→upcast→deserialize→validate).
  3. Dialog closes immediately on parse-success (or parse-best-effort with warnings). The current numbered-list fallback in `PasteRawTextDialog.razor:51-64` is **deleted** — Phase 1's `RecipeFormatParser` now owns coercion-with-warnings, so the dialog's hand-rolled fallback is redundant.
  4. `RecipeEditor.razor` populates with whatever fields resolved; an inline `MudAlert` at the top of the editor lists unresolved-field warnings (e.g. *"Step 3: ingredient `Pomegranate` referenced but not in the ingredient list. Add it or remove the reference before saving."*). User fixes via the chip composer; standard validator gate applies on Save (Phase 1 invariant — non-conforming recipes never persist).

- **D-D2 (Phase 2 "Edit and save anyway"):** **Same code path as D-D1.** The Phase 2 D-08 fallback (validator failed twice → user sees "Edit and save anyway" → lands in `RecipeEditor.razor`) reuses D-D1's parser route + inline-banner UI. Single mental model: any path that produces a maybe-broken recipe lands in the chip editor with the same banner UI. The Phase 2 fallback ALREADY hands a parsed-best-effort recipe to the editor; Phase 3 just makes sure the receiving editor is the chip composer with the inline-banner rendering.

- **D-D3 (EDITOR-06 — Cooking mode):** **Read-only chip rendering with parity, plus clickable ingredient chips that scroll/highlight the ingredients sidebar.** `CookingMode.razor` renders the same chip visuals as the editor (Color.Info for ingredients, Color.Warning for timers) but in a read-only state — the user can't edit chips in cooking mode. Two interaction affordances:
  - **Ingredient chips are clickable** — clicking an ingredient chip in a cooking-mode step scrolls the existing ingredients sidebar to that ingredient and visually highlights it (matching its `[name](#id)` resolved entry, scaled to `_targetServings`). Implementation: JS interop call to scroll-into-view + a transient highlight class.
  - **Timer chips retain today's start-timer button treatment** — the existing `CookingMode.razor:64-79` chip→button mapping (active timers, start, FormatTime) stays; chip visuals just align with the editor's `Color.Warning`/Timer-icon styling.
  Underlying highlight resolution for the existing `RecipeStepTextFormatter.ToHtml` path (already used in `CookingMode.razor:53,58`) is `[name](#id)` link-resolution only — Phase 1 D-13 already deleted the substring-match fallback; Phase 3 just confirms cooking mode reads the new chip rendering for the same string.

- **D-D4 (EDITOR-07 — JS-interop fallback):** **Plain `MudTextField` textarea fallback for step text; Save always works.** If the chip composer's JS interop fails to initialize (caught at component-mount time via try/catch around the JS module import or by detecting that the interop module is unreachable), each step falls back to today's `MudTextField Lines=3` showing the raw `[name](#id)` text. The chip toggle, `@`-trigger autocomplete, replace-popover, and inline timer-suggestion popovers are all absent in the fallback, but Save still works — the canonical record is text-backed (`SUMMARY.md` Q2), so the raw `[name](#id)` text round-trips through the parser unchanged. This is **the** load-bearing invariant for Phase 2's "Edit and save anyway" path: never block Save on a JS-interop failure.
  - In cooking mode (D-D3), JS-interop failure means: ingredient chips render as visuals only (click is a no-op, no scroll-on-click), timer chips fall back to a non-interop rendering equivalent to today's start-timer button (which already works without the chip-specific JS module). No error banner; the page just degrades silently to a less-interactive but functional state.

### Claude's Discretion

These were not gray areas the user needed to weigh in on; the planner can make the calls during plan-phase.

- **Ingredient reorder mechanism on the Ingredients table** — drag handles vs. today's up/down arrow buttons vs. both. EDITOR-04's invariant (preserve `id` across reorder) is locked by Phase 1 D-06; the affordance choice is open. Recommend: **keep today's arrow buttons + add drag handles** (additive change, no regressions).
- **Timer regex broadening per CONCERNS §7** — today's `\d+ (minutes|mins|hours|hrs|seconds|secs)` regex misses fractional times ("1 1/2 hours"), ranges ("20-25 minutes"), word-form numbers ("ten minutes"), and multi-segment timers ("1 hour 30 minutes"). Phase 3 SHOULD broaden detection during planning (since the inline-highlight UX in D-C1 makes the gap more visible). Suggested scope: fractional + ranges + multi-segment; word-form numbers can be deferred to a backlog item if not trivial.
- **Specific Tab/Shift+Tab/Backspace/Arrow keyboard semantics inside the chip composer** — standard chip-input UX, refer to MudBlazor `MudChipSet<T>` defaults; planner verifies during plan-phase that defaults satisfy EDITOR-07.
- **axe-core / accessibility test mechanism** — unit test, CI gate, or manual smoke pass. Planner picks; recommend a manual smoke checklist documented in the phase verification artifacts (matches existing testing posture; no Playwright/Selenium dep in the project today).
- **Replace-popover internals (D-A2)** — `MudPopover` vs. `MudMenu` vs. custom. MudBlazor 8.15 has both; planner picks based on positioning behavior near the chip.
- **Confirmation-dialog framework for Step→Section drop (D-B3)** — `MudDialog` (existing pattern in `PasteRawTextDialog`, `ShareCookbookDialog`) is the obvious fit.
- **Inline-highlight CSS approach for D-C1** — pre-rendered `<span>` wraps via `RecipeStepTextFormatter` modification, or DOM-mutation via JS interop on the rendered textarea. Planner picks; recommend `RecipeStepTextFormatter` extension since it already owns the rendered HTML.
- **Whether to extract a `RecipeStepEditor.razor` component or keep step rendering inline in `RecipeEditor.razor`** — `RecipeEditor.razor` is 468 lines today; pulling each step row into a `RecipeStepEditor` component keeps `RecipeEditor.razor` close to its current size and isolates the chip-composer surface. Recommend extract; planner confirms.
- **Whether to extract a `RecipeChipComposer.razor` shared component used by both the editor and cooking-mode rendering paths** — likely yes; shared component reduces duplication and lets the read-only cooking-mode mode flag through a parameter (`Interactive` bool).
- **File layout** — components likely live under `src/CookBot.Web/Components/Pages/RecipeEditor/` (new folder) with the existing `RecipeEditor.razor` as the entry, plus `RecipeStepEditor.razor`, `RecipeChipComposer.razor`, `IngredientChip.razor`, `TimerChip.razor` siblings. Planner confirms.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents (researcher, planner, executor) MUST read these before planning or implementing.**

### Project & Roadmap
- `.planning/PROJECT.md` — project context, validated capabilities, active scope ("Recipe-mode UX without special syntax"), constraints (MudBlazor 8.15 + Blazor Server + .NET 10)
- `.planning/REQUIREMENTS.md` — Phase 3 owns EDITOR-01..07 (7 reqs). **EDITOR-01 wording is amended during plan-phase per D-A5** — the planner must include this docs change as an explicit task.
- `.planning/ROADMAP.md` §"Phase 3: Editor UX Without Special Syntax" — phase goal, success criteria, dependency invariants (depends on Phase 1 only; Phase 2 already shipped)
- `.planning/STATE.md` Accumulated Decisions Q2 (text-backed model — `[name](#id)` stays as wire-level), Q9 (no scaling of timers/temps)

### Prior Phase Context (deliverables this phase consumes)
- `.planning/phases/01-canonical-format-foundation/01-CONTEXT.md` — full Phase 1 decisions; especially:
  - **D-02** — `StepNode` polymorphism (`ContentStep(string Text, IReadOnlyList<TimerEntry>? Timers)` and `SectionStep(string Heading)`); the boolean `IsSection` flag is NOT in the canonical record. Phase 3's toggle round-trips between these two `kind` discriminator values.
  - **D-06** — Ingredient `id` is the per-recipe local int, immutable across edits. Chip composer relies on this for reorder safety.
  - **D-08** — `RecipeValidator` returns errors-as-data, never throws. Phase 3 surfaces `OrphanIngredient`/`EmptySection` warnings as save-time banners.
  - **D-13** — `Recipe.IngredientRefs` write path retired; `RecipeStepTextFormatter` resolves `[name](#id)` links exclusively (no substring matching). Phase 3 uses the same path for chip rendering.
  - **D-19/D-20** — `IRecipeSchemaDocumentationProvider` is the single AI prompt source; Phase 3 doesn't touch it.
- `.planning/phases/01-canonical-format-foundation/01-VERIFICATION.md` — what Phase 1 actually shipped (sanity-check before consuming).
- `.planning/phases/02-ai-structured-output-conformance/02-CONTEXT.md` — full Phase 2 decisions; especially:
  - **D-08** — "Edit and save anyway" lands user in `RecipeEditor.razor`; Phase 3's chip composer is the receiving surface (D-D2).
  - **Plan 02-05 SUMMARY** — `RecipeValidator` warnings (`OrphanIngredient`, `EmptySection`) are already implemented; Phase 3 surfaces them in the save-time banner UI.
- `.planning/phases/02-ai-structured-output-conformance/02-VERIFICATION.md` — what Phase 2 actually shipped.

### Codebase
- `.planning/codebase/CONCERNS.md` §5 — ingredient-ref special syntax (Phase 3 closes via D-A1..A6).
- `.planning/codebase/CONCERNS.md` §6 — `text:` vs `section:` mutual exclusivity (Phase 3 closes via D-B1..B3).
- `.planning/codebase/CONCERNS.md` §7 — timer detection in two incompatible places (Phase 3 closes via D-C1..C3 + the Claude's-discretion timer-regex broadening).
- `.planning/codebase/ARCHITECTURE.md` §"Recipe Editor" — current editor surface, how it consumes `IRecipeFormatParser` and `IngredientRefDetectionService` (the latter's substring fallback was already deleted by Phase 1 D-13; Phase 3 just confirms the call site is gone).
- `.planning/codebase/STRUCTURE.md` — directory layout (where new `RecipeStepEditor.razor` / `RecipeChipComposer.razor` slot in).
- `.planning/codebase/STACK.md` §"UI" — MudBlazor 8.15 components available; `MudAutocomplete<T>`, `MudChipSet<T>`, `MudChip<T>`, `MudToggleGroup<T>`, `MudPopover`, `MudDialog` are all on this version.
- `.planning/codebase/CONVENTIONS.md` — Razor + Blazor Server patterns, `@rendermode InteractiveServer` everywhere, `OnAfterRenderAsync(firstRender)` data-load pattern.
- `.planning/codebase/TESTING.md` — xUnit 2.9.2 + bUnit (if present); Phase 3 likely needs UI-component tests for the chip composer behavior.

### Source files this phase modifies (start here)
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — major rewrite:
  - Steps section (lines ~108–182) replaced with chip-aware composer.
  - Today's "Add Step" + "Add Section Header" buttons (lines ~113–120) collapse into a single "Add Step" button + per-step `MudToggleGroup` for `Step | Section` (D-B1).
  - Today's `TimerDetectionService.DetectTimers(...)` + `DetectIngredientRefsInStep(...)` chip rendering (lines ~144–167) replaced with the new inline-suggestion popover + persisted timer chip strip (D-C1, D-C3).
  - New top-of-editor `MudAlert` for parse-time warnings on landing from paste / AI fallback (D-D1, D-D2).
- `src/CookBot.Web/Components/Pages/CookingMode.razor` — chip rendering via the shared `RecipeChipComposer` (read-only mode, D-D3); ingredient-chip click → JS-interop scroll/highlight on the ingredients sidebar.
- `src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor` — the hand-rolled numbered-list fallback (lines 51–64) is deleted; dialog becomes a thin `IRecipeFormatParser.TryParse → close` flow (D-D1).
- `src/CookBot.Application/Services/RecipeStepTextFormatter.cs` — likely extended (Claude's discretion) to emit `<span data-timer-suggestion>` wraps around detected timer substrings in step text rendering, so the chip composer's inline-highlight UX (D-C1) reads from the same formatter both in editor and cooking mode. Or: leave the formatter alone and do DOM mutation via JS interop. Planner picks.
- `src/CookBot.Application/Services/TimerDetectionService.cs` — broadened regex per Claude's discretion (CONCERNS §7).
- `src/CookBot.Web/Components/Pages/AiChat.razor` — verify the "Save recipe → opens RecipeEditor" path (Phase 2 Plan 4) lands cleanly in the new chip composer with the inline-banner UI (D-D2). Likely no AiChat changes; if any, scope-limited to verification only.

### Source files this phase creates
- `src/CookBot.Web/Components/Pages/RecipeEditor/RecipeStepEditor.razor` — per-step row component (chip composer + Step/Section toggle + timer chip strip).
- `src/CookBot.Web/Components/Pages/RecipeEditor/RecipeChipComposer.razor` — shared chip-composer surface used by `RecipeStepEditor.razor` (interactive) and `CookingMode.razor` (read-only via `Interactive` bool parameter).
- `src/CookBot.Web/Components/Pages/RecipeEditor/IngredientChip.razor` — ingredient chip with `@`-trigger and replace-popover (D-A1, D-A2).
- `src/CookBot.Web/Components/Pages/RecipeEditor/TimerChip.razor` — explicit timer chip with edit popover (D-C3).
- `src/CookBot.Web/Components/Pages/RecipeEditor/InlineTimerSuggestion.razor` — the dotted-underline + click-to-convert popover overlay (D-C1).
- `src/CookBot.Web/Components/Pages/RecipeEditor/SectionDropConfirmationDialog.razor` (or use inline `MudDialog.Show`) — Step→Section drop-confirm (D-B3).
- `src/CookBot.Web/wwwroot/js/recipe-chip-composer.js` (or extend an existing JS module) — chip-position / caret-anchored autocomplete + JS interop entry points; defines the surface that fails-soft when interop is unreachable (D-D4).
- `tests/CookBot.Tests/Web/RecipeChipComposerTests.cs` (bUnit if available) — chip insertion via `@`-path vs button-path produces the same string; replace-popover swaps cleanly; click-to-remove and Backspace-to-remove both work.
- `tests/CookBot.Tests/Web/StepSectionToggleTests.cs` — `Step → Section` reuses text as heading; `Section → Step` toggle; non-empty `Step → Section` shows confirmation dialog and respects Cancel.
- `tests/CookBot.Tests/Web/TimerSuggestionTests.cs` — detection on debounced edit; per-occurrence Yes/No flow; accepted timer chip persists; second edit pass doesn't re-suggest already-converted durations.
- `tests/CookBot.Tests/Web/PasteFlowTests.cs` — paste pass-through (D-D1) populates editor with parsed fields + warnings banner; Phase 2 fallback path reuses the same banner (D-D2).

### External docs
- [MudBlazor 8.15 — `MudAutocomplete<T>`](https://mudblazor.com/components/autocomplete) — `SearchFunc`, `CoerceText`, `CoerceValue`, custom rendering options.
- [MudBlazor 8.15 — `MudChipSet<T>`](https://mudblazor.com/components/chipset) — `MudChip<T>` rendering, click handling, removable chips.
- [MudBlazor 8.15 — `MudToggleGroup<T>`](https://mudblazor.com/components/togglegroup) — `MudToggleItem` with two-value enum binding.
- [MudBlazor 8.15 — `MudPopover` / `MudMenu`](https://mudblazor.com/components/popover) — anchored popover positioning for chip-replace and timer-suggestion overlays.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`IRecipeFormatParser`** (`src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs`) — Phase 1 rewrote the implementation to delegate to the schema stack. Public surface unchanged. The Paste flow (D-D1) and Phase 2 "Edit and save anyway" (D-D2) both consume this same `TryParse` method. No interface change needed for Phase 3.
- **`RecipeStepTextFormatter`** (`src/CookBot.Application/Services/RecipeStepTextFormatter.cs`) — Phase 1 D-13 made link-resolution the only path (substring fallback deleted). The chip composer's HTML rendering for chips uses this same formatter for the underlying text run; cooking mode also already uses it (`CookingMode.razor:53,58`). Phase 3 may extend it to wrap detected-timer substrings in `<span>`s for the inline-highlight overlay (D-C1) — Claude's discretion.
- **`RecipeValidator`** (`src/CookBot.Application/Recipes/RecipeValidator.cs`) — Phase 1 D-08 + Phase 2 Plan 5 — already returns `OrphanIngredient` and `EmptySection` warnings (per AI-SPEC §1b). Phase 3's save-time banner UI (D-A6, D-D1) reads `ValidationResult.Warnings` directly.
- **`Recipe.CanonicalDocumentJson`** column (Phase 1 D-12) — Phase 3 doesn't touch persistence; the chip composer reads from / writes to `_steps` (in-memory list of `ParsedStep`) the same way today's editor does. `RecipeService.UpdateAsync` continues to project from relational columns + write `CanonicalDocumentJson` in the same code path Phase 1 set up.
- **`TimerDetectionService`** (`src/CookBot.Application/Services/TimerDetectionService.cs`) — currently runs in two places (CONCERNS §7); Phase 3's D-C1 wires it to the inline-suggestion overlay only. The auto-write-on-save path in `RecipeService.CreateAsync` / `UpdateAsync` (lines 65 and 125) is **deleted** as part of Phase 3 (EDITOR-03's "auto-write of timers from regex on save is removed").
- **`MudAutocomplete<string>`** existing usage in `RecipeEditor.razor:72-77` for ingredient name input on the Ingredients table — Phase 3's `@`-trigger autocomplete in step text uses the same component but bound to `MudAutocomplete<Ingredient>` (typed) rather than `<string>`.
- **`MudChip<string>` existing usage** in `RecipeEditor.razor:154-165` for detected-timer + detected-ingredient chips — Phase 3 replaces these with persisted `MudChip<TimerEntry>` (Color.Warning) and `MudChip<Ingredient>` (Color.Info) bound through the shared `IngredientChip` / `TimerChip` components.
- **`PasteRawTextDialog.razor`** — kept, but trimmed to a thin `Parser.TryParse → close` flow; the hand-rolled numbered-list fallback (lines 51–64) is deleted (D-D1). The dialog still exists as the entry point for "I have raw text I want to paste."
- **`UserGuard` cascading user check** — Phase 3 component reuses the same wrapper pattern for the editor pages (no auth changes).

### Established Patterns

- **DI registration via per-project extension** — no new application services in Phase 3; all new components are Razor components in `CookBot.Web` and don't need DI registration beyond MudBlazor's existing setup.
- **`@rendermode InteractiveServer` on every interactive page** — Phase 3 components inherit this from the parent page (`RecipeEditor.razor`, `CookingMode.razor`).
- **JS interop module pattern** — existing `cooking-timers.js` module shows how the project does JS interop for in-page DOM manipulation (browser notifications, timer countdown). The chip-composer JS module follows the same pattern: `wwwroot/js/recipe-chip-composer.js` exposes `caretPositionInTextarea`, `scrollIntoView`, etc., loaded via `IJSRuntime.InvokeAsync`. Catch interop exceptions in the component's `OnAfterRenderAsync` and fall back to the textarea path (D-D4).
- **`OnAfterRenderAsync(firstRender)` for one-shot data loads** — Phase 3 keeps this pattern for editor initialization (load recipe → populate `_steps`).
- **MudBlazor `Color.Warning` for timer chips, `Color.Info` for ingredient chips** — already established in `RecipeEditor.razor:154,161`; Phase 3 keeps the convention.
- **`Severity.Warning` `MudAlert` for non-blocking issues** — pattern from `PasteRawTextDialog.razor:14-19` and `AiChat.razor:121-126`. Phase 3's save-time banner uses `Severity.Warning` for unresolved-ref / orphan-ingredient warnings; `Severity.Error` blocks save only when the validator returns errors (not warnings).
- **Tests scaffold** — `tests/CookBot.Tests/` already has Phase 1 + Phase 2 test files; Phase 3 adds a `tests/CookBot.Tests/Web/` namespace if not present. bUnit usage status: planner confirms (existing test files are Application/Infrastructure-level, not Razor-component-level — bUnit may need to be added; if so it's a one-package addition).

### Integration Points

- **`Program.cs`** — no changes; new components register through MudBlazor's existing setup.
- **`RecipeService.CreateAsync` / `UpdateAsync`** — auto-detect-on-save path for timers is **removed** (EDITOR-03). The save now reads `step.Timers` from the chip composer's persisted state directly. (Phase 1 D-12 already added `CanonicalDocumentJson` write; that stays.)
- **`AiChat.razor` save-recipe → editor hand-off** — Phase 2 Plan 4 wired this; Phase 3 confirms it lands cleanly in the chip composer with the inline-banner UI on validation warnings (D-D2). No AiChat changes expected.
- **`CookingMode.razor` ingredient highlighting** — already routes through `RecipeStepTextFormatter.ToHtml` (Phase 1 D-13). Phase 3 wraps the rendered HTML in the shared `RecipeChipComposer` (read-only) so the ingredient runs render as visual chips matching the editor; click-to-scroll uses a new JS-interop entry point in `recipe-chip-composer.js`.
- **`Migrations/CookBotDbContextModelSnapshot.cs`** — no changes; Phase 3 doesn't touch persistence.

</code_context>

<specifics>
## Specific Ideas

- **Two insertion paths must produce the same chip.** The user wants both `@`-trigger (power-user familiar) and the explicit "Insert ingredient" button (discoverable for first-timers) — but with a hard test invariant that both paths produce an identical underlying `[name](#id)` string given the same selected ingredient. This is a Phase 3 testability anchor, not just a UX bullet.

- **Click on a chip = swap or remove popover, NOT remove-only.** Differs from the recommended default (remove-on-click). This makes the chip a richer interactive object — every chip has a popover anchor — and the planner needs to confirm that 50+ chips in a complex recipe don't introduce render-time pain. If perf is an issue, popover anchors can be lazy (instantiated on click via `@onclick` rather than always-mounted).

- **Per-step "View as text / View as chips" toggle is ephemeral, not persisted.** Avoids adding a column or `Extras` field. The toggle is purely a view-layer affordance for the user who wants to peek at the raw markdown; it does not change what saves.

- **EDITOR-01 amendment is a P0 docs change.** The decision to drop "user-facing index" from EDITOR-01 wording must land in `.planning/REQUIREMENTS.md` as part of the Phase 3 plan, not after. If a downstream agent reads the un-amended EDITOR-01, it will conflict with the chip-name-only decision.

- **Cooking-mode ingredient chips are clickable and scroll-highlight the sidebar.** This is the non-default choice (the recommended was read-only, non-clickable). Adds value but introduces a JS-interop surface in cooking mode that EDITOR-07's graceful-degradation contract must cover (D-D4 second bullet).

- **Inline timer-suggestion is per-occurrence, no bulk.** Even when a long step has 4–5 detected durations, each gets its own Yes/No. Matches the spirit of "explicit chips are the only persisted source" — no easy-button to convert everything.

- **Step→Section confirmation only fires when there's something to drop.** Empty-step toggle is silent. Loaded-step toggle shows a count: "discard {N} timer(s) and {M} ingredient reference(s)" — not just a generic "are you sure?" — so the user can decide informedly.

If the user reviews this CONTEXT.md and wants to adjust any decision, the most likely revision targets are:

- **D-A2** — replace-popover-on-click vs. remove-on-click. The chosen path is heavier; if perf or implementation complexity surfaces during planning, falling back to remove-on-click + a separate "swap" affordance (e.g. the `×` button becomes a longpress menu) is reasonable.
- **D-A4** — if the per-step toggle proves to be a muddy UX decision in practice, dropping it and going chips-only is the cleaner alternative (the recommended default).
- **D-D3** — clickable cooking-mode chips can fall back to read-only-only if the JS-interop work is heavier than expected; cooking mode is read-only, so the click affordance is enhancement, not core.

</specifics>

<deferred>
## Deferred Ideas

Surfaced during synthesis or discuss-phase but not in scope for this phase:

- **Timer regex word-form numbers ("ten minutes")** — broader regex work (fractional + ranges + multi-segment) is in scope under Claude's Discretion; word-form numbers are a separate complexity tier and can be backlog-ed if the planner judges them out of scope. CONCERNS §7 listed them as a gap.
- **Per-step temperature field** (`OvenTempFahrenheit`) — Phase 4 (FEATURE-V2-01..05). Phase 3 ships the chip composer surface; Phase 4 adds the `OvenTempFahrenheit` chip (with the "Not scaled with servings" badge per ROADMAP §Phase 4 success criterion 1) into the same composer.
- **`Recipe.TagsJson` → relational `RecipeTag` table** — Phase 4 (POLISH-04). Phase 3 leaves `_tagsText` (comma-separated) untouched — the chip composer is for step text only, not tags.
- **`LegacyRecipeProjector` deletion + `Recipe.IngredientRefs` column drop** — Phase 4 (POLISH-03). Phase 3 doesn't touch persistence at all.
- **`README.md` "Recipe Format" section + format pattern docs** — Phase 4 (POLISH-05/07). Phase 3 doesn't touch README.
- **MudBlazor 9.x upgrade** — FUTURE-10. Phase 3 ships on MudBlazor 8.15.
- **Partial-JSON streaming UX in AI recipe generation** — Phase 2 already chose compose-then-reveal (Phase 2 D-01); Phase 3 inherits that — there is no streaming UI surface to design here.
- **axe-core / Playwright UI test infrastructure as a CI gate** — Phase 3 lands on a manual smoke checklist; the CI gate is a separate infrastructure milestone if the user wants it.
- **Drag-and-drop for step reordering** — today's up/down arrow buttons (`RecipeEditor.razor:130-134`) are kept; drag-and-drop is a UX nicety that the planner can add to step rows under discretion, but it's not part of EDITOR-04's invariant. Listed here so it doesn't surprise anyone if it's not in the phase delivery.
- **Encrypt-at-rest for `UserProfile.AiApiKey`** — FUTURE-01.
- **Per-sharer cookbook-import consent banner** — FUTURE-12 (Phase 2 deferred this).

### Reviewed Todos (not folded)

(No pending todos in `.planning/todos/pending/` — none to evaluate.)

</deferred>

---

*Phase: 03-editor-ux-without-special-syntax*
*Context gathered: 2026-04-26*
*EDITOR-01 amendment flagged as P0 plan-phase task per D-A5 (chip displays name only; "user-facing index" clause to be removed from REQUIREMENTS.md).*
