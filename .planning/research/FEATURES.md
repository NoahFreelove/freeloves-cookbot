# Feature Research

**Domain:** Recipe data enrichment, interoperability, and photo gallery — v1.4 additive milestone on top of a self-hosted Blazor Server cooking tracker
**Researched:** 2026-06-05
**Confidence:** HIGH (Schema.org + Cooklang via official specs; USDA via official API guide + download page; photo/substitution via leading apps + community research)

---

## Theme 1: Richer Recipe Format

Adds ingredient substitutions, equipment/tools list, per-step doneness cues, and source/provenance. Rides a v3→v4 `RecipeDocument` bump with upcaster. The existing canonical format already carries per-step `Temperature` (v3); these fields extend it further.

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Source URL / attribution line | Users paste recipes from websites; they want to track the original. Recipe apps (Paprika, Mealie, Tandoor) all show a source URL field. Provenance is also used by schema.org `author` + `url` properties. | LOW | Plain `string?` on `RecipeDocument` root. Maps directly to schema.org `author` (Person name string) and a custom `sourceUrl` field. No structured person object needed for v1.4. |
| "Adapted from" note | Standard culinary attribution — users adapt NYT Cooking recipes and want to note that. Expected by food bloggers and home cooks alike. | LOW | Plain `string?` on root alongside `SourceUrl`. Maps to schema.org `description` prefix convention ("Adapted from…") or a dedicated `isBasedOn` field (schema.org CreativeWork inheritance). |
| Equipment / tools list | Bakers in particular need to know up front whether they need a stand mixer, Dutch oven, or candy thermometer before they start. Leading apps (Paprika per-step cookware; Cooklang `#cookware{}`; AllRecipes equipment callouts) all surface this. | LOW–MEDIUM | Recipe-level `string[]` list. Cooklang surfaces cookware inline per-step; at recipe level a deduplicated aggregate is the right default view. Keep it as a freeform string list — do not introduce a cookware entity. |
| Per-step doneness cues | "Cook until golden brown" or "until internal temp reaches 165°F" are the cook's real trigger, not a timer. ATK, serious cooks apps, and cooking mode UI all benefit. Already partially covered by `Temperature`; a companion text cue is the table-stakes complement. | LOW | `string? DonenessCue` on `RecipeStep` (same level as existing `Temperature`). Freeform text. Not a structured enum — doneness is too varied (visual, tactile, temperature, sound). |
| Ingredient substitutions | "Use oat milk instead of whole milk" is ubiquitous in dietary-adaptation recipes. Users with dietary restrictions or missing pantry items expect this. Mealie and Tandoor users have requested it as a first-class field (GitHub Discussion #694). | MEDIUM | Per-`RecipeIngredient` array of `IngredientSubstitution` records: `{ Note: string, Amount?: decimal, Unit?: string, IngredientName?: string }`. The substitution is an ingredient-level concept, not a recipe-level or step-level concept. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| AI-generated substitutions | Let Claude pre-populate substitutions for the common cases (dairy-free, gluten-free swaps) during recipe generation or via an "Suggest substitutions" action. CookBot already has the AI orchestrator; this is a prompt-extension. | MEDIUM | Extend `RecipeDocument` JSON schema + AI prompt; structured-output already handles arrays. One new `substitutions` array on `RecipeIngredient` is sufficient. |
| Doneness cue shown prominently in Cooking Mode | In Cooking Mode, surface the doneness cue as a highlighted callout below the timer — not buried in step text. Paprika shows step notes but not structured doneness. | LOW | UI-only change to `CookingMode.razor`; the data field (`DonenessCue`) is the blocker. |
| Equipment checklist before cook starts | A "You'll need" pre-cook modal that lists deduplicated equipment from the recipe before the user starts cooking mode. | LOW–MEDIUM | Depends on equipment list existing on the recipe. The aggregate is assembled from the `Equipment` array; Cooking Mode renders it as a pre-flight card. |
| Schema-linked source URL (clickable provenance) | In Recipe View, the source URL renders as a live link with the domain name as anchor text ("From allrecipes.com"). Minor but appreciated. | LOW | Pure UI rendering of the `SourceUrl` field. |

### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Structured ingredient substitution entity (like pantry ingredients with autocomplete) | Feels more "correct" from a data-model standpoint | Massive scope increase — requires linking substitutions to the existing ingredient seed database, resolving names, handling free-form vs canonical names. Not needed for v1.4. | Store substitutions as freeform `{ Note, IngredientName?, Amount?, Unit? }` structs. The note alone ("use oat milk") is sufficient for 95% of cases. |
| Cookware-level detail (brand, size, material) | Power users want to note "8-inch cast-iron skillet" | Deep rabbit hole with no user demand evidence. Cooklang itself treats cookware as plain strings. | Keep equipment as `string[]` on `RecipeDocument`. Users write "8-inch cast-iron skillet" in the string. |
| Version history / "adapted from v2 of this recipe" lineage tracking | Interesting for recipe evolution stories | Complex versioning UI, no evidence of user demand. | `AdaptedFrom` (string) covers the common case. Internal `RecipeDocument.Version` already handles schema migration. |
| Automatic doneness cue extraction from step text via NLP | Avoid manual entry | NLP on "until golden" in free-form prose is brittle; the AI can generate the cue field directly. | AI prompt extension to emit `DonenessCue` during generation; manual entry for existing recipes. |

---

## Theme 2: Schema.org Recipe Export (JSON-LD)

One-way export. The goal is SEO / rich-results eligibility for users who self-host with a public-facing URL, and machine-readable structured data for feed aggregators.

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| `name` + `image` in JSON-LD | Google strictly requires these two to be eligible for rich results at all. Without them the markup is valid but invisible to Google. | LOW | Maps directly: `RecipeDocument.Title` → `name`; `Recipe.PhotoUrl` (v3 field) → `image`. Require 16x9, 4x3, and 1x1 aspect ratios per Google guidance — for v1.4 with single hero photo, emit the single URL for all three. |
| `recipeIngredient` as string array | Google recommended; expected by parsers and screen readers. Standard flat list of ingredient strings. | LOW | Map `RecipeIngredient` records: `"{Amount} {Unit} {Name}{, PrepNote}"` string per entry. Substitutions are NOT included here (no schema.org field for them). |
| `recipeInstructions` as `HowToStep` array | Google recommended; required for the richer "recipe card" display with inline step text. `HowToStep` carries `text` + optional `name`. | LOW–MEDIUM | Map `RecipeStep` → `HowToStep { "@type": "HowToStep", "text": step.Text, "name": optional-first-sentence }`. Sections map to `HowToSection`. |
| `prepTime` + `cookTime` + `totalTime` (ISO 8601 Duration) | Google recommended; enables time display in SERPs. | LOW | Map `RecipeDocument.PrepTimeMinutes` + `CookTimeMinutes` → `PT{N}M`. Total = sum. |
| `recipeYield` | Google recommended; shown in rich snippets. | LOW | Maps to `RecipeDocument.Servings` as a string: `"4 servings"`. |
| `description` | Google recommended; shown in the snippet body. | LOW | Maps to `Recipe.Description` (v3 field). |
| `author` | Google recommended; persona trust signal. | LOW | Maps to `RecipeDocument.AuthorName` (new provenance field from Theme 1). Emit as `{ "@type": "Person", "name": "..." }`. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| `nutrition.calories` in JSON-LD | Only emittable once Theme 4 (Nutrition) computes it. If nutrition is computed, including it in JSON-LD is a one-liner and boosts SERP engagement. | LOW (dependent on Theme 4) | Gated on Theme 4 data existing. Emit `{ "@type": "NutritionInformation", "calories": "350 calories" }` per serving. `recipeYield` must also be present when nutrition is specified — Google requires it. |
| `keywords` field | Helps recipe categorization in search. Maps from `RecipeTags` (relational tags shipped in v1.3). | LOW | Join tag names into comma-separated string. |
| `recipeCategory` + `recipeCuisine` | Additional SERP signals. CookBot has no structured category/cuisine fields today. | MEDIUM | Would require adding `Category` + `Cuisine` string fields to `RecipeDocument` v4 — low-effort schema addition but needs UI. Could defer or emit from tags. |
| `datePublished` | Freshness signal for SERPs. | LOW | Maps to `Recipe.CreatedAt` (existing EF entity field). |
| Inline `<script type="application/ld+json">` in Recipe View page head | Makes markup available to crawlers without extra endpoints. | LOW | Blazor Server `HeadContent` component. Rendered server-side so crawlers see it without JS execution. |

### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| `aggregateRating` in JSON-LD | Boosts CTR significantly in SERPs | CookBot has no rating system; fabricating ratings violates Google's structured data policies and results in manual actions against the site. | Omit entirely. If a ratings feature is added in v2+, add it then. |
| `video` in JSON-LD | Video carousels get premium SERP placement | CookBot has no video feature and no video hosting. | Omit. Not in scope. |
| Dedicated `/recipe/{id}.jsonld` endpoint | Clean REST endpoint for machines | Unnecessary for self-hosted LAN use; the inline `<script>` in the page head is sufficient and requires no routing changes. | Inline `ld+json` in page head via `HeadContent`. |
| Round-trip JSON-LD import | Symmetric with export | `recipeIngredient` strings in JSON-LD are free-text, not structured; reconstructing the canonical `RecipeIngredient` objects from them requires NLP parsing. Lossy. | One-way export only. CookBot already has `.cookbook.json` as the lossless round-trip format. |

**What maps cleanly from v3 canonical doc:**
`name` (Title), `image` (PhotoUrl), `description` (Description), `recipeIngredient` (ingredients as strings), `recipeInstructions` (steps as HowToStep), `prepTime`/`cookTime`/`totalTime` (PrepTimeMinutes/CookTimeMinutes), `recipeYield` (Servings), `author` (new AuthorName from Theme 1 provenance), `datePublished` (Recipe.CreatedAt), `keywords` (RecipeTags).

**What is missing from v3 and needs v4 fields:**
`recipeCategory`, `recipeCuisine` (no equivalent today — new optional fields or derived from tags), `nutrition.calories` (blocked on Theme 4).

**What schema.org defines that CookBot intentionally omits:**
`aggregateRating` (no rating system), `video` (no video), `review` (no review system), `suitableForDiet` (no dietary classification today).

---

## Theme 3: Cooklang Export

One-way export to `.cook` file format. Cooklang is a plain-text recipe markup language used by a small but dedicated community of power users and developers.

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Ingredient tokens inline in step text (`@name{amount%unit}`) | This IS Cooklang — any export that just concatenates step text without emitting `@` tokens is not Cooklang, it's plain text. Users exporting to Cooklang expect to use it in Cooklang-compatible apps (Mela, Recipe CLI, etc.). | MEDIUM | CookBot's `RecipeStep.Text` uses `[name](#id)` chips that reference `RecipeIngredient` by ID. The exporter must resolve chip IDs → ingredient records → emit `@{name}{amount%unit}` inline. This is the core mapping challenge. |
| Cookware tokens (`#tool{}`) for equipment list | Equipment from Theme 1's `Equipment` list should appear as Cooklang `#cookware{}` tokens somewhere — either as a preamble step or inline where referenced. | LOW–MEDIUM | No step-level cookware linkage in CookBot (equipment is recipe-level list). Safest: emit a synthetic first step or metadata section listing equipment. Cannot emit inline per step without step-level linkage data. |
| Timer tokens (`~{duration%unit}`) for step timers | Timer chips (`[N min]`) in step text should map to Cooklang `~{N%minutes}` tokens. CookBot already stores timers as structured chips in step text. | MEDIUM | Timer chips in `RecipeStep.Text` are stored as structured `ContentTimer` tokens. Export must render them as `~name{duration%unit}` Cooklang syntax. |
| YAML front matter for metadata | Cooklang supports YAML front matter for title, tags, servings, etc. | LOW | Emit `---\ntitle: {Title}\ntags: [{tags}]\nservings: {Servings}\n---`. Maps from `RecipeDocument` root fields. |
| Section markers (`==Section Name==`) | Multi-section recipes use section dividers; CookBot has `RecipeSection` entities. | LOW | Emit `=={section.Title}==` before steps in that section. Direct mapping. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Preparation notes as Cooklang shorthand (`@onion{1}(diced)`) | Cooklang supports inline prep notes in parentheses; CookBot ingredient prep notes map here. | LOW | If `RecipeIngredient.PrepNote` is populated, append as `(prepNote)` after the `{}` block. |
| Doneness cue as Cooklang comment | Per-step doneness cues from Theme 1 have no Cooklang syntax equivalent, but can be preserved as `-- Doneness: until golden brown` inline comments per step. | LOW | Emit doneness cues as `-- Doneness: {DonenessCue}` comment line after the step text. Non-destructive and parseable by anyone who wants it. |
| Source URL in front matter | Provenance field from Theme 1 maps to YAML front matter naturally. | LOW | `source: {SourceUrl}` in front matter. Cooklang has no reserved key for this, but YAML front matter is open. |

### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Round-trip Cooklang import | Symmetric with export | Going from Cooklang → CookBot canonical `RecipeDocument` requires re-parsing `@ingredient{amount%unit}` tokens into structured `RecipeIngredient` + `RecipeStep` with chip references. This is a separate parser implementation, not a trivial inverse of export. CookBot already has its own canonical import path (`.cookbook.json`). Cooklang import adds complexity without clear user demand for v1.4. | One-way export only. Import may be a future scope item if users request it. |
| Full semantic preservation of all Cooklang constructs in a hypothetical future import | Would need a Cooklang parser in .NET | No .NET Cooklang parser library exists as of research date; building one is large scope. | Defer import. Export only. |
| Cooklang as a storage/canonical format | Cooklang's own docs note it is "not suited as a canonical database format"; it lacks IDs, structured amounts as decimals, and schema versioning. | Conflicts with CookBot's canonical-first invariant (one source of truth = `RecipeDocument`). | Export artifact only; never the canonical form. |

**Round-trip realities:**

What maps cleanly (one-way export works well):
- Step text with `@ingredient` tokens (from chip references) — core Cooklang mapping
- Timer chips → `~{duration%unit}`
- Section names → `==Section==`
- YAML front matter for title, tags, servings
- Prep notes → `@ingredient{amount%unit}(prep note)`

What is lost going CookBot → Cooklang (acceptable for one-way export):
- `RecipeIngredient.Id` (Cooklang has no IDs; names are the identity)
- `RecipeDocument.Version` (no schema versioning in Cooklang)
- `Recipe.PhotoUrl` (no image support in Cooklang plain-text format; could reference in front matter as non-standard field)
- `RecipeIngredient.Substitutions` (no Cooklang syntax for this; could emit as comments)
- `RecipeStep.Temperature` (no Cooklang syntax; could emit as comment or inline text)
- `RecipeStep.DonenessCue` (emit as `--` comment or inline)

What is impossible to recover going Cooklang → CookBot (confirms one-way):
- Structured `RecipeIngredient` objects with IDs for chip binding
- Step-ingredient linkage beyond name matching
- Equipment list (no recipe-level equivalent in Cooklang)

---

## Theme 4: Nutrition

Auto-compute calories and macros from ingredient amounts using USDA FoodData Central data. Per-recipe and per-serving panels.

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Calories per serving | The single most-requested nutrition datum. Users expect any nutrition feature to at minimum show calories. Google rich results also use `nutrition.calories`. | HIGH | Requires: ingredient name matching → USDA food ID → nutrient lookup → unit conversion (volume/count → grams) → per-serving division. Each step has failure modes. |
| Total fat, carbohydrates, protein (macros) per serving | The "big three" macros. Standard on all nutrition panels. US FDA Nutrition Facts label requires them. | HIGH | Same pipeline as calories; USDA FoodData Central includes these as Nutrient IDs 203 (protein), 204 (total fat), 205 (total carbs). |
| Per-serving vs total display toggle | Users scale recipes; per-serving is the standard display but total is needed for meal-prep contexts. | LOW | Divide by `RecipeDocument.Servings`. Toggle is a UI checkbox. Data stored as total; display divides on render. |
| Graceful handling of unmatched ingredients | Not every ingredient will match a USDA entry (brand names, unusual items, "a pinch of"). A nutrition panel that crashes or shows 0 kcal for half the ingredients is worse than no panel. | MEDIUM | Display matched ingredients with their contributions; show unmatched as "could not be calculated" with ingredient name. Show a "% coverage" indicator so users know confidence. Do NOT silently zero unmatched items. |
| Manual override / "this ingredient has ~X calories" | Power users know their ingredients don't match exactly. A per-row manual override prevents the unmatched-ingredient problem from being a blocker. | MEDIUM | Store `RecipeIngredient.ManualCaloriesOverride` (nullable). If set, use it instead of USDA lookup. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Embedded USDA Foundation Foods dataset (SQLite, no API calls) | Self-hosted app; runtime API calls to fdc.nal.usda.gov would require API key management, hit rate limits (1,000 req/hour), and add latency. Embedding the Foundation Foods dataset (459 KB zipped / 6.5 MB unzipped as CSV) as a bundled resource eliminates all of this. Public domain (CC0). | MEDIUM | Foundation Foods (6.5 MB CSV unzipped, April 2026 release) is small enough to bundle. Parse on first startup into a SQLite table or in-memory dictionary. SR Legacy (205 MB JSON unzipped) is too large to bundle; use as fallback for a secondary lookup table if desired. Branded Foods (3.1 GB) is absolutely not bundleable. |
| Fuzzy ingredient name matching | Recipe ingredients are named inconsistently ("all-purpose flour", "AP flour", "plain flour"). Exact USDA name matching will miss many. Fuzzy string matching (Levenshtein / contains / synonym table) dramatically improves coverage. | MEDIUM–HIGH | Implement a two-pass strategy: 1) exact match on USDA description, 2) contains/normalized match (strip quantities, strip prep notes). The existing 600+ ingredient seed database can serve as a bridge — seed ingredients already link to common names. |
| Sodium, fiber, sugar as secondary panel fields | Users with specific health goals (heart health, diabetes management) expect these beyond the big three. | LOW | USDA provides Nutrient IDs 291 (fiber), 269 (sugars), 307 (sodium). Emit in a collapsed "More" section. |
| AI-assisted unmatched ingredient resolution | When an ingredient can't be matched, offer a "Ask AI to estimate" button that prompts Claude with the ingredient name and amount to return an estimated nutrient value. | MEDIUM | Uses existing `IAiService`. Returns a disclaimer-tagged estimate stored in the manual override field. Gated behind `AiFeaturesEnabled` and `UserProfile.AiEnabled`. |

### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Live USDA API calls at recipe view time | Would keep data current with USDA updates | Rate limits (1,000 req/hour), API key management, latency, and offline/self-hosted posture violation. A recipe viewed 100 times would burn 100 API calls on the same lookups. | Bundle Foundation Foods dataset; cache computed nutrition in a `RecipeNutrition` EF entity keyed to `RecipeId`. Recompute when recipe ingredients change. |
| Full US FDA Nutrition Facts label rendering | Visually appealing | The FDA Nutrition Facts label has strict regulatory formatting requirements; reproducing it accurately is a legal and visual minefield. | Show a plain nutritional summary panel, clearly labeled "Estimated nutrition". |
| Micronutrients (vitamins, minerals beyond sodium/fiber) | Some users want complete nutritional profiles | 30+ nutrient fields in USDA data; displaying them all creates UI clutter with minimal value for most users. Data quality also degrades for less-common nutrients. | Offer as an "export to CSV" option or show in a collapsible advanced panel. |
| Calorie computation for ingredients without amounts ("salt to taste", "a handful of") | Perfect coverage is the goal | These genuinely cannot be computed. Silently omitting vs. marking as unmatched is the decision — always mark as unmatched with the ingredient name shown. | Show as "not calculated: salt to taste" in the unmatched list. Do NOT attempt estimation for truly amorphous amounts. |
| Branded food matching via USDA Branded Foods database | Branded products like "Kerrygold butter" have exact nutrition data in USDA | Branded database is 3.1 GB uncompressed — not bundleable. Would require runtime API. | Use generic Foundation Foods entries ("Butter, salted") for branded ingredient names. Note the mismatch in the UI. |

