---
phase: 10-qol-polish-consumer-surfaces
plan: "05"
subsystem: ai-chat-recovery
tags: [ai-chat, dialog, recovery, debounced-validation, qol]
dependency_graph:
  requires:
    - "Phase 7 Plan 07-01 (CbDialogService + SaveRecipeDialog migration)"
    - "Phase 1 (IRecipeFormatParser.TryParse + RecipeDocument canonical format)"
  provides:
    - "RawRecipeEditorDialog — QOL-04 recovery path for structured-output validation failures"
  affects:
    - "AiChat.razor D-09 fallback (replaced silent toast with dialog)"
tech_stack:
  added: []
  patterns:
    - "CbDialog modal via CbDialogService.ShowAsync"
    - "System.Threading.Timer debounce (500ms) for live validation"
    - "Two-dialog hop (RawRecipeEditorDialog → SaveRecipeDialog) preserving Phase 1 invariant"
    - "navigator.clipboard.writeText via IJSRuntime for clipboard copy"
    - "IDisposable timer teardown on circuit disposal"
key_files:
  created:
    - src/CookBot.Web/Components/Pages/RawRecipeEditorDialog.razor
  modified:
    - src/CookBot.Web/Components/Pages/AiChat.razor
decisions:
  - "Used editorParameters/editorOptions variable names in AiChat fallback to avoid CS0136 variable-shadowing with the enclosing parser-success scope's parameters/options locals"
  - "Chose Icon.Names.Bolt (styled red) for validation-failure status row — no X or close icon exists in the codebase icon set; Bolt conveys error without needing a new icon"
metrics:
  duration: "~10 minutes"
  completed: "2026-05-16"
  tasks_completed: 2
  tasks_total: 2
  files_created: 1
  files_modified: 1
---

# Phase 10 Plan 05: Raw AI Response Edit Dialog Summary

**One-liner:** Recovery dialog for AI structured-output failures — debounced live-validating textarea with two-dialog hop through SaveRecipeDialog on parse success.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create RawRecipeEditorDialog component | d94fd30 | src/CookBot.Web/Components/Pages/RawRecipeEditorDialog.razor (created) |
| 2 | Wire RawRecipeEditorDialog into AiChat OpenDraftInEditor fallback | ec3d6c1 | src/CookBot.Web/Components/Pages/AiChat.razor (modified) |

## What Was Built

**RawRecipeEditorDialog** (`src/CookBot.Web/Components/Pages/RawRecipeEditorDialog.razor`):

A CbDialog modal that opens when the AI's structured-output response cannot be parsed by `IRecipeFormatParser`. The dialog provides:

1. **Pretty-printed initial content** — `JsonNode.Parse(RawJson).ToJsonString(WriteIndented: true)` with a try/catch fallback to the verbatim raw string if the AI response is not even valid JSON.
2. **Debounced live validation** — `System.Threading.Timer` fires 500ms after the last keystroke, runs `Parser.TryParse`, and updates a status row (green check + "Valid recipe — ready to save" on success; red Bolt icon + "Validation failed: {first error}" on failure).
3. **Parse-and-save** — disabled when validation fails; on success, closes the dialog via `DialogInstance.Close(CbDialogResult.Ok(true))` and opens `SaveRecipeDialog` with the edited JSON. This two-dialog hop preserves the Phase 1 "never persist non-conforming recipes" invariant — `SaveRecipeDialog` re-parses + does the cookbook-picker + ownership checks before persisting.
4. **Copy raw to clipboard** — `JS.InvokeVoidAsync("navigator.clipboard.writeText", _editedJson)` with toast feedback.
5. **IDisposable** — `_debounceTimer?.Dispose()` in `Dispose()` prevents timer leaks on circuit teardown.

**AiChat.razor change** (`src/CookBot.Web/Components/Pages/AiChat.razor`):

Line 769-770 replaced: the `Toast.Show("The AI draft could not be parsed automatically...")` silent informational toast is gone. The D-09 fallback comment is updated to reference Phase 10 / QOL-04 / D-48 and now invokes `CbDialogService.ShowAsync<RawRecipeEditorDialog>("Edit raw AI response", editorParameters, editorOptions)`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] CS0136 variable-shadowing in AiChat fallback**
- **Found during:** Task 2 — first build after implementing the plan's suggested code
- **Issue:** The plan's suggested snippet used `var parameters` and `var options` for the fallback code. These names conflict with the identically-named locals declared earlier in the enclosing method scope (`OpenDraftInEditor`, lines 757 and 762 in the `if (cookbooks.Any())` block). Blazor Roslyn compilation raises CS0136.
- **Fix:** Renamed the fallback variables to `editorParameters` and `editorOptions`.
- **Files modified:** `src/CookBot.Web/Components/Pages/AiChat.razor`
- **Commit:** ec3d6c1

### Plan Accuracy Notes (not deviations — pre-existing state)

**Task 2 acceptance criteria stated `ShowAsync<SaveRecipeDialog>` count = 1.** The actual pre-existing count was 2 (line 709 and line 763 in AiChat.razor). My change did not add or remove any `SaveRecipeDialog` calls — count remains 2, unchanged. The plan's expected count of 1 was based on an incorrect count of the original file.

## Verification

```
dotnet build FreelovesCookBot.sln --nologo --verbosity quiet
# Build succeeded, 0 errors, 4 pre-existing EF1002 warnings in tests (out of scope)

grep -c "ShowAsync<RawRecipeEditorDialog>" src/CookBot.Web/Components/Pages/AiChat.razor
# 1

! grep -q "The AI draft could not be parsed automatically" src/CookBot.Web/Components/Pages/AiChat.razor
# exit 0 (line removed)
```

## Known Stubs

None. The dialog wires directly to the real `IRecipeFormatParser`, real `CbDialogService`, real `SaveRecipeDialog`, and real `IJSRuntime`. No hardcoded empty values or placeholder text in the data flow.

## Threat Flags

No new threat surface beyond what the plan's threat model covers (T-10-05-01, T-10-05-02, T-10-05-03). The variable rename deviation does not introduce new trust boundaries.

## Self-Check: PASSED

- [x] `src/CookBot.Web/Components/Pages/RawRecipeEditorDialog.razor` exists
- [x] Commit d94fd30 exists (`git log --oneline | grep d94fd30`)
- [x] Commit ec3d6c1 exists (`git log --oneline | grep ec3d6c1`)
- [x] Build succeeds (0 errors)
- [x] Old toast copy not in AiChat.razor
- [x] `ShowAsync<RawRecipeEditorDialog>` present in AiChat.razor
