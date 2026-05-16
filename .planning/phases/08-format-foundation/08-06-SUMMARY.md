---
phase: 08-format-foundation
plan: "06"
subsystem: application+tests
tags: [dotnet, csharp, xunit, denylist, prompt-safety, schema-docs, snapshot]

# Dependency graph
requires:
  - phase: 08-format-foundation/08-03
    provides: "RecipeDocument.PhotoUrl, Description, ContentStep.Temperature — referenced in prose example"

provides:
  - "RecipeSchemaDocumentationProvider: v3 schema example (version:3, photoUrl, description, temperature step)"
  - "PromptDenylistTests: extended regex covering SCHEMA-10 alias tokens (image, imageUrl, picture, summary, desc, temp, oven)"
  - "PromptDenylistTests.Denylist_FiresOn_AliasToken_InSyntheticInput: self-check negative-path Fact"
  - "expected-system-prompt.txt: updated snapshot for v3 schema example"

affects:
  - "08-07+: any future plan touching prompt source files must not introduce alias tokens from the SCHEMA-10 list"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Word-boundary regex \b...\b with IgnoreCase for alias-token scanning (existing D-22 pattern extended per D-36)"
    - "internal static field visibility for self-check Fact (same-assembly access without reflection)"
    - "UPDATE_SNAPSHOTS=1 env-var pattern for snapshot regeneration (D-21 hand-rolled snapshot)"

key-files:
  created: []
  modified:
    - src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs
    - src/CookBot.Application/Services/PromptBuilderService.cs
    - tests/CookBot.Tests/Prompts/PromptDenylistTests.cs
    - tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt

key-decisions:
  - "Replaced XML doc <summary> tags with // comments in both scanned source files to avoid \bsummary\b false-positive (Rule 1 auto-fix: plan didn't anticipate XML doc tag collision)"
  - "Changed 'oven temperatures/temps' to 'baking temperatures/temps' in PromptBuilderService.cs to avoid \boven\b false-positive on legitimate prompt prose (Rule 1 auto-fix)"
  - "Made Denylist internal static (not private) to enable same-assembly Fact test without reflection"
  - "Updated expected-system-prompt.txt snapshot (Rule 3 auto-fix: failing PromptSnapshotTests blocking Task 2 verification)"

requirements-completed:
  - SCHEMA-10

# Metrics
duration: 9min
completed: "2026-05-16"
---

# Phase 8 Plan 06: SCHEMA-10 Denylist Extension + v3 Schema Docs Summary

**SCHEMA-10 denylist extended to catch AI-emitted alias tokens for photoUrl/description/temperature; RecipeSchemaDocumentationProvider example bumped to v3 with canonical field names; self-check negative-path Fact proves the regex fires**

## Performance

- **Duration:** ~9 min
- **Started:** 2026-05-16T03:24:55Z
- **Completed:** 2026-05-16T03:33:15Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- Updated `RecipeSchemaDocumentationProvider.FormatPrompt` JSON example: bumped version to 3, added `"photoUrl"` and `"description"` top-level fields, extended the bake step with `"temperature": { "value": 375, "unit": "F" }`, added Temperature prose note alongside Timers note
- Extended `PromptDenylistTests.Denylist` regex with seven SCHEMA-10 alias tokens: `image|imageUrl|picture|summary|desc|temp|oven`; made field `internal static` for same-assembly Fact access
- Added `Denylist_FiresOn_AliasToken_InSyntheticInput` Fact: positive assertion that `imageUrl` fires the regex; negative assertion that `temperature` does NOT fire `\btemp\b` (word-boundary guards protect legitimate prose)
- Updated `tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt` snapshot to match the v3 schema example output
- All 3 `PromptDenylistTests` pass (2 Theory rows + 1 Fact); full suite 224/224 green

## Task Commits

Each task was committed atomically:

1. **Task 1: Update RecipeSchemaDocumentationProvider for v3 schema example** - `83f680d` (feat)
2. **Task 2: Extend PromptDenylistTests regex + add self-check negative-path test** - `2d1587f` (feat)

## Files Created/Modified

