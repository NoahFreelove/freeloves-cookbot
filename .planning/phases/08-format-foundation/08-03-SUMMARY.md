---
phase: 08-format-foundation
plan: "03"
subsystem: domain+application
tags: [dotnet, csharp, system-text-json, nullable, validation, xunit, schema-green-gate]

# Dependency graph
requires:
  - phase: 08-format-foundation/08-01
    provides: "SchemaAssertionTests RED gate — turned GREEN by this plan"
  - phase: 08-format-foundation/08-02
    provides: "StepTemperature record (decimal Value, TemperatureUnit Unit) — wired into ContentStep here"

provides:
  - "RecipeDocument.PhotoUrl: string? (JsonPropertyName photoUrl, MaxLength 2048)"
  - "RecipeDocument.Description: string? (JsonPropertyName description, MaxLength 4096)"
  - "ContentStep.Temperature: StepTemperature? (JsonPropertyName temperature)"
  - "RecipeValidator: INVALID_TEMPERATURE error per D-27 (F/C whole-degree; Gas 0.5-step in [1.0, 9.5])"
  - "RecipeValidatorTests: 10-row Theory + 1 Fact covering all temperature validation cases"
  - "All 3 SchemaAssertionTests now GREEN — SCHEMA-07 holds by construction"

affects:
  - "08-04: upcaster must write Version=3 into upcasted documents; nullable fields are already shaped correctly"
  - "08-05: parser/serializer round-trip tests can add temperature fixtures without breakage"
  - "08-06: denylist can reference TemperatureUnit.Gas in rule matching"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "System.ComponentModel.DataAnnotations.MaxLength on domain POCO properties — JsonSchemaExporter surfaces maxLength to Anthropic structured-output schema"
    - "Pattern S5/S6 preserved: sealed record, file-scoped namespace, STJ attributes only in CookBot.Domain"
    - "Temperature guard: content.Temperature is { } temp pattern-match before switch(temp.Unit)"
    - "xUnit Theory with double args cast to decimal inside test body (same pattern as StepTemperatureTests)"

key-files:
  created: []
  modified:
    - src/CookBot.Domain/Recipes/RecipeDocument.cs
    - src/CookBot.Domain/Recipes/StepNode.cs
    - src/CookBot.Application/Recipes/RecipeValidator.cs
    - tests/CookBot.Tests/Recipes/RecipeValidatorTests.cs

key-decisions:
  - "MaxLength attributes added to RecipeDocument nullable string fields — planner discretion per D-28; JsonSchemaExporter honors MaxLength and propagates maxLength:N to the AI schema without any manual editing"
  - "Temperature guard inserted in ContentStep branch AFTER the markdown-link loop (follows PATTERNS.md insertion point exactly)"
  - "Temperature == null is explicitly valid — no error emitted — per PITFALLS C7/M2 null-fill policy"
  - "Gas 0.5-step check uses modulo arithmetic: temp.Value % 0.5m != 0m; boundary check [1.0, 9.5] uses both < and > comparators per D-27"

requirements-completed:
  - SCHEMA-01
  - SCHEMA-02
  - SCHEMA-06
  - SCHEMA-07

# Metrics
duration: 12min
completed: "2026-05-15"
---

# Phase 8 Plan 03: v3 Nullable Fields + Temperature Validation Summary

**PhotoUrl, Description, and ContentStep.Temperature wired into the domain; RecipeValidator extended with D-27 temperature rules; all 3 Plan 01 SchemaAssertionTests turn GREEN**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-05-15T18:19:00Z
- **Completed:** 2026-05-15T18:31:00Z
- **Tasks:** 3
- **Files modified:** 4

## Accomplishments

