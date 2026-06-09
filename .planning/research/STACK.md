# Stack Research

**Domain:** Recipe data interoperability and enrichment (v1.4 additive milestone)
**Researched:** 2026-06-05
**Confidence:** HIGH for all five themes

---

## Existing Stack (do not re-research)

| Technology | Version | Role |
|------------|---------|------|
| .NET / C# | 10 | Runtime |
| Blazor Server | InteractiveServer | UI |
| EF Core + SQLite | 10.* | Persistence |
| System.Text.Json | BCL (net10) | JSON everywhere |
| JsonSchema.Net | 9.2.* | RecipeDocument schema validation |
| YamlDotNet | 16.3.0 | Recipe YAML wire format |
| Markdig | 0.45.0 | Markdown rendering |
| QuestPDF | 2025.1.0 | PDF cookbook export |
| AnthropicAiService | custom HttpClient | AI (Sonnet / Haiku / Opus) |

Hard constraints that override every recommendation below:
- System.Text.Json ONLY — no Newtonsoft.Json, no NJsonSchema
- No MudBlazor — custom Razor component system
- No Microsoft.Extensions.AI — existing HttpClient in AnthropicAiService is sufficient
- No official Anthropic SDK NuGet
- GPL-3.0-only — all dependencies must be license-compatible
- Self-hostable / trusted-LAN — no mandatory external services at runtime

---

## Theme 1: Richer Recipe Format (v3 → v4 schema bump)

### Recommendation: Zero new libraries. Pure POCO + upcaster.

The v3 `RecipeDocument` is a sealed record with STJ attributes in `CookBot.Domain/Recipes/`. Adding substitutions, equipment list, per-step doneness cues, and source/provenance follows the identical pattern as the v2→v3 bump:

1. Add nullable C# properties to `RecipeDocument` (and `IngredientEntry`, `StepNode` as needed).
2. Add a `Migration_V3_To_V4 : IRecipeUpcaster` in `CookBot.Application/Recipes/`.
3. Bump `Version` to 4.
4. Update `RecipeJsonSchemaProvider` to reflect the new schema for the AI prompt.
5. Update parser (`RecipeFormatParser.cs`) and validator (`RecipeValidator.cs`).

**New fields recommended:**

| Field | Location | C# type | Notes |
|-------|----------|---------|-------|
| `substitutions` | `IngredientEntry` | `IReadOnlyList<string>?` | Free-text substitution hints, e.g. `["oat milk", "almond milk"]` |
| `equipment` | `RecipeDocument` | `IReadOnlyList<string>?` | Top-level list, e.g. `["stand mixer", "12-inch skillet"]` |
| `donenessCue` | `ContentStep` (inside `StepNode`) | `string?` | Per-step visual/sensory cue, e.g. `"golden brown and a toothpick comes out clean"` |
| `source` | `RecipeDocument` | `RecipeSource?` (new nested record) | Provenance: `{ url, author, publishedDate, notes }` |

`RecipeSource` is a simple STJ-attributed C# record in `CookBot.Domain/Recipes/`; no extra lib needed.

The `[JsonExtensionData] Dictionary<string,JsonElement> Extras` on `RecipeDocument` already forward-compats unknown keys, so v4 docs round-trip safely in v3 parsers until upcasted.

**Confidence:** HIGH — this is exactly how v2→v3 was done; the pattern is proven in the codebase.

---

## Theme 2: Export — Schema.org Recipe (JSON-LD)

### Recommendation: Hand-roll with System.Text.Json. No library needed.

**Why hand-roll beats a library:**
- The Schema.org `Recipe` type has a fixed, small field set for Google Rich Results. There is no .NET Schema.org serialization library that is actively maintained, GPL-compatible, and STJ-native. The closest is a Java-only Google library (`google/schemaorg-java`). Rolling a thin serializer takes ~80 lines of C# and is trivially testable.
- A dedicated `SchemaOrgRecipeSerializer` class in `CookBot.Application/Export/` takes a `RecipeDocument` + `UserProfile` and emits a `string` (the JSON-LD `<script>` block). Inject into the Recipe View Razor page.

**Google Rich Results field requirements (verified against Google Search Central docs, last updated December 2025):**

