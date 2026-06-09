# Architecture Research

**Domain:** v1.4 Recipe Data & Interoperability — integration of 5 new themes into existing Clean/Onion .NET 10 Blazor Server app
**Researched:** 2026-06-05
**Confidence:** HIGH (codebase read directly; external specs verified against official docs)

---

## System Overview — Existing Architecture (v1.3 baseline)

```
┌─────────────────────────────────────────────────────────────────────┐
│  CookBot.Web  (Blazor Server, InteractiveServer)                     │
│  ┌──────────┐ ┌───────────┐ ┌──────────────┐ ┌───────────────────┐  │
│  │RecipeView│ │RecipeEditor│ │AiChat.razor  │ │CookbookPdfService │  │
│  │          │ │            │ │              │ │CookbookTransfer   │  │
│  └──────────┘ └───────────┘ └──────────────┘ │LocalRecipePhoto   │  │
│                                               │  Storage          │  │
│                                               └───────────────────┘  │
├─────────────────────────────────────────────────────────────────────┤
│  CookBot.Application  (pure business logic, no DB/HTTP)              │
│  ┌───────────────┐ ┌──────────────┐ ┌────────────────────────────┐   │
│  │RecipeDocument │ │RecipeUpcaster│ │RecipeJsonSchemaProvider     │   │
│  │  (Domain rec) │ │Chain v1→v2→v3│ │(Anthropic structured-output│   │
│  │               │ │              │ │ schema, cached Lazy<>)      │   │
│  └───────────────┘ └──────────────┘ └────────────────────────────┘   │
│  ┌───────────────┐ ┌──────────────┐ ┌────────────────────────────┐   │
│  │RecipeValidator│ │JsonRecipe    │ │PromptBuilderService         │   │
│  │(semantic)     │ │Serializer    │ │AiRecipeGenerator            │   │
│  └───────────────┘ └──────────────┘ └────────────────────────────┘   │
├─────────────────────────────────────────────────────────────────────┤
│  CookBot.Infrastructure  (adapters: EF Core 10 + SQLite, HTTP)       │
│  ┌───────────────┐ ┌──────────────┐ ┌────────────────────────────┐   │
│  │CookBotDbContext│ │Migrations    │ │AnthropicAiService          │   │
│  │Recipe entity  │ │(forward-only,│ │(HttpClient, IAiService +   │   │
│  │.CanonicalDoc  │ │ auto-applied)│ │ IStructuredAiService)      │   │
│  │  Json         │ │              │ │                            │   │
│  └───────────────┘ └──────────────┘ └────────────────────────────┘   │
├─────────────────────────────────────────────────────────────────────┤
│  CookBot.Domain  (pure POCOs, no framework refs)                     │
│  ┌──────────────────────────────────────────────────────────────┐    │
│  │ RecipeDocument (sealed record, v3)  IngredientEntry           │    │
│  │ StepNode → ContentStep | SectionStep  StepTemperature         │    │
│  │ TimerEntry  NutritionalInfo (stub class, unused today)        │    │
│  └──────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
```

**Key invariants carried from v1.3:**
- UI reads canonical `RecipeDocument` via `Recipe.CanonicalDocumentJson` + `JsonRecipeSerializer`. Display-only layers never mutate canonical.
- Schema bumps ride the upcaster chain (`RecipeUpcasterChain`). `RecipeUpcasterChain.CurrentVersion` is the version gate.
- System.Text.Json only. No Newtonsoft, no NJsonSchema.
- `RecipeJsonSchemaProvider` drives Anthropic structured-output — any schema bump requires a provider update (it reflects the POCO type at runtime via `JsonSchemaExporter`).

---

## Theme 1: Richer Format (v3 → v4)

### What is new vs. modified

