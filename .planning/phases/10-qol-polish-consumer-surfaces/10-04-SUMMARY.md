---
phase: 10-qol-polish-consumer-surfaces
plan: "04"
subsystem: home-page-pantry-match
tags: [home-page, pantry-match, integration, index-verification, qol-01, qol-03]
dependency_graph:
  requires: ["10-03"]
  provides: []
  affects:
    - src/CookBot.Web/Components/Pages/Home.razor.cs
    - tests/CookBot.Tests/Services/PantryMatchIndexSnapshotTests.cs
    - src/CookBot.Infrastructure/Data/Configurations/RecipeIngredientConfiguration.cs
tech_stack:
  added: []
  patterns:
    - IPantryMatchService DI injection in Blazor code-behind
    - PantryMatchResult → HomePantryMatch projection (thin view layer)
    - EF model introspection via GetIndexes() for index snapshot testing
    - In-memory SQLite + EnsureCreated() test pattern (OwnershipTests analog)
key_files:
  created:
    - tests/CookBot.Tests/Services/PantryMatchIndexSnapshotTests.cs
  modified:
    - src/CookBot.Web/Components/Pages/Home.razor.cs
    - src/CookBot.Infrastructure/Data/Configurations/RecipeIngredientConfiguration.cs
decisions:
  - "MetaLine computed inline in projection as 'uses {N} of {M} ingredients' — PantryMatchResult does not carry PrepTime/CookTime fields, so the time prefix from the old stub's MatchMetaLine is intentionally dropped; the scoring quality (D-44 formula) outweighs the time prefix loss"
  - "HasIndex(ri => new { ri.RecipeId, ri.IngredientId }) added to RecipeIngredientConfiguration to mirror the migration-applied composite index in the EF model snapshot — prevents future scaffold migrations from accidentally emitting a DROP INDEX"
  - "Migration file path resolution in test 3 uses 5 levels of ../.. from AppContext.BaseDirectory (net10.0 → Debug → bin → CookBot.Tests → tests → repo root)"
metrics:
  duration_minutes: 15
  completed: "2026-05-17"
  tasks_completed: 2
  tasks_total: 2
  files_created: 1
  files_modified: 2
---

# Phase 10 Plan 04: Home Pantry-Match Swap + Index Snapshot Tests Summary

**One-liner:** Home.razor.cs's deterministic 60%-coverage stub replaced by a thin projection over `IPantryMatchService.GetMatchesAsync`, delivering D-44 exponential-decay ranked results; three EF model snapshot tests guard the Phase 8 composite indexes.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Swap Home.razor.cs BuildPantryMatchesAsync to call IPantryMatchService | d13aed5 | `src/CookBot.Web/Components/Pages/Home.razor.cs` |
| 2 | Write PantryMatchIndexSnapshotTests + HasIndex fix | 4f2fee3 | `tests/CookBot.Tests/Services/PantryMatchIndexSnapshotTests.cs`, `src/CookBot.Infrastructure/Data/Configurations/RecipeIngredientConfiguration.cs` |

## What Was Built

### Task 1: Home.razor.cs IPantryMatchService swap

`src/CookBot.Web/Components/Pages/Home.razor.cs` — five-step edit per plan:

1. **Added `[Inject]`** `private IPantryMatchService PantryMatchService { get; set; } = null!;` alongside `IRecipeMadeService`
2. **Verified namespaces** — `CookBot.Application.Services` and `CookBot.Application.DTOs` already imported at top of file (no new `using` needed)
3. **Replaced stub body** — `BuildPantryMatchesAsync(int userId, IList<PantryItem> pantryItems)` (lines 314-356 in pre-edit file) deleted; replaced with:
   ```csharp
   private async Task<List<HomePantryMatch>> BuildPantryMatchesAsync(int userId, CancellationToken ct = default)
   {
       var results = await PantryMatchService.GetMatchesAsync(userId, ct);
       return results.Select(r => new HomePantryMatch(
           r.RecipeId, r.RecipeName, r.MatchedCount, r.TotalCount,
           $"uses {r.MatchedCount} of {r.TotalCount} ingredients",
           r.FirstMissingIngredientName, r.PhotoUrl)).ToList();
   }
   ```
4. **Updated caller** from `BuildPantryMatchesAsync(userId, allItems)` to `BuildPantryMatchesAsync(userId)` — `allItems` local variable retained for the pantry-glance counters (`_accessiblePantryItemCount`, `_pantryCount`, `_pantryLowCount`, `_pantryExpiringCount`)
5. **Preserved** `OnAfterRenderAsync` POLISH-05 tick hook (from Wave 2 Plan 10-13), `_photoFailedFor` hash set, `HandlePhotoError`, and all other unchanged methods

`HomePantryMatch` record has 7 positional parameters in the current codebase (added `PhotoUrl` in a prior wave): `RecipeId, RecipeName, MatchedCount, TotalCount, MetaLine, MissingIngredientName, PhotoUrl` — projection maps all 7 correctly from `PantryMatchResult`.

### Task 2: PantryMatchIndexSnapshotTests + RecipeIngredientConfiguration fix

