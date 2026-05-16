---
phase: 08-format-foundation
plan: "04"
subsystem: application+testing
tags: [dotnet, csharp, system-text-json, xunit, upcaster, fixture-matrix, tdd]

# Dependency graph
requires:
  - phase: 08-format-foundation/08-03
    provides: "RecipeDocument.PhotoUrl, RecipeDocument.Description, ContentStep.Temperature (StepTemperature?) — the v3 domain fields this upcaster stamps version for"

provides:
  - "Migration_V2_To_V3 : IRecipeUpcaster (FromVersion=2, ToVersion=3) with three independent per-field guards per D-29"
  - "RecipeUpcasterChain.CurrentVersion = 3 (bumped from 2 per D-30)"
  - "DI registration: Migration_V2_To_V3 as IRecipeUpcaster singleton alongside Migration_V1_To_V2"
  - "Fixture matrix: 5 v2-to-v3-*.json fixture files covering no-fields, photo-only, description-only, temperature-only, all-present"
  - "Migration_V2_To_V3_Tests: Theory (5 fixtures) + 3 Facts (no-temperature guard, identity v3, gap detection regression)"

affects:
  - "08-05: parser/serializer round-trip tests will exercise v3 fixtures"
  - "08-06: denylist regex tests depend on currentVersion=3 constant being set"
  - "All plans consuming LegacyRecipeProjector.Project() — it now emits Version=3 (CurrentVersion)"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TDD RED/GREEN: test committed before implementation; RED compile-failed on Migration_V2_To_V3 absence; GREEN passed after creation"
    - "No-op guard contract per D-29: three independent if-null guards as documented contract even though STJ handles actual null-mapping"
    - "MemberData filesystem fixture pattern (Pattern S1): Directory.GetFiles('Fixtures/Recipes/upcaster', 'v2-to-v3-*.json')"

key-files:
  created:
    - src/CookBot.Application/Recipes/Migration_V2_To_V3.cs
    - tests/CookBot.Tests/Recipes/Migration_V2_To_V3_Tests.cs
    - tests/CookBot.Tests/Recipes/Migration_V2_To_V3_ChainTests.cs
    - tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-no-fields.json
    - tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-photo-only.json
    - tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-description-only.json
    - tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-temperature-only.json
    - tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-all-present.json
  modified:
    - src/CookBot.Application/Recipes/RecipeUpcasterChain.cs
    - src/CookBot.Application/DependencyInjection.cs
    - tests/CookBot.Tests/Migration/CanonicalBackfillTests.cs

key-decisions:
  - "Three no-op guards are documented contract per D-29: explicit if-null blocks even though STJ handles absent-key-to-null mapping — defends PITFALLS C7 against future bundled additions"
  - "Temperature guard walks steps[] and checks kind=='content' before inspecting temperature key — SectionSteps never carry temperature (PITFALLS M2 guard)"
  - "CanonicalBackfillTests version assertion updated from hardcoded 2 to RecipeUpcasterChain.CurrentVersion — keeps the assertion semantically correct as version evolves"
  - "Fixture matrix uses JsonNode-level temperature assertions (not typed ContentStep.Temperature) because StepTemperature/ContentStep.Temperature are being added by 08-03 agent in parallel"

requirements-completed:
  - SCHEMA-04
  - SCHEMA-05

# Metrics
duration: 18min
completed: "2026-05-15"
---

# Phase 8 Plan 04: V2->V3 Upcaster Step + Fixture Matrix Summary

**Migration_V2_To_V3 registered in chain with three independent D-29 guards; CurrentVersion bumped to 3; 5-fixture Theory covers all per-field combinations; 208/208 tests green**

## Performance

- **Duration:** ~18 min
- **Started:** 2026-05-15T23:22:00Z
- **Completed:** 2026-05-15T23:40:00Z
- **Tasks:** 2 (+ TDD RED commits)
- **Files modified:** 11

## Accomplishments

- Created `Migration_V2_To_V3 : IRecipeUpcaster` with `FromVersion=2`, `ToVersion=3`; three independent per-field guards (photoUrl, description, per-step temperature) per D-29 contract; stamps `version: 3` on upcasted node
- Bumped `RecipeUpcasterChain.CurrentVersion` from 2 to 3 (single line per PATTERNS.md); chain constructor gap-detection now confirms 1→2→3 at startup
- Added DI registration `services.AddSingleton<IRecipeUpcaster, Migration_V2_To_V3>()` alongside `Migration_V1_To_V2`
- Created 5 fixture JSON files covering all per-field missing/present combinations for the v2→v3 fixture matrix
- Created `Migration_V2_To_V3_Tests` with 8 tests (1 Theory × 5 fixture instances + 3 Facts): fixture matrix, no-temperature M2 guard, identity v3, gap detection regression
- TDD RED/GREEN cycle: `Migration_V2_To_V3_ChainTests` committed as failing (compile error) before implementation; GREEN after implementation

