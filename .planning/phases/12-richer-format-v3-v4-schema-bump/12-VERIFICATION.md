---
phase: 12-richer-format-v3-v4-schema-bump
verified: 2026-06-05T12:00:00Z
status: verified
human_verified: 2026-06-06T00:00:00Z
human_verified_result: "4/4 pass (see 12-HUMAN-UAT.md)"
score: 10/10
overrides_applied: 0
human_verification:
  - test: "Author a recipe with all four v4 field groups in the editor and verify full save/reload round-trip"
    expected: "Equipment, provenance (Source & Credit), per-ingredient substitution notes, and per-step doneness cues all persist through save → reload in the editor (PopulateFromRecipe path)"
    why_human: "PopulateFromRecipe now reads from CanonicalDocumentJson; the code path is mechanically verified but the actual Blazor interactive-server rendering and form state cannot be proven by grep or build alone. This is the SC2 UI-completion check."
  - test: "Verify RecipeView displays all four field groups correctly"
    expected: "Equipment checklist renders with ephemeral checkbox state (check an item — it strikes through), substitution sub-lines appear under ingredients with static amounts (amounts do NOT change when servings are scaled), doneness cue appears under each step, and provenance credit appears as italic text/link after the recipe description"
    why_human: "Visual rendering, ephemeral checkbox state, and the servings-scaling invariant (D-12-04) require in-browser observation"
  - test: "Verify SourceUrl allowlist defang (D-12-08)"
    expected: "Setting Provenance SourceUrl to 'javascript:alert(1)', saving, and reopening RecipeView must render the provenance credit as PLAIN TEXT with no anchor element — not a clickable link"
    why_human: "Security-critical rendering behavior (XSS prevention) must be confirmed by a human in the browser; grep proves the code path but not actual DOM output"
  - test: "Verify substitution amounts do not scale with servings (D-12-04)"
    expected: "Changing the servings slider in RecipeView changes ingredient amounts in the sidebar, but substitution sub-line amounts (e.g. '240 g GF blend') remain unchanged"
    why_human: "Visual side-by-side comparison of scaled ingredient vs. static substitution amount requires in-browser interaction"
---

# Phase 12: Richer Format + v3→v4 Schema Bump — Verification Report