| Field | Status | Maps from `RecipeDocument` |
|-------|--------|---------------------------|
| `@context` = `"https://schema.org/"` | Required boilerplate | hardcoded |
| `@type` = `"Recipe"` | Required boilerplate | hardcoded |
| `name` | **Required** | `RecipeDocument.Name` |
| `image` | **Required** | `RecipeDocument.PhotoUrl` (omit if null) |
| `description` | Recommended | `RecipeDocument.Description` |
| `recipeIngredient` | Recommended | `IngredientEntry` → `"{amount} {unit} {name}"` strings |
| `recipeInstructions` | Recommended | `ContentStep` nodes as `HowToStep` array |
| `cookTime` | Recommended | ISO 8601 duration, e.g. `"PT30M"` from `CookTimeMinutes` |
| `prepTime` | Recommended | ISO 8601 duration from `PrepTimeMinutes` |
| `totalTime` | Recommended | sum of prep + cook |
| `recipeYield` | Recommended | `RecipeDocument.Servings` as string `"4 servings"` |
| `author` | Recommended | user display name or omit |
| `keywords` | Recommended | `RecipeDocument.Tags` joined |
| `nutrition.calories` | Recommended | populated in v1.4 Nutrition theme |

**ISO 8601 duration formatting:** `TimeSpan`/`XmlConvert.ToString(TimeSpan, ...)` is in BCL — no library. Pattern: `$"PT{minutes}M"`.

**Output surface:** The serializer produces a `string`; the Blazor page injects it via `@((MarkupString)...)` inside a `<script type="application/ld+json">` tag. This is display-only — no new endpoints.

**Confidence:** HIGH — Google's documentation is current and definitive; STJ write-path is trivial.

---

## Theme 3: Export — Cooklang

### Recommendation: Hand-roll a `CooklangExporter` in `CookBot.Application/Export/`. Do NOT take a library dependency.

**Why not CookLangNet:**
- Latest NuGet release: `0.4.0`, published 2023-05-21. Total downloads: 8,800. Current-version downloads: 473.
- Targets .NET 7.0 / .NET Standard 2.0 — no .NET 10-specific issues, but the project is effectively unmaintained.
- **Critical problem:** CookLangNet is a **parser only** — it cannot write/serialize `.cook` files. Since CookBot needs one-way export (write), the library provides nothing useful.
- The F#-native API requires the `CookLangNet.CSharp` wrapper, adding a second package and a runtime F# stdlib dependency.

**Cooklang grammar for the export path (verified against cooklang.org/docs/spec/):**

The output-side grammar is a small subset of the full spec. CookBot only needs to emit, not parse:

```
Ingredient:  @name{quantity%unit}        e.g.  @butter{100%g}
             @single-word                  e.g.  @salt
             @multi word{}                 e.g.  @ground black pepper{}
Cookware:    #name{}                       e.g.  #stand mixer{}
             #single-word                  e.g.  #pot
Timer:       ~{duration%unit}             e.g.  ~{25%minutes}
Section:     = Section Name
Step:        plain paragraph text with inline @, #, ~ markup
Metadata:    YAML frontmatter between --- delimiters
Comment:     -- inline comment
```

**Mapping from `RecipeDocument` to `.cook`:**

| `RecipeDocument` field | Cooklang output |
|------------------------|----------------|
| `Name`, `Description`, `Tags`, `Servings`, `PrepTimeMinutes`, `CookTimeMinutes`, source fields | YAML frontmatter block between `---` |
| `Equipment[]` (v4) | `#equipment-name{}` lines at top of first step or as a preamble section |
| `Ingredients[]` | Inline `@name{amount%unit}` within step text; ingredients not referenced in any step get a standalone line |
| `Steps[SectionStep]` | `= Section Name` heading line |
| `Steps[ContentStep].Text` | Paragraph text; ingredient `[name](#id)` chips replaced with `@name{amount%unit}` using `IngredientEntry` lookup |
| `Steps[ContentStep].Temperature` | Appended as plain text in step prose, e.g. `at 180°C/350°F` — no native Cooklang temperature syntax exists |
| `Steps[ContentStep].DonenessCue` (v4) | Appended as plain text — no native Cooklang doneness syntax |
| `Steps[ContentStep].Timers` | `~{duration%minutes}` inline |

