---
phase: 14-photo-gallery
plan: "03"
subsystem: gallery-service
tags: [gallery, recipe-photo, file-cleanup, canonical-sync, ownership, cap]
dependency_graph:
  requires: ["14-01", "14-02"]
  provides: ["14-04"]
  affects: ["RecipeService", "LocalRecipePhotoStorage", "CanonicalDocumentJson"]
tech_stack:
  added: ["IRecipePhotoFileStorage interface", "RecipePhotoService in Infrastructure"]
  patterns: ["ExecuteUpdateAsync bulk-clear + change-tracker detach", "IRecipePhotoFileStorage abstraction (Clean Architecture boundary)"]
key_files:
  created:
    - src/CookBot.Application/Services/IRecipePhotoFileStorage.cs
    - src/CookBot.Infrastructure/Services/RecipePhotoService.cs
    - tests/CookBot.Tests/Services/RecipePhotoServiceTests.cs
  modified:
    - src/CookBot.Application/Services/RecipeService.cs
    - src/CookBot.Web/Services/LocalRecipePhotoStorage.cs
    - src/CookBot.Infrastructure/DependencyInjection.cs
    - src/CookBot.Web/Program.cs
    - tests/CookBot.Tests/Services/OwnershipTests.cs
    - tests/CookBot.Tests/Services/RecipeServiceV4FieldsTests.cs
decisions:
  - "RecipePhotoService placed in CookBot.Infrastructure (not Application) because CookBotDbContext is required for ExecuteUpdateAsync/OrderBy/CountAsync — IRepository<T> does not expose EF Core bulk-update APIs"
  - "IRecipePhotoFileStorage interface created in Application layer to break Application→Web circular dependency — LocalRecipePhotoStorage implements it"
  - "SyncPrimaryPhotoUrlAsync made public (was internal) so Infrastructure-layer RecipePhotoService can call it cross-assembly"
  - "ExecuteUpdateAsync bulk-clear followed by ChangeTracker.Entries detach before SaveChanges — prevents two-primary drift (RESEARCH Pitfall 3)"
metrics:
  duration: "9 minutes"
  completed_date: "2026-06-07"
  tasks: 3
  files: 9
---

# Phase 14 Plan 03: Service Layer (RecipePhotoService + SyncPrimaryPhotoUrlAsync) Summary

Built the complete service layer for the multi-photo gallery: `RecipePhotoService` with gallery CRUD, `RecipeService.SyncPrimaryPhotoUrlAsync` for canonical mirror updates, `RecipeService.DeleteAsync` orphaned-file cleanup, and `LocalRecipePhotoStorage.DeletePhysicalFile`.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | SyncPrimaryPhotoUrlAsync + DeleteAsync file cleanup + DeletePhysicalFile | 9b6e455 | IRecipePhotoFileStorage.cs, RecipeService.cs, LocalRecipePhotoStorage.cs |
| 2 | RecipePhotoService CRUD + DI wiring | 52efb9a | RecipePhotoService.cs, Infrastructure/DependencyInjection.cs |
| 3 | RecipePhotoService behavior tests | cde0f0d | RecipePhotoServiceTests.cs |

## What Was Built

**`IRecipePhotoFileStorage`** (new, `CookBot.Application/Services/`) — thin interface with `DeletePhysicalFile(string url)`. Allows `RecipeService` (Application) to delete local files without referencing `CookBot.Web`. Implemented by `LocalRecipePhotoStorage`.

**`LocalRecipePhotoStorage.DeletePhysicalFile`** (new method) — extracts filename via `Path.GetFileName`, combines with `_uploadsDir`, guards with `AssertPathInsideUploadsDirectory` (PITFALL H2), deletes if exists, no-op if missing.

**`RecipeService.SyncPrimaryPhotoUrlAsync`** (new public method) — the only place that writes `Recipe.PhotoUrl`/`CanonicalDocumentJson` for gallery-driven re-sync (P15/D-14-01). Reads IsPrimary photo (falls back to lowest SortOrder defensively), sets `recipe.PhotoUrl`, deserializes canonical doc, re-serializes with updated `PhotoUrl`.

**`RecipeService.DeleteAsync`** (extended) — enumerates `RecipePhoto` rows via `IRepository<RecipePhoto>.FindAsync` BEFORE cascade, calls `_photoStorage.DeletePhysicalFile` for each `/uploads/` URL inside a try/catch (non-fatal), then calls `_recipeRepo.DeleteAsync` (P13/D-14-11).

**`RecipePhotoService`** (`CookBot.Infrastructure/Services/`) — implements all 6 gallery methods:
- `GetPhotosAsync` — ordered by SortOrder
- `AddPhotoAsync` — server-side cap (`Math.Clamp(MaxPhotosPerRecipe, 1, 20)`), first photo auto-primary, SyncPrimaryPhotoUrlAsync on exit
- `SetPrimaryAsync` — bulk `ExecuteUpdateAsync` clear-all + detach tracked entities, set target, SaveChanges, Sync
- `ReorderAsync` — reassign SortOrder by index, Sync
- `DeleteAsync` — delete local file (non-fatal), remove row, promote lowest-SortOrder if was primary, Sync
- `UpdateCaptionAsync` — set caption, no Sync (caption not mirrored)

