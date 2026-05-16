---
phase: 08-format-foundation
plan: "01"
subsystem: testing
tags: [xunit, json-schema, schema-assertion, h11-audit, schema-v3, tdd-red]

# Dependency graph
requires: []
provides:
  - "SchemaAssertionTests.cs: 3-Fact RED gate asserting photoUrl, description, and ContentStep temperature in RecipeJsonSchemaProvider.GetSchema()"
  - "RecipeFormatParserTests.cs: SCHEMA-12 H11 audit comment confirming all assertions are structural (not string-blob comparisons)"
affects:
  - "08-02: StepTemperature record (must turn GetSchema_StepTemperature_NullableShape GREEN)"
  - "08-03: RecipeDocument v3 (must turn GetSchema_Includes_PhotoUrl_Description GREEN)"
  - "08-05 onward: parser round-trip tests must stay GREEN throughout v3 additions"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Schema contract gate pattern: write RED schema-assertion tests FIRST before any production code changes (D-35/SCHEMA-11 ordering)"
    - "H11 audit pattern: zero string-blob assertions in parser tests ensures v3 nullable additions cannot regress existing fixtures"
    - "JsonNode navigation for polymorphic anyOf schema branches: locate ContentStep branch by checking which anyOf entry contains 'text' property, then assert target property"

key-files:
  created:
    - tests/CookBot.Tests/Recipes/SchemaAssertionTests.cs
  modified:
    - tests/CookBot.Tests/Services/RecipeFormatParserTests.cs

key-decisions:
  - "3 separate [Fact] methods (not 1 combined) per plan behavior section and acceptance criteria — each failure message is isolated for diagnosability"
  - "FindContentStepProperties helper navigates anyOf by checking for 'text' property (the ContentStep discriminator) rather than 'kind':'content' const (which is an extra branch property, not reliable for type discrimination in navigation)"
  - "SCHEMA-12 audit result: ZERO string-blob assertions found in RecipeFormatParserTests — header comment documents this finding per H11 step 4 protocol"

patterns-established:
  - "Schema-assertion-first: SchemaAssertionTests.cs is the gate that Plans 02-03 must turn GREEN; no new v3 field ships until this test exists"
  - "H11 prevention: RecipeFormatParserTests confirmed structural-only — WhenWritingNull on JsonRecipeSerializer._compact means v3 nullable fields won't appear in serialized output"

requirements-completed:
  - SCHEMA-11
  - SCHEMA-12

# Metrics
duration: 15min
completed: "2026-05-16"
---

# Phase 8 Plan 01: Schema Assertion Gate & Parser Audit Summary

**RED gate tests for v3 schema contract (photoUrl, description, ContentStep temperature) committed first; RecipeFormatParserTests confirmed structural-only via SCHEMA-12 H11 audit**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-05-16T03:00:00Z
- **Completed:** 2026-05-16T03:11:49Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- Created `SchemaAssertionTests.cs` with 3 `[Fact]` methods that run RED (intentionally) before Plans 02-03 land: `GetSchema_Includes_PhotoUrl_Description`, `GetSchema_StepTemperature_NullableShape`, `GetSchema_AdditionalPropertiesFalse_OnStepTemperatureSubschema`
- Tests correctly navigate the polymorphic `anyOf` schema for `StepNode` to locate the `ContentStep` branch and fail with precise diagnostic messages naming the missing keys
- Audited `RecipeFormatParserTests.cs` for SCHEMA-12 / H11 string-comparison brittleness: confirmed ZERO string-blob assertions; all 10 tests are structural field comparisons
- Added SCHEMA-12 audit header comment to `RecipeFormatParserTests.cs` documenting H11 prevention work for downstream plans

## Task Commits

Each task was committed atomically:

1. **Task 1: Create SchemaAssertionTests.cs (RED gate)** - `98d79a2` (test)
2. **Task 2: Audit RecipeFormatParserTests for H11 brittleness** - `806ea1c` (refactor)

## Files Created/Modified

- `tests/CookBot.Tests/Recipes/SchemaAssertionTests.cs` - 3-Fact RED schema contract gate per SCHEMA-11 / D-35 ordering
- `tests/CookBot.Tests/Services/RecipeFormatParserTests.cs` - Added SCHEMA-12 H11 audit header comment (no test logic changed)

## Decisions Made

- Used 3 separate `[Fact]` methods rather than 1 combined method — each method provides an isolated failure message that names exactly which field is missing, enabling clearer RED-to-GREEN tracking as Plans 02 and 03 ship
- `FindContentStepProperties` helper locates the ContentStep anyOf branch by checking for the presence of `"text"` in `properties` (the required ContentStep field) rather than by checking `"kind": {"const": "content"}` — this is more robust since the const pattern is a property value, not a branch discriminator in navigation
- SCHEMA-12 audit: found ZERO string-blob assertions in RecipeFormatParserTests — applied Step 4 protocol (header comment only, no test rewrites needed)

## Deviations from Plan

None — plan executed exactly as written.

The one minor clarification: the plan's `must_haves.artifacts[0].contains` field says `GetSchema_Includes_PhotoUrl_Description_StepTemperature` (combined), but the plan's `<behavior>` section and acceptance criteria explicitly list 3 separate test method names. The implementation follows the behavior section and acceptance criteria (3 separate facts), which are the authoritative specification within the plan.

## Issues Encountered

- `dotnet test` commands must be run from the worktree directory (not the main repo) — the worktree has its own checkout of test files and the compiled dll reflects the worktree's content. Running from the main repo silently uses main repo test files (which lack the new `SchemaAssertionTests.cs`).

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- Plans 02 (StepTemperature) and 03 (RecipeDocument v3) now have an executable contract: `SchemaAssertionTests` must turn GREEN (all 3 facts passing) by end of Plan 03
- `RecipeFormatParserTests` is confirmed safe from H11 brittleness — Plans 02-05 can add v3 nullable fields without risking existing parser test regressions
- `JsonRecipeSerializer._compact` confirmed to use `WhenWritingNull` — null v3 fields will not appear in serialized output, so no existing fixtures will change

---
*Phase: 08-format-foundation*
*Completed: 2026-05-16*
