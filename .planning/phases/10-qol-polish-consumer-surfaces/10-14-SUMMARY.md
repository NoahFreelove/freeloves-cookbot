---
phase: 10-qol-polish-consumer-surfaces
plan: "14"
subsystem: web-ui
tags: [edit-profile, ai-usage, read-surface, telemetry, prod-17-phase10-read-surface]
requirements: [PROD-17]

dependency_graph:
  requires:
    - Plan 10-07 (AI assistant instructions card at W-05 ordinal #4 — card-order contract)
    - Plan 10-12 (Accent color card at W-05 ordinal #3 — card-order contract)
    - Phase 9 Plan 09-05 (AiUsageLog table + IX_AiUsageLogs_KeyOwnerId_Timestamp composite index)
    - CookBotDbContext.AiUsageLogs DbSet (pre-existing from Phase 9)
    - CookBotSettings.AiPricingVerifiedDate (pre-existing from Phase 9)
    - Microsoft.EntityFrameworkCore globally imported in _Imports.razor (pre-existing)
  provides:
    - EditProfile 'AI usage' CbCard at W-05 ordinal position #5
    - Rolling 30-day input tokens + output tokens + estimated cost USD figures
    - Empty state: "No AI activity in the last 30 days."
    - Pricing footnote: "Pricing as of {AiPricingVerifiedDate}"
    - Cross-user disclosure footnote: "Includes spending by users sharing your key"
  affects:
    - EditProfile page layout (new card at position #5 — between AI assistant instructions and AI features)

tech_stack:
  added: []
  patterns:
    - EF Core AsNoTracking + SumAsync aggregation over AiUsageLogs (Phase 9 telemetry read surface)
    - Server-side WHERE predicate for privacy boundary (KeyOwnerId == userId)
    - Conditional Razor rendering: empty state vs. dl figure rows
    - AiPricingVerifiedDate?.ToString("yyyy-MM-dd") ?? "—" null-safe display

key_files:
  created: []
  modified:
    - src/CookBot.Web/Components/Pages/EditProfile.razor

decisions:
  - "W-05 ordinal enforced: AI assistant instructions=109 < AI usage=136 < AI features=161"
  - "IsRetryAttempt == false filter applied server-side per PITFALL H9 — excludes 2-retry repair-loop rows to prevent double-counting"
  - "Microsoft.EntityFrameworkCore already globally imported in _Imports.razor — no per-file @using needed"
  - "SumAsync on empty source set returns 0 (not throws) — null SumAsync is not needed here; EF Sum on empty set returns 0 for long/decimal"
  - "LoadAiUsageAsync called inside the existing OnAfterRenderAsync user-switch block, immediately after _forceShowOwnKeyFields = false"

metrics:
  duration: "~5 minutes"
  completed_date: "2026-05-16"
  tasks_completed: 1
  tasks_total: 1
  files_created: 0
  files_modified: 1
---

# Phase 10 Plan 14: AI Usage Widget (EditProfile) Summary

**One-liner:** Rolling 30-day AI usage card added to EditProfile at W-05 ordinal #5; EF query aggregates AiUsageLogs by KeyOwnerId excluding retry rows; pricing footnote and cross-user disclosure rendered per PROD-17/PROD-18.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add 'AI usage' CbCard to EditProfile.razor at ordinal position #5 | ad662be | EditProfile.razor |

## What Was Built

### Task 1 — AI usage CbCard at W-05 ordinal position #5

`src/CookBot.Web/Components/Pages/EditProfile.razor` modified:

**New `<CbCard>` block** inserted immediately after the closing `</CbCard>` of "AI assistant instructions" (ordinal #4, Plan 10-07) and immediately before the "AI features" `<CbCard>` (ordinal #6, existing). Contains:

- `<CbEyebrow>AI usage</CbEyebrow>` with caption "Rolling 30-day Anthropic API usage for keys you own."
- Conditional rendering:
  - Empty state (`_aiInputTokens30d == 0 && _aiOutputTokens30d == 0 && _aiCost30d == 0`): renders "No AI activity in the last 30 days."
  - Non-empty state: `<dl>` grid with three rows — Input tokens (N0 format), Output tokens (N0 format), Estimated cost ($F4 format)
- Pricing footnote: `Pricing as of @(CookBotSettingsOptions.Value.AiPricingVerifiedDate?.ToString("yyyy-MM-dd") ?? "—").` (PITFALL H10)
- Cross-user disclosure footnote: `Includes spending by users sharing your key.` (PITFALL M9 / PROD-18)

**`@code` additions:**

Three fields added above the QOL-06 prompt editor fields:
```csharp
private long _aiInputTokens30d;
private long _aiOutputTokens30d;
private decimal _aiCost30d;
```

`LoadAiUsageAsync(int userId)` method added at end of `@code` block:
```csharp
private async Task LoadAiUsageAsync(int userId)
{
    var cutoff = DateTime.UtcNow.AddDays(-30);
    var rows = DbContext.AiUsageLogs.AsNoTracking()
        .Where(r => r.KeyOwnerId == userId && !r.IsRetryAttempt && r.Timestamp >= cutoff);
    _aiInputTokens30d = await rows.SumAsync(r => (long)r.InputTokens);
    _aiOutputTokens30d = await rows.SumAsync(r => (long)r.OutputTokens);
    _aiCost30d = await rows.SumAsync(r => r.EstimatedCostUsd);
}
```

`await LoadAiUsageAsync(_loadedUserId.Value)` called inside the existing `OnAfterRenderAsync` user-switch block immediately after `_forceShowOwnKeyFields = false;`.

**W-05 ordinal verified:** `<CbEyebrow>AI assistant instructions</CbEyebrow>` at line 109 < `<CbEyebrow>AI usage</CbEyebrow>` at line 136 < `<CbEyebrow>AI features</CbEyebrow>` at line 161.

## Deviations from Plan

None — plan executed exactly as written. One non-issue noted:

- `Microsoft.EntityFrameworkCore` (needed for `SumAsync`) is globally imported in `_Imports.razor` line 9 — no per-file `@using` directive needed. Plan step 3 said "Add `using Microsoft.EntityFrameworkCore;` if not already present" — correctly skipped.

## Threat Surface Scan

| Flag | File | Description |
|------|------|-------------|
| T-10-14-01 (documented, mitigated) | EditProfile.razor | EF WHERE predicate `KeyOwnerId == userId` is server-side — cross-user data cannot leak. Matches plan threat register disposition "mitigate". |
| T-10-14-02 (documented, mitigated) | EditProfile.razor | `!r.IsRetryAttempt` applied server-side — retry rows excluded per PITFALL H9. Matches plan threat register disposition "mitigate". |
| T-10-14-03 (documented, mitigated) | EditProfile.razor | Composite index `IX_AiUsageLogs_KeyOwnerId_Timestamp` from Phase 9 PROD-14 makes this O(log n). Rolling 30-day window caps result set. Matches plan threat register disposition "mitigate". |

No new threat surface beyond what the plan's threat model documented.

## Known Stubs

None. All shipped functionality is fully wired:
- `LoadAiUsageAsync` queries real `DbContext.AiUsageLogs` via server-side EF predicate
- `_aiInputTokens30d`, `_aiOutputTokens30d`, `_aiCost30d` bound to real aggregate query results
- `CookBotSettingsOptions.Value.AiPricingVerifiedDate` reads real `CookBotSettings` (Phase 9 PROD-16)
- Empty state conditional (`== 0 && == 0 && == 0`) handles both empty DB and genuinely zero usage

## Self-Check: PASSED

- `src/CookBot.Web/Components/Pages/EditProfile.razor` — FOUND; contains:
  - `AI usage` eyebrow: FOUND
  - `_aiInputTokens30d`: FOUND
  - `_aiOutputTokens30d`: FOUND
  - `_aiCost30d`: FOUND
  - `KeyOwnerId == userId`: FOUND
  - `!r.IsRetryAttempt`: FOUND
  - `AddDays(-30)`: FOUND
  - `Pricing as of`: FOUND
  - `Includes spending by users sharing your key`: FOUND
  - `No AI activity in the last 30 days`: FOUND
- W-05 ordinal: AI assistant instructions=109 < AI usage=136 < AI features=161 — PASSED
- Build (Web project): 0 errors, 0 warnings — PASSED
- Build (full solution): 0 errors in Web/Application/Infrastructure/Domain projects; 5 pre-existing errors in test project (CbTopBarServiceTests.cs references CbTopBarService not yet landed from parallel plan — out of scope per deviation rules scope boundary)
- Commit: ad662be — verified in git log
