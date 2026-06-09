# Phase 12: Richer Format + v3→v4 Schema Bump — Pattern Map

**Mapped:** 2026-06-05
**Files analyzed:** 19 new/modified files (5 create, 14 modify)
**Analogs found:** 19 / 19

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `src/CookBot.Domain/Recipes/IngredientSubstitution.cs` | model | transform | `src/CookBot.Domain/Recipes/IngredientEntry.cs` | exact |
| `src/CookBot.Domain/Recipes/RecipeProvenance.cs` | model | transform | `src/CookBot.Domain/Recipes/StepTemperature.cs` | exact |
| `src/CookBot.Domain/Recipes/IngredientEntry.cs` | model | transform | self (add property after line 29) | exact |
| `src/CookBot.Domain/Recipes/StepNode.cs` (ContentStep) | model | transform | self (add property after line 25) | exact |
| `src/CookBot.Domain/Recipes/RecipeDocument.cs` | model | transform | self (add properties after line 44) | exact |
| `src/CookBot.Application/Recipes/Migration_V3_To_V4.cs` | service | transform | `src/CookBot.Application/Recipes/Migration_V2_To_V3.cs` | exact |
| `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` | service | transform | self (line 14 only) | exact |
| `src/CookBot.Application/DependencyInjection.cs` | config | request-response | self (lines 29–31) | exact |
| `src/CookBot.Application/Recipes/RecipeValidator.cs` | service | transform | self (DetectOrphanIngredients/DetectEmptySections pattern) | role-match |
| `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs` | service | request-response | self (FormatPrompt raw string) | exact |
| `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs` | model | transform | self (ParsedRecipe/ParsedStep/ParsedIngredient) | exact |
| `src/CookBot.Application/Services/RecipeFormatParser.cs` | service | transform | self (ProjectToParsedRecipe lines 259–298; frontmatter classes 302–343) | exact |
| `src/CookBot.Application/Recipes/JsonRecipeSerializer.cs` | service | transform | self — NO CHANGE NEEDED | n/a |
| `src/CookBot.Web/Components/Pages/RecipeView.razor` | component | request-response | self (ingredient loop 149–167; step loop 177–218; hero 54–68) | exact |
| `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` | component | request-response | self (temperature picker block 128–134) | exact |
| `src/CookBot.Web/Components/Pages/RecipeEditor.razor` | component | request-response | self (Tags card 270–300; ingredient loop 118–162) | exact |
| `tests/CookBot.Tests/Recipes/Migration_V3_To_V4_Tests.cs` | test | transform | `tests/CookBot.Tests/Recipes/Migration_V2_To_V3_Tests.cs` | exact |
| `tests/CookBot.Tests/Recipes/Migration_V3_To_V4_ChainTests.cs` | test | transform | `tests/CookBot.Tests/Recipes/Migration_V2_To_V3_ChainTests.cs` | exact |
| `tests/CookBot.Tests/Recipes/RecipeUpcasterTests.cs` | test | transform | self (MakeChain line 16; gap test 100–107) | exact |
| `tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt` | test | request-response | self — regenerated via VERIFY_AUTO=true | exact |
| `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-*.json` | test fixture | transform | `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-*.json` | exact |

---

## Pattern Assignments

### `src/CookBot.Domain/Recipes/IngredientSubstitution.cs` (model, new)

**Analog:** `src/CookBot.Domain/Recipes/IngredientEntry.cs` (full file, 31 lines)

**Complete file pattern to copy** (IngredientEntry.cs lines 1–31):
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CookBot.Domain.Recipes;

public sealed record IngredientEntry
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("amount")]
    public double Amount { get; init; }

    [JsonPropertyName("unit")]
    public string Unit { get; init; } = "";

    [JsonPropertyName("note")]
    public string? Note { get; init; }

    /// <summary>Forward-compat: unknown ingredient-level keys round-trip per FORMAT-09.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extras { get; init; } = new();
}
```

**Adapt to produce `IngredientSubstitution.cs` as:**
- `required string Note` — the freeform field (maps to the `required` convention)
- `string? Name`, `double? Amount`, `string? Unit` — all optional structured fields
- `[JsonExtensionData] Dictionary<string, JsonElement> Extras { get; init; } = new();` — FORMAT-09 forward-compat, identical to IngredientEntry
- `[MaxLength]` caps per discretion: `Note` → 512, `Name` → 256 (follow `RecipeDocument.Description=4096` / `PhotoUrl=2048` precedent in `RecipeDocument.cs` lines 30–35)
- Uses `System.ComponentModel.DataAnnotations` for `[MaxLength]` (same import as `RecipeDocument.cs` line 1)

---

### `src/CookBot.Domain/Recipes/RecipeProvenance.cs` (model, new)

**Analog:** `src/CookBot.Domain/Recipes/StepTemperature.cs` (full file, 22 lines) for sealed-record structure; `RecipeDocument.cs` lines 1–2 for `[MaxLength]` import.

**StepTemperature.cs full file** (lines 1–13) — structural template:
```csharp
using System.Text.Json.Serialization;

namespace CookBot.Domain.Recipes;

/// <summary>A per-step oven or hob temperature attached to a <see cref="ContentStep"/>.</summary>
public sealed record StepTemperature
{
    [JsonPropertyName("value")]
    public required decimal Value { get; init; }

    [JsonPropertyName("unit")]
    public required TemperatureUnit Unit { get; init; }
}
```

**Adapt to produce `RecipeProvenance.cs` as:**
- All-optional fields: `string? SourceUrl`, `string? AuthorName`, `string? SourceName` (D-12-07)
- camelCase `[JsonPropertyName]` on each field: `"sourceUrl"`, `"authorName"`, `"sourceName"`
- `[MaxLength]` on each: `SourceUrl` → 2048 (matches `PhotoUrl` in RecipeDocument line 30), `AuthorName` → 256, `SourceName` → 512
- `[JsonExtensionData] Dictionary<string, JsonElement> Extras { get; init; } = new();` — same FORMAT-09 pattern
- Needs both `using System.ComponentModel.DataAnnotations;` and `using System.Text.Json;` and `using System.Text.Json.Serialization;`

---

### `src/CookBot.Domain/Recipes/RecipeDocument.cs` (model, modified)

**Analog:** self — add after existing properties

**Empty-list property pattern** (RecipeDocument.cs lines 37–44):
```csharp
[JsonPropertyName("tags")]
public IReadOnlyList<string> Tags { get; init; } = [];

