# Deferred Items — Phase 03

## Out-of-scope artifacts observed during Plan 03 execution

The following untracked files were observed in this worktree at execution time. They are
**NOT owned by Plan 03**; they belong to Plan 02 (Wave 2 sibling), and Plan 03's
parallel-safety guarantee says the two plans touch disjoint files. Plan 03 deliberately
did not delete or modify them — `<destructive_git_prohibition>` forbids removing files
the current task did not create.

- `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` (untracked)
- `tests/CookBot.Tests/Web/StepSectionToggleTests.cs` (untracked)

Two tests in `StepSectionToggleTests.cs` fail at run-time with
`InvalidOperationException : Missing <MudPopoverProvider />`. This is a Plan 02 issue
(Plan 02's bUnit setup likely needs `MudPopoverProvider` registered or its tests
restructured). Plan 03's full test surface (180 tests including the new TimerDetection,
TimerSuggestion, and PasteFlow tests) is green when these out-of-scope tests are excluded.

If Plan 02's worktree merge resolves these as part of its own scope, this entry can be
removed. If they persist after the wave merges, file a Plan 02 follow-up.
