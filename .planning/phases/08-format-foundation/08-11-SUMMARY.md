---
phase: 08-format-foundation
plan: 11
subsystem: database
tags: [ef-core, sqlite, migrations, recipe-tags, clean-02, drop-column]

# Dependency graph
requires:
  - phase: 08-08
    provides: "AddRecipeTagTable migration + relational callsite switchover (D-26 dual-write)"
  - phase: 08-10
    provides: "CLEAN-01 — LegacyRecipeProjector deleted; D-32 complete"
provides:
  - "Recipe.TagsJson C# property removed from Domain entity"
  - "RecipeConfiguration HasDefaultValue('[]') clause removed"
  - "RecipeService dual-write to TagsJson removed — relational RecipeTag is sole tag path"
  - "DropTagsJsonColumn EF migration (20260516041718) — Recipes.TagsJson column dropped"
  - "CLEAN-02 fully closed — relational RecipeTag table is the only source of truth for recipe tags"
affects:
  - "08-12 (AddPantryMatchIndexes already shipped in wave 4; no dependency)"
  - "Any future plan reading Recipe entity or querying tag data"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Raw SQL seeding in tests for pre-drop DB state simulation (RecipeTagBackfillTests)"
    - "EF DropColumn migration with AddColumn in Down() for rollback completeness"

key-files:
  created:
    - src/CookBot.Infrastructure/Migrations/20260516041718_DropTagsJsonColumn.cs
    - src/CookBot.Infrastructure/Migrations/20260516041718_DropTagsJsonColumn.Designer.cs
  modified:
    - src/CookBot.Domain/Entities/Recipe.cs
    - src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs
    - src/CookBot.Infrastructure/Migrations/CookBotDbContextModelSnapshot.cs
    - src/CookBot.Application/Services/RecipeService.cs
    - src/CookBot.Web/Components/Pages/RecipeView.razor
    - tests/CookBot.Tests/Recipes/RecipeTagBackfillTests.cs
    - tests/CookBot.Tests/Services/OwnershipTests.cs
    - tests/CookBot.Tests/Services/RecipeAccessExtensionsTests.cs
    - tests/CookBot.Tests/Services/RecipeCookingAiContextTests.cs

key-decisions:
  - "RecipeTagBackfillTests reframed via raw SQL ALTER TABLE + UPDATE to simulate pre-drop DB state (option a from plan) — backfill SQL regression value preserved without the C# property"
  - "System.Text.Json using directive removed from RecipeService (JsonSerializer.Serialize no longer called; JsonRecipeSerializer is from Application.Recipes namespace)"
  - "Stale TagsJson references in RecipeView.razor comments updated to remove obsolete column name"
  - "Smoke test backup file absence is correct: DatabaseBackupService skips backup on fresh install (no prior DB file) — in production upgrade scenario the backup would appear"

patterns-established:
  - "Drop-column migrations must be preceded by callsite switchover (D-26 sequencing: dual-write in Plan 08, drop in Plan 11)"
  - "Tests that validate pre-drop SQL behavior seed via raw SQL DDL after EnsureCreated() to simulate the historical DB state"

requirements-completed:
  - CLEAN-02

# Metrics
duration: 8min
completed: 2026-05-16
---

# Phase 8 Plan 11: DropTagsJsonColumn Summary

**CLEAN-02 finalized: Recipe.TagsJson C# property deleted, RecipeService dual-write removed, DropTagsJsonColumn EF migration generated — relational RecipeTag table is now the sole tag store**

## Performance

- **Duration:** 8 min
- **Started:** 2026-05-16T04:12:23Z
- **Completed:** 2026-05-16T04:20:27Z
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments

- Removed `Recipe.TagsJson` property from Domain entity — EF model no longer maps the column
- Removed `HasDefaultValue("[]")` from `RecipeConfiguration` — configuration reflects entity removal
- Removed both `TagsJson = JsonSerializer.Serialize(parsed.Tags)` dual-write lines from `RecipeService.CreateAsync` and `UpdateAsync` — only the relational `recipe.Tags.Add(new RecipeTag { Name = name })` path remains
- Generated `DropTagsJsonColumn` migration (timestamp `20260516041718`) with correct `Up()=DropColumn("TagsJson","Recipes")` and symmetric `Down()=AddColumn`
- Updated `RecipeTagBackfillTests` to seed the TagsJson column via raw SQL DDL + UPDATE, preserving full regression coverage of the backfill SQL without relying on the removed C# property
- Updated 4 test files (`OwnershipTests`, `RecipeAccessExtensionsTests`, `RecipeCookingAiContextTests`, `RecipeTagBackfillTests`) to remove stale TagsJson property references

## Task Commits

Each task was committed atomically:

1. **Task 1: Remove Recipe.TagsJson property + RecipeConfiguration default + RecipeService dual-write** - `3372253` (feat)
2. **Task 2: Generate DropTagsJsonColumn EF migration** - `88d4f14` (feat)

## Files Created/Modified