**Unit conversion reality for nutrition:**
"2 cups flour" → grams is ingredient-density-specific: all-purpose flour = ~120-128g/cup (range of 8g across authoritative sources due to measuring technique variation). The correct approach: maintain a per-ingredient density table (volume-to-gram conversion), use the middle of the range as the default, and display a low-confidence marker for volume-measured dry ingredients. USDA Foundation Foods provides nutrient values per 100g; all calculations should normalize to grams first.

**USDA data licensing:** CC0 1.0 Universal (public domain dedication). No attribution required, no licensing restrictions. Safe to bundle in a GPL-3.0 project.

---

## Theme 5: Photo Enhancements

Multiple photos per recipe (gallery), backfilling photos to existing single-photo or no-photo recipes, and an optional reverse-image AI "find a photo" feature. Builds on v1.3's single hero photo pipeline (upload + paste-URL, magic-byte validation, scheme allowlist, 12 MB limits).

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Multiple photos per recipe with gallery view | Leading recipe apps (Paprika, Mealie, AllRecipes) all support multiple photos. A recipe may have a finished-dish shot, in-progress shots, and a plating shot. | MEDIUM | Requires new `RecipePhoto` entity (Id, RecipeId, Url/FilePath, Caption, SortOrder, IsPrimary). EF migration. Photo upload/paste UI extended to multi-upload. Gallery component in Recipe View. |
| Primary photo designation | One photo is the "hero" — used as the thumbnail in Cookbook collage, Schema.org `image`, PDF cover. Users can designate which photo is primary. | LOW | `RecipePhoto.IsPrimary` boolean. Enforce exactly one primary per recipe. Migrations must back-fill existing `Recipe.PhotoUrl` → first `RecipePhoto` record with `IsPrimary = true`. |
| Drag-to-reorder photos | Paprika uses tap-hold drag for photo ordering. Users expect to be able to set the display order of a gallery. | MEDIUM | `SortOrder` int column. Drag-reorder in editor UI. Blazor drag-drop or up/down arrow controls (simpler, more accessible). |
| Photo captions | Paprika supports photo naming/renaming. Captions help distinguish "finished dish" from "step 3 — before the fold". | LOW | `RecipePhoto.Caption` nullable string. Optional text input in the gallery editor. |
| Backfill UI for existing recipes | Post-v1.3, recipes have at most one `PhotoUrl`. The migration to multi-photo must be smooth: existing hero photo becomes the primary, and a gallery editor appears in the recipe editor for adding more. | MEDIUM | EF migration: read `Recipe.PhotoUrl`, create `RecipePhoto` row with `IsPrimary=true`, `SortOrder=0`. After migration, `Recipe.PhotoUrl` column becomes a computed/deprecated field pointing to the primary photo URL (or can be null and resolved dynamically). |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Per-step photo linking | Paprika allows embedding `[photo: name]` references in step text. Shows a thumbnail in Cooking Mode — extremely useful for "it should look like this". | HIGH | Requires either a `StepPhotoUrl` on `RecipeStep` (simple, limited to one per step) or a `RecipeStepPhoto` junction entity (powerful, complex). Step-level photo is a strong differentiator from most recipe apps; also HIGH complexity. Consider deferring to v1.5. |
| Reverse-image AI "find a photo for this recipe" | AI suggests a web image URL or generates a stock-like image based on recipe title + description. Reduces friction for adding a hero photo to a newly AI-generated recipe. | HIGH | See anti-features below — this is listed as differentiator because it can be done safely (Unsplash/Pexels API for free stock photos, framed as "search for photos"), but has significant caveats. |
| Bulk photo backfill via AI-suggested Unsplash query | For existing recipes with no photo, offer a batch action: "Suggest photos for all recipes without photos" that queries Unsplash API with the recipe title and presents the top result for confirmation. | HIGH | Unsplash API is free for attribution use (CC license, must show "Photo by X on Unsplash"). Requires Unsplash API key management (new external dependency). |
| Photo thumbnail in recipe cards on Home/List views | Multiple photos can be shown as a mini-collage on the recipe list card, like Cookbook collage thumbnails. Visual density improvement. | LOW–MEDIUM | Reuse existing `CbRecipeCard` component; pull the primary photo URL from the new `RecipePhoto` table. |

### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| AI reverse-image search finding web photos | "Automate hero photos" | Finding photos via web reverse-image search and embedding them in the recipe is copyright infringement unless the image is licensed for reuse. Google Image Search results are not free to embed. | Frame as "search for free-licensed photos" on Unsplash/Pexels/Pixabay, which provide clearly licensed results. Always require user confirmation before saving. Never auto-fetch a photo from a random web URL found via image search. |
| AI image generation (DALL-E / Stable Diffusion) for recipe photos | AI food images look plausible | AI-generated food images do not represent the actual dish the user will make — they're fictional visualizations. For a recipe tracker ("durable home for recipes you actually cook"), a generated image is actively misleading. Legal status of AI-generated images is also unsettled as of 2026. | Encourage users to upload their own photo after cooking. The existing paste-URL pipeline lets users grab photos from their own cloud storage. |
| Automatic cropping / resizing server-side | Quality improvement | Adds ImageSharp or similar dependency; PNG/JPEG re-encoding quality is opinionated. v1.3 intentionally avoided server-side image processing. | Store originals; use CSS `object-fit: cover` for gallery thumbnails. Add a note to the upload UI about recommended aspect ratios. |
| Unlimited photo storage per recipe | Power users want all their cook photos | Storage concern on self-hosted instances with limited disk. Current single-photo cap is 12 MB. | Cap at e.g., 10 photos per recipe, each up to 12 MB. Document the limit clearly. |

