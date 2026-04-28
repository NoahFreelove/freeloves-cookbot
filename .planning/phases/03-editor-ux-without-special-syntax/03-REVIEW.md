---
phase: 03-editor-ux-without-special-syntax
reviewed: 2026-04-26T00:00:00Z
depth: standard
files_reviewed: 25
files_reviewed_list:
  - src/CookBot.Application/AssemblyAttributes.cs
  - src/CookBot.Application/Recipes/IngredientLinkPatterns.cs
  - src/CookBot.Application/Recipes/RecipeValidator.cs
  - src/CookBot.Application/Services/RecipeService.cs
  - src/CookBot.Application/Services/RecipeStepTextFormatter.cs
  - src/CookBot.Application/Services/TimerDetectionService.cs
  - src/CookBot.Web/AssemblyAttributes.cs
  - src/CookBot.Web/Components/App.razor
  - src/CookBot.Web/Components/Pages/CookingMode.razor
  - src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor
  - src/CookBot.Web/Components/Pages/RecipeEditor.razor
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/IngredientChip.razor
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/InlineTimerSuggestion.razor
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/SectionDropConfirmationDialog.razor
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/TimerChip.razor
  - src/CookBot.Web/wwwroot/app.css
  - src/CookBot.Web/wwwroot/js/recipe-chip-composer.js
  - tests/CookBot.Tests/Application/TimerDetectionServiceRegexTests.cs
  - tests/CookBot.Tests/CookBot.Tests.csproj
  - tests/CookBot.Tests/Web/PasteFlowTests.cs
  - tests/CookBot.Tests/Web/RecipeChipComposerTests.cs
  - tests/CookBot.Tests/Web/StepSectionToggleTests.cs
  - tests/CookBot.Tests/Web/TimerSuggestionTests.cs
findings:
  critical: 0
  warning: 5
  info: 6
  total: 11
status: issues_found
---

# Phase 3: Code Review Report

**Reviewed:** 2026-04-26T00:00:00Z
**Depth:** standard
**Files Reviewed:** 25
**Status:** issues_found

## Summary

Phase 3 introduces a chip-aware composer (`RecipeChipComposer`), per-step editor (`RecipeStepEditor`), inline timer-suggestion popover (`InlineTimerSuggestion`), explicit timer chip (`TimerChip`), JS interop module (`recipe-chip-composer.js`), and a broadened `TimerDetectionService` with fractional / range / multi-segment patterns. The `auto-write of timers` fallback in `RecipeService` is correctly removed; the consolidated `IngredientLinkPatterns` regex is correctly the single source of truth in the *new* code paths; the `InternalsVisibleTo` bridges are tightly scoped; the new `ToHtmlWithTimerSuggestions` HTML-encodes user content via the sentinel two-pass strategy and is well-tested for XSS (`TimerSuggestionTests.ToHtmlWithTimerSuggestions_HtmlEncodesScriptInjection_T03P03_01`).

The XSS, authorization, JS-interop input validation, and forbidden-package checks all come back clean. No `Console.WriteLine` / `console.log` debug artifacts, no `.Result` / `.Wait()` antipatterns, no second AI provider abstraction.

Five warnings were found, most centered on the chip-composer's contenteditable interaction surface (segment typing is silently dropped, picker positioning uses wrong coordinate space) and a long-standing `IngredientRefs` read in cooking mode that became dead after the Phase 1 D-13 retirement of writes — Phase 3's edits to that file pull it into review scope. Six informational items track regex robustness, dead public surface, and a stale duplicate regex that the consolidation effort missed.

## Warnings

### WR-01: Contenteditable segment edits are silently discarded