**New — CookBot.Domain/Recipes/**

| New type | Where | Fields | Notes |
|---|---|---|---|
| `IngredientSubstitution` (record) | `IngredientEntry` | `string Name`, `string? Note` | Simple POCO |
| `DonenessHint` (record or string alias) | `ContentStep` | `string? Doneness` — plain string (e.g., "golden brown", "internal temp 165°F") | Keep as a string; avoid a structured enum that the AI must hit exactly |
| `EquipmentEntry` (record) | `RecipeDocument` | `string Name`, `string? Note` | Recipe-level list |

**Modified — existing types**

| Type | Change |
|---|---|
| `IngredientEntry` | Add `IReadOnlyList<IngredientSubstitution>? Substitutions { get; init; }` |
| `ContentStep` | Add `string? DonenessCue { get; init; }` |
| `RecipeDocument` | Add `IReadOnlyList<EquipmentEntry> Equipment { get; init; } = []`; add `RecipeProvenance? Provenance { get; init; }` |

**New — CookBot.Domain/Recipes/RecipeProvenance.cs**

Fields: `string? SourceName`, `string? SourceUrl`, `string? OriginalAuthor`, `DateOnly? AdaptedDate`. All optional / nullable.

**New — CookBot.Application/Recipes/Migration_V3_To_V4.cs**

Follows V2→V3 precedent exactly: per-field null-coalescing no-ops + stamps `version: 4`. Fields are all nullable on the new POCO properties so STJ maps absent→null automatically; guards document the contract and prevent bundle-throw (PITFALLS C7).

**Modified — RecipeUpcasterChain.cs**
`CurrentVersion = 4` (was 3).

**Modified — RecipeValidator.cs**
- Add warning (not error) for `EquipmentEntry` with empty name.
- Add warning for ingredient substitution where `Name` is empty.
- `DonenessCue` is a free string — no validation rule needed beyond length.
- `Provenance.SourceUrl` — if present, warn if not a valid http/https URL (use `Uri.TryCreate`).

**Modified — RecipeJsonSchemaProvider.cs**
No code change — the provider reflects `RecipeDocument` via `JsonSchemaExporter` at runtime, so adding the new properties to the POCO automatically updates the Anthropic structured-output schema on next startup. Verify the Anthropic `anyOf`/`additionalProperties` post-walk still behaves with the new nested types.

**Modified — RecipeSchemaDocumentationProvider.cs + PromptBuilderService.cs**
Add human-readable documentation for the four new fields to the AI system prompt section. Equipment is recipe-level. Substitutions are per-ingredient. DonenessCue is per-step. Provenance is recipe-level optional metadata.

**Modified — RecipeFormatParser.cs / JsonRecipeSerializer.cs**
Serializer just works (new fields round-trip via STJ). Parser needs YAML-side mapping for any YAML wire-format representation of the new fields. Since the canonical format is JSON-first and YAML is the copy/paste surface, document YAML representations in the format spec section of the prompt.

**Modified — CookbookTransferDtos.cs**
The `CookbookTransferRecipe` DTO inside the export file has historically been a flat shape separate from `RecipeDocument`. With v1.3, that gap should be narrowed. For v1.4, the simplest path is: let `CookbookTransferService` serialize the canonical `RecipeDocument` JSON directly as the recipe payload (rather than a bespoke flat DTO), then on import deserialize through `RecipeUpcasterChain`. This eliminates the DTO-shape divergence once and for all.

### EF column decisions

All four new field groups (`Equipment`, `Substitutions`, `DonenessCue`, `Provenance`) live **exclusively inside `CanonicalDocumentJson`** — no new EF columns.

Rationale:
- These are format-level fields consumed only by the canonical read path. No query needs to filter by equipment, substitution, or provenance.
- Adding EF columns for display-only data would require denormalized sync logic between the column and the JSON blob — the same problem v1.3 addressed for `PhotoUrl`/`Description`, which are duplicated columns only because they feed `Recipe.PhotoUrl` for the photo display pipeline (non-canonical read path). No equivalent consumer exists for the new v4 fields.
- `Recipe.Description` and `Recipe.PhotoUrl` remain the only bridged columns; no new ones for v4.

### Data flow for schema bump

```
Import / AI generation
    ↓
RecipeUpcasterChain.UpcastToCurrent(jsonNode)   [v3 → v4 via Migration_V3_To_V4]
    ↓
JsonRecipeSerializer.Deserialize<RecipeDocument>(json)
    ↓
RecipeValidator.Validate(doc)                   [new warnings for provenance URL, empty equipment]
    ↓
Recipe.CanonicalDocumentJson = serialized v4 JSON
    ↓
RecipeView / Editor read doc.Equipment, doc.Provenance, step.DonenessCue, ing.Substitutions
```

---

## Theme 2: Schema.org JSON-LD Export

### Classification

Read-only projection. No schema change to `RecipeDocument` or DB. Analogous to `CookbookPdfService` (Web layer service, accepts a `RecipeDocument`, produces a formatted output).

### New component: `RecipeJsonLdService`

**Layer:** `CookBot.Web/Services/` — it has no DB access needs (the Razor page already loaded the `RecipeDocument`), so Web layer is correct. If it later needs to be testable in isolation from Blazor, move to `CookBot.Application/` and accept `RecipeDocument` directly (preferred — Application layer is the right home for format projections).

**Recommended layer: CookBot.Application/Recipes/JsonLdRecipeProjector.cs**

Rationale: It is a pure function from `RecipeDocument` → `string` (JSON-LD), has no Blazor dependencies, and is testable with xUnit. Follow the same pattern as `RecipeSchemaDocumentationProvider`.

**Method signature:**
```csharp
public string Project(RecipeDocument doc, string canonicalPageUrl);
```

Returns a JSON string ready for injection into `<script type="application/ld+json">`.

**Required Schema.org fields from RecipeDocument v4:**

| JSON-LD property | Source in RecipeDocument | Notes |
|---|---|---|
| `@context` | static: `"https://schema.org"` | |
| `@type` | static: `"Recipe"` | |
| `name` | `doc.Name` | required by Google |
| `image` | `doc.PhotoUrl` | required by Google; omit if null |
| `description` | `doc.Description` | recommended |
| `recipeYield` | `doc.Servings.ToString()` | required if nutrition present |
| `prepTime` | ISO 8601 duration from `doc.PrepTimeMinutes` | `PT{N}M` |
| `cookTime` | ISO 8601 duration from `doc.CookTimeMinutes` | `PT{N}M` |
| `totalTime` | sum of prep+cook if both present | |
| `recipeIngredient` | flatten `IngredientEntry` → `"{amount} {unit} {name}"` string array | |
| `recipeInstructions` | `ContentStep` items → `HowToStep` array; `SectionStep` → `HowToSection` | section wraps its content steps |
| `keywords` | `doc.Tags` joined by `, ` | |
| `author` | `doc.Provenance.OriginalAuthor` if present | `Person` type |
| `url` | `canonicalPageUrl` | the recipe page URL passed by caller |

**Emission point in RecipeView.razor:**
```razor
@if (_jsonLdScript is not null)
{
    <HeadContent>
        <script type="application/ld+json">@((MarkupString)_jsonLdScript)</script>
    </HeadContent>
}
```

Blazor Server's `<HeadContent>` component injects into `<head>` at render time, which is correct for SEO crawlers hitting server-rendered HTML.

**Optional download:**
Add a "Copy JSON-LD" button on RecipeView alongside the existing export buttons. Reuse `CookbookDownloadHelper` / `download.js` interop.

### Integration points

- **New:** `JsonLdRecipeProjector.cs` in `CookBot.Application/Recipes/`
- **Modified:** `RecipeView.razor` — inject `JsonLdRecipeProjector`, call on load, emit `<HeadContent>` script block
- **No DB change, no new EF migration**

---

## Theme 3: Cooklang Export

### Classification

Read-only projection from `RecipeDocument` → `.cook` text. No import, no schema change.

### New component: `CooklangRecipeProjector.cs`

**Layer:** `CookBot.Application/Recipes/` — same rationale as JSON-LD projector. Pure function.

**Method signature:**
```csharp
public string Project(RecipeDocument doc);
```

**Mapping strategy:**

| Cooklang element | RecipeDocument source | Notes |
|---|---|---|
| YAML frontmatter `---` | `name`, `servings`, `prepTimeMinutes`, `cookTimeMinutes`, `tags` | Optional — Cooklang supports YAML front matter |
| Step paragraph (blank-line separated) | `ContentStep.Text` with ingredient refs rewritten | See rewrite rule below |
| `@ingredient{amount%unit}` | Per `ContentStep`, expand `[name](#id)` refs to `@name{amount%unit}` using ingredient list | Look up `IngredientEntry` by id |
| `#cookware{}` | `doc.Equipment` items | Emit as a preamble comment or prepend to first step mentioning the equipment |
| `~{duration%unit}` | `TimerEntry` in `ContentStep.Timers` | Inject inline into the step text at end of sentence |
| `== Section ==` | `SectionStep.Heading` | Cooklang section syntax |
| Metadata | `doc.Provenance.SourceName`, `SourceUrl` | YAML front matter keys |

**Ingredient ref rewrite rule:**
The step text contains `[flour](#2)`. Look up `IngredientEntry` with `Id == 2` in `doc.Ingredients`. Emit `@flour{2%cups}` (using the ingredient's `Amount` and `Unit`). If the same ingredient is referenced in multiple steps, Cooklang convention is to repeat the `@` annotation each time — this is correct behavior.

**DonenessCue mapping:** Append as a Cooklang comment `-- doneness: {cue}` on the same step line, or inline as prose in the step text. Given Cooklang doesn't have a native doneness construct, append as `-- {DonenessCue}` comment.

**Substitutions:** No Cooklang native; skip or append as `-- substitutions: {list}` comment.

**Integration points:**

- **New:** `CooklangRecipeProjector.cs` in `CookBot.Application/Recipes/`
- **Modified:** `RecipeView.razor` — add "Export as .cook" button; call projector, download via `download.js`
- **No DB change, no EF migration**

---

## Theme 4: Nutrition (USDA FoodData Central)

This theme is the most architecturally complex because it introduces a new external HTTP dependency, a caching/persistence layer, and a new UI panel.

### External API

- Base URL: `https://api.nal.usda.gov/fdc/v1/`
- Key endpoints: `GET /foods/search?query={ingredient}&dataType=Foundation,SR Legacy&pageSize=5` and `GET /food/{fdcId}`
- Auth: `api_key` query parameter (free registration at fdc.nal.usda.gov)
- Rate limit: 1,000 requests/hour per IP (sufficient for self-hosted single-household use)
- License: CC0 — no attribution requirement in the UI, though citing FDC in README is appropriate
- Key nutrient IDs: Energy/kcal = 1008, Protein = 1003, Total Fat = 1004, Carbohydrates = 1005

**Preferred data type:** Foundation Foods (generic whole foods, ~8,000 entries, complete nutrient profiles) first; fall back to SR Legacy. Branded Foods are a last resort (product-specific, variable).

### New Infrastructure component: `FdcClient.cs`

**Layer:** `CookBot.Infrastructure/Nutrition/`

Mirrors `AnthropicAiService` structurally: `HttpClient`-based, `IOptions<CookBotSettings>`-injected API key.

```
CookBotSettings additions:
  FdcApiKey: string?      — null means nutrition feature disabled (graceful degradation)
```

**FdcClient responsibilities:**
- `SearchFoodsAsync(string query, string[] dataTypes, int pageSize)` → `FdcSearchResult[]`
- `GetFoodAsync(int fdcId)` → `FdcFoodDetail` with `foodNutrients` array
- All HTTP errors surfaced as `FdcException` (never throws `HttpRequestException` directly — caller handles gracefully)

**Response DTOs** (Application layer, not Infrastructure): `FdcFoodItem`, `FdcNutrient`, `FdcSearchResult`. These are thin STJ-deserialized records, no business logic.

### New Application component: `NutritionService.cs`

**Layer:** `CookBot.Application/Nutrition/` or `CookBot.Application/Services/`

**Responsibilities:**
1. Accept a `RecipeDocument` and resolve nutrition per ingredient
2. For each `IngredientEntry`: call `FdcClient.SearchFoodsAsync(ingredient.Name)` → pick best match → call `FdcClient.GetFoodAsync(fdcId)` → extract nutrients 1003/1004/1005/1008
3. Convert FDC per-100g values to the recipe quantity using `UnitConversionService` (already exists in Application)
4. Aggregate across all ingredients → total recipe nutrients; divide by `doc.Servings` → per-serving panel
5. Return `RecipeNutritionResult { IReadOnlyList<IngredientNutritionLine> Lines, NutritionTotals Total, NutritionTotals PerServing, IReadOnlyList<string> UnmatchedIngredients }`

**Graceful degradation:** Ingredients with no FDC match go into `UnmatchedIngredients`. The panel shows "X ingredients could not be matched" with names listed. Never fail the whole request because one ingredient is ambiguous.

**Unit conversion challenge:** FDC data is per 100g. Recipes use volumetric units (cups, tbsp) for non-weight ingredients. Required: a density lookup or approximation table for common ingredients (e.g., 1 cup flour ≈ 120g). `UnitConversionService` already handles weight-to-weight conversions; it needs extension for volume-to-weight density approximations for the top ~20 common ingredients. This is a known approximation, and the UI should note "estimates based on typical densities."

### Caching strategy

**Problem:** FDC lookups are expensive (2 HTTP calls per ingredient, rate-limited). A 10-ingredient recipe = 20 calls. Re-running nutrition for the same recipe should reuse results.

**Decision: Two-level cache**

| Level | What | Where | TTL |
|---|---|---|---|
| Per-ingredient FDC match | `(normalizedName → fdcId, nutrientsPer100g)` | New `FdcLookupCache` SQLite table | 90 days — FDC data changes infrequently |
| Per-recipe nutrition | `(recipeId, documentHash → NutritionTotals)` | New `RecipeNutritionCache` SQLite table | Invalidated on `Recipe.UpdatedAt` change |

**`FdcLookupCache` table schema:**
```
FdcLookupCaches
  Id INTEGER PK
  NormalizedIngredientName TEXT NOT NULL UNIQUE INDEX
  FdcId INTEGER NOT NULL
  NutrientDataJson TEXT NOT NULL   -- serialized FdcNutrient[] for the 4 macros
  CachedAt DATETIME NOT NULL
```

**`RecipeNutritionCache` table schema:**
```
RecipeNutritionCaches
  Id INTEGER PK
  RecipeId INTEGER NOT NULL FK → Recipes
  DocumentHash TEXT NOT NULL     -- SHA-256 of CanonicalDocumentJson; invalidation key
  NutritionResultJson TEXT NOT NULL
  ComputedAt DATETIME NOT NULL
  UNIQUE (RecipeId)
```

This means: if `CanonicalDocumentJson` changes (recipe edited), `DocumentHash` changes, the cache row is stale → recompute. This is a hash comparison on read, not a trigger.

**Offline behavior:** If FDC is unreachable and the ingredient is in `FdcLookupCache`, serve from cache regardless of TTL (best-effort offline). If neither cache nor network, mark as unmatched.

**EF migrations:** Two new tables via EF Core migration. No changes to `Recipe` entity.

### New Domain types

**`CookBot.Domain/Models/NutritionalInfo.cs`** already exists as a stub (4 fields). Promote it or replace it with a richer `RecipeNutritionSummary` record that includes `PerServing` and `Total` breakdowns plus the `UnmatchedIngredients` list. Do not store `RecipeNutritionSummary` inside `RecipeDocument` (it is computed, not authored).

**Should nutrition live inside RecipeDocument?** No. Nutrition is computed from the canonical document; it is not part of the recipe spec itself. Storing it in `CanonicalDocumentJson` would mean the AI needs to emit it (it shouldn't) and would invalidate the cache every time a non-nutrition field changes. Keep it entirely in `RecipeNutritionCache`.

### UI: Nutrition panel on RecipeView

A collapsible panel below the ingredients list:
- "Nutrition (estimated per serving)" header with refresh button
- On first view: "Calculate nutrition" CTA → triggers `NutritionService` → loading state → results
- Shows: Calories, Protein, Carbs, Fat per serving + total recipe
- Shows unmatched ingredient names with a note: "Estimates based on typical densities; may not reflect your specific ingredients"
- FDC attribution line: "Powered by USDA FoodData Central"

**Feature gate:** `CookBotSettings.FdcApiKey is null` → panel shows "Nutrition requires a USDA FDC API key (free). Add it in appsettings.json." Same pattern as AI kill switch.

### Integration points

| Component | Status | Notes |
|---|---|---|
| `FdcClient.cs` | New — `CookBot.Infrastructure/Nutrition/` | HttpClient, FDC API key from CookBotSettings |
| `NutritionService.cs` | New — `CookBot.Application/Services/` | Orchestrates FdcClient + UnitConversionService + cache |
| `FdcLookupCache` entity | New — `CookBot.Domain/Entities/` | Cache ingredient→FDC match |
| `RecipeNutritionCache` entity | New — `CookBot.Domain/Entities/` | Per-recipe computed nutrition |
| `CookBotSettings` | Modified — add `FdcApiKey: string?` | |
| `RecipeView.razor` | Modified — add nutrition panel | |
| EF migration | New — adds 2 tables | |
| `UnitConversionService` | Modified — add density approximations for ~20 common ingredients | |

---

## Theme 5: Photo Gallery

### Current state

`LocalRecipePhotoStorage` saves `IBrowserFile` → `wwwroot/uploads/{guid}.ext` → returns `/uploads/path`. `Recipe.PhotoUrl` (single string column) + `RecipeDocument.PhotoUrl` (v3 canonical field). UI: single hero photo on RecipeView + RecipeEditor.

### Decision: Entity table vs. canonical-doc array

**Use a new `RecipePhoto` entity table, not a canonical-doc array.**

Rationale:
1. Photos are operational data (file paths, upload timestamps, ordering), not recipe format data. They do not belong in the AI-facing canonical document.
2. The existing `Recipe.PhotoUrl` precedent is already an EF column bridge (not canonical-doc-only), set in Phase 8 specifically because photo display is a UI concern separate from the format. Multiple photos follow the same pattern.
3. Storing photo paths in `CanonicalDocumentJson` would require the AI to emit them (it should not) and would make the canonical doc host-specific (absolute upload paths).
4. A `RecipePhoto` table allows EF `Include()` queries, ordered display, and future operations (delete, reorder) without JSON parsing.

**`RecipePhoto` entity (new — Domain layer):**
```
RecipePhotos
  Id INTEGER PK
  RecipeId INTEGER NOT NULL FK → Recipes  (cascade delete)
  Url TEXT NOT NULL                        -- /uploads/{guid}.ext or paste URL
  DisplayOrder INTEGER NOT NULL DEFAULT 0  -- lower = earlier in gallery
  IsHero BIT NOT NULL DEFAULT 0           -- exactly one per recipe should be hero
  AiGenerated BIT NOT NULL DEFAULT 0      -- reverse-image AI path flag
  UploadedAt DATETIME NOT NULL
```

**Migration strategy for existing `Recipe.PhotoUrl`:**
EF migration: create `RecipePhotos` table; backfill one row per recipe where `Recipe.PhotoUrl IS NOT NULL` (set as hero, `DisplayOrder = 0`); keep `Recipe.PhotoUrl` column as a read-only legacy column for backward compat but stop writing to it (soft-deprecate). Alternatively: keep writing to `Recipe.PhotoUrl` as the "hero photo URL" denormalized for the few non-gallery read paths (home dashboard thumbnail, recipe list card) to avoid N+1 queries.

**Recommended: Keep `Recipe.PhotoUrl` as a denormalized hero-URL column** (already in the DB + used by pantry-match home dashboard). On every photo mutation (add/reorder/delete), sync `Recipe.PhotoUrl` to the current hero photo's `Url`. This is a two-write operation in the service layer, not a trigger.

### LocalRecipePhotoStorage changes

`SaveAsync` returns a URL string — no change to the method signature. The caller (`RecipeEditor`) will now call a new `RecipePhotoService.AddPhotoAsync(recipeId, url, isHero)` instead of directly setting `Recipe.PhotoUrl`.

### New Application component: `RecipePhotoService.cs`

**Layer:** `CookBot.Application/Services/`

**Methods:**
- `AddPhotoAsync(int recipeId, string url, int userId)` — validates ownership, creates `RecipePhoto` row, if first photo sets `IsHero = true` and syncs `Recipe.PhotoUrl`
- `SetHeroAsync(int recipeId, int photoId, int userId)` — sets one photo as hero, clears others, syncs `Recipe.PhotoUrl`
- `ReorderAsync(int recipeId, int[] orderedPhotoIds, int userId)` — updates `DisplayOrder` columns
- `DeleteAsync(int recipeId, int photoId, int userId)` — deletes row + file on disk; if deleted was hero, promote next photo
- `GetPhotosAsync(int recipeId, int userId)` → `IReadOnlyList<RecipePhoto>` ordered by `DisplayOrder`

### Reverse-image AI path

**What:** User clicks "Find a photo for this recipe" → system sends recipe name + description to Claude with a request to describe what the dish looks like → ??? → show result.

**Clarification of "reverse-image AI":** Given CookBot is self-hosted with no image search API, the most useful interpretation is: use `AnthropicAiService` to generate a textual description of the dish, then surface image search links (Google Images, Unsplash), OR accept a URL the user pastes. A true "AI fetches an image" path requires an external image-generation API (DALL-E, Stability) which is out of scope per the "Anthropic only" constraint.

**Practical design:** The "reverse-image AI" feature = Claude describes the dish in visual terms (color, texture, plating) + returns suggested search terms for the user to find a photo. The user pastes the URL they find. This keeps the feature purely within the existing Anthropic integration, costs a single API call, and doesn't add a second AI provider. Implementation: a new `IAiService.SendMessageAsync` call with a prompt like "Describe what [recipe name] looks like for a food photography search" → surfaced as a modal on RecipeEditor with suggested terms + "Paste image URL" field.

### Gallery UI on RecipeView

- Hero photo at top (existing behavior, extended)
- Photo strip below hero: horizontally scrollable thumbnails, click to expand
- Edit mode in RecipeEditor: multi-photo upload panel with drag-reorder (using HTML5 drag-and-drop, no JS framework), set-hero button, delete button

### Integration points

| Component | Status | Notes |
|---|---|---|
| `RecipePhoto` entity | New — `CookBot.Domain/Entities/` | |
| `RecipePhotoService.cs` | New — `CookBot.Application/Services/` | CRUD + hero sync |
| `RecipePhotoConfiguration.cs` | New — `CookBot.Infrastructure/Data/Configurations/` | EF fluent config |
| `LocalRecipePhotoStorage` | Modified — no API change; possibly add `DeleteAsync(url)` | Needed by RecipePhotoService.DeleteAsync |
| `Recipe.PhotoUrl` | Modified — keep as denormalized hero; sync on every photo mutation | |
| `RecipeView.razor` | Modified — gallery strip, hero promote | |
| `RecipeEditor.razor` | Modified — multi-upload panel, reorder, AI describe CTA | |
| EF migration | New — adds `RecipePhotos` table + backfill from `Recipe.PhotoUrl` | |

---

## Dependency-Aware Build Order

The dependency graph across the five themes drives the phase sequence:

```
Phase 12: v3→v4 Schema Bump (Theme 1)
    ↓ (all export formats read from v4 RecipeDocument)
Phase 13: JSON-LD Export + Cooklang Export (Themes 2+3 — can bundle, both are read-only projections)
    ↓
Phase 14: Photo Gallery (Theme 5 — reads RecipeDocument.PhotoUrl from v4, adds RecipePhoto table)
    ↓
Phase 15: Nutrition — USDA FDC (Theme 4 — last; most complex, reads finalized RecipeDocument)
    ↓
Phase 16: UAT + Integration
```

**Why this order:**

1. **Schema bump first (Phase 12).** Export services (JSON-LD, Cooklang) must project the final v4 `RecipeDocument`. If exports are built against v3 and v4 adds `Equipment` / `Substitutions`, the exports would need a re-pass. Bump the schema once, then build consumers against the stable v4 shape.

2. **Exports second and bundled (Phase 13).** JSON-LD and Cooklang are both pure projection services with no DB changes. They are simple, low-risk, and can be developed in a single phase with two plans each (projector + RecipeView wiring). No cross-dependency between them.

3. **Photo gallery third (Phase 14).** Independent of nutrition. The `RecipePhoto` table is a new entity; its EF migration has no dependency on nutrition tables. The v4 `RecipeDocument.PhotoUrl` field is unchanged from v3 semantically (still the hero URL). Gallery builds on Phase 12's stable schema.

4. **Nutrition last (Phase 15).** Most complex theme: new external HTTP client, two new cache tables, unit conversion extensions, graceful degradation logic. Benefits from a stable RecipeDocument v4 to read from. Isolated from photo and export concerns.

5. **UAT in Phase 16.** Reuse the Playwright harness shipped in v1.3 Phase 11 (`tests/uat-harness/`).

---

## Component Boundary Summary

```
┌─────────────────────────────────────────────────────────────────────┐
│  CookBot.Web (new/modified for v1.4)                                 │
│  RecipeView.razor — JSON-LD HeadContent + gallery + nutrition panel  │
│  RecipeEditor.razor — multi-photo upload + AI describe CTA           │
└──────────────────────────┬──────────────────────────────────────────┘
                           │ DI injection
┌──────────────────────────▼──────────────────────────────────────────┐
│  CookBot.Application (new for v1.4)                                  │
│  Recipes/JsonLdRecipeProjector.cs        (new)                       │
│  Recipes/CooklangRecipeProjector.cs      (new)                       │
│  Recipes/Migration_V3_To_V4.cs           (new)                       │
│  Services/NutritionService.cs            (new)                       │
│  Services/RecipePhotoService.cs          (new)                       │
│  Recipes/RecipeUpcasterChain.cs          (modified — CurrentVersion=4)│
│  Recipes/RecipeValidator.cs              (modified — new v4 warnings) │
│  Services/PromptBuilderService.cs        (modified — v4 AI prompt)   │
│  Services/UnitConversionService.cs       (modified — density table)  │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────────────┐
│  CookBot.Infrastructure (new for v1.4)                               │
│  Nutrition/FdcClient.cs                  (new)                       │
│  Data/Configurations/RecipePhotoConfig   (new)                       │
│  Data/Configurations/FdcLookupCacheConfig (new)                      │
│  Data/Configurations/RecipeNutritionCacheConfig (new)                │
│  Migrations/AddRecipePhotosTable         (new)                       │
│  Migrations/AddNutritionCacheTables      (new)                       │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────────────┐
│  CookBot.Domain (new for v1.4)                                       │
│  Recipes/IngredientSubstitution.cs       (new)                       │
│  Recipes/EquipmentEntry.cs               (new)                       │
│  Recipes/RecipeProvenance.cs             (new)                       │
│  Recipes/IngredientEntry.cs              (modified — add Substitutions)│
│  Recipes/StepNode.cs (ContentStep)       (modified — add DonenessCue)│
│  Recipes/RecipeDocument.cs               (modified — add Equipment, Provenance)│
│  Entities/RecipePhoto.cs                 (new)                       │
│  Entities/FdcLookupCache.cs              (new)                       │
│  Entities/RecipeNutritionCache.cs        (new)                       │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Canonical-First / Display-Only Invariant Compliance

| Theme | Invariant honored | How |
|---|---|---|
| Richer format (v4) | YES | New fields live in `CanonicalDocumentJson`; no display layer reads raw JSON; all reads go through `JsonRecipeSerializer.Deserialize` |
| JSON-LD export | YES | Pure projection from `RecipeDocument`; `<script>` tag in `<head>` is read-only; no DB write |
| Cooklang export | YES | Pure projection; download only |
| Nutrition | YES | `RecipeNutritionCache` is a separate table; never merged back into `CanonicalDocumentJson`; nutrition panel is display-only |
| Photo gallery | YES | `RecipePhoto` table is operational data, not format data; `Recipe.PhotoUrl` hero sync is write-through on mutation, not a read-path concern |

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Storing nutrition inside RecipeDocument

**What:** Adding `nutrition: { ... }` to the v4 schema so the AI can emit macros.
**Why wrong:** The AI does not know FDC data; it would hallucinate nutritional values. Nutrition is computed, not authored. It would bloat `CanonicalDocumentJson`, pollute the AI prompt schema, and break the cache invalidation model.
**Instead:** Compute via `NutritionService` and cache in `RecipeNutritionCache`. Display via panel; never feed back to AI.

### Anti-Pattern 2: Putting photo paths in CanonicalDocumentJson

**What:** Storing `photos: ["/uploads/abc.jpg", "/uploads/def.jpg"]` in the canonical doc.
**Why wrong:** Photo paths are host-specific operational state, not recipe format data. They would travel in `.cookbook.json` exports and break on import (path doesn't exist on the recipient's host). The AI would receive the array and potentially hallucinate paths.
**Instead:** `RecipePhoto` entity table. On export, omit or strip photo URLs (or export with a flag noting "photos not included").

### Anti-Pattern 3: Bundle-throw in Migration_V3_To_V4

**What:** Handling all four new field groups (Equipment, Substitutions, DonenessCue, Provenance) in a single branching if/else block.
**Why wrong:** If any single field's guard throws, the entire upcaster fails. PITFALLS C7 from v1.3: "never bundle-throw."
**Instead:** Four independent no-op guards, each independently gated. Stamps `version: 4` only at the end.

### Anti-Pattern 4: Blocking FDC API calls on the UI render path

**What:** Calling `NutritionService.ComputeAsync` inside `OnInitializedAsync` on RecipeView.
**Why wrong:** FDC calls can take 2–10 seconds per ingredient. The page would hang.
**Instead:** Render the page immediately with a "Calculate nutrition" CTA. Trigger computation only on explicit user action. Show a spinner during computation. Cache results.

### Anti-Pattern 5: Single `HttpClient` per call in FdcClient

**What:** `new HttpClient()` per FDC request (same footgun as the pre-v1.3 `AnthropicAiService`).
**Why wrong:** Socket exhaustion under load; DNS not refreshed.
**Instead:** Register `FdcClient` with `IHttpClientFactory` via named client in `DependencyInjection.cs`. The `AnthropicAiService` has this exact technical debt (`Concern 16` in CONCERNS.md) — FdcClient should not repeat it.

---

## Integration Points Summary

### External Services

| Service | Integration Pattern | Notes |
|---|---|---|
| USDA FoodData Central | `FdcClient` (Infrastructure) — REST/JSON via named `HttpClient` | Free API key required; CC0 data; 1000 req/hr |
| Anthropic Claude (vision) | Reuse existing `AnthropicAiService.SendMessageAsync` | Pass image description prompt; no new client |
| Schema.org | Static string constants in `JsonLdRecipeProjector` | No HTTP call; pure projection |
| Cooklang | Pure text generation; no external dependency | |

### Internal Boundaries

| Boundary | Communication | Notes |
|---|---|---|
| NutritionService ↔ FdcClient | Direct DI injection (Application → Infrastructure via interface `IFdcClient`) | Define `IFdcClient` in Application layer, implement in Infrastructure — same Clean Arch pattern as `IAiService` |
| RecipePhotoService ↔ LocalRecipePhotoStorage | Direct injection (Application → Web is a layer inversion — move `LocalRecipePhotoStorage` to Application, or define `IRecipePhotoStorage` interface in Application with implementation in Web/Infrastructure) | Current `LocalRecipePhotoStorage` is in `CookBot.Web` and has Blazor's `IWebHostEnvironment` dependency. Either: keep it in Web and call from Razor pages (not service), or extract `IRecipePhotoStorage` interface into Application and implement in Infrastructure using `IWebHostEnvironment` from DI |
| JsonLdRecipeProjector ↔ RecipeView | Injected Application service; called on page load | No async needed — pure CPU computation |
| CooklangProjector ↔ RecipeView | Same; triggered on button click | |

---

## Sources

- Existing codebase read directly: `RecipeDocument.cs`, `StepNode.cs`, `IngredientEntry.cs`, `Migration_V2_To_V3.cs`, `RecipeUpcasterChain.cs`, `RecipeValidator.cs`, `RecipeJsonSchemaProvider.cs`, `LocalRecipePhotoStorage.cs`, `AnthropicAiService.cs`, `.planning/PROJECT.md`, `.planning/codebase/ARCHITECTURE.md`
- [USDA FoodData Central API Guide](https://fdc.nal.usda.gov/api-guide/) — endpoints, rate limits, data types, CC0 license
- [FDC OpenAPI Specification](https://fdc.nal.usda.gov/api-spec/fdc_api.html) — nutrient IDs and request/response shapes
- [Google Search: Recipe Structured Data](https://developers.google.com/search/docs/appearance/structured-data/recipe) — required/recommended JSON-LD properties, nutrition shape
- [Schema.org Recipe type](https://schema.org/Recipe) — canonical type definition
- [Cooklang Specification](https://cooklang.org/docs/spec/) — grammar for .cook files: `@ingredient`, `#cookware`, `~timer`, section syntax, YAML front matter
- [Anthropic Vision Documentation](https://docs.claude.com/en/docs/build-with-claude/vision) — image input methods (URL vs base64), supported formats

---

*Architecture research for: CookBot v1.4 Recipe Data & Interoperability*
*Researched: 2026-06-05*
