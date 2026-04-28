---
phase: 03-editor-ux-without-special-syntax
verified: 2026-04-26T23:08:42Z
status: gaps_found
score: 4/9 must-haves verified
overrides_applied: 0
note: "Sibling file `03-VERIFICATION.md` is the manual a11y smoke checklist authored by Plan 04 Task 3 (auto-approved but not manually walked). This file is the goal-backward verifier output and does NOT replace the smoke checklist; it cross-references it under SC#5."
gaps:
  - truth: "Users can author free text in the chip composer (typing instructions like 'Bake at high heat' between chips)"
    status: failed
    reason: "WR-01: `OnSegmentInput` (RecipeChipComposer.razor:237-242) reads `e.Value?.ToString() ?? string.Empty` from a `ChangeEventArgs` raised by `@oninput` on a `<span contenteditable=\"plaintext-only\">`. Blazor's `oninput` on contenteditable elements does NOT populate `ChangeEventArgs.Value` — it stays null for non-input elements. Every keystroke replaces the segment with the empty string, silently wiping the text the user just typed. bUnit tests use the imperative `SimulateAtTriggerSelectionAsync` / `SimulateButtonInsertionAsync` helpers and never dispatch a real `oninput` event on a contenteditable span, so the bug is invisible to CI. The fallback `MudTextField` (`_jsInteropAvailable=false`) and the D-A4 view-mode toggle are the only paths where users can edit free step text right now — but the chip composer itself, the surface the phase goal centers on, is broken for free-text typing."
    artifacts:
      - path: src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor
        issue: "@oninput handler on line 31 calls OnSegmentInput at line 237 which can never read the typed text — `e.Value` is null on contenteditable. Result: every keystroke wipes the segment."
    missing:
      - "Wire JS interop to read `.textContent` of the segment span on input and pass it to a `[JSInvokable]` callback, OR bind the chip composer to a hidden `<input>` whose value mirrors the contenteditable surface, OR drop `@oninput` on segments and capture edits via a JS-side `blur`/`input` bridge that emits the full reconstructed text."
      - "Add a bUnit test that fires `oninput` with `e.Value=\"new text\"` AND uses `cut.Find(\"span[contenteditable]\").Input(\"new text\")` (or invokes the JSInvokable) so this regression is locked at CI level."
  - truth: "Phase 3 EDITOR-07 accessibility / browser-degradation smoke pass has been verified by a human"
    status: failed
    reason: "The 9-item manual smoke checklist in `03-VERIFICATION.md` (Tab/Shift+Tab nav, screen reader chip announcement, JS-interop-fail fallback, color contrast, IME composition, cooking-mode chip click → scroll-and-highlight) was auto-approved via `/gsd-execute-phase 03 --auto` under `workflow.auto_advance=true`. The auto-approval log explicitly states: 'The 9 smoke items above were NOT manually walked.' SC#5 (axe-core/screen-reader smoke pass + JS-interop graceful degradation) and EDITOR-07 cannot be verified by automation in this codebase (no axe-core, Playwright, or Lighthouse infra)."
    artifacts:
      - path: .planning/phases/03-editor-ux-without-special-syntax/03-VERIFICATION.md
        issue: "Auto-approval log notes the 9 manual smoke items as outstanding UAT; EDITOR-07 gate not satisfied."
    missing:
      - "Walk the 9-item smoke checklist (Tab navigation, Backspace/Arrow chip semantics, screen reader announcement, @-trigger autocomplete keyboard, Step/Section radiogroup, inline timer suggestion popover, JS-interop-fail fallback, chip color contrast, IME composition + cooking-mode chip scroll-and-highlight) in a real browser session and append a sign-off line with developer name + date + any deviations."
  - truth: "Cooking-mode ingredient sidebar highlights the ingredients referenced by the current step (chip-rendering parity for highlighting)"
    status: partial
    reason: "WR-03: `CookingMode.razor:146` reads `CurrentStep.IngredientRefs.Contains(ri.RecipeLocalId)` to drive the sidebar highlight. Phase 1 D-13 retired writes to `RecipeStep.IngredientRefs`; `RecipeService.CreateAsync` / `UpdateAsync` no longer populate that list. Recipes saved after the milestone have an empty `IngredientRefs` and the sidebar highlight never fires. Pre-existing recipes work until they're re-saved. Cooking-mode chip rendering inside step text is correct (uses `<RecipeChipComposer Interactive=\"false\">` and `[name](#id)` resolution) and the chip-click → `scrollIntoViewWithHighlight` path also works — only the static sidebar background-highlight is broken. EDITOR-06 SC#4 ('uses [name](#id) link resolution exclusively for highlighting (no substring matching)') is partially regressed: the link-resolution path was supposed to *replace* the IngredientRefs-driven highlight, not coexist with a now-empty IngredientRefs read."
    artifacts:
      - path: src/CookBot.Web/Components/Pages/CookingMode.razor
        issue: "Line 146 reads `CurrentStep.IngredientRefs.Contains(...)` against a list that is empty for every recipe saved after Phase 1. The sidebar highlight permanently fails to fire on freshly-saved recipes."
    missing:
      - "Replace `CurrentStep.IngredientRefs.Contains(ri.RecipeLocalId)` with a fresh `IngredientLinkPatterns.Pattern.Matches(CurrentStep.Text)` ID extraction. Suggested helper: `private HashSet<int> CurrentStepRefIds() => IngredientLinkPatterns.Pattern.Matches(CurrentStep.Text ?? \"\").Select(m => int.TryParse(m.Groups[2].Value, out var id) ? id : -1).Where(id => id > 0).ToHashSet();` Cache per render."
  - truth: "The single canonical `[name](#id)` regex lives in IngredientLinkPatterns and is the only definition in the codebase"
    status: partial
    reason: "IN-01: `src/CookBot.Application/Services/IngredientRefDetectionService.cs:8-10` still defines its own duplicate regex `private static readonly Regex MarkdownLinkPattern = new(@\"\\[([^\\]]+)\\]\\(#(\\d+)\\)\")` (note the `+` vs `*` divergence — this rejects empty display names; the canonical version accepts them). The Phase 3 plan's stated truth was 'The single canonical IngredientLinkPattern lives in one file and is consumed by RecipeStepTextFormatter, RecipeValidator, and the chip composer (no duplicate regex).' The consolidation hit those three sites but missed `IngredientRefDetectionService`. The class is effectively dead — `RecipeService` no longer calls it after the Phase 1 D-13 IngredientRefs write retirement — but the duplicate regex undermines the 'do not redefine elsewhere' doc-comment in `IngredientLinkPatterns.cs` and is a regression vector for anyone reviving the service."
    artifacts:
      - path: src/CookBot.Application/Services/IngredientRefDetectionService.cs
        issue: "Lines 8-10 redefine a near-identical [name](#id) regex (with `+` instead of `*`). The class has no production callers (only the EF Core migration mentions the now-empty IngredientRefs column)."
    missing:
      - "Delete `IngredientRefDetectionService.cs` (no production callers — `grep -rn IngredientRefDetectionService src tests` confirms only the file itself), OR swap its `MarkdownLinkPattern` for `IngredientLinkPatterns.Pattern` if any caller resurfaces."
  - truth: "Backspace at position 0 of a text segment immediately after a chip removes the preceding chip (EDITOR-07 keyboard nav semantics)"
    status: failed
    reason: "IN-03: `OnSegmentKeyDown` (RecipeChipComposer.razor:230-235) is wired as `@onkeydown` on every contenteditable text segment but the body is `return Task.CompletedTask;` with the comment 'Detection via JS-side caret position is a follow-up refinement; placeholder here.' The EDITOR-07 invariant ('Backspace at position 0 immediately after a chip removes the chip') is advertised by the wiring but never fires. Smoke-checklist Item 2 explicitly tests this and is among the unwalked items."
    artifacts:
      - path: src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor
        issue: "Lines 230-235 are a no-op placeholder. EDITOR-07 keyboard nav (Backspace-removes-prior-chip) does not work."
    missing:
      - "Either delete the `@onkeydown` wiring (avoid the impression that the feature works) or land the JS-interop bridge that detects caret-at-position-0 and removes the prior chip's substring. Track in deferred-items.md if punted."
