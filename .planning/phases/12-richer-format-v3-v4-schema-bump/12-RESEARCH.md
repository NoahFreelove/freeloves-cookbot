# Phase 12: Richer Format + v3→v4 Schema Bump — Research

**Researched:** 2026-06-05
**Domain:** .NET/C# schema evolution, Blazor Server UI, xUnit+Verify.Xunit snapshot testing
**Confidence:** HIGH — all findings grounded in direct codebase reads; zero external lookups required.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- D-12-01: `IngredientSubstitution` is a new pure POCO record in `CookBot.Domain/Recipes/` with `required string Note`, `string? Name`, `double? Amount`, `string? Unit`.
- D-12-02: Carried as `IReadOnlyList<IngredientSubstitution> Substitutions { get; init; } = []` on `IngredientEntry`.
- D-12-03: `IngredientSubstitution` carries `[JsonExtensionData] Extras` dictionary.
- D-12-04: Substitution `Amount` is display-only — does NOT scale. `RecipeScalingService` stays untouched.
- D-12-05: Equipment is `IReadOnlyList<string> Equipment { get; init; } = []` on `RecipeDocument` (not a structured `EquipmentEntry`).
- D-12-06: `string? DonenessCue { get; init; }` on `ContentStep` only, alongside `Temperature`. No enum, `[MaxLength]` guard only.
- D-12-07: `RecipeProvenance` record in `CookBot.Domain/Recipes/` with `string? SourceUrl`, `string? AuthorName`, `string? SourceName`. `RecipeProvenance? Provenance` on `RecipeDocument` is nullable. No `AdaptedDate`.
- D-12-08: `SourceUrl` rendered as clickable link MUST pass `RecipePhotoUrlValidator` scheme-allowlist. Fails → plain text (never live link).
- D-12-09: AI MUST NOT fabricate `SourceUrl` or `AuthorName`. Prompt: "leave `provenance` null unless a source is explicitly provided; never invent a URL or author."
- D-12-10: Phase 12 surfaces new fields in `RecipeEditor` + `RecipeView` ONLY. Cooking Mode surfacing is DEFERRED.
- D-12-11: Prompt instructs Claude to naturally populate `equipment` and per-step `donenessCue`; emit `substitutions` only when genuinely useful; leave `provenance` null by default.
- D-12-12: `Migration_V3_To_V4 : IRecipeUpcaster` (`FromVersion => 3`, `ToVersion => 4`) with four independent per-field null-guard no-ops. Copy `Migration_V2_To_V3` structure verbatim (PITFALL C7).
- D-12-13: `RecipeUpcasterChain.CurrentVersion` → 4. Register `Migration_V3_To_V4` in `DependencyInjection.cs` after `Migration_V2_To_V3` IN THE SAME PLAN as the class (prevents P1 startup crash). Gap-detection test extended for v3→v4.
- D-12-14: `RecipeJsonSchemaProvider` needs NO code change — reflection auto-updates. Prompt-snapshot test MUST be regenerated and committed in the same change (FORMAT-06, P3). Watch anyOf/`additionalProperties` strict-mode for `IngredientSubstitution` and `RecipeProvenance`.
- D-12-15: `RecipeValidator` gains warnings (not errors) for new fields. Provenance `SourceUrl` disallowed-scheme → warning; substitution with neither `Note` nor `Name` → warning.

### Claude's Discretion
- Exact `[MaxLength]` caps for new string fields (follow `PhotoUrl=2048` / `Description=4096` precedents).
- Editor authoring affordances (sub-rows for substitutions, tag-style input for equipment, per-step text field for doneness, recipe-meta block for provenance).
- Internal naming of the four upcaster guards and test fixture filenames.

### Deferred Ideas (OUT OF SCOPE)
- Cooking Mode surfacing of equipment and doneness cues.
- Structured `EquipmentEntry` record.
- Proportional substitution-amount scaling.
- Provenance `AdaptedDate`.
- AI-assisted substitution generation as a dedicated feature.
- First-class `recipeCategory`/`recipeCuisine` v4 fields.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FORMAT-01 | A recipe ingredient carries one or more substitutions (freeform note + optional structured name/amount/unit), authored in editor and displayed on recipe. | `IngredientEntry` POCO + editor row pattern + RecipeView ingredient sidebar loop |
| FORMAT-02 | A recipe carries an equipment/tools list (`string[]`), authored in editor and displayed on recipe. | `RecipeDocument` POCO + right-rail meta card pattern in editor |
| FORMAT-03 | A recipe step carries a per-step doneness cue (`string?`, alongside existing Temperature), authored and displayed. | `ContentStep` POCO + `StepTemperaturePicker.razor` pattern directly reusable |
| FORMAT-04 | A recipe carries source/provenance (`SourceUrl`, `AuthorName`, optional "adapted from"), authored and displayed with source link. | `RecipeDocument` POCO + `RecipePhotoUrlValidator` reuse |
| FORMAT-05 | v3 doc upcasts to v4 with all new fields null/empty — no data loss, no throw. `RecipeUpcasterChain.CurrentVersion` = 4. | `Migration_V2_To_V3` copy-target; `RecipeUpcasterChain` const update |
| FORMAT-06 | `RecipeValidator` enforces new field rules; AI JSON schema includes them; prompt-snapshot test updated. | `RecipeSchemaDocumentationProvider` prose update; snapshot regen via `VERIFY_AUTO=true` |
| FORMAT-07 | `RecipeFormatParser` + `JsonRecipeSerializer` round-trip all four new field groups; parser tests cover null/present/edge. | `JsonRecipeSerializer._compact` options + round-trip via `Recipe.CanonicalDocumentJson`; `RecipeService` save path |
</phase_requirements>

---

## Summary

Phase 12 is a mechanical replay of the Phase 8 v2→v3 bump. Every pattern to follow, every file to change, and every test to update already exists in the codebase. The research task is to pin exact file paths, line numbers, and copy-targets so the planner can write atomic tasks without additional codebase investigation.

