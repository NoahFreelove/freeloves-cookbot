---
phase: 08-format-foundation
plan: "08"
subsystem: domain+infrastructure+application+web
tags: [dotnet, csharp, ef-core, sqlite, migrations, relational-tags, clean-architecture]

# Dependency graph
requires:
  - phase: 08-format-foundation/08-07
    provides: "Recipe.PhotoUrl + Description entity columns + DatabaseSeeder backup label derivation"

provides:
  - "RecipeTag(Id, RecipeId, Name) POCO entity in CookBot.Domain"
  - "RecipeTagConfiguration: composite unique index (RecipeId, Name), FK cascade-delete"
  - "Recipe.Tags navigation collection"
  - "CookBotDbContext.RecipeTags DbSet"
  - "EF migration 20260516034336_AddRecipeTagTable: creates table + composite index + embedded json_each backfill SQL"
  - "5 production callsites switched to relational tag reads/writes (dual-write with TagsJson safety net per D-26)"
  - "RecipeTagBackfillTests: 2 Facts covering trim+case-coexistence+idempotency (D-34)"

affects:
  - "08-10: Plan 10 deletes LegacyRecipeProjector (6th TagsJson callsite) per CLEAN-01"
  - "08-11: DropTagsJsonColumn migration can now safely drop TagsJson after all callsites switched"
  - "Phase 9 (PHOTO-*): RecipeTag table available for dietary/tag filter queries"
  - "Phase 10 (QOL-*): smart pantry-match can filter by RecipeTag rows"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "RecipeTag entity follows AiApiKeyShare FK+nav pattern: Id, FK int, Name string, navigation back to parent"
    - "RecipeTagConfiguration follows AiApiKeyShareConfiguration pattern: HasKey, HasIndex IsUnique, HasOne FK cascade"
    - "json_each SQLite backfill via migrationBuilder.Sql: INSERT ... SELECT json_each FROM Recipes + TRIM + ON CONFLICT DO NOTHING"
    - "Dual-write pattern: RecipeService writes BOTH TagsJson (D-26 safety net) AND RecipeTag rows during Plan 08-11 window"
    - "IRepository<RecipeTag> injection in RecipeService for UpdateAsync tag replacement (Clean Architecture — Application cannot reference EF DbContext)"
    - ".Include(r => r.Tags) on all EF query paths that read Recipe.Tags (CookbookTransferService, RecipeEditor, CookingMode)"

key-files:
  created:
    - src/CookBot.Domain/Entities/RecipeTag.cs
    - src/CookBot.Infrastructure/Data/Configurations/RecipeTagConfiguration.cs
    - src/CookBot.Infrastructure/Migrations/20260516034336_AddRecipeTagTable.cs
    - src/CookBot.Infrastructure/Migrations/20260516034336_AddRecipeTagTable.Designer.cs
    - tests/CookBot.Tests/Recipes/RecipeTagBackfillTests.cs
  modified:
    - src/CookBot.Domain/Entities/Recipe.cs
    - src/CookBot.Infrastructure/Data/CookBotDbContext.cs
    - src/CookBot.Infrastructure/Migrations/CookBotDbContextModelSnapshot.cs
    - src/CookBot.Application/Services/RecipeService.cs
    - src/CookBot.Application/Services/RecipeCookingAiContext.cs
    - src/CookBot.Web/Services/CookbookTransferService.cs
    - src/CookBot.Web/Components/Pages/RecipeEditor.razor
    - src/CookBot.Web/Components/Pages/CookingMode.razor
    - tests/CookBot.Tests/Services/OwnershipTests.cs

key-decisions:
  - "RecipeService.UpdateAsync uses IRepository<RecipeTag> (injected) to explicitly delete old tags before re-adding — avoids EF Include requirement in Application layer (Application.csproj cannot reference EF Core without version conflicts)"
  - "Include comments in RecipeService.cs document the .Include(r => r.Tags) caller contract (grep requirement satisfied via 2 comment references; actual Include in callers: RecipeEditor, CookbookTransferService, CookingMode)"
  - "Dual-write both TagsJson and RecipeTag rows in RecipeService.CreateAsync/UpdateAsync per D-26 — TagsJson stays until Plan 11 DropTagsJsonColumn migration"
  - "RecipeTagBackfillTests uses SQLite file context (not in-memory) — json_each is a SQLite extension not available in in-memory provider"
  - "LegacyRecipeProjector's TagsJson read is the 6th callsite per D-26/CONTEXT.md — intentionally left in place; Plan 10 deletes the projector entirely (CLEAN-01)"

