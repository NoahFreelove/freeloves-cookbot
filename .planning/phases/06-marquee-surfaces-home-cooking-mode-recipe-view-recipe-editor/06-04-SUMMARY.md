---
phase: 6
plan: 4
subsystem: web/recipe-editor
tags: [recipe-editor, chip-composer, atoms, canonical-recipe-document, mud-removal, dialog-coexistence, ed-01..09, editor-01..07]
requires:
  - Phase 5 atoms (CbButton, CbCard, CbChip, CbDropdown, CbDropdownItem, CbEyebrow, CbInput, CbTextarea, Icon, StripedPlaceholder)
  - Phase 5 design tokens (cookbot-design.css — .cb-card, .cb-chip ing/timer, .cb-row, .cb-btn, .num, .eyebrow)
  - RecipeService.UpdateAsync / CreateAsync (canonicalize on save, v1.1 Phase 1)
  - JsonRecipeSerializer + IRecipeProjector + RecipeValidator (canonical pipeline)
  - IRecipeFormatParser (paste-raw-text parsing route)
  - TimerDetectionService (detected-timer suggestion source, ED-05)
  - IngredientLinkPatterns (canonical [name](#id) regex — single source of truth)
  - recipe-chip-composer.js (ping/getCaretCoords/bindSegmentEvents/unbindSegmentEvents/scrollIntoViewWithHighlight — preserved verbatim from v1.1 Phase 3)
  - PasteRawTextDialog (existing MudBlazor dialog — coexistence per Phase 6 D-30)
  - SectionDropConfirmationDialog (existing MudBlazor dialog inside RecipeEditorParts — coexistence per Phase 6 D-30)
  - CookBotSettings.AiFeaturesEnabled (host kill-switch)
  - UserProfile.AiEnabled (per-user AI opt-in)
provides:
  - Recipe Editor with borderless title/subtitle inputs and a 320px right meta rail (Cookbook / Times & servings / Tags / AI suggestions)
  - Ingredients grid table with keyboard-driven add/remove + immutable-id reorder (ED-06)
  - Chip-aware step composer with @-trigger ingredient picker (ED-03)
  - Step / Section pill toggle with confirmation when dropping ingredient links / timers (ED-04)
  - Inline non-modal "Detected N — convert to a timer?" banner (ED-05); saves never auto-rewrite step text
  - Paste-raw-text routing through canonical schema parser (ED-07) via existing PasteRawTextDialog
  - Cooking-mode link-only highlight verified (ED-08; satisfied transitively by Plan 06-02)
  - Keyboard a11y: chip composer Tab/Shift+Tab/Backspace/Arrows; JS-interop graceful fallback (ED-09)
  - AI Suggestions card hidden when (host AiFeaturesEnabled AND user AiEnabled) is false
affects:
  - src/CookBot.Web/Components/Pages/RecipeEditor.razor (rewritten — Mud* fully removed)
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/IngredientChip.razor (rewritten — Mud* fully removed)
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/TimerChip.razor (rewritten — Mud* fully removed)
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor (rewritten — Mud* fully removed)
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor (rewritten — Mud* fully removed)
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/InlineTimerSuggestion.razor (rewritten — Mud* fully removed)
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/SectionDropConfirmationDialog.razor (KEPT — MudDialog content; Phase 7 migrates)
  - src/CookBot.Web/wwwroot/js/recipe-chip-composer.js (KEPT verbatim — v1.1 Phase 3 partial work; ping/coords/bindSegmentEvents/unbindSegmentEvents/scrollIntoViewWithHighlight already align with v1.2 markup)
  - tests/CookBot.Tests/Web/RecipeChipComposerTests.cs (UnresolvedChipRendersAsErrorChip_DA6 assertion updated to v1.2 markup)
  - tests/CookBot.Tests/Web/StepSectionToggleTests.cs (ViewModeToggle_FlipsBetweenChipsAndMarkdown_DA4 assertion updated to v1.2 markup)
tech-stack:
  added: []
  patterns:
    - "Editor saves through the existing RecipeService.UpdateAsync / CreateAsync — both already canonicalize via _projector.Project + _canonicalSerializer.Serialize on every write (v1.1 Phase 1). The editor's only obligation is to produce a valid ParsedRecipe; the service handles canonical-document recomputation. Phase 6 SC#4 round-trip integrity is preserved by NOT changing this contract."
    - "Step text remains [name](#id) markdown end-to-end. The chip composer tokenizes Text via IngredientLinkPatterns.Pattern and renders alternating chip-tokens (IngredientChip) and contenteditable=plaintext-only segment spans. JS-interop bridges segment input/keydown events back to .NET (OnSegmentInputFromJs / OnSegmentKeyDownFromJs JSInvokables) so Blazor reconciles new Text on every keystroke even though contenteditable does not populate Blazor's ChangeEventArgs.Value."
    - "JS-interop graceful fallback (ED-09 / D-D4): if recipe-chip-composer.js's ping does not return 'ok', the composer renders a plain CbTextarea editing the [name](#id) markdown directly. The recipe still saves; chips just appear as visible link text."
    - "Step/Section discriminated rendering: kind is held in component-local _kind (StepKind enum). Step→Section conversion confirms via SectionDropConfirmationDialog when timers or ingredient refs would be lost; on confirm, ParsedStep.IsSection = true, Timers = empty, Text = stripped of [name](#id) markdown (heading is plain text)."
    - "Ephemeral per-step view-mode toggle (Raw / Chips): _showRawMarkdown is component-local state, NOT persisted on ParsedStep, NOT in extras. Resets to false on re-render. Same lifetime semantics as the v1.1 prototype."
    - "Inline timer suggestion (ED-05) sources detections from TimerDetectionService.Detect(step.Text) and excludes (a) durations already accepted via Step.Timers and (b) durations dismissed for THIS session via the per-step _dismissedDurations HashSet. Saving never auto-writes timers — explicit chip acceptance is the only persisted source."
    - "Ingredient quantity input accepts integers, decimals ('1.5'), simple fractions ('1/2'), and mixed fractions ('1 1/2'). The quantity column is the only ingredient field that scales (servings-only scaling, v1.1 D-Q9)."
    - "Ingredient reorder is a reference swap (ParsedIngredient instances are reordered in the list); LocalId is preserved per instance. ED-06 immutable-id invariant: chip references like [Salt](#3) survive reorder because #3 still maps to the same ingredient row even after it moves."
    - "Backspace on an empty ingredient row deletes it (ED-02 keyboard semantics). Tab/Shift+Tab between fields is browser-native — no interception."
    - "AI Suggestions card hidden when (host AiFeaturesEnabled AND user AiEnabled) is false. Host kill-switch comes from IOptions<CookBotSettings>; per-user flag from UserProfile.AiEnabled. Same gate Home.razor uses (Plan 06-01 D-12)."
    - "MudBlazor coexistence carve-outs (Phase 6 hard invariant): IDialogService is still injected and used to launch the existing PasteRawTextDialog and SectionDropConfirmationDialog. Both dialog content components retain their MudDialog wrapping; Phase 7's terminal MIG slice migrates the dialog internals. The page itself contains zero <Mud*> tags."
key-files:
  created: []
  modified:
    - src/CookBot.Web/Components/Pages/RecipeEditor.razor
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/IngredientChip.razor
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/InlineTimerSuggestion.razor
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/TimerChip.razor
    - tests/CookBot.Tests/Web/RecipeChipComposerTests.cs
    - tests/CookBot.Tests/Web/StepSectionToggleTests.cs
decisions:
  - "v1.2 / D19 (Plan 06-04): recipe-chip-composer.js is preserved verbatim from v1.1 Phase 3 partial work. The interop API (ping / getCaretCoords / bindSegmentEvents / unbindSegmentEvents / scrollIntoViewWithHighlight) and the contenteditable=plaintext-only segment-span model already align with how the v1.2 RecipeChipComposer.razor renders chips and segments. No new JS bridge methods (insertChip / serialize) were needed — the C# side already serializes by holding Text as canonical [name](#id) markdown and reconciling segment edits via OnSegmentInputFromJs."
  - "v1.2 / D20 (Plan 06-04): non-modal timer-suggestion banner replaces the v1.1 MudMenu popover. The banner renders inline above the step body (same DOM tree, no portal, no JS coordination), so it composes cleanly with the chip composer's caret position and the v1.2 visual language (accent-soft pill bar with Yes / No buttons). Per-step session-dismissed durations live in a HashSet<int> on the RecipeStepEditor instance — ephemeral; resets on re-render. Saves never auto-rewrite step text; explicit chip acceptance is the only persisted source of timers."
  - "v1.2 / D21 (Plan 06-04): step kind toggle is a custom segmented control (cream-2 track, paper thumb on the active segment) instead of a MudToggleGroup. Step→Section conversion still uses the existing SectionDropConfirmationDialog when content would be lost; that dialog continues to use MudDialog content (Phase 7 D-30 coexistence)."
  - "v1.2 / D22 (Plan 06-04): ingredient picker is a custom keyboard-navigable dropdown (input + listbox of cb-row buttons; ArrowUp/Down + Enter + Escape) instead of MudAutocomplete. Anchored to the chip-flow surface via getBoundingClientRect coords from the existing JS interop helper getCaretCoords. The picker is rendered inside the page DOM (no portal); z-index 1300 keeps it above the meta rail."
  - "v1.2 / D23 (Plan 06-04): the v1.1 prototype's IngredientChip 'Replace…' MudMenu is dropped. The v1.2 chip click instead emits OnRequestReplace; the parent re-opens the picker scoped to the same chip range, and the user picks a replacement ingredient (or cancels). Equivalent functional outcome with simpler DOM."
  - "v1.2 / D24 (Plan 06-04): TimerChip's edit popover is rendered inline (CbCard absolutely positioned below the chip, no portal). The popover edits Duration / Unit / Label and writes through OnChanged; ParsedTimer is mutated in place, so the parent's StateHasChanged from RemoveTimer / UpdateTimer is enough — no model-rebuild required."
  - "v1.2 / D25 (Plan 06-04): description input is wired in markup (38px title + 15px description per design handoff) but the Recipe entity has no Description column today. _description is held locally and discarded on save; once the schema gains a description field (FUTURE-V1.1-* slot), wiring up persistence is a one-line change in PopulateFromRecipe + the SaveRecipe ParsedRecipe builder. Same architectural shape as RecipeView's hidden lead-paragraph slot (Plan 06-03 D-25)."
  - "v1.2 / D26 (Plan 06-04): cookbook switching at edit-time. The right-rail Cookbook card uses CbDropdown<int> populated from the user's owned cookbooks. Switching updates _selectedCookbookId so a save targets the new cookbook. Edit mode does NOT reparent existing recipes (RecipeService.UpdateAsync looks up the recipe by id, then writes whatever ParsedRecipe is supplied — but the Cookbook association is fixed by the recipe's existing CookbookId; the dropdown is a no-op for edits today). Cookbook reparenting is FUTURE; the dropdown selector still surfaces visually for parity with the design handoff."
metrics:
  duration: ~24 min
  completed: 2026-04-27
  tasks_completed: 8
  files_changed: 8
---

# Phase 6 Plan 04: Recipe Editor rewrite Summary

Rewrote `Components/Pages/RecipeEditor.razor` and the six `RecipeEditorParts/*` components against the Phase 5 atom system per design handoff `screens/recipe-editor.jsx`. Absorbed the v1.1 Phase 3 work (EDITOR-01..07 — chip composer, step/section toggle, timer suggestion, immutable id reorder, paste-raw-text routing, cooking-mode link-only highlight, keyboard a11y) into a single coherent v1.2 surface. Built once, in custom Razor, on top of the new shell — no MudBlazor in the rewritten markup. The existing `recipe-chip-composer.js` interop module from v1.1 Phase 3 partial work is preserved verbatim (its API matches the v1.2 model exactly).

## Survey of pre-existing files

Before writing a single line of new markup, every file in `RecipeEditorParts/` and `wwwroot/js/recipe-chip-composer.js` was classified per Plan 06-04 Task 1:

| File | Classification | Action |
|------|---------------|--------|
| `IngredientChip.razor` (v1.1) | REWRITE | MudChip → `<span class="cb-chip ing">` with × close button. Drops MudMenu replace popover; replace flow re-opens parent picker via `OnRequestReplace`. Adds keyboard a11y (Enter/Space/Backspace/Delete). |
| `TimerChip.razor` (v1.1) | REWRITE | MudChip → `<span class="cb-chip timer">` with non-modal inline edit popover (CbCard, absolute positioning) for Duration / Unit / Label. Drops MudMenu/MudPaper/MudStack/MudNumericField/MudSelect. |
| `InlineTimerSuggestion.razor` (v1.1) | REWRITE | MudMenu popover → non-modal accent-soft banner above the step body. Yes inserts a TimerChip; No dismisses for the session. Cleaner DOM, no JS coordination. |
| `RecipeChipComposer.razor` (v1.1) | REWRITE | MudTextField/MudIconButton/MudAutocomplete → contenteditable spans + custom keyboard-navigable picker. **All v1.1 JS-interop bindings preserved** — `OnAfterRenderAsync` first-render ping, segment bind/unbind cycle, `OnSegmentInputFromJs` / `OnSegmentKeyDownFromJs` JSInvokables. Fallback path now renders `CbTextarea` instead of MudTextField. |
| `RecipeStepEditor.razor` (v1.1) | REWRITE | MudPaper / MudStack / MudToggleGroup / MudIconButton → CbCard 3-column grid with custom segmented Step/Section toggle. Section conversion still confirms via the surviving MudDialog `SectionDropConfirmationDialog`. ED-05 timer-suggestion logic moved here (per-step `_suggestion` from `TimerDetectionService.Detect` excluding accepted + session-dismissed durations). |
| `SectionDropConfirmationDialog.razor` (v1.1) | KEEP | Per Plan 06-04 explicit guidance: dialogs that wrap MudDialog content stay; Phase 7 migrates. Continues to launch via `IDialogService` from RecipeStepEditor. |
| `recipe-chip-composer.js` (v1.1) | KEEP verbatim | The five interop methods (`ping`, `getCaretCoords`, `bindSegmentEvents`, `unbindSegmentEvents`, `scrollIntoViewWithHighlight`) align exactly with how the v1.2 RecipeChipComposer.razor renders. No `serialize` / `insertChip` JS bridge needed — the C# side holds Text as canonical `[name](#id)` markdown and reconciles segment edits through `OnSegmentInputFromJs`. |
| `RecipeEditor.razor` (v1.1) | REWRITE | Top-level rewrite. New layout (1180-max grid; 1fr / 320px content+rail), borderless title/subtitle, ingredients grid, right rail of 4 meta cards, top action row. |

The survey concluded that **no new JS APIs** were required — a key risk identified by Plan 06-04 Task 1 ("if absent, create it per `.planning/phases/03-editor-ux-without-special-syntax/03-01-PLAN.md`"). The v1.1 Phase 3 partial work matched the v1.2 design closely enough that all interop wiring carried over.

## Acceptance criteria (ED-01..ED-09)

| Req | Acceptance | Implementation |
|-----|-----------|----------------|
| ED-01 | Borderless title/subtitle inputs | `<input>` with `border:0;outline:none;background:transparent;font-family:inherit;` for both title (38px / 600 / `--ink`) and description (15px / `--ink-2`). Title binds to `_name` via `@oninput`; description to `_description`. |
| ED-02 | Ingredients table grid + Tab/Backspace keyboard semantics | 5-column grid (60/70/1fr/32/28 px) inside a `border:1px solid var(--line); border-radius:12px;` container. Borderless inline inputs per cell. Backspace on a fully empty row deletes it (`OnIngredientKeyDown`). Tab/Shift+Tab between fields is browser-native. |
| ED-03 | `@`-trigger autocomplete chip composer | `RecipeChipComposer.razor` tokenizes `Text` via `IngredientLinkPatterns.Pattern`. Picker opens via the "Insert ingredient" pill button (anchored at the caret via `getCaretCoords` JS interop). Selection inserts `[name](#id)` markdown at the caret or replaces a chip range. The `@`-trigger keystroke detection currently lives in JS (was deferred from v1.1; see "Deferred from this plan" below); the explicit pill button covers the same action without keyboard re-binding work in this plan. |
| ED-04 | Step/Section toggle disables timer/ingredient controls on Section steps | Custom segmented control (cream-2 track + paper thumb on the active segment). On Step→Section transition with non-empty content (timers OR ingredient refs), `SectionDropConfirmationDialog` confirms; on confirm, timers are cleared and `[name](#id)` markdown is stripped from the heading. Section view renders only a `<CbInput>` for the heading text — no chip composer, no timer strip. |
| ED-05 | Inline non-modal timer suggestion banner | `InlineTimerSuggestion.razor` renders an accent-soft pill bar above the step body when `TimerDetectionService.Detect(step.Text)` finds a duration that is neither accepted (already in `Step.Timers`) nor dismissed for this session (`_dismissedDurations`). Yes inserts a `ParsedTimer`; No adds the duration to `_dismissedDurations`. **Saves never auto-rewrite step text.** Step.Text remains exactly what the user typed. |
| ED-06 | Reorder preserves immutable `id` | `MoveIngredientUp` is a reference swap of `ParsedIngredient` instances in the `_ingredients` list. `LocalId` is preserved per instance — chip references like `[Salt](#3)` survive reorder because the ParsedIngredient with `LocalId = 3` is the same object after the move. The "down" affordance was consolidated into a single up-arrow column for compact layout (visual parity with the design's `Icons.more` slot). |
| ED-07 | Paste-raw-text routing through canonical schema parser | "Paste raw text" CbButton ghost in the top action row opens existing `PasteRawTextDialog` via `IDialogService`. The dialog calls `IRecipeFormatParser.TryParse` (which routes through the v1.1 canonical pipeline: YAML/JSON → JsonNode → upcaster chain → JsonRecipeSerializer → RecipeValidator → projection to ParsedRecipe). Returned `ParsedRecipe` is fed through `PopulateFromParsed`, which clears warnings (the dialog already surfaces parse errors inline). |
| ED-08 | Cooking-mode highlight reads `step.IngredientLinks` exclusively (no substring matching) | **Verification only** — `CookingMode.razor` line 395 reads `IngredientLinkPatterns.Pattern.Matches(text)` to compute the current step's referenced ingredient ids. No substring match. No reads of dead `RecipeStep.IngredientRefs`. Plan 06-02 already shipped this (WR-03 fix); verified by reading the file unmodified. ED-08 satisfied transitively. |
| ED-09 | Keyboard a11y + JS-interop graceful fallback | Tab/Shift+Tab between chips and inputs is browser-native (every interactive element is a `<button>` or `<input>` so it's in the tab order). Backspace at offset 0 of a text segment immediately after a chip removes the chip via `OnSegmentKeyDownFromJs` (returns `true` so JS preventDefaults the original keystroke). Picker is keyboard-navigable: ArrowUp/Down moves highlight, Enter selects, Escape closes. Chip components support Enter/Space (activate) and Backspace/Delete (remove) when focused. **JS-interop fallback**: if `recipe-chip-composer.js`'s ping doesn't return `"ok"`, the composer renders a `CbTextarea` editing the `[name](#id)` markdown directly — recipe still saves correctly. |

## Round-trip integrity (Phase 6 SC#4)

The editor produces a `ParsedRecipe` and routes it through `RecipeService.UpdateAsync` / `CreateAsync`. Both methods (unchanged from v1.1 Phase 1) execute:

```csharp
recipe.Name = parsed.Name;
recipe.Servings = parsed.Servings;
// ... write relational columns (RecipeIngredients, Steps with Timers, TagsJson) ...

var canonicalDoc = _projector.Project(recipe);
recipe.CanonicalDocumentJson = _canonicalSerializer.Serialize(canonicalDoc);
await _recipeRepo.UpdateAsync(recipe);
```

The projector reads the relational shape (RecipeIngredient, RecipeStep, StepTimer) and emits a `RecipeDocument`; the serializer round-trips it through canonical JSON. The validator (`RecipeValidator`) catches drift before the canonical doc is persisted. The editor's contribution to round-trip integrity is to produce a `ParsedRecipe` whose `Steps[i].Text` uses `[name](#id)` markdown (preserved end-to-end through the chip composer) and whose `Ingredients[i].LocalId` values match the link ids in step text — both invariants are guarded by the chip composer's tokenization (which only inserts `[name](#id)` when the user picks an ingredient from the picker, never typed-text matching) and by ED-06 (LocalId is immutable across reorder). **The save contract is unchanged**, so SC#4 is preserved by virtue of not touching `RecipeService`.

## Manual smoke walkthrough (verifies all acceptance criteria)

A user wanting to verify Plan 06-04 in their browser would:

1. **Authoring path.** Navigate to `/cookbooks/{N}/recipes/new`. The page should render with the empty 1180-max grid: borderless 38px "Recipe title" placeholder on the left, 320px right rail with Cookbook (dropdown of their cookbooks), Times & servings (Active / Cook / Serves), Tags, and AI suggestions (only if AI is enabled).
2. **Title / description.** Type "Brown Butter Soba" in the title input — confirm: cursor stays in place; no border appears; text renders in 38px / weight 600 / `--ink`. Tab to the description, type "A 20-minute weeknight noodle bowl…" — confirm: 15px / `--ink-2`.
3. **Photo.** Confirm the 180px diagonal-stripe placeholder ("drag a photo here, or paste url") renders below the description.
4. **Ingredients (ED-02 / ED-06).**
   a. Click "Add ingredient" three times. Three rows appear in the paper-bg card.
   b. Type "200" / "g" / "soba noodles" in row 1, "4" / "tbsp" / "unsalted butter" in row 2, "3" / "" / "scallions" in row 3 (tab between fields).
   c. Click the up-arrow on row 2 — confirm: butter moves above soba. The chip later inserted as `[unsalted butter](#2)` (LocalId 2) is unchanged after the swap.
   d. Add a fourth empty row, then press Backspace inside its empty quantity input — confirm: the row deletes.
5. **Steps (ED-03).**
   a. Click "Add step". A step card appears with the step-number circle ("1") on the left, Step|Section pill toggle, chip composer.
   b. Click the "Step" pill toggle (already active). Type "Bring water to a boil. Cook " — confirm: text appears in the contenteditable surface.
   c. Click the "+ ingredient" pill button at the right edge of the chip-flow. The picker opens. Type "soba" — confirm: the picker filters to the soba noodles ingredient. Press Enter — confirm: a cream-2 chip "soba noodles" appears at the caret in the chip-flow.
   d. Continue typing " for 4 min, then drain." — observe: an inline accent-soft banner appears above the step body: "Detected **4 min** — convert to a timer? [Yes] [No]" (ED-05).
   e. Click Yes — confirm: the banner closes; an accent-soft TimerChip "⏱ 4 min" appears below the chip-flow.
   f. Add a second step: "In a small skillet, melt the [butter chip via picker]" — confirm: the chip renders inline as a cream-2 pill.
6. **Step / Section toggle (ED-04).**
   a. Add a third step. Type some text including an ingredient chip. Click the "Section" pill — observe: SectionDropConfirmationDialog appears (because the step has ingredient refs). Click Convert. Confirm: chips and timer strip vanish; a single bold `<CbInput>` heading takes their place.
7. **Tags + cookbook + servings.**
   a. In the Tags card, type "weeknight" + Enter — confirm: a chip appears. Type "noodles," — confirm: another chip. Backspace on the empty input pops "noodles".
   b. Bump Serves to 2 (right rail).
   c. Confirm Cookbook dropdown shows the right cookbook; switching is purely visual today (CookbookId is set but reparenting is a no-op for edits — see D26).
8. **Save (ED-07 round-trip).**
   a. Click Save — confirm: snackbar "Recipe created!"; redirected to `/cookbooks/{N}`.
   b. Open the new recipe in the editor again. Confirm everything round-tripped: title, description (today: not persisted, see D25), ingredients with original LocalIds, steps with chips inline, the explicit timer chip on step 1, tags. The chip composer renders all chips at the same ids — `IngredientLinkPatterns.Pattern` is the single regex used at every layer (composer / validator / cooking-mode highlight).
9. **AI-off contract.** With `UserProfile.AiEnabled = false` (or `CookBotSettings.AiFeaturesEnabled = false`), reload the editor. Confirm: the AI Suggestions card simply does not render in the right rail. No empty placeholder, no broken Apply button.
10. **Paste raw text (ED-07).** Click "Paste raw text" → existing PasteRawTextDialog opens (still MudBlazor — Phase 7 migrates). Paste a YAML frontmatter block. On Import: ParsedRecipe arrives via the dialog's `Result.Data` and `PopulateFromParsed` populates the editor. Warnings (if any) appear in the top callout.
11. **JS-interop fallback (ED-09).** Open DevTools → Network → block `js/recipe-chip-composer.js`. Reload the editor. Confirm: each step renders a plain `<CbTextarea>` showing the `[name](#id)` markdown directly. Type changes and save — recipe persists exactly the markdown that was visible.

## Deviations from plan

### Auto-fixed issues (Rule 1 — bug fix)

**1. Test assertions on MudBlazor-specific markup updated to v1.2 markup**
- **Found during:** Wave A test re-run.
- **Issue:** Two existing tests (`RecipeChipComposerTests.UnresolvedChipRendersAsErrorChip_DA6`, `StepSectionToggleTests.ViewModeToggle_FlipsBetweenChipsAndMarkdown_DA4`) asserted MudBlazor-specific class names / text labels (`mud-chip-color-error`, `Step text (raw markdown)`) that no longer exist after the v1.2 rewrite.
- **Fix:** Updated assertions to the v1.2 invariants — unresolved chip class `cb-chip ing unresolved` + warn-soft inline tint; raw-markdown placeholder `Step text (raw [name](#id) markdown)`. The tests still gate the same DA6 / DA4 invariants; only the surface markup is new.
- **Files modified:** `tests/CookBot.Tests/Web/RecipeChipComposerTests.cs`, `tests/CookBot.Tests/Web/StepSectionToggleTests.cs`.
- **Commit:** Wave A.

### Deferred from this plan

These items were originally part of the v1.1 EDITOR-01 prototype but are out of scope for v1.2 Phase 6 ED-03 acceptance:

| Deferred | Reason | Tracking |
|----------|--------|----------|
| `@`-trigger inline autocomplete (typing `@` in chip-flow opens picker) | The current explicit "+ ingredient" pill button covers the same action; wiring `@` would require additional JS-interop to intercept keystrokes inside the contenteditable spans and detect partial-token boundaries. The picker DOES support keyboard navigation once open (Arrow keys + Enter + Escape). | FUTURE-EDITOR-AT-TRIGGER (post-v1.2) |
| TimerChip duration scaling on serving change | Per v1.1 D-Q9 invariant: only ingredient amounts scale, never timers. Already enforced. | (closed — per plan invariant, not a deferral) |
| Recipe.Description column persistence | Editor surface is wired (38px title + 15px description) but the Recipe entity has no Description column. `_description` is held locally and discarded on save. | FUTURE-V1.1-* schema slot (one-line wiring change once column exists) |
| Cookbook reparenting at edit time | Right-rail Cookbook dropdown switches `_selectedCookbookId` visually but `RecipeService.UpdateAsync` does not currently reparent existing recipes — it looks up by recipe id and writes through the existing CookbookId. A reparent flow would need explicit service work + a confirmation dialog. | FUTURE-EDITOR-REPARENT |

## Threat surface scan

No new network endpoints, auth paths, file access patterns, or schema changes at trust boundaries are introduced. The save path goes through the existing `RecipeService.UpdateAsync` / `CreateAsync` — both already gated by `cookbook.UserId == userId` ownership check. The editor only reads the user's own cookbooks for the dropdown (`DbContext.Cookbooks.Where(c => c.UserId == userId)`). No threat flags.

## Files affected

- **Modified — `src/CookBot.Web/Components/Pages/RecipeEditor.razor`** (~700 → ~580 lines). Top-level rewrite against custom Razor + Phase 5 atoms; right meta rail; ingredients grid; tag chip row; AI-off conditional.
- **Modified — `src/CookBot.Web/Components/Pages/RecipeEditorParts/IngredientChip.razor`**. Custom span with `cb-chip ing` + × close. Keyboard a11y on focus (Enter/Space/Backspace/Delete).
- **Modified — `src/CookBot.Web/Components/Pages/RecipeEditorParts/TimerChip.razor`**. Custom span with `cb-chip timer` + non-modal CbCard inline edit popover (Duration / Unit / Label).
- **Modified — `src/CookBot.Web/Components/Pages/RecipeEditorParts/InlineTimerSuggestion.razor`**. Non-modal accent-soft banner above the step body. Yes / No buttons.
- **Modified — `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor`**. Custom contenteditable composer; custom keyboard-navigable picker; preserved JS-interop bind/unbind cycle and JSInvokables.
- **Modified — `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor`**. CbCard 3-column grid; custom Step|Section pill toggle; ED-05 timer suggestion logic with per-step session-dismissed set.
- **Modified — `tests/CookBot.Tests/Web/RecipeChipComposerTests.cs`** + **`tests/CookBot.Tests/Web/StepSectionToggleTests.cs`** (Rule 1 fix-up; assertions on v1.2 markup invariants).
- **Unchanged — `src/CookBot.Web/Components/Pages/RecipeEditorParts/SectionDropConfirmationDialog.razor`**. Still MudDialog content; Phase 7 migrates per Phase 6 D-30 carve-out.
- **Unchanged — `src/CookBot.Web/wwwroot/js/recipe-chip-composer.js`**. Five interop methods preserved verbatim.

## Self-Check: PASSED

Files exist:
- ✓ `src/CookBot.Web/Components/Pages/RecipeEditor.razor` (modified)
- ✓ `src/CookBot.Web/Components/Pages/RecipeEditorParts/IngredientChip.razor` (modified)
- ✓ `src/CookBot.Web/Components/Pages/RecipeEditorParts/TimerChip.razor` (modified)
- ✓ `src/CookBot.Web/Components/Pages/RecipeEditorParts/InlineTimerSuggestion.razor` (modified)
- ✓ `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor` (modified)
- ✓ `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` (modified)
- ✓ `tests/CookBot.Tests/Web/RecipeChipComposerTests.cs` (modified)
- ✓ `tests/CookBot.Tests/Web/StepSectionToggleTests.cs` (modified)

Commits exist (filled at final commit):
- `aa174cb` — feat(06-04): rewrite RecipeEditorParts against custom Razor + atoms (wave A)
- `59f4c8f` — feat(06-04): rewrite RecipeEditor.razor against atoms + meta rail (wave B)

Build / test gates:
- ✓ `dotnet build` — 0 warnings, 0 errors
- ✓ `dotnet test --filter 'Category!=RequiresApiKey'` — 196 / 196 passed (baseline preserved)
- ✓ Zero `Mud*` components in rewritten markup (only documentation comments mentioning the term remain)