The schema bump is JSON-column-only: the four new field groups (`equipment`, `provenance`, per-ingredient `substitutions`, per-step `donenessCue`) live inside `Recipe.CanonicalDocumentJson`. No EF migration is needed — confirmed by the v2→v3 precedent where `photoUrl` and `description` were added to `RecipeDocument` with no new database columns beyond what was already added in Phase 8's own DB migration. The v4 POCO changes are purely in-memory shape; the SQLite column is `TEXT` and the schema bump is transparent to EF.

The three gated pitfalls are P1 (DI gap → startup crash if `CurrentVersion` bumped before migration registered), P2 (bundle-throw → all four null-guards must be independent), and P3 (AI schema drift → snapshot must be regenerated and committed atomically). The `ExternalizeAnyOfBranches` pass in `RecipeJsonSchemaProvider` already handles nested-object anyOf branches for `StepNode`; the new `IngredientSubstitution` and `RecipeProvenance` records will generate new `$defs` entries via the same pass — no code change to the provider is needed.

**Primary recommendation:** Implement in this order — (1) Domain POCOs, (2) upcaster + DI + CurrentVersion + gap-test in one task, (3) validator, (4) schema-doc prose + snapshot regen, (5) editor UI, (6) view UI. Never split the upcaster class from its DI registration across separate tasks.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| New POCO field shapes (substitutions, equipment, doneness, provenance) | Domain | — | Pure records, no framework refs, per Clean/Onion arch |
| v3→v4 JSON-node migration | Application (Recipes/) | — | `IRecipeUpcaster` interface lives in Application |
| DI wiring of new upcaster | Application (DI.cs) | — | All upcaster registrations live here |
| Validation rules for new fields | Application (Recipes/) | — | `RecipeValidator` is Application layer |
| AI prompt schema generation | Application (Recipes/) | — | `RecipeJsonSchemaProvider` reflects Domain POCOs |
| AI prompt prose (format description) | Application (Recipes/) | — | `RecipeSchemaDocumentationProvider` is Application |
| Round-trip serialization | Application (Recipes/) | Domain (POCO attrs) | `JsonRecipeSerializer` + STJ `[JsonPropertyName]` attrs |
| Authoring UI (editor) | Web (Blazor) | Application (DTOs) | `RecipeEditor.razor` + `RecipeEditorParts/` |
| Display UI (view) | Web (Blazor) | Domain (RecipeDocument) | `RecipeView.razor` reads `_doc: RecipeDocument` directly |
| Persistence | Infrastructure (EF/SQLite) | Application (RecipeService) | `CanonicalDocumentJson` TEXT column — no schema change |

---

## Standard Stack

No new packages. All implementation uses the existing stack. [VERIFIED: direct codebase read]

| Component | Existing Artifact | Phase 12 Use |
|-----------|-------------------|--------------|
| POCO records | `RecipeDocument`, `IngredientEntry`, `ContentStep`, `StepTemperature` | Copy conventions for 3 new records |
| JSON serialization | `System.Text.Json` + `JsonRecipeSerializer` | Auto-picks up new POCO fields |
| Schema reflection | `RecipeJsonSchemaProvider` (no change) | Auto-reflects new fields via `JsonSchemaExporter` |
| Upcaster chain | `Migration_V2_To_V3`, `RecipeUpcasterChain` | Copy for `Migration_V3_To_V4`, bump `CurrentVersion` |
| Validation | `RecipeValidator` | Add warnings for new fields |
| Snapshot testing | `Verify.Xunit` 31.12.5 | Regen snapshot after prompt prose change |
| URL validation | `RecipePhotoUrlValidator` | Reuse for provenance `SourceUrl` |
| Editor UI | `RecipeEditor.razor`, `RecipeEditorParts/StepTemperaturePicker.razor` | Model for new authoring controls |

---

## Package Legitimacy Audit

> No new packages are installed in this phase. Section is N/A.

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

---

## Architecture Patterns

### System Architecture Diagram

```
User Edit Action
      │
      ▼
RecipeEditor.razor (_ingredients: List<ParsedIngredient>, _steps: List<ParsedStep>)
  │  Substitutions: List<IngredientSubstitution> added to editor state
  │  Equipment: List<string> added to right-rail meta card
  │  DonenessCue: string? added to each ParsedStep, picked by RecipeStepEditor
  │  Provenance: new meta form block in right rail
      │
      ▼  SaveRecipe() → builds ParsedRecipe
RecipeService.CreateAsync / UpdateAsync
      │
      ▼  builds RecipeDocument (v4) with new fields
JsonRecipeSerializer.Serialize(doc)
      │
      ▼  compact JSON string
Recipe.CanonicalDocumentJson (SQLite TEXT column — unchanged shape)

---

Read Path:
Recipe.CanonicalDocumentJson
      │
      ▼
RecipeUpcasterChain.UpcastToCurrent(node)   ← Migration_V3_To_V4 fires for v3 docs
      │
      ▼
JsonRecipeSerializer.Deserialize(node) → RecipeDocument (v4)
      │
      ▼
RecipeView.razor / RecipeFormatParser  ← _doc.Equipment, _doc.Provenance, ing.Substitutions, step.DonenessCue
```

### Recommended Project Structure (new files only)

```
src/CookBot.Domain/Recipes/
├── IngredientSubstitution.cs    # NEW — FORMAT-01 POCO
└── RecipeProvenance.cs          # NEW — FORMAT-04 POCO

src/CookBot.Application/Recipes/
└── Migration_V3_To_V4.cs        # NEW — FORMAT-05 upcaster

tests/CookBot.Tests/Recipes/
├── Migration_V3_To_V4_Tests.cs  # NEW — fixture-matrix (mirrors Migration_V2_To_V3_Tests.cs)
└── Migration_V3_To_V4_ChainTests.cs  # NEW — chain wiring (mirrors Migration_V2_To_V3_ChainTests.cs)

tests/CookBot.Tests/Fixtures/Recipes/upcaster/
├── v3-to-v4-no-fields.json          # NEW fixture
├── v3-to-v4-all-present.json        # NEW fixture
├── v3-to-v4-substitutions-only.json # NEW fixture
├── v3-to-v4-equipment-only.json     # NEW fixture
├── v3-to-v4-doneness-only.json      # NEW fixture
└── v3-to-v4-provenance-only.json    # NEW fixture
```