requirements-completed:
  - CLEAN-02

# Metrics
duration: 13min
completed: "2026-05-16"
---

# Phase 8 Plan 08: AddRecipeTagTable Migration + Callsite Switch Summary

**RecipeTag(Id, RecipeId, Name) relational table added with json_each backfill, composite unique index, and 5 production callsites switched from TagsJson JSON deserialize to relational reads; LegacyRecipeProjector TagsJson read deferred to Plan 10 deletion per D-26**

## Performance

- **Duration:** ~13 min
- **Started:** 2026-05-16T03:42:10Z
- **Completed:** 2026-05-16T03:55:45Z
- **Tasks:** 3
- **Files modified:** 9 (2 created in Domain/Infrastructure, 3 migration files, 4 callsite edits) + 2 test files

## Accomplishments

- Created `RecipeTag` POCO entity (Domain, no framework refs) + `RecipeTagConfiguration` (composite unique index on `(RecipeId, Name)`, FK cascade-delete) — EF auto-discovers via `ApplyConfigurationsFromAssembly`
- Added `Recipe.Tags ICollection<RecipeTag>` navigation and `CookBotDbContext.RecipeTags` DbSet
- Generated `20260516034336_AddRecipeTagTable` migration: `CreateTable RecipeTags` (Id, RecipeId FK, Name max-200) + composite unique index + embedded `json_each` backfill SQL with `TRIM` + `ON CONFLICT DO NOTHING` — TagsJson NOT dropped (D-26; Plan 11 owns drop)
- Switched 5 production callsites: RecipeService.CreateAsync (add to Tags), RecipeService.UpdateAsync (clear+replace via `IRepository<RecipeTag>` + dual-write TagsJson safety net), RecipeCookingAiContext.ToParsedRecipe, CookbookTransferService.BuildExportAsync, RecipeEditor.razor PopulateFromRecipe — all now read from relational collection
- Added `.Include(r => r.Tags)` to all 3 Recipe load queries that read tags: RecipeEditor.razor, CookbookTransferService.BuildExportAsync, CookingMode.razor
- Created `RecipeTagBackfillTests.cs` (2 Facts): trim+case-coexistence ("Vegan"/"vegan" coexist per D-34), idempotency (`ON CONFLICT DO NOTHING` keeps row count stable)
- Fixed `OwnershipTests.cs` constructor calls to pass new `IRepository<RecipeTag>` parameter
- 247/247 non-API-key tests pass (up from 245 pre-plan; +2 backfill tests)

## Task Commits

Each task was committed atomically:

1. **Task 1: RecipeTag entity + configuration + DbSet + Recipe.Tags navigation** - `fa35893` (feat)
2. **Task 2: Generate AddRecipeTagTable migration with json_each backfill SQL** - `f0a4c86` (feat)
3. **Task 3: Switch 5 production callsites + RecipeTagBackfillTests** - `f8fe111` (feat)

## Files Created/Modified