---

## Feature Dependencies

```
Theme 1: Richer Format (v4 schema bump + upcaster)
    └──required by──> Theme 2: Schema.org JSON-LD (AuthorName, SourceUrl fields)
    └──required by──> Theme 3: Cooklang export (Equipment, DonenessCue for comment passthrough)
    └──required by──> Theme 5: Photo gallery (v4 schema must accommodate PhotoUrl deprecation / RecipePhoto migration)

Theme 4: Nutrition
    └──enhances──> Theme 2: Schema.org (nutrition.calories in JSON-LD only emittable when Theme 4 exists)
    └──independent of──> Theme 1, 3, 5

Theme 5: Photo Enhancements
    └──depends on──> v1.3 photo pipeline (upload, paste-URL, magic-byte validation, scheme allowlist, 12 MB limit)
    └──enhances──> Theme 2: Schema.org (multi-aspect-ratio image array becomes possible with multiple photos)

RecipeDocument v4 bump (Theme 1)
    └──required before──> all other themes that add new fields to RecipeDocument
    └──upcaster chain: v1→v2→v3→v4 (existing pattern from v1.3)
```

### Dependency Notes

- **Theme 1 (v4 bump) should be Phase 12:** All other themes add fields to `RecipeDocument` or depend on v4 provenance fields (AuthorName for Schema.org, Equipment for Cooklang). The schema bump and upcaster must land first.
- **Theme 4 (Nutrition) is independent:** USDA data pipeline has no schema dependencies beyond ingredient amount/unit fields that already exist in v3. Can be parallelized with Theme 1 planning but should be implemented after the v4 bump lands (to avoid a second upcaster pass).
- **Theme 5 (Photos) is independent of Themes 2/3:** Gallery entity is a new `RecipePhoto` table, not a `RecipeDocument` field. However, the v4 upcaster is a natural point to migrate `Recipe.PhotoUrl` → primary `RecipePhoto` row — coordinate timing.
- **Theme 2 (Schema.org) + Theme 3 (Cooklang) are export-only:** No schema dependencies introduced; they read from the canonical doc. Can be Phase 13/14 after the v4 bump.

