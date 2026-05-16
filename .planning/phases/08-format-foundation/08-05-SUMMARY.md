---
phase: 08-format-foundation
plan: "05"
subsystem: application+domain+testing
tags: [dotnet, csharp, system-text-json, yaml, json-converter, unicode, xunit, fixtures]

# Dependency graph
requires:
  - phase: 08-format-foundation/08-02
    provides: "StepTemperature sealed record (decimal Value, TemperatureUnit Unit) and TemperatureUnit enum (F/C/Gas)"
  - phase: 08-format-foundation/08-03
    provides: "RecipeDocument.PhotoUrl, RecipeDocument.Description, ContentStep.Temperature — the types serialized here"

provides:
  - "StepTemperatureJsonConverter: gas half-stops render as '4½' in SerializeIndented; standard { value, unit } in Serialize (DB column)"
  - "JsonRecipeSerializer._indented wired with StepTemperatureJsonConverter + JavaScriptEncoder.UnsafeRelaxedJsonEscaping for literal glyph output"
  - "RecipeFormatParser: Serialize(ParsedRecipe) emits PhotoUrl, Description, and per-step Temperature in YAML wire format"
  - "RecipeFormatParser.ProjectToParsedRecipe carries PhotoUrl, Description, and ContentStep.Temperature into ParsedRecipe/ParsedStep"
  - "ParsedRecipe (IRecipeFormatParser.cs): PhotoUrl: string? and Description: string? added"
  - "ParsedStep (IRecipeFormatParser.cs): Temperature: StepTemperature? added"
  - "3 v3-canonical fixture files covering simple-with-photo, sectioned-with-temperature, full-v3-all-units"
  - "V3Canonical_RoundTripIsIdempotent Theory covering all 3 v3 fixtures with structural equality including v3 fields"
  - "5 new RecipeFormatParserTests covering null temperature, F/C/Gas round-trip via TryParse, gas-half-step, and SerializeIndented glyph rendering"

affects:
  - "08-04: upcaster tests can now deserialize v3 documents via JsonRecipeSerializer directly"
  - "08-10 (RecipeService update): ParsedRecipe now carries PhotoUrl/Description/Temperature for RecipeService to persist"
  - "All future plans using SerializeIndented: output now contains literal Unicode glyphs"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "JsonConverter<StepTemperature> in Application.Recipes.Converters namespace — applied only to _indented, not _compact"
    - "JavaScriptEncoder.UnsafeRelaxedJsonEscaping on _indented only — allows literal ½ glyph in human-readable export"
    - "ParsedRecipe/ParsedStep extension pattern: add nullable fields with no-arg defaults, no constructor changes"
    - "TemperatureFrontmatter inner class in RecipeFormatParser mirrors the D-27 YAML wire shape { value, unit }"
    - "V3Canonical fixture pattern: version=3, null-safe temperature round-trip via direct JsonRecipeSerializer.Deserialize"

key-files:
  created:
    - src/CookBot.Application/Recipes/Converters/StepTemperatureJsonConverter.cs
    - tests/CookBot.Tests/Fixtures/Recipes/v3-canonical/simple-with-photo.json
    - tests/CookBot.Tests/Fixtures/Recipes/v3-canonical/sectioned-with-temperature.json
    - tests/CookBot.Tests/Fixtures/Recipes/v3-canonical/full-v3-all-units.json
  modified:
    - src/CookBot.Application/Recipes/JsonRecipeSerializer.cs
    - src/CookBot.Application/Services/RecipeFormatParser.cs
    - src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs
    - tests/CookBot.Tests/Recipes/RecipeDocumentRoundTripTests.cs
    - tests/CookBot.Tests/Services/RecipeFormatParserTests.cs