### Pattern 1: POCO record with `[JsonExtensionData]` and `[JsonPropertyName]` (copy-target)

**Source:** `src/CookBot.Domain/Recipes/IngredientEntry.cs` (lines 1–31)

```csharp
// Source: direct read of IngredientEntry.cs
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CookBot.Domain.Recipes;

public sealed record IngredientEntry
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    // ... other fields ...

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extras { get; init; } = new();
}
```

**Apply this pattern to:** `IngredientSubstitution` (with `required string Note`) and `RecipeProvenance` (all-optional fields). Both records need `[JsonExtensionData] Extras` (D-12-03, FORMAT-09).

### Pattern 2: Nullable optional field on `ContentStep` (copy-target for `DonenessCue`)

**Source:** `src/CookBot.Domain/Recipes/StepNode.cs` lines 26–29

```csharp
// Existing: StepTemperature? Temperature (line 25)
[JsonPropertyName("temperature")]
public StepTemperature? Temperature { get; init; }

// New — same pattern, string? instead of record?:
[JsonPropertyName("donenessCue")]
public string? DonenessCue { get; init; }
```

`DonenessCue` goes on `ContentStep` only. `SectionStep` does NOT get it — per D-12-06 and the `Temperature` precedent.

### Pattern 3: Empty-list-not-null on `RecipeDocument` (copy-target for `Equipment`)

**Source:** `src/CookBot.Domain/Recipes/RecipeDocument.cs` lines 38–43

```csharp
// Existing convention (Tags, Ingredients, Steps):
[JsonPropertyName("tags")]
public IReadOnlyList<string> Tags { get; init; } = [];

// New — same pattern:
[JsonPropertyName("equipment")]
public IReadOnlyList<string> Equipment { get; init; } = [];
```

### Pattern 4: Nullable top-level record on `RecipeDocument` (copy-target for `Provenance`)

**Source:** `RecipeDocument.cs` lines 29–35 (`PhotoUrl`, `Description` — nullable string precedents)

```csharp
// New — nullable record, same nullable-deserialize-to-null semantics:
[JsonPropertyName("provenance")]
public RecipeProvenance? Provenance { get; init; }
```

STJ maps absent JSON key → null on `RecipeProvenance?` automatically. No migration guard needed beyond the no-op documentation comment.

### Pattern 5: `Migration_V3_To_V4` (exact copy-target)

**Source:** `src/CookBot.Application/Recipes/Migration_V2_To_V3.cs` (full file, 58 lines)

The v4 upcaster follows this structure verbatim:
- Class header: `public sealed class Migration_V3_To_V4 : IRecipeUpcaster`
- `FromVersion => 3`, `ToVersion => 4`
- Four independent guard blocks (one per new field group) — all no-ops, documentation only
- Final line: `obj["version"] = 4;`

Guard structure for the two list fields (`equipment`, per-ingredient `substitutions`):
```csharp
// Guard for equipment (top-level array absent → stays absent → STJ maps to empty [] on IReadOnlyList<string>)
if (obj["equipment"] is null) { /* no-op */ }

// Guard for provenance (nullable record absent → stays absent → STJ maps to null on RecipeProvenance?)
if (obj["provenance"] is null) { /* no-op */ }

// Guard for per-step donenessCue (walk steps, content-only):
if (obj["steps"] is JsonArray steps)
{
    foreach (var step in steps.OfType<JsonObject>())
    {
        if (step["kind"]?.GetValue<string>() == "content" && step["donenessCue"] is null)
        { /* no-op: DonenessCue is string?; STJ maps absent -> null */ }
    }
}

// Guard for per-ingredient substitutions:
if (obj["ingredients"] is JsonArray ingredients)
{
    foreach (var ing in ingredients.OfType<JsonObject>())
    {
        if (ing["substitutions"] is null) { /* no-op: empty list default */ }
    }
}
```

### Pattern 6: DI registration (exact insertion point)

**Source:** `src/CookBot.Application/DependencyInjection.cs` lines 29–31

```csharp
// Current (lines 29–31):
services.AddSingleton<IRecipeUpcaster, Migration_V1_To_V2>();
services.AddSingleton<IRecipeUpcaster, Migration_V2_To_V3>();
services.AddSingleton<RecipeUpcasterChain>();

// After Phase 12 (insert between line 30 and 31):
services.AddSingleton<IRecipeUpcaster, Migration_V1_To_V2>();
services.AddSingleton<IRecipeUpcaster, Migration_V2_To_V3>();
services.AddSingleton<IRecipeUpcaster, Migration_V3_To_V4>();  // NEW — Phase 12
services.AddSingleton<RecipeUpcasterChain>();
```

`RecipeUpcasterChain` is registered AFTER all upcasters so DI resolves `IEnumerable<IRecipeUpcaster>` completely before the chain constructor runs its gap-validation.

### Pattern 7: `RecipeUpcasterChain.CurrentVersion` bump

**Source:** `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` line 14

```csharp
// Current:
public const int CurrentVersion = 3;

// After Phase 12:
public const int CurrentVersion = 4;
```

This single-line change MUST be in the same task as `Migration_V3_To_V4` DI registration to prevent P1 startup crash.

### Pattern 8: `RecipeValidator` warning block (copy-target)

**Source:** `src/CookBot.Application/Recipes/RecipeValidator.cs` lines 66–82 (temperature validation block)

New validator rules go in the `Validate` method as warnings (never errors), following the `DetectOrphanIngredients` / `DetectEmptySections` pattern:
- **Provenance `SourceUrl` scheme check:** `RecipePhotoUrlValidator.TryValidate(doc.Provenance?.SourceUrl, ...)` → warning if `errorCode` is not null
- **Empty substitution:** substitution entry where both `Note` and `Name` are null/empty → warning at `/ingredients/{i}/substitutions/{j}`