---

## MVP Definition for v1.4

### Launch With (minimum viable for each theme)

- **Theme 1:** `SourceUrl` + `AuthorName` (provenance), `Equipment []string` (recipe-level), `RecipeStep.DonenessCue` (string?), `RecipeIngredient.Substitutions []IngredientSubstitution`; v3→v4 upcaster (all fields nullable, existing recipes upcasted to v4 with null values); AI prompt schema update; unit tests for upcaster and new fields.
- **Theme 2:** `<script type="application/ld+json">` in Recipe View `HeadContent`; emit required (`name`, `image`) + all mappable recommended fields from v4 doc; omit `aggregateRating`, `video`, `nutrition` (unless Theme 4 lands first in same milestone); Google Rich Results test validated.
- **Theme 3:** `.cook` file download from Recipe View; correct `@ingredient{amount%unit}` token emission from chip references; `~{timer%unit}` from timer chips; `==Section==` from sections; YAML front matter with title/tags/servings/source.
- **Theme 4:** Foundation Foods CSV bundled as seed data in `Infrastructure`; fuzzy name matcher in `Application`; `RecipeNutrition` EF entity caching computed values; per-serving/total toggle in Recipe View; unmatched-ingredient list with coverage indicator; no live USDA API calls.
- **Theme 5:** `RecipePhoto` entity + EF migration (back-fills existing `Recipe.PhotoUrl`); multi-upload in Recipe Editor; gallery in Recipe View with primary designation + drag-reorder; `Recipe.PhotoUrl` computed from primary photo for backward compat.

