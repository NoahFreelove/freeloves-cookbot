---
phase: 08-format-foundation
plan: "07"
subsystem: domain+infrastructure
tags: [dotnet, csharp, ef-core, sqlite, migrations, schema]

# Dependency graph
requires:
  - phase: 08-format-foundation/08-03
    provides: "RecipeDocument.PhotoUrl + Description domain shape (this plan persists that shape to the DB)"

provides:
  - "Recipe.PhotoUrl: nullable string entity column (max 2048 via fluent API)"
  - "Recipe.Description: nullable string entity column (max 4096 via fluent API)"
  - "EF migration 20260516032653_AddRecipePhotoUrlAndDescription: two nullable AddColumn<string> calls on Recipes table"
  - "DatabaseSeeder: backup label derived from pending[0] migration name (removes hardcoded 'RecipeCanonicalDocument' literal)"

affects:
  - "08-08: AddRecipeTagTable migration picks up the backup-label fix automatically"
  - "08-11: DropTagsJsonColumn migration picks up the backup-label fix automatically"
  - "08-12: any subsequent migration picks up the backup-label fix automatically"
  - "Phase 9 (PHOTO-*): PhotoUrl column is the persistence target for the upload/paste-URL UI"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "EF fluent API HasMaxLength on nullable string columns — enforces DB constraint without data annotation on entity"
    - "DatabaseSeeder pending[0].Split('_', 2)[1] pattern for backup label derivation — makes each migration produce its own named .bak"
    - "Forward-only EF migration: Up() adds columns, Down() drops them; matches existing RecipeCanonicalDocument analog exactly"

key-files:
  created:
    - src/CookBot.Infrastructure/Migrations/20260516032653_AddRecipePhotoUrlAndDescription.cs
    - src/CookBot.Infrastructure/Migrations/20260516032653_AddRecipePhotoUrlAndDescription.Designer.cs
  modified:
    - src/CookBot.Domain/Entities/Recipe.cs
    - src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs
    - src/CookBot.Infrastructure/Data/DatabaseSeeder.cs
    - src/CookBot.Infrastructure/Migrations/CookBotDbContextModelSnapshot.cs

key-decisions:
  - "Max-length constraints placed in fluent API (RecipeConfiguration) not as data annotations on Recipe entity — matches existing project convention per D-28"
  - "PhotoUrl and Description placed adjacent to CanonicalDocumentJson in Recipe.cs — mirrors PATTERNS.md 'other string? fields cluster there' guidance"
  - "Tags navigation NOT added in this plan — deferred to Plan 08 (RecipeTag entity creation) per plan instructions"
  - "DatabaseSeeder backup label: pending[0].Split('_', 2)[1] recovers class name from '{timestamp}_{Name}' format — simplest correct approach"
  - "On a fresh-install boot, backup is created for V2InitialCreate (the actual first pending migration), not AddRecipePhotoUrlAndDescription — this is correct behavior; the backup covers the DB state before all pending migrations apply"

requirements-completed:
  - SCHEMA-05
  - SCHEMA-06

# Metrics
duration: 6min
completed: "2026-05-16"
---

# Phase 8 Plan 07: AddRecipePhotoUrlAndDescription Migration Summary

**EF migration #1 of D-31's four lands cleanly; Recipe entity gains PhotoUrl (max 2048) and Description (max 4096); DatabaseSeeder backup-label hardcode removed in favor of pending-migration-derived naming**

## Performance

- **Duration:** ~6 min
- **Started:** 2026-05-16T03:23:49Z
- **Completed:** 2026-05-16T03:30:36Z
- **Tasks:** 2
- **Files modified:** 6 (2 entity/config, 3 migration, 1 seeder)

## Accomplishments

- Added `string? PhotoUrl` and `string? Description` to `Recipe` entity, adjacent to `CanonicalDocumentJson`, with no default values (nullable, default NULL)
- Added `builder.Property(r => r.PhotoUrl).HasMaxLength(2048)` and `builder.Property(r => r.Description).HasMaxLength(4096)` to `RecipeConfiguration.Configure()` per D-28 fluent API convention
- Generated EF migration `20260516032653_AddRecipePhotoUrlAndDescription` with two `AddColumn<string>` calls (PhotoUrl maxLength:2048, Description maxLength:4096, both nullable:true) on the Recipes table
- `CookBotDbContextModelSnapshot.cs` auto-updated by EF tooling to include both new columns
- Removed hardcoded `"RecipeCanonicalDocument"` backup label from `DatabaseSeeder.SeedAsync`; replaced with `pending[0].Split('_', 2)[1]` derivation so each subsequent migration (Plans 08/11/12) produces its own correctly-named `.pre-{Name}.bak` file per D-31
- App boot (worktree fresh install): all migrations applied cleanly; `dotnet ef database update` confirms "No migrations were applied. The database is already up to date."
- 223/223 tests pass; 0 warnings, 0 errors on full solution build

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Recipe.PhotoUrl + Description entity columns + RecipeConfiguration constraints** - `62fea73` (feat)
2. **Task 2: Generate EF migration + fix DatabaseSeeder backup label** - `85c9af6` (feat)

