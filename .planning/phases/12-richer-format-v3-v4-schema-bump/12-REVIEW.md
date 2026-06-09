---
phase: 12-richer-format-v3-v4-schema-bump
reviewed: 2026-06-06T03:07:07Z
depth: standard
files_reviewed: 15
files_reviewed_list:
  - src/CookBot.Domain/Recipes/IngredientSubstitution.cs
  - src/CookBot.Domain/Recipes/RecipeProvenance.cs
  - src/CookBot.Domain/Recipes/RecipeDocument.cs
  - src/CookBot.Domain/Recipes/IngredientEntry.cs
  - src/CookBot.Domain/Recipes/StepNode.cs
  - src/CookBot.Application/Recipes/Migration_V3_To_V4.cs
  - src/CookBot.Application/Recipes/RecipeUpcasterChain.cs
  - src/CookBot.Application/DependencyInjection.cs
  - src/CookBot.Application/Recipes/RecipeValidator.cs
  - src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs
  - src/CookBot.Application/Services/RecipeFormatParser.cs
  - src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs
  - src/CookBot.Web/Components/Pages/RecipeView.razor
  - src/CookBot.Web/Components/Pages/RecipeEditor.razor
  - src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor
findings:
  critical: 3
  warning: 4
  info: 2
  total: 9
status: issues_found
---

# Phase 12: Code Review Report

**Reviewed:** 2026-06-06T03:07:07Z
**Depth:** standard
**Files Reviewed:** 15
**Status:** issues_found

## Summary

Phase 12 ships the v3→v4 schema bump adding four field groups (ingredient substitutions,
recipe-level equipment list, per-step doneness cue, source/provenance). The domain POCOs,
migration, validator, schema documentation provider, and UI surfaces are all present and
generally well-structured. The phase-context invariants for provenance link-safety and
substitution non-scaling are satisfied in the view layer. However, three blockers exist:

1. **`RecipeService` drops all four new v4 fields on every save.** The canonical document
   construction in both `CreateAsync` and `UpdateAsync` was not updated to include
   `Equipment`, `Provenance`, `DonenessCue` (on steps), or `Substitutions` (on
   ingredients). Any recipe saved through the editor loses these fields permanently.

2. **`PopulateFromRecipe` in `RecipeEditor.razor` does not read `Equipment`, `Provenance`,
   `Substitutions`, or `DonenessCue` from the loaded entity / canonical doc.** Editing an
   existing recipe silently clears all four new fields, guaranteeing data loss on the next
   save.

3. **`IngredientSubstitution.Note` is declared `required string` but `SubstitutionFrontmatter.Note`
   is `string?`.** When `RecipeFormatParser.Serialize` writes a substitution with a
   null `Note` (possible if a user bypasses the editor), the YAML round-trip materializes
   null back to the `required` property — STJ will not reject it at deserialization; the
   domain invariant on `required` is silently violated on the parse-back, corrupting the
   object.

---

## Critical Issues

### CR-01: RecipeService drops Equipment, Provenance, DonenessCue, and Substitutions on every save

**File:** `src/CookBot.Application/Services/RecipeService.cs:108-128` (CreateAsync) and `236-256` (UpdateAsync)

**Issue:** Both `CreateAsync` and `UpdateAsync` construct a `RecipeDocument` to write into
`CanonicalDocumentJson`. Neither construction includes the four new v4 fields:

- `Equipment` is omitted entirely (defaults to `[]`).
- `Provenance` is omitted entirely (defaults to `null`).
- The `ContentStep` lambda omits `DonenessCue` (defaults to `null`).
- The `IngredientEntry` selector omits `Substitutions` (defaults to `[]`).

Because `RecipeView` reads exclusively from `CanonicalDocumentJson`, every save through
the editor wipes equipment, provenance, doneness cues, and substitutions, even when the
editor correctly builds them into the `ParsedRecipe` DTO. This is a silent data-loss
defect that the user has no way to detect.

**Fix — both construction sites must mirror all ParsedRecipe fields:**

```csharp
// In RecipeService.CreateAsync and UpdateAsync — update the RecipeDocument constructor:
Ingredients = parsed.Ingredients.Select(i => new IngredientEntry
{
    Id = i.LocalId,
    Name = i.Name,
    Amount = i.Amount,
    Unit = i.Unit,
    Note = i.Note,
    Substitutions = i.Substitutions.ToList(),  // ADD
}).ToList(),
Steps = parsed.Steps.Select<ParsedStep, StepNode>(s => s.IsSection
    ? new SectionStep { Heading = s.Text }
    : new ContentStep
    {
        Text = s.Text,
        Timers = s.Timers?.Select(t => new TimerEntry { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList(),
        Temperature = s.Temperature,
        DonenessCue = s.DonenessCue,  // ADD
    }).ToList(),
Equipment = parsed.Equipment.ToList(),          // ADD
Provenance = parsed.Provenance,                  // ADD
```