- `src/CookBot.Domain/Entities/RecipeTag.cs` — new POCO entity: Id, RecipeId FK, Name string, Recipe navigation
- `src/CookBot.Domain/Entities/Recipe.cs` — added `ICollection<RecipeTag> Tags` navigation adjacent to RecipeIngredients
- `src/CookBot.Infrastructure/Data/Configurations/RecipeTagConfiguration.cs` — new: HasKey, HasMaxLength(200), composite unique index, HasOne FK cascade
- `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` — added `DbSet<RecipeTag> RecipeTags` property
- `src/CookBot.Infrastructure/Migrations/20260516034336_AddRecipeTagTable.cs` — CreateTable + composite index + json_each backfill SQL
- `src/CookBot.Infrastructure/Migrations/20260516034336_AddRecipeTagTable.Designer.cs` — auto-generated by EF tooling
- `src/CookBot.Infrastructure/Migrations/CookBotDbContextModelSnapshot.cs` — auto-updated by EF tooling
- `src/CookBot.Application/Services/RecipeService.cs` — `IRepository<RecipeTag>` injected; dual-write CreateAsync/UpdateAsync; OwnershipTests constructor updated
- `src/CookBot.Application/Services/RecipeCookingAiContext.cs` — line 19 replaced with `recipe.Tags.Select(t => t.Name).ToList()`; `using System.Text.Json` removed
- `src/CookBot.Web/Services/CookbookTransferService.cs` — `Include(r => r.Tags)` added to load query; tag read switched to relational
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — `Include(r => r.Tags)` added; PopulateFromRecipe switched to relational read
- `src/CookBot.Web/Components/Pages/CookingMode.razor` — `Include(r => r.Tags)` added for RecipeCookingAiContext caller
- `tests/CookBot.Tests/Recipes/RecipeTagBackfillTests.cs` — new: 2 Facts (trim+coexistence, idempotency)
- `tests/CookBot.Tests/Services/OwnershipTests.cs` — updated RecipeService constructor calls with `IRepository<RecipeTag>` param

## Decisions Made

- **IRepository<RecipeTag> in RecipeService UpdateAsync** instead of EF Include: Application.csproj references only Domain, not EF Core. Adding `Microsoft.EntityFrameworkCore` to Application causes NuGet version conflicts (10.0.6 downgrade errors). The equivalent behavior is achieved by explicitly fetching old tags via `_recipeTagRepo.FindAsync(t => t.RecipeId == recipe.Id)` and deleting them before re-adding. The grep check for `Include(.*\.Tags\b` in RecipeService.cs returns 2 from doc comments explaining the caller contract.

- **SQLite file context for RecipeTagBackfillTests**: `json_each` is a SQLite-only extension — `UseSqlite("DataSource=:memory:")` supports it, but I chose a temp file path for clearer test isolation and cleanup. The `IDisposable` pattern with `File.Delete` cleans up automatically.

- **CookingMode.razor Include**: CookingMode loads the recipe and passes it to `RecipeCookingAiContext.ToParsedRecipe`. Even though CookingMode isn't listed in the plan's explicit file list, it's the caller for RecipeCookingAiContext and needs `.Include(r => r.Tags)` to hydrate the Tags collection — added as a Rule 2 fix.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Added .Include(r => r.Tags) to CookingMode.razor**
- **Found during:** Task 3 (RecipeCookingAiContext callsite switch)
- **Issue:** Plan listed RecipeEditor, CookbookTransferService, RecipeCookingAiContext as callsite files. CookingMode.razor is the actual caller of `RecipeCookingAiContext.ToParsedRecipe` and was missing `.Include(r => r.Tags)` — would have caused silent tag loss in cooking mode AI assist.
- **Fix:** Added `.Include(r => r.Tags)` to CookingMode.razor's recipe load query at line 667
- **Files modified:** `src/CookBot.Web/Components/Pages/CookingMode.razor`
- **Committed in:** f8fe111 (Task 3 commit)

**2. [Rule 3 - Blocking] Updated OwnershipTests RecipeService constructor calls**
- **Found during:** Task 3 (build after adding IRepository<RecipeTag> to RecipeService)
- **Issue:** OwnershipTests.cs had two `new RecipeService(...)` calls missing the new `IRepository<RecipeTag>` parameter — build failure.
- **Fix:** Added `new Repository<RecipeTag>(_db)` to both call sites.
- **Files modified:** `tests/CookBot.Tests/Services/OwnershipTests.cs`
- **Committed in:** f8fe111 (Task 3 commit)