**File:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor:237-242`
**Issue:** `OnSegmentInput` reads `e.Value?.ToString() ?? string.Empty` from a `ChangeEventArgs` raised by an `@oninput` event on a `<span contenteditable="plaintext-only">`. Blazor's `oninput`-on-contenteditable does **not** populate `ChangeEventArgs.Value` with the new text — that field is `null` for all non-input elements. The handler therefore replaces the segment with the empty string on every keystroke: typing into a segment between two chips wipes the segment instead of editing it. Because the bUnit tests (`RecipeChipComposerTests`) only exercise the imperative `SimulateAtTriggerSelectionAsync` / `SimulateButtonInsertionAsync` helpers and never dispatch a real `oninput` event on a segment, this regression is invisible to CI. End-to-end manual typing in the chip view is broken; the fallback `MudTextField` (`_jsInteropAvailable=false`) is the only path where users can edit step text right now.
**Fix:** Either (a) wire JS-interop to read `.textContent` of the segment span on input and pass it to a `[JSInvokable]` callback, or (b) bind the chip-composer to a hidden `<input>` whose value mirrors the contenteditable surface, or (c) drop the `@oninput` handler on segments and capture edits via a `blur` JS-interop bridge that emits the full reconstructed text. Add a bUnit test that fires `oninput` with `e.Value="new text"` AND a manual `cut.Find("span[contenteditable]").Input("new text")` to lock the regression.

### WR-02: Picker popover uses wrong coordinate origin (positioning bug)

**File:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor:42` and `src/CookBot.Web/wwwroot/js/recipe-chip-composer.js:14-29`
**Issue:** `getCaretCoords` returns coordinates **relative to the element's top-left corner** (`cr.left - rect.left`, `cr.bottom - rect.top`). The Razor side then renders the picker with `position:absolute;left:{x}px;top:{y}px`. `position:absolute` resolves against the nearest **positioned** ancestor — `.chip-flow` has no `position` declared in `app.css`, so positioning resolves up to the page root (or whatever `MudBlazor`'s nearest positioned ancestor happens to be). The picker will appear in the wrong location whenever `.chip-flow` is offset from that ancestor, which is essentially every render. This is a usability bug for the D-A1 caret-anchored picker — users see the picker fly off to the page corner instead of opening at their caret.
**Fix:** Either (a) add `.chip-flow { position: relative; }` to `app.css` so the absolute positioning resolves against the chip-flow box (matching the JS coordinate space), or (b) change the JS to return viewport coordinates and use `position:fixed` on the picker container. Option (a) is smaller and matches the JS contract.

### WR-03: `CurrentStep.IngredientRefs` read in cooking mode never matches anything

**File:** `src/CookBot.Web/Components/Pages/CookingMode.razor:146`
**Issue:** `var isReferenced = CurrentStep.IngredientRefs.Contains(ri.RecipeLocalId);` drives the highlight styling on the cooking-mode ingredient sidebar. `RecipeService.CreateAsync` / `UpdateAsync` no longer write to `RecipeStep.IngredientRefs` (removed in Phase 1 D-13, comments at `RecipeService.cs:81-83` and `:150-151`). Every recipe saved after the milestone has an empty `IngredientRefs` list, so `isReferenced` is permanently `false` and the sidebar highlight never fires. Pre-existing recipes still work until they're re-saved. The file was edited this phase (chip-composer integration, `id="ingredient-{LocalId}"` anchor for scroll-to-ingredient), so the bug is now in Phase 3's review surface.
**Fix:** Replace the `IngredientRefs.Contains` read with a fresh resolution against `[name](#id)` markdown in `CurrentStep.Text`, matching the new chip-composer contract. Suggested:
```csharp
private HashSet<int> CurrentStepRefIds()
    => IngredientLinkPatterns.Pattern.Matches(CurrentStep.Text ?? "")
        .Select(m => int.TryParse(m.Groups[2].Value, out var id) ? id : -1)
        .Where(id => id > 0)
        .ToHashSet();
```
Then `var isReferenced = CurrentStepRefIds().Contains(ri.RecipeLocalId);` — cache per render to avoid recomputing inside the foreach. (The `CookingMode` razor sits in `CookBot.Web` which has the `InternalsVisibleTo` from `CookBot.Application`, so `IngredientLinkPatterns` is reachable.)

### WR-04: `int.Parse` in fractional pattern can throw `OverflowException` on malicious input