[JsonPropertyName("ingredients")]
public IReadOnlyList<IngredientEntry> Ingredients { get; init; } = [];

[JsonPropertyName("steps")]
public IReadOnlyList<StepNode> Steps { get; init; } = [];
```

**Nullable string pattern** (RecipeDocument.cs lines 29–35):
```csharp
[JsonPropertyName("photoUrl")]
[MaxLength(2048)]
public string? PhotoUrl { get; init; }

[JsonPropertyName("description")]
[MaxLength(4096)]
public string? Description { get; init; }
```

**Insertions after line 44 (after `Steps`):**
```csharp
[JsonPropertyName("equipment")]
public IReadOnlyList<string> Equipment { get; init; } = [];

[JsonPropertyName("provenance")]
public RecipeProvenance? Provenance { get; init; }
```

`Equipment` follows the empty-list-not-null convention. `Provenance` follows the nullable-record-deserializes-to-null convention (STJ maps absent key → null automatically on `RecipeProvenance?`).

---

### `src/CookBot.Domain/Recipes/IngredientEntry.cs` (model, modified)

**Analog:** self — add before `Extras`

**Existing property before insertion point** (IngredientEntry.cs lines 25–30):
```csharp
[JsonPropertyName("note")]
public string? Note { get; init; }

/// <summary>Forward-compat: unknown ingredient-level keys round-trip per FORMAT-09.</summary>
[JsonExtensionData]
public Dictionary<string, JsonElement> Extras { get; init; } = new();
```

**Insert between `Note` and `Extras`:**
```csharp
[JsonPropertyName("substitutions")]
public IReadOnlyList<IngredientSubstitution> Substitutions { get; init; } = [];
```

Empty-list default (never null) mirrors `Tags`/`Ingredients`/`Steps` on `RecipeDocument`. D-12-02 locks this.

---

### `src/CookBot.Domain/Recipes/StepNode.cs` (model, modified — ContentStep only)

**Analog:** self — add after Temperature on ContentStep

**Existing Temperature property** (StepNode.cs lines 24–25):
```csharp
[JsonPropertyName("temperature")]
public StepTemperature? Temperature { get; init; }
```

**Insert after Temperature, before `Extras`:**
```csharp
[JsonPropertyName("donenessCue")]
public string? DonenessCue { get; init; }
```

`DonenessCue` goes on `ContentStep` ONLY — never on `SectionStep`. Mirrors the `Temperature` nullable precedent. `[MaxLength(512)]` per discretion. Does NOT need `System.ComponentModel.DataAnnotations` import if it is not already present — check StepNode.cs imports first (currently only `System.Text.Json` and `System.Text.Json.Serialization` are imported; add `using System.ComponentModel.DataAnnotations;` if adding `[MaxLength]`).

---

### `src/CookBot.Application/Recipes/Migration_V3_To_V4.cs` (service, new)

**Analog:** `src/CookBot.Application/Recipes/Migration_V2_To_V3.cs` — **verbatim copy-target** (full 58-line file)

**Full Migration_V2_To_V3.cs** (lines 1–58):
```csharp
using System.Linq;
using System.Text.Json.Nodes;

namespace CookBot.Application.Recipes;

/// <summary>
/// JSON-node-level rewrites moving a v2 recipe document to v3. ...
/// Stamps <c>version: 3</c> on completion.
/// </summary>
public sealed class Migration_V2_To_V3 : IRecipeUpcaster
{
    public int FromVersion => 2;
    public int ToVersion => 3;

    public JsonNode Upcast(JsonNode input)
    {
        var obj = input.AsObject();

        // Guard 1: photoUrl absent => stays absent (...)
        // PITFALLS C7 — independent from other guards
        if (obj["photoUrl"] is null) { /* no-op: STJ maps absent -> null on PhotoUrl: string? */ }

        // Guard 2: description absent => stays absent (...)
        // PITFALLS C7 — independent from Guard 1.
        if (obj["description"] is null) { /* no-op: STJ maps absent -> null on Description: string? */ }

        // Guard 3: per-step temperature absent => stays absent (NEVER zero-fill — PITFALLS M2).
        // PITFALLS C7 — independent from Guards 1 and 2.
        if (obj["steps"] is JsonArray steps)
        {
            foreach (var step in steps.OfType<JsonObject>())
            {
                if (step["kind"]?.GetValue<string>() == "content" && step["temperature"] is null)
                {
                    // no-op: ContentStep.Temperature is StepTemperature?; STJ maps absent -> null.
                }
            }
        }

        obj["version"] = 3;
        return obj;
    }
}
```

**Adapt for v3→v4 with four independent guards:**

| Guard # | Field | Pattern |
|---------|-------|---------|
| 1 | `equipment` | `if (obj["equipment"] is null) { /* no-op: IReadOnlyList<string> defaults to [] */ }` |
| 2 | `provenance` | `if (obj["provenance"] is null) { /* no-op: RecipeProvenance? defaults to null */ }` |
| 3 | `donenessCue` per content step | Walk `obj["steps"]` as JsonArray, OfType<JsonObject>(), check `kind=="content"`, guard absent `donenessCue` |
| 4 | `substitutions` per ingredient | Walk `obj["ingredients"]` as JsonArray, OfType<JsonObject>(), guard absent `substitutions` |

Final line: `obj["version"] = 4;`

**CRITICAL:** All four guards are separate `if` blocks — never combined. PITFALL C7 / P2.

---

### `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` (service, modified)

**Analog:** self — single-line change

**Line 14 before/after:**
```csharp
// Before:
public const int CurrentVersion = 3;