### Add After Validation (v1.4.x)

- Nutrition: manual calorie override per ingredient
- Nutrition: sodium/fiber/sugar secondary panel
- Photos: photo captions
- Schema.org: `recipeCategory` + `recipeCuisine` fields (requires new v4 or v4.1 fields + editor UI)
- Cooklang: doneness cue as `--` comment passthrough

### Future Consideration (v1.5+)

- Photos: per-step photo linking (high complexity)
- Photos: Unsplash API bulk backfill (external API dependency)
- Nutrition: AI-assisted unmatched ingredient resolution
- Cooklang: import (requires .NET Cooklang parser or custom implementation)
- Nutrition: micronutrient extended panel

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Source/provenance fields (Theme 1) | HIGH | LOW | P1 |
| Equipment list (Theme 1) | HIGH | LOW | P1 |
| Per-step doneness cues (Theme 1) | HIGH | LOW | P1 |
| Ingredient substitutions (Theme 1) | HIGH | MEDIUM | P1 |
| v3→v4 upcaster (Theme 1) | HIGH | LOW–MEDIUM | P1 |
| Schema.org JSON-LD export (Theme 2) | MEDIUM | LOW–MEDIUM | P1 |
| Cooklang export (Theme 3) | MEDIUM | MEDIUM | P2 |
| Multiple photos + gallery (Theme 5) | HIGH | MEDIUM | P1 |
| Primary photo migration (Theme 5) | HIGH | LOW | P1 |
| Nutrition calories + macros (Theme 4) | HIGH | HIGH | P1 |
| USDA bundled dataset (Theme 4) | HIGH | MEDIUM | P1 |
| Unmatched ingredient handling (Theme 4) | HIGH | MEDIUM | P1 |
| Schema.org `nutrition.calories` (Theme 2, depends on Theme 4) | MEDIUM | LOW | P2 |
| Per-step photo linking (Theme 5) | MEDIUM | HIGH | P3 |
| AI-assisted substitution generation (Theme 1) | MEDIUM | MEDIUM | P2 |
| Unsplash bulk backfill (Theme 5) | LOW | HIGH | P3 |