`RecipeValidator` is constructor-injected with `RecipePhotoUrlValidator` already (confirmed via `DependencyInjection.cs` line 27). No new DI wiring needed.

Wait — checking actual constructor: `RecipeValidator` is registered Singleton (line 25) and has no constructor parameters visible in the file. `RecipePhotoUrlValidator` is also Singleton (line 27). To use the validator inside `RecipeValidator`, inject it via constructor. This is a new dependency — verify the `RecipeValidator` class has no constructor currently.

**Source:** `src/CookBot.Application/Recipes/RecipeValidator.cs` — class has no explicit constructor; all methods are static helpers. The `Validate(RecipeDocument)` method is the only public entrypoint and takes no DI deps. For provenance URL checking, either (a) inject `RecipePhotoUrlValidator` via constructor (requires updating DI registration from Singleton to take a Singleton dep — fine), or (b) inline the scheme check directly using `Uri.TryCreate` + `uri.Scheme is "http" or "https"`. Option (b) is simpler and avoids touching DI; the same logic is 3 lines. RECOMMENDATION: inline the scheme check in `RecipeValidator` (same logic, no new dep). [ASSUMED — planner should confirm]

### Pattern 9: `RecipeSchemaDocumentationProvider` prose update (FORMAT-06)

**Source:** `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs` lines 11–53

The `FormatPrompt` constant is a hand-authored `"""..."""` raw string containing the example JSON shape sent to the AI. It currently shows version 3 and the existing fields. Phase 12 requires:

1. Bump `"version": 3` → `"version": 4` in the example.
2. Add `"equipment": ["stand mixer", "9-inch cake pan"]` at the top-level.
3. Add `"substitutions": [{"note": "use oat milk for dairy-free"}]` on at least one ingredient in the example.
4. Add `"donenessCue": "golden brown on top and toothpick comes out clean"` on at least one content step.
5. Add `"provenance": null` (or a commented example) with the explicit instruction from D-12-09: "leave `provenance` null unless a source is explicitly provided; never invent a URL or author."
6. Add guidance for D-12-11: naturally populate `equipment` and `donenessCue`; emit `substitutions` only when genuinely useful.

After this change, the prompt snapshot test (`PromptSnapshotTests.BuildSystemPrompt`) will fail with a diff. Accept the new snapshot by running:
```bash
VERIFY_AUTO=true dotnet test tests/CookBot.Tests/ --filter "FullyQualifiedName~BuildSystemPrompt"
```
This regenerates `tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt`. Commit both the `.cs` change and the `.verified.txt` update in the same commit (P3 gate).

### Pattern 10: Blazor editor authoring (copy-target for `StepTemperaturePicker`)

**Source:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/StepTemperaturePicker.razor` (full file, 155 lines)

The doneness cue editor is simpler than the temperature picker — it is a plain `<input type="text">` with no unit pills. Model its integration in `RecipeStepEditor.razor` after lines 128–134 of that file (the temperature picker block):

```razor
@* Phase 12 — per-step doneness cue (alongside Temperature) *@
@if (_kind == StepKind.Step)
{
    <div style="margin-top:2px;">
        <input type="text"
               value="@Step.DonenessCue"
               placeholder="Doneness cue (e.g. golden brown, 165°F internal)"
               aria-label="Doneness cue"
               @oninput="@(e => Step.DonenessCue = e.Value as string)" />
    </div>
}
```

`ParsedStep` must gain a `string? DonenessCue` property to carry this through the save path.

### Anti-Patterns to Avoid

- **Bundle-throw in Migration_V3_To_V4:** All four null-guards must be separate `if` blocks. Do NOT combine into a single compound check — PITFALL C7. The v2→v3 migration is the reference (see file above).
- **Zero-filling absent fields:** Never set `obj["equipment"] = new JsonArray()` in the migration. The upcaster must only stamp `version: 4`. STJ deserializes absent arrays as the default `= []` on `IReadOnlyList<string>`.
- **Splitting upcaster from DI registration:** `Migration_V3_To_V4` class + DI registration + `CurrentVersion = 4` MUST land in the same atomic commit. Deploying `CurrentVersion = 4` before the migration is registered causes a startup crash (P1).
- **Modifying `RecipeJsonSchemaProvider` code:** No changes to the provider class itself. The two post-processing passes (`SetAdditionalPropertiesFalse` and `ExternalizeAnyOfBranches`) already handle any new nested-object subschemas the new records introduce.
- **Storing new fields outside `CanonicalDocumentJson`:** Equipment, substitutions, doneness cues, and provenance are ONLY in the JSON column. No new EF columns. No new migrations.
- **Scaling substitution amounts:** `RecipeScalingService` is untouched. D-12-04 is a hard invariant.
- **Emitting provenance from AI:** D-12-09 is a hard constraint in the prompt — the AI must be instructed to leave `provenance` null.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| URL scheme allowlist for `SourceUrl` | Custom regex/Uri check | `RecipePhotoUrlValidator.TryValidate` | Already ships; handles protocol-relative, path-only, malformed shapes; produces `errorCode` for toast messages |
| JSON schema generation | Manual JSON string | `RecipeJsonSchemaProvider` (no change) | `JsonSchemaExporter` auto-reflects new POCO fields; the two post-processing passes handle Anthropic strict-mode requirements |
| Snapshot diff detection | Manual string comparison | `Verify.Xunit` | Already wired via `ModuleInitializer.cs`; use `VERIFY_AUTO=true dotnet test` to accept |
| Version chain gap detection | Custom startup check | `RecipeUpcasterChain` constructor | Gap detection already throws `InvalidOperationException` at construction time; just extend the tests |

---

## Files to Create vs. Modify

### Create (new files)

| File | Why |
|------|-----|
| `src/CookBot.Domain/Recipes/IngredientSubstitution.cs` | New POCO (FORMAT-01, D-12-01/02/03) |
| `src/CookBot.Domain/Recipes/RecipeProvenance.cs` | New POCO (FORMAT-04, D-12-07) |
| `src/CookBot.Application/Recipes/Migration_V3_To_V4.cs` | New upcaster (FORMAT-05, D-12-12) |
| `tests/CookBot.Tests/Recipes/Migration_V3_To_V4_Tests.cs` | Fixture-matrix tests (mirrors `Migration_V2_To_V3_Tests.cs`) |
| `tests/CookBot.Tests/Recipes/Migration_V3_To_V4_ChainTests.cs` | Chain wiring tests (mirrors `Migration_V2_To_V3_ChainTests.cs`) |
| `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-no-fields.json` | Fixture: v3 doc with none of the 4 new fields |
| `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-all-present.json` | Fixture: v3 doc with all 4 new fields |
| `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-substitutions-only.json` | Fixture: partial field coverage |
| `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-equipment-only.json` | Fixture: partial field coverage |
| `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-doneness-only.json` | Fixture: partial field coverage |
| `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-provenance-only.json` | Fixture: partial field coverage |

### Modify (existing files)

| File | What Changes | Line(s) |
|------|-------------|---------|
| `src/CookBot.Domain/Recipes/RecipeDocument.cs` | Add `Equipment` + `Provenance` properties | After line 44 (after `Tags`) |
| `src/CookBot.Domain/Recipes/IngredientEntry.cs` | Add `Substitutions` property | After line 29 (before `Extras`) |
| `src/CookBot.Domain/Recipes/StepNode.cs` | Add `DonenessCue` to `ContentStep` | After line 25 (after `Temperature`) |
| `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs` | Add `DonenessCue` to `ParsedStep`; add `Substitutions` to `ParsedIngredient`; add `Equipment`/`Provenance` to `ParsedRecipe` | Lines 18–40 |
| `src/CookBot.Application/DependencyInjection.cs` | Insert `Migration_V3_To_V4` registration at line ~31; bump `CurrentVersion` is in upcaster chain | Line 31 |
| `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` | `CurrentVersion = 3` → `CurrentVersion = 4` | Line 14 |
| `src/CookBot.Application/Recipes/RecipeValidator.cs` | Add new warning rules for provenance URL + empty substitution | After line 165 (new private methods) |
| `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs` | Update `FormatPrompt` — version 4, add 4 new fields in example JSON, add prompt guidance | Lines 11–53 |
| `src/CookBot.Application/Services/RecipeFormatParser.cs` | Add new fields to `ProjectToParsedRecipe`, `RecipeFrontmatter`, `IngredientFrontmatter`, `StepFrontmatter` | Lines 259–343 |
| `src/CookBot.Web/Components/Pages/RecipeEditor.razor` | Add equipment meta card; add provenance meta card; add substitution rows per ingredient | Ingredient loop (~118–172), right rail (~204–) |
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` | Add doneness cue input below temperature picker | After line 134 |
| `src/CookBot.Web/Components/Pages/RecipeView.razor` | Equipment checklist, substitution chips under ingredients, doneness per step, provenance credit | Ingredient sidebar (~149–167), step loop (~177–218), hero section |
| `tests/CookBot.Tests/Recipes/RecipeUpcasterTests.cs` | Extend `RecipeUpcasterChain_GapInVersions_ThrowsAtConstruction` to cover v3→v4 chain; update `MakeChain()` | Lines 15–16, 100–107 |
| `tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt` | Regenerate after `RecipeSchemaDocumentationProvider` update | Full file (generated by `VERIFY_AUTO=true dotnet test`) |