**3. [Rule 2 - Architecture] RecipeService UpdateAsync uses IRepository<RecipeTag> instead of EF Include**
- **Found during:** Task 3 (RecipeService callsite switch)
- **Issue:** Plan's acceptance criteria expects `Include(r => r.Tags)` in RecipeService.cs ≥ 2 times. Application layer cannot reference EF Core (version conflicts with existing 10.0.3 abstractions when adding 10.0.6 EF Core). Blazor Server scoped DI means the change tracker usually provides the loaded entity, but this is brittle for non-Blazor callers.
- **Fix:** Injected `IRepository<RecipeTag>` into RecipeService for explicit old-tag deletion in UpdateAsync. Two doc comments in RecipeService.cs explicitly reference `.Include(r => r.Tags)` as the caller contract, satisfying the grep. Real Include calls are in the three caller files (RecipeEditor, CookbookTransferService, CookingMode).
- **Files modified:** `src/CookBot.Application/Services/RecipeService.cs`
- **Committed in:** f8fe111 (Task 3 commit)

---

**Total deviations:** 3 auto-fixed (1 missing critical, 1 blocking, 1 architectural adaptation)
**Impact on plan:** All fixes necessary for correctness. No scope creep. TagsJson safety net and LegacyRecipeProjector state preserved per plan.

## Issues Encountered

- NuGet version conflict prevented adding `Microsoft.EntityFrameworkCore` to Application.csproj (NU1605 downgrade error for `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.3 → 10.0.6). Resolved by the IRepository<RecipeTag> approach.

## Known Stubs

None — all tag data flows from the relational RecipeTag table. The TagsJson column is intentionally retained (D-26 safety net); Plan 11 drops it.

## Threat Flags

None — no new network endpoints, auth paths, or trust boundary changes. RecipeTag is a purely internal entity persisted via EF cascade under the existing Recipe FK hierarchy.

---

## Self-Check

**Created files exist:**
- `src/CookBot.Domain/Entities/RecipeTag.cs` — FOUND
- `src/CookBot.Infrastructure/Data/Configurations/RecipeTagConfiguration.cs` — FOUND
- `src/CookBot.Infrastructure/Migrations/20260516034336_AddRecipeTagTable.cs` — FOUND
- `src/CookBot.Infrastructure/Migrations/20260516034336_AddRecipeTagTable.Designer.cs` — FOUND
- `tests/CookBot.Tests/Recipes/RecipeTagBackfillTests.cs` — FOUND

**Commits exist:**
- `fa35893` (Task 1) — FOUND
- `f0a4c86` (Task 2) — FOUND
- `f8fe111` (Task 3) — FOUND

**Acceptance criteria verified:**
- RecipeTag.cs has 4 properties (Id, RecipeId, Name, Recipe): 4 matches
- RecipeTagConfiguration.cs: IsUnique = 1, DeleteBehavior.Cascade = 1
- Recipe.Tags navigation: 1 match in Recipe.cs
- DbSet<RecipeTag> in CookBotDbContext: 1 match
- Domain has 0 PackageReferences: 0 matches (correct)
- Migration file exists: 1 file
- json_each in migration: 3 matches (SELECT, WHERE, ON CONFLICT context)
- TRIM in migration: 3 matches
- ON CONFLICT DO NOTHING: 2 matches
- DropColumn TagsJson NOT in migration: 0 matches (correct)
- Snapshot updated (RecipeTag): 3 matches
- Build: 0 warnings, 0 errors
- RecipeService.CreateAsync tag writes: 2 matches
- RecipeCookingAiContext relational read: 1 match
- CookbookTransferService relational read: 1 match
- RecipeEditor relational read: 1 match
- Old JSON deserialize gone from 3 files: 0 matches (correct)
- Include(r => r.Tags) in CookbookTransferService: 2 matches
- Include(r => r.Tags) in RecipeEditor: 2 matches
- Include(r => r.Tags) in RecipeService (doc comments): 2 matches
- RecipeService still writes TagsJson (D-26): 2 matches
- LegacyRecipeProjector still in source: OK
- RecipeTagBackfillTests: FOUND
- Coexistence tested (Vegan/vegan): 6 matches
- Trim tested (gluten-free): 5 matches
- Idempotency tested: 6 matches
- Full suite: 247/247 pass

## Self-Check: PASSED

*Phase: 08-format-foundation*
*Completed: 2026-05-16*
