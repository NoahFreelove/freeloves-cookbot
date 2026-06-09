---
phase: 12-richer-format-v3-v4-schema-bump
plan: "02"
subsystem: domain-interfaces,application-services,tests
tags: [editor-dtos, round-trip, format-parser, tdd, FORMAT-07]
dependency_graph:
  requires: ["12-01"]
  provides: [editor-dtos-v4, parser-bridge-v4, round-trip-tests-v4]
  affects: [IRecipeFormatParser, RecipeFormatParser, ParsedRecipe, ParsedStep, ParsedIngredient]
tech_stack:
  added: []
  patterns: [tdd-red-green, null-when-empty-serialize-idiom, domain-record-reuse-in-editor-dto]
key_files:
  created:
    - tests/CookBot.Tests/Recipes/RecipeRoundTripTests.cs
  modified:
    - src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs
    - src/CookBot.Application/Services/RecipeFormatParser.cs
decisions:
  - "Domain RecipeProvenance record reused directly on ParsedRecipe — no ParsedProvenance parallel (RESEARCH open question #2 resolution)"
  - "SubstitutionFrontmatter inner class mirrors TimerFrontmatter pattern for YAML serialization"
  - "Equipment serialized null-when-empty matching existing Tags idiom in Serialize path"
  - "TryParse path already works via untyped YAML → JsonNode → RecipeDocument — no changes needed"
metrics:
  duration_minutes: 8
  completed_date: "2026-06-06"
  tasks_completed: 2
  tasks_total: 2
  files_created: 1
  files_modified: 2
---

# Phase 12 Plan 02: Editor DTO Extensions + Round-Trip Guarantee — Summary

**One-liner:** Extended the second parallel shape system (ParsedRecipe/ParsedStep/ParsedIngredient editor DTOs) with all four v4 field groups, wired ProjectToParsedRecipe and Serialize through RecipeFormatParser, and proved SC2 data-layer round-trip with 37 TDD tests covering null/present/edge fixtures.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Extend editor DTOs (ParsedRecipe/ParsedStep/ParsedIngredient) | bc18e7c | IRecipeFormatParser.cs |
| 1 (RED) | Add failing round-trip tests | ebe2a1c | RecipeRoundTripTests.cs (new) |
| 2 (GREEN) | Wire four groups through RecipeFormatParser bridge + Serialize + frontmatter | 96f0731 | RecipeFormatParser.cs |

## Verification

- `dotnet build src/CookBot.Domain`: 0 errors
- `dotnet build src/CookBot.Application`: 0 errors
- `dotnet test --filter "Category!=RequiresApiKey"`: 368 passed (331 existing + 37 new), 0 failed
- `dotnet test --filter "Category!=RequiresApiKey&FullyQualifiedName~RoundTrip"`: 37 passed
- `JsonRecipeSerializer.cs` confirmed unchanged (`git diff --stat` produces no output)
- FORMAT-07 proven: all four groups survive parser bridge + YAML Serialize path
- SC2 (data layer) proven: fully-populated RecipeDocument survives Serialize→Deserialize with field-level equality

## Deviations from Plan

None — plan executed exactly as written. The TryParse/deserialize path required no changes because:
1. `YamlToJsonNode` uses the untyped YamlDotNet deserializer which produces a generic object graph
2. `ConvertGraph` maps it to `JsonNode`
3. `JsonRecipeSerializer.Deserialize(JsonNode)` handles the new fields via `[JsonPropertyName]` attributes already present on Domain POCOs from Plan 01

## Known Stubs

None — all four field groups are fully wired in the editor DTO layer. The `Serialize` YAML path emits them; `TryParse` reads them back; `ProjectToParsedRecipe` bridges them.

## Threat Flags

No new security surface. T-12-04 (field drop / silent data loss when parallel shapes diverge) fully mitigated:
- ProjectToParsedRecipe extended for all four groups
- Serialize extended for all four groups
- Round-trip tests assert field-level equality (SC2)

## Self-Check: PASSED

Files confirmed present:
- src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs — FOUND (modified)
- src/CookBot.Application/Services/RecipeFormatParser.cs — FOUND (modified)
- tests/CookBot.Tests/Recipes/RecipeRoundTripTests.cs — FOUND (created)

Commits confirmed: bc18e7c, ebe2a1c, 96f0731 — all present in git log.
