---
phase: 03-editor-ux-without-special-syntax
plan: 03
subsystem: editor-ux
tags: [blazor, mudblazor, cooking-mode, paste-flow, timer-regex, regex, formatter, jsinterop]
requires:
  - Phase 1 D-12 (text-backed canonical record)
  - Phase 1 D-13 (link-resolution-only highlighting)
  - Plan 03-01 (shared RecipeChipComposer + IngredientLinkPatterns)
provides:
  - TimerDetectionService.Detect (fractional/range/multi-segment/simple)
  - RecipeStepTextFormatter.ToHtmlWithTimerSuggestions overload
  - InlineTimerSuggestion.razor (per-occurrence Yes/No popover)
  - CookingMode read-only chip rendering + ingredient scroll-and-highlight
  - PasteRawTextDialog parser pass-through (numbered-list fallback removed)
affects:
  - src/CookBot.Application/Services/TimerDetectionService.cs (rewritten — backward-compatible)
  - src/CookBot.Application/Services/RecipeStepTextFormatter.cs (extended)
  - src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor (15 lines deleted)
  - src/CookBot.Web/Components/Pages/CookingMode.razor (chip composer integration)
tech-stack:
  added: []
  patterns:
    - Unicode-bracket sentinels (U+27E6 ⟦ / U+27E7 ⟧) for two-pass HTML wrap-then-encode
    - Consumed-range tracking (bool[]) for ordered regex application without re-eating substrings
    - Half-open interval intersection check to skip wrapping inside existing ingredient-ref ranges
key-files:
  created:
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/InlineTimerSuggestion.razor
    - tests/CookBot.Tests/Application/TimerDetectionServiceRegexTests.cs
    - tests/CookBot.Tests/Web/PasteFlowTests.cs
    - tests/CookBot.Tests/Web/TimerSuggestionTests.cs
    - .planning/phases/03-editor-ux-without-special-syntax/deferred-items.md
  modified:
    - src/CookBot.Application/Services/TimerDetectionService.cs
    - src/CookBot.Application/Services/RecipeStepTextFormatter.cs
    - src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor
    - src/CookBot.Web/Components/Pages/CookingMode.razor