---

## Round-Trip Path (SC2 verification target)

The save/load round-trip for new v4 fields follows this exact path:

**Save:**
1. `RecipeEditor.razor` → `ParsedRecipe` (new fields in `ParsedStep.DonenessCue`, `ParsedIngredient.Substitutions`, `ParsedRecipe.Equipment`, `ParsedRecipe.Provenance`)
2. `RecipeService.UpdateAsync` → builds `RecipeDocument` from `ParsedRecipe`
3. `JsonRecipeSerializer.Serialize(doc)` → compact JSON string (STJ `WhenWritingNull` skips nulls; empty lists serialize as `[]`)
4. `recipe.CanonicalDocumentJson = ...` → stored in SQLite

**Load:**
1. `Recipe.CanonicalDocumentJson` read from DB
2. `JsonNode.Parse(json)` → `RecipeUpcasterChain.UpcastToCurrent(node)` (v4 doc is identity pass if version=4)
3. `JsonRecipeSerializer.Deserialize(node)` → `RecipeDocument` (new fields auto-populated from JSON)
4. `RecipeView.razor` → `_doc.Equipment`, `ing.Substitutions`, `step.DonenessCue`, `_doc.Provenance`

**Testable assertion for SC2:** Serialize a `RecipeDocument` with all four field groups populated, deserialize the resulting JSON, and assert field-level equality. No special serializer changes needed — the `_compact` options in `JsonRecipeSerializer` already handle `WhenWritingNull` and camelCase.

**Key gap:** `RecipeFormatParser.ProjectToParsedRecipe` (lines 259–298) and `RecipeFrontmatter` inner classes (lines 302–343) currently only project the existing fields. They must be extended to carry the four new field groups — otherwise the YAML `Serialize(ParsedRecipe)` path (used by some surfaces) loses them. This is a FORMAT-07 requirement and affects the `Serialize` overload in `RecipeFormatParser`, not just the `TryParse` path.

---

## Common Pitfalls

### Pitfall P1: DI Gap → Startup Crash
**What goes wrong:** `RecipeUpcasterChain.CurrentVersion` is bumped to 4 but `Migration_V3_To_V4` is not yet registered. The chain constructor finds no upcaster for 3→4. At startup, DI resolves `RecipeUpcasterChain` → constructor throws `InvalidOperationException` → app fails to start.
**Why it happens:** Forgetting to register the migration, or splitting the CurrentVersion bump across a different task than the DI registration.
**How to avoid:** Keep `CurrentVersion = 4`, DI registration, and `Migration_V3_To_V4.cs` in a single atomic task. The planner must verify these three changes appear together.
**Warning signs:** `RecipeUpcasterChain_GapInVersions_ThrowsAtConstruction` test passes (it only tests construction-time gap detection); the app startup crash is the runtime symptom.

