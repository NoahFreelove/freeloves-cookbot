---
phase: 08-format-foundation
plan: 12
subsystem: database
tags: [ef-core, sqlite, migrations, indexes, composite-index]

# Dependency graph
requires:
  - phase: 08-07
    provides: AddRecipePhotoUrlAndDescription migration (D-31 migration #3)
provides:
  - AddPantryMatchIndexes EF migration (D-31 migration #4 of 4)
  - IX_RecipeIngredients_RecipeId_IngredientId composite index for Phase 10 pantry-match join performance
affects:
  - Phase 10 QOL-03 smart pantry-match algorithm

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Hand-written EF migration body for indexes not declared in entity configuration

key-files:
  created:
    - src/CookBot.Infrastructure/Migrations/20260516034227_AddPantryMatchIndexes.cs
    - src/CookBot.Infrastructure/Migrations/20260516034227_AddPantryMatchIndexes.Designer.cs

key-decisions:
  - "PantryItems already had IX_PantryItems_PantryId_IngredientId as a UNIQUE index via PantryItemConfiguration.HasIndex — the plan assumed UserId column (wrong) and a non-unique index (redundant since unique also satisfies lookup perf); the migration creates only the RecipeIngredients composite index"
  - "Hand-written Up() body with CreateIndex calls, not EF-generated from HasIndex in entity configuration — keeps Phase 8 entity configuration changes minimal"
  - "Column ordering in IX_RecipeIngredients_RecipeId_IngredientId follows PATTERNS.md (RecipeId first) over PITFALLS H7 (IngredientId first); if Phase 10 needs the alternate ordering a second index can be added then"

patterns-established:
  - "Hand-written EF migration CreateIndex pattern: CreateIndex(name, table, columns[]) with symmetric DropIndex in Down()"

requirements-completed: []

# Metrics
duration: 12min
completed: 2026-05-16
---

# Phase 8 Plan 12: AddPantryMatchIndexes Migration Summary

**D-31 migration #4 (final): IX_RecipeIngredients_RecipeId_IngredientId composite index ships Phase 8; PantryItems already covered by pre-existing unique index from PantryItemConfiguration**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-05-16T03:42:00Z
- **Completed:** 2026-05-16T03:54:00Z
- **Tasks:** 1
- **Files created:** 2

## Accomplishments

- Generated EF migration `AddPantryMatchIndexes` (timestamp `20260516034227`) via `dotnet ef migrations add`
- Hand-wrote the `Up()` body with `IX_RecipeIngredients_RecipeId_IngredientId` composite index on `RecipeIngredients(RecipeId, IngredientId)` for Phase 10 QOL-03 pantry-match join performance
- Confirmed `PantryItems` already has `IX_PantryItems_PantryId_IngredientId` as a UNIQUE index via `PantryItemConfiguration.HasIndex` — no duplicate index needed
- Verified `cookbot.db.pre-AddPantryMatchIndexes.bak` created on boot
- Build clean (0 warnings), 246 unit/integration tests pass (6 gated live-API tests excluded by `Category!=RequiresApiKey` filter as pre-existing behavior)

## Task Commits

1. **Task 1: Generate AddPantryMatchIndexes migration** - `2aeeec9` (feat)

## Files Created/Modified

- `src/CookBot.Infrastructure/Migrations/20260516034227_AddPantryMatchIndexes.cs` - Hand-written EF migration with one CreateIndex (RecipeIngredients) and symmetric DropIndex in Down()
- `src/CookBot.Infrastructure/Migrations/20260516034227_AddPantryMatchIndexes.Designer.cs` - Auto-generated EF migration scaffold/Designer file

## Decisions Made

1. **Only one index created (not two):** The plan specified two composite indexes — RecipeIngredients and PantryItems. On schema verification, `PantryItem` has `PantryId` (not `UserId` as the plan assumed). More critically, `PantryItemConfiguration.cs` already declares `HasIndex(p => new { p.PantryId, p.IngredientId }).IsUnique()`, which creates `IX_PantryItems_PantryId_IngredientId` as part of the initial schema migration. Adding a duplicate non-unique index would be redundant (a unique index serves all the query-performance purposes of a non-unique one). The migration therefore creates only `IX_RecipeIngredients_RecipeId_IngredientId`.

2. **Column ordering follows PATTERNS.md:** `(RecipeId, IngredientId)` per PATTERNS.md rather than `(IngredientId, RecipeId)` per PITFALLS H7. If Phase 10 discovers the alternate ordering is needed for join direction, a second index can be added in that phase (a forgivable schedule slip vs. potential confusion here).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] PantryItems index omitted — plan had wrong column name and duplicate intent**

- **Found during:** Task 1 (schema verification, Step A)
- **Issue:** Plan specified `PantryItem(UserId, IngredientId)` but `PantryItem` entity has `PantryId` not `UserId`. Furthermore, `PantryItemConfiguration.HasIndex(p => new { p.PantryId, p.IngredientId }).IsUnique()` already creates `IX_PantryItems_PantryId_IngredientId` as a UNIQUE composite index in the initial schema. The first smoke boot confirmed the index already existed and the migration attempt crashed with `SQLite Error 1: 'index IX_PantryItems_PantryId_IngredientId already exists'`.
- **Fix:** Omitted the PantryItems `CreateIndex` from Up() entirely. The pre-existing unique index satisfies the Phase 10 QOL-03 lookup-performance requirement. The migration comment documents this for future Phase 10 implementers.
- **Files modified:** `20260516034227_AddPantryMatchIndexes.cs`
- **Verification:** Smoke boot applied migration successfully; both `RecipeIngredients` and `PantryItems` confirm correct indexes via Python/SQLite introspection; all tests pass
- **Committed in:** `2aeeec9` (part of task commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - bug: wrong column name + redundant index)
**Impact on plan:** The Phase 10 QOL-03 performance requirement is still fully satisfied — `RecipeIngredients` gets its new composite index, and `PantryItems` already has an equivalent (stronger) unique composite index. Phase 10 can proceed as a zero-migration phase per CONTEXT.md D-31.

## Issues Encountered

- First smoke boot crashed because `dotnet ef migrations add` was run against the main repo path (`/home/noah/Desktop/projects/freeloves-cookbot`) while the worktree lives at `.claude/worktrees/agent-ab0e2ff3a113f74ea/`. The migration files were generated in the main repo and then copied to the worktree for staging. Both paths share the same SQLite DB, so the first boot (before the PantryItems issue was fixed) ran against the real DB.
- The `PromptSnapshotTests.BuildSystemPrompt` test showed a transient failure when run in the full test suite but passed in isolation and on re-run — a known Verify framework ordering sensitivity, not caused by this plan's changes.

## Known Stubs

None.

## Threat Flags

None — this plan creates only database indexes (read-only performance optimization). No new network endpoints, auth paths, file access patterns, or schema structure changes at trust boundaries.

## Next Phase Readiness

- D-31 (the four-migration group) is now fully complete: `AddRecipePhotoUrlAndDescription` + `AddRecipeTagTable` + `DropTagsJsonColumn` + `AddPantryMatchIndexes` have all shipped in Phase 8
- Phase 10 can implement QOL-03 (smart pantry-match) as a pure code-and-test phase with zero EF migrations as promised in CONTEXT.md D-31
- The composite index `IX_RecipeIngredients_RecipeId_IngredientId` is in the DB; Phase 10 should use `WHERE RecipeIngredients.RecipeId = X AND RecipeIngredients.IngredientId IN (...)` join patterns to leverage it

---
*Phase: 08-format-foundation*
*Completed: 2026-05-16*
