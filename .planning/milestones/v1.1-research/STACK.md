# Technology Stack — Subsequent Milestone Additions

**Project:** FreelovesCookBot
**Milestone scope:** Recipe-mode UX without special syntax · canonical/versioned recipe format · reliable AI structured output · format-driven new features
**Researched:** 2026-04-25
**Overall confidence:** HIGH

This is an *additive* stack document. The existing baseline (.NET 10 / Blazor Server `InteractiveServer` / SQLite + EF Core 10 / MudBlazor 8.15 / Anthropic via raw `HttpClient` / YamlDotNet 16.3.0 / Markdig 0.45.0 / QuestPDF 2025.1.0 / xUnit 2.9.2) is documented in `.planning/codebase/STACK.md` and is **kept as-is** unless an entry below explicitly says otherwise. The intent is to add only what the milestone needs and to avoid churn.

## Stack Overview (additions only)

| Area | Recommendation | Status |
|------|---------------|--------|
| Anthropic API client | Keep raw `HttpClient` in `AnthropicAiService.cs`; add structured-outputs request shape (`output_config.format` + `strict: true` tools) | **Add behavior, no library swap** |
| AI structured output | Use Anthropic Structured Outputs (GA, model-side constrained decoding) — compile schema once, send with every recipe-emitting request | **Add** |
| JSON Schema generation | `System.Text.Json.Schema.JsonSchemaExporter` (BCL, .NET 10) | **Add (already on path via STJ)** |
| JSON Schema validation | `JsonSchema.Net` 9.x (`json-everything`) | **Add (one new package)** |
| Schema versioning pattern | In-document `version: <int>` field + ordered C# migration steps in `Application` layer (no library) | **Add (pattern, no package)** |
| Token/chip composer for steps | Build on existing `MudAutocomplete` + `MudChipSet<T>` from MudBlazor 8.15; do **not** introduce a rich-text editor | **Pattern only, no new package** |
| MudBlazor upgrade to 9.x | Defer — out-of-scope churn for this milestone (see "What NOT to add") | **Skip** |
| YamlDotNet | Keep at 16.3.0; treat YAML as a *display/paste* format, JSON as the canonical wire format | **No change** |
| Markdig | Keep at 0.45.0 | **No change** |
| Cooklang or third-party recipe DSL | Reject — own the canonical format | **Skip** |

## Recommendations by Area

### 1. Anthropic structured output — the core unlock

**Recommendation:** Adopt Anthropic's **Structured Outputs** feature (now GA on the Claude API as of late 2025/early 2026) inside the existing `AnthropicAiService.cs`. Send `output_config.format = { type: "json_schema", schema: <recipe schema> }` on every request that should produce a recipe, and use `strict: true` on tool definitions if/when tools are used.

**Why:** This replaces "tell the model nicely to emit our format" with **constrained decoding** at the inference layer — the model literally cannot emit tokens that violate the schema. This directly solves the milestone's "AI chat reliably emits the canonical format" goal and lets the existing opt-out clause (`PromptBuilderService.cs:201`) be removed without losing reliability.

**Confidence:** HIGH — feature is GA, documented at `platform.claude.com/docs/en/build-with-claude/structured-outputs`, supports all three curated models in this app (`claude-haiku-4-5`, `claude-sonnet-4-6`, `claude-opus-4-7`).

**Key facts to design against:**
- Request shape: top-level `output_config.format` (current standard). The older `anthropic-beta: structured-outputs-2025-11-13` header + `output_format` parameter still works during a transition window — pin to the GA shape.
- Strict tool use: `"strict": true` on each tool definition gives guaranteed-valid `input_schema` matching for tool calls (orthogonal to JSON output; can combine).
- Schema complexity ceilings per request: **20 strict tools**, **24 optional parameters total**, **16 union-typed parameters**. The recipe schema must stay well under these (it will — recipes are flat-ish).
- `additionalProperties: false` is required on every object node when used with strict mode. Bake this into the schema generator config.
- Streaming: structured outputs work with the existing SSE streaming flow; partial JSON arrives as `content_block_delta`. Validation/repair runs on the assembled final string, not per-chunk.