---

## Sources

- [Google Recipe Rich Results documentation](https://developers.google.com/search/docs/appearance/structured-data/recipe) — required vs recommended properties (HIGH confidence)
- [Schema.org/Recipe type definition](https://schema.org/Recipe) — property types and inheritance from CreativeWork/Thing (HIGH confidence)
- [Cooklang specification](https://cooklang.org/docs/spec/) — ingredient/cookware/timer/metadata syntax (HIGH confidence)
- [Cooklang recipe formats comparison for developers](https://cooklang.org/blog/41-recipe-formats-for-developers/) — round-trip realities and format limitations (HIGH confidence)
- [USDA FoodData Central API guide](https://fdc.nal.usda.gov/api-guide/) — endpoints, rate limits, data types (HIGH confidence)
- [USDA FoodData Central download datasets](https://fdc.nal.usda.gov/download-datasets/) — Foundation Foods 459 KB zipped / 6.5 MB unzipped; CC0 license (HIGH confidence)
- [USDA FoodData Central FAQs](https://fdc.nal.usda.gov/faq/) — licensing confirmation CC0 1.0 Universal (HIGH confidence)
- Paprika app (iOS user guide and feature documentation) — multiple photos, drag-reorder, per-step photo embedding, equipment handling (MEDIUM confidence — app behavior; no official schema docs)
- Mealie GitHub discussions (#694, #2264, #4311) — ingredient substitution feature requests; absence of a first-class substitutions field confirmed (MEDIUM confidence)
- Cups-to-grams conversion variance analysis (multiple culinary sources) — density variability for volume-measured dry ingredients (MEDIUM confidence — consistent across sources)

---

*Feature research for: v1.4 Recipe Data & Interoperability (FreelovesCookBot)*
*Researched: 2026-06-05*