// After:
public const int CurrentVersion = 4;
```

**CRITICAL:** This change MUST land in the same atomic task as `Migration_V3_To_V4.cs` creation and its DI registration. Deploying this alone causes a P1 startup crash.

---

### `src/CookBot.Application/DependencyInjection.cs` (config, modified)

**Analog:** self — single-line insertion at line 31

**Current lines 29–31:**
```csharp
services.AddSingleton<IRecipeUpcaster, Migration_V1_To_V2>();
services.AddSingleton<IRecipeUpcaster, Migration_V2_To_V3>();
services.AddSingleton<RecipeUpcasterChain>();
```

**After insertion (new line between 30 and 31):**
```csharp
services.AddSingleton<IRecipeUpcaster, Migration_V1_To_V2>();
services.AddSingleton<IRecipeUpcaster, Migration_V2_To_V3>();
services.AddSingleton<IRecipeUpcaster, Migration_V3_To_V4>();  // Phase 12
services.AddSingleton<RecipeUpcasterChain>();
```

`RecipeUpcasterChain` must remain last so DI fully resolves `IEnumerable<IRecipeUpcaster>` before the chain constructor runs gap-validation.

---

### `src/CookBot.Application/Recipes/RecipeValidator.cs` (service, modified)

**Analog:** self — add two new private warning methods modeled on `DetectOrphanIngredients` / `DetectEmptySections`

**Existing warning method pattern** (RecipeValidator.cs lines 109–136):
```csharp
private static void DetectOrphanIngredients(RecipeDocument doc, List<ValidationWarning> warnings)
{
    if (doc.Ingredients.Count == 0) return;
    // ... logic ...
    warnings.Add(new ValidationWarning(
        Path: $"/ingredients/{i}",
        Code: "OrphanIngredient",
        Message: $"Ingredient '{ing.Name}' (id={ing.Id}) is not referenced by any step."));
}
```

**Existing warning dispatch pattern** (RecipeValidator.cs lines 97–101):
```csharp
// AI-SPEC §1b enhancements (warnings, not errors — do not trigger the repair loop):
DetectOrphanIngredients(doc, warnings);
DetectEmptySections(doc, warnings);

return new ValidationResult(errors, warnings);
```

**Two new warning methods to add (after line 165):**

1. `DetectInvalidProvenanceUrl` — inline Uri scheme check (no new DI dep):
```csharp
private static void DetectInvalidProvenanceUrl(RecipeDocument doc, List<ValidationWarning> warnings)
{
    var url = doc.Provenance?.SourceUrl;
    if (string.IsNullOrWhiteSpace(url)) return;

    if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
    {
        warnings.Add(new ValidationWarning(
            Path: "/provenance/sourceUrl",
            Code: "InvalidProvenanceUrl",
            Message: "Provenance SourceUrl must be an absolute http or https URL."));
    }
}
```

2. `DetectEmptySubstitutions` — per RESEARCH open question #2 (inline, no new dep):
```csharp
private static void DetectEmptySubstitutions(RecipeDocument doc, List<ValidationWarning> warnings)
{
    for (var i = 0; i < doc.Ingredients.Count; i++)
    {
        var subs = doc.Ingredients[i].Substitutions;
        for (var j = 0; j < subs.Count; j++)
        {
            if (string.IsNullOrWhiteSpace(subs[j].Note) && string.IsNullOrWhiteSpace(subs[j].Name))
            {
                warnings.Add(new ValidationWarning(
                    Path: $"/ingredients/{i}/substitutions/{j}",
                    Code: "EmptySubstitution",
                    Message: "Substitution has neither a Note nor a Name."));
            }
        }
    }
}
```

Also dispatch these two from the `Validate` method alongside the existing calls (after line 99, before `return new ValidationResult(...)`).

**Inline scheme check rationale:** `RecipeValidator` currently has no constructor (all methods static helpers). Adding a constructor dep on `RecipePhotoUrlValidator` to reuse 3 lines is over-engineering. The inline `Uri.TryCreate + uri.Scheme is "http" or "https"` check is identical logic (RESEARCH open question #1 resolution: inline).

---

### `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs` (service, modified)

**Analog:** self — update `FormatPrompt` raw string literal (lines 11–53)

**Current FormatPrompt excerpt** (lines 11–50):
```csharp
private const string FormatPrompt = """
    When providing a recipe, emit a fenced code block with this exact JSON shape:

    ```recipe
    {
      "version": 3,
      "name": "Recipe Name",
      ...
      "ingredients": [
        { "id": 1, "name": "ingredient name", "amount": 2, "unit": "cups" },
        { "id": 2, "name": "another ingredient", "amount": 1, "unit": "tbsp", "note": "optional note" }
      ],
      "steps": [
        { "kind": "content", "text": "Step instruction with [ingredient name](#1)." },
        { "kind": "section", "heading": "Section header" },
        { "kind": "content", "text": "Bake for 25 minutes.",
          "timers": [{ "duration": 25, "unit": "min", "label": "bake" }],
          "temperature": { "value": 375, "unit": "F" } }
      ]
    }
    ```

    Use [ingredient name](#id) markdown links in step text to reference ingredients by their per-recipe id.
    ...
    Field guidance:
    - `description`: 1–2 sentences saying what the dish is — no history, no cooking advice.
    - `steps[]`: begin with the first cooking action — do not write an introductory paragraph as step 1.
    ...
    """;
