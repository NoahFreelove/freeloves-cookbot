---
phase: 03-editor-ux-without-special-syntax
plan: 02
subsystem: editor-ux
tags: [blazor, mudblazor, editor, step-section-toggle, timer-chip, dialog, view-mode-toggle]
requires:
  - Phase 3 Plan 01 (RecipeChipComposer + IngredientChip + bUnit infrastructure)
  - Phase 1 D-13 (link-resolution-only highlighting)
  - Phase 1 D-12 (text-backed canonical record)
provides:
  - RecipeStepEditor.razor (per-step row component with Step/Section toggle, chip composer integration, timer chip strip, ephemeral D-A4 view-mode toggle)
  - TimerChip.razor (persisted explicit timer chip with edit popover — Duration / Unit / Label)
  - SectionDropConfirmationDialog.razor (D-B3 confirmation when converting non-empty Step to Section)
  - FakeDialogService bUnit recorder pattern (reusable IDialogService stub for future dialog-dispatch unit tests)
  - NoOpPopoverService bUnit pattern (lets MudMenu render in unit tests without a MudPopoverProvider)
  - MudAlert warning banner field on RecipeEditor.razor (D-D1/D-D2 receive surface)
affects:
  - src/CookBot.Web/Components/Pages/RecipeEditor.razor (slimmed; step body delegated to RecipeStepEditor; AddSectionHeader and DetectIngredientRefsInStep deleted; one Add button remains)
tech-stack:
  added: []  # No new packages
  patterns:
    - bUnit FakeDialogService recorder for verifying ShowAsync<T> dispatch + parameters without rendering a MudDialogProvider
    - bUnit NoOpPopoverService stub for components that use MudMenu / MudPopover internally
    - One-way bound MudToggleGroup (Value + ValueChanged) for async-confirmable toggles (Pitfall 5)
    - Ephemeral component-local UI state via private bool fields (D-A4) — distinct from [Parameter] state and from persisted entity state
key-files:
  created:
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/SectionDropConfirmationDialog.razor
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/TimerChip.razor
    - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor
    - tests/CookBot.Tests/Web/StepSectionToggleTests.cs
  modified:
    - src/CookBot.Web/Components/Pages/RecipeEditor.razor