- `src/CookBot.Domain/Entities/Recipe.cs` — TagsJson property removed
- `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs` — HasDefaultValue("[]") removed
- `src/CookBot.Application/Services/RecipeService.cs` — dual-write removed, unused System.Text.Json import removed
- `src/CookBot.Infrastructure/Migrations/20260516041718_DropTagsJsonColumn.cs` — new: Up()=DropColumn, Down()=AddColumn
- `src/CookBot.Infrastructure/Migrations/20260516041718_DropTagsJsonColumn.Designer.cs` — new: EF snapshot for this migration
- `src/CookBot.Infrastructure/Migrations/CookBotDbContextModelSnapshot.cs` — TagsJson removed from Recipe entity block
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — stale comment references updated
- `tests/CookBot.Tests/Recipes/RecipeTagBackfillTests.cs` — reframed to seed via raw SQL (plan option a)
- `tests/CookBot.Tests/Services/OwnershipTests.cs` — TagsJson = "[]" removed from setup
- `tests/CookBot.Tests/Services/RecipeAccessExtensionsTests.cs` — TagsJson = "[]" removed from 3 setup sites
- `tests/CookBot.Tests/Services/RecipeCookingAiContextTests.cs` — TagsJson = "[]" removed from 4 setup sites

## Decisions Made

**RecipeTagBackfillTests reframing approach (option a):** Kept the test alive by seeding via `ALTER TABLE Recipes ADD COLUMN TagsJson` after `EnsureCreated()`, then `UPDATE` to inject values. This gives the test its own simulated pre-drop DB state. The backfill SQL (`json_each(r.TagsJson)`) and idempotency assertions are preserved unchanged — full regression value retained.

**Unused import removal:** `using System.Text.Json;` removed from RecipeService since `JsonSerializer.Serialize` is no longer called. `JsonRecipeSerializer` is in `CookBot.Application.Recipes` (already imported) and uses its own STJ wrapper internally.

## Deviations from Plan

**1. [Rule 1 - Bug fix] Stale TagsJson comment references in RecipeView.razor**
- **Found during:** Task 1 acceptance criteria grep
- **Issue:** `grep -rE '\.TagsJson' src/ | grep -v Migrations/` returned hits from inline code comments in `RecipeView.razor` lines 20 and 270
- **Fix:** Updated comments to reference legacy column context without naming TagsJson explicitly
- **Files modified:** `src/CookBot.Web/Components/Pages/RecipeView.razor`
- **Verification:** grep returns 0 hits after edit
- **Committed in:** `3372253` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 — comment cleanup to satisfy acceptance criteria grep)
**Impact on plan:** Minor comment update; no logic change. Zero scope creep.

## Issues Encountered

**EF migration from wrong repo root:** First `dotnet ef migrations add` ran from the main repo directory (`/home/noah/.../freeloves-cookbot/`) which does not have the worktree's Task 1 changes. The generator saw no model diff and produced an empty migration. Resolution: ran `dotnet restore` and `dotnet ef` from inside the worktree directory where the Recipe entity changes are applied. The correctly populated migration was then generated.

## Known Stubs

None — plan 11 removes legacy infrastructure; no new features with stub data paths introduced.

## Threat Flags

None — no new network endpoints, auth paths, or trust boundary surfaces introduced. DropColumn is a pure schema cleanup with no security surface.

## Self-Check: PASSED

**Files created:**
- `src/CookBot.Infrastructure/Migrations/20260516041718_DropTagsJsonColumn.cs` — FOUND
- `src/CookBot.Infrastructure/Migrations/20260516041718_DropTagsJsonColumn.Designer.cs` — FOUND
- `.planning/phases/08-format-foundation/08-11-SUMMARY.md` — FOUND (this file)

**Commits exist:**
- `3372253` (Task 1) — FOUND
- `88d4f14` (Task 2) — FOUND

**Acceptance criteria:**
- TagsJson refs in Recipe.cs: 0
- TagsJson refs in RecipeConfiguration.cs: 0
- TagsJson refs in RecipeService.cs: 0
- Relational add count in RecipeService: 2
- Production-code TagsJson refs (src/ ex Migrations): 0
- Migration file count: 1
- DropColumn in Up(): 1
- Targets TagsJson on Recipes: 4 matches
- Snapshot TagsJson refs: 0
- Build: clean (0 warnings, 0 errors)
- Tests (ex RequiresApiKey): 247 passed, 0 failed

## Next Phase Readiness

- CLEAN-02 fully closed — relational RecipeTag is the sole tag store
- D-31 migration #3 of 4 shipped (DropTagsJsonColumn; #4 = AddPantryMatchIndexes shipped in Plan 12)
- D-26 sequencing honored: AddRecipeTagTable + callsite switchover (Plan 08) shipped before DropTagsJsonColumn (Plan 11)
- Phase 8 success criterion #5 (TagsJson half) met
- No blockers for remaining phase plans

---
*Phase: 08-format-foundation*
*Completed: 2026-05-16*