- `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs` — bumped version 2→3; added photoUrl and description fields; added temperature to bake step; added Temperature prose note; replaced XML doc comment with // comments (Rule 1 fix)
- `src/CookBot.Application/Services/PromptBuilderService.cs` — replaced XML doc `<summary>` with // comment; changed "oven temperatures/temps" to "baking temperatures/temps" (Rule 1 fix for denylist false-positives)
- `tests/CookBot.Tests/Prompts/PromptDenylistTests.cs` — extended Denylist regex with 7 alias tokens; made field `internal static`; added `Denylist_FiresOn_AliasToken_InSyntheticInput` Fact with positive+negative assertions; replaced XML class comment with // comments
- `tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt` — regenerated snapshot for v3 schema example (version:3, photoUrl, description, temperature step, Temperature prose line)

## Decisions Made

- Replaced XML doc `<summary>` tags in scanned source files with `//` comments — `\bsummary\b` matches `</summary>` via word boundary before the `s`. Plan said "fix offending source lines"; removing XML doc tags is the minimal fix that preserves the regex as specified
- Changed "oven temperatures/temps" → "baking temperatures/temps" in PromptBuilderService.cs — `\boven\b` matched legitimate English cooking prompt prose. Plan said "fix offending source line — don't relax the regex"; wording change preserves intent while eliminating false-positive
- Made Denylist `internal static` (not `private static`) to enable the self-check Fact to reference it without reflection; aligns with Pattern S6 (file-scoped namespace)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed \bsummary\b false-positive on XML doc tags**
- **Found during:** Task 2 pre-implementation verification
- **Issue:** `\bsummary\b` in the new denylist regex matches `</summary>` XML doc tags because `<` and `/` are non-word characters, creating a word boundary before `summary`. Both `RecipeSchemaDocumentationProvider.cs` (class-level `/// <summary>`) and `PromptBuilderService.cs` (method-level `/// <summary>`) contain these tags, which would have failed the Theory test
- **Fix:** Replaced `/// <summary>...</summary>` XML doc blocks with `//` comments in both scanned source files (Task 1 scope for RecipeSchemaDocumentationProvider.cs; also fixed PromptBuilderService.cs as the second scanned file)
- **Files modified:** `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs`, `src/CookBot.Application/Services/PromptBuilderService.cs`
- **Committed in:** `83f680d` (Task 1 commit)

**2. [Rule 1 - Bug] Fixed \boven\b false-positive on legitimate English prompt prose**
- **Found during:** Task 2 pre-implementation verification
- **Issue:** `\boven\b` matched "Use Fahrenheit for oven temperatures." and "Use Fahrenheit for oven temps." in `PromptBuilderService.cs` — legitimate English description of the domain of temperatures, not an alias token instruction. This would have failed the PromptSourceFiles Theory test for PromptBuilderService.cs
- **Fix:** Changed "oven temperatures" → "baking temperatures" and "oven temps" → "baking temps" in PromptBuilderService.cs. Meaning is fully preserved; "baking temperature" is a standard cooking term
- **Files modified:** `src/CookBot.Application/Services/PromptBuilderService.cs`
- **Committed in:** `83f680d` (Task 1 commit)

**3. [Rule 3 - Blocker] Updated snapshot fixture after FormatPrompt change broke PromptSnapshotTests**
- **Found during:** Task 2 full suite run
- **Issue:** Changing `FormatPrompt` content (version, photoUrl, description, temperature) caused `PromptSnapshotTests.DefaultTemplate_AssembledPrompt_MatchesSnapshot` to fail — the committed fixture at `tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt` still reflected v2 content
- **Fix:** Regenerated the snapshot using `UPDATE_SNAPSHOTS=1 dotnet test` to get the correct expected output, then wrote the v3 content to the source fixture file
- **Files modified:** `tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt`
- **Committed in:** `2d1587f` (Task 2 commit)

## Known Stubs

None — all plan goals fully wired. The denylist Fact exercises the actual Denylist regex; the Theory test scans actual source files; the RecipeSchemaDocumentationProvider example uses real canonical field names.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema trust boundary changes introduced.

---
*Phase: 08-format-foundation*
*Completed: 2026-05-16*