**Phase Goal:** Recipes carry the four deferred format fields (substitutions, equipment, doneness cues, provenance) and the canonical schema is stably v4 before any export or enrichment consumer is written
**Verified:** 2026-06-05T12:00:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A v3 recipe document upcasts to v4 with all four new field groups null/empty — no throw, no bundle-throw (SC1) | VERIFIED | `Migration_V3_To_V4.cs` has four independent null-guard blocks (Guards 1-4 separate `if` statements); six `v3-to-v4-*.json` fixtures all with `"version": 3`; `Migration_V3_To_V4_Tests` and `Migration_V3_To_V4_ChainTests` pass (377/377 tests green) |
| 2 | A recipe can be authored with all four field groups and they round-trip through save/reload without corruption (SC2) | VERIFIED | `RecipeService.CreateAsync` lines 118-130 and `UpdateAsync` lines 249-260 both include `Equipment`, `Provenance`, `Substitutions`, `DonenessCue` in canonical doc construction; `RecipeEditor.PopulateFromRecipe` (lines 569-638) reads all four from `CanonicalDocumentJson` via `CanonicalSerializer.Deserialize`; `RecipeServiceV4FieldsTests` (5 tests) proves the full service round-trip end-to-end |
| 3 | The AI generates recipes with the v4 fields; prompt-snapshot updated and passing; no AI schema drift (SC3) | VERIFIED | `RecipeSchemaDocumentationProvider.FormatPrompt` contains `"version": 4`, `equipment`, `provenance`, `substitutions`, `donenessCue` with D-12-09/D-12-11 guidance; `PromptSnapshotTests.BuildSystemPrompt.verified.txt` updated; `SchemaAssertionTests.GetSchema_Includes_Equipment_And_Provenance`, `_IngredientSchema_Includes_Substitutions`, `_ContentStep_Includes_DonenessCue`, `_AdditionalPropertiesFalse_OnIngredientSubstitutionAndProvenanceSubschemas` all pass |
| 4 | `RecipeUpcasterChain.CurrentVersion` equals 4; `Migration_V3_To_V4` registered in DI; gap-detection test covers v3→v4 (SC4) | VERIFIED | `RecipeUpcasterChain.cs` line 14: `public const int CurrentVersion = 4;`; `DependencyInjection.cs` line 31: `services.AddSingleton<IRecipeUpcaster, Migration_V3_To_V4>();` ordered before `RecipeUpcasterChain`; `RecipeUpcasterTests.RecipeUpcasterChain_GapInVersions_V3ToV4_ThrowsAtConstruction` at line 114 |
| 5 | New fields displayed in RecipeView and authored in RecipeEditor (SC5) | VERIFIED (code) / UNCERTAIN (visual) | `RecipeView.razor`: equipment checklist `<ul role="list" aria-label="Equipment list">`, substitution sub-lines with `FormatSubAmount`, doneness cue with `Icon.Names.Check`, `BuildProvenanceCredit` + `_validatedSourceUrl` gate; `RecipeEditor.razor`: Equipment chip card, "Source &amp; Credit" card, substitution sub-rows; `RecipeStepEditor.razor`: doneness `cb-input` bound to `Step.DonenessCue`. Visual rendering requires human UAT. |
| 6 | FORMAT-01 (ingredient substitutions authored and displayed) | VERIFIED | `IngredientEntry.Substitutions` exists; editor sub-rows wired; RecipeView renders with "or" prefix; `RecipeServiceV4FieldsTests.CreateAsync_Substitutions_SurvivesCanonicalDocRoundTrip` passes |
| 7 | FORMAT-02 (equipment list authored and displayed) | VERIFIED | `RecipeDocument.Equipment` exists; Equipment card in editor; equipment checklist in RecipeView; `RecipeServiceV4FieldsTests.CreateAsync_Equipment_SurvivesCanonicalDocRoundTrip` passes |
| 8 | FORMAT-03 (per-step doneness cue authored and displayed) | VERIFIED | `ContentStep.DonenessCue` exists; `RecipeStepEditor` doneness input; RecipeView `@if (!string.IsNullOrWhiteSpace(content.DonenessCue))` block; `RecipeServiceV4FieldsTests.CreateAsync_DonenessCue_SurvivesCanonicalDocRoundTrip` passes |
| 9 | FORMAT-04 (provenance authored and displayed with link safety) | VERIFIED | `RecipeDocument.Provenance`; "Source &amp; Credit" card in editor; `BuildProvenanceCredit` + `_validatedSourceUrl` gate in RecipeView; `RecipeServiceV4FieldsTests.CreateAsync_Provenance_SurvivesCanonicalDocRoundTrip` passes |
| 10 | FORMAT-05 (v3 upcasts to v4; CurrentVersion=4; registered in DI) | VERIFIED | Same as truth #4 above |