`tests/CookBot.Tests/Services/PantryMatchIndexSnapshotTests.cs` — 3 `[Fact]` tests using in-memory SQLite + `EnsureCreated()` pattern from `OwnershipTests.cs`:

| Test | Assertion |
|------|-----------|
| `RecipeIngredient_HasCompositeIndexOn_RecipeId_IngredientId` | EF model has index over `["RecipeId", "IngredientId"]` on `RecipeIngredient` |
| `PantryItem_HasCompositeIndexOn_PantryId_IngredientId` | EF model has index over `["PantryId", "IngredientId"]` on `PantryItem` |
| `AddPantryMatchIndexes_MigrationFile_Exists` | File matching `*_AddPantryMatchIndexes.cs` exists in Migrations directory |

`src/CookBot.Infrastructure/Data/Configurations/RecipeIngredientConfiguration.cs` — added `builder.HasIndex(ri => new { ri.RecipeId, ri.IngredientId })` so the EF model snapshot matches the migration-applied composite index. Without this, test 1 would fail because `EnsureCreated()` only applies model-configured indexes.

## Deviations from Plan

### Auto-added: HasIndex to RecipeIngredientConfiguration (Rule 2 — Missing Critical Functionality)

**Found during:** Task 2 implementation — when writing the EF model introspection test, discovered that `IX_RecipeIngredients_RecipeId_IngredientId` was added only via the `AddPantryMatchIndexes` migration, not via `IEntityTypeConfiguration.HasIndex()`. The EF model snapshot (`GetIndexes()`) does not include migration-only indexes, so test 1 would have failed without this fix.

**Issue:** The EF model was out-of-sync with the database schema — a future `dotnet ef migrations add` scaffold would see the migration-applied index as "unknown" and potentially emit a `DROP INDEX` then `CREATE INDEX` cycle or miss it entirely.

**Fix:** Added `builder.HasIndex(ri => new { ri.RecipeId, ri.IngredientId })` to `RecipeIngredientConfiguration.Configure()` with a comment linking to QOL-03 and the Phase 8 migration. This keeps the EF model snapshot consistent with the database.

**Why not architectural:** No new table, no new column, no schema change — the index already exists in the database. This is a model-configuration alignment fix.

**Files modified:** `src/CookBot.Infrastructure/Data/Configurations/RecipeIngredientConfiguration.cs`
**Commit:** 4f2fee3

### Path adjustment: Migration file test used 5 levels of `../..` not 4 (Rule 1 — Bug)

**Found during:** Task 2 verification — test 3 threw `DirectoryNotFoundException` with 4 levels. Counted: `AppContext.BaseDirectory` = `bin/Debug/net10.0/` → up 4 = `CookBot.Tests/` → correct is 5 up = `tests/` → 6 up = repo root. Fixed to 5 `../` to reach the repo root, then `src/CookBot.Infrastructure/Migrations`.

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet build` | PASS (0 errors, pre-existing 4 EF1002 warnings only) |
| `dotnet test --filter PantryMatchServiceTests` | PASS — 14/14 passing |
| `dotnet test --filter PantryMatchIndexSnapshotTests` | PASS — 3/3 passing |
| `grep MinCoverageRatio Home.razor.cs` returns 0 | PASS |
| `grep "BuildPantryMatchesAsync(userId);"` returns ≥ 1 | PASS |
| `grep "var allItems = await PantryService.GetAllUserAccessibleItemsAsync"` returns 1 | PASS |
| `IPantryMatchService PantryMatchService` inject present | PASS |
| `PantryMatchService.GetMatchesAsync` called in stub | PASS |

## Known Stubs

None. Home.razor.cs now calls the real `IPantryMatchService.GetMatchesAsync` (Plan 10-03) which applies the D-44 exponential-decay scoring, D-45 AND-combined dietary filter, and D-46 configurable weights from `appsettings.json`. No hardcoded/placeholder values flow to the pantry-match hero.

## Threat Flags

None beyond what the plan's threat register covers:
- T-10-04-01 (Information Disclosure): All authz filtering happens inside `PantryMatchService.GetMatchesAsync` — Home is a pure projection layer with no authz logic.
- T-10-04-02 (Composite index removal): Guarded by `PantryMatchIndexSnapshotTests` — all 3 tests pass.

## Self-Check: PASSED

- `src/CookBot.Web/Components/Pages/Home.razor.cs` — FOUND, modified
- `tests/CookBot.Tests/Services/PantryMatchIndexSnapshotTests.cs` — FOUND, created
- `src/CookBot.Infrastructure/Data/Configurations/RecipeIngredientConfiguration.cs` — FOUND, modified
- Commit d13aed5 — FOUND (feat(10-04): swap Home BuildPantryMatchesAsync stub for IPantryMatchService)
- Commit 4f2fee3 — FOUND (feat(10-04): add PantryMatchIndexSnapshotTests + HasIndex to RecipeIngredientConfig)
- `dotnet test --filter PantryMatchIndexSnapshotTests` — Passed: 3, Failed: 0
- `dotnet test --filter PantryMatchServiceTests` — Passed: 14, Failed: 0
- `dotnet build FreelovesCookBot.sln` — 0 errors