```

**Required mutations:**
1. `"version": 3` → `"version": 4`
2. Add `"equipment": ["stand mixer", "9-inch cake pan"]` at top-level (after `"tags"`)
3. Add `"substitutions": [{"note": "use oat milk for dairy-free"}]` on one ingredient in the example
4. Add `"donenessCue": "golden brown on top and toothpick comes out clean"` on one content step in the example
5. Add `"provenance": null` at top level with the D-12-09 directive
6. Extend "Field guidance" block with D-12-11 instructions (populate `equipment` and `donenessCue` naturally; `substitutions` only when useful; `provenance` null by default)

After this change, run `VERIFY_AUTO=true dotnet test tests/CookBot.Tests/ --filter "FullyQualifiedName~BuildSystemPrompt"` and commit the updated `.verified.txt` in the same change (P3 gate).

---

### `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs` (model, modified)

**Analog:** self — extend existing DTO classes

**FLAG — TWO PARALLEL SHAPE SYSTEMS (PITFALL P5):** `IRecipeFormatParser.cs` defines the editor-side DTOs (`ParsedRecipe`, `ParsedStep`, `ParsedIngredient`). These are a second shape system parallel to `RecipeDocument`. Both must be extended or new fields silently drop at save.

**Current ParsedRecipe** (IRecipeFormatParser.cs lines 5–16):
```csharp
public class ParsedRecipe
{
    public string Name { get; set; } = string.Empty;
    public int Servings { get; set; } = 1;
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<ParsedIngredient> Ingredients { get; set; } = new();
    public List<ParsedStep> Steps { get; set; } = new();
}
```

**Current ParsedStep** (lines 18–24):
```csharp
public class ParsedStep
{
    public string Text { get; set; } = string.Empty;
    public bool IsSection { get; set; }
    public List<ParsedTimer>? Timers { get; set; }
    public StepTemperature? Temperature { get; set; }
}
```

**Current ParsedIngredient** (lines 33–40):
```csharp
public class ParsedIngredient
{
    public int LocalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Amount { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Note { get; set; }
}
```

**Insertions required:**

`ParsedRecipe` — add two properties (follow mutable-class + `List<>` init pattern):
```csharp
public List<string> Equipment { get; set; } = new();
public RecipeProvenance? Provenance { get; set; }
```

`ParsedStep` — add one property (follow `Temperature` nullable pattern):
```csharp
public string? DonenessCue { get; set; }
```

`ParsedIngredient` — add one property (follow mutable-class + `List<>` init pattern):
```csharp
public List<IngredientSubstitution> Substitutions { get; set; } = new();
```

`ParsedRecipe` and `ParsedIngredient` use `List<>` (mutable, not `IReadOnlyList<>`) per the existing convention for editor DTOs. `RecipeProvenance` is reused directly from Domain (RESEARCH open question #2 resolution: no `ParsedProvenance` parallel — Domain POCO has no framework refs). Requires `using CookBot.Domain.Recipes;` — already present on line 1.

---

### `src/CookBot.Application/Services/RecipeFormatParser.cs` (service, modified)

**Analog:** self — extend `ProjectToParsedRecipe` and frontmatter classes

**Current ProjectToParsedRecipe** (RecipeFormatParser.cs lines 259–298):
```csharp
private static ParsedRecipe ProjectToParsedRecipe(RecipeDocument doc) => new()
{
    Name = doc.Name,
    Servings = doc.Servings,
    PrepTimeMinutes = doc.PrepTimeMinutes,
    CookTimeMinutes = doc.CookTimeMinutes,
    PhotoUrl = doc.PhotoUrl,
    Description = doc.Description,
    Tags = doc.Tags.ToList(),
    Ingredients = doc.Ingredients.Select(i => new ParsedIngredient
    {
        LocalId = i.Id,
        Name = i.Name,
        Amount = i.Amount,
        Unit = i.Unit,
        Note = i.Note,
    }).ToList(),
    Steps = doc.Steps.Select(s => s switch
    {
        ContentStep c => new ParsedStep
        {
            Text = c.Text,
            IsSection = false,
            Timers = c.Timers?.Select(t => new ParsedTimer { ... }).ToList(),
            Temperature = c.Temperature,
        },
        SectionStep sec => new ParsedStep { Text = sec.Heading, IsSection = true, Timers = null },
        _ => throw new InvalidOperationException(...)
    }).ToList(),
};
```

**Required additions to `ProjectToParsedRecipe`:**

In the `ParsedRecipe` initializer:
```csharp
Equipment = doc.Equipment.ToList(),
Provenance = doc.Provenance,
```

In the `ParsedIngredient` initializer (inside `.Select(i => new ParsedIngredient { ... })`):
```csharp
Substitutions = i.Substitutions.ToList(),
```

In the `ContentStep` branch (inside `ParsedStep` initializer):
```csharp
DonenessCue = c.DonenessCue,
```

**Current frontmatter classes** (lines 302–343):
```csharp
private class RecipeFrontmatter
{
    public string? Name { get; set; }
    ...
    public List<IngredientFrontmatter>? Ingredients { get; set; }
    public List<StepFrontmatter>? Steps { get; set; }
}

private class IngredientFrontmatter
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public double Amount { get; set; }
    public string? Unit { get; set; }
    public string? Note { get; set; }
}

private class StepFrontmatter
{
    public string? Text { get; set; }
    public string? Section { get; set; }
    public List<TimerFrontmatter>? Timers { get; set; }
    public TemperatureFrontmatter? Temperature { get; set; }
}
```

**Required additions to frontmatter classes:**

`RecipeFrontmatter` — add:
```csharp
public List<string>? Equipment { get; set; }
public RecipeProvenance? Provenance { get; set; }
```

`IngredientFrontmatter` — add:
```csharp
public List<SubstitutionFrontmatter>? Substitutions { get; set; }
```

`StepFrontmatter` — add:
```csharp
public string? DonenessCue { get; set; }
```

Add new inner class `SubstitutionFrontmatter` (follow `TimerFrontmatter` pattern at lines 332–337):
```csharp
private class SubstitutionFrontmatter
{
    public string? Note { get; set; }
    public string? Name { get; set; }
    public double? Amount { get; set; }
    public string? Unit { get; set; }
}
```

Also update the `Serialize(ParsedRecipe recipe)` path that builds `RecipeFrontmatter` from a `ParsedRecipe` to include the new fields — find that construction site (it will be in the `Serialize` method, which builds a `RecipeFrontmatter` from `ParsedRecipe`) and add the four new field mappings.

---

### `src/CookBot.Application/Recipes/JsonRecipeSerializer.cs` (NO CHANGE)

**Confirmed no change needed.** `JsonRecipeSerializer` serializes `RecipeDocument` directly via `JsonSerializer.Serialize(doc, _compact)` (line 42). New POCO properties on `RecipeDocument`, `IngredientEntry`, `ContentStep` with `[JsonPropertyName]` attributes auto-serialize. The `_compact` options use `WhenWritingNull` — absent optional fields are skipped. Empty `IReadOnlyList<>` with `= []` default serializes as `[]` (not null), which is the correct behavior per D-12-05/D-12-02.

---

### `src/CookBot.Web/Components/Pages/RecipeView.razor` (component, modified)

**Analog:** self — four insertion points

**Insertion 1: Provenance credit** — after `_doc.Description` block (around line 67):

Existing pattern (lines 64–67):
```razor
@if (!string.IsNullOrWhiteSpace(_doc.Description))
{
    <p class="recipe-lede" style="font-size:16px;color:var(--ink-2);margin:8px 0 0 0;line-height:1.5;">@_doc.Description</p>
}
```

Add after this block:
```razor
@if (_doc.Provenance is { } prov && (prov.AuthorName != null || prov.SourceName != null))
{
    var credit = BuildProvenanceCredit(prov);
    <div style="margin-top:8px;">
        @if (_validatedSourceUrl != null)
        {
            <a href="@_validatedSourceUrl" target="_blank" rel="noopener noreferrer"
               style="font-size:14px;font-style:italic;color:var(--ink-3);text-decoration:underline;text-underline-offset:3px;">
                @credit
            </a>
        }
        else
        {
            <p style="font-size:14px;font-style:italic;color:var(--ink-3);margin:0;">@credit</p>
        }
    </div>
}
```

`_validatedSourceUrl` is a computed `string?` field in `@code`, set in `OnParametersSet` via `RecipePhotoUrlValidator.TryValidate(prov?.SourceUrl, out var normalized, out _)` — store `normalized` when the call returns true and normalized is non-null. Requires injecting `RecipePhotoUrlValidator` (already registered Singleton in DI).

**Insertion 2: Substitution sub-lines** — inside `@foreach (var ing in _doc.Ingredients)` loop, after the existing ingredient row (around line 156):

Existing ingredient row (lines 151–156):
```razor
<div style="display:flex;gap:12px;padding:10px 0;border-bottom:1px solid var(--line);align-items:baseline;">
    <span class="num" style="flex:0 0 64px;...">@FormatQty(ing)</span>
    <span style="flex:1;min-width:0;...">@ing.Name ...</span>