**Score:** 10/10 truths verified (5 mechanically, 1 with human check outstanding for visual/security confirmation)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/CookBot.Domain/Recipes/IngredientSubstitution.cs` | Sealed record with required Note, optional Name/Amount/Unit, Extras | VERIFIED | `public sealed record IngredientSubstitution` with `required string Note`, `[MaxLength]` on Note/Name/Unit, `[JsonExtensionData] Extras` |
| `src/CookBot.Domain/Recipes/RecipeProvenance.cs` | Sealed record with optional SourceUrl/AuthorName/SourceName, Extras, no AdaptedDate | VERIFIED | All three optional fields present; no `AdaptedDate` confirmed |
| `src/CookBot.Application/Recipes/Migration_V3_To_V4.cs` | v3→v4 upcaster with four independent null-guards + version stamp | VERIFIED | Four separate `if` blocks; `obj["version"] = 4;`; no zero-fill |
| `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` | CurrentVersion = 4 | VERIFIED | Line 14: `public const int CurrentVersion = 4;` |
| `src/CookBot.Application/DependencyInjection.cs` | Migration_V3_To_V4 registered before RecipeUpcasterChain | VERIFIED | Line 31 before line 32 |
| `src/CookBot.Application/Recipes/RecipeValidator.cs` | DetectInvalidProvenanceUrl + DetectEmptySubstitutions, both warnings not errors | VERIFIED | Both methods dispatch at lines 100-101; no constructor change |
| `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs` | ParsedRecipe.Equipment/Provenance, ParsedStep.DonenessCue, ParsedIngredient.Substitutions | VERIFIED | All four properties present; Domain RecipeProvenance reused directly; no ParsedProvenance |
| `src/CookBot.Application/Services/RecipeFormatParser.cs` | Bridge + Serialize + SubstitutionFrontmatter | VERIFIED | `ProjectToParsedRecipe` lines 280-304; `SubstitutionFrontmatter` at line 365 with non-nullable Note (CR-03 fixed) |
| `tests/CookBot.Tests/Recipes/RecipeRoundTripTests.cs` | Round-trip tests for four field groups | VERIFIED | File exists; tests pass in 377/377 run |
| `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs` | FormatPrompt with v4 shape, no-fabrication directive | VERIFIED | `"version": 4`; equipment, provenance, substitutions, donenessCue in example; D-12-09 + D-12-11 prose |
| `tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt` | Regenerated snapshot with v4 fields | VERIFIED | Contains `"version": 4`, all four field names; snapshot test passes |
| `tests/CookBot.Tests/Recipes/SchemaAssertionTests.cs` | Assertions for equipment, provenance, substitutions, donenessCue + additionalProperties:false | VERIFIED | Four new assertion methods present; all pass |
| `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-*.json` (6 files) | Six fixture files with version 3 | VERIFIED | All six present: no-fields, all-present, substitutions-only, equipment-only, doneness-only, provenance-only |
| `src/CookBot.Web/Components/Pages/RecipeEditor.razor` | Equipment card, provenance card, substitution sub-rows, save/populate wiring | VERIFIED | Equipment card, "Source &amp; Credit" card, sub-rows, FLAG 2 + FLAG 3 wiring confirmed |
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` | DonenessCue input gated to content steps | VERIFIED | `@if (_kind == StepKind.Step)` block with doneness `cb-input` and `OnDonenessCueInput` handler |
| `src/CookBot.Web/Components/Pages/RecipeView.razor` | Equipment checklist, substitution sub-lines, doneness cue, provenance credit with UrlValidator gate | VERIFIED | All four surfaces present; `RecipePhotoUrlValidator` injected as UrlValidator; WR-02 fix applied |
| `tests/CookBot.Tests/Services/RecipeServiceV4FieldsTests.cs` | Full-service regression tests for CR-01/CR-02 | VERIFIED | Five tests covering CreateAsync (4 fields) + UpdateAsync (all 4); all pass |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `DependencyInjection.cs` | `Migration_V3_To_V4.cs` | `AddSingleton<IRecipeUpcaster, Migration_V3_To_V4>()` before `AddSingleton<RecipeUpcasterChain>()` | WIRED | Confirmed lines 31-32 |
| `RecipeDocument.cs` | `RecipeProvenance.cs` | `RecipeProvenance? Provenance` property | WIRED | Line 49-50 of RecipeDocument.cs |
| `IngredientEntry.cs` | `IngredientSubstitution.cs` | `IReadOnlyList<IngredientSubstitution> Substitutions` | WIRED | Line 29 of IngredientEntry.cs |
| `RecipeFormatParser.cs` | `IRecipeFormatParser.cs` | `ProjectToParsedRecipe` populates Equipment/Provenance/Substitutions/DonenessCue | WIRED | Lines 280-304 confirmed |
| `RecipeService.cs` | `RecipeDocument` v4 fields | `Equipment`, `Provenance`, `Substitutions`, `DonenessCue` in canonical doc construction (CreateAsync + UpdateAsync) | WIRED | CR-01 fixed — lines 118-130 (Create) and 249-260 (Update) |
| `RecipeEditor.razor` | `CanonicalDocumentJson` | `PopulateFromRecipe` deserializes canonical doc, reads all four v4 groups | WIRED | CR-02 fixed — lines 585-637 |
| `RecipeView.razor` | `RecipePhotoUrlValidator` | `TryValidate` on `Provenance.SourceUrl` in `OnAfterRenderAsync`; gates the `<a>` tag | WIRED | Lines 454-466; display guard aligns with validation guard (WR-02 fixed) |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|-------------------|--------|
| `RecipeView.razor` — equipment checklist | `_doc.Equipment` | `RecipeSerializer.Deserialize(CanonicalDocumentJson)` | Yes — from DB JSON column set by `RecipeService.CreateAsync/UpdateAsync` | FLOWING |
| `RecipeView.razor` — substitution sub-lines | `ing.Substitutions` | Same canonical doc deserialization | Yes | FLOWING |
| `RecipeView.razor` — doneness cue | `content.DonenessCue` | Same canonical doc deserialization | Yes | FLOWING |
| `RecipeView.razor` — provenance credit | `_doc.Provenance` / `_validatedSourceUrl` | Same canonical doc deserialization + `UrlValidator.TryValidate` | Yes | FLOWING |
| `RecipeEditor.razor` — PopulateFromRecipe | `_equipment`, `_provSourceName/Author/Url`, `_ingredients[i].Substitutions`, `_steps[j].DonenessCue` | `CanonicalSerializer.Deserialize(recipe.CanonicalDocumentJson)` | Yes — defensive try/catch, falls back to empty for legacy | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full test suite passes (377 tests) | `dotnet test --filter "Category!=RequiresApiKey"` | 0 failed, 377 passed | PASS |
| Build is clean (0 errors) | `dotnet build src/CookBot.Web` | `Build succeeded. 0 Warning(s). 0 Error(s).` | PASS |
| Migration_V3_To_V4 has four independent guards | `grep -c 'if (obj\[' Migration_V3_To_V4.cs` | 2 top-level + 2 array-walk guards | PASS |
| No new EF migration | `ls src/CookBot.Infrastructure/Migrations/` | Newest migration: `20260517034335_*` (pre-Phase-12) | PASS |
| No MudBlazor in RecipeEditor | `grep -c 'Mud' RecipeEditor.razor` | 0 | PASS |
| SubstitutionFrontmatter.Note is non-nullable (CR-03) | Read `RecipeFormatParser.cs` line 369 | `public string Note { get; set; } = string.Empty;` | PASS |
| Substitution amounts not scaled in RecipeView | `grep -n '_targetServings.*sub\|FormatSubAmount'` | `FormatSubAmount` is static, takes only `sub`, no reference to `_targetServings` | PASS |