**DI wiring:**
- `Program.cs`: `AddScoped<IRecipePhotoFileStorage>` → `LocalRecipePhotoStorage`
- `Infrastructure/DependencyInjection.cs`: `AddScoped<RecipePhotoService>()`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Architecture] IRecipePhotoFileStorage interface added**
- **Found during:** Task 1 implementation
- **Issue:** `RecipeService` (Application) cannot reference `LocalRecipePhotoStorage` (Web) — circular dependency (Application → Web → Infrastructure → Application). The plan assumed direct injection.
- **Fix:** Created `IRecipePhotoFileStorage` interface in Application layer; `LocalRecipePhotoStorage` implements it; registered in `Program.cs` as `AddScoped<IRecipePhotoFileStorage>` → `LocalRecipePhotoStorage`.
- **Files modified:** `IRecipePhotoFileStorage.cs` (new), `LocalRecipePhotoStorage.cs`, `Program.cs`
- **Commits:** 9b6e455

**2. [Rule 2 - Architecture] RecipePhotoService placed in Infrastructure instead of Application**
- **Found during:** Task 2 implementation
- **Issue:** The plan specifies `CookBot.Application/Services/RecipePhotoService.cs` but `CookBotDbContext` is in Infrastructure — Application cannot reference Infrastructure.
- **Fix:** Created `RecipePhotoService` in `CookBot.Infrastructure/Services/`. Infrastructure references Application, so it can call `RecipeService.SyncPrimaryPhotoUrlAsync` and use `IRepository<Cookbook>` from Domain.
- **Files modified:** `src/CookBot.Infrastructure/Services/RecipePhotoService.cs` (new)
- **Commit:** 52efb9a

**3. [Rule 2 - Architecture] SyncPrimaryPhotoUrlAsync made public**
- **Found during:** Task 2 implementation
- **Issue:** `internal` visibility means cross-assembly callers (Infrastructure calling Application) can't access it.
- **Fix:** Changed to `public`. The method is not on a public interface, so it remains encapsulated from Razor components.
- **Files modified:** `RecipeService.cs`
- **Commit:** 52efb9a

**4. [Rule 1 - Bug] EF change-tracker two-primary drift fix**
- **Found during:** Task 3 (RED phase discovered, GREEN phase fixed)
- **Issue:** After `ExecuteUpdateAsync` (bypasses EF change tracker), tracked photo entities still hold stale `IsPrimary=true` in memory. `SaveChanges` re-applied them, producing 2 primary rows.
- **Fix:** In `SetPrimaryAsync` and promote-on-delete: detach all tracked `RecipePhoto` entities for the recipe via `ChangeTracker.Entries<RecipePhoto>()`, then re-fetch the target with `FindAsync` before setting `IsPrimary=true`.
- **Files modified:** `RecipePhotoService.cs`
- **Commit:** cde0f0d

**5. [Rule 2 - Bug] Existing tests updated for new RecipeService constructor**
- **Found during:** Task 1 build
- **Issue:** `OwnershipTests` and `RecipeServiceV4FieldsTests` construct `RecipeService` directly; they broke when the constructor gained `IRepository<RecipePhoto>`, `IRecipePhotoFileStorage`, and `ILogger<RecipeService>` params.
- **Fix:** Added `NullPhotoFileStorage` stub and `NullLogger` to both test fixtures.
- **Files modified:** `OwnershipTests.cs`, `RecipeServiceV4FieldsTests.cs`
- **Commit:** 9b6e455

## Test Results

439 tests pass (431 pre-existing + 8 new RecipePhotoServiceTests):
- First-photo-becomes-primary + PhotoUrl re-sync
- Exactly-one-primary after SetPrimary (including two-primary drift fix)
- Promote-on-delete (lowest SortOrder) + PhotoUrl re-sync
- Cap enforced (MaxPhotosPerRecipe=2, 3rd add throws)
- Local `/uploads/` file physically deleted on single-photo delete
- External `https://` URL delete is a no-op (no error)
- Cross-user mutation throws `UnauthorizedAccessException`
- Reorder reassigns SortOrder values

## Known Stubs

None — this plan delivers production service code, not UI stubs.

## Threat Flags

No new threat surface beyond what the plan's threat model covers. All T-14-06 through T-14-10 mitigations are implemented:
- T-14-06: Cross-user gallery mutation — `AssertOwnershipAsync` on every mutation
- T-14-07: Path traversal on file delete — `DeletePhysicalFile` routes through `AssertPathInsideUploadsDirectory`
- T-14-08: DoS cap — `Math.Clamp(MaxPhotosPerRecipe, 1, 20)` in `AddPhotoAsync`
- T-14-09: Orphaned files — `RecipeService.DeleteAsync` enumerates + deletes before cascade
- T-14-10: Primary mirror desync — only `SyncPrimaryPhotoUrlAsync` writes canonical

## Self-Check: PASSED

Files verified to exist:
- src/CookBot.Application/Services/IRecipePhotoFileStorage.cs — FOUND
- src/CookBot.Infrastructure/Services/RecipePhotoService.cs — FOUND
- tests/CookBot.Tests/Services/RecipePhotoServiceTests.cs — FOUND

Commits verified:
- 9b6e455 (Task 1) — FOUND
- 52efb9a (Task 2) — FOUND
- cde0f0d (Task 3) — FOUND

Build: 0 errors, 4 pre-existing warnings (unrelated RecipeTagBackfillTests EF1002)
Tests: 439 passed, 0 failed