</div>
```

Add after each ingredient row `<div>`:
```razor
@if (ing.Substitutions.Count > 0)
{
    <div style="padding:4px 0 8px 0;border-bottom:1px solid var(--line);">
        @foreach (var sub in ing.Substitutions)
        {
            <div style="display:flex;align-items:baseline;gap:8px;margin-top:4px;padding-left:8px;">
                <span style="font-size:11px;color:var(--ink-4);flex-shrink:0;">or</span>
                <span style="font-size:13px;color:var(--ink-3);line-height:1.45;">
                    @(sub.Name != null ? $"{FormatSubAmount(sub)} {sub.Name}" : sub.Note)
                    @if (sub.Name != null && sub.Note != null) { <span> — @sub.Note</span> }
                </span>
            </div>
        }
    </div>
}
```

Add `FormatSubAmount` helper in `@code`.

**Insertion 3: Equipment checklist** — after Tags block (around line 167):

Existing Tags block (lines 159–167):
```razor
@if (_doc.Tags.Count > 0)
{
    <div style="display:flex;gap:6px;margin-top:14px;flex-wrap:wrap;">
        @foreach (var tag in _doc.Tags)
        {
            <CbChip Variant="CbChip.CbChipVariant.Tag" Label="@tag" />
        }
    </div>
}
```

Add after this block, before `</aside>`:
```razor
@if (_doc.Equipment.Count > 0)
{
    <CbEyebrow><div style="margin-top:16px;margin-bottom:8px;">Equipment</div></CbEyebrow>
    <ul role="list" aria-label="Equipment list" style="list-style:none;margin:0;padding:0;">
        @foreach (var item in _doc.Equipment)
        {
            var captured = item;
            <li>
                <label class="cb-checkbox">
                    <input type="checkbox"
                           checked="@_checkedEquipment.Contains(captured)"
                           @onchange="@(() => ToggleEquipment(captured))" />
                    <span class="box"></span>
                    <span style="font-size:14px;color:var(--ink-2);">@captured</span>
                </label>
            </li>
        }
    </ul>
}
```

Add `_checkedEquipment: HashSet<string>` field and `ToggleEquipment(string item)` method in `@code`. Checkbox state is ephemeral — not persisted.

**Insertion 4: Doneness cue** — after the temperature display block inside `else if (step is ContentStep content)` (around line 214):

Existing temperature block (lines 205–214):
```razor
@if (content.Temperature != null)
{
    var tempDisplay = _unitMode == "converted" && _unitSystem.HasValue
        ? UnitDisplayService.FormatTemperature(content.Temperature, _unitSystem.Value)
        : $"{content.Temperature.Value}°{content.Temperature.Unit}";
    <div style="margin-top:8px;display:inline-flex;align-items:center;gap:5px;font-size:13px;color:var(--ink-3);font-weight:500;">
        <Icon Name="@Icon.Names.Flame" Size="13" />
        @tempDisplay
    </div>
}
```

Add after the temperature `@if` block, before `</div>` (closing the ContentStep area):
```razor
@if (!string.IsNullOrWhiteSpace(content.DonenessCue))
{
    <div style="margin-top:8px;display:inline-flex;align-items:center;gap:4px;
                font-size:13px;color:var(--ink-3);font-weight:400;font-style:italic;line-height:1.4;">
        <Icon Name="@Icon.Names.Check" Size="13" />
        @content.DonenessCue
    </div>
}
```

---

### `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` (component, modified)

**Analog:** self — one insertion point after the temperature picker block

**Existing temperature picker block** (RecipeStepEditor.razor lines 128–134):
```razor
@if (_kind == StepKind.Step)
{
    <div style="margin-top:2px;">
        <StepTemperaturePicker Temperature="@Step.Temperature"
                               TemperatureChanged="OnTemperatureChanged" />
    </div>
}
```

**Insert after this block (before `</div>` closing the flex column at line 135):**
```razor
@* Phase 12 — per-step doneness cue (alongside Temperature) *@
@if (_kind == StepKind.Step)
{
    <div style="margin-top:2px;">
        <span style="display:inline-flex;align-items:center;gap:8px;font-size:13px;flex-wrap:wrap;">
            <span style="color:var(--ink-3);font-size:11px;text-transform:uppercase;
                         letter-spacing:0.06em;font-weight:600;">
                Doneness
            </span>
            <input type="text"
                   class="cb-input"
                   value="@Step.DonenessCue"
                   placeholder="e.g. golden brown, 165°F internal temp"
                   aria-label="Doneness cue"
                   style="font-size:13px;padding:4px 8px;flex:1;min-width:120px;"
                   @oninput="@(e => OnDonenessCueInput(e))" />
        </span>
    </div>
}
```

**Add handler in `@code`** (mirror `OnTemperatureChanged` at line 264):
```csharp
private Task OnDonenessCueInput(ChangeEventArgs e)
{
    // Mutate-in-place pattern (a) — same as OnTemperatureChanged.
    Step.DonenessCue = e.Value as string;
    return Task.CompletedTask;
}
```

The outer `<span>` with `display:inline-flex;align-items:center;gap:8px;flex-wrap:wrap` exactly mirrors the `StepTemperaturePicker.razor` wrapper style (line 31 of that file). Label style mirrors line 32 of that file.

---

### `src/CookBot.Web/Components/Pages/RecipeEditor.razor` (component, modified)

**Analog:** self — ingredient loop area (lines 118–162) and Tags card (lines 270–300)

**Pattern 1: Tags card** (lines 270–300) — copy for Equipment card:
```razor
<CbCard Padding="18">
    <CbEyebrow><div style="margin-bottom:10px;">Tags</div></CbEyebrow>
    <div style="display:flex;gap:6px;flex-wrap:wrap;margin-bottom:8px;">
        @if (_tags.Count == 0) { <span style="font-size:12.5px;color:var(--ink-3);">No tags yet.</span> }
        else {
            @foreach (var t in _tags.ToList()) {
                var captured = t;
                <span class="cb-chip" style="cursor:default;">
                    @captured
                    <button type="button" aria-label="@($"Remove tag {captured}")"
                            style="background:transparent;border:0;color:inherit;cursor:pointer;
                                   font-size:14px;line-height:1;padding:0 0 0 4px;display:inline-flex;align-items:center;"
                            @onclick="@(() => RemoveTag(captured))">×</button>
                </span>
            }
        }
    </div>
    <input type="text" value="@_tagInput" placeholder="add tag…" aria-label="Add tag"
           style="width:100%;padding:8px 10px;border-radius:8px;border:1px solid var(--line);
                  background:var(--cream);font-family:inherit;font-size:13px;outline:none;"
           @oninput="OnTagInputInput" @onkeydown="OnTagInputKeyDown" />