## Files Created/Modified

- `src/CookBot.Domain/Entities/Recipe.cs` — `string? PhotoUrl` and `string? Description` added adjacent to `CanonicalDocumentJson`; XML doc comments explain purpose and phase
- `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs` — `HasMaxLength(2048)` for PhotoUrl and `HasMaxLength(4096)` for Description added after CanonicalDocumentJson configuration block
- `src/CookBot.Infrastructure/Migrations/20260516032653_AddRecipePhotoUrlAndDescription.cs` — new forward-only migration: Up() adds both columns, Down() drops both
- `src/CookBot.Infrastructure/Migrations/20260516032653_AddRecipePhotoUrlAndDescription.Designer.cs` — auto-generated designer file by EF tooling
- `src/CookBot.Infrastructure/Migrations/CookBotDbContextModelSnapshot.cs` — auto-updated by EF tooling to include both new columns
- `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` — backup label derivation rewritten: `pending[0].Split('_', 2)[1]` replaces hardcoded `"RecipeCanonicalDocument"`

## Decisions Made

- Placed max-length constraints in fluent API (not data annotations) to match the project convention established for other Recipe columns (`Name` uses `HasMaxLength(300)` via fluent API)
- Chose the simplest derivation for backup label: split on first underscore at count 2, take index 1; handles edge case where migration name has no underscore by returning the whole name
- Intentionally did not add the `ICollection<RecipeTag> Tags` navigation property — Plan 08 creates `RecipeTag` entity; adding the navigation before the entity exists would be premature

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] EF tools invoked with absolute paths to avoid worktree/main-repo CWD confusion**
- **Found during:** Task 2 (EF migration generation)
- **Issue:** CWD was the worktree directory but EF tools require explicit project/startup-project paths to target the correct source tree
- **Fix:** All `dotnet ef` and `dotnet build` commands used absolute paths to `$WT/src/CookBot.Infrastructure` and `$WT/src/CookBot.Web`
- **Impact:** No code changes; procedural fix only

**2. [Rule 3 - Blocking] Accidental commit to master (main repo) reversed before worktree work**
- **Found during:** Task 1 initial commit
- **Issue:** Initial `Edit` tool calls targeted `/home/noah/.../src/CookBot.Domain/Entities/Recipe.cs` (main repo path) instead of the worktree equivalent; `git commit` ran in the main repo and landed on `master`
- **Fix:** Reset master to `e0d68d8` (undoing the accidental commit); reset worktree branch to `e0d68d8` per `worktree_branch_check` instructions; re-applied all edits using worktree absolute paths
- **Impact:** Both task commits are on the `worktree-agent-a1992dfd3802cbffb` branch as required; master was restored to its pre-execution state

## Known Stubs

None — both new columns are persisted entity properties with correct schema. PhotoUrl and Description are intentionally NULL on existing rows (no data motion per plan); this is expected schema evolution, not a stub.

## Threat Flags

None — no new network endpoints, auth paths, or trust boundary changes. The new columns are persisted only via the existing EF save path; no new ingress surface introduced.

---

## Self-Check

**Created files exist:**
- `src/CookBot.Infrastructure/Migrations/20260516032653_AddRecipePhotoUrlAndDescription.cs` — FOUND
- `src/CookBot.Infrastructure/Migrations/20260516032653_AddRecipePhotoUrlAndDescription.Designer.cs` — FOUND

**Commits exist:**
- `62fea73` (Task 1) — FOUND
- `85c9af6` (Task 2) — FOUND

**Acceptance criteria verified:**
- `Recipe.PhotoUrl` property: 1 match
- `Recipe.Description` property: 1 match
- `HasMaxLength(2048)` and `HasMaxLength(4096)` in RecipeConfiguration: 2 matches
- `TagsJson` in RecipeConfiguration NOT removed: 1 match
- `ICollection<RecipeTag>` NOT added: 0 matches (correct)
- `AddColumn<string>` in migration: 2 matches
- `maxLength: 2048` in migration: 1 match
- `maxLength: 4096` in migration: 1 match
- `nullable: true` in migration: 2 matches
- Snapshot updated (PhotoUrl/Description): 3 matches
- Hardcoded `"RecipeCanonicalDocument"` in DatabaseSeeder: 0 matches (correct)
- `pending[0]` / `GetPendingMigrationsAsync` pattern: 2 matches
- Build: 0 warnings, 0 errors
- Tests: 223/223 pass

## Self-Check: PASSED

*Phase: 08-format-foundation*
*Completed: 2026-05-16*