**Integration points in existing code:**
- `src/CookBot.Infrastructure/AI/AnthropicAiService.cs` — extend `SendMessageAsync` / `StreamMessageAsync` to accept an optional `JsonElement schema` (or a typed `RecipeOutputContract`) and serialize it into the request body.
- `src/CookBot.Application/Services/PromptBuilderService.cs:201` — remove the opt-out clause once structured output is on.
- `src/CookBot.Web/Components/Pages/AiChat.razor` (`ExtractRecipeContent` ladder) — replace the three-tier fallback with a single deterministic JSON parse, then validate, then **at most one** repair re-prompt.

### 2. Should we replace the raw `HttpClient` with the official Anthropic .NET SDK?

**Recommendation:** **No**, not in this milestone.

The official `Anthropic` NuGet package (v12.17.0, released 2026-04-24, "official Claude SDK for C# as of v10+") exists and exposes `IChatClient` from `Microsoft.Extensions.AI.Abstractions`. It would let you pipe the app through the broader MEAI ecosystem (function invocation, telemetry, etc.).

**Why defer:**
- The current `AnthropicAiService` is ~one file, already has streaming + curated-model + extended-thinking-filter logic working, and is wired to `IAiService`.
- The structured-outputs win is a JSON-body change, not a client change. Adding the SDK is independent churn.
- `Microsoft.Extensions.AI` and `IChatClient` are worth a *separate* future milestone if the app gains a second provider — the existing `IAiService` abstraction already covers that need today.
- Targets `net8.0` / `netstandard2.0`; works on .NET 10 but adds a transitive dependency (`Microsoft.Extensions.AI.Abstractions >= 10.4.0`) and a learning curve.

**Confidence:** HIGH on "defer" — current code works; SDK is a quality-of-life upgrade, not a feature unlock.

**Reconsider when:** A second AI provider is added, function-calling pipelines get complex, or telemetry/observability becomes a goal.

### 3. JSON Schema generation — `System.Text.Json.Schema.JsonSchemaExporter`

**Recommendation:** Use `System.Text.Json.Schema.JsonSchemaExporter` (BCL, .NET 10). Generate the recipe schema at startup from the canonical C# DTO (`CanonicalRecipe` or similar) and cache it.