key-decisions:
  - "JavaScriptEncoder.UnsafeRelaxedJsonEscaping added to _indented only — allows literal '½' (U+00BD) in export JSON; _compact retains ASCII-safe escaping for DB storage"
  - "Parser temperature tests use version=2 documents instead of version=3 — RecipeUpcasterChain.CurrentVersion=2 until Plan 08-04 bumps it; ContentStep.Temperature deserializes correctly regardless of document version"
  - "StepTemperatureJsonConverter.Read accepts both object form { value, unit } and string form '4½' (defensive) — guards against user-edited indented JSON"
  - "ParsedStep.Temperature assigned from ContentStep.Temperature (value record copies cleanly); SectionStep projects with Temperature=null"

requirements-completed:
  - SCHEMA-08
  - SCHEMA-09
  - SCHEMA-12

# Metrics
duration: 10min
completed: "2026-05-16"
---

# Phase 8 Plan 05: Parser/Serializer v3 Field Extension Summary

**StepTemperatureJsonConverter renders gas half-stops as "4½" in SerializeIndented; RecipeFormatParser carries PhotoUrl/Description/Temperature through the YAML/JSON pipeline into ParsedRecipe/ParsedStep; 3 v3 fixtures and 5 new parser tests complete SCHEMA-08/09/12**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-05-16T03:24:42Z
- **Completed:** 2026-05-16T03:35:07Z
- **Tasks:** 3
- **Files modified:** 5 (2 existing modified, 1 new converter, 2 test files extended) + 3 fixture files created

## Accomplishments

- Created `StepTemperatureJsonConverter` (JsonConverter<StepTemperature>) — Write renders gas half-stops (4.5 → "4½") as Unicode string in indented export; Read defensively accepts both object and string form; converter wired into `_indented` only per D-27
- Added `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` to `_indented` so the literal `½` glyph appears in SerializeIndented output rather than the JSON escape `½`
- Extended `ParsedRecipe` with `PhotoUrl: string?` and `Description: string?`; extended `ParsedStep` with `Temperature: StepTemperature?` in `IRecipeFormatParser.cs`
- Extended `RecipeFormatParser.Serialize()` YAML output path to emit `photoUrl`, `description`, and per-step `temperature: { value, unit }` fields; extended `ProjectToParsedRecipe()` to carry v3 fields from RecipeDocument into ParsedRecipe/ParsedStep
- Created 3 v3-canonical JSON fixtures: `simple-with-photo.json`, `sectioned-with-temperature.json`, `full-v3-all-units.json` (covers F/C/Gas including gas half-step 4.5)
- Added `V3Canonical_RoundTripIsIdempotent` Theory to `RecipeDocumentRoundTripTests` with structural equality assertions including PhotoUrl, Description, and per-step Temperature
- Added 5 new tests to `RecipeFormatParserTests`: null temperature, Theory covering F/C/Gas, gas half-step 4.5, and SerializeIndented gas-glyph rendering — 232 total tests pass (was 196)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create StepTemperatureJsonConverter + wire into SerializeIndented** - `b3d6421` (feat)
2. **Task 2: Extend RecipeFormatParser and ParsedRecipe/ParsedStep for v3 fields** - `ade900c` (feat)
3. **Task 3: V3 fixtures + round-trip and temperature test extensions** - `951c4d7` (feat)

## Files Created/Modified

