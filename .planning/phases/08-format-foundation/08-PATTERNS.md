# Phase 8: Format Foundation - Pattern Map

**Mapped:** 2026-05-15
**Files analyzed:** 31 (15 modified, 13 created, 3 deleted)
**Analogs found:** 28 / 28 non-delete entries
**Phase 1 reference status:** load-bearing — every upcaster/format/migration pattern below is anchored in `.planning/phases/01-canonical-format-foundation/` precedents (Migration_V1_To_V2, RecipeUpcasterChain, RecipeDocument, JsonRecipeSerializer, PromptSnapshotTests, PromptDenylistTests, RecipeConfiguration, DatabaseSeeder).

---

## File Classification

### Created (13)

| File | Role | Data Flow |
|------|------|-----------|
| `src/CookBot.Domain/Recipes/StepTemperature.cs` | Domain POCO (record + enum) | typed value carrier |
| `src/CookBot.Application/Recipes/Migration_V2_To_V3.cs` | Application upcaster | JsonNode transform |
| `src/CookBot.Application/Recipes/Converters/StepTemperatureJsonConverter.cs` | Application JsonConverter | custom serialization |
| `src/CookBot.Domain/Entities/RecipeTag.cs` | Domain entity POCO | relational row |
| `src/CookBot.Infrastructure/Data/Configurations/RecipeTagConfiguration.cs` | EF configuration | fluent mapping |
| `src/CookBot.Infrastructure/Migrations/{ts}_AddRecipePhotoUrlAndDescription.cs` | EF migration | DDL |
| `src/CookBot.Infrastructure/Migrations/{ts}_AddRecipeTagTable.cs` | EF migration + data backfill | DDL + raw SQL |
| `src/CookBot.Infrastructure/Migrations/{ts}_DropTagsJsonColumn.cs` | EF migration | DDL |
| `src/CookBot.Infrastructure/Migrations/{ts}_AddPantryMatchIndexes.cs` | EF migration | DDL (indexes) |
| `tests/CookBot.Tests/ModuleInitializer.cs` | test bootstrap | static init |
| `tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt` | Verify snapshot fixture | reference text |
| `tests/CookBot.Tests/Recipes/Migration_V2_To_V3_Tests.cs` | xUnit Theory test | fixture matrix |
| `tests/CookBot.Tests/Recipes/SchemaAssertionTests.cs` | xUnit fact test | schema introspection |
| `tests/CookBot.Tests/Recipes/StepTemperatureTests.cs` | xUnit Theory test | per-unit validation |
| `tests/CookBot.Tests/Recipes/RecipeTagBackfillTests.cs` | xUnit test | EF in-memory |
| `tests/CookBot.Tests/Fixtures/Recipes/v3-canonical/*.json` | fixture data | reference JSON |
| `tests/CookBot.Tests/Fixtures/Recipes/upcaster/v2-to-v3-*.json` | fixture data | reference JSON |

### Modified (15)

| File | Role | Data Flow |
|------|------|-----------|
| `src/CookBot.Domain/Recipes/RecipeDocument.cs` | Domain record | + 2 nullable props, Version=3 |
| `src/CookBot.Domain/Recipes/StepNode.cs` | Domain record | + Temperature on ContentStep |
| `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` | Application service | bump CurrentVersion |
| `src/CookBot.Application/Recipes/RecipeJsonSchemaProvider.cs` | Application service | regenerates (verify only) |
| `src/CookBot.Application/Recipes/RecipeValidator.cs` | Application validator | + per-unit temperature rules |
| `src/CookBot.Application/Services/RecipeFormatParser.cs` | Application parser | YAML/JSON round-trip |
| `src/CookBot.Application/Services/JsonRecipeSerializer.cs` | Application serializer | indented gas half-stop |
| `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs` | Application prose | schema example update |
| `src/CookBot.Application/Services/PromptBuilderService.cs` | Application service | denylist regex extension callsite |
| `src/CookBot.Application/Services/RecipeService.cs` | Application service | drop IRecipeProjector + relational tag write |
| `src/CookBot.Domain/Entities/Recipe.cs` | Domain entity | + PhotoUrl, Description, drop TagsJson |
| `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` | EF context | + DbSet<RecipeTag> |
| `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs` | EF configuration | new column lengths, drop TagsJson default |
| `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` | EF seeder | + null-canonical guard, drop projector arg |
| `src/CookBot.Application/DependencyInjection.cs` | Composition root | + Migration_V2_To_V3 registration |
| `src/CookBot.Infrastructure/DependencyInjection.cs` | Composition root | drop LegacyRecipeProjector registration |
| `src/CookBot.Web/Components/Pages/RecipeEditor.razor` | Razor page | relational tag read at line 420 |
| `src/CookBot.Web/Services/CookbookTransferService.cs` | Web service | relational tag read at line 71 |
| `src/CookBot.Application/Services/RecipeCookingAiContext.cs` | Application service | relational tag read at line 19 |
| `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` | xUnit test (REPLACED) | Verify-based snapshot |
| `tests/CookBot.Tests/Prompts/PromptDenylistTests.cs` | xUnit test | extend denylist regex |
| `tests/CookBot.Tests/CookBot.Tests.csproj` | csproj | + Verify.Xunit 31.12.5 |
| `README.md` | docs | + "Recipe Format" section |

### Deleted (3)

| File | Rationale |
|------|-----------|
| `src/CookBot.Application/Recipes/IRecipeProjector.cs` | CLEAN-01 (Phase 1 `DELETE-AFTER-V1.1`) |
| `src/CookBot.Infrastructure/Data/Migrations/Helpers/LegacyRecipeProjector.cs` | CLEAN-01 (Phase 1 `DELETE-AFTER-V1.1`) |
| `tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt` | D-35 (Verify replaces hand-rolled fixture) |

---

## Pattern Assignments

### Domain layer

#### `src/CookBot.Domain/Recipes/StepTemperature.cs` (create)

- **Role:** Domain POCO (record + enum, no framework deps)
- **Closest analog:** `src/CookBot.Domain/Recipes/TimerEntry.cs:6-16`
- **Excerpt:**
  ```csharp
  using System.Text.Json.Serialization;

  namespace CookBot.Domain.Recipes;

  /// <summary>A timer attached to a <see cref="ContentStep"/>: a duration with a unit and optional label.</summary>
  public sealed record TimerEntry
  {
      [JsonPropertyName("duration")]
      public required int Duration { get; init; }

      [JsonPropertyName("unit")]
      public string Unit { get; init; } = "min";

      [JsonPropertyName("label")]
      public string? Label { get; init; }
  }
  ```
