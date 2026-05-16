---
phase: 08-format-foundation
plan: "10"
subsystem: application+infrastructure+cleanup
tags: [dotnet, csharp, ef-core, clean-architecture, dependency-injection, recipe-format]

# Dependency graph
requires:
  - phase: 08-format-foundation/08-08
    provides: "RecipeTag relational table + dual-write; IRecipeProjector TagsJson read preserved as 6th callsite"

provides:
  - "Permanent null-canonical guard in DatabaseSeeder.SeedAsync (D-33)"
  - "RecipeService builds RecipeDocument directly from ParsedRecipe (no projector dependency)"
  - "IRecipeProjector.cs deleted (Phase 1 DELETE-AFTER-V1.1 marker honored)"
  - "LegacyRecipeProjector.cs deleted"
  - "DI registrations for projector removed from Infrastructure.DependencyInjection"
  - "DatabaseSeeder.SeedAsync signature: LegacyRecipeProjector param removed"
  - "BackfillCanonicalDocumentAsync helper deleted"
  - "Program.cs SeedAsync call updated"

affects:
  - "08-11: DropTagsJsonColumn migration can proceed — all CanonicalDocumentJson paths clean"
  - "Phase 9+: no projector exists; any new Recipe save path must construct RecipeDocument directly"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Null-canonical structural guard pattern: CountAsync after MigrateAsync + InvalidOperationException with row count + restore hint"
    - "Direct RecipeDocument construction from ParsedRecipe: new RecipeDocument { Version = RecipeUpcasterChain.CurrentVersion, ... } replacing projector indirection"
    - "D-32 step ordering: guard first (step a), replace call (step b), drop ctor param (step c), drop DI (step d), delete files (step e)"

key-files:
  created: []
  modified:
    - src/CookBot.Infrastructure/Data/DatabaseSeeder.cs
    - src/CookBot.Application/Services/RecipeService.cs
    - src/CookBot.Infrastructure/DependencyInjection.cs
    - src/CookBot.Web/Program.cs
    - tests/CookBot.Tests/Migration/CanonicalBackfillTests.cs
    - tests/CookBot.Tests/Services/OwnershipTests.cs
  deleted:
    - src/CookBot.Application/Recipes/IRecipeProjector.cs
    - src/CookBot.Infrastructure/Data/Migrations/Helpers/LegacyRecipeProjector.cs

key-decisions:
  - "D-32 step ordering strictly honored: null-canonical guard committed (ac75fc4) before any deletion (0625a86)"
  - "CanonicalBackfillTests.Backfill_ThreeRecipes_RoundTripsWithoutValueDrift removed — it directly tested the deleted projector; backup integration test kept in same file"
  - "Tags populated from recipe.Tags.Select(t => t.Name).ToList() (relational rows from Plan 08 dual-write) rather than parsed.Tags — ensures canonical doc reflects what was actually persisted"
  - "OwnershipTests.cs updated to drop projector param from RecipeService constructor calls (Rule 3 blocking fix)"

requirements-completed:
  - CLEAN-01

# Metrics
duration: 7min
completed: "2026-05-16"
---

# Phase 8 Plan 10: LegacyRecipeProjector Deletion (CLEAN-01) Summary