### Pitfall P2: Bundle-Throw in Migration
**What goes wrong:** The four new field guards are combined into a single `if` or share state — if one throws, all subsequent guards are skipped, leaving a partial upcast.
**Why it happens:** Developer unfamiliar with the pattern combines guards for "efficiency."
**How to avoid:** Copy `Migration_V2_To_V3.cs` verbatim — four separate `if` blocks. Each guard's comment explicitly notes it is independent (PITFALL C7).
**Warning signs:** Upcast test `Upcast_V3Fixture_ProducesVersion4` throws on a partial-field fixture.

### Pitfall P3: AI Schema Drift (Snapshot Not Regenerated)
**What goes wrong:** `RecipeSchemaDocumentationProvider.FormatPrompt` is updated (version bump, new fields) but `PromptSnapshotTests.BuildSystemPrompt.verified.txt` is not regenerated. The snapshot test fails in CI. Worse: the prompt is updated but not the POCO or vice versa — the AI schema and the actual RecipeDocument diverge.
**Why it happens:** Developer updates the prompt prose but forgets to accept the snapshot, or commits only one of the two files.
**How to avoid:** In the same task that touches `RecipeSchemaDocumentationProvider.cs`, run `VERIFY_AUTO=true dotnet test tests/CookBot.Tests/ --filter "FullyQualifiedName~BuildSystemPrompt"` and commit the updated `.verified.txt` in the same git commit.
**Warning signs:** `PromptSnapshotTests.BuildSystemPrompt` fails with "received != verified."

