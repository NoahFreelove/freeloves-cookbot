---
phase: 10-qol-polish-consumer-surfaces
plan: "06"
subsystem: prompt-builder
tags: [prompt-builder, ai, null-fallback, tdd, qol]
dependency_graph:
  requires: []
  provides: [PromptBuilderService.BuildSystemPrompt null-fallback for QOL-06]
  affects: [PromptBuilderService, UserProfile.AiSystemPromptTemplate, AI system prompt]
tech_stack:
  added: []
  patterns: [TDD RED/GREEN, IsNullOrWhiteSpace ternary, xUnit Fact]
key_files:
  created:
    - tests/CookBot.Tests/Services/PromptBuilderServiceNullFallbackTests.cs
  modified:
    - src/CookBot.Application/Services/PromptBuilderService.cs
decisions:
  - "D-52: BuildSystemPrompt null-fallback — use IsNullOrWhiteSpace ternary; whitespace-only treated as null; corrects QOL-06 'already loaded' misclaim"
metrics:
  duration: "~2 minutes"
  completed: "2026-05-16"
  tasks_completed: 3
  tasks_total: 3
  files_created: 1
  files_modified: 1
---

# Phase 10 Plan 06: PromptBuilderService Null-Fallback Wiring Summary

**One-liner:** `BuildSystemPrompt` null-fallback ternary via `IsNullOrWhiteSpace` makes `UserProfile.AiSystemPromptTemplate` live (D-52), tested by three Fact tests covering null/whitespace/custom branches.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Write PromptBuilderServiceNullFallbackTests (RED) | b81f14d | tests/CookBot.Tests/Services/PromptBuilderServiceNullFallbackTests.cs |
| 2 | Rewire BuildSystemPrompt with null-fallback ternary (GREEN) | 4e1e1bc | src/CookBot.Application/Services/PromptBuilderService.cs |
| 3 | Verify Phase 8 PromptSnapshotTests still passes | (no commit — verify only) | (no changes) |

## What Was Built

`UserProfile.AiSystemPromptTemplate` has existed as a persisted field since v1.0 but was dead code — `PromptBuilderService.BuildSystemPrompt` always called `ResolveTemplate(DefaultTemplate, ...)`. This plan corrects that by introducing a null-fallback ternary:

```csharp
var template = string.IsNullOrWhiteSpace(profile.AiSystemPromptTemplate)
    ? DefaultTemplate
    : profile.AiSystemPromptTemplate;
return ResolveTemplate(template, profile, pantryItems);
```

Three Fact tests cover all branches: `null` → default, whitespace-only → default, non-empty custom → custom template honoured.

Phase 8's `PromptSnapshotTests` remains unaffected because it calls `svc.ResolveTemplate(DefaultTemplate, ...)` directly, not `BuildSystemPrompt`, and no `.verified.txt` snapshot files were regenerated.

## Verification Results

- `dotnet test --filter FullyQualifiedName~PromptBuilderServiceNullFallbackTests`: Passed (3/3)
- `dotnet test --filter FullyQualifiedName~PromptSnapshotTests`: Passed (2/2)
- `dotnet build src/CookBot.Application/`: Build succeeded (0 warnings, 0 errors)
- `git diff tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs`: empty (file unchanged)
- `git diff tests/CookBot.Tests/Snapshots/`: empty (no snapshot files modified)
- `git diff HEAD~2 HEAD -- src/CookBot.Application/Services/PromptBuilderService.cs`: only `BuildSystemPrompt` body changed

## Deviations from Plan

None — plan executed exactly as written. The TDD RED/GREEN cycle followed correctly:
- RED: `BuildSystemPrompt_CustomTemplate_RespectsOverride` failed (1/3 failed) before the production change
- GREEN: All 3 tests passed after the one-statement rewire

## Known Stubs

None. The null-fallback wiring is complete and exercises real production logic.

## Threat Flags

No new threat surface introduced beyond what the plan's threat model documented:

| Flag | File | Description |
|------|------|-------------|
| T-10-06-01 (documented, accepted) | src/CookBot.Application/Services/PromptBuilderService.cs | `AiSystemPromptTemplate` is now injected verbatim into the system prompt; owner-controlled, no XSS surface; QOL-07 (Plan 10-07) ships the warning UI |

## Self-Check: PASSED

- `tests/CookBot.Tests/Services/PromptBuilderServiceNullFallbackTests.cs` — FOUND
- `src/CookBot.Application/Services/PromptBuilderService.cs` (contains `IsNullOrWhiteSpace`) — FOUND
- Commit `b81f14d` — FOUND (`test(10-06): add failing PromptBuilderServiceNullFallbackTests (RED)`)
- Commit `4e1e1bc` — FOUND (`feat(10-06): rewire BuildSystemPrompt with null-fallback ternary (GREEN)`)
