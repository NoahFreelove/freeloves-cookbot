---
phase: 13-export-interoperability
plan: 02
subsystem: export
tags: [cooklang, recipe-export, snapshot-testing, pure-function]

# Dependency graph
requires:
  - phase: 12-richer-format
    provides: RecipeDocument v4 with IngredientEntry.Substitutions, StepTemperature, TimerEntry, SectionStep/ContentStep polymorphic steps
provides:
  - CooklangRecipeProjector.Project(RecipeDocument) pure static Application fn → .cook string
  - Golden snapshot baseline for Cooklang output shape
  - Full unit test coverage: always-braces, sanitization, sections, timers, equipment, substitution placement
affects:
  - 13-03 (RecipeView export button wiring uses CooklangRecipeProjector.Project)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pure static Application-layer projector with no DI (mirrors FractionFormatter / JsonRecipeSerializer shape)"
    - "StringBuilder line-emission (analog: CookbookPdfService field-walk)"
    - "Cooklang always-braces @name{amount%unit} rule; Sanitize() strips @/#/~ from prose"
    - "Trailing substitution comment block (deterministic order — IngredientEntry order, then Substitutions order)"
    - "Verify.Xunit golden snapshot test with Snapshots/ routing via ModuleInitializer"

key-files:
  created:
    - src/CookBot.Application/Recipes/CooklangRecipeProjector.cs
    - tests/CookBot.Tests/Recipes/CooklangRecipeProjectorTests.cs
    - tests/CookBot.Tests/Snapshots/CooklangRecipeProjectorTests.FullDocument_ProducesExpectedCooklang.verified.txt
  modified: []

key-decisions:
  - "Recipe-level Equipment[] emitted as '-- Equipment: item' comment lines (not >> or inline #cookware) — D8 resolution"
  - "Substitution block always TRAILING (after ingredients section), deterministic order from IngredientEntry.Substitutions"
  - "Sanitize() strips @/#/~ entirely from prose (empty replacement); Cooklang has no escape char — D7"
  - "Ingredient tokens always use braces form @name{amount%unit} regardless of name complexity — §Pitfall 3"

patterns-established:
  - "Pure static Application projector: static class with single Project(RecipeDocument) entry point, no DI"
  - "Cooklang special-char sanitization: strip @/#/~ from ToPlainText output before emission"

requirements-completed: [INTEROP-03, INTEROP-04]

# Metrics
duration: 3min
completed: 2026-06-06
---

# Phase 13 Plan 02: Cooklang Projector Summary

**Pure static CooklangRecipeProjector that emits valid .cook text from RecipeDocument v4, with always-braces ingredient tokens, prose sanitization, and a trailing deterministic substitution comment block.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-06-06T21:53:47Z
- **Completed:** 2026-06-06T21:57:03Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Implemented `CooklangRecipeProjector.cs` as a pure static Application function (no DI, no RecipeService)
- All Cooklang emission rules: `>> metadata`, `-- Equipment:` lines, `== Section ==` headings, `@name{amount%unit}` always-braces, `~{n%unit}` / `~label{n%unit}` timers, `-- temp` / `-- doneness` comments, trailing `-- Substitution (name): note` block
- 12 tests covering: golden snapshot, always-braces, section headings, timer forms, temperature/doneness comments, prose sanitization, equipment-not-inline, substitution placement
- Golden snapshot committed at `tests/CookBot.Tests/Snapshots/CooklangRecipeProjectorTests.FullDocument_ProducesExpectedCooklang.verified.txt`

## Task Commits

1. **Task 1: CooklangRecipeProjector pure projector** - `c4a785a` (feat)
2. **Task 2: Cooklang golden snapshot + sanitization tests** - `155959b` (test)

**Plan metadata:** (this commit) (docs: complete plan)

## Files Created/Modified
- `src/CookBot.Application/Recipes/CooklangRecipeProjector.cs` - Pure static projector: RecipeDocument → .cook string
- `tests/CookBot.Tests/Recipes/CooklangRecipeProjectorTests.cs` - 12 unit + snapshot tests
- `tests/CookBot.Tests/Snapshots/CooklangRecipeProjectorTests.FullDocument_ProducesExpectedCooklang.verified.txt` - Committed golden baseline

## Decisions Made
- Recipe-level `Equipment[]` emitted as `-- Equipment: item` comment lines (one per line), not as a single `>> equipment:` line or inline `#cookware`. Matches D8 resolution from RESEARCH.md Open Q4.
- Substitution comments are grouped in a TRAILING block after the ingredients section, sourced exclusively from `IngredientEntry.Substitutions` in document order — deterministic and snapshot-pinnable.
- `Sanitize()` strips `@`, `#`, `~` entirely (empty replacement). Prose like "Cook @ 350 #1 ~5 min" → "Cook  350 1 5 min" — the simplest safe transform when Cooklang has no escape character.
- `@eggs{4}` form (amount only, no %) when unit is empty string, `@name{}` form when both zero/empty.

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None — no external service configuration required.

## Next Phase Readiness
- `CooklangRecipeProjector.Project(RecipeDocument)` is ready for Plan 03 to wire into RecipeView's `ExportCooklang` handler
- Golden snapshot locked — any future formatting change will surface as a diff
- All 407 non-API-gated tests pass (6 pre-existing API key gated failures unchanged)

## Threat Model Coverage
| Threat ID | Mitigation | Test |
|-----------|-----------|------|
| T-13-03 (prose @/#/~ injection) | `Sanitize()` strips all three chars from `ToPlainText` output | `ProseSanitized` [Fact] |
| T-13-04 (name truncation via bare tokens) | Always-braces `@name{amount%unit}` | `IngredientsAlwaysBraced` [Fact] |

## Self-Check: PASSED

- `[ -f src/CookBot.Application/Recipes/CooklangRecipeProjector.cs ]` → FOUND
- `[ -f tests/CookBot.Tests/Recipes/CooklangRecipeProjectorTests.cs ]` → FOUND
- `[ -f tests/CookBot.Tests/Snapshots/CooklangRecipeProjectorTests.FullDocument_ProducesExpectedCooklang.verified.txt ]` → FOUND
- `git log --oneline | grep "13-02"` → c4a785a feat(13-02) + 155959b test(13-02) found
- `dotnet test --filter "FullyQualifiedName~CooklangRecipeProjector"` → Passed 12/12

---
*Phase: 13-export-interoperability*
*Completed: 2026-06-06*