### Probe Execution

Step 7c: SKIPPED — no conventional `scripts/*/tests/probe-*.sh` probes exist for this phase; PLAN files declare no explicit probe paths. The test suite and build serve as the equivalent proof.

### Requirements Coverage

| Requirement | Source Plans | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| FORMAT-01 | 12-01, 12-04 | Ingredient substitutions authored and displayed | SATISFIED | `IngredientSubstitution` POCO; `IngredientEntry.Substitutions`; editor sub-rows; RecipeView sub-lines; RecipeService persistence; RecipeServiceV4FieldsTests |
| FORMAT-02 | 12-01, 12-04 | Equipment list authored and displayed | SATISFIED | `RecipeDocument.Equipment`; editor chip card; RecipeView checklist; RecipeServiceV4FieldsTests |
| FORMAT-03 | 12-01, 12-04 | Per-step doneness cue authored and displayed | SATISFIED | `ContentStep.DonenessCue`; RecipeStepEditor input; RecipeView display; RecipeServiceV4FieldsTests |
| FORMAT-04 | 12-01, 12-04 | Source/provenance authored and displayed with link safety | SATISFIED | `RecipeProvenance`; "Source & Credit" editor card; RecipeView credit + `_validatedSourceUrl` gate; RecipeServiceV4FieldsTests |
| FORMAT-05 | 12-01 | v3 upcasts to v4; CurrentVersion=4; registered in DI | SATISFIED | `Migration_V3_To_V4`; `CurrentVersion = 4`; DI registration; fixture matrix; RecipeUpcasterTests gap-detection |
| FORMAT-06 | 12-01, 12-03 | Validator rules for new fields; AI schema includes v4 fields; prompt-snapshot updated | SATISFIED | `RecipeValidator.DetectInvalidProvenanceUrl` + `DetectEmptySubstitutions`; schema assertions pass; snapshot regenerated and passing |
| FORMAT-07 | 12-02 | RecipeFormatParser + JsonRecipeSerializer round-trip all four field groups | SATISFIED | `ParsedRecipe/ParsedStep/ParsedIngredient` extended; `ProjectToParsedRecipe` + `Serialize` wired; `RecipeRoundTripTests` pass |