**File:** `src/CookBot.Application/Services/TimerDetectionService.cs:111-113`
**Issue:** `ParseFractionalToSeconds` calls `int.Parse(m.Groups[1].Value, ...)`, `int.Parse(m.Groups[2].Value, ...)`, `int.Parse(m.Groups[3].Value, ...)`. The regex `\d+` matches arbitrarily long digit sequences; a step text like `"Bake for 99999999999999999 1/2 hours"` will throw `OverflowException` inside the formatter, propagating up through `ToHtmlWithTimerSuggestions` to the Razor render path. The other parsers (`ParseMultiSegmentToSeconds`, `ParseRangeToSeconds`, `ParseSimpleToSeconds`) use `double.Parse` which returns `Infinity` rather than throwing on overflow (still wrong, but non-throwing and clamped by the `(int)(n * 60)` cast which overflows silently to a huge value or `int.MinValue`). The threat model is "the recipe-step text is fully attacker-controlled by the recipe author" — for a multi-user trusted-LAN deployment this is a DOS / availability concern, not a security one, but it crashes the editor render.
**Fix:** Switch to `int.TryParse` and bail out (`return 0;`) on overflow:
```csharp
if (!int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var whole) && m.Groups[1].Success) return 0;
if (!int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var num)) return 0;
if (!int.TryParse(m.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var den)) return 0;
```
Also consider a sanity ceiling (e.g. a duration of more than 100 hours is almost certainly garbage) for all four parsers — clamp `seconds` to a reasonable upper bound before returning.

### WR-05: `MudMenu @bind-Open` on a conditionally-rendered element will glitch on close

**File:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/IngredientChip.razor:27-34`
**Issue:** The `MudMenu` is wrapped in `@if (Interactive && _menuOpen)`. When `_menuOpen` flips to `false` (via `MudMenu` raising `OpenChanged(false)` on outside-click or item selection), the entire `MudMenu` element is removed from the render tree the next render — but `MudMenu`'s own DOM-cleanup / popover-disconnect is racing with that removal. In practice this works because the `OnClick` handler on `MudMenuItem` already sets `_menuOpen=false` synchronously before the menu unmounts, but a click outside the menu (which raises `OpenChanged(false)` via `@bind-Open`) sets `_menuOpen=false` from inside MudMenu, triggers a re-render that drops the element, and the `IPopoverService.DestroyPopoverAsync` lifecycle may be invoked on an already-removed component. This is the classic anti-pattern noted in MudBlazor's own docs: `@if`-wrap a `MudMenu` that uses `@bind-Open`, you get pop-back jitter and orphaned popovers in the popover provider.
**Fix:** Drop the `@if` guard and let `MudMenu`'s own `Open` parameter do the work:
```razor
<MudMenu @bind-Open="_menuOpen"
         Disabled="@(!Interactive)"
         AnchorOrigin="Origin.BottomLeft" TransformOrigin="Origin.TopLeft">
    <MudMenuItem ... />