---

### CR-02: PopulateFromRecipe in RecipeEditor silently clears all four new v4 fields on edit

**File:** `src/CookBot.Web/Components/Pages/RecipeEditor.razor:568-610`

**Issue:** `PopulateFromRecipe(Recipe recipe)` — the path taken when opening an existing
recipe for edit — reads the relational entity columns but does not read from
`CanonicalDocumentJson`. It therefore never surfaces `Equipment`, `Provenance`,
`Substitutions`, or `DonenessCue`. These fields exist only in the canonical JSON; the
relational entity does not have corresponding columns (confirmed: `RecipeIngredient` has no
`Substitutions` column; `RecipeStep` has no `DonenessCue` column in scope). The edit flow
is: `PopulateFromRecipe` → user makes changes → `SaveRecipe` → `RecipeService.UpdateAsync`.
Any v4 data the recipe already had is silently discarded at load time, so saving propagates
the wipeout even if the user does not touch those fields.

**Fix:** Read from `CanonicalDocumentJson` when available, using the existing
`RecipeSerializer.Deserialize` path (already used in `RecipeView`). Fall back to relational
columns for legacy recipes:

```csharp
private void PopulateFromRecipe(Recipe recipe)
{
    // ... existing scalar field reads (name, description, photoUrl, etc.) ...

    // For v4 fields that live only in the canonical doc, read from JSON:
    RecipeDocument? canonicalDoc = null;
    if (!string.IsNullOrEmpty(recipe.CanonicalDocumentJson))
    {
        try { canonicalDoc = _recipeSerializer.Deserialize(recipe.CanonicalDocumentJson); }
        catch { /* best-effort; proceed with relational data */ }
    }

    _equipment = canonicalDoc?.Equipment.ToList() ?? new List<string>();
    var prov = canonicalDoc?.Provenance;
    _provSourceName = prov?.SourceName ?? string.Empty;
    _provAuthorName = prov?.AuthorName ?? string.Empty;
    _provSourceUrl  = prov?.SourceUrl  ?? string.Empty;

    _ingredients = recipe.RecipeIngredients
        .OrderBy(ri => ri.RecipeLocalId)
        .Select(ri =>
        {
            // Find matching canonical ingredient for Substitutions (relational has none):
            var canonicalIng = canonicalDoc?.Ingredients
                .FirstOrDefault(ci => ci.Id == ri.RecipeLocalId);
            return new ParsedIngredient
            {
                LocalId = ri.RecipeLocalId,
                Name = ri.Ingredient.Name,
                Amount = ri.Amount,
                Unit = ri.Unit,
                Note = ri.Note,
                Substitutions = canonicalIng?.Substitutions.ToList() ?? new List<IngredientSubstitution>(),
            };
        }).ToList();

    _steps = recipe.Steps
        .OrderBy(s => s.Order)
        .Select((s, idx) =>
        {
            var canonicalStep = canonicalDoc?.Steps.ElementAtOrDefault(idx) as ContentStep;
            return new ParsedStep
            {
                Text = s.Text,
                IsSection = s.IsSection,
                Timers = ...,
                Temperature = ...,  // already present
                DonenessCue = canonicalStep?.DonenessCue,
            };
        }).ToList();
}
```

Note: `RecipeSerializer` is already injected in `RecipeView`; it must also be injected into
`RecipeEditor` (or the `RecipeService` must expose a helper that returns a `ParsedRecipe`
from the canonical doc).

---

### CR-03: IngredientSubstitution.Note declared required but SubstitutionFrontmatter.Note is nullable — round-trip violation

**File:** `src/CookBot.Domain/Recipes/IngredientSubstitution.cs:16` and
`src/CookBot.Application/Services/RecipeFormatParser.cs:366`