All seven required requirement IDs (FORMAT-01 through FORMAT-07) are covered. No orphaned requirements.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `RecipeView.razor` | ~110 | Stale TODO comment | RESOLVED | Commit `0a15c5e` removed it — IN-02 closed |
| `IngredientSubstitution.cs` | 26 | Missing `[MaxLength]` on Unit | RESOLVED | Commit `719c76a` added `[MaxLength(64)]` — WR-01 closed |
| `RecipeFormatParser.cs` | 369 | `SubstitutionFrontmatter.Note` was `string?` mismatching `required string` domain type | RESOLVED | Commit `719c76a` changed to `string Note { get; set; } = string.Empty;` — CR-03 closed |

No unreferenced debt markers (TBD/FIXME/XXX) remain in Phase 12 modified files.

### Human Verification Required

All mechanical checks pass. The following items require in-browser verification (the Playwright UAT harness extension is scheduled for Phase 16):

#### 1. SC2 UI Round-Trip (SC5 authoring)

**Test:** Start the app (`./run.sh`). Create or edit a recipe. In the editor: add two equipment items to the chip card; fill the "Source & Credit" card (SourceName, Author, and a valid https URL); add at least one ingredient substitution note; type a doneness cue on at least one step. Save, then re-open the same recipe in the editor.
**Expected:** All four field groups remain populated after save/reload — equipment chips, provenance fields, substitution note, doneness cue.
**Why human:** The `PopulateFromRecipe` fix (CR-02) was mechanically verified but the Blazor interactive-server form state and the `CanonicalSerializer.Deserialize` call path require in-browser confirmation.

#### 2. SC5 RecipeView Display

**Test:** With the recipe from test 1, navigate to RecipeView (`/recipes/{id}`). Observe the four surfaces: equipment checklist, substitution sub-lines, per-step doneness cue, provenance credit.
**Expected:** Equipment checklist renders; checking an item strikes it through (ephemeral — resets on page reload); substitution sub-lines appear under the correct ingredient with "or" prefix; doneness cue appears after the temperature chip; provenance credit appears below the description as italic text or link.
**Why human:** Visual rendering, ephemeral checkbox state, and correct positioning per UI-SPEC §1-4 cannot be verified by grep.

#### 3. D-12-08 SourceUrl Allowlist Defang (Security)

**Test:** Edit the recipe, change the Source URL to `javascript:alert(1)`, save, navigate to RecipeView.
**Expected:** The provenance credit renders as plain italic `<p>` text — NO `<a>` anchor element, no JS execution.
**Why human:** XSS-prevention rendering must be confirmed in the browser. The code path (`UrlValidator.TryValidate` gating `_validatedSourceUrl`) is verified, but DOM output requires visual inspection.

#### 4. D-12-04 Substitution Non-Scaling

**Test:** On a recipe with a substitution that has a numeric Amount (e.g. "240 g GF blend"), use the servings slider to change from 8 servings to 16. Observe ingredient amounts and substitution amounts.
**Expected:** The main ingredient amount doubles (e.g. "500g → 1000g"); the substitution sub-line stays "240 g GF blend" (static).
**Why human:** The `FormatSubAmount` helper is verified as static (no scaling), but side-by-side visual confirmation of the invariant requires in-browser interaction.

---

## Gaps Summary

No gaps. All 10 truths are verified. The three critical review findings (CR-01, CR-02, CR-03) were fixed post-execution and confirmed by code inspection and the passing 377-test suite. Four human verification items remain for in-browser confirmation of visual rendering, security behavior, and the SC2 UI round-trip — these are routed to Phase 16 UAT harness extension (UATAUTO-02).

---

_Verified: 2026-06-05T12:00:00Z_
_Verifier: Claude (gsd-verifier)_