</CbCard>
```

Equipment card is a near-verbatim copy with:
- Heading "Equipment" instead of "Tags"
- Empty state "No equipment yet." (matches Tags pattern: "No tags yet.")
- Placeholder "add equipment…"
- `class="cb-chip tag"` (vs bare `cb-chip`) — uses the `tag` variant per UI-SPEC
- State fields `_equipment: List<string>`, `_equipmentInput: string`
- Handlers `AddEquipment`, `RemoveEquipment`, `OnEquipmentInputInput`, `OnEquipmentInputKeyDown` — mirror the tag handler signatures

Insert Equipment card after Tags card (after line 300), before AI suggestions card.

**Pattern 2: Substitution sub-rows** — insert inside the ingredient loop (after the existing row `<div>` closing at line 161):

Ingredient loop pattern (lines 118–162) shows a `@for (int i = 0; i < _ingredients.Count; i++)` loop with `var index = i; var ing = _ingredients[index];`. The substitution sub-rows use the same `index` variable:

```razor
@* Phase 12 — substitution sub-rows under each ingredient *@
@if (ing.Substitutions is { Count: > 0 })
{
    @foreach (var (sub, si) in ing.Substitutions.Select((s, idx) => (s, idx)))
    {
        <div style="display:grid;grid-template-columns:1fr 24px;gap:0;
                    background:var(--cream-2);border-bottom:1px solid var(--line);
                    padding:8px 16px 8px 24px;align-items:center;">
            <input type="text" class="cb-input"
                   value="@sub.Note"
                   placeholder="Substitution note (e.g. oat milk for dairy-free)"
                   aria-label="@($"Substitution {si + 1} for {ing.Name}")"
                   style="font-size:13px;padding:4px 8px;"
                   @oninput="@(e => OnSubstitutionNoteInput(index, si, e))" />
            <button type="button" aria-label="Remove substitution"
                    style="background:transparent;border:0;color:var(--ink-4);cursor:pointer;
                           display:flex;align-items:center;justify-content:center;"
                    @onclick="@(() => RemoveSubstitution(index, si))">
                <Icon Name="@Icon.Names.Trash" Size="13" />
            </button>
        </div>
    }
}
<div style="padding:4px 16px 8px 24px;background:var(--cream-2);border-bottom:1px solid var(--line);">
    <button type="button" class="cb-btn ghost"
            style="font-size:12.5px;padding:4px 8px;height:24px;"
            @onclick="@(() => AddSubstitution(index))">
        <Icon Name="@Icon.Names.Plus" Size="11" /> Add substitution
    </button>
</div>
```

Add `@code` methods: `AddSubstitution(int ingIndex)`, `RemoveSubstitution(int ingIndex, int subIndex)`, `OnSubstitutionNoteInput(int ingIndex, int subIndex, ChangeEventArgs e)`.

**Pattern 3: Provenance card** — new `<CbCard>` after Equipment card, modeled on "Times & servings" card (lines 230–268) with `<label class="cb-label">` + `<input class="cb-input">` rows:

```razor
<CbCard Padding="18">
    <CbEyebrow><div style="margin-bottom:8px;">Source &amp; Credit</div></CbEyebrow>
    <div style="display:flex;flex-direction:column;gap:8px;">
        <div>
            <label class="cb-label" for="provenance-source-name">Adapted from</label>
            <input id="provenance-source-name" type="text" class="cb-input"
                   value="@_provSourceName" placeholder="e.g. Smitten Kitchen"
                   aria-label="Source name (adapted from)"
                   @oninput="@(e => _provSourceName = e.Value as string ?? string.Empty)" />
        </div>
        <div>
            <label class="cb-label" for="provenance-author">Author</label>
            <input id="provenance-author" type="text" class="cb-input"
                   value="@_provAuthorName" placeholder="e.g. Deb Perelman"
                   aria-label="Author name"
                   @oninput="@(e => _provAuthorName = e.Value as string ?? string.Empty)" />
        </div>
        <div>
            <label class="cb-label" for="provenance-url">Source URL</label>
            <input id="provenance-url" type="url" class="cb-input"
                   value="@_provSourceUrl" placeholder="https://…"
                   aria-label="Source URL"
                   @oninput="@(e => _provSourceUrl = e.Value as string ?? string.Empty)" />
            @if (_provUrlWarning != null)
            {
                <div role="alert" style="font-size:12.5px;color:var(--warn);margin-top:4px;">
                    @_provUrlWarning
                </div>
            }
        </div>
    </div>
</CbCard>
```

State fields: `_provSourceName`, `_provAuthorName`, `_provSourceUrl` (`string`), `_provUrlWarning` (`string?`). On-blur URL validation via inline scheme check. On save, build `RecipeProvenance?` from these fields and pass through `ParsedRecipe.Provenance`.

**Save path** (RecipeEditor.razor lines 786–805 pattern) — add to the `ParsedRecipe` initializer in `SaveRecipe()`:
```csharp
Equipment = _equipment.ToList(),
Provenance = string.IsNullOrWhiteSpace(_provSourceName) && string.IsNullOrWhiteSpace(_provAuthorName) && string.IsNullOrWhiteSpace(_provSourceUrl)
    ? null
    : new RecipeProvenance { SourceName = NullIfEmpty(_provSourceName), AuthorName = NullIfEmpty(_provAuthorName), SourceUrl = NullIfEmpty(_provSourceUrl) },
