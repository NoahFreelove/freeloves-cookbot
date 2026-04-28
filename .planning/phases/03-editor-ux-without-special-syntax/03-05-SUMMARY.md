---
phase: 03-editor-ux-without-special-syntax
plan: 05
subsystem: editor-ux
tags: [blazor, contenteditable, jsinterop, dotnetobjectreference, chip-composer, gap-closure, bunit]
requires:
  - Phase 3 Plan 01 (recipe-chip-composer.js module + RecipeChipComposer foundation)
  - Phase 3 Plan 02 (RecipeStepEditor wiring + editor integration)
provides:
  - window.RecipeChipComposer.bindSegmentEvents / unbindSegmentEvents JS bridge
  - RecipeChipComposer JSInvokable OnSegmentInputFromJs (WR-01 fix)
  - RecipeChipComposer JSInvokable OnSegmentKeyDownFromJs (IN-03 / EDITOR-07 fix)
  - IAsyncDisposable lifecycle for DotNetObjectReference cleanup
  - 6 bUnit regression tests pinned to the new JSInvokable surface
affects:
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor
  - src/CookBot.Web/wwwroot/js/recipe-chip-composer.js
  - tests/CookBot.Tests/Web/RecipeChipComposerTests.cs
tech-stack:
  added: []
  patterns:
    - DotNetObjectReference + JSInvokable per-component callback pattern
    - Per-render _renderTokens snapshot for segmentIndex → (Start, Length, IsChip) lookup
    - IAsyncDisposable unbind loop for JS event listener cleanup
    - Idempotent JS rebind on re-render (unbindSegmentEvents before bindSegmentEvents)
key-files:
  created: []
  modified:
    - src/CookBot.Web/wwwroot/js/recipe-chip-composer.js
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor
    - tests/CookBot.Tests/Web/RecipeChipComposerTests.cs
decisions:
  - JS bridge uses DotNetObjectReference.invokeMethodAsync (not ElementReference) because
    contenteditable @oninput is structurally broken in Blazor Server — e.Value is always null
    on contenteditable elements. The fix reads el.textContent from JS and calls back via
    JSInvokable. This is not a Blazor bug to be worked around with a hacky timeout; it is
    a fundamental limitation of Blazor's DOM diffing on contenteditable.
  - _renderTokens is cleared at the start of each render pass and populated inline in the
    foreach loop. This gives OnSegmentInputFromJs / OnSegmentKeyDownFromJs a stable snapshot
    of the token positions from the most-recent render, indexed by the same JS-side segmentIndex.
  - OnAfterRenderAsync now returns early on firstRender after StateHasChanged so that the
    segment-bind loop only runs on the *second* render (after _jsInteropAvailable is set and
    the chip-flow div is present in the DOM).
  - bindSegmentEvents is idempotent: if called twice on the same element it calls
    unbindSegmentEvents first, avoiding duplicate listener accumulation.
  - @using Microsoft.AspNetCore.Components.Web dropped from RecipeChipComposer.razor because
    KeyboardEventArgs is no longer referenced after the Blazor @onkeydown handler was removed.
metrics:
  duration: ~20 minutes
  completed: 2026-04-27
---

# Phase 3 Plan 05: JS-Interop Bridge + WR-01/IN-03 Gap Closure Summary

**One-liner:** Fixed contenteditable typing (WR-01) and Backspace-removes-prior-chip (IN-03 / EDITOR-07) by replacing Blazor's broken `@oninput`/`@onkeydown` handlers with a `DotNetObjectReference` + `bindSegmentEvents` JS bridge that reads `.textContent` and caret offset, and calling back via two new `[JSInvokable]` methods; locked both behaviors with 6 bUnit regression tests that exercise the JSInvokable surface directly.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add JS-interop bridge for contenteditable segment events | `ed24f3c` | recipe-chip-composer.js |
| 2 | Replace OnSegmentInput with JSInvokable callback (WR-01 fix) | `46e4306` | RecipeChipComposer.razor |
| 3 | Implement Backspace-removes-prior-chip via OnSegmentKeyDownFromJs (IN-03 fix) | `6571a76` | RecipeChipComposer.razor |
| 4 | Add bUnit regression tests for WR-01 typing and IN-03 Backspace | `5b700ac` | RecipeChipComposerTests.cs |

