---
phase: 14
plan: 01
subsystem: domain/infrastructure/tests
tags: [gallery, entity, migration, backfill, ef-core, settings]
dependency_graph:
  requires: []
  provides: [RecipePhoto entity, RecipePhotos table, GALLERY-01 backfill, MaxPhotosPerRecipe setting]
  affects: [Recipe entity (Photos nav), CookBotDbContext (RecipePhotos DbSet), CookBotSettings (MaxPhotosPerRecipe)]
tech_stack:
  added: []
  patterns: [relational-child-entity, ef-fluent-config-on-child, migration-sql-backfill, in-memory-sqlite-test]
key_files:
  created:
    - src/CookBot.Domain/Entities/RecipePhoto.cs
    - src/CookBot.Infrastructure/Data/Configurations/RecipePhotoConfiguration.cs
    - src/CookBot.Infrastructure/Migrations/20260607124611_AddRecipePhotosTable.cs
    - src/CookBot.Infrastructure/Migrations/20260607124611_AddRecipePhotosTable.Designer.cs
    - tests/CookBot.Tests/Migration/RecipePhotoBackfillTests.cs
  modified:
    - src/CookBot.Domain/Entities/Recipe.cs
    - src/CookBot.Infrastructure/Data/CookBotDbContext.cs
    - src/CookBot.Infrastructure/Migrations/CookBotDbContextModelSnapshot.cs
    - src/CookBot.Application/DTOs/CookBotSettings.cs
decisions:
  - "FK + cascade configured on child (RecipePhotoConfiguration), RecipeConfiguration.cs untouched (D-14-02)"
  - "Backfill runs in migration Up() via migrationBuilder.Sql for atomicity (research Pattern 3 / D-14 Claude's discretion)"
  - "MaxPhotosPerRecipe = 10 with clamped [1,20] note; no clamping in getter (matches DatabaseBackupRetention precedent D-14-04-cap)"
metrics:
  duration: "~12 minutes"
  completed: "2026-06-07"
  tasks_completed: 3
  files_changed: 9
---

# Phase 14 Plan 01: RecipePhoto Entity, Migration, Backfill, Settings Summary

## One-liner

Relational `RecipePhoto` child entity with EF cascade, composite index, GALLERY-01 backfill migration (one primary row per existing `Recipe.PhotoUrl`), and `MaxPhotosPerRecipe` cap setting — the data foundation for the multi-photo gallery.

## What Was Built

### Task 1: RecipePhoto entity + Recipe navigation + DbSet + EF configuration

**`RecipePhoto.cs`** (new) — child entity mirroring the `RecipeIngredient` POCO shape:
- `Id`, `RecipeId`, `Url` (max 2048, required), `Caption` (nullable, max 512), `SortOrder` (default 0), `IsPrimary` (default false), `Recipe Recipe = null!` back-ref.

**`Recipe.cs`** (modified) — added `ICollection<RecipePhoto> Photos { get; set; } = new List<RecipePhoto>();` after the `Tags` collection.

**`CookBotDbContext.cs`** (modified) — added `DbSet<RecipePhoto> RecipePhotos => Set<RecipePhoto>();` after `RecipeTags`. No change to `OnModelCreating` — `ApplyConfigurationsFromAssembly` auto-discovers `RecipePhotoConfiguration`.

**`RecipePhotoConfiguration.cs`** (new) — EF fluent config:
- PK on Id; Url `HasMaxLength(2048).IsRequired()`; Caption `HasMaxLength(512)` (no IsRequired); SortOrder `HasDefaultValue(0)`; IsPrimary `HasDefaultValue(false)`.
- Composite index `{ RecipeId, SortOrder }` for ordered gallery queries.
- FK `HasOne(p => p.Recipe).WithMany(r => r.Photos).HasForeignKey(p => p.RecipeId).OnDelete(DeleteBehavior.Cascade)` on the child — `RecipeConfiguration.cs` unmodified.

### Task 2: RecipePhotos table migration + MaxPhotosPerRecipe setting

**Migration `20260607124611_AddRecipePhotosTable`** (new):
- `CreateTable("RecipePhotos")` with all five columns (INTEGER autoincrement, TEXT url/caption, INTEGER sortorder/isprimary with defaults).
- FK `FK_RecipePhotos_Recipes_RecipeId` with `ReferentialAction.Cascade`.
- Non-unique composite index `IX_RecipePhotos_RecipeId_SortOrder`.
- GALLERY-01 backfill: `INSERT INTO RecipePhotos (RecipeId, Url, SortOrder, IsPrimary) SELECT Id, PhotoUrl, 0, 1 FROM Recipes WHERE PhotoUrl IS NOT NULL AND PhotoUrl != ''` — runs atomically inside `MigrateAsync()`.
- `Down()` drops the table.

**`CookBotSettings.cs`** (modified) — `MaxPhotosPerRecipe = 10` with XML-doc note that clamping `[1,20]` is done at runtime by `RecipePhotoService`.

### Task 3: Backfill + cascade regression test

**`RecipePhotoBackfillTests.cs`** (new) — 2 tests, both green:
1. **Backfill losslessness**: seeds 4 recipes (2 with PhotoUrl, 1 null, 1 empty) → runs exact migration SQL → asserts 2 rows, each `IsPrimary=true`, `SortOrder=0`, `Url` matching source.
2. **Cascade delete**: seeds recipe + 3 RecipePhoto rows → deletes recipe → asserts 0 rows remain.

## Deviations from Plan

**1. [Rule 2 - Minor] Fixed xUnit2029 warnings in backfill test**
- **Found during:** Task 3 test run
- **Issue:** `Assert.Empty(collection.Where(...))` triggers xUnit2029 analyzer warning — xUnit recommends `Assert.DoesNotContain` for predicate-based emptiness checks.
- **Fix:** Changed two `Assert.Empty(allPhotos.Where(...))` calls to `Assert.DoesNotContain(allPhotos, p => ...)`.
- **Files modified:** `tests/CookBot.Tests/Migration/RecipePhotoBackfillTests.cs`
- **Commit:** c4fe1fa

No other deviations — plan executed exactly as written.

## Verification

- `dotnet build` exits 0 (0 errors, 4 pre-existing warnings from `RecipeTagBackfillTests.cs` unrelated to this plan).
- `dotnet test --filter "FullyQualifiedName~RecipePhotoBackfillTests"` passes: 2/2 green.
- Migration file contains `INSERT INTO RecipePhotos` with `WHERE PhotoUrl IS NOT NULL AND PhotoUrl != ''` guard.
- `Recipe.PhotoUrl` is NOT removed or touched (D-14-01).
- `RecipeConfiguration.cs` is unmodified (verified via `git diff` showing 0 changed lines).
- `DatabaseSeeder.cs` is unmodified (backfill lives in migration `Up()` only).

## Known Stubs

None — this plan is data-layer only; no UI or service stubs exist.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema changes at external trust boundaries. The backfill SQL reads only from `Recipes.PhotoUrl` (trusted local column); no user-supplied input. T-14-01 and T-14-02 mitigations applied as specified.

## Self-Check: PASSED

- `src/CookBot.Domain/Entities/RecipePhoto.cs` — FOUND
- `src/CookBot.Infrastructure/Data/Configurations/RecipePhotoConfiguration.cs` — FOUND
- `src/CookBot.Infrastructure/Migrations/20260607124611_AddRecipePhotosTable.cs` — FOUND
- `tests/CookBot.Tests/Migration/RecipePhotoBackfillTests.cs` — FOUND
- Commits: 07a090f (Task 1), 8b3c543 (Task 2), c4fe1fa (Task 3) — all present in `git log`