</MudMenu>
```
Or use `MudMenu`'s `ActivatorContent` so the chip itself is the activator — that's the v8.15 idiom for chip→menu wiring.

## Info

### IN-01: `IngredientRefDetectionService` defines a duplicate of the consolidated regex

**File:** `src/CookBot.Application/Services/IngredientRefDetectionService.cs:8-10`
**Issue:** Phase 3 created `IngredientLinkPatterns.Pattern = @"\[([^\]]*)\]\(#(\d+)\)"` as the single source of truth for `[name](#id)` matching, but `IngredientRefDetectionService` still carries its own slightly-different copy: `@"\[([^\]]+)\]\(#(\d+)\)"` (note the `+` vs `*` — this version rejects empty display names, the new one accepts them). The class is a no-op at this point (`DetectRefs` returns refs computed from markdown only, and `RecipeService` no longer calls it for `IngredientRefs` writes), but the duplicate regex undermines the "do not redefine elsewhere" doc comment in `IngredientLinkPatterns.cs`.
**Fix:** Delete `IngredientRefDetectionService` if it has no remaining callers (a quick `grep -rn "IngredientRefDetectionService" src tests` confirms no production callers — only `Migrations` mentions on the `IngredientRefs` column). If callers exist, swap its `MarkdownLinkPattern` for `IngredientLinkPatterns.Pattern`.

### IN-02: `TimerDetectionService.DetectTimers` is now dead public API

**File:** `src/CookBot.Application/Services/TimerDetectionService.cs:54-64`
**Issue:** The Phase-3 plan correctly removed the only call site (`RecipeService.CreateAsync` / `UpdateAsync` ternary fallback). A grep confirms no other production callers; only a single unit test (`TimerDetectionServiceRegexTests.Simple_BackwardCompatible`) still exercises this method to assert backward-compat unit splitting. Keeping a public method exclusively for one test introduces a temptation to re-introduce the auto-write fallback later. The method also carries the only call site of `SplitToDurationAndUnit`, which becomes dead alongside it.
**Fix:** Either (a) port the relevant assertions from `Simple_BackwardCompatible` over to the `Detect(...)` API (asserting on `TotalSeconds`, like every other test does) and delete `DetectTimers` + `SplitToDurationAndUnit`, or (b) mark `DetectTimers` `[Obsolete]` with a message pointing at `Detect`.

### IN-03: `RecipeChipComposer.OnSegmentKeyDown` is a no-op placeholder

**File:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor:230-235`
**Issue:** Wired as `@onkeydown` on every text segment but the body is `return Task.CompletedTask;` with a comment "Detection via JS-side caret position is a follow-up refinement; placeholder here." This is intentional incomplete scope per the comment, but the EDITOR-07 invariant ("Backspace at position 0 immediately after a chip removes the chip") is *advertised* to users via the chip composer UX without actually working.
**Fix:** Either delete the `@onkeydown` wiring (avoid the perception that the feature works) or land the JS-interop bridge to detect caret-at-position-0 and remove the prior chip. If kept as-is, add a deferred-work TODO that links to the follow-up phase / issue.

### IN-04: `RecipeEditor.PopulateFromParsed` clears warnings without ever populating them

**File:** `src/CookBot.Web/Components/Pages/RecipeEditor.razor:285`
**Issue:** `_warnings.Clear();` is the only mutation of `_warnings` in `PopulateFromParsed`; the comment block above it explicitly says "ParsedRecipe doesn't carry warnings yet" and "_warnings is the receive surface — Plan 03 Task 2 ... refines this further." The banner at `:31-40` will therefore never light up until `ParsedRecipe` grows a `Warnings` field. This is a known incomplete scope (D-D1/D-D2 forward-extension hook), not a bug, but the dead `Clear()` is misleading.
**Fix:** Either delete the `Clear()` and the `_warnings` field/banner block until `ParsedRecipe.Warnings` lands, or pipe through warnings from `RecipeValidator.Validate(canonical-projection-of-parsed)` so the banner has something to show.

### IN-05: `_pickerVisible` has no dismiss path other than ingredient selection

**File:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor:40-53,206-228`
**Issue:** When the user clicks the "+" `MudIconButton` the picker opens; if they then click outside the `MudAutocomplete` without selecting anything, `_pickerVisible` stays `true` and the picker remains on screen until they make a selection or navigate away. There's no escape-key handler, no outside-click handler, no clear/cancel button.
**Fix:** Either wrap the picker in a `MudPopover` with `OverflowBehavior.FlipNever` and an `OutsideClick` handler that flips `_pickerVisible=false`, or add a tiny "×" button to the absolute-positioned picker div.

### IN-06: `MudChip` `OnClose` lambda discard parameter style is inconsistent

**File:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/IngredientChip.razor:11,21` and `TimerChip.razor:8`
**Issue:** `IngredientChip` uses `OnClose="@HandleClose"` with a method that takes `MudChip<string> chip` and discards it. `TimerChip` uses `OnClose="@(_ => OnRemove.InvokeAsync())"` with the discard pattern. Either is fine; pick one and apply it consistently in the chip parts.
**Fix:** Standardize on `OnClose="@(_ => HandleRemove())"` everywhere — same as `TimerChip`. Removes the `MudChip<string> chip` reference in `IngredientChip.HandleClose` which is unused anyway.

---

_Reviewed: 2026-04-26T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