## JS-Interop Bridge Architecture

The root cause of both gaps is that Blazor Server's `@oninput` binding on `contenteditable` elements does not populate `ChangeEventArgs.Value` — it is always `null`. This is a structural limitation, not a fixable quirk.

**The bridge:**
1. `OnAfterRenderAsync` (after first render) creates a `DotNetObjectReference<RecipeChipComposer>` and calls `JS.InvokeVoidAsync("RecipeChipComposer.bindSegmentEvents", elementId, dotNetRef, segmentIndex)` for each non-chip segment span in `_renderTokens`.
2. `bindSegmentEvents` in `recipe-chip-composer.js` attaches two listeners:
   - `input` → `dotNetRef.invokeMethodAsync("OnSegmentInputFromJs", segmentIndex, el.textContent)` — reads the real DOM text content.
   - `keydown` → `dotNetRef.invokeMethodAsync("OnSegmentKeyDownFromJs", segmentIndex, event.key, caretOffset)` — reads caret offset from `window.getSelection()`. Returns `true` to signal `event.preventDefault()`.
3. `[JSInvokable] OnSegmentInputFromJs` looks up the token at `segmentIndex` in `_renderTokens`, substitutes the new text into `Text`, and calls `OnTextChanged`.
4. `[JSInvokable] OnSegmentKeyDownFromJs` implements EDITOR-07: Backspace at `caretOffset=0` when `_renderTokens[segmentIndex - 1].IsChip` calls `RemoveChip` and returns `true`. All other keys return `false`.

**D-D4 fail-soft preserved:** The `_jsInteropAvailable` guard from Plan 01 is unchanged. If `ping` fails, `bindSegmentEvents` is never called, and the MudTextField fallback renders `[name](#id)` raw text. Save still works.

## Key Changes to RecipeChipComposer.razor

**Lines modified / added:**

- Line 8: `@implements IAsyncDisposable` added.
- Lines 13, 32: `_renderTokens.Clear()` added before foreach; `_renderTokens.Add(...)` added after each token.
- Lines 28-30: Segment `<span>` — removed `@onkeydown` and `@oninput` Blazor handlers; added `id="@($"{_composerId}-seg-{idx}")"` for JS targeting.
- Lines 102-105: Three new fields — `_dotNetRef`, `_boundSegmentElementIndexes`, `_renderTokens`.
- Lines 109-168: `OnAfterRenderAsync` expanded from a first-render-only probe to a full bind/rebind lifecycle; `DisposeAsync` added.
- Lines 281-290: `[JSInvokable] OnSegmentInputFromJs` replaces the broken `OnSegmentInput`.
- Lines 292-315: `[JSInvokable] OnSegmentKeyDownFromJs` — new; implements EDITOR-07.
- Removed: `@using Microsoft.AspNetCore.Components.Web` (KeyboardEventArgs no longer referenced).
- Removed: `private Task OnSegmentKeyDown(KeyboardEventArgs e, ...)` no-op stub.
- Removed: `private Task OnSegmentInput(ChangeEventArgs e, ...)` broken handler.

## Test Counts Before/After

| State | Count |
|-------|-------|
| Before (Plan 02 baseline) | 185 passing |
| After (this plan) | 191 passing |
| New tests added | 6 |

**6 new test names:**
1. `ContenteditableInput_UpdatesText_WR01Regression` — leading segment updated via OnSegmentInputFromJs
2. `ContenteditableInputOnTrailingSegment_UpdatesText_WR01Regression` — trailing segment updated
3. `BackspaceAtOffsetZero_RemovesPriorChip_IN03Regression` — prior chip removed, returns true
4. `BackspaceAtOffsetFive_DoesNothing_IN03Regression` — non-zero offset, returns false
5. `BackspaceWhenPriorIsNotChip_DoesNothing_IN03Regression` — no prior chip, returns false
6. `ArrowLeftAtOffsetZero_DoesNotRemoveChip_IN03Regression` — non-Backspace key, returns false