decisions:
  - Range timers persist as the LOWEST bound (RESEARCH.md A4: cooking convention — check at earliest doneness)
  - Multi-segment regex applies first to prevent SimplePattern eating "1 hour" out of "1 hour 30 minutes"
  - Sentinel-encoding strategy in ToHtmlWithTimerSuggestions: U+27E6/U+27E7 brackets (uncommon Unicode that survives WebUtility.HtmlEncode) wrap on the original text BEFORE encoding, then a post-pass regex emits literal <span> markup whose inner content has already been HTML-encoded by ToHtml. Solves the T-03P03-01 XSS threat AND the "no nested chip" idempotency concern in one pass.
  - Skip wrapping when the detected substring overlaps any [name](#id) ingredient-link match — prevents nested ingredient-ref/timer-suggestion spans
  - Cooking-mode step text wrapped in a styled <div> instead of MudText (RecipeChipComposer renders block-level chip flow; embedding in MudText would produce nested <p> tags)
  - bUnit PasteFlow test uses MudDialogProvider + IDialogService.ShowAsync pattern (a bare RenderComponent<MudDialog> renders empty markup because MudDialog requires a hosting provider)
metrics:
  duration: ~9 minutes
  completed: 2026-04-26
---

# Phase 3 Plan 03: Cooking Mode + Paste Flow + Timer Regex + Inline Timer Suggestion Summary

**One-liner:** Broadened timer detection to fractional/range/multi-segment patterns, shipped the inline timer-suggestion formatter overload with sentinel-based XSS-safe wrap, deleted the redundant numbered-list fallback in PasteRawTextDialog, and wired CookingMode to render via the shared RecipeChipComposer with chip-click → sidebar scroll-and-highlight.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Broaden TimerDetectionService regex + xUnit theory tests | `3723af1` | TimerDetectionService.cs, TimerDetectionServiceRegexTests.cs |
| 2 | PasteRawTextDialog cleanup + ToHtmlWithTimerSuggestions + InlineTimerSuggestion + bUnit tests | `12282db` | PasteRawTextDialog.razor, RecipeStepTextFormatter.cs, InlineTimerSuggestion.razor, PasteFlowTests.cs, TimerSuggestionTests.cs |
| 3 | Wire CookingMode to RecipeChipComposer + scroll-on-chip-click | `4f37862` | CookingMode.razor |

(Note: commit `ed99b3c` between Tasks 1 and 2 is from Plan 02's parallel sibling executor — not part of Plan 03 work.)

## Plan Output Questions Answered

**1. Did the sentinel-encoding strategy in `ToHtmlWithTimerSuggestions` work, or did it require a different approach?**

It worked exactly as written in the plan. The two-pass approach using Unicode brackets `⟦TS:N⟧…⟦/TS⟧` (U+27E6 / U+27E7):

1. First pass `WrapTimerSuggestionsWithSentinels` operates on the ORIGINAL text BEFORE HTML encoding, marking detected timer substrings with the sentinels and skipping any duration whose seconds value is in `alreadyConvertedDurationsSeconds` (D-C3) or whose range overlaps an existing `[name](#id)` ingredient-link match.
2. Second pass calls `ToHtml(wrappedSource)` which HTML-encodes everything (including `<script>` content) but leaves the U+27E6/U+27E7 sentinels untouched (they aren't `<>&"'`).
3. Final pass uses a Singleline+Compiled regex `⟦TS:(\d+)⟧(.*?)⟦/TS⟧` to substitute literal `<span class="timer-suggestion" data-duration-seconds="...">…</span>` markup.

This solves both the T-03P03-01 XSS threat (verified by test `ToHtmlWithTimerSuggestions_HtmlEncodesScriptInjection_T03P03_01`: a step containing `<script>alert(1)</script>` produces `&lt;script&gt;alert(1)&lt;/script&gt;` in the output, with the timer wrap still applied to the genuine `25 minutes` substring) AND the "no double-wrap inside ingredient-ref span" idempotency concern (the `OverlapsIngredientLink` check skips any candidate substring whose half-open range intersects an `IngredientLinkPatterns.Pattern` match).

**2. Was the bUnit `PasteFlowTests` source-presence check preserved or replaced with grep-based acceptance only?**

Replaced with the MudDialogProvider+IDialogService.ShowAsync pattern. A bare `RenderComponent<PasteRawTextDialog>` rendered empty markup because `MudDialog` requires a hosting `MudDialogProvider` cascade. The test now (a) renders `MudDialogProvider`, (b) calls `IDialogService.ShowAsync<PasteRawTextDialog>("Paste")`, (c) asserts the provider's markup contains the dialog's label `"Paste your recipe text here"`. This is more robust than source-string parsing and gives the dialog real lifecycle exercise.

The plan's grep-based gates (`! grep -qE '@\d+\.\s\*'` and `! grep -q 'partial = new ParsedRecipe'`) remain the deletion-confirmation gates — both pass.

**3. Any cooking-mode rendering quirks discovered during execution (nested MudText, etc.)?**

One quirk: `RecipeChipComposer` renders a block-level `<div class="chip-flow">`. Embedding it inside `<MudText Typo="Typo.h4">` (which itself emits a `<p>` or `<h4>`) would nest a block element inside an inline-context heading, producing invalid HTML and Blazor diff churn. The fix was to replace the wrapping `<MudText>` with a styled `<div class="mb-4 recipe-body" style="font-weight: 500; line-height: 1.6;">` for the current-step rendering. The section-header `<MudText Typo="Typo.subtitle1">` was left in place because section headings are short and rarely contain ingredient refs (Phase 1 D-02), so the chip composer's TokenizeText returns a single-segment span there — no block-in-inline conflict.

**4. Confirmation that `^\d+\.\s*` numbered-list regex is gone from `PasteRawTextDialog.razor`.**

Confirmed. `! grep -qE '@\d+\.\s*' src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor` exits 0. The deleted block (15 lines) handled `_rawText.Split('\n').Select(l => Regex.Replace(l.Trim(), @"^\d+\.\s*", "")).Where(...).ToList()` and the partial `ParsedRecipe { Steps = lines.Select(...) }` construction with a `MudDialog.Close(DialogResult.Ok(partial))` from the fallback branch. None of those statements survive. The Submit method is now the canonical 7-line parser pass-through described in PATTERNS.md / RESEARCH.md.

## Deviations from Plan

### Auto-fixed Issues (Rule 1 — Test correctness)

**1. [Rule 1 — Test bug] `ToHtmlWithTimerSuggestions_DoesNotWrapInsideIngredientLink` had a malformed assertion**

- **Found during:** Task 2, first test run.
- **Issue:** Initial assertion used a contorted `html.Substring(0, html.IndexOf("</span>") + …).Replace(" ", "")` expression that triggered `Assert.DoesNotContain("ingredient-ref", …)` even though the implementation was correct. The actual implementation correctly emits `Add <span class="ingredient-ref" data-ingredient-id="1">5 minute rice</span> and stir` with no nested timer-suggestion span — but the test's substring manipulation was checking the wrong thing.
- **Fix:** Simplified to two clear assertions: `Assert.DoesNotContain("data-ingredient-id=\"1\"><span class=\"timer-suggestion\"", html)` (no immediate nesting) and `Assert.DoesNotContain("class=\"timer-suggestion\"", html)` (the only candidate `5 minute` was inside the ingredient link, so no timer wrap should appear at all in this case).
- **Files modified:** `tests/CookBot.Tests/Web/TimerSuggestionTests.cs`.
- **Commit:** `12282db` (same as Task 2 — fix made before commit).

**2. [Rule 1 — Test bug] `PasteFlowTests.DialogShellRenders` produced empty markup**

- **Found during:** Task 2, first test run.
- **Issue:** A bare `ctx.RenderComponent<PasteRawTextDialog>()` returned empty markup because `MudDialog` only renders when hosted by a `MudDialogProvider`. The plan's example test code didn't account for this.
- **Fix:** Switched to the MudDialogProvider+IDialogService.ShowAsync pattern. Render `MudDialogProvider`, call `dialogService.ShowAsync<PasteRawTextDialog>("Paste")` inside `InvokeAsync`, then assert the provider's markup contains the dialog's input field label `"Paste your recipe text here"`.
- **Files modified:** `tests/CookBot.Tests/Web/PasteFlowTests.cs`.
- **Commit:** `12282db`.

### Plan-deviation acknowledged in advance (Wave 1 folder rename)

**3. [Wave 1 deviation] InlineTimerSuggestion.razor placed in `RecipeEditorParts/`, not `RecipeEditor/`**

- **Found during:** Wave 1 Plan 01 (already documented in 03-01-SUMMARY.md). The folder `Components/Pages/RecipeEditor/` collides with the existing `RecipeEditor.razor` page class via Razor's source-generator namespace collapse.
- **Fix applied here:** New file created at `src/CookBot.Web/Components/Pages/RecipeEditorParts/InlineTimerSuggestion.razor` (matching the renamed folder). The `@using CookBot.Web.Components.Pages.RecipeEditorParts` directive was added to `CookingMode.razor` so `<RecipeChipComposer>` resolves correctly.
- **Files affected:** `InlineTimerSuggestion.razor` placed under `RecipeEditorParts/`; `CookingMode.razor` `@using` updated.
- **Source:** Plan prompt's `<wave_1_deviation_note>` block.

### Out-of-scope artifacts observed (logged, not fixed)

**4. Plan 02 sibling files leaked into the worktree**

- **Found during:** Task 3 final test run.
- **Files (untracked at execution time):** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` and `tests/CookBot.Tests/Web/StepSectionToggleTests.cs`. These belong to Plan 02 (Wave 2 sibling).
- **Symptom:** Two tests in `StepSectionToggleTests.cs` fail with `Missing <MudPopoverProvider />` — this is a Plan 02 bUnit setup issue, not Plan 03 territory.
- **Action:** Logged in `.planning/phases/03-editor-ux-without-special-syntax/deferred-items.md` per scope-boundary rule. Per `<destructive_git_prohibition>`, did NOT delete the untracked files. Plan 03's full test surface (180 tests) is green when these out-of-scope tests are excluded; commit `ed99b3c` (Plan 02 partial) was created by the Plan 02 sibling worktree merge.
- **Plan-text impact:** None — Plan 03's success criteria are met independently.

## Authentication Gates

None.

## Threat-Surface Notes

The plan's `<threat_model>` listed:
- **T-03P03-01 mitigate (XSS via ToHtmlWithTimerSuggestions)** — Verified by `TimerSuggestionTests.ToHtmlWithTimerSuggestions_HtmlEncodesScriptInjection_T03P03_01`. A step containing `<script>alert(1)</script>` is HTML-encoded to `&lt;script&gt;alert(1)&lt;/script&gt;` while the timer wrap is correctly applied to the legitimate `25 minutes` substring. The sentinel approach guarantees the inner timer-suggestion span content goes through `WebUtility.HtmlEncode` (in `ToHtml`) before the post-pass regex inserts the `<span>` open/close — no path for raw HTML to leak into the output.
- **T-03P03-02 accept (regex backtracking DoS)** — All four regex patterns are linear-time on bounded input (recipe step text typically < 500 chars). No nested quantifiers over alternations. `RegexOptions.Compiled` enabled.
- **T-03P03-03 accept (cooking-mode chip click → JS)** — Element ID `ingredient-{int}` is constructed from `RecipeIngredient.RecipeLocalId` (server-assigned int, no user-input flow). Verified — no path to inject arbitrary strings into `getElementById`.
- **T-03P03-04 mitigate (paste-dialog hand-rolled fallback)** — The deletion is the mitigation. The canonical parser now gates all paste paths; no redundant escape hatch can swallow malformed input and persist partial data.

No new threat surface introduced beyond the register.

## TDD Gate Compliance

This plan has `tdd="false"` on all three tasks (frontmatter was not `type: tdd`); standard `feat()` commits are correct per task type. No RED/GREEN/REFACTOR sequence required.

## Self-Check: PASSED

**Files created (5) — all present:**
- src/CookBot.Web/Components/Pages/RecipeEditorParts/InlineTimerSuggestion.razor — FOUND
- tests/CookBot.Tests/Application/TimerDetectionServiceRegexTests.cs — FOUND
- tests/CookBot.Tests/Web/PasteFlowTests.cs — FOUND
- tests/CookBot.Tests/Web/TimerSuggestionTests.cs — FOUND
- .planning/phases/03-editor-ux-without-special-syntax/deferred-items.md — FOUND

**Files modified (4) — verified via grep gates:**
- src/CookBot.Application/Services/TimerDetectionService.cs — `MultiSegmentPattern`/`RangePattern`/`FractionalPattern`/`SimplePattern`/`DetectedTimer record` all present
- src/CookBot.Application/Services/RecipeStepTextFormatter.cs — `public static string ToHtmlWithTimerSuggestions` + `timer-suggestion` + `data-duration-seconds` all present
- src/CookBot.Web/Components/Pages/PasteRawTextDialog.razor — numbered-list regex AND `partial = new ParsedRecipe` both DELETED; `Parser.TryParse` retained
- src/CookBot.Web/Components/Pages/CookingMode.razor — 2× `<RecipeChipComposer Interactive="false"`, `OnIngredientChipClick="ScrollToIngredient"`, `id="@($"ingredient-{ri.RecipeLocalId}")"`, `private async Task ScrollToIngredient`, `RecipeChipComposer.scrollIntoViewWithHighlight`, `catch (JSDisconnectedException)`, `private List<ParsedIngredient> _recipeIngredientsParsed` all present

**Commits — all present in `git log`:**
- `3723af1` feat(03-03): broaden TimerDetectionService regex with fractional/range/multi-segment — FOUND
- `12282db` feat(03-03): paste pass-through, timer-suggestion overload, InlineTimerSuggestion popover — FOUND
- `4f37862` feat(03-03): wire CookingMode to RecipeChipComposer + scroll-to-ingredient on chip click — FOUND

**Build:** `dotnet build FreelovesCookBot.sln` — 0 warnings, 0 errors.

**Tests (excluding out-of-scope `StepSectionToggleTests` from Plan 02 sibling):** 180 / 180 passing (was 160 baseline; 20 new tests added — 14 in TimerDetectionServiceRegexTests, 5 in TimerSuggestionTests, 1 in PasteFlowTests).

**State files untouched (parallel-execution rule):** `git status --short .planning/STATE.md .planning/ROADMAP.md` — empty (no changes).

**No duplicate ingredient-link regex re-introduced:** `IngredientLinkPatterns.Pattern` is the sole source consumed by `RecipeStepTextFormatter.ToHtml` and the new `OverlapsIngredientLink` helper.