- Added `string? PhotoUrl` (MaxLength 2048) and `string? Description` (MaxLength 4096) to `RecipeDocument` after `CookTimeMinutes` per PATTERNS.md insertion point; `using System.ComponentModel.DataAnnotations` added for MaxLength
- Added `StepTemperature? Temperature` to `ContentStep` between `Timers` and `Extras`; `SectionStep` untouched
- Extended `RecipeValidator.Validate()` with per-unit temperature guard: F/C must be whole-degree; Gas must be 0.5-step multiple in [1.0, 9.5]; uses `INVALID_TEMPERATURE` error code; null temperature is valid
- Extended `RecipeValidatorTests` with `Temperature_Validation_PerUnitRules` Theory (10 rows covering F-valid, F-invalid, C-valid, C-invalid, Gas-valid-whole, Gas-valid-half, Gas-below-range, Gas-at-ceiling, Gas-above-range, Gas-not-half-step) plus `Temperature_NullTemperature_IsValid` Fact
- All 3 `SchemaAssertionTests` (`GetSchema_Includes_PhotoUrl_Description`, `GetSchema_StepTemperature_NullableShape`, `GetSchema_AdditionalPropertiesFalse_OnStepTemperatureSubschema`) turn GREEN — SCHEMA-07 holds by construction because `RecipeJsonSchemaProvider` regenerated automatically against the updated types

## Task Commits

Each task was committed atomically:

1. **Task 1: Add PhotoUrl + Description to RecipeDocument** - `03aec29` (feat)
2. **Task 2: Add Temperature to ContentStep** - `e7b88ae` (feat)
3. **Task 3: Extend RecipeValidator with temperature rules + tests** - `485c144` (feat)

## Files Created/Modified

- `src/CookBot.Domain/Recipes/RecipeDocument.cs` — PhotoUrl (string?, JsonPropertyName "photoUrl", MaxLength 2048) and Description (string?, JsonPropertyName "description", MaxLength 4096) inserted after CookTimeMinutes before Tags; `using System.ComponentModel.DataAnnotations` added
- `src/CookBot.Domain/Recipes/StepNode.cs` — StepTemperature? Temperature (JsonPropertyName "temperature") inserted in ContentStep between Timers and Extras
- `src/CookBot.Application/Recipes/RecipeValidator.cs` — per-unit temperature guard inserted in ContentStep branch after markdown-link loop; INVALID_TEMPERATURE error code for F/C fractional and Gas out-of-range/non-half-step values
- `tests/CookBot.Tests/Recipes/RecipeValidatorTests.cs` — Temperature_Validation_PerUnitRules Theory (10 InlineData rows) + Temperature_NullTemperature_IsValid Fact added; pre-change count 7, now 9 (Fact+Theory)

## Decisions Made

- Used `System.ComponentModel.DataAnnotations.MaxLength` (not a custom attribute) because `JsonSchemaExporter` honors it natively, propagating `maxLength:N` into the AI-facing JSON schema without any manual editing — satisfies SCHEMA-07 by construction
- Gas boundary and 0.5-step check combined into single condition: `temp.Value % 0.5m != 0m || temp.Value < 1.0m || temp.Value > 9.5m` — mirrors PATTERNS.md exactly
- xUnit `Assert.True(result.Errors.Any(...), message)` used instead of `Assert.Contains(..., userMessage:)` — the named `userMessage` param does not exist on `Assert.Contains<T>(IEnumerable<T>, Predicate<T>)`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed invalid named argument `userMessage` on `Assert.Contains`**
- **Found during:** Task 3 (test compile)
- **Issue:** `Assert.Contains(collection, predicate, userMessage: "...")` does not compile — xUnit's `Assert.Contains<T>(IEnumerable<T>, Predicate<T>)` overload has no `userMessage` parameter
- **Fix:** Replaced with `Assert.True(result.Errors.Any(e => e.Code == "INVALID_TEMPERATURE"), $"...")` which accepts a string message as second arg
- **Files modified:** tests/CookBot.Tests/Recipes/RecipeValidatorTests.cs
- **Committed in:** 485c144 (Task 3 commit)

## Known Stubs

None — all plan goals fully wired. PhotoUrl and Description exist on RecipeDocument but are deliberately nullable (v2 documents have them as null until the upcaster writes v3). This is intentional by design, not a stub.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema trust boundary changes introduced.

---
*Phase: 08-format-foundation*
*Completed: 2026-05-15*