The exporter is a single static class; ~150 lines. The `[name](#id)` ingredient-link syntax in step text is already resolved by `RecipeStepTextFormatter`; the export path needs its own pass that looks up the `IngredientEntry` by `id` and emits the `@name{amount%unit}` form. `YamlDotNet` (already a dependency) serializes the frontmatter block.

**Confidence:** HIGH for the grammar rules; MEDIUM for the chip-to-inline-ingredient substitution edge cases (e.g. step references an ingredient not in the ingredients list — needs a graceful fallback).

---

## Theme 4: Nutrition — USDA FoodData Central

### Recommendation: Plain HttpClient (matches the Anthropic pattern). No .NET FDC library exists. Optional offline seeding from SR Legacy CSV.

**No .NET FDC client library exists on NuGet.** The only client libraries are Node.js/Python/Clojure. Use the same `HttpClient`-based pattern as `AnthropicAiService`.

### API access details (verified against fdc.nal.usda.gov/api-guide/)

| Parameter | Value |
|-----------|-------|
| Base URL | `https://api.nal.usda.gov/fdc/v1` |
| Auth | Free API key via data.gov signup; passed as `?api_key=KEY` query parameter |
| Rate limit (signed key) | 1,000 requests/hour/IP |
| Rate limit (DEMO_KEY) | 30 req/hour, 50 req/day — not suitable for production |
| Over-limit response | HTTP 429; key blocked 1 hour |
| Headers to check | `X-RateLimit-Limit`, `X-RateLimit-Remaining` |
| Key signup URL | https://fdc.nal.usda.gov/api-key-signup/ |
| License | CC0 1.0 Universal (public domain) — no legal attribution requirement |
| Attribution request | Suggested (not required): "U.S. Department of Agriculture, Agricultural Research Service. FoodData Central, 2025." |

### Dataset selection

| Dataset | Foods | Status | Recommendation |
|---------|-------|--------|---------------|
| **Foundation Foods** | ~800 items, April 2026 release | Actively updated | **Primary** — prefer for basic/unprocessed ingredients |
| **SR Legacy** | 7,793 items, April 2018 | Final release, no updates | **Secondary fallback** — much broader coverage for common recipe ingredients |
| **Branded Foods** | ~1.1M items | Actively updated | **Exclude** — brand-specific products are not useful for generic recipe ingredients |
| **Survey / FNDDS** | Composite dishes | Updated 2024 | Exclude — aggregated meal data, not raw ingredients |

For recipe nutrition, query both `Foundation` and `SR Legacy` simultaneously using `dataType=Foundation,SR+Legacy` to maximize coverage while avoiding branded noise. Foundation Foods gives better analytical data for staples (butter, onion varieties, etc.); SR Legacy fills gaps for the thousands of ingredients Foundation doesn't cover.

### Key API endpoints

```
GET /fdc/v1/foods/search?query={name}&dataType=Foundation,SR+Legacy&pageSize=5&api_key=KEY
GET /fdc/v1/food/{fdcId}?api_key=KEY
```

The search endpoint returns a `foods` array with embedded `foodNutrients`. Calorie/macro nutrient IDs are stable:

| Macro | Nutrient ID | Unit |
|-------|------------|------|
| Energy | 1008 | kcal |
| Protein | 1003 | g |
| Total Fat | 1004 | g |
| Total Carbohydrate | 1005 | g |

Nutrient values are **per 100 g** of food. To compute per-serving values: `value = nutrientPer100g * (ingredientGrams / 100)`. This requires unit conversion from `MeasurementUnit` to grams — use the existing `UnitConversionService` where possible; implement a gram-conversion table for volume measurements (e.g. ml of water ≈ g, ml of oil ≠ g without density lookup).

### The ingredient-name matching problem

This is the hardest part of the theme. "2 tbsp butter" → FDC "Butter, salted" (fdcId 173430, SR Legacy) is a fuzzy string match, not deterministic lookup. Approaches in increasing complexity:

1. **Simple: FDC text search with the canonical `IngredientEntry.Name`** — send the name as-is. Works well for "butter", "all-purpose flour", "eggs". Fails for recipe-specific names like "room-temperature butter" or "good olive oil".
2. **Better: Pre-normalize the name** — strip adjectives (room-temperature, good, fresh, organic) before the query. A small deny-list of modifiers covers most cases.
3. **Cache / persist matches** — store `(normalizedName → fdcId)` in a new `NutritionCache` table to avoid re-querying the same ingredient repeatedly. This is essential given the 1,000 req/hour limit.
4. **Manual override** — expose a UI affordance to map an unmatched ingredient to a specific FDC food. Defer to v1.5.