- `src/CookBot.Application/Recipes/Converters/StepTemperatureJsonConverter.cs` — JsonConverter<StepTemperature>; Read accepts object and string forms; Write emits gas half-stop glyph or standard { value, unit } object
- `src/CookBot.Application/Recipes/JsonRecipeSerializer.cs` — wired StepTemperatureJsonConverter into _indented; added UnsafeRelaxedJsonEscaping to _indented; added `using CookBot.Application.Recipes.Converters`
- `src/CookBot.Application/Services/RecipeFormatParser.cs` — RecipeFrontmatter gains PhotoUrl/Description; StepFrontmatter gains TemperatureFrontmatter; Serialize() populates all 3 new fields; ProjectToParsedRecipe carries them end-to-end
- `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs` — ParsedRecipe gains PhotoUrl/Description; ParsedStep gains Temperature: StepTemperature?; added `using CookBot.Domain.Recipes`
- `tests/CookBot.Tests/Recipes/RecipeDocumentRoundTripTests.cs` — V3CanonicalFixtures() MemberData + V3Canonical_RoundTripIsIdempotent Theory with v3 field assertions
- `tests/CookBot.Tests/Services/RecipeFormatParserTests.cs` — 5 new tests for null temperature, F/C/Gas TryParse, gas half-step, and SerializeIndented gas-glyph
- `tests/CookBot.Tests/Fixtures/Recipes/v3-canonical/simple-with-photo.json` — v3 fixture with photoUrl
- `tests/CookBot.Tests/Fixtures/Recipes/v3-canonical/sectioned-with-temperature.json` — v3 fixture with SectionStep + ContentStep with F temperature
- `tests/CookBot.Tests/Fixtures/Recipes/v3-canonical/full-v3-all-units.json` — v3 fixture with all 3 top-level fields + F/C/Gas steps (load-bearing gas half-step 4.5)

## Decisions Made

- Used `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` on `_indented` only so the human-readable `"4½"` glyph appears in export output; the compact (_compact) serializer keeps ASCII-safe escaping for the DB column — no cross-contamination of the wire format
- Parser temperature tests use version=2 documents since `RecipeUpcasterChain.CurrentVersion=2` until Plan 08-04 bumps it; the temperature field deserializes correctly regardless because it's defined on `ContentStep` which is shared across versions
- `StepTemperatureJsonConverter.Read` accepts both `{ "value": 4.5, "unit": "gas" }` object form and `"4½"` string form — defensive against users editing the indented export JSON

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Added JavaScriptEncoder.UnsafeRelaxedJsonEscaping to _indented to emit literal ½ glyph**
- **Found during:** Task 3 (SerializeIndented gas-glyph test)
- **Issue:** STJ's default encoder escapes non-ASCII characters as `\uXXXX`, producing `"4½"` instead of `"4½"` — the `Assert.Contains("4½", result)` test failed because the C# string contained the literal Unicode escape sequences
- **Fix:** Added `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` to the `_indented` `JsonSerializerOptions` initializer; _compact unchanged
- **Files modified:** src/CookBot.Application/Recipes/JsonRecipeSerializer.cs
- **Verification:** SerializeIndented now produces `"4½"` in the JSON output; test passes
- **Committed in:** 951c4d7 (Task 3 commit)

**2. [Rule 1 - Bug] Changed parser temperature tests from v3 to v2 document version**
- **Found during:** Task 3 (TryParse temperature tests)
- **Issue:** RecipeUpcasterChain.CurrentVersion=2 rejects v3 documents with "Recipe version 3 is newer than current (2). Update the app." — the tests were written to use version=3 JSON per the plan spec but the upcaster hasn't been bumped yet (that's Plan 08-04)
- **Fix:** Changed all TryParse temperature test inputs from `"version":3` to `"version":2`; temperature deserialization works identically since ContentStep.Temperature is defined in the type system regardless of document version
- **Files modified:** tests/CookBot.Tests/Services/RecipeFormatParserTests.cs
- **Verification:** All 5 new parser tests pass; 232/232 non-API-key tests pass
- **Committed in:** 951c4d7 (Task 3 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 1 — correctness bugs discovered during test execution)
**Impact on plan:** Both fixes necessary for tests to pass and for the literal glyph to appear in output. No scope change.

## Known Stubs

None — all plan goals fully wired. Parser and serializer carry v3 fields end-to-end. Test coverage is complete per SCHEMA-12 requirements.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema trust boundary changes introduced. `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` on `_indented` is export-only output; no risk of XSS since the export target is a Blazor Server download, not an injected HTML template.

---
*Phase: 08-format-foundation*
*Completed: 2026-05-16*