All 6 call `OnSegmentInputFromJs` / `OnSegmentKeyDownFromJs` directly — the same surface the JS bridge calls at runtime. None use `SimulateAtTriggerSelectionAsync` or `SimulateButtonInsertionAsync`.

## Verification Gates — All Passed

1. **Build:** `dotnet build FreelovesCookBot.sln` — 0 warnings, 0 errors.
2. **Test suite:** `dotnet test --filter "Category!=RequiresApiKey" --no-build` — 191/191 passing (185 baseline + 6 new), 0 failing.
3. **WR-01 grep gate:** `! grep -qE 'e\.Value\?\.ToString\(\) \?\? string\.Empty' RecipeChipComposer.razor` — PASSED.
4. **IN-03 grep gate:** `! grep -q 'placeholder here' RecipeChipComposer.razor` — PASSED.
5. **JSInvokable surface present:** `grep -c '\[JSInvokable\]' RecipeChipComposer.razor` returns 2.
6. **JS bridge present:** `grep -q 'bindSegmentEvents' recipe-chip-composer.js` — PASSED.
7. **Regression tests pinned:** `grep -c 'WR01Regression\|IN03Regression' RecipeChipComposerTests.cs` returns 6.

## Deviations from Plan

None — plan executed exactly as written. The `@using Microsoft.AspNetCore.Components.Web` removal was a natural consequence of deleting the `KeyboardEventArgs`-typed `OnSegmentKeyDown` method; this is an expected cleanup, not a deviation.

## Known Stubs

None. Both fixes are fully wired end-to-end. The JS bridge calls the JSInvokable callbacks. The JSInvokable callbacks mutate `Text` via `OnTextChanged`. The MudTextField fallback (D-D4) is unchanged.

## Threat Surface

Plan's threat register (`T-03P05-01` through `T-03P05-05`) was followed:
- **T-03P05-01 mitigate** — `OnSegmentInputFromJs` bounds-checks `segmentIndex` against `_renderTokens.Count`; `IsChip` guard prevents writing chip tokens; text flows through existing validator gate on save.
- **T-03P05-02 mitigate** — `_dotNetRef` disposed in `DisposeAsync`; per-component-instance scope.
- **T-03P05-03 accept** — trusted-LAN posture; Blazor circuit max message size inherited.
- **T-03P05-04 accept** — `key` string is browser-emitted; worst case is recoverable chip removal.
- **T-03P05-05 mitigate** — JSInvokables are instance methods; DotNetObjectReference scopes invocations to the originating component. No cross-circuit path.

No new threat surface introduced beyond the plan's register.

## Self-Check: PASSED

**Files modified (3) — all present:**
- `src/CookBot.Web/wwwroot/js/recipe-chip-composer.js` — FOUND (bindSegmentEvents + unbindSegmentEvents added)
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeChipComposer.razor` — FOUND (JSInvokables + lifecycle added)
- `tests/CookBot.Tests/Web/RecipeChipComposerTests.cs` — FOUND (6 new Fact tests added)

**Commits — all present in git log:**
- `ed24f3c` feat(03-05): add bindSegmentEvents/unbindSegmentEvents JS bridge — FOUND
- `46e4306` feat(03-05): wire JSInvokable OnSegmentInputFromJs bridge (WR-01 fix) — FOUND
- `6571a76` feat(03-05): implement OnSegmentKeyDownFromJs JSInvokable (IN-03 fix) — FOUND
- `5b700ac` test(03-05): add WR-01 typing + IN-03 Backspace regression tests — FOUND

**Build:** `dotnet build FreelovesCookBot.sln` — 0 warnings, 0 errors.

**Tests:** `dotnet test --filter "Category!=RequiresApiKey"` — 191 / 191 passing.