**Recommendation for v1.4:** Implement approach 2 (normalize then search) + approach 3 (local cache table). Design the cache so approach 4 can be added later without schema changes.

### Offline dataset option

The SR Legacy CSV download is 3.1 MB zipped (unzipped: smaller than the Branded download which is 2.9 GB). Bundling SR Legacy as a SQLite seed table is feasible and eliminates API calls for the ~7,793 SR Legacy foods. This is a legitimate self-host concern: the app must work without internet access on a trusted LAN.

**Recommendation:** Seed SR Legacy CSV into the app's own SQLite database at startup (similar to the existing `seeds/ingredients.json` seeder). Foundation Foods CSV (32 MB unzipped, 3.7 MB zipped) is also small enough to bundle. This gives full offline nutrition for the ~8,600 combined Foundation + SR Legacy foods — which covers essentially all recipe staples. Branded foods (2.9 GB) must NOT be bundled; use the live API for user-triggered branded-food lookup only.

The seeded data is static (SR Legacy is final; Foundation Foods refreshes bi-annually). Ship the seed data at the time of the v1.4 release; a future "refresh" command can replace it.

### New EF entities for nutrition

| Entity | Purpose |
|--------|---------|
| `FdcFood` | Seeded lookup table: `fdcId`, `description`, `dataType`, `calories`, `proteinG`, `fatG`, `carbsG` |
| `IngredientNutritionCache` | Maps `normalizedIngredientName → fdcId + portionGrams + matchedAt` (deduplicate API calls) |
| `RecipeNutritionSnapshot` | Cached per-recipe nutrition totals (recomputed on save if ingredients change) |

All three are EF entities in `CookBot.Domain/Entities/`; no new packages.

### FdcApiService

Model after `AnthropicAiService`: a named `HttpClient` registered via `IHttpClientFactory`, baseAddress `https://api.nal.usda.gov/fdc/v1`, API key stored in `CookBotSettings.FdcApiKey` (new optional config field). The service is **no-op/graceful-fallback when the key is absent** — nutrition panel shows "not available" rather than crashing.

**License note:** CC0 1.0 is fully GPL-3.0-compatible. Attribution is morally appropriate (add a "Nutrition data from USDA FoodData Central" footnote in the nutrition panel UI) but not legally required.

**Confidence:** HIGH for API shape and dataset choice; MEDIUM for ingredient matching accuracy (inherently fuzzy problem; graceful handling of unknowns is required).

---

## Theme 5: Photos — Multiple / Gallery + Optional Reverse-Image AI

### 5a: Gallery storage — extend existing pattern, no new library

The existing `LocalRecipePhotoStorage` saves files to `wwwroot/uploads/` as `{guid}{ext}` with magic-byte validation. The current `RecipeDocument.PhotoUrl` (v3) is a single `string?`. For v4 a gallery requires a list.

**Schema change:** Replace `PhotoUrl: string?` in `RecipeDocument` with `Photos: IReadOnlyList<string>` (default empty). The v3→v4 upcaster preserves the single `PhotoUrl` by moving it into `Photos[0]` if non-null. The EF `Recipe.PhotoUrl` column stays as the "hero" denormalized field for list views (set to `Photos[0]` on save).

**No new EF table needed.** Photos are serialized inside `Recipe.CanonicalDocumentJson` as part of the `RecipeDocument`. Local file references are already paths like `/uploads/{guid}.jpg`. The gallery is just `IReadOnlyList<string>` — could be local upload paths, external URLs, or a mix.

**LocalRecipePhotoStorage** already handles single-file upload with all safety invariants (magic-byte sniff, path-traversal guard, GUID filename). Extend it with a multi-file variant (`SaveAllAsync(IReadOnlyList<IBrowserFile>)`) that loops and calls the existing `SaveAsync` per file.

**Deletion:** Track which `/uploads/` files are referenced by any recipe; on recipe delete or photo remove, clean up orphaned files. This requires a sweep (can be sync at delete time or a background sweep task). The sweep can query `CookBotDbContext` for all `CanonicalDocumentJson` values and scan for the `/uploads/{guid}` paths — no schema change needed.