```

Also extend `PopulateFromParsed(ParsedRecipe parsed)` (find this method — it loads editor state from a parsed recipe) to populate the new `_equipment`, `_provSourceName`, `_provAuthorName`, `_provSourceUrl` fields.

---

### `tests/CookBot.Tests/Recipes/Migration_V3_To_V4_Tests.cs` (test, new)

**Analog:** `tests/CookBot.Tests/Recipes/Migration_V2_To_V3_Tests.cs` (full 121-line file)

**Copy the entire file** and adapt:
- Class name: `Migration_V3_To_V4_Tests`
- `MakeChain()` at line 25 becomes: `new(new IRecipeUpcaster[] { new Migration_V1_To_V2(), new Migration_V2_To_V3(), new Migration_V3_To_V4() })`
- `V2ToV3Fixtures()` → `V3ToV4Fixtures()` — glob pattern `"v3-to-v4-*.json"`
- `Upcast_V2Fixture_ProducesVersion3` → `Upcast_V3Fixture_ProducesVersion4` — asserts `version==4`
- `Upcast_NoTemperature_ContentStepTemperatureIsNull` → `Upcast_NoNewFields_NewFieldsAreNull` — walks steps for absent `donenessCue` + walks ingredients for absent `substitutions`; checks top-level `equipment` absent; checks `provenance` absent
- `Upcast_VersionAlreadyThree_IsIdentity` → `Upcast_VersionAlreadyFour_IsIdentity` — v4 identity pass
- `ChainConstructor_ThrowsOnGap` — use `FakeUpcaster(4,5)` alongside V1→V2 + V2→V3, leaving 3→4 gap; keep `Assert.Contains("gap", ex.Message)` intact

---

### `tests/CookBot.Tests/Recipes/Migration_V3_To_V4_ChainTests.cs` (test, new)

**Analog:** `tests/CookBot.Tests/Recipes/Migration_V2_To_V3_ChainTests.cs` (full 48-line file)

**Copy the entire file** and adapt:
- Class name: `Migration_V3_To_V4_ChainTests`
- `Migration_V2_To_V3_HasCorrectVersionRange` → `Migration_V3_To_V4_HasCorrectVersionRange` — `new Migration_V3_To_V4(); Assert.Equal(3, FromVersion); Assert.Equal(4, ToVersion)`
- `RecipeUpcasterChain_CurrentVersion_IsThree` → `RecipeUpcasterChain_CurrentVersion_IsFour` — `Assert.Equal(4, RecipeUpcasterChain.CurrentVersion)`
- `Migration_V2_To_V3_UpcastsVersionFieldToThree` → `Migration_V3_To_V4_UpcastsVersionFieldToFour` — input `"version":3`, assert `result["version"] == 4`
- `Chain_WithBothUpcasters_UpcastsV1ToV3` → `Chain_WithAllThreeUpcasters_UpcastsV1ToV4` — chain includes all three upcasters; v1 input → assert `version==4`

---

### `tests/CookBot.Tests/Recipes/RecipeUpcasterTests.cs` (test, modified)

**Analog:** self — two targeted changes

**Change 1: `MakeChain()` at line 16:**
```csharp
// Before:
private static RecipeUpcasterChain MakeChain() =>
    new(new IRecipeUpcaster[] { new Migration_V1_To_V2() });

// After (if these tests still use CurrentVersion=4 as their ceiling):
// NOTE: RecipeUpcasterTests still only tests V1 behavior; MakeChain stays V1-only.
// The gap test at line 100 is the one that must be updated.
```

**Change 2: `RecipeUpcasterChain_GapInVersions_ThrowsAtConstruction` at lines 100–107:**

Current:
```csharp
var fake3to4 = new FakeUpcaster(3, 4);
var ex = Assert.Throws<InvalidOperationException>(() =>
    new RecipeUpcasterChain(new IRecipeUpcaster[] { new Migration_V1_To_V2(), fake3to4 }));
Assert.Contains("gap", ex.Message);
```

The existing test already proves gap detection; it leaves a 2→3 gap which is still a valid gap even at CurrentVersion=4. The test still passes after the bump. However, D-12-13 requires a v3→v4 gap test. Add a second fact:
```csharp
[Fact]
public void RecipeUpcasterChain_GapInVersions_V3ToV4_ThrowsAtConstruction()
{
    // V1→V2, V2→V3 present; V3→V4 absent; fake V4→V5 present to create a 3→4 gap.
    var fake4to5 = new FakeUpcaster(4, 5);
    var ex = Assert.Throws<InvalidOperationException>(() =>
        new RecipeUpcasterChain(new IRecipeUpcaster[]
        {
            new Migration_V1_To_V2(),
            new Migration_V2_To_V3(),
            fake4to5
        }));
    Assert.Contains("gap", ex.Message);
}
```

Also add:
```csharp
[Fact]
public void UpcastToCurrent_VersionAlreadyFour_IsIdentity()
{
    var chain = new RecipeUpcasterChain(new IRecipeUpcaster[]
    {
        new Migration_V1_To_V2(),
        new Migration_V2_To_V3(),
        new Migration_V3_To_V4(),
    });
    var node = JsonNode.Parse("""{"version":4,"name":"X","ingredients":[],"steps":[]}""")!;
    var result = chain.UpcastToCurrent(node);
    Assert.Equal(4, result["version"]!.GetValue<int>());
}
```

---

### Fixture files `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v3-to-v4-*.json` (new)

**Analog:** `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-no-fields.json` and `v2-to-v3-all-present.json`

**`v2-to-v3-no-fields.json`** (verbatim — the base template):
```json
{
  "version": 2,
  "name": "Simple Bread",
  "servings": 1,
  "tags": [],
  "ingredients": [
    { "id": 1, "name": "Flour", "amount": 500, "unit": "g" }
  ],
  "steps": [
    { "kind": "content", "text": "Mix [flour](#1) with water." },
    { "kind": "section", "heading": "Baking" }
  ]
}
```

**`v3-to-v4-no-fields.json`** — same structure with `"version": 3` (no new fields present):
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
    { "kind": "content", "text": "Mix [flour](#1) with water." },
    { "kind": "section", "heading": "Baking" }
  ]
}
```