**Why:** It's in the BCL — zero new dependencies. It's the same exporter that powers ASP.NET Core OpenAPI, Semantic Kernel, and `Microsoft.Extensions.AI` tool-calling, so the output JSON Schema is already shaped to be consumed by LLM provider APIs (including Anthropic's `output_config.format`). Honors `[JsonPropertyName]`, `[JsonRequired]`, nullability, polymorphism via `[JsonDerivedType]`.

**Confidence:** HIGH — Microsoft Learn docs confirm GA in .NET 9, available in .NET 10. The app already serializes everything via `System.Text.Json`.

**Configuration to set:**
- `JsonSchemaExporterOptions.TreatNullObliviousAsNonNullable = true` — matches the project's `<Nullable>enable</Nullable>` posture so reference types only become nullable when explicitly `T?`.
- Post-process the emitted `JsonNode` to inject `additionalProperties: false` on every `object`-typed node (Anthropic strict mode requires this; STJ does not emit it by default).

**Integration points:**
- New file: `src/CookBot.Application/Recipes/Canonical/CanonicalRecipeSchema.cs` — static `JsonNode Schema { get; }` cached singleton.
- DI: register as `Singleton<CanonicalRecipeSchema>` so all callers share the cached schema and the post-processed-for-Anthropic variant.

### 4. JSON Schema validation — `JsonSchema.Net` 9.x

**Recommendation:** Add `JsonSchema.Net` (the `json-everything` package, currently 9.2.0) as a single new dependency for **runtime validation** of inbound recipes (AI responses, paste-in JSON, imported `.cookbook.json`).

**Why this and not the alternatives:**

| Option | Verdict | Reason |
|--------|---------|--------|
| `JsonSchema.Net` | **Pick** | First-party `System.Text.Json` integration (no `JObject` round-trips), supports JSON Schema drafts 6, 7, 2019-09, **2020-12** (which is what `JsonSchemaExporter` emits), MIT, actively maintained, used by `Microsoft.Extensions.AI`. |
| `NJsonSchema` | Skip | Mature and feature-rich (RicoSuter), but historically `Newtonsoft.Json`-rooted; the codebase is 100% `System.Text.Json`. Adding `Newtonsoft.Json` as a transitive is unnecessary churn. |
| `Newtonsoft.Json.Schema` | Skip | Commercial license above small-project usage caps. GPL-3.0 compatibility unclear; not worth the friction. |
| `LateApexEarlySpeed.JsonSchema` | Skip | Faster in micro-benchmarks but smaller surface, less mainstream — premature optimization for a single-host self-host app. |

**Confidence:** HIGH on `JsonSchema.Net`; the STJ-native angle is decisive given the rest of the codebase.

**License:** MIT — GPL-3.0 compatible.

**Integration points:**
- `src/CookBot.Application/Recipes/Canonical/CanonicalRecipeValidator.cs` — `ValidationResult Validate(JsonNode payload)`, returns either parsed `CanonicalRecipe` or a structured list of errors (path + message).
- `src/CookBot.Web/Components/Pages/AiChat.razor` — call validator after the model returns; on failure, send **one** repair prompt that includes the error list, then surface a clear UI failure.
- `src/CookBot.Web/Services/CookbookTransferService.cs` — validate incoming `.cookbook.json` before EF Core mapping; on validation failure, run the version-migration pipeline (see §5).

### 5. Canonical format & schema versioning — pattern, not package

**Recommendation:** Define the canonical format as a single C# DTO tree under `src/CookBot.Application/Recipes/Canonical/`, with an explicit top-level `int Version` field. Keep migrations as ordered, pure functions in code (e.g. `IRecipeMigration` with `int FromVersion`, `JsonNode Apply(JsonNode)`). No third-party migration framework needed at this scale.

**Layout:**

```
src/CookBot.Application/Recipes/Canonical/
  CanonicalRecipe.cs              # current-version DTO
  CanonicalRecipeSchema.cs        # JsonSchemaExporter-cached schema
  CanonicalRecipeValidator.cs     # JsonSchema.Net validator
  Migrations/
    IRecipeMigration.cs
    Migration_V1_To_V2.cs         # current `.cookbook.json` v1 -> canonical v2
    RecipeMigrationPipeline.cs    # reads version, runs ordered migrations to current
```

**Why no library:**
- Schema migration libraries (e.g. JSON-Schema-Migrate ports) are heavier than the problem.
- The .NET community standard for JSON document upgrades is "read `version`, switch on it, mutate `JsonNode`" — clean, debuggable, testable with xUnit.
- EF Core migrations already cover *database* schema; this is *document* schema.

**Backward-compatibility path required by the milestone constraint:**
- `.cookbook.json` files in the wild today have `SchemaVersion = 1` on the **outer envelope** (`CookbookTransferDocument`) but recipes inside are not version-stamped. Migration step v1 → v2 inserts `version: 2` on every recipe and rewrites `prepTimeMinutes`/`cookTimeMinutes`/`IsSection`/`localId` into the canonical names. Outer envelope `SchemaVersion` bumps to 2 in the same step.

**Confidence:** HIGH — pattern is well-established; the codebase already has `CookbookTransferDocument.SchemaVersion = 1` so the precedent exists.

**Integration points:**
- `src/CookBot.Application/DTOs/CookbookTransferDtos.cs` — bump `SchemaVersion` constant; add `MinimumSupportedVersion`.
- `src/CookBot.Web/Services/CookbookTransferService.cs` — call `RecipeMigrationPipeline.UpgradeToCurrent(json)` before deserialization.
- xUnit tests under `tests/CookBot.Tests/Recipes/Canonical/` — golden-file v1 sample + assertion that it round-trips through the migration to v2 lossless.

### 6. Token/chip step composer — pattern over `MudAutocomplete` + `MudChipSet`

**Recommendation:** Build the no-syntax step composer using **what MudBlazor 8.15 already ships**:

- `MudAutocomplete<Ingredient>` — typeahead pinned to the existing 600+ ingredient seed (`seeds/ingredients.json`)
- `MudChipSet<T>` with `MudChip<T>` instances, each chip carrying an `int IngredientId` + display text
- `MudIconButton` for an inline "+ timer" affordance that prompts for a duration via `MudNumericField` and inserts a typed chip

The step itself is modeled as `List<StepToken>` where `StepToken` is one of `TextToken("preheat oven to ")`, `IngredientToken(IngredientId, DisplayName)`, `TimerToken(TimeSpan)`. Serialization to the canonical JSON is a 1:1 mapping. Round-trip back to the YAML *display* form (if still kept for paste-in) is a small renderer.

**Why not a rich-text editor (TinyMCE / Quill / Tiptap / Syncfusion / Blazored.TextEditor):**
- Rich-text editors store HTML or Delta JSON; both leak presentation into the canonical format and fight the "schema validates everything" goal.
- Mention/token rich-text in Blazor universally requires JS interop wrappers (Tribute.js, Quill mention modules) — the project explicitly minimizes JS (only `cooking-timers.js` and `download.js`).
- Licensing — TinyMCE and Syncfusion have commercial tiers that complicate GPL-3.0 status; Blazored.TextEditor (Quill) and Tiptap are MIT but still drag in heavy JS.
- The composer surface area is small: text + ingredient chips + timer chips. A custom Razor component on top of MudBlazor primitives is ~300 lines and stays in C#.

**Confidence:** HIGH on the pattern — MudBlazor's chip + autocomplete primitives are explicitly designed for this composition (community feature requests in `MudBlazor#328`, `#7423` are exactly this scenario, and the answer has been "compose the existing primitives").

**Integration points:**
- New: `src/CookBot.Web/Components/Recipes/StepComposer.razor` (+ `.razor.cs`) — replaces the raw textarea step entry in `RecipeEditor.razor`.
- New: `src/CookBot.Application/Recipes/Canonical/StepToken.cs` — discriminated union (`[JsonDerivedType]` polymorphism) for canonical persistence.
- `RecipeEditor.razor` — wire `StepComposer` into the step list; remove the `[name](#id)` syntax help text.
- `RecipeFormatParser.cs` — keeps responsibility for **paste-in** parsing (free-form / numbered / YAML), but emits `List<StepToken>` directly so there's a single internal representation.

### 7. YAML round-trip — keep YamlDotNet 16.3.0 as a *display/paste* layer only

**Recommendation:** Keep `YamlDotNet` 16.3.0 (current latest is 17.0.1; upgrade is optional and orthogonal to this milestone). Demote YAML from "wire format" to "human-paste / human-display format only." JSON (validated against the schema) becomes the **canonical** representation everywhere — DB JSON columns, AI request/response, `.cookbook.json` export, in-memory.

**Why:**
- YAML's appeal was human-readable paste-in; it's still good at that.
- YAML's downsides for our use: no comment-preserving round-trip (long-standing YamlDotNet limitation, see `aaubry/YamlDotNet#96`, `#152`, `#451`), implicit-typing footguns (Norway problem, version strings auto-quoted differently across libraries), no native JSON-Schema toolchain.
- Anthropic Structured Outputs only accepts JSON Schema, not YAML schema.
- A single canonical JSON eliminates the "three competing serializations" problem flagged in `.planning/codebase/CONCERNS.md` §1–4.

**No alternative YAML library is needed.** Other .NET YAML options (YamlConfiguration, custom forks) don't materially solve the round-trip-comments gap, and we don't need it because YAML is no longer the canonical store.

**Confidence:** HIGH on the demotion strategy; MEDIUM on whether to bump YamlDotNet to 17.0.1 in the same milestone — acceptable but not required.

**Integration points:**
- `src/CookBot.Application/Services/RecipeFormatParser.cs` — keep YAML *parser* path; redirect output to `CanonicalRecipe` instead of the current ad-hoc shape.
- Add `CanonicalRecipeYamlRenderer` for the "Copy as YAML" affordance if any UX still wants it (likely just the prompt builder).

### 8. Cooklang and other domain DSLs — explicit reject

**Recommendation:** Do **not** adopt Cooklang (or RecipeML, JSON-LD `Recipe`, MealMaster) as the canonical format.

**Why:**
- Cooklang is a fine plain-text format but binds the recipe body to its specific `@ingredient{quantity}` / `#cookware` / `~timer{}` syntax, which is exactly the "users shouldn't have to know our special syntax" *anti-goal* of this milestone.
- Schema.org `Recipe` (JSON-LD) is great for SEO/interop *export* but is loosely typed (free-text instructions) and undermines structured-output/validation guarantees.
- Owning the canonical format (with version field) lets us add per-step temperature, ingredient substitutions, expiration dates, equipment requirements without negotiating with an external spec.

**Optional follow-up (not this milestone):** Expose a one-way *export* to Schema.org `Recipe` JSON-LD if SEO becomes a goal. Out of scope now.

**Confidence:** HIGH.

## What NOT to Add (and Why)

| Not adding | Why |
|------------|-----|
| Official `Anthropic` NuGet SDK (v12.17.0) | Existing `HttpClient`-based service works; structured outputs is a body-shape change, not a client change. Re-evaluate when a second provider is added. |
| `Microsoft.Extensions.AI` / `IChatClient` | Same reason — `IAiService` already abstracts the provider; no current driver for the broader MEAI surface. |
| MudBlazor 9.x upgrade (current latest 9.4.0) | Major version with breaking changes; not required to deliver this milestone. The chip/autocomplete primitives we need are stable in 8.15. Treat as a separate maintenance milestone. |
| `Newtonsoft.Json` / `Newtonsoft.Json.Schema` | App is 100% `System.Text.Json`; adding Newtonsoft pulls in ~400 KB and a parallel JSON model. License of `Newtonsoft.Json.Schema` is also commercial above small-project caps. |
| `NJsonSchema` | Newtonsoft-rooted; STJ alternative `JsonSchema.Net` is a cleaner fit. |
| Cooklang / RecipeML / Schema.org as canonical | Defeats "no special syntax" goal and gives up control over versioning. |
| TinyMCE / Quill / Tiptap / Syncfusion / Blazored.TextEditor | Rich-text editors don't fit a typed-token model; all require JS interop; licensing varies; over-scoped for the composer's actual surface. |
| A second YAML library | YamlDotNet 16.3.0 is sufficient as a paste/display layer. The comment-round-trip pain disappears once YAML is no longer canonical. |
| A schema migration framework | At ~one or two version transitions, ordered C# functions are simpler than any library. |
| OpenAI / Gemini / local-model clients | Out of milestone scope per `PROJECT.md`; structured outputs design must stay portable across `IAiService` but no second implementation lands here. |
| Identity middleware / OAuth | Out of scope per `PROJECT.md`; trusted-LAN posture unchanged. |
| Containerization (Docker), CI workflows | Out of scope per `PROJECT.md`. |

## Integration Points (where the additions land in the existing tree)

```
src/
  CookBot.Application/
    Recipes/
      Canonical/                                  # NEW namespace
        CanonicalRecipe.cs                        # NEW — versioned DTO (with `Version` int)
        StepToken.cs                              # NEW — text/ingredient/timer discriminated union
        CanonicalRecipeSchema.cs                  # NEW — JsonSchemaExporter cache + Anthropic post-processing
        CanonicalRecipeValidator.cs               # NEW — JsonSchema.Net wrapper, error surface
        Migrations/
          IRecipeMigration.cs                     # NEW
          Migration_V1_To_V2.cs                   # NEW
          RecipeMigrationPipeline.cs              # NEW
    Services/
      RecipeFormatParser.cs                       # CHANGE — emit CanonicalRecipe instead of legacy shape
      PromptBuilderService.cs                     # CHANGE — remove opt-out clause; embed schema reference
  CookBot.Infrastructure/
    AI/
      AnthropicAiService.cs                       # CHANGE — accept JSON schema; send output_config.format; strict tools
  CookBot.Web/
    Components/
      Pages/
        AiChat.razor                              # CHANGE — replace 3-tier extractor with parse→validate→repair-once
        RecipeEditor.razor                        # CHANGE — replace step textarea with <StepComposer>
      Recipes/                                    # NEW folder
        StepComposer.razor + .razor.cs            # NEW — MudAutocomplete + MudChipSet composition
        IngredientChip.razor                      # NEW
        TimerChip.razor                           # NEW
    Services/
      CookbookTransferService.cs                  # CHANGE — run RecipeMigrationPipeline on import
tests/
  CookBot.Tests/
    Recipes/Canonical/                            # NEW — schema gen + validator + migration tests
```

## Installation (additive — single new package)

```bash
# Single new dependency
dotnet add src/CookBot.Application package JsonSchema.Net --version 9.2.*

# Nothing else — System.Text.Json.Schema.JsonSchemaExporter is BCL.
# Anthropic structured outputs is an HTTP body change, not a package.
# MudAutocomplete/MudChipSet are already in MudBlazor 8.15.
```

## Confidence Assessment

| Area | Confidence | Reason |
|------|------------|--------|
| Anthropic Structured Outputs adoption | HIGH | GA documented at platform.claude.com; supported on all three curated models in this app (Haiku 4.5 / Sonnet 4.6 / Opus 4.7); confirmed via Anthropic platform docs and multiple independent writeups. |
| Defer official Anthropic .NET SDK | HIGH | Existing service is small and works; no provider abstraction need this milestone. |
| `JsonSchemaExporter` (BCL) | HIGH | Microsoft Learn confirms .NET 10 availability; powers ASP.NET Core OpenAPI and `Microsoft.Extensions.AI`. |
| `JsonSchema.Net` 9.x | HIGH | json-everything is the de facto STJ-native choice; supports draft 2020-12 (matches exporter output); MIT-licensed; actively maintained. |
| Token composer on MudAutocomplete + MudChipSet | HIGH | Components exist and are stable in MudBlazor 8.15; community feature requests (`#328`, `#7423`) confirm this is the intended composition. |
| Skip MudBlazor 9.x upgrade | HIGH | 9.4.0 (released 2026-04-22) is current; upgrading is optional churn unrelated to milestone goals. |
| Demote YAML, JSON canonical | HIGH | Anthropic structured outputs is JSON-only; comment round-trip in YAML is a known unsolved area in YamlDotNet; eliminates "three serializations" problem. |
| Reject Cooklang | HIGH | Direct conflict with "no special syntax" milestone goal. |
| Schema-versioning pattern (no library) | HIGH | Standard practice; existing `CookbookTransferDocument.SchemaVersion = 1` already establishes the pattern in-tree. |
| Skip rich-text editor libraries | HIGH | None match the typed-token model; all add JS interop; license/scope mismatch. |

## Open Questions for Roadmap

These are not blockers — flag for phase-specific research later if needed:

1. **Schema complexity headroom** — The recipe schema with ingredient substitutions + per-step temperature + equipment will need to stay under Anthropic's 24-optional-parameter total. Worth a sanity-check pass once the new fields are locked down.
2. **Streaming + structured outputs UX** — Confirm during phase build that partial JSON streaming chunks render acceptably in `AiChat.razor` (the user sees building JSON, not building markdown). May want a "compose then reveal" mode.
3. **Repair prompt budget** — One repair attempt on validation failure is the proposed budget; verify in testing that this is enough with Sonnet 4.6 / Opus 4.7 once schemas stabilize.

## Sources

- [Anthropic Structured Outputs (official docs)](https://platform.claude.com/docs/en/build-with-claude/structured-outputs) — HIGH; GA, request shape, model coverage, complexity limits, strict tool use.
- [Anthropic C# SDK docs](https://platform.claude.com/docs/en/api/sdks/csharp) — HIGH; confirms official SDK identity.
- [Anthropic NuGet package (v12.17.0, 2026-04-24)](https://www.nuget.org/packages/Anthropic/) — HIGH; current version, MEAI integration, target frameworks.
- [Microsoft Learn — JsonSchemaExporter (.NET 10)](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/extract-schema) — HIGH; BCL availability, API shape.
- [Microsoft Learn — JsonSchemaExporter API reference](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.schema.jsonschemaexporter?view=net-10.0) — HIGH.
- [JsonSchema.Net on NuGet (9.2.0)](https://www.nuget.org/packages/JsonSchema.Net) — HIGH; current version, license, draft support.
- [MudBlazor NuGet (9.4.0, 2026-04-22)](https://www.nuget.org/packages/MudBlazor) — HIGH; informs the "skip the 9.x upgrade this milestone" decision.
- [MudBlazor Chips component docs](https://mudblazor.com/components/chips) — HIGH; confirms `MudChipSet`/`MudChip` API.
- [MudBlazor token-input feature requests #328](https://github.com/MudBlazor/MudBlazor/issues/328), [#7423](https://github.com/MudBlazor/MudBlazor/issues/7423) — MEDIUM; community confirmation that the autocomplete+chipset composition is the intended pattern.
- [YamlDotNet comment-preservation issues #96](https://github.com/aaubry/YamlDotNet/issues/96), [#152](https://github.com/aaubry/YamlDotNet/issues/152), [#451](https://github.com/aaubry/YamlDotNet/issues/451) — HIGH; documents the long-standing limitation that motivates demoting YAML from canonical.
- [YamlDotNet on NuGet (17.0.1)](https://www.nuget.org/packages/YamlDotNet) — HIGH; confirms current version (we stay on 16.3.0 for this milestone).
- [Cooklang format comparison](https://cooklang.org/blog/41-recipe-formats-for-developers/) — HIGH; confirms Cooklang is a special-syntax format (rejected for the no-syntax goal).
- [JSON Schema versioning guidance — json-everything example](https://docs.json-everything.net/schema/examples/version-selection/) — MEDIUM; pattern reference for in-document version field.
- [What's new in System.Text.Json (.NET 9 baseline)](https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-9/) — HIGH; JsonSchemaExporter introduction.

---

*Stack additions research: 2026-04-25*