**Confidence:** HIGH — additive to the existing v1.3 pattern.

### 5b: Optional reverse-image AI — use the existing Anthropic path, no new vision API

**Context:** The existing `AnthropicAiService` already calls `https://api.anthropic.com/v1/messages` over a plain `HttpClient`. All current Claude models (Haiku 4.5, Sonnet 4.6, Opus 4.7) support vision.

**How it works in the Anthropic API (verified against platform.claude.com/docs/en/build-with-claude/vision):**

The messages API accepts image content blocks in two forms:
- **Base64:** `{ "type": "image", "source": { "type": "base64", "media_type": "image/jpeg", "data": "<base64>" } }`
- **URL:** `{ "type": "image", "source": { "type": "url", "url": "https://..." } }` (added January 2026)

Supported formats: JPEG, PNG, GIF, WebP. Max size 10 MB. Image cost: ~1,568 tokens for a 1920×1080 image at Sonnet pricing (~$0.0047 per image).

**The "find a photo" feature is image description, not image generation.** Claude cannot generate images. The workflow is:

```
User clicks "Find a photo" →
App sends recipe name + ingredient list + steps to Claude as text →
Claude describes what the finished dish looks like →
Optionally: user pastes a URL they found from that description →
OR: App shows the description as a search hint ("Search Google Images for: …")
```

Claude does NOT retrieve or generate images. The "reverse-image AI" framing in the milestone context should be interpreted as "AI-assisted photo discovery" — Claude describes the dish, the user finds and uploads a photo themselves.

**If the feature is "given an existing uploaded photo, ask Claude to confirm it looks like the recipe"** — that IS doable with the vision path: send the uploaded photo (base64 from disk) + recipe name to Claude, ask it to rate relevance. This uses the existing `HttpClient`-based `SendMessageAsync`, with the content array modified to include an image block before the text prompt. No new service, no new library — just an overload or helper method on `AnthropicAiService` that accepts a `byte[]` image + message string.

**Recommendation:** Extend `AnthropicAiService` with a `SendWithImageAsync(systemPrompt, imageBytes, mediaType, textPrompt, apiKey)` method. Keep it in `CookBot.Infrastructure/AI/`. No new interface or abstraction — vision is a capability of the existing provider.

**Cost awareness:** A "describe this recipe for photo search" prompt with Haiku is ~$0.0002 per call (text only, no image). Including a user-uploaded photo for "does this match?" confirmation is ~$0.0049 per call (Sonnet, 1MP image). Both are negligible at self-host scale.

**Confidence:** HIGH for the API mechanics; MEDIUM for user-experience value of the feature (depends on implementation scope chosen).

---

## New Packages Required

**Zero new NuGet packages for v1.4.** All five themes are achievable with existing dependencies plus hand-rolled code:

| Theme | New package? | Why not |
|-------|-------------|---------|
| Richer format (v4) | None | Pure POCO + upcaster |
| Schema.org JSON-LD | None | System.Text.Json write path; ~80 lines |
| Cooklang export | None | CookLangNet is parser-only, unmaintained; grammar is simple enough to hand-roll |
| USDA FDC nutrition | None | No .NET FDC library exists; plain HttpClient matches existing pattern |
| Photo gallery + vision | None | Extend existing `LocalRecipePhotoStorage` + `AnthropicAiService` |

---