decisions:
  - Components placed under `Pages/RecipeEditorParts/` (not the planned `Pages/RecipeEditor/`) per Wave 1 deviation — folder collision with `RecipeEditor.razor` page class.
  - TimerChip / RecipeStepEditor / Step.Timers all use `ParsedTimer` (CookBot.Domain.Interfaces) — the type ParsedStep.Timers actually holds — not StepTimer (CookBot.Domain.Entities) as written in the plan. Both have identical shape; this is a pure namespace correction with no behavioral impact.
  - FakeDialogService models the real MudBlazor 8.15 IDialogService surface (constraint `where T : IComponent`, full Show* / ShowAsync* overload set, Close, CreateReference, `DialogInstanceAddedAsync` / `OnDialogCloseRequested` events). The plan's draft used `where T : ComponentBase` and a fictional `GetDialogReference` / `OnDialogInstanceAdded` shape; corrected to compile against MudBlazor 8.15.
  - NoOpPopoverService stub registered in test context — TimerChip's MudMenu eagerly creates a MudPopover during render even with `Open=false`, which the real PopoverService rejects without a MudPopoverProvider in the render tree. The stub is the clean unit-test alternative to wrapping every render in a provider.
  - MudToggleGroup uses `Size="Size.Small"` (not the plan's `Dense="true"` — there is no Dense parameter on MudToggleGroup in MudBlazor 8.15).
metrics:
  duration: ~50 minutes
  completed: 2026-04-26
---

# Phase 3 Plan 02: RecipeStepEditor + Step/Section Toggle + D-A4 View-Mode Summary

**One-liner:** Composed Wave 1's chip composer into the actual editor surface — extracted the step-row body into `RecipeStepEditor.razor` owning the [Step | Section] toggle, the chip composer, the persisted timer chip strip, and the per-step ephemeral D-A4 "View as text / View as chips" view-mode toggle. Built `TimerChip` (edit popover) and `SectionDropConfirmationDialog` (D-B3). Slimmed `RecipeEditor.razor` to delegate step rendering and removed the `AddSectionHeader` / `DetectIngredientRefsInStep` legacy fallbacks. The chip composer is now the default step-text editing surface; the D-A4 toggle is the user-controlled escape hatch for raw `[name](#id)` markdown.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Build TimerChip + SectionDropConfirmationDialog | `ed99b3c` | RecipeEditorParts/TimerChip.razor, RecipeEditorParts/SectionDropConfirmationDialog.razor |
| 2 | Build RecipeStepEditor + StepSectionToggleTests (5 [Fact]s) | `5e73389` | RecipeEditorParts/RecipeStepEditor.razor, Web/StepSectionToggleTests.cs |
| 3 | Rewrite RecipeEditor.razor — collapse Add buttons, delete fallbacks, add warning banner, delegate step rows | `f030f8d` | RecipeEditor.razor |

## Plan Output Questions Answered

1. **Final line count of `RecipeEditor.razor`:** 431 (down from 468 → net -37). The plan's aspirational ≤ 320 target is **NOT** met. Rationale: the metadata, ingredients, paste-raw-text, and save blocks (which dominate the file by LOC) were intentionally untouched per the plan's task list. The substance — chip composer is the default step-text editing surface; AddSectionHeader and DetectIngredientRefsInStep are gone; one Add button; warning banner present; RecipeStepEditor delegation in place — is fully achieved. Documented under "Deviations from Plan".
2. **AddSectionHeader and DetectIngredientRefsInStep deleted?** Yes — `grep -r "AddSectionHeader\|DetectIngredientRefsInStep" src/` returns zero hits.
3. **FakeDialogService recorder used for D-B3 dispatch test?** Yes — `tests/CookBot.Tests/Web/StepSectionToggleTests.cs` defines an internal `FakeDialogService` recorder that captures `ShowAsync<T>(title, parameters, options)` calls. The `NonEmptyStepToSection_ShowsConfirmation_AndCancelReverts_DB3_Pitfall5` test asserts exactly one call with `DialogType == typeof(SectionDropConfirmationDialog)`, `TimerCount == 1`, `RefCount == 1`. The `NonEmptyStepToSection_ConfirmedConvert_DropsTimersAndStripsRefs_DB3` test exercises the post-confirmation path. No MudDialogProvider integration was needed; no manual-smoke punt.
4. **`_showRawMarkdown` invariants:**
   - `private bool _showRawMarkdown` in `RecipeStepEditor.razor` — confirmed not a `[Parameter]` (verified by negative grep `! grep -qE '\[Parameter\][^\]]*public bool _?ShowRawMarkdown'`).
   - `src/CookBot.Domain/Entities/RecipeStep.cs` was NOT modified by this plan (verified by negative grep `! grep -qE 'ShowRawMarkdown|ViewMode|view_mode' src/CookBot.Domain/Entities/RecipeStep.cs`).
   - `Step.Extras` is NOT augmented with any view-mode key (verified by negative grep `! grep -qE 'Extras\["(view_mode|ViewMode|ShowRawMarkdown|raw_markdown)"\]'`).
   - The toggle resets to chip view on save+reload — guaranteed by the field being component-local; `OnParametersSet` does not touch it.
5. **Deviations from `RecipeStepEditor` parameter signatures planned vs. shipped:** Two type-substitutions, one MudBlazor-API correction, one folder-path correction.
   - `Step.Timers` shape: shipped as `List<ParsedTimer>?` (the actual type on `ParsedStep` in CookBot.Domain.Interfaces); plan code referenced `List<StepTimer>` (CookBot.Domain.Entities). Same field shape, different namespace; pure namespace correction.
   - `TimerChip.Timer` parameter: shipped `ParsedTimer`, plan-code-spec was `StepTimer`. Same reason as above.
   - `MudToggleGroup` size: shipped `Size="Size.Small"`, plan-code-spec was `Dense="true"`. MudBlazor 8.15's MudToggleGroup has no Dense parameter.
   - Component path: shipped under `RecipeEditorParts/`, plan-spec was `RecipeEditor/`. Wave 1 deviation already covered this.

## Deviations from Plan

### Plan-text deviations (path renames inherited from Wave 1)

**1. [Plan deviation — Wave 1 inheritance] All component paths use `RecipeEditorParts/` instead of `RecipeEditor/`**
- **Reason:** Wave 1 had to rename the folder to dodge a Razor source-generator namespace collision with the existing `RecipeEditor.razor` page class. PLAN.md, PATTERNS.md, and CONTEXT.md still reference the old path.
- **Files affected (paths shipped):**
  - `src/CookBot.Web/Components/Pages/RecipeEditorParts/SectionDropConfirmationDialog.razor`
  - `src/CookBot.Web/Components/Pages/RecipeEditorParts/TimerChip.razor`
  - `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor`
- **Test using directive:** `using CookBot.Web.Components.Pages.RecipeEditorParts;`
- **RecipeEditor.razor `@using`:** `@using CookBot.Web.Components.Pages.RecipeEditorParts`

### Auto-fixed Issues (Rule 1 — Spec drift / type corrections)

**2. [Rule 1 — Type] Plan code referenced `StepTimer` (CookBot.Domain.Entities) but `ParsedStep.Timers` is `List<ParsedTimer>?` (CookBot.Domain.Interfaces)**
- **Found during:** Task 1 (drafting TimerChip) and Task 2 (drafting RecipeStepEditor).
- **Issue:** Plan's code blocks declared `[Parameter] public StepTimer Timer { get; set; }` and used `Step.Timers = new List<StepTimer>();`. Compiling against the actual domain types would have failed because `ParsedStep.Timers` is typed `List<ParsedTimer>?`.
- **Fix:** Substituted `ParsedTimer` for `StepTimer` everywhere in the new components and tests. The two types have identical shape (`int Duration`, `string Unit`, `string? Label`), so no behavioral or persistence change.
- **Files modified:** TimerChip.razor, RecipeStepEditor.razor, StepSectionToggleTests.cs.
- **Commits:** `ed99b3c` (TimerChip), `5e73389` (RecipeStepEditor + tests).

### Auto-fixed Issues (Rule 3 — Blocker)

**3. [Rule 3 — Blocker] Plan's FakeDialogService stub used wrong generic constraint and fictional members**
- **Found during:** Task 2 build.
- **Issue:** Plan's draft used `where T : ComponentBase` for `ShowAsync<T>` overrides. MudBlazor 8.15's `IDialogService.ShowAsync<TComponent>` actually constrains to `IComponent`. Build error: `CS0425: The constraints for type parameter 'T' of method 'FakeDialogService.ShowAsync<T>' must match...`. The plan also stubbed an `OnDialogInstanceAdded` event (real name: `DialogInstanceAddedAsync`, `Func<,>`) and a `GetDialogReference` method that does not exist on the interface. The full synchronous `Show*` overload family was missing entirely.
- **Fix:** Rewrote `FakeDialogService` against the real MudBlazor 8.15 surface — `where T : IComponent` constraint everywhere, full `Show* / ShowAsync*` overload family, `Close(IDialogReference, DialogResult?)`, `CreateReference()`, `event Func<IDialogReference, Task>? DialogInstanceAddedAsync`, `event Action<IDialogReference, DialogResult>? OnDialogCloseRequested`. `IDialogReference` stub matched the real surface (`Id`, `RenderFragment`, `Result` returns `Task<DialogResult?>`, `RenderCompleteTaskCompletionSource`, `Dialog`, `Dismiss(DialogResult?)`, `Close()`, `Close(DialogResult?)`, `InjectDialog`, `InjectRenderFragment`, `GetReturnValueAsync<T>`).
- **Commit:** `5e73389`.

**4. [Rule 3 — Blocker] MudMenu (rendered by TimerChip when Step.Timers is non-empty) crashed in bUnit tests with "Missing <MudPopoverProvider>"**
- **Found during:** Task 2, first test run. Two of five tests failed with `System.InvalidOperationException : Missing <MudPopoverProvider />` — specifically the tests that seeded `Step.Timers = new List<ParsedTimer> { ... }` (the others have empty timers and don't render `<TimerChip>`).
- **Issue:** TimerChip's `<MudMenu>` eagerly creates a `MudPopover` during render even when `Open=false`. The real `PopoverService.CreatePopoverAsync` rejects with this error if a `MudPopoverProvider` is not in the render tree. The standard fix (`ctx.RenderTree.Add<MudPopoverProvider>()`) failed because `MudPopoverProvider` has no `ChildContent` parameter — bUnit's RootRenderTree wrapper requires one.
- **Fix:** Stubbed `IPopoverService` with a `NoOpPopoverService` private record-class — implements `IsInitialized => true`, `CreatePopoverAsync` / `UpdatePopoverAsync` / `DestroyPopoverAsync` as completed Tasks, `Subscribe`/`Unsubscribe`/`GetProviderCountAsync` as no-ops, `DisposeAsync` returns `ValueTask.CompletedTask`. Registered via `ctx.Services.AddSingleton<IPopoverService>(new NoOpPopoverService())` in `CreateContext`. All five tests pass after the fix.
- **Files modified:** StepSectionToggleTests.cs.
- **Commit:** `5e73389`.

**5. [Rule 3 — Blocker] MudToggleGroup has no `Dense` parameter in MudBlazor 8.15**
- **Found during:** Task 2 first compile of RecipeStepEditor.
- **Issue:** Plan code wrote `<MudToggleGroup ... Dense="true" />`. Reflection against MudBlazor 8.15.0 confirms MudToggleGroup<T> has only Color, Size, Outlined, CheckMark, Delimiters, FixedContent, Vertical, Ripple, RightToLeft, SelectionMode, Disabled, Value/ValueChanged, Values/ValuesChanged.
- **Fix:** Replaced `Dense="true"` with `Size="Size.Small"`. Same visual outcome (compact rendering).
- **Commit:** `5e73389`.

### Documented spec gaps (no auto-fix needed)

**6. [Plan spec gap — line count target ≤ 320 not met]** RecipeEditor.razor finished at 431 lines, not ≤ 320. The reduction (468 → 431, -37 lines) is real and substantively complete: the inline step body (~52 lines), `AddSectionHeader` (4 lines), and `DetectIngredientRefsInStep` (11 lines) were all deleted. The `<RecipeStepEditor>` invocation block (~10 lines), warning-banner UI (~12 lines), `_warnings` field (1 line), `@using` directive (1 line), and warnings-clear in `PopulateFromParsed` (~8 lines including the comment) are all additions. The remaining bulk is metadata, ingredients, paste-raw-text, and save logic — which the plan's task list did not target. The aspirational ≤ 320 target was based on optimistic accounting; the spirit of the goal (chip composer as default step-text surface; legacy fallbacks gone; one Add button; warning banner) is fully met.

**7. [Plan grep brittleness]** The plan's `<verify>` regex `MudIconButton[^/]*(_showRawMarkdown|View as)` can't match across newlines. The MudIconButton in RecipeStepEditor spans 4 lines (Size + Icon + OnClick + aria-label). Verified manually via `tr '\n' ' ' < file | grep -qE '...'` that the implementation contains both the `_showRawMarkdown` reference (in the Icon expression and the OnClick handler) and the "View as" aria-label string.

## Authentication Gates

None.

## Threat-Surface Notes

The plan's `<threat_model>` was followed:
- **T-03P02-01 mitigate** — D-B3 confirmation dialog gates the destructive Step→Section path when `Timers` or refs are non-empty. Empty-state silent toggle is deliberate (D-B2). Visual-state revert on Cancel via Pitfall-5 one-way binding. **Both behaviors unit-tested** (`NonEmptyStepToSection_ShowsConfirmation_AndCancelReverts_DB3_Pitfall5` + `NonEmptyStepToSection_ConfirmedConvert_DropsTimersAndStripsRefs_DB3`).
- **T-03P02-02 accept** — `SectionDropConfirmationDialog` echoes only integer counts (`@TimerCount`, `@RefCount`); no step text rendered.
- **T-03P02-03 n/a** — D-A4 view-mode toggle is a render-branch switch; no new persistence path. Both branches (chip composer and raw-markdown MudTextField) bind `Step.Text` via `OnTextChanged`; no new save paths or fields.

No new threat surface introduced beyond the threat register.

## Self-Check: PASSED

**Files created (4) — all present:**
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/SectionDropConfirmationDialog.razor` — FOUND
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/TimerChip.razor` — FOUND
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` — FOUND
- `tests/CookBot.Tests/Web/StepSectionToggleTests.cs` — FOUND

**Files modified (1):**
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — modified (468 → 431 lines)

**Commits — all present in `git log`:**
- `ed99b3c` feat(03-02): add SectionDropConfirmationDialog + TimerChip components — FOUND
- `5e73389` feat(03-02): add RecipeStepEditor with Step/Section toggle + D-A4 view-mode — FOUND
- `f030f8d` feat(03-02): rewrite RecipeEditor.razor — collapse Add buttons + delegate steps — FOUND

**Build:** `dotnet build FreelovesCookBot.sln` — 0 warnings, 0 errors.

**Tests:** `dotnet test --filter "Category!=RequiresApiKey"` — 185 / 185 passing (155 prior + Plan 01's 4 + Plan 03-03's 26 + my 5 new StepSectionToggleTests).

**Substring helper retired:** `grep -r "DetectIngredientRefsInStep" src/` — empty.

**D-A4 entity invariant:** `grep -E 'ShowRawMarkdown|view_mode|ViewMode' src/CookBot.Domain/Entities/RecipeStep.cs` — empty.