deferred:
  - truth: "Active timers / detection regex robustness against integer overflow on adversarial inputs"
    addressed_in: "Future hardening pass (no current phase)"
    evidence: "WR-04 flags `int.Parse` overflow risk in `TimerDetectionService.ParseFractionalToSeconds` (lines 111-113). Trusted-LAN posture per CLAUDE.md (multi-user but not internet-exposed) means this is a stability concern, not a security one. No phase in the v1.1 milestone roadmap covers regex hardening."
  - truth: "Picker popover anchored at caret position renders at correct screen coordinates"
    addressed_in: "Not addressed in milestone roadmap"
    evidence: "WR-02 — `position:absolute` resolves against a non-positioned ancestor because `.chip-flow` lacks `position: relative`. The picker visibility logic works (it opens), but it appears in the wrong location. This is a UX bug uncovered by manual smoke Item 4 (`@`-trigger keyboard) which has not been walked. Recommend a follow-up issue."
human_verification:
  - test: "Walk the 9-item manual a11y smoke checklist in `03-VERIFICATION.md`"
    expected: "Each Tab/Shift+Tab path, screen-reader announcement, JS-fail fallback, color contrast, IME composition, and cooking-mode chip click works as documented; sign-off line appended with deviations."
    why_human: "No axe-core/Playwright/Lighthouse infra in this codebase. Browser-only behavior (focus order, screen reader announcements, IME composition, color contrast in dark mode) cannot be programmatically verified."
  - test: "End-to-end recipe authoring flow with WR-01 fix"
    expected: "User opens a fresh recipe, types 'Bake the' into a chip composer text segment, clicks `+` to insert a chip for 'Salt', types ' for 5 minutes', and the saved step text reads 'Bake the [Salt](#1) for 5 minutes'."
    why_human: "Confirms the WR-01 fix actually works in a real browser circuit. bUnit tests use imperative simulators that bypass the @oninput → ChangeEventArgs path; only a real Blazor Server circuit reproduces the bug."
  - test: "Cooking-mode sidebar highlight on freshly-saved recipe (after WR-03 fix)"
    expected: "Save a new recipe with chips referencing 3 ingredients. Open cooking mode, navigate to step 1. The 3 referenced ingredients in the sidebar are visually highlighted with the primary-lighten background; non-referenced ones are not."
    why_human: "Per-render sidebar highlight is a visual property; only a browser test can confirm the highlight fires. The recipe must be freshly saved through Phase 1's RecipeService to expose the empty-IngredientRefs path."