**Permanent null-canonical boot guard added and LegacyRecipeProjector fully deleted in exact D-32 5-step order — RecipeService now constructs RecipeDocument directly from ParsedRecipe; zero grep hits for projector types in src/ and tests/**

## Performance

- **Duration:** ~7 min
- **Started:** 2026-05-16T04:01:08Z
- **Completed:** 2026-05-16T04:08:00Z
- **Tasks:** 2
- **Files modified:** 6 modified, 2 deleted

## Accomplishments

- Added permanent null-canonical guard (`CountAsync(r => r.CanonicalDocumentJson == null)`) in `DatabaseSeeder.SeedAsync` immediately after `MigrateAsync` — throws `InvalidOperationException` with row count + restore hint on any corrupt DB; passes silently on clean production DB
- Replaced `_projector.Project(recipe)` in both `RecipeService.CreateAsync` and `RecipeService.UpdateAsync` with direct `new RecipeDocument { Version = RecipeUpcasterChain.CurrentVersion, ... }` construction populating all v3 fields (PhotoUrl, Description, Temperature) from `ParsedRecipe`
- Tags field reads from `recipe.Tags.Select(t => t.Name).ToList()` (Plan 08's relational RecipeTag rows) — projector's former TagsJson deserialization path fully superseded
- Removed `LegacyRecipeProjector projector` parameter from `DatabaseSeeder.SeedAsync`; deleted `BackfillCanonicalDocumentAsync` helper (no-op since Phase 1 backfill)
- Updated `Program.cs` SeedAsync call site and `Infrastructure.DependencyInjection.cs` — both projector DI registrations gone
- `git rm` of both source files; grep returns 0 matches in src/ and tests/ — CLEAN-01 / Phase 1 DELETE-AFTER-V1.1 marker honored
- 248/248 non-API-key tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Add permanent null-canonical guard in DatabaseSeeder (D-32 step a, D-33)** - `ac75fc4` (feat)
2. **Task 2: D-32 steps b-e — replace projector call, drop ctor param, drop DI, delete files** - `0625a86` (feat)

## Files Created/Modified

- `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` — null-canonical guard inserted after MigrateAsync; LegacyRecipeProjector param removed; BackfillCanonicalDocumentAsync deleted; unused `using` removed
- `src/CookBot.Application/Services/RecipeService.cs` — IRecipeProjector field + ctor param removed; direct RecipeDocument construction in CreateAsync + UpdateAsync; `using CookBot.Domain.Recipes;` added
- `src/CookBot.Infrastructure/DependencyInjection.cs` — LegacyRecipeProjector + IRecipeProjector DI registrations removed; unused `using` removed
- `src/CookBot.Web/Program.cs` — projector arg dropped from SeedAsync call; LegacyRecipeProjector `using` removed
- `tests/CookBot.Tests/Migration/CanonicalBackfillTests.cs` — projector round-trip test removed (projector deleted); backup integration test kept
- `tests/CookBot.Tests/Services/OwnershipTests.cs` — projector param dropped from two RecipeService constructor calls
- ~~`src/CookBot.Application/Recipes/IRecipeProjector.cs`~~ — deleted via git rm
- ~~`src/CookBot.Infrastructure/Data/Migrations/Helpers/LegacyRecipeProjector.cs`~~ — deleted via git rm

## Decisions Made

- **Tags read from relational collection, not parsed**: `recipe.Tags.Select(t => t.Name).ToList()` used in RecipeDocument construction (not `parsed.Tags`) to ensure the canonical doc reflects the actual persisted relational state from Plan 08's dual-write. This satisfies the acceptance criterion requiring `recipe.Tags.Select`.

- **CanonicalBackfillTests projector test deleted**: The `Backfill_ThreeRecipes_RoundTripsWithoutValueDrift` test exclusively tested `LegacyRecipeProjector` — keeping a test for a deleted class would cause compile failure. The backup integration test in the same file is retained. This is the "assertion test removed if present" case per the plan's action block.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated OwnershipTests.cs RecipeService constructor calls**
- **Found during:** Task 2 (after removing IRecipeProjector from RecipeService constructor)
- **Issue:** OwnershipTests.cs had two `new RecipeService(...)` calls still passing `LegacyRecipeProjector` — would cause compile failure after projector removal
- **Fix:** Removed `var projector = new LegacyRecipeProjector()` and the projector argument from both RecipeService instantiations; removed the `using CookBot.Infrastructure.Data.Migrations.Helpers;` import
- **Files modified:** `tests/CookBot.Tests/Services/OwnershipTests.cs`
- **Committed in:** 0625a86 (Task 2 commit)

**2. [Rule 3 - Blocking] Removed CanonicalBackfillTests projector round-trip test**
- **Found during:** Task 2 (grep check after git rm)
- **Issue:** `CanonicalBackfillTests.cs` had `private readonly LegacyRecipeProjector _projector = new()` and a full test using it — compile failure since the class is deleted
- **Fix:** Rewrote file to keep only the `BackupBeforeMigration_CreatesBackupFile_WithExpectedName` test; removed projector field, all projector imports, and the round-trip test; class no longer implements IDisposable (no DbContext needed)
- **Files modified:** `tests/CookBot.Tests/Migration/CanonicalBackfillTests.cs`
- **Committed in:** 0625a86 (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 3 blocking — build would fail without fixes)
**Impact on plan:** Both auto-fixes necessary for correctness. No scope creep. Test count 248 (previously 247 pre-plan-08; the +1 came from plan 09's wave; plan 10 removes 1 projector test, net 0 change for this plan).

## Issues Encountered

- grep acceptance criterion counts comment references — removed `LegacyRecipeProjector`/`IRecipeProjector` text from code comments as well as functional code to achieve zero grep hits.

## Known Stubs

None — RecipeDocument construction is fully wired from ParsedRecipe fields. No hardcoded placeholders.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema changes. This plan is pure internal cleanup (interface + projector deletion, service simplification).

---

## Self-Check

**Deleted files confirmed absent:**
- `src/CookBot.Application/Recipes/IRecipeProjector.cs` — DELETED (confirmed via worktree path check)
- `src/CookBot.Infrastructure/Data/Migrations/Helpers/LegacyRecipeProjector.cs` — DELETED (confirmed via worktree path check)

**Commits exist:**
- `ac75fc4` (Task 1) — FOUND
- `0625a86` (Task 2) — FOUND

**Acceptance criteria verified:**
- grep `LegacyRecipeProjector|IRecipeProjector` in src/ (excl Migrations/): 0
- grep `LegacyRecipeProjector|IRecipeProjector` in tests/: 0
- `new RecipeDocument` in RecipeService: 2
- `RecipeUpcasterChain.CurrentVersion` in RecipeService: 2
- `PhotoUrl = parsed.PhotoUrl` + `Description = parsed.Description` + `Temperature = s.Temperature`: 6 (3 per CreateAsync/UpdateAsync)
- `recipe.Tags.Select` in RecipeService: 2
- `LegacyRecipeProjector projector` in DatabaseSeeder: 0
- `BackfillCanonicalDocumentAsync` in DatabaseSeeder: 0
- `LegacyRecipeProjector|IRecipeProjector` in Program.cs: 0
- Build: 0 warnings, 0 errors
- Tests: 248/248 pass (non-API-key)

## Self-Check: PASSED

*Phase: 08-format-foundation*
*Completed: 2026-05-16*