## What NOT to Add

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `CookLangNet` / `CookLangNet.CSharp` | Parser-only (can't write .cook); v0.4.0 last updated May 2023; ~9k downloads total; unmaintained | Hand-roll `CooklangExporter` (~150 lines) against the published EBNF spec |
| Any Schema.org .NET library | No GPL-compatible, STJ-native, actively-maintained option exists | Hand-roll `SchemaOrgRecipeSerializer` (~80 lines) |
| `Microsoft.Extensions.AI` | Project hard constraint; breaks the existing AnthropicAiService architecture | Existing `AnthropicAiService` HttpClient |
| Official Anthropic SDK NuGet | Project hard constraint | Existing `AnthropicAiService` HttpClient |
| `Newtonsoft.Json` / `NJsonSchema` | Project hard constraint; 100% STJ codebase | System.Text.Json BCL |
| `MudBlazor` | Removed wholesale in v1.2; project hard constraint | Custom Razor component system |
| USDA Branded Foods (2.9 GB) bundled in-app | 2.9 GB compressed dataset is impractical to ship; branded items not useful for recipe nutrition | Bundle Foundation + SR Legacy only (~35 MB unzipped combined); live API for branded lookups only |
| A separate `CookBot.Schemas` project | Project hard constraint; `RecipeDocument` belongs in Domain | `CookBot.Domain/Recipes/` |
| A separate `CookBot.Nutrition` project | Over-engineering for self-host scale | New services in `CookBot.Application/` and entities in Domain |
| A third-party image generation API (DALL-E, Stability AI) | Out of scope per trusted-LAN posture; adds a second paid API dependency | AI-assisted description + user-sourced upload |

---

## Installation

No new packages to install. The existing `dotnet restore` and `dotnet build` pipeline is unchanged.

If the optional SR Legacy / Foundation Foods offline seed approach is adopted, the CSVs are committed to `seeds/nutrition/` and processed at startup by extending `DatabaseSeeder.SeedAsync`. No NuGet change needed; CSV parsing uses `string.Split` or a minimal span-based reader — no CsvHelper dependency.

---

## Alternatives Considered

| Recommended | Alternative | Why Alternative Loses |
|-------------|-------------|----------------------|
| Hand-roll Cooklang exporter | CookLangNet 0.4.0 | Parser-only; unmaintained since 2023; F# runtime dependency; net10 compatibility unverified |
| Hand-roll Schema.org JSON-LD | `schema-net` (NuGet) or any third-party | No actively maintained GPL-compatible STJ-native .NET Schema.org library found |
| Offline seed Foundation + SR Legacy | Live-only FDC API | App must work offline on trusted LAN; 1,000 req/hour limit is tight for batch operations; seed eliminates API dependency for staples |
| Extend `AnthropicAiService` for vision | New `IVisionAiService` abstraction | Only one AI provider exists; adding an abstraction here adds complexity without value |
| `IReadOnlyList<string> Photos` in `RecipeDocument` | New `RecipePhoto` EF entity | Photos are logically part of the canonical recipe document; EF entity adds a migration and join query for every recipe load; the JSON column already exists |

---

## Version Compatibility

| Package | Current in codebase | v1.4 change |
|---------|--------------------|-|
| JsonSchema.Net | 9.2.* | No change — schema for v4 `RecipeDocument` updates `RecipeJsonSchemaProvider` output, not the package version |
| YamlDotNet | 16.3.0 | No change — reused by Cooklang exporter for YAML frontmatter serialization |
| QuestPDF | 2025.1.0 | No change — PDF export picks up new schema v4 fields automatically if the PDF template reads `RecipeDocument` |
| EF Core | 10.* | New migrations for `FdcFood`, `IngredientNutritionCache`, `RecipeNutritionSnapshot` tables |

---

## Sources

- Google Search Central — Recipe structured data: https://developers.google.com/search/docs/appearance/structured-data/recipe (verified December 2025 update)
- Cooklang specification: https://cooklang.org/docs/spec/ (ingredients, cookware, timers, sections, metadata syntax verified)
- USDA FoodData Central API Guide: https://fdc.nal.usda.gov/api-guide/ (auth, rate limits, dataset types — HIGH confidence)
- USDA FDC Download Datasets: https://fdc.nal.usda.gov/download-datasets/ (file sizes and formats — HIGH confidence)
- USDA FDC OpenAPI (live call to search endpoint with DEMO_KEY): nutrient IDs 1003/1004/1005/1008 confirmed in response body (HIGH confidence)
- Anthropic Vision docs: https://platform.claude.com/docs/en/build-with-claude/vision (image formats, base64/URL sources, token costs — HIGH confidence, verified current)
- CookLangNet on NuGet: https://www.nuget.org/packages/CookLangNet — v0.4.0, 2023-05-21, 8,800 total downloads (MEDIUM confidence on "unmaintained" — no recent GitHub activity observed)
- SR Legacy item count: 7,793 foods (verified via USDA National Nutrient Database documentation)
- Foundation Foods vs SR Legacy: https://fdc.nal.usda.gov/Foundation_Foods_Documentation/ (Foundation is higher-accuracy for staples; SR Legacy provides broader coverage — HIGH confidence)

---

*Stack research for: FreelovesCookBot v1.4 Recipe Data & Interoperability*
*Researched: 2026-06-05*
