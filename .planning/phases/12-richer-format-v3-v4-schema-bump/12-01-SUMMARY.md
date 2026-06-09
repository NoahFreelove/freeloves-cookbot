---
phase: 12-richer-format-v3-v4-schema-bump
plan: "01"
subsystem: domain-schema,application-recipes,tests
tags: [schema-bump, upcaster, domain-poco, validator, tdd]
dependency_graph:
  requires: []
  provides: [v4-domain-pocos, migration-v3-v4, recipe-upcaster-chain-v4, validator-provenance-substitutions]
  affects: [RecipeDocument, IngredientEntry, ContentStep, RecipeUpcasterChain, RecipeValidator]
tech_stack:
  added: []
  patterns: [independent-null-guard-upcaster, empty-list-not-null-default, warning-not-error-validator]
key_files:
  created:
    - src/CookBot.Domain/Recipes/IngredientSubstitution.cs
    - src/CookBot.Domain/Recipes/RecipeProvenance.cs
    - src/CookBot.Application/Recipes/Migration_V3_To_V4.cs
    - tests/CookBot.Tests/Recipes/Migration_V3_To_V4_Tests.cs
    - tests/CookBot.Tests/Recipes/Migration_V3_To_V4_ChainTests.cs
    - tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-no-fields.json
    - tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-all-present.json
    - tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-substitutions-only.json
    - tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-equipment-only.json
    - tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-doneness-only.json
    - tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-provenance-only.json
  modified:
    - src/CookBot.Domain/Recipes/RecipeDocument.cs
    - src/CookBot.Domain/Recipes/IngredientEntry.cs
    - src/CookBot.Domain/Recipes/StepNode.cs
    - src/CookBot.Application/Recipes/RecipeUpcasterChain.cs
    - src/CookBot.Application/DependencyInjection.cs
    - src/CookBot.Application/Recipes/RecipeValidator.cs
    - tests/CookBot.Tests/Recipes/RecipeUpcasterTests.cs
    - tests/CookBot.Tests/Recipes/Migration_V2_To_V3_ChainTests.cs
decisions:
  - "Migration_V3_To_V4 follows exact V2→V3 pattern with four independent null-guard no-ops (D-12-12, P2)"
  - "RecipeValidator scheme check inline (no constructor dep on RecipePhotoUrlValidator) per RESEARCH open question #1"
  - "Migration_V2_To_V3_ChainTests.CurrentVersion_IsThree updated to expect 4 (Rule 1 fix — stale assertion)"
metrics:
  duration_minutes: 10
  completed_date: "2026-06-06"
  tasks_completed: 3
  tasks_total: 3
  files_created: 11
  files_modified: 8
---

# Phase 12 Plan 01: v3→v4 Schema Bump — Summary

**One-liner:** v3→v4 schema bump adding equipment list, RecipeProvenance record, per-ingredient IngredientSubstitution list, and per-step DonenessCue to the canonical Domain POCOs, with Migration_V3_To_V4 upcaster atomically registered in DI, CurrentVersion bumped to 4, validator warnings for invalid provenance URL and empty substitutions, and a 25-test fixture matrix proving SC1 + SC4.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add four v4 field groups to Domain POCOs | 65c84f3 | IngredientSubstitution.cs (new), RecipeProvenance.cs (new), RecipeDocument.cs, IngredientEntry.cs, StepNode.cs |
| 2 | Ship Migration_V3_To_V4 + DI registration + CurrentVersion bump atomically (P1) | c22fe9c | Migration_V3_To_V4.cs (new), RecipeUpcasterChain.cs, DependencyInjection.cs, Migration_V2_To_V3_ChainTests.cs |
| 3 | Validator warnings + full upcaster/chain/fixture test matrix | 6ef81ee | RecipeValidator.cs, 6 fixtures (new), Migration_V3_To_V4_Tests.cs (new), Migration_V3_To_V4_ChainTests.cs (new), RecipeUpcasterTests.cs |

## Verification

- `dotnet build src/CookBot.Domain`: 0 errors
- `dotnet build src/CookBot.Application`: 0 errors
- `dotnet test --filter "Category!=RequiresApiKey"`: 360 passed (335 existing + 25 new), 0 failed
- SC1 proven: all 6 v3-to-v4 fixtures upcast to version=4 with no throw, four independent guards (P2)
- SC4 proven: CurrentVersion=4, Migration_V3_To_V4 registered in DI, v3→v4 gap-detection test present and passing

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Migration_V2_To_V3_ChainTests.RecipeUpcasterChain_CurrentVersion_IsThree stale assertion**
- **Found during:** Task 2
- **Issue:** `RecipeUpcasterChain_CurrentVersion_IsThree` asserted `CurrentVersion == 3`, which fails once CurrentVersion is bumped to 4.
- **Fix:** Updated assertion to expect 4; kept the method name for historical traceability; added clarifying comment.
- **Files modified:** `tests/CookBot.Tests/Recipes/Migration_V2_To_V3_ChainTests.cs`
- **Commit:** c22fe9c

## Known Stubs

None — all four field groups are fully wired on the Domain POCOs and upcaster. No placeholder values.

## Threat Flags

No new security surface beyond what the threat model covered. All three mitigations applied:
- T-12-01: `DetectInvalidProvenanceUrl` inline http/https allowlist in RecipeValidator
- T-12-02: `[MaxLength]` on all new string fields (Note 512, Name 256, SourceUrl 2048, AuthorName 256, SourceName 512, DonenessCue 512)
- T-12-03: Four independent null-guards in Migration_V3_To_V4 (P2); fixture matrix proves no bundle-throw

## Self-Check: PASSED

Files confirmed present:
- src/CookBot.Domain/Recipes/IngredientSubstitution.cs — FOUND
- src/CookBot.Domain/Recipes/RecipeProvenance.cs — FOUND
- src/CookBot.Application/Recipes/Migration_V3_To_V4.cs — FOUND
- tests/CookBot.Tests/Recipes/Migration_V3_To_V4_Tests.cs — FOUND
- tests/CookBot.Tests/Recipes/Migration_V3_To_V4_ChainTests.cs — FOUND

Commits confirmed: 65c84f3, c22fe9c, 6ef81ee — all present in git log.