---

# Phase 3: Editor UX Without Special Syntax — Goal-Backward Verification

**Phase Goal (verbatim ROADMAP):** Users author and edit recipes (including ingredient references, timers, and section headers) through a chip-aware composer built on `MudAutocomplete<Ingredient>` + `MudChipSet<T>`; no one types `[name](#id)`, picks `text:` vs `section:`, or watches the app silently rewrite their step text.

**Verified:** 2026-04-26T23:08:42Z
**Status:** gaps_found
**Score:** 4 of 9 must-haves verified
**Re-verification:** No — initial goal-backward verification (sibling `03-VERIFICATION.md` is the unrelated manual a11y smoke checklist authored by Plan 04 Task 3).

## Goal Achievement

### Observable Truths (derived from ROADMAP Success Criteria + plan must_haves)

| # | Truth (must be TRUE for the goal) | Status | Evidence |
|---|---|---|---|
| 1 | A user can insert ingredient chips via `@`-trigger autocomplete or the "Insert ingredient" affordance, producing identical `[name](#id)` markdown for both paths (D-A1, SC#1). | VERIFIED | `RecipeChipComposer.razor:120-136` — `SimulateInsertChip` is the single helper called by both paths; `RecipeChipComposerTests.AtTriggerInsertion_AndButtonInsertion_ProduceIdenticalUnderlyingText_DA1Invariant` asserts identical output strings. Test passes. |
| 2 | Chips render with the ingredient name (D-A3 / D-A5) and an unresolved `[name](#id)` renders as a red error chip (D-A6, EDITOR-04). | VERIFIED | `IngredientChip.razor:8-25` — two `MudChip` branches: `Color.Error` for `!IsResolved`, `Color.Info` for resolved; both render `@DisplayName` (no `#id` printed). `RecipeChipComposerTests.UnresolvedChipRendersAsErrorChip_DA6` asserts `mud-chip-color-error` class. Test passes. |
| 3 | Each step has an explicit `[Step | Section]` toggle; non-empty Step → Section opens a confirmation dialog naming timer + ingredient-ref counts (D-B1/B2/B3, SC#2). | VERIFIED | `RecipeStepEditor.razor:24-32` — `MudToggleGroup<StepKind>` with `[Step | Section]` items. Lines 118-158 — `OnKindRequested` opens `SectionDropConfirmationDialog` when `timerCount + refCount > 0`; Cancel reverts via `StateHasChanged()`. `SectionDropConfirmationDialog.razor` renders TimerCount and RefCount. `RecipeEditor.razor:127-130` — single "Add Step" button (no separate "Add Section Header"); `AddSectionHeader` and `DetectIngredientRefsInStep` are both deleted from RecipeEditor.razor. |
| 4 | Cooking-mode renders step text and section headings via `<RecipeChipComposer Interactive=false>`; chip click scrolls and pulses the matching ingredient sidebar entry (D-D3, EDITOR-06 chip rendering parity). | VERIFIED (chip rendering + scroll); FAILED (sidebar highlight — see truth #9 / WR-03) | `CookingMode.razor:54-65` — both section header and step body render through `<RecipeChipComposer Interactive="false">`. Lines 144-148 — sidebar items expose `id="ingredient-{RecipeLocalId}"`. Lines 417-418 — `JS.InvokeAsync<bool>("RecipeChipComposer.scrollIntoViewWithHighlight", $"ingredient-{recipeLocalId}")` wraps in try/catch for `JSDisconnectedException`. |
| 5 | `PasteRawTextDialog.razor` Submit is a thin `Parser.TryParse → Close` pass-through; the hand-rolled numbered-list fallback (Phase 1 carryover) is deleted (SC#4 / EDITOR-05). | VERIFIED | `PasteRawTextDialog.razor:37-51` — Submit is exactly `Parser.TryParse → Close` (no fallback branch); the dialog file is 52 lines total (down from ~70 pre-phase). `PasteFlowTests` asserts the source is just a parser pass-through. |
| 6 | `RecipeService.CreateAsync` / `UpdateAsync` no longer auto-write timers from the regex; explicit `Timers` chips are the sole persisted source (SC#3 / EDITOR-03 finalization). | VERIFIED | `RecipeService.cs:74,146` — both step loops project `(ps.Timers ?? new())` directly. `grep -n "TimerDetectionService.DetectTimers"` against RecipeService returns no hits. |
| 7 | Timer detection regex broadens to fractional / range / multi-segment durations; range persists as the lowest bound; backward-compat with simple "25 minutes" preserved (EDITOR-03 regex broadening). | VERIFIED | `TimerDetectionService.cs:15-32` — `MultiSegmentPattern`, `RangePattern`, `FractionalPattern`, `SimplePattern` defined. Lines 46-49 — patterns applied in correct precedence (multi-segment first to prevent SimplePattern eating `1 hour` from `1 hour 30 minutes`). `TimerDetectionServiceRegexTests` covers all four shapes; tests pass. |
| 8 | Single canonical `[name](#id)` regex lives in `IngredientLinkPatterns.cs` consumed by formatter, validator, and the chip composer. | PARTIAL — see WR-IN-01 | `IngredientLinkPatterns.cs:11-13` exists; `RecipeValidator.cs:55,101`, `RecipeStepTextFormatter.cs:26,54,110`, `RecipeChipComposer.razor:146`, and `RecipeStepEditor.razor:148,161` all consume it. **However** `IngredientRefDetectionService.cs:8-10` still defines a duplicate `MarkdownLinkPattern` (with `+` instead of `*`). Class is dead code (no production callers) but the regex was not consolidated. |
| 9 | Cooking-mode ingredient sidebar visually highlights the items referenced by the current step (chip-rendering parity for highlight). | FAILED | `CookingMode.razor:146` reads `CurrentStep.IngredientRefs.Contains(ri.RecipeLocalId)`. Phase 1 D-13 retired writes to `IngredientRefs`; `RecipeService` no longer populates it. Highlight never fires on freshly-saved recipes. See WR-03. |

**Score:** 4/9 must-haves fully verified. 2 partial. 3 failed.

### Deferred Items

| # | Item | Addressed In | Evidence |
|---|---|---|---|
| 1 | Timer regex `int.Parse` overflow hardening (WR-04) | Future hardening pass (no current phase) | Trusted-LAN posture; no v1.1 milestone phase covers regex hardening. |
| 2 | Picker popover positioning bug (WR-02 — `.chip-flow` needs `position: relative`) | Not addressed in milestone roadmap | UX bug; uncovered only by smoke Item 4 (unwalked). Recommend follow-up issue, not a phase blocker. |

### Required Artifacts

| Artifact | Expected | Status | Details |
|---|---|---|---|
| `src/CookBot.Application/Recipes/IngredientLinkPatterns.cs` | Single source of truth regex class | VERIFIED | 14 lines, contains `internal static class IngredientLinkPatterns` and `public static readonly Regex Pattern`. |
| `src/CookBot.Application/AssemblyAttributes.cs` | `[InternalsVisibleTo("CookBot.Web")]` for chip composer access to internal regex | VERIFIED | File exists per review file list. |
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor` | Three render branches (interactive chip flow / read-only chip flow / MudTextField fallback); JS-interop probe; consumes `IngredientLinkPatterns.Pattern` | PARTIAL | All three render branches present (lines 10-87). Probe correctly catches JSException/JSDisconnectedException/TaskCanceledException. **But** `OnSegmentInput` (line 237) is functionally broken — see WR-01. |
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/IngredientChip.razor` | Name-only display; replace popover; × remove; red error chip when unresolved | VERIFIED | 73 lines; both color branches present; `MudMenu` with Replace + Remove items; `OnClose` for × removal. |
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` | Step/Section toggle; D-A4 view-mode toggle; chip composer; persisted timer chip strip | VERIFIED | 187 lines. Step/Section `MudToggleGroup`, ephemeral `_showRawMarkdown`, raw-markdown `MudTextField Lines="3"` branch (lines 56-63), `<RecipeChipComposer Interactive="true">` chip branch, `<TimerChip>` strip (lines 76-87). |
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/TimerChip.razor` | Persisted explicit timer chip with edit popover | VERIFIED | File present in directory listing; review file-list confirms. |
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/SectionDropConfirmationDialog.razor` | TimerCount + RefCount confirmation dialog | VERIFIED | 27 lines; `[Parameter] TimerCount` and `[Parameter] RefCount` declared; renders count text. |
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/InlineTimerSuggestion.razor` | Yes/No popover for inline timer suggestion | VERIFIED | File present; uses `MudMenu` (line 9). |
| `src/CookBot.Web/Components/Pages/RecipeEditor.razor` | Slim host page; one Add Step button; warning MudAlert; AddSectionHeader + DetectIngredientRefsInStep deleted | VERIFIED | 431 lines (still > planned 250 but `RecipeStepEditor` invocation present at line 138; `MudAlert Severity="Severity.Warning"` at line 33; single `MudButton OnClick="AddStep"` at line 127; both deleted methods absent). |
| `src/CookBot.Web/Components/Pages/CookingMode.razor` | Chip rendering via `<RecipeChipComposer Interactive=false>`; sidebar with `id="ingredient-{LocalId}"`; chip-click scroll-and-highlight | PARTIAL | Chip rendering correct (lines 54, 61); ingredient ID anchors correct (line 148); scroll-and-highlight correct (line 417). **But** sidebar background highlight (line 146) reads dead `IngredientRefs`. WR-03. |
| `src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor` | Numbered-list fallback DELETED; Submit is a thin parser pass-through | VERIFIED | 52 lines; Submit is exactly `Parser.TryParse → Close`; no fallback branch present. |
| `src/CookBot.Application/Services/RecipeService.cs` | Auto-write fallback DELETED — `(ps.Timers ?? new())` is sole source | VERIFIED | Lines 74, 146 — both step projections. No `TimerDetectionService.DetectTimers(ps.Text)` calls. |
| `src/CookBot.Application/Services/TimerDetectionService.cs` | Regex broadened — fractional + range + multi-segment + simple, range as lowest | VERIFIED | All four patterns at lines 15-32; precedence at lines 46-49. |
| `src/CookBot.Application/Services/RecipeStepTextFormatter.cs` | `ToHtmlWithTimerSuggestions` overload wraps detected substrings; HTML-encodes user content (XSS-safe) | VERIFIED | Line 64 declares the overload; line 77 emits `<span class="timer-suggestion" data-duration-seconds="...">`. Review notes the sentinel two-pass HTML-encoding strategy is XSS-safe with a dedicated test. |
| `src/CookBot.Web/wwwroot/js/recipe-chip-composer.js` | `ping`, `getCaretCoords`, `scrollIntoViewWithHighlight` on `window.RecipeChipComposer` | VERIFIED | All three methods present at lines 4, 7, 14, 33. |
| `src/CookBot.Web/Components/App.razor` | `<script src="js/recipe-chip-composer.js">` registered | VERIFIED | Line 23. |
| `src/CookBot.Web/wwwroot/app.css` | `.chip-flow`, `.chip-highlight-pulse`, `.timer-suggestion` rules | VERIFIED (3/3 rules) — but missing `position: relative` on `.chip-flow` per WR-02 deferred | Lines 56, 63, 69. |
| `tests/CookBot.Tests/CookBot.Tests.csproj` | bUnit PackageReference | VERIFIED | Line 13: `bunit Version="1.40.0"`. |
| `tests/CookBot.Tests/Web/RecipeChipComposerTests.cs` | 4 [Fact] tests covering D-A1, tokenization, fallback, unresolved | VERIFIED — but does not exercise WR-01 path | All 4 tests present; pass. None dispatches a real `oninput` event; the helpers are imperative `SimulateInsertChip` calls. |
| `tests/CookBot.Tests/Web/StepSectionToggleTests.cs` | Step→Section conversion + confirmation flow + D-A4 view-mode toggle | VERIFIED | File present in review file-list; tests pass per `dotnet test` (185 total). |
| `tests/CookBot.Tests/Web/PasteFlowTests.cs` | Pass-through flow; numbered-list-fallback-deletion verified | VERIFIED | Present per review. |
| `tests/CookBot.Tests/Web/TimerSuggestionTests.cs` | bUnit: timer-suggestion span rendering + per-occurrence accept/dismiss + XSS encoding | VERIFIED | Present; review confirms XSS test. |
| `tests/CookBot.Tests/Application/TimerDetectionServiceRegexTests.cs` | Theory tests for all four patterns | VERIFIED | Present. |

### Key Link Verification

| From | To | Via | Status | Details |
|---|---|---|---|---|
| `RecipeChipComposer.razor` | `IngredientLinkPatterns.cs` | `IngredientLinkPatterns.Pattern.Matches(text)` | WIRED | Line 146. |
| `RecipeChipComposer.razor` | `recipe-chip-composer.js` | `JS.InvokeAsync<string>("RecipeChipComposer.ping")` | WIRED | Line 110, with try/catch around three exception types. |
| `App.razor` | `recipe-chip-composer.js` | `<script src="js/recipe-chip-composer.js">` | WIRED | Line 23. |
| `RecipeEditor.razor` | `RecipeStepEditor.razor` | `<RecipeStepEditor Step="..." ...>` in for-loop | WIRED | Line 138. |
| `RecipeStepEditor.razor` | `RecipeChipComposer.razor` | `<RecipeChipComposer Interactive="true" Text="@Step.Text" TextChanged="OnTextChanged" Ingredients="@Ingredients" />` | WIRED | Lines 67-70. |
| `RecipeStepEditor.razor` | `SectionDropConfirmationDialog.razor` | `DialogService.ShowAsync<SectionDropConfirmationDialog>(...)` | WIRED | Line 135. |
| `CookingMode.razor` | `RecipeChipComposer.razor` (read-only) | `<RecipeChipComposer Interactive="false" Text="@CurrentStep.Text" Ingredients="..." OnIngredientChipClick="ScrollToIngredient" />` | WIRED | Lines 61-64. |
| `CookingMode.razor` | `recipe-chip-composer.js` | `JS.InvokeAsync<bool>("RecipeChipComposer.scrollIntoViewWithHighlight", $"ingredient-{recipeLocalId}")` | WIRED | Lines 417-418, with `JSDisconnectedException` catch (Pitfall 4). |
| `PasteRawTextDialog.razor` | `IRecipeFormatParser` | `Parser.TryParse(_rawText, out var parsed, out var errors)` | WIRED | Line 44. |
| `RecipeStepTextFormatter.cs` | `TimerDetectionService` | `TimerDetectionService.CompiledTimerPattern` | WIRED | Per `grep` — `ToHtmlWithTimerSuggestions` consumes the pattern. |
| `RecipeService.cs` | `TimerDetectionService` (NEGATIVE link — should NOT exist on save path) | `(?!TimerDetectionService.DetectTimers(ps.Text))` | VERIFIED ABSENT | grep returns no hits in RecipeService. |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|---|---|---|---|---|
| `RecipeChipComposer` (interactive) | `Text` (parameter, two-way bound to `Step.Text`) | `RecipeStepEditor.OnTextChanged` writes; chip composer reads | YES (chip insertion path); NO (free-text typing path — WR-01) | HOLLOW — wired for chip-insertion data flow but the contenteditable `oninput` event source produces empty strings for every keystroke, wiping segment text. |
| `RecipeChipComposer` (read-only, cooking mode) | `Text` (from `CurrentStep.Text` in CookingMode) | DB-backed `RecipeStep.Text` projected through `_recipe.Steps` | YES | FLOWING. |
| `IngredientChip` | `DisplayName` + `IngredientId` + `IsResolved` | Tokenizer in `RecipeChipComposer.TokenizeText` matching `IngredientLinkPatterns.Pattern` against `Text` | YES | FLOWING. |
| `RecipeStepEditor` timer chip strip | `Step.Timers` | `ParsedStep.Timers` from EF projection or new step | YES | FLOWING. |
| `CookingMode` ingredient sidebar `isReferenced` | `CurrentStep.IngredientRefs` | Phase 1 D-13 retired writes to this list — every freshly-saved recipe has empty `IngredientRefs` | NO (always empty post-Phase-1) | DISCONNECTED. WR-03 — should read `IngredientLinkPatterns.Pattern.Matches(CurrentStep.Text)` instead. |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|---|---|---|---|
| Solution builds clean | `dotnet build FreelovesCookBot.sln` | 0 warnings, 0 errors | PASS |
| Test suite passes | `dotnet test --filter "Category!=RequiresApiKey" --no-build` | 185/185 passed | PASS |
| Canonical regex consolidation (consumer count) | `grep -rn "IngredientLinkPatterns.Pattern" src/` | 8 hits across formatter, validator, chip composer, step editor — all expected consumers wired | PASS |
| Auto-write deletion | `grep -n "TimerDetectionService.DetectTimers" src/CookBot.Application/Services/RecipeService.cs` | (no hits) | PASS |
| Numbered-list fallback deletion | `wc -l src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor` and grep for fallback branch | 52 lines; Submit is `Parser.TryParse → Close` only | PASS |
| AddSectionHeader / DetectIngredientRefsInStep deletion | `grep -n "AddSectionHeader\|DetectIngredientRefsInStep" src/CookBot.Web/Components/Pages/RecipeEditor.razor` | (no hits) | PASS |
| WR-01: contenteditable `e.Value` source verification | `grep -n "OnSegmentInput" RecipeChipComposer.razor` + read body | Line 237: `var newSegment = e.Value?.ToString() ?? string.Empty;` reads ChangeEventArgs.Value which is null for contenteditable @oninput | FAIL — bug confirmed by source inspection. |
| WR-03: dead read of IngredientRefs | `grep -n "IngredientRefs" CookingMode.razor` + cross-check writes in RecipeService | Line 146 reads `CurrentStep.IngredientRefs.Contains(...)`; no writes anywhere in RecipeService | FAIL — bug confirmed. |
| IN-01: duplicate regex check | `grep -rn "MarkdownLinkPattern\|@\"\\\\\\[" src/CookBot.Application` | `IngredientRefDetectionService.cs:8-10` — duplicate `MarkdownLinkPattern` with `+` instead of `*` | FAIL — duplicate regex still exists. |

### Requirements Coverage

| Requirement | Source Plan | Description (REQUIREMENTS.md) | Status | Evidence |
|---|---|---|---|---|
| EDITOR-01 | 03-01, 03-02, 03-04 | Chip-aware composer; @-trigger / Insert ingredient affordance; chips render the ingredient name; `[name](#id)` invisible | PARTIAL | Composer exists; `@`-trigger and Insert button paths produce identical strings; chips render names. **But** WR-01 means the user cannot type free text in the chip composer between/after chips — every keystroke wipes the segment. The phrase "chip-aware composer" is broken for the typing dimension of authoring. EDITOR-01 amendment landed in REQUIREMENTS.md per D-A5. |
| EDITOR-02 | 03-02 | Step/Section toggle; section steps disable timer/ingredient-chip controls | SATISFIED | `MudToggleGroup<StepKind>` in `RecipeStepEditor.razor:24-32`; `_kind == StepKind.Step` gates the timer chip strip and the D-A4 view-mode toggle (lines 35, 73). Section heading uses `MudTextField` only (lines 47-50). Confirmation dialog wired via D-B3. |
| EDITOR-03 | 03-03, 03-04 | Detected timers surface as Yes/No suggestion; auto-write of timers from regex on save is removed | SATISFIED | `InlineTimerSuggestion.razor` opens Yes/No popover; `RecipeStepTextFormatter.ToHtmlWithTimerSuggestions` wraps detected substrings; `RecipeService.CreateAsync`/`UpdateAsync` no longer call `TimerDetectionService.DetectTimers` on the save path. |
| EDITOR-04 | 03-01, 03-02 | Reorder preserves immutable `id`; chip rendering uses `[name](#id)` link | SATISFIED | Chip rendering reads `id` via `IngredientLinkPatterns.Pattern.Matches(...).Groups[2]` (RecipeChipComposer.razor:150); reorder uses `MoveStepUp`/`MoveStepDown` only on step list, not ingredient list, and `RecipeIngredient.RecipeLocalId` is the immutable id from Phase 1 D-06. |
| EDITOR-05 | 03-03 | Paste raw text routes through new schema stack; never persists non-conforming | SATISFIED | `PasteRawTextDialog.Submit` is a thin `Parser.TryParse → Close`; failure path keeps the dialog open with `_errors` displayed. No persistence path bypasses the parser. |
| EDITOR-06 | 03-03 | Cooking-mode chip rendering for ingredient highlighting; link-resolution exclusively | PARTIAL | Step-text chip rendering uses `<RecipeChipComposer Interactive="false">` with link-resolution exclusively (no substring matching). **But** sidebar background highlight (CookingMode.razor:146) reads dead `IngredientRefs` — fails to fire on freshly-saved recipes. |
| EDITOR-07 | 03-01, 03-04 | Keyboard nav (Tab/Shift+Tab; Backspace deletes chip; Arrows); axe-core/screen-reader smoke pass; JS-interop graceful degradation | NEEDS HUMAN + PARTIAL | JS-interop graceful degradation: VERIFIED (D-D4 fallback test passes; `JSDisconnectedException` try/catch in CookingMode and RecipeChipComposer). Backspace-deletes-chip: FAILED — `OnSegmentKeyDown` is a no-op placeholder (IN-03). Tab nav, screen reader, color contrast, IME: NEEDS HUMAN per smoke checklist (auto-approved without walking). |

**Orphan check:** `grep "Phase 3" .planning/REQUIREMENTS.md` returns 7 EDITOR-IDs (lines 165-171). All 7 are claimed by phase plans (EDITOR-01..07 distributed across 03-01, 03-02, 03-03, 03-04 frontmatter). **No orphaned requirements.**

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|---|---|---|---|---|
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor` | 230-235 | No-op handler with comment `placeholder here` for `OnSegmentKeyDown` | Warning | EDITOR-07 keyboard semantic ("Backspace at position 0 removes prior chip") is wired to a placeholder; users see no behavior on Backspace. IN-03. |
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor` | 237-242 | `e.Value?.ToString() ?? string.Empty` — reads null on contenteditable | Blocker | Every keystroke in a chip composer text segment replaces the segment with the empty string. Chip-composer typing flow is functionally broken. WR-01. |
| `src/CookBot.Web/Components/Pages/CookingMode.razor` | 146 | Reads `CurrentStep.IngredientRefs` which is permanently empty post-Phase-1 D-13 | Warning | Cooking-mode sidebar highlight never fires on freshly-saved recipes. WR-03. |
| `src/CookBot.Application/Services/IngredientRefDetectionService.cs` | 8-10 | Duplicate of `IngredientLinkPatterns.Pattern` (with `+` vs `*`) | Info | Undermines "single source of truth" claim. Class is dead code; deletion candidate. IN-01. |
| `src/CookBot.Web/Components/Pages/RecipeEditor.razor` | 285 | `_warnings.Clear();` with no populate path | Info | Banner block is dead until ParsedRecipe carries warnings. IN-04. |

### Behavioral Spot-Checks Summary

Build green, 185/185 tests pass, regex consolidation 8 hits across expected consumers, auto-write deletion verified, paste fallback deletion verified. The bUnit test suite **does not** exercise the `@oninput` ChangeEventArgs path, which is precisely where WR-01 lives — the regression is invisible to CI.

### Human Verification Required

#### 1. Walk the 9-item manual a11y smoke checklist

**Test:** Open `./run.sh`, navigate to a fresh recipe in `http://localhost:7000`, and execute every item in `03-VERIFICATION.md` Items 1-9 (Tab/Shift+Tab nav; Backspace/Arrow chip semantics; screen reader chip announcement; @-trigger autocomplete keyboard-only; Step/Section radiogroup; inline timer-suggestion popover; JS-interop-fail fallback via DevTools Request Blocking; color contrast on chip variants; IME composition; cooking-mode chip click → scroll-and-highlight).

**Expected:** Each item's expected behavior fires; sign-off appended to `03-VERIFICATION.md` with developer name, date, and any deviations.

**Why human:** No axe-core / Playwright / Lighthouse infra in this codebase. Browser-only behavior (focus order, screen reader announcements, IME composition, color contrast in dark mode, JS-interop blocking) cannot be programmatically verified.

#### 2. End-to-end recipe authoring after WR-01 fix

**Test:** With WR-01 patched, open a fresh recipe, type `Bake the ` into a chip composer text segment, click `+` to insert a chip for `Salt`, type ` for 5 minutes`. Save and reload.

**Expected:** Step text reads `Bake the [Salt](#1) for 5 minutes`; cooking mode renders `Bake the {Salt-chip} for 5 minutes` with chip rendering parity.

**Why human:** Confirms the WR-01 fix actually works in a real Blazor Server circuit. bUnit tests use imperative simulators that bypass the `@oninput` → `ChangeEventArgs` path; only a real circuit reproduces the contenteditable behavior.

#### 3. Cooking-mode sidebar highlight on freshly-saved recipe (after WR-03 fix)

**Test:** With WR-03 patched, save a brand-new recipe with chips referencing 3 ingredients. Open cooking mode, navigate to the step that uses those chips.

**Expected:** The 3 referenced ingredients in the sidebar receive the `primary-lighten` background; non-referenced ones do not.

**Why human:** Per-render visual highlight is a CSS property; the recipe must be freshly saved through Phase 1's RecipeService to expose the empty-IngredientRefs path that WR-03 fixes.

### Gaps Summary

The phase achieved 4 of 9 goal-truths fully and 2 partially. Two architectural successes are unambiguous: (a) the chip-insertion paths (`@`-trigger + button) are unified through `SimulateInsertChip` and produce identical `[name](#id)` strings; (b) the auto-write fallback in `RecipeService` is gone and the timer regex is broadened to fractional/range/multi-segment.

Two **goal-blocking** gaps remain:

**Gap 1 (WR-01) — Chip-composer typing is broken.** The `@oninput` handler on contenteditable spans reads `ChangeEventArgs.Value` which is null for non-input elements; every keystroke wipes the segment. The entire chip-composer free-text typing flow (the centerpiece of the phase goal) does not work in a real browser circuit. The bUnit tests use imperative `SimulateInsertChip` calls and miss this regression. Currently the only way to author free text is the D-A4 raw-markdown toggle or the JS-interop-fail fallback `MudTextField` — both of which were designed as escape hatches, not workaround paths for a broken composer.

**Gap 2 (EDITOR-07 not walked) — Manual smoke checklist auto-approved.** The 9-item a11y/browser-degradation checklist in `03-VERIFICATION.md` was auto-approved under `workflow.auto_advance=true`, but the auto-approval log explicitly notes "the 9 smoke items above were NOT manually walked." SC#5 (axe-core/screen-reader smoke pass) and EDITOR-07 cannot be claimed without that walk.

Three additional gaps are real but lower-impact:

**Gap 3 (WR-03)** — Cooking-mode sidebar highlight reads dead `IngredientRefs`; sidebar highlight never fires on freshly-saved recipes. Cosmetic but regresses an existing UX feature in a file Phase 3 edited.

**Gap 4 (IN-01)** — Duplicate `[name](#id)` regex remains in `IngredientRefDetectionService.cs` (with a different regex shape — `+` vs `*`). Plan 01's stated truth was "single source of truth"; this is the one consolidation site that was missed.

**Gap 5 (IN-03)** — `OnSegmentKeyDown` is a no-op placeholder; EDITOR-07's "Backspace at position 0 deletes the prior chip" semantic doesn't work.

**Recommendation:** Open a `gsd-plan-phase --gaps` round to address WR-01 (the goal-blocker), WR-03, IN-01, IN-03, and pair it with a manual smoke walkthrough so the EDITOR-07 gate can be sign-off. WR-02 and WR-04 are reasonable follow-up issues outside the phase-closure path.

---

_Verified: 2026-04-26T23:08:42Z_
_Verifier: Claude (gsd-verifier, goal-backward verification)_
_Sibling file `03-VERIFICATION.md` — manual a11y smoke checklist (Plan 04 Task 3 deliverable, auto-approved without manual walkthrough) — preserved untouched._
