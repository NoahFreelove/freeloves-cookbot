# Architecture Patterns

**Project:** FreelovesCookBot — Subsequent Milestone (canonical recipe format + AI conformance + chip editor)
**Researched:** 2026-04-25
**Mode:** Project research, architecture dimension
**Confidence:** HIGH for layering and integration points (rooted in the existing codebase audit); MEDIUM for the third-party API specifics (Anthropic structured outputs, EF Core 10 JSON column behavior).

---

## TL;DR Recommendation

1. **Canonical schema lives as POCO records in `CookBot.Domain/Recipes/V1/`**, with a small `IRecipeSchemaSerializer` family (YAML, JSON, JsonSchema-export) implemented in `CookBot.Application`. The POCO records are the single source of truth; YAML wire format, cookbook export shape, and the AI prompt's JSON schema are all *projections* of them. EF stores a serialized copy of the canonical document in an owned-JSON column alongside the existing relational columns (so existing queries keep working), and a one-time migration repopulates the canonical column from current rows.
2. **Versioning is via an `int Version` field on the canonical document and a chain of `IRecipeUpcaster` functions** (`v1 → v2 → v3 …`) that run during deserialization. The newest version is the only one in-app code reads. This is the [Marten/event-sourcing upcasting pattern](https://martendb.io/events/versioning.html) applied to recipe documents.
3. **AI structured-output orchestration is a new `IAiRecipeGenerator` abstraction** in `CookBot.Application/Services/Ai/`, which wraps `IAiService` (existing). It owns the schema-pinning, validation, and one-shot repair pass. It is **not** a decorator on `IAiService` — the orchestration is recipe-specific and has no business living on the generic AI primitive.
4. **Step composer is text-backed at the model layer** (the `Text` field stays a string with `[name](#id)` markdown links), but the editor renders a **structured token stream** built on the fly by tokenizing the same regex used by `RecipeStepTextFormatter`. On save, the token stream serializes back to the same markdown-linked string. Trade-off: zero schema migration cost, full AI-prompt round-trip parity, at the cost of slightly more JS interop to manage caret position in a contenteditable.
5. **Build order:** canonical schema → version+upcaster scaffold → AI structured output → chip editor → format-driven new features. Every step after the first depends on schema decisions, so decisions land in phase 1 and don't churn.

---

## Recommended Architecture (Layered View)

```
┌─────────────────────────────────────────────────────────────────────────┐
│ CookBot.Web                                                             │
│   - RecipeEditor.razor (chip composer host)                             │
│   - StepComposer.razor [NEW]   StepComposerInterop.razor [NEW]          │
│   - AiChat.razor (uses IAiRecipeGenerator instead of IAiService for     │
│     recipe-emitting paths)                                              │
│   - PromptBuilder.razor (consumes RecipeSchemaDocumentationProvider)    │
│   - ImportCookbookDialog.razor (delegates version handling to upcaster) │
│   - wwwroot/js/step-composer.js [NEW]  (caret + autocomplete)           │
└────────┬────────────────────────────────────────────────────────────────┘
         │
┌────────▼────────────────────────────────────────────────────────────────┐
│ CookBot.Application                                                     │
│   Services/                                                             │
│     RecipeFormatParser.cs (REWORKED — delegates to                      │
│       IRecipeSchemaSerializer; keeps IRecipeFormatParser as a thin      │
│       compatibility wrapper for YAML callers)                           │
│   Recipes/ [NEW SUBNAMESPACE]                                           │
│     Schema/                                                             │
│       IRecipeSchemaSerializer.cs  (Yaml | JsonExport | JsonSchemaSpec)  │
│       YamlRecipeSerializer.cs                                           │
│       JsonExportRecipeSerializer.cs                                     │
│       RecipeJsonSchemaProvider.cs (emits JSON Schema for AI/docs)       │
│       RecipeSchemaDocumentationProvider.cs (single template for prompt) │
│     Versioning/                                                         │
│       IRecipeUpcaster.cs                                                │
│       RecipeUpcasterChain.cs                                            │
│       Upcasters/V1ToV2.cs, V2ToV3.cs, ...                               │
│     Validation/                                                         │
│       RecipeValidator.cs (semantic validation: ingredient ID            │
│         uniqueness, ref→id matching, timer non-negative, ...)           │
│     Ai/                                                                 │
│       IAiRecipeGenerator.cs   [NEW abstraction]                         │
│       AiRecipeGenerator.cs    (validate → repair → return)              │
│       RecipeRepairPromptBuilder.cs                                      │
│   DTOs/                                                                 │
│     CookbookTransferDtos.cs (REWORKED — recipes use canonical record)   │
└────────┬────────────────────────────────────────────────────────────────┘
         │
┌────────▼────────────────────────────────────────────────────────────────┐
│ CookBot.Domain                                                          │
│   Entities/Recipe.cs (gains CanonicalDocumentJson string column)        │
│   Recipes/ [NEW SUBNAMESPACE — pure POCOs, no framework deps]           │
│     RecipeDocument.cs        (the canonical record, has Version field)  │
│     RecipeIngredientNode.cs                                             │
│     RecipeStepNode.cs   (discriminated: ContentStep | SectionStep)      │
│     RecipeTimerNode.cs                                                  │
│     RecipeSchemaConstants.cs (CurrentVersion = N)                       │
│   Interfaces/                                                           │
│     IRecipeFormatParser.cs (existing; will internally delegate)         │
└────────┬────────────────────────────────────────────────────────────────┘
         │
┌────────▼────────────────────────────────────────────────────────────────┐
│ CookBot.Infrastructure                                                  │
│   Data/Configurations/RecipeConfiguration.cs                            │
│     - keep existing OwnsMany(Steps).ToJson() for back-compat reads      │
│     - add CanonicalDocumentJson nvarchar(max)/json column               │
│   Migrations/<timestamp>_RecipeCanonicalDocument.cs                     │
│     - add column, populate from existing rows                           │
│   AI/AnthropicAiService.cs                                              │
│     - existing IAiService unchanged                                     │
│     - NEW: ResponseFormat overload accepting output_config / tool_use   │
│       (or kept inside AiRecipeGenerator if we don't want to widen the   │
│        IAiService surface)                                              │
└─────────────────────────────────────────────────────────────────────────┘
```

### Component Boundaries

| Component | Responsibility | Communicates With |
|-----------|---------------|-------------------|
| `CookBot.Domain/Recipes/RecipeDocument` | The canonical recipe record. POCO, immutable, includes `Version`. | Nothing (pure data) |
| `IRecipeSchemaSerializer<TFormat>` (Application/Recipes/Schema) | Serialize/deserialize between `RecipeDocument` and a wire format (YAML, JSON, JsonSchema). One implementation per wire format. | `RecipeDocument`, `RecipeUpcasterChain` |
| `RecipeJsonSchemaProvider` (Application) | Emits a JSON Schema describing `RecipeDocument` for AI structured-output `output_config.format.schema` and for `/prompt-builder`. Single source of truth for the format description. | `RecipeDocument`, `Microsoft.Extensions.AI` JSON schema exporter or `Corvus.JsonSchema` source generator |
| `RecipeUpcasterChain` (Application) | Orders registered `IRecipeUpcaster` functions and applies them in sequence on read. | Document is read from DB / import / AI / paste |
| `RecipeValidator` (Application) | Semantic validation post-deserialization (referential integrity, etc.). Returns a structured result, never throws. | `RecipeDocument` |
| `IAiRecipeGenerator` (Application/Recipes/Ai) | Orchestrates: build prompt → call `IAiService` with structured-output config → parse → validate → if invalid, build repair prompt → re-call once → return success or detailed error. | `IAiService`, `RecipeJsonSchemaProvider`, `IRecipeSchemaSerializer`, `RecipeValidator` |
| `RecipeFormatParser` (existing, reworked) | Stays as `IRecipeFormatParser` for backwards compatibility but internally delegates to `YamlRecipeSerializer + RecipeUpcasterChain + RecipeValidator`. | The new schema components |
| `StepComposer.razor` (Web) | Renders a step's `Text` as a chip stream, edits in-place, emits the same `[name](#id)` markdown string back on save. | `js/step-composer.js`, `IngredientResolver`, `IngredientRefDetectionService` (existing regex) |
| `Recipe.CanonicalDocumentJson` (Domain entity) | Persisted JSON copy of the canonical document. Computed on save from the same data the relational columns hold. | EF Core, `IRecipeSchemaSerializer<JsonExport>` |

### What Stays Modified-In-Place vs. New Abstractions

| Concern | Decision | Reasoning |
|---------|----------|-----------|
| `IAiService` | **Modify in place**: add an overload that accepts a `ResponseFormat` parameter (JSON schema for structured output). Existing string-streaming overload keeps working for chat. | The provider abstraction is the right place for the `output_config` plumbing; recipe-specific orchestration is *not*. |
| `IRecipeFormatParser` | **Keep as compatibility shim**, rewrite internals. | Many call sites (`PasteRawTextDialog`, `AiChat.ExtractRecipeContent`, `RecipeCookingAiContext`) call `Parser.TryParse`. Don't break them in this milestone. |
| `PromptBuilderService.ResolveRecipeFormat / BuildCopyablePrompt` | **Modify in place** to read from `RecipeSchemaDocumentationProvider` (one constant). Removes the duplicated literal strings called out in CONCERNS §13. | This is exactly the duplication the milestone wants to kill. |
| `AiChat.ExtractRecipeContent` | **Replace with `IAiRecipeGenerator.TryParseAssistantMessageAsync`**. Old heuristic-cascade method goes away. | CONCERNS §9: the loose third-arm extractor is the loudest correctness bug. With structured output we get a strict fence + schema-validated body and don't need three fallbacks. |
| `CookbookTransferDocument` | **Modify in place**. The cookbook envelope (metadata + recipes array + `SourceApp`) is fine; only the recipe shape inside changes. Bump `SchemaVersion` to 2. | PROJECT.md "Out of Scope" pins the envelope. |
| `Recipe`, `RecipeStep`, `StepTimer`, `RecipeIngredient` entities | **Modify in place** — keep relational columns for indexed queries. Add `CanonicalDocumentJson`. | We need indexed queries (filter by `IsSection`, `Order`, etc.) and want to avoid breaking the existing `Include(...)` paths in `CookingMode.razor`. |
| `RecipeStep.IngredientRefs` | **Keep as derived**, recomputed on save from canonical document. | Already derived today. No reason to change. |
| `Recipe.TagsJson` | **Replace with relational `Tags` table OR move into the canonical document**. Pick relational for filterability, otherwise canonical. | CONCERNS §3: deserializing `TagsJson` at every read site is a code smell. The milestone is the right time. |
| `TimerDetectionService` (regex auto-detect) | **Move to suggestion-only**. The chip editor surfaces "Detected: 25 min — convert to a timer? [yes/no]". | CONCERNS §7: silent auto-detection is the source of false positive timers. |

---

## Data Flow

### Flow A: AI generates a recipe (chat path)

```
User message
   │
   ▼
AiChat.razor
   │ (recipe intent detected — see "Heuristic for routing chat → recipe path" below)
   ▼
IAiRecipeGenerator.GenerateAsync(userPrompt, profile, conversation, apiKey, modelId)
   │
   ├─► PromptBuilderService.BuildSystem(...)                  ← single source of recipe-format text
   ├─► RecipeJsonSchemaProvider.GetCurrentSchema()            ← derived from RecipeDocument record
   │
   ▼
IAiService.SendStructuredAsync(system, messages, schema, apiKey, modelId)
   │ (Anthropic /v1/messages with output_config.format = json_schema)
   ▼
JSON body in assistant message
   │
   ▼
JsonExportRecipeSerializer.Deserialize(body, out RecipeDocument doc, out errors)
   │
   ▼
RecipeUpcasterChain.UpcastToCurrent(doc)        ← rare on AI output (model emits current),
   │                                              but cheap and protects against schema drift
   ▼
RecipeValidator.Validate(doc)
   │
   ├── valid? ──► return Success(doc)
   │
   └── invalid? ──┐
                  ▼
   RecipeRepairPromptBuilder.Build(doc, errors)
   "Your previous response did not validate. Issues: <list>. Re-emit
    the recipe in the schema. The schema is: <inline schema>."
                  │
                  ▼
   IAiService.SendStructuredAsync(...) [ONE retry only]
                  │
                  ▼
   Validate again
                  │
                  ├── valid → Success(doc)
                  └── still invalid → Failure(detailed errors, last raw body)
                                       UI surfaces: "AI couldn't produce a valid recipe.
                                       [Edit raw text] [Try again]"
```

**Critical decisions in this flow:**

- **Use Anthropic structured outputs (`output_config.format.type = "json_schema"`)** as the primary mechanism, available GA on Sonnet 4.5+ and Opus 4.1+, see [Anthropic structured outputs docs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs). The model literally cannot produce tokens that violate the schema, eliminating syntactic-error retries entirely. Implementation in C# is "raw JSON schema passed via `output_config`" — no SDK is needed (we already use raw HTTP).
- **One retry only**, not unlimited. Retrying past one round burns tokens and often fails the same way; a single targeted error message is the proven pattern, see [Snippets Ltd: validation-retry loops](https://snippets.ltd/blog/structured-outputs-with-claude-json-schemas-validation-retry-loops).
- **Repair prompt includes the original document**, the validator's error list, and the schema. This is the [retry-with-error-feedback pattern](https://snippets.ltd/blog/structured-outputs-with-claude-json-schemas-validation-retry-loops).
- **Back-compat for the chat path**: free-form chat that doesn't request a recipe stays on the existing `IAiService.StreamMessageAsync` path. Only when the user (or a heuristic) signals "I want a recipe" does the structured-output path kick in. The trigger is explicit (a "Generate Recipe" button on the chat page) rather than implicit, eliminating CONCERNS §9's three-arm extractor entirely.
- **Removes the opt-out clause** (CONCERNS §10). With structured output, the format is enforced server-side; there's nothing to opt out of.

### Flow B: Manual step authoring (chip composer)

```
User clicks "Add step" or focuses an existing step
   │
   ▼
StepComposer.razor mounts a contenteditable <div>
   │
   ▼ (initial render)
TokenizeForDisplay(step.Text)
   │ - regex \[([^\]]+)\]\(#(\d+)\)  → IngredientChip nodes
   │ - timer-detect (existing regex)  → TimerChip suggestions (informational, not destructive)
   │ - everything else                → text nodes
   │
   ▼
Renders: [text-node "Bake the "] [chip "potatoes" id=3] [text-node " for 25 minutes."]
   │
User types "@" or clicks "+ ingredient"
   │
   ▼
js/step-composer.js shows MudPopover-positioned MudAutocomplete over recipe ingredients
   │
   ▼
On select: insertIngredientChip(displayName, id, caretPosition)
   │
   ▼
On save (or @bind-Value), serialize tokens back to markdown:
   text + `[${chip.name}](#${chip.id})` + text
   │
   ▼
step.Text = "Bake the [potatoes](#3) for 25 minutes."
   │
   ▼
RecipeService.UpdateAsync persists as today
   │ - IngredientRefDetectionService recomputes IngredientRefs from chip-emitted markdown
   │   (no behavior change vs today, just guaranteed correctness because chips were
   │    inserted from a known ingredient list)
   ▼
DB write + canonical document recompute
```

**Why text-backed and not a structured `StepDocument`:**

| Decision | Text-backed (recommended) | Structured `StepDocument` |
|----------|--------------------------|--------------------------|
| Schema migration cost | Zero — `RecipeStep.Text` stays the same string | Significant — every step in every existing recipe needs migrating |
| AI prompt parity | Trivial — feed the same string to the AI | Need to flatten back to a string for prompts anyway |
| Round-trip to YAML | Trivial — same string, same regex | Need a tokens-to-YAML converter, with edge cases (escaping `[`, `]`, `(`, `)` inside text) |
| Existing tests | All keep passing | All `RecipeStepTextFormatterTests`, `IngredientRefDetectionServiceTests` rewrite |
| Editor rendering | Tokenize-then-render at mount, serialize-on-save | Already structured; simpler editor |
| YAML export of timers | Unchanged from today | Same — timers are already structured |
| Disadvantage | Editor needs tokenize/serialize logic, caret-position management in JS | More code to write, more migration work |

The existing codebase already treats step text as the source of truth — the markdown links in `[name](#id)` already encode structure. A structured model would essentially duplicate this. The chip composer is a **view layer** over the same canonical string. This matches the [ProseMirror inline-node model](https://prosemirror.net/) but persisted as serialized markdown rather than as a node tree.

### Flow C: Existing JSON cookbook import (back-compat)

```
User uploads .cookbook.json
   │
   ▼
ImportCookbookDialog.razor
   │
   ▼
CookbookTransferService.Deserialize(json, out doc, out errors)
   │
   ▼ (envelope version check)
doc.SchemaVersion switch
   │
   ├── 1 (old) ──► For each Recipes[i]:
   │                 - construct a RecipeDocument with Version=1
   │                 - RecipeUpcasterChain.UpcastToCurrent(...)  ← V1→V2 upcaster handles
   │                                                               PrepTimeMinutes→prepTime,
   │                                                               IsSection bool→step variant,
   │                                                               localId→id
   │                 - RecipeValidator.Validate(...)
   │                 - persist normally
   │
   └── 2 (current) ──► RecipeDocuments are already canonical; persist directly
```

**Result:** every `.cookbook.json` file ever written stays importable, and the upcaster chain owns the transformation. No special-case branches in `CookbookTransferService`.

### Flow D: AI prompt building (system message)

```
PromptBuilderService.ResolveRecipeFormat()
   │
   ▼
RecipeSchemaDocumentationProvider.GetMarkdownDescription()
   │ (single source — describes the same RecipeDocument)
   │
   ├── Returns: human-readable schema description + canonical example
   │
   ▼
Embedded in system prompt
   │
   ▼
For structured-output paths, the JSON schema itself is sent in output_config
   (RecipeJsonSchemaProvider.GetCurrentSchema()). The prose description in the
   system prompt is for context, not enforcement.
```

This kills CONCERNS §13 (duplicated format spec). One source for the markdown description; one source for the schema; both derived from the same `RecipeDocument` record.

---

## Patterns to Follow

### Pattern 1: Canonical document as POCO records, projections as serializers

**What:** Define the recipe shape once as a record in `CookBot.Domain/Recipes/`. Every other representation is a projection.

**When:** Use whenever the same data needs to be expressed in multiple formats (YAML, JSON, JsonSchema, AI prompt example, DB JSON column).

**Example:**

```csharp
// CookBot.Domain/Recipes/RecipeDocument.cs
namespace CookBot.Domain.Recipes;

public sealed record RecipeDocument
{
    public required int Version { get; init; }
    public required string Name { get; init; }
    public int Servings { get; init; } = 1;
    public int? PrepTimeMinutes { get; init; }
    public int? CookTimeMinutes { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<RecipeIngredientNode> Ingredients { get; init; } = [];
    public IReadOnlyList<RecipeStepNode> Steps { get; init; } = [];
}

public sealed record RecipeIngredientNode
{
    public required int Id { get; init; }            // local-to-recipe; the [name](#id) target
    public required string Name { get; init; }
    public decimal? Amount { get; init; }
    public string? Unit { get; init; }
    public string? Note { get; init; }
}

// Discriminated union via abstract record + sealed subtypes
public abstract record RecipeStepNode;

public sealed record ContentStep : RecipeStepNode
{
    public required string Text { get; init; }
    public IReadOnlyList<RecipeTimerNode> Timers { get; init; } = [];
}

public sealed record SectionStep : RecipeStepNode
{
    public required string Heading { get; init; }
}

public sealed record RecipeTimerNode
{
    public required int Duration { get; init; }
    public required string Unit { get; init; }       // "min" | "hr" | "sec"
    public string? Label { get; init; }
}
```

The discriminated union (`ContentStep` | `SectionStep`) replaces the current "either `text` or `section` exclusivity" footgun (CONCERNS §6) — it's enforced by the type system.

### Pattern 2: Versioned upcasters

**What:** A chain of pure functions that transform `version N` documents into `version N+1`.

**When:** Whenever the canonical document shape changes after release.

**Example:**

```csharp
// CookBot.Application/Recipes/Versioning/IRecipeUpcaster.cs
public interface IRecipeUpcaster
{
    int FromVersion { get; }                                     // e.g. 1
    int ToVersion { get; }                                       // e.g. 2
    JsonNode Apply(JsonNode source);                             // operates on the JSON form
}

// CookBot.Application/Recipes/Versioning/Upcasters/V1ToV2.cs
public sealed class V1ToV2 : IRecipeUpcaster
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public JsonNode Apply(JsonNode source)
    {
        var obj = source.AsObject();
        // Example v1→v2 changes (illustrative — actual changes locked at requirements):
        if (obj.TryGetPropertyValue("prepTimeMinutes", out var prep))
        {
            obj.Remove("prepTimeMinutes");
            obj["prepTimeMinutes"] = prep;       // unify on prepTimeMinutes (not prepTime)
        }
        // Section/text variant migration: { isSection: true, text: "Bake" } → { kind: "section", heading: "Bake" }
        // ...
        obj["version"] = 2;
        return obj;
    }
}
```

Why JSON-level (vs. typed C# transforms): once the schema gets to V3, V4, V5, you'd have to keep `RecipeDocumentV1`, `RecipeDocumentV2`, `RecipeDocumentV3` records around forever. JSON-level upcasters operate on `System.Text.Json.Nodes.JsonNode` and only the *current* C# record needs to exist. Pattern source: [event-driven.io: simple events versioning patterns](https://event-driven.io/en/simple_events_versioning_patterns/).

### Pattern 3: Validator returns a result, doesn't throw

**What:** Semantic validation post-parse, returns errors as data.

**When:** Always. Existing pattern in the codebase (`IRecipeFormatParser.TryParse`, `CookbookTransferService.Deserialize`).

**Example:**

```csharp
public sealed record RecipeValidationResult(bool IsValid, IReadOnlyList<RecipeValidationError> Errors);

public sealed record RecipeValidationError(string Path, string Code, string Message);

public static RecipeValidationResult Validate(RecipeDocument doc)
{
    var errors = new List<RecipeValidationError>();
    if (string.IsNullOrWhiteSpace(doc.Name))
        errors.Add(new("name", "required", "Recipe name is required."));
    if (doc.Servings <= 0)
        errors.Add(new("servings", "positive", "Servings must be > 0."));
    var ids = doc.Ingredients.Select(i => i.Id).ToList();
    if (ids.Distinct().Count() != ids.Count)
        errors.Add(new("ingredients", "uniqueId", "Ingredient ids must be unique within a recipe."));
    // ref-id matching
    var refRegex = new Regex(@"\[([^\]]+)\]\(#(\d+)\)");
    foreach (var (step, idx) in doc.Steps.OfType<ContentStep>().Select((s, i) => (s, i)))
        foreach (Match m in refRegex.Matches(step.Text))
            if (!ids.Contains(int.Parse(m.Groups[2].Value)))
                errors.Add(new($"steps[{idx}].text", "danglingRef",
                    $"Step references ingredient #{m.Groups[2].Value} which is not in ingredients."));
    return new(errors.Count == 0, errors);
}
```

### Pattern 4: AI structured-output orchestrator

**What:** A purpose-specific service that combines schema + validation + repair, hiding all of it from the UI.

**When:** Anywhere AI emits structured data. Already exists implicitly in `PantryAiPopulationService` (which has CONCERNS §20's 290 lines of JSON-repair heuristics — that goes away with structured output).

**Example:**

```csharp
public interface IAiRecipeGenerator
{
    Task<RecipeGenerationResult> GenerateAsync(
        string userPrompt,
        IReadOnlyList<AiMessage> conversation,
        UserProfile profile,
        EffectiveAiCredentials creds,
        CancellationToken ct = default);
}

public sealed record RecipeGenerationResult(
    bool Success,
    RecipeDocument? Recipe,
    IReadOnlyList<RecipeValidationError> Errors,
    string? RawResponseBody);   // surfaced on failure for "Edit raw text" affordance
```

### Pattern 5: Schema-derived prompt documentation

**What:** Generate the human-readable schema description shown in `/prompt-builder` and embedded in the system prompt **from the same record** that drives the JSON schema.

**When:** Any time the format spec would otherwise be a literal string in code.

**Implementation options:**
- Hand-write the markdown once, **next to the record**, as `[Description("...")]` on each property + a static `RecipeSchemaDocumentationProvider.RenderMarkdown()` that walks the record via reflection. (Simpler.)
- Use [`Microsoft.Extensions.AI`'s JSON schema exporter](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/extract-schema) (built into `System.Text.Json` in .NET 9+) to emit the schema, and a separate small markdown generator that walks the same record. (More automated.)

Either way, the literal string only exists in **one** place. CONCERNS §13 is closed.

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Putting schema definitions in `CookBot.Schemas` standalone project

**What:** Creating a fifth project for schemas.

**Why bad:** The records are pure POCOs with no framework dependencies. They already belong in `CookBot.Domain`, which is the "no NuGet refs" project. A fifth project adds solution complexity for no isolation benefit.

**Instead:** A namespace inside `CookBot.Domain/Recipes/`. Same dependency posture.

### Anti-Pattern 2: Decorating `IAiService` with recipe-specific orchestration

**What:** Wrap `IAiService` with a `ValidatingAiService` that runs schema checks for every call.

**Why bad:** `IAiService` is a generic AI primitive — it serves chat, pantry import, cooking-mode step assist, and (future) recipe generation. The repair pass logic and schema selection are recipe-specific. Forcing them into the AI primitive's middleware breaks the abstraction.

**Instead:** A separate `IAiRecipeGenerator` that *uses* `IAiService` plus a structured-output overload on `IAiService`. The orchestration logic lives next to recipe code, not next to HTTP.

### Anti-Pattern 3: Storing the canonical document as the only source

**What:** Drop the existing relational columns (`Recipe.Servings`, `Recipe.PrepTimeMinutes`, the `RecipeIngredient` table, etc.) and keep only `Recipe.CanonicalDocumentJson`.

**Why bad:** Loses indexed query support. `CookbookList.razor` filters/sorts by `Servings`, scaling queries hit `RecipeIngredient`, the autocomplete uses `Ingredients.NormalizedName`. Going JSON-only forces full-document scans.

**Instead:** Hybrid. Relational columns stay (the existing schema continues to power queries). The canonical document is an **additional** column that's recomputed on save and is the authoritative export/AI/import source. On read paths that don't need full structure, hit the relational columns. On export/AI/import, use the canonical document.

### Anti-Pattern 4: Migrating step text to a structured `StepDocument` model

**What:** Replace `RecipeStep.Text` (string) with `RecipeStep.Document` (a tree of inline nodes).

**Why bad:** Migration cost is high (every existing step needs converting), back-compat with YAML pasting needs a tree-to-string converter, and the AI prompt still wants strings. Three lossy round-trips for editor convenience.

**Instead:** Keep `Text` as the string. Tokenize at editor mount, serialize to markdown on save. The chip UI is a view, not a model.

### Anti-Pattern 5: Auto-detecting timers without user confirmation

**What:** Keep CONCERNS §7's silent regex auto-detection.

**Why bad:** False positives create timer chips the user didn't ask for. Cooking mode shows them as if they were authoritative.

**Instead:** Detection becomes a **suggestion** in the chip composer ("Did you mean a 25-minute timer?"). Explicit timer chips in the YAML/JSON win. No auto-rewriting on save.

---

## Migration Strategy for Existing Data

### Existing recipes in `cookbot.db`

**Migration:** `<timestamp>_RecipeCanonicalDocument`

```csharp
public partial class RecipeCanonicalDocument : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CanonicalDocumentJson",
            table: "Recipes",
            type: "TEXT",
            nullable: true);   // nullable to allow back-fill; tightened to required after back-fill
    }
    // Back-fill: see DatabaseSeeder addition below.
}
```

**Back-fill** runs as a one-shot in `DatabaseSeeder.SeedAsync` after `MigrateAsync()`:

```csharp
// In DatabaseSeeder, run once:
var stale = await db.Recipes
    .Include(r => r.Ingredients)
    .Include(r => r.Steps).ThenInclude(s => s.Timers)
    .Where(r => r.CanonicalDocumentJson == null)
    .ToListAsync();

foreach (var recipe in stale)
{
    var doc = LegacyRecipeProjector.Project(recipe);   // builds RecipeDocument from current cols
    recipe.CanonicalDocumentJson = JsonSerializer.Serialize(doc);
}
await db.SaveChangesAsync();
```

`LegacyRecipeProjector` is throwaway code (deleted after one release cycle): it reads the current relational shape and builds a `RecipeDocument` with `Version = CurrentVersion`. Since the source is the running DB, no upcaster runs — we project directly to current.

**Note on EF Core 10 JSON columns:** EF 10 changes JSON column behavior with `UseCompatibilityLevel(170)`, see the [EF 10 breaking changes doc](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/breaking-changes). For SQLite this is moot (SQLite has no native JSON column type — it's all `TEXT`), but the existing `OwnsMany(...).ToJson()` configurations are unaffected. **Confidence: MEDIUM**, recommend a quick smoke test on a copy of `cookbot.db` after the migration runs.

### Existing `.cookbook.json` exports in the wild

These have `SchemaVersion: 1` and the per-recipe shape that diverges from the YAML format (CONCERNS §2). The fix is in `CookbookTransferService.Deserialize`:

```csharp
public CookbookTransferDocument? Deserialize(string json, out List<string> errors)
{
    errors = new();
    var envelope = JsonSerializer.Deserialize<CookbookTransferEnvelope>(json, Options);
    if (envelope is null) { errors.Add("Empty document"); return null; }

    var recipes = new List<RecipeDocument>();
    foreach (var rawRecipe in envelope.Recipes)
    {
        // Stamp the version on entry. Old exports were V1 even though the field didn't exist.
        var asNode = JsonSerializer.SerializeToNode(rawRecipe)!.AsObject();
        asNode["version"] ??= envelope.SchemaVersion;
        var current = upcasterChain.UpcastToCurrent(asNode);
        var doc = current.Deserialize<RecipeDocument>(Options)!;
        var validation = validator.Validate(doc);
        if (!validation.IsValid)
            errors.AddRange(validation.Errors.Select(e => $"recipe '{rawRecipe.Name}': {e.Message}"));
        else
            recipes.Add(doc);
    }
    return new CookbookTransferDocument(envelope.SchemaVersion, recipes, ...);
}
```

The V1→V2 upcaster handles every divergence CONCERNS §2 lists: `prepTimeMinutes`/`cookTimeMinutes` → unified field, `isSection: bool` + `text` → `{kind, heading|text}` discriminated form, `localId` → `id`, missing `version` → `1`.

### YAML pastes from earlier versions of the AI

Old YAML pastes will lack a `version` field. The YAML deserializer stamps `version: 1` when the field is absent and routes through the upcaster chain. Same path as JSON imports.

---

## Scalability Considerations

| Concern | At 100 recipes | At 1K recipes | At 10K recipes |
|---------|---------------|---------------|----------------|
| Reading canonical document | Trivial — single column read | Trivial | Indexed by `RecipeId`, paginate list views (CONCERNS §30) |
| Recomputing canonical on save | One serialize per save | Same | Same (per-row cost) |
| Upcaster chain depth | 0–1 hops | Same | Cap at maybe 5 versions in the chain; keep a "rebake" job that rewrites old `CanonicalDocumentJson` to current to flatten chain depth |
| AI structured output token cost | Schema is ~2KB JSON | Same | Same (sent once per request) |
| AI repair-pass round trips | <5% need repair (estimate) | Same | Worth tracking: if the rate climbs, the schema or prompt is misaligned |

---

## Build Order (Dependency-Sorted)

This ordering ensures each step's deliverables are ready when the next step depends on them.

| # | Phase | Deliverable | Depends On | Why this order |
|---|-------|-------------|------------|----------------|
| 1 | **Canonical schema** | `RecipeDocument` record + `RecipeIngredientNode` + `RecipeStepNode` discriminated union + `RecipeSchemaConstants.CurrentVersion = 1` | nothing | Everything else projects from the record. Decisions land here. |
| 2 | **Serializers + JSON schema provider** | `YamlRecipeSerializer`, `JsonExportRecipeSerializer`, `RecipeJsonSchemaProvider`, `RecipeSchemaDocumentationProvider` | (1) | Required by AI structured output, by `IRecipeFormatParser` rewrite, and by prompt-builder consolidation. |
| 3 | **Versioning + upcaster scaffold + validator** | `IRecipeUpcaster`, `RecipeUpcasterChain` (initially empty — V1 is current), `RecipeValidator` | (1)(2) | Even with no upcasters today, the scaffold has to exist before any future schema change. Validator unblocks AI orchestration. |
| 4 | **Persistence layer change** | `Recipe.CanonicalDocumentJson` column + EF migration + back-fill in `DatabaseSeeder` + `LegacyRecipeProjector` | (1)(2)(3) | Persistence has to know what to store. Done before features that read it. |
| 5 | **`IRecipeFormatParser` rewrite** | Existing parser delegates to `YamlRecipeSerializer + UpcasterChain + Validator` | (1)(2)(3) | Existing call sites keep working; structured-output AI path piggybacks on validator/upcaster. |
| 6 | **`PromptBuilderService` consolidation** | `ResolveRecipeFormat` + `BuildCopyablePrompt` both read from `RecipeSchemaDocumentationProvider`; opt-out clause removed | (2) | One source of format text. Pre-req for AI structured output. |
| 7 | **AI structured output** | `IAiService.SendStructuredAsync` overload + `IAiRecipeGenerator` + `AiRecipeGenerator` + repair prompt builder | (3)(5)(6) + Anthropic `output_config` | Recipe-emit reliability. Replaces `AiChat.ExtractRecipeContent`. Makes the AI a trusted source. |
| 8 | **Cookbook transfer integration** | `CookbookTransferService.Deserialize` routes through upcaster; `SchemaVersion` bumped to 2; `BuildExportAsync` uses canonical document | (3)(4)(5) | Closes the loop on round-trip; old `.cookbook.json` files stay importable. |
| 9 | **Chip step composer** | `StepComposer.razor` + `js/step-composer.js` + tokenize/serialize pair + integration into `RecipeEditor.razor` | (1)(5) (because tokenizer reads ingredient list and writes `[name](#id)`) | UX win, doesn't block format work, can land independently. |
| 10 | **Format-driven new feature(s)** | One additive field exercised end-to-end: schema bump from V1 to V2, new `RecipeUpcaster` for V1→V2, UI surface, AI prompt update auto-flows from `RecipeSchemaDocumentationProvider` | all of the above | Validates that the versioning machinery actually works. Locks in the pattern for future additions. |
| 11 | **Cleanup of dead code** | Delete `AiChat.ExtractRecipeContent` heuristic cascade, delete the duplicated format strings, delete `LegacyRecipeProjector` (single-cycle helper) | all of the above | After everything proves out. |

**Critical path:** 1 → 2 → 3 → 7 (AI conformance is the longest chain).
**Parallel-safe:** 9 (chip composer) can start after 5 and run in parallel with 7-8.

### Phase Mapping Suggestion

The above 11 deliverables map to roughly **4 GSD phases**:

| Suggested Phase | Steps | Output |
|-----------------|-------|--------|
| Phase 1: "Canonical Format" | 1–6 | One source of truth across YAML/JSON/DB/prompt |
| Phase 2: "AI Conformance" | 7–8 | Reliable AI emission + import round-trip |
| Phase 3: "Chip Editor" | 9 | UX win for manual authoring |
| Phase 4: "Format-driven feature" | 10–11 | Exercises versioning, locks in the pattern |

Phases 1 and 2 are non-negotiable order. Phase 3 is parallelizable with Phase 2. Phase 4 must be last.

---

## Sources

### High-Confidence (Primary)

- [Anthropic Structured Outputs — Claude API Docs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs) — `output_config.format.type = "json_schema"` GA on Sonnet 4.5+ and Opus 4.1+.
- [Microsoft Learn — JSON schema exporter (System.Text.Json)](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/extract-schema) — built-in `JsonSchemaExporter` to derive schemas from .NET types.
- [Marten — Events Versioning](https://martendb.io/events/versioning.html) — canonical .NET reference for upcaster patterns on JSON-stored documents.
- [event-driven.io — Simple events versioning patterns](https://event-driven.io/en/simple_events_versioning_patterns/) — comparison of CLR-type vs. JSON-level upcasting.
- [EF Core 10 Breaking Changes](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/breaking-changes) — JSON column behavior changes; relevant for verifying `OwnsMany(...).ToJson()` migration.
- The codebase audit: `.planning/codebase/ARCHITECTURE.md`, `.planning/codebase/CONCERNS.md`, `.planning/codebase/STRUCTURE.md`.

### Medium-Confidence (Pattern References)

- [Snippets Ltd — Structured Outputs with Claude: JSON Schemas, Validation, and Retry Loops](https://snippets.ltd/blog/structured-outputs-with-claude-json-schemas-validation-retry-loops) — retry-with-error-feedback pattern; one-shot repair recommendation.
- [Towards Data Science — A Hands-On Guide to Anthropic's New Structured Output Capabilities](https://towardsdatascience.com/hands-on-with-anthropics-new-structured-output-capabilities/) — practical examples of `output_config` shape.
- [Corvus.JsonSchema](https://github.com/corvus-dotnet/Corvus.JsonSchema) — .NET 8+ JSON schema validator/generator with YAML support; alternative to System.Text.Json's exporter if features outgrow it.
- [json-everything — Source-Generated JSON Schemas](https://docs.json-everything.net/schema/schemagen/automatic-generation/) — `[GenerateJsonSchema]` source generator approach.
- [ProseMirror Document Model](https://prosemirror.net/) — reference for the inline-text-with-mention model that the chip composer mimics conceptually (without adopting ProseMirror itself).
- [MudBlazor Chips component](https://mudblazor.com/components/chips) — exists but is not a token-input field; the [open issue #328](https://github.com/MudBlazor/MudBlazor/issues/328) confirms a custom contenteditable approach is required.
- [Azure Cosmos DB design pattern: Schema Versioning](https://learn.microsoft.com/en-us/samples/azure-samples/cosmos-db-design-patterns/schema-versioning/) — generic version-field-on-document pattern reference.

### Lower-Confidence (Background)

- [Anthropic Cookbook — Extracting Structured JSON](https://github.com/anthropics/anthropic-cookbook/blob/main/tool_use/extracting_structured_json.ipynb) — pre-structured-outputs approach; useful as a fallback if structured outputs are unavailable on a given model.
- [MudBlazor.HtmlEditor](https://github.com/erinnmclaughlin/MudBlazor.HtmlEditor) — community rich-text editor; reference only, not recommended (overkill for our chip composer scope).

---

*Architecture research: 2026-04-25*