**Issue:** `IngredientSubstitution` declares `public required string Note { get; init; }` (non-nullable,
required). `SubstitutionFrontmatter` — the intermediate class used by `RecipeFormatParser.Serialize`
— declares `public string? Note { get; set; }`. If a substitution somehow has a null `Note`
in the YAML (e.g. hand-authored YAML omitting the `note` key, or a future code path that
constructs an `IngredientSubstitution` via `with {}` update without copying `Note`), the
YAML serializer writes `note: ` (null), YamlDotNet deserializes it as the C# null, and
`StringToJsonValue` converts it to a null JSON token. STJ then attempts to populate the
`required string Note` property with null. In .NET 10 with NRTs enabled, a `required`
property with a null value from deserialization does **not** throw by default (no
`[DisallowNull]` applied); the property is silently populated with null, violating the
domain invariant. This is a latent defect exposed by the mismatch between the domain type
and the serialization DTO.

Additionally, the `AddSubstitution` method in `RecipeEditor.razor` (line 797) already
creates `new IngredientSubstitution { Note = string.Empty }`, so this path is safe today.
The vulnerability lies in the YAML round-trip path.

**Fix — two options; apply both for defence in depth:**

Option A: Make `SubstitutionFrontmatter.Note` non-nullable to match the domain invariant:
```csharp
private class SubstitutionFrontmatter
{
    public string Note { get; set; } = string.Empty;  // was: string?
    public string? Name { get; set; }
    public double? Amount { get; set; }
    public string? Unit { get; set; }
}
```

Option B: Guard in `ProjectToParsedRecipe` / `RecipeFormatParser.Serialize` mapping:
In `Serialize`, substitute `s.Note ?? string.Empty` when writing `SubstitutionFrontmatter.Note`.

---

## Warnings

### WR-01: IngredientSubstitution.Unit has no MaxLength constraint

**File:** `src/CookBot.Domain/Recipes/IngredientSubstitution.cs:26`

**Issue:** The `Unit` property on `IngredientSubstitution` has no `[MaxLength]` annotation,
while sibling properties `Note` (512) and `Name` (256) both have length constraints. The
analogous `IngredientEntry.Unit` is also unconstrained, but in that case the field is
initialized to `""` and the schema is established. For `IngredientSubstitution`, an AI-
emitted substitution unit string could be arbitrarily long; without a `[MaxLength]` EF Core
cannot generate an appropriate column constraint if this type is ever persisted as an owned
entity, and the validator cannot surface the issue.

**Fix:**
```csharp
[JsonPropertyName("unit")]
[MaxLength(64)]
public string? Unit { get; init; }
```

---

### WR-02: RecipeView provenance credit renders an empty string when SourceUrl is set but both AuthorName and SourceName are null

**File:** `src/CookBot.Web/Components/Pages/RecipeView.razor:71-89`

**Issue:** The outer guard (line 71) checks `prov.AuthorName != null || prov.SourceName != null`.
If a `RecipeProvenance` object has only `SourceUrl` populated (author and source name both
null), this guard is `false` and the credit block is skipped — **but the provenance URL is
still validated and `_validatedSourceUrl` is still assigned in `OnAfterRenderAsync`**. The
URL is computed even though it can never be displayed. This is a logic mismatch: the display
guard and the validation guard are decoupled with no comment explaining the asymmetry.

More importantly, if a future code path reaches `BuildProvenanceCredit` with both names null
(currently blocked by the outer guard), it returns `string.Empty`, which would render an
empty `<a>` element — an invisible but technically present link whose `href` contains the
validated URL.

**Fix:** Align the URL validation guard to mirror the display guard so that `_validatedSourceUrl`
is only ever set when the credit block will actually render. In `OnAfterRenderAsync`:

```csharp
// Only validate the URL when the credit block will render (mirrors line 71 guard).
var prov = _doc.Provenance;
if (prov is { } p &&
    (p.AuthorName != null || p.SourceName != null) &&
    !string.IsNullOrWhiteSpace(p.SourceUrl) &&
    UrlValidator.TryValidate(p.SourceUrl, out var normalized, out _) &&
    normalized != null)
{
    _validatedSourceUrl = normalized;
}
```

---

### WR-03: DetectEmptySubstitutions ignores the domain invariant — Note is required, Name is not

**File:** `src/CookBot.Application/Recipes/RecipeValidator.cs:170`

**Issue:** The validator warns when `string.IsNullOrWhiteSpace(subs[j].Note) && string.IsNullOrWhiteSpace(subs[j].Name)`.
The domain type declares `Note` as `required string`, meaning a substitution is valid if it
has a non-empty `Note` alone. The warning condition is sound for the `required` invariant.
However, the condition checks both fields with `&&` (AND) to fire. This means a substitution
with an empty `Note = ""` and a non-null `Name` does **not** trigger the warning — even though
the `Note` field is semantically required and was emitted as whitespace. An AI that emits
`{"note": "  ", "name": "oat milk"}` will pass validation without warning, despite the
whitespace-only `Note` being a broken domain object.