### Pitfall P4: `anyOf` Strict-Mode for New Nested Records
**What goes wrong:** `IngredientSubstitution` and `RecipeProvenance` are new object records. `JsonSchemaExporter` emits their schemas as inline object subschemas. If they appear inside an `anyOf` branch (they should not, since they're on non-polymorphic properties), `ExternalizeAnyOfBranches` must handle them.
**Why it happens:** `StepNode` is the ONLY polymorphic type in the current schema — its `anyOf` discriminator is what triggered the `ExternalizeAnyOfBranches` pass. New non-polymorphic records (`IngredientSubstitution`, `RecipeProvenance`) will appear as `properties` entries with `type: "object"` — NOT inside `anyOf`. The `SetAdditionalPropertiesFalse` pass will correctly add `"additionalProperties": false` to them.
**How to avoid:** The existing passes handle this automatically. No code change to `RecipeJsonSchemaProvider`. Confirm by running `SchemaAssertionTests` + adding a new schema assertion test that checks `equipment`, `provenance`, `substitutions`, and `donenessCue` appear in the emitted schema.
**Warning signs:** Anthropic structured-output returns a schema-validation error mentioning `additionalProperties`.

### Pitfall P5: `ParsedRecipe`/`ParsedStep`/`ParsedIngredient` Not Extended
**What goes wrong:** The Domain POCOs are updated but `IRecipeFormatParser.cs` DTOs (`ParsedRecipe`, `ParsedStep`, `ParsedIngredient`) are not. The editor cannot carry the new fields to the save path, and `RecipeFormatParser.ProjectToParsedRecipe` silently drops them.
**Why it happens:** There are two parallel shape systems: `RecipeDocument` (canonical POCO) and `ParsedRecipe` (editor DTO). Both must be updated.
**How to avoid:** The files-to-modify table above lists `IRecipeFormatParser.cs` explicitly. The planner should gate SC2 (round-trip) on this change.
**Warning signs:** SC2 round-trip test passes for serialization but new fields are absent after reload.

### Pitfall P6: `SourceUrl` Rendered as Live Link Without Validation
**What goes wrong:** `RecipeView.razor` renders `Provenance.SourceUrl` as `<a href="@_doc.Provenance.SourceUrl">` without calling `RecipePhotoUrlValidator`. A malicious recipe with `javascript:alert(1)` as the SourceUrl becomes a live XSS vector (even in trusted-LAN posture, defense-in-depth applies).
**How to avoid:** D-12-08 is explicit: call `RecipePhotoUrlValidator.TryValidate` in the view's `@code` block and only render an `<a>` tag if it passes. Otherwise render plain text.
**Warning signs:** RecipeView links to a `javascript:` or `data:` URL without warning.

---

## Test Update Checklist

### Tests to Extend (existing files)

| File | What to Add |
|------|-------------|
| `tests/CookBot.Tests/Recipes/RecipeUpcasterTests.cs` | (a) Update `MakeChain()` at line 16 to include all three upcasters; (b) add `UpcastToCurrent_VersionAlreadyFour_IsIdentity` test; (c) add a v3→v4 gap-detection test using `FakeUpcaster(4,5)` alongside V1→V2, V2→V3, skipping V3→V4. |
| `tests/CookBot.Tests/Recipes/RecipeUpcasterTests.cs` | Update `UpcastToCurrent_VersionGreaterThanCurrent_Throws` — it currently uses `version:999` and expects "newer than current" message. Still passes with `CurrentVersion=4`. No change needed unless test asserts exact version number. |
| `tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt` | Full regen via `VERIFY_AUTO=true dotnet test` after prompt prose change. |

### Tests to Create (new files — exact template)

**`Migration_V3_To_V4_ChainTests.cs`** (copy `Migration_V2_To_V3_ChainTests.cs`):
- `Migration_V3_To_V4_HasCorrectVersionRange` — asserts `FromVersion=3`, `ToVersion=4`
- `RecipeUpcasterChain_CurrentVersion_IsFour` — asserts `RecipeUpcasterChain.CurrentVersion == 4`
- `Migration_V3_To_V4_UpcastsVersionFieldToFour` — minimal v3 node → asserts version=4 after upcast
- `Chain_WithAllThreeUpcasters_UpcastsV1ToV4` — V1→V2→V3→V4 chain integration

**`Migration_V3_To_V4_Tests.cs`** (copy `Migration_V2_To_V3_Tests.cs`):
- `V3ToV4Fixtures()` MemberData — loads all `v3-to-v4-*.json` fixtures from upcaster dir
- `Upcast_V3Fixture_ProducesVersion4` — per-fixture theory, version=4, no throw (SC1, P2)
- `Upcast_NoNewFields_NewFieldsAreNull` — v3 doc with no new fields → doneness/substitutions/equipment/provenance all null/empty after upcast (PITFALL C7 + SC1)
- `Upcast_VersionAlreadyFour_IsIdentity` — identity pass

**Fixture format** (from `v2-to-v3-no-fields.json` — copy pattern):
```json
{
  "version": 3,
  "name": "Simple Bread",
  "servings": 1,
  "tags": [],
  "ingredients": [
    { "id": 1, "name": "Flour", "amount": 500, "unit": "g" }
  ],
  "steps": [
    { "kind": "content", "text": "Mix [flour](#1) with water." }
  ]
}
```

`v3-to-v4-all-present.json` should include all four new field groups in their v3-doc-as-of-before-upcast form (i.e., they would already be there as unknown fields — but since v4 introduces them, a "v3 doc with new fields already present" tests the no-double-application invariant).

### Snapshot Regen Command

```bash
# From project root:
VERIFY_AUTO=true dotnet test tests/CookBot.Tests/ \
  --filter "FullyQualifiedName~PromptSnapshotTests.BuildSystemPrompt"
# Then inspect the .verified.txt diff and commit.
```

`VERIFY_AUTO=true` instructs `Verify.Xunit` to automatically copy `.received.txt` → `.verified.txt` without requiring the DiffEngine GUI. [ASSUMED — based on standard Verify.Xunit documentation; confirm against version 31.12.5 if behavior differs]

### Full Test Run Command

```bash
dotnet test tests/CookBot.Tests/
```

---

## EF Migration Implication: None Required

The v2→v3 bump (Phase 8) confirms this pattern: `photoUrl`, `description`, and per-step `temperature` were added to `RecipeDocument` with NO EF migration. The fields live inside `Recipe.CanonicalDocumentJson` (a `TEXT` column). SQLite stores arbitrary JSON; EF Core never inspects the column contents.

Phase 12 follows the identical pattern. `Equipment`, `Provenance`, `Substitutions`, `DonenessCue` are all inside the JSON blob. No new EF columns. No `dotnet ef migrations add` command.

**Verification:** `src/CookBot.Infrastructure/Migrations/` — the last migration added for v3 fields would be named something like `AddRecipeV3Fields` or similar. Phase 12 adds nothing to this directory. [VERIFIED: direct read of DependencyInjection.cs shows no migration-related code in Application layer; infrastructure migration list not directly read but v2→v3 precedent is authoritative]

---

## UI Surface Details

### `RecipeView.razor` — Insertion Points

**Equipment checklist** — insert after the `Tags` block in the ingredient sidebar (after line ~167):
```razor
@if (_doc.Equipment.Count > 0)
{
    <CbEyebrow><div style="margin-top:14px;margin-bottom:8px;">Equipment</div></CbEyebrow>
    @foreach (var item in _doc.Equipment)
    {
        <div><!-- checkbox, ephemeral state not persisted --><label>@item</label></div>
    }
}
```

**Substitution chips** — insert below each ingredient row in the sidebar loop (after line ~156):
```razor
@if (ing.Substitutions.Count > 0)
{
    <div>@foreach (var sub in ing.Substitutions) { <!-- chip or sub-line --> }</div>
}
```

**Doneness cue** — insert below temperature display in the step loop (after line ~214):
```razor
@if (!string.IsNullOrWhiteSpace(content.DonenessCue))
{
    <div style="margin-top:6px;...">@content.DonenessCue</div>
}
```

**Provenance** — insert in hero section, below the description paragraph (after the `_doc.Description` block ~line 67):
```razor
@if (_doc.Provenance is { } prov && (prov.AuthorName != null || prov.SourceName != null))
{
    <!-- "Adapted from {SourceName} by {AuthorName}" with SourceUrl as link if allowlist-valid -->
}
```

### `RecipeEditor.razor` — Insertion Points

**Equipment** — add as a tag-style input in the right rail meta section (same column as Tags card, after ~line 270).

**Provenance** — add as a form block in the right rail (SourceUrl input, AuthorName input, SourceName input) after the Tags card.

**Substitutions** — add a collapsible sub-row under each ingredient row in the ingredient table (~line 162 area). Minimum: a single text `<input>` for the freeform `Note`; structured fields can be a Phase 12 discretion item.

**Doneness cue** — handled entirely in `RecipeStepEditor.razor`, not in the parent `RecipeEditor.razor`. Add below the `StepTemperaturePicker` block (after line 134 of `RecipeStepEditor.razor`).

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `VERIFY_AUTO=true dotnet test` is the correct env-var invocation for Verify.Xunit 31.12.5 to auto-accept snapshots | Test Update Checklist | Snapshot regen step requires manual file copy instead; low risk, adds 1 manual step |
| A2 | Inline scheme check in `RecipeValidator` is preferred over injecting `RecipePhotoUrlValidator` as a constructor dep | Architecture Patterns §8 | Either approach works; injecting the validator is slightly more testable but adds DI complexity for a 3-line check |
| A3 | No EF migration is needed for Phase 12 (confirmed by v2→v3 precedent but Infrastructure migrations directory not directly read) | EF Migration section | If a migration IS needed, add one for any new EF entity columns — but the pattern strongly confirms no columns are added |

---

## Open Questions (RESOLVED)

> All three were resolved during planning (Phase 12 plans 12-01..12-04); recommendations were adopted.

1. **`RecipeValidator` + `RecipePhotoUrlValidator` dependency**
   - What we know: `RecipeValidator` has no constructor; `RecipePhotoUrlValidator` is registered Singleton.
   - What's unclear: Planner must decide: inject validator (add constructor) or inline the 3-line Uri scheme check.
   - Recommendation: Inline the check. `uri.Scheme is "http" or "https"` is self-contained and avoids a new DI dependency.
   - **RESOLVED:** Inline the scheme check in `RecipeValidator` (no constructor dependency) — adopted in plan 12-01/T3.

2. **`ParsedRecipe` DTOs for provenance and substitutions**
   - What we know: `ParsedRecipe`, `ParsedStep`, `ParsedIngredient` live in `IRecipeFormatParser.cs` and are mutable classes.
   - What's unclear: Whether `RecipeProvenance` should be reused directly as the DTO type, or whether a `ParsedProvenance` class should parallel `ParsedIngredient`.
   - Recommendation: Reuse `RecipeProvenance` record directly on `ParsedRecipe` — it is already a Domain POCO with no framework deps; creating a parallel DTO adds complexity for no benefit.
   - **RESOLVED:** Reuse the Domain `RecipeProvenance` record directly (no `ParsedProvenance`) — adopted in plan 12-02/T1.

3. **`[MaxLength]` caps for new string fields**
   - Claude's Discretion item — planner decides.
   - Recommendation: `DonenessCue` → 512 (prose, but brief); `AuthorName` → 256; `SourceName` → 512; `SourceUrl` → 2048 (matches `PhotoUrl`); `IngredientSubstitution.Note` → 512; `IngredientSubstitution.Name` → 256.
   - **RESOLVED:** Caps adopted as recommended — set in plan 12-01/T1.

---

## Environment Availability

> Step 2.6: SKIPPED (no external dependencies — all implementation is code/config changes on existing stack).

---

## Validation Architecture

> `nyquist_validation` is explicitly `false` in `.planning/config.json`. Section omitted.

---

## Security Domain

**`security_enforcement`** — not explicitly set to `false` in config. Reviewing applicable ASVS categories for Phase 12 scope.

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input Validation | yes | `RecipePhotoUrlValidator` reuse for `SourceUrl` (D-12-08); `[MaxLength]` on new string fields |
| V2 Authentication | no | No auth changes |
| V3 Session Management | no | No session changes |
| V4 Access Control | no | Authorization stays inside `RecipeService` (existing pattern) |
| V6 Cryptography | no | No cryptographic operations |

| Threat Pattern | STRIDE | Standard Mitigation |
|----------------|--------|---------------------|
| `javascript:` / `data:` URL injection via `Provenance.SourceUrl` | Tampering | `RecipePhotoUrlValidator.TryValidate` — render plain text on fail (D-12-08) |
| AI-fabricated authority URLs ("from nytimes.com/recipe") | Spoofing | D-12-09 prompt directive: "never invent a URL or author" |
| Oversized provenance/substitution strings stored to DB | Denial of Service | `[MaxLength]` attributes on all new string fields |

---

## Sources

### Primary (HIGH confidence — direct codebase reads)
- `src/CookBot.Application/Recipes/Migration_V2_To_V3.cs` — literal copy-template for `Migration_V3_To_V4`
- `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` — `CurrentVersion` const location (line 14)
- `src/CookBot.Application/DependencyInjection.cs` — upcaster DI registration block (lines 29–31)
- `src/CookBot.Application/Recipes/RecipeJsonSchemaProvider.cs` — anyOf/additionalProperties behavior (confirmed: existing passes handle new nested records automatically)
- `src/CookBot.Application/Recipes/RecipeValidator.cs` — warning pattern (lines 66–82 temperature block)
- `src/CookBot.Domain/Recipes/RecipeDocument.cs` — POCO conventions (camelCase, `= []` defaults, `Extras`)
- `src/CookBot.Domain/Recipes/IngredientEntry.cs` — `Substitutions` insertion target
- `src/CookBot.Domain/Recipes/StepNode.cs` — `DonenessCue` insertion target on `ContentStep`
- `src/CookBot.Domain/Recipes/StepTemperature.cs` — `DonenessCue` pattern reference (nullable field on `ContentStep`)
- `src/CookBot.Application/Services/RecipePhotoUrlValidator.cs` — reuse surface for D-12-08
- `src/CookBot.Application/Recipes/JsonRecipeSerializer.cs` — round-trip serializer (no change needed)
- `src/CookBot.Application/Services/RecipeFormatParser.cs` — files to modify: `ProjectToParsedRecipe` + inner frontmatter classes
- `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs` — FormatPrompt prose to update
- `src/CookBot.Application/Services/PromptBuilderService.cs` — prompt pipeline (no change; schema doc provider is the hook)
- `src/CookBot.Application/Services/RecipeService.cs` — save path (`CanonicalDocumentJson` set at lines 128, 256)
- `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs` — `ParsedRecipe`/`ParsedStep`/`ParsedIngredient` DTO definitions
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — editor save path (lines 752–829); ingredient loop (118–172); right rail (204+)
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` — per-step UI; temperature picker insertion point (lines 128–134)
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/StepTemperaturePicker.razor` — UI pattern for new per-step controls
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — display surfaces; `_doc: RecipeDocument` reads (lines 149–218)
- `tests/CookBot.Tests/Recipes/Migration_V2_To_V3_Tests.cs` — fixture-matrix test template
- `tests/CookBot.Tests/Recipes/Migration_V2_To_V3_ChainTests.cs` — chain wiring test template
- `tests/CookBot.Tests/Recipes/RecipeUpcasterTests.cs` — gap-detection test to extend (lines 100–107)
- `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` — snapshot test (uses `Verifier.Verify`)
- `tests/CookBot.Tests/ModuleInitializer.cs` — snapshot path config (`Snapshots/` directory)
- `tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt` — current snapshot (version 3, no new fields)
- `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-*.json` — fixture format reference
- `.planning/config.json` — `nyquist_validation: false` confirmed; `commit_docs: true`

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all existing; no new packages
- Architecture: HIGH — direct codebase reads confirm all insertion points and copy-targets
- Pitfalls P1/P2/P3: HIGH — fully grounded in `RecipeUpcasterChain.cs` constructor code and `Migration_V2_To_V3.cs` pattern
- UI surfaces: HIGH — exact insertion points confirmed from Razor file reads
- Snapshot acceptance command: MEDIUM — standard Verify.Xunit pattern, one assumption (A1)

**Research date:** 2026-06-05
**Valid until:** 2026-08-01 (stable codebase; no dependency on external services)