**`v3-to-v4-all-present.json`** — based on `v2-to-v3-all-present.json` but at version 3, all four field groups present:
```json
{
  "version": 3,
  "name": "Pizza Margherita",
  "servings": 2,
  "equipment": ["pizza stone", "pizza peel"],
  "provenance": { "sourceName": "Bon Appétit", "authorName": "Sarah Jampel", "sourceUrl": "https://example.com/pizza" },
  "tags": ["italian"],
  "ingredients": [
    { "id": 1, "name": "Pizza dough", "amount": 1, "unit": "ball",
      "substitutions": [{"note": "use store-bought if in a rush"}] }
  ],
  "steps": [
    { "kind": "content", "text": "Bake [pizza dough](#1).",
      "temperature": { "value": 250, "unit": "C" },
      "donenessCue": "crust charred and puffed" }
  ]
}
```

Remaining four partial fixtures (`v3-to-v4-substitutions-only.json`, `v3-to-v4-equipment-only.json`, `v3-to-v4-doneness-only.json`, `v3-to-v4-provenance-only.json`) each carry version 3 and include only their named field group — derived from the `all-present` fixture by removing the other three groups.

---

## Shared Patterns

### JsonPropertyName camelCase + JsonExtensionData Extras
**Source:** `src/CookBot.Domain/Recipes/IngredientEntry.cs` lines 13–30
**Apply to:** `IngredientSubstitution.cs`, `RecipeProvenance.cs`
```csharp
[JsonPropertyName("camelCaseFieldName")]
public SomeType FieldName { get; init; }

[JsonExtensionData]
public Dictionary<string, JsonElement> Extras { get; init; } = new();
```

### MaxLength attribute pattern
**Source:** `src/CookBot.Domain/Recipes/RecipeDocument.cs` lines 29–35
**Apply to:** `IngredientSubstitution.Note/Name`, `RecipeProvenance.SourceUrl/AuthorName/SourceName`, `ContentStep.DonenessCue`
```csharp
using System.ComponentModel.DataAnnotations;
// ...
[JsonPropertyName("photoUrl")]
[MaxLength(2048)]
public string? PhotoUrl { get; init; }
```

### Empty-list-not-null property default
**Source:** `src/CookBot.Domain/Recipes/RecipeDocument.cs` lines 37–44
**Apply to:** `RecipeDocument.Equipment`, `IngredientEntry.Substitutions`, `ParsedRecipe.Equipment`, `ParsedIngredient.Substitutions`
```csharp
[JsonPropertyName("tags")]
public IReadOnlyList<string> Tags { get; init; } = [];
```
(Domain POCOs use `IReadOnlyList<>` with `= []`; editor DTOs use `List<>` with `= new()`)

### ValidationWarning emission (never throws, warning-not-error)
**Source:** `src/CookBot.Application/Recipes/RecipeValidator.cs` lines 109–136 (`DetectOrphanIngredients`)
**Apply to:** new `DetectInvalidProvenanceUrl`, `DetectEmptySubstitutions` methods
```csharp
warnings.Add(new ValidationWarning(
    Path: $"/path/to/field",
    Code: "CODE_STRING",
    Message: "Human readable message."));
```

### Upcaster no-op guard (PITFALLS C7)
**Source:** `src/CookBot.Application/Recipes/Migration_V2_To_V3.cs` lines 33–53
**Apply to:** `Migration_V3_To_V4.cs` — all four guards
```csharp
// Each field gets its OWN if block — never combined
if (obj["fieldName"] is null) { /* no-op: STJ maps absent -> null/default */ }
```

### Mutate-in-place Blazor event handler
**Source:** `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipeStepEditor.razor` lines 264–272 (`OnTemperatureChanged`)
**Apply to:** `OnDonenessCueInput` in RecipeStepEditor; `OnSubstitutionNoteInput` in RecipeEditor
```csharp
private Task OnTemperatureChanged(StepTemperature? newTemperature)
{
    Step.Temperature = newTemperature;
    return Task.CompletedTask;
}
```

### Tags card pattern (right-rail chip input)
**Source:** `src/CookBot.Web/Components/Pages/RecipeEditor.razor` lines 270–300
**Apply to:** Equipment card in RecipeEditor right rail (near-verbatim copy)

---

## Special Flags

### FLAG 1 — Two Parallel Shape Systems (PITFALL P5)
The codebase has two parallel shape representations:
- `RecipeDocument` / `IngredientEntry` / `ContentStep` — canonical Domain POCOs
- `ParsedRecipe` / `ParsedIngredient` / `ParsedStep` — editor DTOs in `IRecipeFormatParser.cs`

Both must be updated in the same task. `RecipeFormatParser.ProjectToParsedRecipe` (lines 259–298) is the bridge. If the Domain POCOs gain new fields but `ProjectToParsedRecipe` is not updated, new fields silently drop at save. The executor MUST treat these as one atomic change.

### FLAG 2 — RecipeEditor `PopulateFromParsed` must be found and updated
`RecipeEditor.razor` has a `PopulateFromParsed(ParsedRecipe parsed)` method (used by PasteRawText dialog callback at line 742). Its exact location was not read in the pattern phase — executor must `grep` for it. It loads `_tags`, `_ingredients`, `_steps`, etc. from a parsed recipe. All four new field groups (`_equipment`, `_provSourceName`, `_provAuthorName`, `_provSourceUrl`, and substitutions on each ingredient) must be populated from `parsed` here.

### FLAG 3 — RecipeEditor `SaveRecipe` path must include new fields
The `SaveRecipe()` method builds `ParsedRecipe` at lines 786–805. All four new field groups must be included in the initializer — Equipment, Provenance, and the per-ingredient Substitutions (already carried on `_ingredients[i].Substitutions` since `_ingredients` is `List<ParsedIngredient>`).

### FLAG 4 — RecipeJsonSchemaProvider: no code change, but verify schema assertions
`RecipeJsonSchemaProvider` needs no code changes (confirmed). After Domain POCO additions, the `SetAdditionalPropertiesFalse` and `ExternalizeAnyOfBranches` passes will auto-handle new nested object subschemas (`IngredientSubstitution`, `RecipeProvenance`). The executor should run `SchemaAssertionTests` and confirm the new fields appear in the emitted schema.

---

## No Analog Found

All files have clear analogs. No files require falling back to RESEARCH.md patterns exclusively.

---

## Metadata

**Analog search scope:** `src/CookBot.Domain/Recipes/`, `src/CookBot.Application/Recipes/`, `src/CookBot.Application/Services/`, `src/CookBot.Application/`, `src/CookBot.Web/Components/Pages/`, `tests/CookBot.Tests/Recipes/`, `tests/CookBot.Tests/Fixtures/`
**Files scanned:** 21 source files read directly
**Pattern extraction date:** 2026-06-05