**Fix:** Warn independently when `Note` is whitespace-only, regardless of `Name`:
```csharp
if (string.IsNullOrWhiteSpace(subs[j].Note))
{
    warnings.Add(new ValidationWarning(
        Path: $"/ingredients/{i}/substitutions/{j}",
        Code: "MissingSubstitutionNote",
        Message: "Substitution Note is required and must not be empty."));
}
```

---

### WR-04: RecipeUpcasterChain.UpcastToCurrent does not detect a version-already-at-current but no matching upcaster scenario for skipped versions

**File:** `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs:37-63`

**Issue:** The chain loop short-circuits when `version == CurrentVersion` (line 44). This is
correct for the happy path. However, consider a recipe node whose stored `version` integer is,
say, `2` and the registered upcasters are `[1→2, 2→3, 3→4]`. After applying `1→2`, version
becomes `2`; the loop continues and applies `2→3`, then `3→4`. This is correct.

The defect is the `continue` path (line 49–51): if a upcaster's `FromVersion` does not match
the current `version`, it is skipped. Because `_upcasters` is ordered by `FromVersion` and
the gap-check in the constructor only validates the `ToVersion → FromVersion` chain between
consecutive registered upcasters, a registration hole (e.g. only `[1→2, 3→4]` registered —
missing `2→3`) would pass the constructor check **if there is only one pair** and then
silently skip the `3→4` upcaster for a v2 document, producing an incorrectly v2-stamped
`RecipeDocument` deserialized against the v4 schema.

Actually, on re-reading: the constructor check `_upcasters[i].ToVersion != _upcasters[i+1].FromVersion`
does catch the `1→2`, `3→4` gap (2 ≠ 3 throws). So this is fine for gap detection at startup.
The real concern is that the `version > CurrentVersion` check fires AFTER the loop — but if a
document has `version = 5` and `CurrentVersion = 4`, the loop finds no matching upcaster
(since none has `FromVersion == 5`), skips all entries, and then hits the post-loop check
which correctly throws. This is a benign path that is correctly handled.

**The actual defect:** When a document enters with `version = 3` and only upcasters `[1→2, 2→3,
3→4]` are registered, everything is fine. But if `Migration_V3_To_V4` is NOT registered (e.g.
a deployment mistake), the loop skips `3→4` (no `FromVersion == 3`), exits, version is still 3,
and the post-loop check `version > CurrentVersion` is false (3 < 4), so no exception is thrown.
The v3 document is then deserialized into the v4 `RecipeDocument` shape — the new v4 fields
will be absent/defaulted, which is safe, but there is **no indication** that upcasting was
incomplete. The chain's post-loop invariant should assert `version == CurrentVersion`:

```csharp
// Replace the current post-loop check:
if (version < CurrentVersion)
{
    throw new InvalidOperationException(
        $"Upcasting stalled at version {version}; no upcaster found for {version}→{version + 1}. " +
        "The chain may have a missing registration.");
}
if (version > CurrentVersion)
{
    throw new InvalidOperationException(
        $"Recipe version {version} is newer than current ({CurrentVersion}). Update the app.");
}
```

---

## Info

### IN-01: RecipeDocument.Name has no MaxLength constraint

**File:** `src/CookBot.Domain/Recipes/RecipeDocument.cs:18`

**Issue:** `RecipeDocument.Name` (the recipe title) carries `required string Name` but no
`[MaxLength]` annotation. The analogous `RecipeProvenance.AuthorName` has `[MaxLength(256)]`.
Without a length cap, an AI-emitted extremely long name can pass schema validation and reach
the DB. This is a pre-existing gap (not introduced by Phase 12) but worth noting since Phase 12
adds sibling constrained fields.

**Fix:** Add `[MaxLength(256)]` to `RecipeDocument.Name`.

---

### IN-02: TODO comment in RecipeView.razor was not resolved

**File:** `src/CookBot.Web/Components/Pages/RecipeView.razor:110`

**Issue:** `@* TODO: surface made-count when RecipeMade log entity lands (FUTURE-Recently-Cooked) *@`
— this comment describes future work but the made-count IS now surfaced (lines 473–479 load
`_madeCount` from `RecipeMadeService`). The comment is stale and describes a state that no
longer exists, which may mislead future readers into thinking the feature is unimplemented.

**Fix:** Remove the TODO comment; the feature it describes has already shipped.

---

_Reviewed: 2026-06-06T03:07:07Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