## Task Commits

Each task was committed atomically:

1. **RED: Chain tests (failing)** - `7af5def` (test)
2. **Task 1: Migration_V2_To_V3 + CurrentVersion + DI** - `935db33` (feat)
3. **Task 2: Fixture matrix + Migration_V2_To_V3_Tests** - `10ee420` (feat)

## Files Created/Modified

- `src/CookBot.Application/Recipes/Migration_V2_To_V3.cs` — New upcaster; three independent D-29 no-op guards; stamps version=3
- `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` — `CurrentVersion = 3` (single line)
- `src/CookBot.Application/DependencyInjection.cs` — Added `services.AddSingleton<IRecipeUpcaster, Migration_V2_To_V3>()`
- `tests/CookBot.Tests/Recipes/Migration_V2_To_V3_ChainTests.cs` — RED gate tests (4 assertions; all pass GREEN)
- `tests/CookBot.Tests/Recipes/Migration_V2_To_V3_Tests.cs` — Theory (5 fixtures) + 3 Facts
- `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-no-fields.json` — Minimal v2 doc: no photoUrl, description, temperature
- `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-photo-only.json` — v2 doc with photoUrl pre-present
- `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-description-only.json` — v2 doc with description
- `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-temperature-only.json` — v2 doc with temperature on ContentStep
- `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-all-present.json` — All three fields present
- `tests/CookBot.Tests/Migration/CanonicalBackfillTests.cs` — Fixed hardcoded `Assert.Equal(2, ...)` → `RecipeUpcasterChain.CurrentVersion`

## Decisions Made

- Three no-op `if (obj["X"] is null) { /* ... */ }` guards are written explicitly as documented contract per D-29 — STJ handles the actual null-mapping, but the guards defend PITFALLS C7 (never bundle additions) for future contributors
- Temperature guard in the upcaster walks `steps[] OfType<JsonObject>()` and only checks `kind=="content"` steps — `SectionStep` never carries temperature (PITFALLS M2)
- Fixture matrix uses JsonNode-level temperature absence assertion (`Assert.Null(step["temperature"])`) rather than typed `ContentStep.Temperature` — plan 08-03 adds that property in a parallel worktree; asserting at the JSON node layer works correctly in both states
- `CanonicalBackfillTests`: updated `Assert.Equal(2, roundTripped.Version)` to `Assert.Equal(RecipeUpcasterChain.CurrentVersion, roundTripped.Version)` because `LegacyRecipeProjector` hardcodes `Version = RecipeUpcasterChain.CurrentVersion`; after the bump it now emits version=3

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed hardcoded version==2 assertion in CanonicalBackfillTests**
- **Found during:** Task 2 (full test suite run)
- **Issue:** `CanonicalBackfillTests.Backfill_ThreeRecipes_RoundTripsWithoutValueDrift` asserts `Assert.Equal(2, roundTripped.Version)`. `LegacyRecipeProjector.Project()` sets `Version = RecipeUpcasterChain.CurrentVersion` — after the CurrentVersion bump to 3, the projector now emits version=3, breaking the hardcoded assertion
- **Fix:** Changed `Assert.Equal(2, roundTripped.Version)` to `Assert.Equal(RecipeUpcasterChain.CurrentVersion, roundTripped.Version)` so the assertion tracks the constant semantically
- **Files modified:** `tests/CookBot.Tests/Migration/CanonicalBackfillTests.cs`
- **Verification:** Full suite 208/208 pass
- **Committed in:** `10ee420` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 — bug fix)
**Impact on plan:** Fix necessary for correctness; no scope creep. The hardcoded version was a pre-existing fragility that the CurrentVersion bump exposed.

## Issues Encountered

None — implementation followed PATTERNS.md spec exactly. The xUnit analyzer warning about unused `fixtureName` parameter was resolved inline by using it in the assertion message.

## Known Stubs

None — all plan goals fully wired. The three no-op guards are intentional by design (D-29 documented contract), not stubs.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema trust boundary changes introduced. This plan modifies only the upcaster chain internals and test code.

## Next Phase Readiness

- `Migration_V2_To_V3` is registered and tested; v2 cookbooks imported post-Phase-8 will upcast to v3 cleanly
- Phase 8 Success Criterion #1 (v2 cookbook imports to v3 with no data loss, no throw) is structurally proven via fixture matrix
- D-29's three-independent-guards contract visible in source
- D-30's `CurrentVersion=3` in place
- Ready for 08-05 (parser/serializer round-trip tests for v3 fixtures)

---
*Phase: 08-format-foundation*
*Completed: 2026-05-15*