- **Apply pattern:** Copy verbatim shape — `sealed record`, `[JsonPropertyName]` on every property, `required` on the load-bearing one. `StepTemperature(decimal Value, TemperatureUnit Unit)` per D-27; co-locate the `enum TemperatureUnit { F, C, Gas }` in the same file (TimerEntry has no companion enum but `StepNode`'s discriminator shows enum-style discriminators are kept tight to the record). No `Microsoft.*` references, no EF, no `JsonExtensionData` (this is a leaf value object — only the parent `ContentStep` and `RecipeDocument` carry `Extras`).

---

#### `src/CookBot.Domain/Recipes/RecipeDocument.cs` (modify)

- **Role:** Domain record — canonical doc, source of truth across all formats
- **Closest analog (self):** `src/CookBot.Domain/Recipes/RecipeDocument.cs:11-40`
- **Excerpt (current v2 shape — extend, do not restructure):**
  ```csharp
  public sealed record RecipeDocument
  {
      [JsonPropertyName("version")]
      public required int Version { get; init; }

      [JsonPropertyName("name")]
      public required string Name { get; init; }

      [JsonPropertyName("servings")]
      public int Servings { get; init; } = 1;

      [JsonPropertyName("prepTimeMinutes")]
      public int? PrepTimeMinutes { get; init; }
      // ... tags, ingredients, steps ...

      [JsonExtensionData]
      public Dictionary<string, JsonElement> Extras { get; init; } = new();
  }
  ```
- **Apply pattern:** Insert `[JsonPropertyName("photoUrl")] public string? PhotoUrl { get; init; }` and `[JsonPropertyName("description")] public string? Description { get; init; }` next to the other nullables (after `CookTimeMinutes`, before `Tags`). Per Discretion: add `[MaxLength(2048)]` / `[MaxLength(4096)]` so `JsonSchemaExporter` surfaces `maxLength` to Anthropic. `JsonRecipeSerializer` already uses `WhenWritingNull` (see `JsonRecipeSerializer.cs:26`), so v2 docs round-trip without emitting the new keys. **Do not** touch `Version` default — bump only the upcaster's `CurrentVersion` constant per D-30 (V1 / V2 fixtures still decode as int 1 or 2 via the `required` field).

---

#### `src/CookBot.Domain/Recipes/StepNode.cs` (modify — add `Temperature` to `ContentStep`)

- **Role:** Domain polymorphic record
- **Closest analog (self):** `src/CookBot.Domain/Recipes/StepNode.cs:16-27`
- **Excerpt:**
  ```csharp
  /// <summary>An instruction step with prose text and an optional timer list.</summary>
  public sealed record ContentStep : StepNode
  {
      [JsonPropertyName("text")]
      public required string Text { get; init; }

      [JsonPropertyName("timers")]
      public IReadOnlyList<TimerEntry>? Timers { get; init; }

      /// <summary>Forward-compat: unknown step-level keys round-trip per FORMAT-09.</summary>
      [JsonExtensionData]
      public Dictionary<string, JsonElement> Extras { get; init; } = new();
  }
  ```
- **Apply pattern:** Add `[JsonPropertyName("temperature")] public StepTemperature? Temperature { get; init; }` between `Timers` and `Extras` (preserve nullable-collection-then-scalar ordering). Nullable per PITFALLS C7/M2 — null-fill, never zero-fill. Leave `SectionStep` untouched (sections never carry temperature).

---

#### `src/CookBot.Domain/Entities/Recipe.cs` (modify)

- **Role:** EF entity POCO (mutable class, get/set, default-initialized)
- **Closest analog (self):** `src/CookBot.Domain/Entities/Recipe.cs:3-23`
- **Excerpt:**
  ```csharp
  public class Recipe
  {
      public int Id { get; set; }
      public int CookbookId { get; set; }
      public string Name { get; set; } = string.Empty;
      public int Servings { get; set; }
      public int? PrepTimeMinutes { get; set; }
      public int? CookTimeMinutes { get; set; }
      public string TagsJson { get; set; } = "[]";
      // ...
      public string? CanonicalDocumentJson { get; set; }

      public Cookbook Cookbook { get; set; } = null!;
      public List<RecipeStep> Steps { get; set; } = new();
      public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
  }
  ```
- **Apply pattern:** Insert `public string? PhotoUrl { get; set; }` and `public string? Description { get; set; }` next to `CanonicalDocumentJson` (other `string?` fields cluster there). Delete `TagsJson` only in the second migration's source change (D-26 sequencing); the column survives the first migration so backfill can read it. Add navigation `public ICollection<RecipeTag> Tags { get; set; } = new List<RecipeTag>();` per the existing pattern on line 22.

---

#### `src/CookBot.Domain/Entities/RecipeTag.cs` (create)

- **Role:** Domain entity POCO (composite-keyed child of `Recipe`)
- **Closest analog:** `src/CookBot.Domain/Entities/AiApiKeyShare.cs:6-15`
- **Excerpt:**
  ```csharp
  /// <summary>
  /// Grants <see cref="RecipientUserId"/> the ability to use <see cref="OwnerUserId"/>'s Anthropic API key (server-side only).
  /// </summary>
  public class AiApiKeyShare
  {
      public int Id { get; set; }
      public int OwnerUserId { get; set; }
      public int RecipientUserId { get; set; }
      public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

      public User Owner { get; set; } = null!;
      public User Recipient { get; set; } = null!;
  }
  ```
- **Apply pattern:** Mirror the shape: `Id`, `RecipeId` (FK), `Name` (max 200 — set in configuration). One navigation back: `public Recipe Recipe { get; set; } = null!;`. Match the XML doc-comment style — single sentence describing purpose. No `CreatedAt` (tags are not audited). Constructor default `Name = string.Empty;` if you want to avoid `required`.

---

### Application layer

#### `src/CookBot.Application/Recipes/Migration_V2_To_V3.cs` (create)

- **Role:** Application upcaster — implements `IRecipeUpcaster`, registered Singleton
- **Closest analog:** `src/CookBot.Application/Recipes/Migration_V1_To_V2.cs:18-83`
- **Excerpt:**
  ```csharp
  public sealed class Migration_V1_To_V2 : IRecipeUpcaster
  {
      public int FromVersion => 1;
      public int ToVersion => 2;

      public JsonNode Upcast(JsonNode input)
      {
          var obj = input.AsObject();

          // 1. Time-field rename (units in field name; Pitfall C2 / FORMAT-03)
          RenameKey(obj, "prepTime", "prepTimeMinutes");
          RenameKey(obj, "cookTime", "cookTimeMinutes");

          // 2. ingredients[].localId -> ingredients[].id (D-06)
          if (obj["ingredients"] is JsonArray ings)
          {
              foreach (var ing in ings.OfType<JsonObject>())
              {
                  RenameKey(ing, "localId", "id");
              }
          }
          // ... step rebuild logic ...
          obj["version"] = 2;
          return obj;
      }
  }
  ```
- **Apply pattern:** Per D-29, single class, **null-coalescing per field** (NOT bundled try/throw). Structure for V2→V3:
  ```csharp
  public sealed class Migration_V2_To_V3 : IRecipeUpcaster
  {
      public int FromVersion => 2;
      public int ToVersion => 3;
      public JsonNode Upcast(JsonNode input)
      {
          var obj = input.AsObject();
          // Each guard is INDEPENDENT — partial failure of one cannot break the others (PITFALLS C7).
          // photoUrl: leave absent if not present; deserializer maps absent -> null (PITFALLS M2).
          // description: same.
          // For each ContentStep: leave temperature absent if not present (never zero-fill).
          obj["version"] = 3;
          return obj;
      }
  }
  ```
  Per ARCHITECTURE.md §"V2→V3 upcaster default values", actual null-injection is **unnecessary** — STJ maps absent keys to nullable `null` and `JsonRecipeSerializer` already uses `WhenWritingNull`. So the body is effectively a version stamp. Keep the three per-field comments anyway as the documented contract per D-29 ("three independent `if (root["photoUrl"] is null) ...` style guards"); the planner may choose to make them explicit null-set guards if the test fixture matrix prefers asserting on present-but-null. Match the XML-doc summary block style from `Migration_V1_To_V2.cs:5-17` (use `<list type="bullet">`).

---

#### `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` (modify)

- **Role:** Application orchestrator — registered Singleton
- **Closest analog (self):** `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs:11-30`
- **Excerpt:**
  ```csharp
  public sealed class RecipeUpcasterChain
  {
      /// <summary>Latest <see cref="Domain.Recipes.RecipeDocument.Version"/> the app understands.</summary>
      public const int CurrentVersion = 2;

      private readonly IReadOnlyList<IRecipeUpcaster> _upcasters;

      public RecipeUpcasterChain(IEnumerable<IRecipeUpcaster> upcasters)
      {
          _upcasters = upcasters.OrderBy(u => u.FromVersion).ToList();

          for (int i = 0; i < _upcasters.Count - 1; i++)
          {
              if (_upcasters[i].ToVersion != _upcasters[i + 1].FromVersion)
              {
                  throw new InvalidOperationException(
                      $"Upcaster chain has a gap: {_upcasters[i].ToVersion} -> {_upcasters[i + 1].FromVersion}");
              }
          }
      }
  }
  ```
- **Apply pattern:** Single-line change: `public const int CurrentVersion = 3;`. The gap-detection loop already validates the chain at construction — when `Migration_V2_To_V3` is registered alongside `Migration_V1_To_V2`, the constructor verifies `1→2→3` covers every step. No other edits.

---

#### `src/CookBot.Application/DependencyInjection.cs` (modify)

- **Role:** Composition extension
- **Closest analog (self):** `src/CookBot.Application/DependencyInjection.cs:22-27`
- **Excerpt:**
  ```csharp
  // Phase 1 canonical-format scaffold (Plan 01-01). Stateless pure services -> Singleton.
  services.AddSingleton<IRecipeSchemaDocumentationProvider, RecipeSchemaDocumentationProvider>();
  services.AddSingleton<RecipeJsonSchemaProvider>();
  services.AddSingleton<RecipeValidator>();
  services.AddSingleton<JsonRecipeSerializer>();
  services.AddSingleton<IRecipeUpcaster, Migration_V1_To_V2>();
  services.AddSingleton<RecipeUpcasterChain>();
  ```
- **Apply pattern:** Add one line directly under the existing Migration_V1_To_V2 registration: `services.AddSingleton<IRecipeUpcaster, Migration_V2_To_V3>();`. MS DI collects multiple registrations of the same service into the constructor's `IEnumerable<IRecipeUpcaster>` (already consumed in `RecipeUpcasterChain(IEnumerable<IRecipeUpcaster> upcasters)`). No registration for `StepTemperatureJsonConverter` here — JsonConverters are not DI-registered; they're added via `JsonSerializerOptions.Converters` inside `JsonRecipeSerializer`'s constructor.

---

#### `src/CookBot.Application/Recipes/Converters/StepTemperatureJsonConverter.cs` (create)

- **Role:** Application JsonConverter — gas half-stop rendering for human-readable JSON only
- **Closest analog:** No project-local custom JsonConverters exist; `src/CookBot.Application/Recipes/JsonRecipeSerializer.cs:22-33` shows the surrounding wire-up pattern
- **Excerpt:**
  ```csharp
  public JsonRecipeSerializer()
  {
      _compact = new JsonSerializerOptions
      {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
      };

      _indented = new JsonSerializerOptions(_compact)
      {
          WriteIndented = true,
      };
  }
  ```
- **Apply pattern:** Subclass `JsonConverter<StepTemperature>`. Per D-27, the **compact** wire format always writes `{ "value": 4.5, "unit": "gas" }` (default STJ behavior — no custom converter needed there). The converter applies **only** to `_indented` for `SerializeIndented` output, replacing `4.5` with the string `"4½"` for gas half-stops. Register on `_indented.Converters.Add(new StepTemperatureJsonConverter())` inside `JsonRecipeSerializer`'s constructor. Implement `Read` as pass-through (delegate to default deserialization shape) and `Write` to switch on `value.Unit == TemperatureUnit.Gas && value.Value % 1m != 0m` and write `"4½"` / `"7½"` strings; otherwise emit the standard `{value, unit}` object. Keep namespace `CookBot.Application.Recipes.Converters` per the new directory.

---

#### `src/CookBot.Application/Recipes/RecipeValidator.cs` (modify — add temperature rules)

- **Role:** Application validator — never throws, returns `ValidationResult`
- **Closest analog (self):** `src/CookBot.Application/Recipes/RecipeValidator.cs:50-78`
- **Excerpt:**
  ```csharp
  for (int i = 0; i < doc.Steps.Count; i++)
  {
      switch (doc.Steps[i])
      {
          case ContentStep content:
              foreach (Match m in IngredientLinkPatterns.Pattern.Matches(content.Text))
              {
                  var idText = m.Groups[2].Value;
                  if (!int.TryParse(idText, out var refId) || !ids.Contains(refId))
                  {
                      errors.Add(new ValidationError(
                          $"/steps/{i}/text",
                          "DANGLING_REF",
                          $"Step references ingredient #{idText} which is not in ingredients."));
                  }
              }
              break;

          case SectionStep section:
              if (string.IsNullOrWhiteSpace(section.Heading))
              {
                  errors.Add(new ValidationError(
                      $"/steps/{i}/heading", "REQUIRED", "Section heading is required."));
              }
              break;
      }
  }
  ```
- **Apply pattern:** Inside the existing `ContentStep` branch, after the existing markdown-link loop, add a guard:
  ```csharp
  if (content.Temperature is { } temp)
  {
      switch (temp.Unit)
      {
          case TemperatureUnit.F:
          case TemperatureUnit.C:
              if (temp.Value != Math.Truncate(temp.Value))
                  errors.Add(new ValidationError($"/steps/{i}/temperature/value",
                      "INVALID_TEMPERATURE", $"{temp.Unit} temperature must be whole-degree."));
              break;
          case TemperatureUnit.Gas:
              if (temp.Value % 0.5m != 0m || temp.Value < 1.0m || temp.Value > 9.5m)
                  errors.Add(new ValidationError($"/steps/{i}/temperature/value",
                      "INVALID_TEMPERATURE", "Gas mark must be a 0.5-step value in [1.0, 9.5]."));
              break;
      }
  }
  ```
  Use error code `INVALID_TEMPERATURE` consistent with existing codes (`REQUIRED`, `OUT_OF_RANGE`, `DUPLICATE_ID`, `DANGLING_REF`). Per D-27 these are **errors**, not warnings (validator's `IsValid` flips, triggering the repair loop).

---

#### `src/CookBot.Application/Services/RecipeFormatParser.cs` (modify — YAML/JSON for new fields)

- **Role:** Application parser — routes YAML/JSON through the upcaster chain
- **Closest analog (self):** `src/CookBot.Application/Services/RecipeFormatParser.cs:135-165, 292-324`
- **Excerpt (current YAML serialization shape):**
  ```csharp
  var frontmatter = new RecipeFrontmatter
  {
      Name = recipe.Name,
      Servings = recipe.Servings,
      PrepTime = recipe.PrepTimeMinutes,
      CookTime = recipe.CookTimeMinutes,
      Tags = recipe.Tags.Any() ? recipe.Tags : null,
      Ingredients = recipe.Ingredients.Select(i => new IngredientFrontmatter { ... }).ToList(),
      Steps = recipe.Steps.Select(s => s.IsSection
          ? new StepFrontmatter { Section = s.Text }
          : new StepFrontmatter { Text = s.Text, Timers = ... }
      ).ToList(),
  };
  // ...
  private class RecipeFrontmatter
  {
      public string? Name { get; set; }
      public int Servings { get; set; } = 1;
      public int? PrepTime { get; set; }
      public int? CookTime { get; set; }
      // ...
  }

  private class StepFrontmatter
  {
      public string? Text { get; set; }
      public string? Section { get; set; }
      public List<TimerFrontmatter>? Timers { get; set; }
  }
  ```
- **Apply pattern:** Add `public string? PhotoUrl { get; set; }` and `public string? Description { get; set; }` to `RecipeFrontmatter`; YamlDotNet's `OmitNull` (line 57 — `ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)`) handles backward compat automatically. Add `public TimerFrontmatter? Temperature { get; set; }` … wait — separate inner class: introduce `private class TemperatureFrontmatter { public decimal Value { get; set; } public string? Unit { get; set; } }` and `public TemperatureFrontmatter? Temperature { get; set; }` on `StepFrontmatter`. YAML wire format per D-27 stores `temperature: { value: 4.5, unit: "gas" }` — no half-stop string in YAML. Then extend `frontmatter.Steps.Select` to populate it from `s.Temperature` (need to extend `ParsedStep` / `ParsedRecipe` similarly — see `ProjectToParsedRecipe` at line 252 and the corresponding `ParsedStep` POCO).

---

#### `src/CookBot.Application/Services/JsonRecipeSerializer.cs` (modify — wire converter)

- **Role:** Application serializer
- **Closest analog (self):** lines 22-33 (shown above under StepTemperatureJsonConverter)
- **Apply pattern:** In `_indented` initializer (only), append `Converters = { new StepTemperatureJsonConverter() }` so `SerializeIndented` renders gas half-stops as `"4½"`. **Do not** add the converter to `_compact` — the SQLite-stored canonical JSON and wire format always use `{ value, unit }`. No structural changes to `Serialize`/`Deserialize`.

---

#### `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs` (modify — schema prose example)

- **Role:** AI prompt schema example
- **Closest analog (self):** `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs:10-41`
- **Excerpt:**
  ```csharp
  private const string FormatPrompt = """
      When providing a recipe, emit a fenced code block with this exact JSON shape:

      ```recipe
      {
        "version": 2,
        "name": "Recipe Name",
        ...
        "steps": [
          { "kind": "content", "text": "Step instruction with [ingredient name](#1)." },
          { "kind": "section", "heading": "Section header" },
          { "kind": "content", "text": "Bake for 25 minutes.",
            "timers": [{ "duration": 25, "unit": "min", "label": "bake" }] }
        ]
      }
      ```
      ...
      If you cannot emit a recipe in the structured format, ask the user a clarifying question instead.
      """;
  ```
- **Apply pattern:** Bump example `"version": 2` → `"version": 3`. Add `"photoUrl"`, `"description"` at the top object level (matching positions in the C# record). Extend one of the content steps to include `"temperature": { "value": 375, "unit": "F" }`. Keep the closing strict-mode directive untouched — D-22 prohibits opt-out language, and D-36 keeps the `PromptDenylistTests` regex (which scans this exact file per line 20 of `PromptDenylistTests.cs`) live.

---

#### `src/CookBot.Application/Services/RecipeService.cs` (modify — drop projector + relational tag writes)

- **Role:** Application service
- **Closest analog (self):** `src/CookBot.Application/Services/RecipeService.cs:17-94`
- **Excerpt (current constructor + CreateAsync persistence):**
  ```csharp
  public RecipeService(
      IRecipeFormatParser parser,
      IRepository<Recipe> recipeRepo,
      IRepository<Ingredient> ingredientRepo,
      IRepository<Cookbook> cookbookRepo,
      IRecipeProjector projector,
      JsonRecipeSerializer canonicalSerializer)
  {
      _parser = parser;
      _recipeRepo = recipeRepo;
      _ingredientRepo = ingredientRepo;
      _cookbookRepo = cookbookRepo;
      _projector = projector;
      _canonicalSerializer = canonicalSerializer;
  }

  // ... in CreateAsync:
  var recipe = new Recipe
  {
      CookbookId = cookbookId,
      Name = parsed.Name,
      Servings = parsed.Servings,
      PrepTimeMinutes = parsed.PrepTimeMinutes,
      CookTimeMinutes = parsed.CookTimeMinutes,
      TagsJson = JsonSerializer.Serialize(parsed.Tags),
  };
  // ...
  var canonicalDoc = _projector.Project(recipe);
  recipe.CanonicalDocumentJson = _canonicalSerializer.Serialize(canonicalDoc);
  ```
- **Apply pattern (CLEAN-01 step b):** Remove `IRecipeProjector projector` from the constructor, drop the `_projector` field. Replace the `var canonicalDoc = _projector.Project(recipe);` call with direct construction from `parsed` (the `ParsedRecipe` already carries the canonical data — see `ProjectToParsedRecipe` at `RecipeFormatParser.cs:252`, which is the inverse projection). Concretely:
  ```csharp
  var canonicalDoc = new RecipeDocument
  {
      Version = RecipeUpcasterChain.CurrentVersion,
      Name = parsed.Name,
      Servings = parsed.Servings,
      PrepTimeMinutes = parsed.PrepTimeMinutes,
      CookTimeMinutes = parsed.CookTimeMinutes,
      Tags = parsed.Tags.ToList(),
      Ingredients = parsed.Ingredients.Select(i => new IngredientEntry { Id = i.LocalId, Name = i.Name, Amount = i.Amount, Unit = i.Unit, Note = i.Note }).ToList(),
      Steps = parsed.Steps.Select<ParsedStep, StepNode>(s => s.IsSection
          ? new SectionStep { Heading = s.Text }
          : new ContentStep { Text = s.Text, Timers = s.Timers?.Select(t => new TimerEntry { Duration = t.Duration, Unit = t.Unit, Label = t.Label }).ToList(), Temperature = s.Temperature }).ToList(),
  };
  recipe.CanonicalDocumentJson = _canonicalSerializer.Serialize(canonicalDoc);
  ```
  **CLEAN-02 step:** drop `TagsJson = JsonSerializer.Serialize(parsed.Tags)` and the matching `recipe.TagsJson = ...` at line 117. Replace with `recipe.Tags.Clear(); foreach (var name in parsed.Tags.Select(t => t.Trim()).Where(t => t.Length > 0)) recipe.Tags.Add(new RecipeTag { Name = name });` per D-34's trim+preserve-case rule.

---

#### `src/CookBot.Application/Services/RecipeCookingAiContext.cs` (modify — line 19 relational tag read)

- **Role:** Application static helper for cooking-mode AI context
- **Closest analog (self):** `src/CookBot.Application/Services/RecipeCookingAiContext.cs:14-35`
- **Excerpt:**
  ```csharp
  public static ParsedRecipe ToParsedRecipe(Recipe recipe, int targetServings)
  {
      var baseServings = recipe.Servings > 0 ? recipe.Servings : 1;
      targetServings = Math.Max(1, targetServings);

      var tags = JsonSerializer.Deserialize<List<string>>(recipe.TagsJson ?? "[]") ?? new();
      var ingredients = recipe.RecipeIngredients
          .OrderBy(ri => ri.RecipeLocalId)
          // ...
  ```
- **Apply pattern:** Replace line 19 with `var tags = recipe.Tags.Select(t => t.Name).ToList();` (caller must `Include(r => r.Tags)`). Drop the `using System.Text.Json;` if no other STJ usage remains in the file. Same change pattern applies to `RecipeEditor.razor:420` and `CookbookTransferService.cs:71` — all three callsites collapse from "deserialize the JSON column" to "project the relational collection".

---

### Infrastructure layer

#### `src/CookBot.Infrastructure/Data/Configurations/RecipeTagConfiguration.cs` (create)

- **Role:** EF entity configuration — composite index + FK with cascade
- **Closest analog:** `src/CookBot.Infrastructure/Data/Configurations/AiApiKeyShareConfiguration.cs:7-18`
- **Excerpt:**
  ```csharp
  public class AiApiKeyShareConfiguration : IEntityTypeConfiguration<AiApiKeyShare>
  {
      public void Configure(EntityTypeBuilder<AiApiKeyShare> builder)
      {
          builder.HasKey(s => s.Id);
          builder.HasIndex(s => new { s.OwnerUserId, s.RecipientUserId }).IsUnique();
          builder.HasOne(s => s.Owner).WithMany(u => u.AiApiKeySharesOwned).HasForeignKey(s => s.OwnerUserId)
              .OnDelete(DeleteBehavior.Cascade);
          builder.HasOne(s => s.Recipient).WithMany(u => u.AiApiKeySharesReceived).HasForeignKey(s => s.RecipientUserId)
              .OnDelete(DeleteBehavior.Cascade);
      }
  }
  ```
- **Apply pattern:** Copy structure verbatim. `builder.HasKey(t => t.Id);`, `builder.Property(t => t.Name).HasMaxLength(200).IsRequired();`, `builder.HasIndex(t => new { t.RecipeId, t.Name }).IsUnique();` (D-34: case-sensitive in SQLite = "Vegan"/"vegan" coexist). `builder.HasOne(t => t.Recipe).WithMany(r => r.Tags).HasForeignKey(t => t.RecipeId).OnDelete(DeleteBehavior.Cascade);`. Auto-picked up by `CookBotDbContext.OnModelCreating`'s `ApplyConfigurationsFromAssembly` (line 29) — no DbContext code beyond `DbSet<RecipeTag> RecipeTags => Set<RecipeTag>();`.

---

#### `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs` (modify)

- **Role:** EF configuration
- **Closest analog (self):** `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs:9-26`
- **Excerpt:**
  ```csharp
  public void Configure(EntityTypeBuilder<Recipe> builder)
  {
      builder.HasKey(r => r.Id);
      builder.Property(r => r.Name).HasMaxLength(300).IsRequired();
      builder.Property(r => r.TagsJson).HasDefaultValue("[]");

      // Phase 1 / D-12: canonical RecipeDocument JSON snapshot. TEXT, nullable.
      builder.Property(r => r.CanonicalDocumentJson)
          .HasColumnType("TEXT");

      builder.OwnsMany(r => r.Steps, steps =>
      {
          steps.ToJson();
          steps.OwnsMany(s => s.Timers);
      });

      builder.HasMany(r => r.RecipeIngredients).WithOne(ri => ri.Recipe).HasForeignKey(ri => ri.RecipeId).OnDelete(DeleteBehavior.Cascade);
  }
  ```
- **Apply pattern:** Add `builder.Property(r => r.PhotoUrl).HasMaxLength(2048);` and `builder.Property(r => r.Description).HasMaxLength(4096);` per D-28. **Delete** the `builder.Property(r => r.TagsJson).HasDefaultValue("[]");` line only when `Recipe.TagsJson` is removed (third migration). Adjacent to the `HasMany(RecipeIngredients)` line, add `builder.HasMany(r => r.Tags).WithOne(t => t.Recipe).HasForeignKey(t => t.RecipeId).OnDelete(DeleteBehavior.Cascade);` (or omit if `RecipeTagConfiguration` configures it from the dependent side — DRY rule: configure FK on dependent side only).

---

#### `src/CookBot.Infrastructure/Migrations/{ts}_AddRecipePhotoUrlAndDescription.cs` (create)

- **Role:** EF migration — two nullable string column adds
- **Closest analog:** `src/CookBot.Infrastructure/Migrations/20260425223916_RecipeCanonicalDocument.cs:7-27`
- **Excerpt:**
  ```csharp
  public partial class RecipeCanonicalDocument : Migration
  {
      protected override void Up(MigrationBuilder migrationBuilder)
      {
          migrationBuilder.AddColumn<string>(
              name: "CanonicalDocumentJson",
              table: "Recipes",
              type: "TEXT",
              nullable: true);
      }

      protected override void Down(MigrationBuilder migrationBuilder)
      {
          migrationBuilder.DropColumn(
              name: "CanonicalDocumentJson",
              table: "Recipes");
      }
  }
  ```
- **Apply pattern:** Two `AddColumn<string>` calls (`PhotoUrl` with `maxLength: 2048`, `Description` with `maxLength: 4096`), both `nullable: true`. `Down()` drops both. Keep `#nullable disable` at top (EF generator convention — every existing migration has it). Generate with `dotnet ef migrations add AddRecipePhotoUrlAndDescription --project src/CookBot.Infrastructure --startup-project src/CookBot.Web`. Backup file `cookbot.db.pre-AddRecipePhotoUrlAndDescription.bak` fires automatically via `IDatabaseBackupService` invoked from `DatabaseSeeder` (see `DatabaseSeeder.cs:32`).

---

#### `src/CookBot.Infrastructure/Migrations/{ts}_AddRecipeTagTable.cs` (create — table + backfill)

- **Role:** EF migration — CreateTable + composite index + FK + raw SQL backfill
- **Closest analog:** `src/CookBot.Infrastructure/Migrations/20260416175214_AiApiKeyShares.cs:12-69` (table+FK+composite-unique-index pattern) and `src/CookBot.Infrastructure/Migrations/20260428004334_ScheduledRecipesAndRecipeMades.cs:14-89` (multi-table + multi-index, closest dual pattern)
- **Excerpt (from AiApiKeyShares migration):**
  ```csharp
  migrationBuilder.CreateTable(
      name: "AiApiKeyShares",
      columns: table => new
      {
          Id = table.Column<int>(type: "INTEGER", nullable: false)
              .Annotation("Sqlite:Autoincrement", true),
          OwnerUserId = table.Column<int>(type: "INTEGER", nullable: false),
          RecipientUserId = table.Column<int>(type: "INTEGER", nullable: false),
          CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
      },
      constraints: table =>
      {
          table.PrimaryKey("PK_AiApiKeyShares", x => x.Id);
          table.ForeignKey(
              name: "FK_AiApiKeyShares_Users_OwnerUserId",
              column: x => x.OwnerUserId,
              principalTable: "Users",
              principalColumn: "Id",
              onDelete: ReferentialAction.Cascade);
          // ...
      });

  migrationBuilder.CreateIndex(
      name: "IX_AiApiKeyShares_OwnerUserId_RecipientUserId",
      table: "AiApiKeyShares",
      columns: new[] { "OwnerUserId", "RecipientUserId" },
      unique: true);
  ```
- **Apply pattern:** Generate via `dotnet ef migrations add AddRecipeTagTable`. Verify the generated `Up()` matches the analog shape; then **append the backfill** by hand (the EF generator won't author the data motion) using `migrationBuilder.Sql(...)`:
  ```csharp
  migrationBuilder.Sql(@"
      INSERT INTO RecipeTags (RecipeId, Name)
      SELECT r.Id, TRIM(json_each.value)
      FROM Recipes r, json_each(r.TagsJson)
      WHERE TRIM(json_each.value) <> ''
      ON CONFLICT DO NOTHING;
  ");
  ```
  SQLite ships with `json_each` (used elsewhere). `ON CONFLICT DO NOTHING` defends against `(RecipeId, Name)` unique-index collisions if a recipe has duplicate-case-identical tags in its JSON. **Do not** drop `TagsJson` here — that's the third migration per D-26's rollback-granularity rationale.

---

#### `src/CookBot.Infrastructure/Migrations/{ts}_DropTagsJsonColumn.cs` (create)

- **Role:** EF migration — single DropColumn
- **Closest analog:** `src/CookBot.Infrastructure/Migrations/20260425223916_RecipeCanonicalDocument.cs:20-26` (`Down()` shows the DropColumn shape)
- **Excerpt:**
  ```csharp
  protected override void Down(MigrationBuilder migrationBuilder)
  {
      migrationBuilder.DropColumn(
          name: "CanonicalDocumentJson",
          table: "Recipes");
  }
  ```
- **Apply pattern:** Generate after the `Recipe.TagsJson` C# property is deleted and all six callsites are migrated (otherwise EF won't generate a DropColumn). `Up()` drops `TagsJson` from `Recipes`. `Down()` re-adds `AddColumn<string>("TagsJson", "Recipes", type: "TEXT", nullable: false, defaultValue: "[]")`. The current `HasDefaultValue("[]")` constraint at `RecipeConfiguration.cs:13` is dropped automatically when the column goes — no extra code per the Discretion note.

---

#### `src/CookBot.Infrastructure/Migrations/{ts}_AddPantryMatchIndexes.cs` (create)

- **Role:** EF migration — pure CreateIndex
- **Closest analog:** `src/CookBot.Infrastructure/Migrations/20260428004334_ScheduledRecipesAndRecipeMades.cs:71-89`
- **Excerpt:**
  ```csharp
  migrationBuilder.CreateIndex(
      name: "IX_RecipeMades_RecipeId_CompletedAt",
      table: "RecipeMades",
      columns: new[] { "RecipeId", "CompletedAt" });

  migrationBuilder.CreateIndex(
      name: "IX_RecipeMades_UserId_CompletedAt",
      table: "RecipeMades",
      columns: new[] { "UserId", "CompletedAt" });
  ```
- **Apply pattern:** Two non-unique composite indexes: `IX_RecipeIngredients_RecipeId_IngredientId` on `RecipeIngredients(RecipeId, IngredientId)` and `IX_PantryItems_UserId_IngredientId` on `PantryItems(UserId, IngredientId)`. `Down()` symmetrically drops both via `DropIndex`. Per CONTEXT.md: these indexes are Phase 10 fuel — Phase 8 ships them so Phase 10 has zero migrations.

---

#### `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` (modify)

- **Role:** EF DbContext
- **Closest analog (self):** `src/CookBot.Infrastructure/Data/CookBotDbContext.cs:10-30`
- **Excerpt:**
  ```csharp
  public DbSet<User> Users => Set<User>();
  public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
  // ...
  public DbSet<AiApiKeyShare> AiApiKeyShares => Set<AiApiKeyShare>();
  public DbSet<ScheduledRecipe> ScheduledRecipes => Set<ScheduledRecipe>();
  public DbSet<RecipeMade> RecipeMades => Set<RecipeMade>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
      modelBuilder.ApplyConfigurationsFromAssembly(typeof(CookBotDbContext).Assembly);
  }
  ```
- **Apply pattern:** Append one line in the DbSet block: `public DbSet<RecipeTag> RecipeTags => Set<RecipeTag>();`. No edit to `OnModelCreating` — `ApplyConfigurationsFromAssembly` picks up `RecipeTagConfiguration` automatically.

---

#### `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` (modify — guard + drop projector arg)

- **Role:** EF seeder
- **Closest analog (self):** `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs:20-39, 108-135`
- **Excerpt:**
  ```csharp
  public static async Task SeedAsync(
      CookBotDbContext context,
      IDatabaseBackupService backupService,
      LegacyRecipeProjector projector,
      JsonRecipeSerializer serializer,
      string contentRootPath)
  {
      // Step 1: backup before migrate (D-15 / MIGRATION-02 / Pitfall C4).
      var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
      if (pending.Count > 0)
      {
          await backupService.BackupBeforeMigrationAsync("RecipeCanonicalDocument", CancellationToken.None);
      }

      // Step 2: apply migrations.
      await context.Database.MigrateAsync();

      // Step 3: idempotent backfill (D-16 / MIGRATION-01 / MIGRATION-07).
      await BackfillCanonicalDocumentAsync(context, projector, serializer);
      // ...
  ```
- **Apply pattern (D-32 step a — null-canonical guard):** After `MigrateAsync` and before any seed logic, add:
  ```csharp
  var nullCanonicalCount = await context.Recipes.CountAsync(r => r.CanonicalDocumentJson == null);
  if (nullCanonicalCount > 0)
  {
      throw new InvalidOperationException(
          $"{nullCanonicalCount} recipe(s) have null CanonicalDocumentJson after migrate. " +
          "This indicates an incomplete v1.1 backfill — restore from cookbot.db.pre-* backup and re-run.");
  }
  ```
  Apply tone consistent with the existing `throw new InvalidOperationException("Could not locate FreelovesCookBot.sln...")` style in `TestHost.cs:81-82` and the explanatory remediation hint pattern from `RecipeUpcasterChain.cs:58`. **D-32 steps b–e:** drop `LegacyRecipeProjector projector` from the signature, delete the `BackfillCanonicalDocumentAsync(context, projector, serializer)` call (the backfill is a no-op now per CONTEXT.md), delete the `BackfillCanonicalDocumentAsync` helper method. Update `Program.cs`'s `SeedAsync` call to drop the projector argument.

---

### Test layer

#### `tests/CookBot.Tests/CookBot.Tests.csproj` (modify — add Verify.Xunit)

- **Role:** csproj
- **Closest analog (self):** `tests/CookBot.Tests/CookBot.Tests.csproj:10-17`
- **Excerpt:**
  ```xml
  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.*" />
    <PackageReference Include="bunit" Version="1.40.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  ```
- **Apply pattern:** Append a single `<PackageReference Include="Verify.Xunit" Version="31.12.5" />` to that ItemGroup. Compatible with `xunit 2.9.2` per STACK.md dependency analysis. Do NOT add `Verify.XunitV3` — that would force an xUnit v3 migration. `Verify.DiffPlex` arrives transitively. **Also:** add a second `None Update` for `Snapshots\**\*.verified.txt` matching the existing `Fixtures\**\*.*` pattern at line 31, so verified snapshots ship with the test binaries:
  ```xml
  <None Update="Snapshots\**\*.verified.txt" CopyToOutputDirectory="PreserveNewest" />
  ```

---

#### `tests/CookBot.Tests/ModuleInitializer.cs` (create)

- **Role:** Verify path config (runs once per test assembly load)
- **Closest analog:** No existing `ModuleInitializer` in the project — this is a Verify convention; the closest path-discovery pattern is `tests/CookBot.Tests/TestHost.cs:66-83` (FindRepoRoot)
- **Excerpt (TestHost path-discovery pattern to mirror):**
  ```csharp
  public static string FindRepoRoot()
  {
      var dir = new DirectoryInfo(AppContext.BaseDirectory);
      for (int i = 0; i < 10 && dir is not null; i++)
      {
          if (File.Exists(Path.Combine(dir.FullName, "FreelovesCookBot.sln")))
          {
              return dir.FullName;
          }
          dir = dir.Parent;
      }
      throw new InvalidOperationException("Could not locate FreelovesCookBot.sln...");
  }
  ```
- **Apply pattern:** Standard Verify ModuleInitializer shape; route snapshots to `tests/CookBot.Tests/Snapshots/` (not the per-test-class default sibling):
  ```csharp
  using System.Runtime.CompilerServices;
  using VerifyTests;

  namespace CookBot.Tests;

  public static class ModuleInitializer
  {
      [ModuleInitializer]
      public static void Init()
      {
          Verifier.DerivePathInfo((sourceFile, projectDirectory, type, method) =>
              new PathInfo(
                  directory: Path.Combine(projectDirectory, "Snapshots"),
                  typeName: type.Name,
                  methodName: method.Name));
      }
  }
  ```
  `internal static` per the conventions doc (one type per file, file-scoped namespace).

---

#### `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` (modify — REPLACE with Verify)

- **Role:** xUnit test — switch from hand-rolled fixture-equality to Verify
- **Closest analog (self, being replaced):** `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs:13-43`
- **Excerpt (current hand-rolled shape):**
  ```csharp
  public class PromptSnapshotTests
  {
      [Fact]
      public void DefaultTemplate_AssembledPrompt_MatchesSnapshot()
      {
          var profile = TestHost.MakeProfile();
          var pantry = Array.Empty<PantryItem>();
          var svc = TestHost.GetPromptBuilderService();
          var actual = svc.ResolveTemplate(PromptBuilderService.DefaultTemplate, profile, pantry);
          // ... fixture file read + Assert.Equal ...
      }
  }
  ```
- **Apply pattern:** Replace the body with a single Verify call. New shape per D-35:
  ```csharp
  using VerifyXunit;

  namespace CookBot.Tests.Prompts;

  [UsesVerify]
  public class PromptSnapshotTests
  {
      [Fact]
      public Task BuildSystemPrompt() // method name -> snapshot filename
      {
          var profile = TestHost.MakeProfile();
          var pantry = Array.Empty<PantryItem>();
          var svc = TestHost.GetPromptBuilderService();
          var actual = svc.ResolveTemplate(PromptBuilderService.DefaultTemplate, profile, pantry);
          return Verifier.Verify(actual);
      }
  }
  ```
  Preserve the `TestHost.MakeProfile()` deterministic fixture (W4 rules, see `TestHost.cs:46-63`). On first run, Verify writes `PromptSnapshotTests.BuildSystemPrompt.received.txt`; rename to `.verified.txt` and commit. Per Discretion note: also inject one SCHEMA-10 alias token (e.g., `AiSystemPromptTemplate = "use the imageUrl field"`) into a second `[Fact]` to verify the denylist regex fires — keeps the denylist test self-checking.

---

#### `tests/CookBot.Tests/Prompts/PromptDenylistTests.cs` (modify — extend regex)

- **Role:** xUnit Theory test — source-file regex scanner
- **Closest analog (self):** `tests/CookBot.Tests/Prompts/PromptDenylistTests.cs:12-33`
- **Excerpt:**
  ```csharp
  public class PromptDenylistTests
  {
      private static readonly Regex Denylist =
          new(@"\b(fallback|informal|plain numbered|If you can'?t follow)\b",
              RegexOptions.IgnoreCase);

      [Theory]
      [InlineData("src/CookBot.Application/Services/PromptBuilderService.cs")]
      [InlineData("src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs")]
      public void PromptSourceFiles_ContainNoOptOutPhrases(string relativePath)
      {
          var repoRoot = TestHost.FindRepoRoot();
          var full = Path.Combine(repoRoot, relativePath);
          Assert.True(File.Exists(full), $"Source file not found: {full}");

          var src = File.ReadAllText(full);
          var matches = Denylist.Matches(src).Select(m => m.Value).ToList();
          Assert.True(
              matches.Count == 0,
              $"Found opt-out phrases in {relativePath}: {string.Join(", ", matches)}");
      }
  }
  ```
- **Apply pattern (D-36 + SCHEMA-10):** Extend the regex literal to include the seven photo/description/temperature alias tokens:
  ```csharp
  new(@"\b(fallback|informal|plain numbered|If you can'?t follow|image|imageUrl|picture|summary|desc|temp|oven)\b",
      RegexOptions.IgnoreCase);
  ```
  **Caveat:** the existing schema example uses words like `imageUrl` only in the C# field/constant name `PhotoUrl` (no `imageUrl` literal in the prompt). Once `Migration_V2_To_V3` adds `photoUrl` and the documentation provider mentions it, verify the new tokens don't false-positive against legitimate prose (e.g., `temperature` contains `temp` — use `\btemp\b` with word boundaries to exclude). Consider splitting into separate `[Theory]` rows by token if false-positive defense becomes complex.

---

#### `tests/CookBot.Tests/Recipes/Migration_V2_To_V3_Tests.cs` (create — fixture matrix)

- **Role:** xUnit Theory test — fixture-driven per-field matrix
- **Closest analog:** `tests/CookBot.Tests/Recipes/RecipeDocumentRoundTripTests.cs:17-91` (MemberData + filesystem fixtures) and `tests/CookBot.Tests/Recipes/RecipeUpcasterTests.cs:15-115` (chain wiring + version assertions)
- **Excerpt (round-trip pattern with MemberData):**
  ```csharp
  public class RecipeDocumentRoundTripTests
  {
      public static IEnumerable<object[]> V2CanonicalFixtures()
      {
          var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recipes", "v2-canonical");
          foreach (var path in Directory.GetFiles(dir, "*.json"))
          {
              yield return new object[] { Path.GetFileName(path), File.ReadAllText(path) };
          }
      }

      [Theory]
      [MemberData(nameof(V2CanonicalFixtures))]
      public void V2Canonical_RoundTripIsIdempotent(string fixtureName, string jsonText)
      {
          var serializer = new JsonRecipeSerializer();
          var validator = new RecipeValidator();

          var doc = serializer.Deserialize(jsonText);
          var roundTripped = serializer.Deserialize(serializer.Serialize(doc));
          // ... structural assertions on each field ...
      }
  }
  ```
- **Excerpt (upcaster fact pattern):**
  ```csharp
  private static RecipeUpcasterChain MakeChain() =>
      new(new IRecipeUpcaster[] { new Migration_V1_To_V2() });

  [Fact]
  public void UpcastToCurrent_VersionAbsent_StampsV1AndUpcastsToV2()
  {
      var node = JsonNode.Parse("""{"name":"X","ingredients":[],"steps":[]}""")!;
      var result = MakeChain().UpcastToCurrent(node);
      Assert.Equal(2, result["version"]!.GetValue<int>());
  }
  ```
- **Apply pattern:** Combine both. New `MakeChain()` registers both upcasters: `new(new IRecipeUpcaster[] { new Migration_V1_To_V2(), new Migration_V2_To_V3() })`. Per D-29, one `[Theory]` fed by a `MemberData` source reading `Fixtures/Recipes/upcaster/v2-to-v3-*.json` (per CONTEXT.md fixture matrix entry). Each fixture filename encodes the missing-field combination (e.g., `v2-to-v3-no-photo.json`, `v2-to-v3-all-present.json`). Per-fixture assertions verify the absent field stays absent / null after upcast, version becomes 3, and no other field is touched. Add focused `[Fact]`s for: (a) v2 with no temperature → all `ContentStep.Temperature == null` (PITFALLS C7/M2); (b) version-already-3 is identity; (c) chain gap validation still holds.

---

#### `tests/CookBot.Tests/Recipes/SchemaAssertionTests.cs` (create — SCHEMA-11, FIRST test)

- **Role:** xUnit fact test — asserts `RecipeJsonSchemaProvider.GetSchema()` emits the new fields
- **Closest analog:** `tests/CookBot.Tests/Recipes/RecipeJsonSchemaProviderTests.cs` (existing) and the inline `RecipeJsonSchemaProvider.GetSchema()` shape at `src/CookBot.Application/Recipes/RecipeJsonSchemaProvider.cs:25-46`
- **Excerpt:** (RecipeJsonSchemaProvider's lazy schema cache)
  ```csharp
  public JsonNode GetSchema() => _schema.Value;
  ```
- **Apply pattern (per PITFALLS C8 / M3 — FIRST test written, must run green before any prod code merges):**
  ```csharp
  [Fact]
  public void GetSchema_Includes_PhotoUrl_Description_StepTemperature()
  {
      var schema = new RecipeJsonSchemaProvider().GetSchema().AsObject();
      var props = schema["properties"]!.AsObject();
      Assert.True(props.ContainsKey("photoUrl"));
      Assert.True(props.ContainsKey("description"));
      // Navigate into anyOf -> ContentStep -> properties to find temperature
      // (steps is a polymorphic array; ContentStep is one anyOf branch)
      // ... assertion on temperature with nullable shape per PITFALLS M3 ...
  }
  ```
  Add a second `[Fact]` asserting `additionalProperties: false` recursively reaches the new `StepTemperature` subschema (`SetAdditionalPropertiesFalse` at line 52 already walks the whole tree — this is regression protection).

---

#### `tests/CookBot.Tests/Recipes/StepTemperatureTests.cs` (create)

- **Role:** xUnit Theory test — per-unit validation matrix
- **Closest analog:** `tests/CookBot.Tests/Recipes/RecipeValidatorTests.cs` shape (existing, same `RecipeValidator.Validate(doc)` entry point)
- **Apply pattern:** Build `RecipeDocument` instances with a single `ContentStep` carrying various `StepTemperature` values via `[Theory]` `[InlineData]`:
  - `F, 350` → valid
  - `F, 350.5` → INVALID_TEMPERATURE
  - `C, 180` → valid
  - `C, 180.5` → INVALID_TEMPERATURE
  - `Gas, 4` → valid
  - `Gas, 4.5` → valid
  - `Gas, 0.5` → INVALID_TEMPERATURE (below range)
  - `Gas, 9.5` → valid
  - `Gas, 10` → INVALID_TEMPERATURE (above range)
  - `Gas, 4.25` → INVALID_TEMPERATURE (not 0.5-step)
  Assert `result.Errors.Any(e => e.Code == "INVALID_TEMPERATURE")` for negatives, `result.IsValid` for positives. Match `RecipeValidator`'s never-throws contract.

---

#### `tests/CookBot.Tests/Recipes/RecipeTagBackfillTests.cs` (create)

- **Role:** xUnit test — EF in-memory + raw SQL backfill validation
- **Closest analog:** `tests/CookBot.Tests/Migration/CanonicalBackfillTests.cs` (file exists per the directory listing — same EF backfill pattern, runs the seeder, asserts post-state)
- **Apply pattern:** Spin up an in-memory or SQLite-file `CookBotDbContext`, seed two recipes with `TagsJson = """["Vegan","vegan"," gluten-free "]"""`, run the `AddRecipeTagTable` migration (or call the `migrationBuilder.Sql` payload directly against the connection), then assert:
  - Both `Vegan` and `vegan` exist as distinct `RecipeTag` rows (D-34 case-sensitive coexistence).
  - `gluten-free` is stored without leading/trailing whitespace (trim).
  - Composite unique index does not block the two case-variants (different `Name` string values per SQLite default case sensitivity).
  - On a second backfill execution, `ON CONFLICT DO NOTHING` keeps the row count stable (idempotency).

---

### Web layer

#### `src/CookBot.Web/Components/Pages/RecipeEditor.razor` (modify — line 420)

- **Role:** Razor page — tag read switch only
- **Closest analog:** see RecipeCookingAiContext.cs pattern above
- **Apply pattern:** Replace `_tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(recipe.TagsJson ?? "[]") ?? new();` with `_tags = recipe.Tags.Select(t => t.Name).ToList();`. Ensure the `Recipe` query path uses `.Include(r => r.Tags)`. NO other UI changes — photo/description/temperature pickers explicitly defer to Phase 9 per Phase Boundary.

---

#### `src/CookBot.Web/Services/CookbookTransferService.cs` (modify — line 71)

- **Role:** Web service — JSON cookbook export/import
- **Apply pattern:** Replace `tags = JsonSerializer.Deserialize<List<string>>(recipe.TagsJson) ?? new();` with `tags = recipe.Tags.Select(t => t.Name).ToList();`. Caller's query path must `.Include(r => r.Tags)`. Per CONTEXT.md integration note: envelope `SchemaVersion` does **not** bump — only per-recipe `RecipeDocument.Version` bumps to 3 through the upcaster chain on import.

---

### Docs

#### `README.md` (modify — add "Recipe Format" section)

- **Role:** Project docs
- **Closest analog:** `src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs:10-41` (the AI-facing schema prose — README section can lift the same code-fenced example for visual parity)
- **Apply pattern (D-37):** Inline section in `README.md` below the (yet-to-be-written-in-Phase-9) "Install" section, with five subsections per D-37: (a) one-paragraph description of `RecipeDocument`; (b) YAML wire example with all v3 fields populated (use `---\n...\n---\n` frontmatter); (c) JSON export example (same recipe, indented form via `JsonRecipeSerializer.SerializeIndented` — note gas half-stops render as `"4½"`); (d) V1→V2→V3 upcaster lineage as a markdown bullet list, one line per migration (V1→V2: time-field rename, localId→id, step kind discriminator; V2→V3: photoUrl, description, per-step temperature); (e) note about forward-only upcaster + internally-managed format.

---

## Shared Patterns

### Pattern S1: xUnit Theory with filesystem-driven MemberData

**Source:** `tests/CookBot.Tests/Recipes/RecipeDocumentRoundTripTests.cs:17-43`
**Apply to:** `Migration_V2_To_V3_Tests`, `RecipeTagBackfillTests` (any test driven by a `Fixtures/Recipes/*` matrix)

```csharp
public static IEnumerable<object[]> V2CanonicalFixtures()
{
    var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recipes", "v2-canonical");
    foreach (var path in Directory.GetFiles(dir, "*.json"))
    {
        yield return new object[] { Path.GetFileName(path), File.ReadAllText(path) };
    }
}

[Theory]
[MemberData(nameof(V2CanonicalFixtures))]
public void V2Canonical_RoundTripIsIdempotent(string fixtureName, string jsonText) { ... }
```

Fixture files ride along via the csproj `<None Update="Fixtures\**\*.*" CopyToOutputDirectory="PreserveNewest" />` glob at line 31. Drop new `v3-canonical/*.json` and `upcaster/v2-to-v3-*.json` files; they auto-deploy.

---

### Pattern S2: EF migration with backup-on-pending

**Source:** `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs:29-36`
**Apply to:** All four new EF migrations — backup fires automatically; no per-migration code needed.

```csharp
var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
if (pending.Count > 0)
{
    await backupService.BackupBeforeMigrationAsync("RecipeCanonicalDocument", CancellationToken.None);
}
await context.Database.MigrateAsync();
```

Note: the current call passes the literal string `"RecipeCanonicalDocument"` as the backup label. To produce the four distinct backup files D-31 promises (`pre-AddRecipePhotoUrlAndDescription.bak`, etc.), the planner should either change this to iterate over `pending` and call backup per-migration, or rename to a generic label like `pending[0]` — flag for planner discussion.

---

### Pattern S3: Singleton-registered IRecipeUpcaster

**Source:** `src/CookBot.Application/DependencyInjection.cs:26`
**Apply to:** `Migration_V2_To_V3` registration.

```csharp
services.AddSingleton<IRecipeUpcaster, Migration_V1_To_V2>();
services.AddSingleton<RecipeUpcasterChain>();
```

MS DI multi-registration: all `IRecipeUpcaster` singletons are collected into the constructor's `IEnumerable<IRecipeUpcaster>` and sorted by `FromVersion` (see `RecipeUpcasterChain:20`).

---

### Pattern S4: Throw early at boundaries (BCL exceptions)

**Source:** `src/CookBot.Application/Services/RecipeService.cs:35-39` + `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs:26-29`
**Apply to:** `DatabaseSeeder` null-canonical guard (D-32 step a).

```csharp
var cookbook = await _cookbookRepo.GetByIdAsync(cookbookId)
    ?? throw new InvalidOperationException("Cookbook not found.");

if (cookbook.UserId != userId)
    throw new UnauthorizedAccessException("You do not own this cookbook.");
```

Use `InvalidOperationException` with a concise message + remediation hint. Never log via `ILogger` (no logging framework in production code per CONVENTIONS.md §"Logging").

---

### Pattern S5: STJ attributes, never Newtonsoft / NJsonSchema

**Source:** `src/CookBot.Domain/Recipes/RecipeDocument.cs:13-39` + project-wide CLAUDE.md "Things to avoid"
**Apply to:** Every new POCO (`StepTemperature`, `RecipeTag`, fixture JSON).

`[JsonPropertyName("camelCase")]` everywhere; `[JsonExtensionData] public Dictionary<string, JsonElement> Extras { get; init; } = new();` on every top-level / nested forward-compat record (NOT on leaf value objects like `TimerEntry` / `StepTemperature`); no `[JsonProperty]`, no `JsonConvert`, no schema-validation library other than the existing `JsonSchema.Net`.

---

### Pattern S6: File-scoped namespace + nullable + implicit usings

**Source:** `src/CookBot.Application/Recipes/Migration_V1_To_V2.cs:1-3` + CONVENTIONS.md §"File-Scoped Namespaces"
**Apply to:** Every new `.cs` file in src/ and tests/.

```csharp
using System.Text.Json.Nodes; // only non-implicit usings

namespace CookBot.Application.Recipes;

public sealed class Migration_V2_To_V3 : IRecipeUpcaster { ... }
```

One public type per file; file name matches type name; no block-style namespaces (EF-generated migrations are the only exception).

---

## No Analog Found

| File | Reason |
|------|--------|
| `tests/CookBot.Tests/ModuleInitializer.cs` | Verify.Xunit convention; no existing ModuleInitializer in repo. Mirror Verify docs + project conventions (file-scoped namespace, `internal static`). |
| `src/CookBot.Application/Recipes/Converters/StepTemperatureJsonConverter.cs` | No custom `JsonConverter<T>` exists in project. Use BCL `JsonConverter<StepTemperature>` shape; wire through `JsonRecipeSerializer._indented.Converters` (see line 29-32). |
| `tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt` | Generated by Verify on first test run; commit after manual approval. No pre-shipped analog. |

---

## Metadata

**Analog search scope:**
- `src/CookBot.Domain/Recipes/` (RecipeDocument, StepNode, TimerEntry, IngredientEntry)
- `src/CookBot.Domain/Entities/` (Recipe, AiApiKeyShare for table+FK pattern)
- `src/CookBot.Application/Recipes/` (Migration_V1_To_V2, RecipeUpcasterChain, JsonRecipeSerializer, RecipeJsonSchemaProvider, RecipeValidator, IRecipeProjector, RecipeSchemaDocumentationProvider)
- `src/CookBot.Application/Services/` (RecipeService, RecipeFormatParser, RecipeCookingAiContext, PromptBuilderService)
- `src/CookBot.Application/DependencyInjection.cs`
- `src/CookBot.Infrastructure/Data/` (CookBotDbContext, DatabaseSeeder, Configurations/RecipeConfiguration, Configurations/AiApiKeyShareConfiguration, Configurations/CookbookShareConfiguration, Configurations/RecipeIngredientConfiguration, Migrations/Helpers/LegacyRecipeProjector)
- `src/CookBot.Infrastructure/Migrations/` (RecipeCanonicalDocument, AiApiKeyShares, ScheduledRecipesAndRecipeMades)
- `tests/CookBot.Tests/` (TestHost, CookBot.Tests.csproj)
- `tests/CookBot.Tests/Prompts/` (PromptSnapshotTests, PromptDenylistTests)
- `tests/CookBot.Tests/Recipes/` (RecipeUpcasterTests, RecipeDocumentRoundTripTests)

**Files scanned:** ~28 source/test/migration files (Read tool calls), 4 directory listings (Bash tool calls).

**Phase 1 precedent dependency:** PATTERNS.md treats `01-canonical-format-foundation/` as load-bearing per CONTEXT.md's "Phase 1 Reference" section — every upcaster/serializer/schema/migration pattern above traces to a Phase 1 file or convention. Phase 8 extends; it does not invent new abstractions.

**Pattern extraction date:** 2026-05-15
