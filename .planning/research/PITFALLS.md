# Pitfalls Research

**Domain:** Recipe app — schema evolution, external nutrition API, SEO markup export, one-way format export, photo galleries with AI assist
**Researched:** 2026-06-05
**Confidence:** HIGH (schema bump, photo storage — based on existing codebase evidence); MEDIUM (USDA FDC, Schema.org, Cooklang — based on official docs + codebase context); MEDIUM (AI photo legal risk — based on legal research)

---

## Critical Pitfalls

### Pitfall 1: Forgetting to register Migration_V3_To_V4 in DI — chain gap at runtime, not at test time

**What goes wrong:**
`RecipeUpcasterChain` validates for gaps at *construction time*, meaning the gap-detection test only runs when `AddApplication()` wires up the DI container. Any callsite that constructs `RecipeUpcasterChain` directly (a test helper, a standalone migration tool, a console seed script) with only the V1→V2 and V2→V3 upcasters and forgets the new V3→V4 one will throw `InvalidOperationException: Upcaster chain has a gap: 3 -> ?` at boot — not during development. The symptom in production is a startup crash, not a silent wrong-version read.

**Why it happens:**
The DI registration in `DependencyInjection.cs` adds upcasters via `services.AddSingleton<IRecipeUpcaster, Migration_V3_To_V4>()` — if that line is omitted, the chain is structurally incomplete. Developers writing the new migration file remember the class but forget to register it. Tests that construct the chain directly with `new IRecipeUpcaster[]{ new Migration_V1_To_V2(), new Migration_V2_To_V3() }` will also miss the new step if the test fixture is not updated in the same PR.

**How to avoid:**
- Write `Migration_V3_To_V4_ChainTests` (parallel to the existing `Migration_V2_To_V3_ChainTests`) as part of the Phase that introduces v4. The test must include V3→V4 in the chain or it will fail immediately.
- Add a `SchemaAssertionTests`-style invariant: `Assert.Equal(RecipeUpcasterChain.CurrentVersion, _chain.UpcastToCurrent(minimalV1Node)["version"].GetValue<int>())` using a chain built from DI rather than `new`.
- The `RecipeUpcasterChain.CurrentVersion` constant lives next to the chain class — bump it to 4 in the same commit that adds `Migration_V3_To_V4`, so a missing registration fails the constant-vs-chain-end assertion immediately.

**Warning signs:**
- App crashes at startup with `InvalidOperationException: Upcaster chain has a gap`.
- Test run includes `Migration_V2_To_V3_ChainTests` but no `Migration_V3_To_V4_ChainTests`.
- `grep -r "IRecipeUpcaster" src/CookBot.Application/DependencyInjection.cs` does not show a line for `Migration_V3_To_V4`.

**Phase to address:**
The richer-format schema bump phase (Phase 12 or wherever v4 lands). The DI registration and the gap-detection test must be in the same plan as the `Migration_V3_To_V4` class file.

---

### Pitfall 2: Bundle-throw in the V3→V4 upcaster — one absent field aborts the other fields

**What goes wrong:**
If `Migration_V3_To_V4.Upcast()` writes all new fields in a single block without independent guards, a recipe missing one of the new v4 fields (e.g., `substitutions` absent because it was authored before v4) will crash the whole upcast. This was explicitly defended against in `Migration_V2_To_V3` — the comments cite `PITFALLS C7 — never bundle-throw` and each guard is `if (obj["field"] is null) { /* no-op */ }`. A copy-paste of that pattern without understanding why it exists will produce a bundled upcast that throws for partial v3 docs.

**Why it happens:**
The new v4 fields (substitutions list, equipment list, per-step doneness cues, source/provenance) are all nullable/optional. A developer who writes `obj["substitutions"] = JsonSerializer.SerializeToNode(...)` without checking for pre-existing presence will silently overwrite any value already set by a forward-compat `RecipeDocument.Extras` round-trip. Another common failure: doing a `.AsArray()` call on `obj["substitutions"]` when the field is absent throws `InvalidOperationException`.

**How to avoid:**
- Follow the exact pattern in `Migration_V2_To_V3`: one `if (obj["field"] is null) { /* no-op */ }` guard per new field, with no shared state between guards.
- The v4 fixture set must include at least: a v3 doc with none of the new fields, a v3 doc with only `substitutions` set, a v3 doc with only `equipment` set, and a doc with all new fields set — each must upcast to v4 without throwing.
- The `Upcast_NoTemperature_ContentStepTemperatureIsNull`-style test has a direct parallel for each new field: assert the field is absent on output if absent on input.

**Warning signs:**
- `Migration_V3_To_V4.Upcast()` accesses a new field's array/object without a null check.
- Fixture test matrix has fewer than 4 combinations for the new fields.
- Code review sees `obj["substitutions"].AsArray()` rather than `obj["substitutions"] as JsonArray`.

**Phase to address:**
The richer-format schema bump phase. Treat as non-negotiable: the fixture matrix and independent-guard pattern are the acceptance criteria.

---

### Pitfall 3: AI schema drift — the model still emits v3 fields after the schema bump to v4

**What goes wrong:**
`RecipeJsonSchemaProvider` generates the Anthropic structured-output schema from `RecipeDocument` via `JsonSchemaExporter`. After the v4 bump, new fields (`substitutions`, `equipment`, `doneness`, `source`) appear in the exported schema. However, the `AiRecipeGenerator` caches the schema lazily (`_schema = new Lazy<JsonNode>(BuildSchema)`). If the app hot-reloads without a full restart (dev only, unlikely in prod), or if a test uses the pre-v4 schema snapshot, the AI will be constrained to emit v3-shaped output and the new fields will be empty on every AI-generated recipe.

The more insidious variant: the prompt documentation (in `RecipeSchemaDocumentationProvider.GetFormatPrompt()`) is updated to mention the new fields, but the JSON schema passed to Anthropic's `output_config.format` is not — or vice versa. The AI "knows" about the new fields from the system prompt but the structured-output grammar rejects them, causing silent loss.

**Why it happens:**
Two code sites must be updated in sync: (1) the `RecipeDocument` POCO type (which drives the schema exporter), and (2) the `RecipeSchemaDocumentationProvider` format-prompt string. They are in separate files; a partial PR update leaves them mismatched. The existing `PromptSnapshotTests` (Verify.Xunit byte-stable snapshot) will catch drift — but only if the snapshot is updated in the same commit.

**How to avoid:**
- Update the prompt snapshot (`Snapshots/PromptSnapshotTests.*`) in the same plan that adds v4 fields to `RecipeDocument`. The snapshot test fails on CI if the two are out of sync.
- Add the new optional fields (`substitutions?`, `equipment?`, `doneness?`, `source?`) to `RecipeDocument` as nullable with `[JsonPropertyName]` attributes before writing the documentation provider changes, so the schema exporter reflects them automatically.
- Add an `AiRecipeFixtureTests`-style integration fixture for a recipe that includes the new fields to verify the round-trip (generate JSON with new fields → `JsonRecipeSerializer.Deserialize` → non-null values).

**Warning signs:**
- `PromptSnapshotTests` is updated without also updating `RecipeDocument`.
- AI-generated recipes always have null `substitutions` and null `equipment` even after the schema bump.
- `RecipeJsonSchemaProvider.GetSchema()` output (logged at debug level or dumped in a test) does not contain the new field names.

**Phase to address:**
The richer-format schema bump phase. The prompt snapshot test update is the verification gate.

---

### Pitfall 4: USDA FoodData Central ingredient name matching returns the wrong food — silently wrong nutrition

**What goes wrong:**
The FDC `/foods/search` endpoint does text-based fuzzy matching. "Butter, unsalted" may return a branded margarine. "Brown sugar" may rank a Foundation Foods entry for "Sugars, brown" (100 g reference) above the "Brown sugar, packed" entry that matches the recipe context. "Chicken breast" may return values for raw, with-bone, which is a different calorie density than boneless-skinless cooked. Any wrong match propagates into per-serving calorie/macro numbers that look plausible but are wrong. No error is thrown; the calculation completes "successfully."

**Why it happens:**
FDC full-text search returns results ranked by relevance, not by cooking-use likelihood. The database includes five data types (Foundation Foods, SR Legacy, FNDDS, Branded Foods, Experimental) with wildly different granularity. "1 cup flour" matches "Wheat flour, all-purpose" in Foundation Foods (correct) but could also match "ENRICHED FLOUR" from a branded food (different serving size reference). Without explicit data-type filtering (`dataType=Foundation,SR Legacy`) and a fuzzy-match confidence threshold, every lookup is a gamble.

**How to avoid:**
- Restrict initial lookups to `dataType=Foundation Foods,SR Legacy` — these have peer-reviewed per-100g values and are not brand-specific. Never accept a Branded Foods result without user confirmation.
- Always store the matched FDC food ID, food description, and data type alongside the computed nutrition value. Display the match to the user ("Matched to: Wheat flour, all-purpose [FDC #20081]") so errors are visible.
- Implement a confidence threshold: if the top search result's relevance score is below the threshold, mark the ingredient as "unmatched" rather than silently using the low-confidence match.
- Show a per-ingredient match review UI before persisting nutrition. Do not auto-persist without user acknowledgement on first use.
- For unmatched or low-confidence ingredients, display "-- kcal" rather than zero or an estimated value. "Zero" is indistinguishable from "we computed this and it's zero calories."

**Warning signs:**
- Nutrition panel for a recipe with "2 cups all-purpose flour" shows calorie count far below the expected ~900 kcal.
- Pantry ingredient "olive oil" returns a branded product's nutrition instead of "Oil, olive, salad or cooking [FDC #4053]".
- Matched food descriptions are not displayed to the user anywhere in the UI.

**Phase to address:**
The nutrition phase. The match-review UX and confidence threshold must be in the acceptance criteria; the phase should not be considered done until a "show me what was matched" surface exists.

---

### Pitfall 5: Volume-to-mass unit conversion using a generic density — silently wrong calorie count

**What goes wrong:**
FDC nutrition values are per 100 g (by mass). Recipe ingredients are in volume units: "1 cup all-purpose flour", "2 tablespoons olive oil", "1/2 cup packed brown sugar". Converting volume to mass requires the ingredient-specific density. Using a generic water density (1 g/mL) produces: 1 cup flour = 237 g instead of the correct ~125 g — nearly double the mass, nearly double the calories. Applying the wrong density silently doubles or halves every volume-measured ingredient's calorie contribution.

**Why it happens:**
Developers reach for a simple `volume_ml * 1.0 g/mL` formula because it is simple and the error is not obvious. FDC does include serving-size data (`servingSize` and `servingSizeUnit` fields in the food detail response) for some foods, but this is inconsistent across data types and not available for all Foundation Foods entries. The temptation is to ignore density and use mass-from-volume directly.

**How to avoid:**
- Build a density lookup table for the ~50 most common cooking ingredients (flour, sugar, butter, oil, milk, cream, honey, cocoa powder, etc.) from authoritative sources (USDA ARS measurement conversion tables). This covers 90% of recipe volume measurements.
- For ingredients not in the density table, mark their volume-unit conversions as "approximate" and surface a disclaimer.
- Prefer to match FDC entries that have a `gramWeight` field for the matching household measure (e.g., "1 cup, sifted" = 125 g for all-purpose flour) — FDC `foodPortions` data includes this for many Foundation Foods entries.
- Never use the water density constant (1 g/mL ≈ 236.6 g/cup) as a fallback without flagging the result as low-accuracy.
- The existing `UnitConversionService` already handles unit normalization — the density lookup belongs here, not in the nutrition service, so the conversion is testable in isolation.

**Warning signs:**
- "1 cup butter" computes as ~237 g instead of the correct ~227 g.
- "1 cup all-purpose flour" computes caloric contribution matching ~900+ kcal instead of the expected ~455 kcal.
- The nutrition service has a line like `var massGrams = volumeMl * 1.0` without a density lookup call.

**Phase to address:**
The nutrition phase. The density lookup table must be a first-class artifact with unit tests covering the 20 most common ingredients.

---

### Pitfall 6: Presenting nutrition numbers without a disclaimer — user treats app as medical/dietary authority

**What goes wrong:**
A user with a medical dietary restriction (diabetes, kidney disease, eating disorder) uses the app's calorie panel for meal planning and relies on it as accurate. The numbers are USDA-sourced estimates, not certified nutritional analysis. Recipe cooking (reduction, caramelization, absorption) changes actual nutrient content. Serving sizes are self-reported. A wrong FDC match (Pitfall 4) or a density error (Pitfall 5) silently produces wrong numbers. The app is GPL-licensed for self-hosting, so the maintainer bears no commercial liability — but the user of a particular self-hosted instance might.

**Why it happens:**
Nutrition features without disclaimers are the norm in popular recipe apps, so developers copy the pattern. The legal and UX requirement to disclaim accuracy feels like polish — it is skipped for MVP.

**How to avoid:**
- Display a persistent non-dismissable disclaimer on every nutrition panel: "Estimates based on USDA FoodData Central. Results are approximate and not suitable for medical dietary planning."
- Use the word "estimate" not "calories" in the heading.
- Attribute FDC as the data source per the CC0 license request: "Data: USDA FoodData Central."
- Do not display macro precision beyond one decimal place — false precision amplifies the impression of accuracy.

**Warning signs:**
- Nutrition panel shows "Calories: 482.7 kcal" with no disclaimer text visible.
- The UI design for the nutrition panel looks identical to a certified nutritional label.

**Phase to address:**
The nutrition phase. The disclaimer copy should be in the UX spec before the first commit of nutrition UI.

---

### Pitfall 7: USDA FDC API outage or rate limit causes a synchronous exception that crashes the recipe-save flow

**What goes wrong:**
If nutrition lookup is called synchronously during recipe save (as part of `RecipeService.CreateAsync` or `UpdateAsync`), an FDC API outage (HTTP 503), network timeout, or rate-limit response (HTTP 429) causes the save to fail with an unhandled exception. The user loses their recipe edit because a third-party API was down.

**Why it happens:**
Treating nutrition as a first-class save step rather than a post-save enrichment. It mirrors the pattern of "compute everything at save time" that works for local operations but is fragile for external calls.

**How to avoid:**
- Nutrition lookup must be a *post-save* enrichment, never blocking the save path. Save the recipe first, then enqueue/trigger nutrition computation as a fire-and-forget background task or explicit UI action.
- Cache FDC results in a local SQLite table keyed by `(normalizedIngredientName, fdcFoodId)` with a TTL (e.g., 30 days). FDC data changes infrequently; a stale cache is better than a failed save.
- Rate-limit awareness: the FDC API allows 1,000 requests/hour per IP with a registered key. At ~15 ingredients per recipe, a user batch-importing 70 recipes would exhaust the hourly limit. Cache hits must be used to stay under the limit.
- On HTTP 429 or timeout, the nutrition column stays null and a soft warning ("Nutrition unavailable — will retry") is shown. Never block the user flow.

**Warning signs:**
- `FdcNutritionService.EnrichAsync()` is called inside `RecipeService.CreateAsync`.
- There is no local cache table for FDC results.
- No retry policy or circuit-breaker pattern around the FDC HTTP call.

**Phase to address:**
The nutrition phase. The architectural decision (post-save enrichment + cache) must be in the phase plan before the implementation plan.

---

### Pitfall 8: Schema.org JSON-LD uses relative image URLs or local `/uploads/` paths — fails Google indexing

**What goes wrong:**
The Schema.org `image` field requires a fully-qualified HTTPS URL ("crawlable and indexable" per Google's structured data requirements). A recipe with a locally-uploaded photo at `/uploads/abc123.jpg` will have that path written into the JSON-LD block as `"image": "/uploads/abc123.jpg"` — a relative URL that Google's indexer cannot resolve from an external crawl. Google will reject the image field and the recipe will not qualify for rich results.

**Why it happens:**
The `RecipeDocument.PhotoUrl` field stores whatever was saved: a local upload path (`/uploads/{guid}.jpg`) or a pasted external URL. JSON-LD generation reads `doc.PhotoUrl` directly without checking whether it is an absolute HTTPS URL. For a trusted-LAN self-hosted app, the question of "what is the public base URL?" is non-trivial — there is no `HttpContext.Request.Host` equivalent that is always correct for SEO purposes.

**How to avoid:**
- JSON-LD generation is SEO-only; the primary use case for this app is self-hosted personal use, not Google indexing. Make this explicit: the `<script type="application/ld+json">` block is conditionally omitted when `PhotoUrl` is null or is a relative/local path. A recipe with only a local upload photo simply does not emit a `image` field in the JSON-LD.
- If an absolute URL is available, validate it is `http://` or `https://` before including it.
- Do not invent a configurable `PublicBaseUrl` setting for this milestone unless SEO is an explicitly requested feature — it adds complexity for a trusted-LAN app.

**Warning signs:**
- JSON-LD serializer calls `$"\"image\": \"{doc.PhotoUrl}\""` without checking `Uri.IsAbsoluteUri`.
- A recipe with a local upload shows `"image": "/uploads/abc123.jpg"` in the page source.

**Phase to address:**
The Schema.org export phase. The conditional-omission logic for relative URLs is a first-class requirement, not a polish item.

---

### Pitfall 9: Schema.org ISO 8601 duration formatted as bare minutes integer instead of PT{N}M

**What goes wrong:**
Google requires `cookTime` and `prepTime` in ISO 8601 duration format: `"PT30M"` for 30 minutes, `"PT1H30M"` for 90 minutes. Writing `"cookTime": 30` or `"cookTime": "30 minutes"` fails structured data validation. Google's Rich Results Test will flag these as errors and the recipe will be ineligible for rich results. The `RecipeDocument` stores times as `int? PrepTimeMinutes` and `int? CookTimeMinutes` — the conversion to `PT{H}H{M}M` format is a trivial but easy-to-forget step.

**Why it happens:**
The developer serializes the integer directly from the `RecipeDocument` property. The format looks reasonable in isolation; the error only surfaces when validated externally.

**How to avoid:**
- Write a static helper `IsoFormatDuration(int? totalMinutes)` that returns `null` when input is null, `"PT{N}M"` for under 60 minutes, and `"PT{H}H{M}M"` for 60+ minutes.
- Add a unit test for the boundary cases: null → null, 0 → `"PT0M"`, 30 → `"PT30M"`, 60 → `"PT1H"`, 90 → `"PT1H30M"`.
- The JSON-LD serializer should call this helper, not access the int field directly.

**Warning signs:**
- JSON-LD output contains `"cookTime": 30` or `"cookTime": "30 minutes"`.
- No unit test for the duration formatter.
- Google's Rich Results Test returns a `cookTime` format error.

**Phase to address:**
The Schema.org export phase. The duration formatter and its tests must be in the first plan that produces JSON-LD output.

---

### Pitfall 10: Cooklang export implies round-trip capability — users expect re-import

**What goes wrong:**
Cooklang export is intentionally one-way: section headers, ingredient substitutions, doneness cues, per-step temperature, source/provenance, prep/cook time metadata, and the `RecipeDocument.Extras` round-trip bag are all either lost or imperfectly mapped during export. If the UI presents a "Download as Cooklang" button without a visible disclaimer, users will download their recipes, use a Cooklang tool to modify them, and expect to re-import them back. The re-import path does not exist; data edited externally is lost.

**Why it happens:**
Export buttons imply bidirectionality to users. The `PROJECT.md` is explicit that Cooklang export is "one-way, no import required" — but that invariant must be surfaced in the UI, not just in the planning documents.

**How to avoid:**
- Add a clear "(one-way export)" or "for sharing only" label to every Cooklang download affordance.
- Do not add a Cooklang import path in this milestone.
- Log a warning for any `RecipeDocument` field that has a non-null value that cannot be represented in Cooklang (substitutions, provenance, etc.) so the developer knows the loss is expected and intentional.

**Warning signs:**
- The download button is labeled "Export to Cooklang" with no qualification.
- An issue is opened asking for Cooklang import — this is the expected user response when the round-trip assumption is made.

**Phase to address:**
The Cooklang export phase. The "one-way only" label is an acceptance criterion.

---

### Pitfall 11: Cooklang special characters in ingredient names or step text break the export — `@`, `#`, `~` in content

**What goes wrong:**
Cooklang uses `@` to start ingredient references, `#` for cookware, and `~` for timers. If a recipe step's text contains these characters naturally (e.g., a step that references a social-media handle, a temperature like "45°C #target", or a step with a tilde in a brand name), the Cooklang serializer will produce malformed output. Worse: there is no documented escaping mechanism in the Cooklang spec for these characters — the spec does not define a backslash-escape or similar. The exported `.cook` file may parse incorrectly in a Cooklang-compatible tool.

**Why it happens:**
Recipe step text in `RecipeDocument.StepNode.ContentStep.Text` is freeform with `[name](#id)` ingredient links as the only structured content. The Cooklang exporter must strip `[name](#id)` links and replace them with `@name{...}` syntax, but it must also defensively scan for bare `@`, `#`, `~` in surrounding text and handle them. There is no standard escape — the safe option is to replace them with Unicode lookalikes or remove them.

**How to avoid:**
- Before emitting step text to Cooklang, strip or replace bare `@`, `#`, `~` characters that are not part of a recognized `[name](#id)` pattern. Log the substitution so the developer can see what was sanitized.
- Write a test: a recipe step containing `"Add to a 45°C bath #temp"` should produce valid Cooklang output with the `#` either removed or rendered as a plain `#` in a manner that does not start a cookware reference.
- Mark the Cooklang export as "best-effort" in the UI disclaimer — lossy not just for format features but for edge-case characters.

**Warning signs:**
- No test cases with `@`, `#`, or `~` in step text.
- The Cooklang exporter passes `step.Text` directly to output without sanitization.

**Phase to address:**
The Cooklang export phase. Character sanitization must be in the same plan as the step-text serializer.

---

### Pitfall 12: AI "find a photo" returns a hallucinated URL or a copyrighted image — persisted and displayed

**What goes wrong:**
An AI asked to "find a photo for this recipe" may return a plausible-looking image URL that (a) does not exist (hallucinated — HTTP 404 on fetch), (b) is an infringing image (Getty, Shutterstock, photographer's portfolio — copyright not cleared), (c) shows the wrong food (a photo of a beef stew labeled as a chicken curry), or (d) is a privacy-violating image (a real person's food photo from a private Instagram). All four scenarios persist a bad URL to `RecipeDocument.PhotoUrl`, potentially in a shared cookbook that other users see.

**Why it happens:**
Claude (and other LLMs) do not have reliable access to live image databases. They pattern-match from training data — they may emit URLs that look like stock photo URLs but are fabricated, or they may recall real URLs from training that are no longer valid or that link to images they are not authorized to reproduce. The v1.3 `RecipePhotoUrlValidator` validates scheme (`http`/`https` only) and max length but does not validate that the URL actually resolves to a real image. The `onerror` fallback handles broken URLs in rendering but does not prevent persisting them.

**How to avoid:**
- Do NOT ask the AI to provide a photo URL directly. Instead, implement the "find a photo" feature as a search-query suggestion — the AI suggests search terms (e.g., "chocolate lava cake close-up food photography"), and the user pastes their own chosen URL. This removes the copyright and hallucination risk entirely.
- If a direct AI URL path is kept, always HEAD-check the URL before persisting: HTTP 4xx = reject. This eliminates hallucinated URLs at save time.
- Add a copyright disclaimer: "Ensure you have the right to use any image you add to a recipe." Display this on every photo input surface.
- Do not add "reverse image search" capability that automatically finds and persists photos without user review. The user must always be the last actor before a photo URL is saved.
- Extend the existing `RecipePhotoUrlValidator` to optionally do a HEAD request (with a short timeout, fire-and-forget) and surface a "URL could not be verified" warning to the editor.

**Warning signs:**
- The AI is instructed to "provide a URL for an image of this recipe" in the system prompt.
- Photo URLs from AI are persisted without any fetch-validation step.
- No copyright disclaimer is visible anywhere in the photo input UI.

**Phase to address:**
The photo enhancement phase. The "search-term suggestion only" decision must be made before implementation begins; reverting after building a direct-URL-from-AI flow is expensive.

---

### Pitfall 13: Multiple-photos gallery — orphaned files on recipe delete or photo removal

**What goes wrong:**
When a recipe is deleted or a photo is removed from the gallery, the underlying file in `wwwroot/uploads/` is not deleted. With a single hero photo per recipe (v1.3 state), the orphan risk was low — only one file per recipe. With a gallery of N photos, recipe deletion or repeated photo cycling by the user accumulates orphaned files that grow the deployment volume indefinitely. In a Docker container with a mounted persistent volume, this silently fills the disk.

**Why it happens:**
The EF Core delete for a `Recipe` row does not know about the filesystem. `wwwroot/uploads/` is outside the database transaction scope. Without an explicit file-deletion hook in `RecipeService.DeleteAsync` (or an analogous `RecipePhotoService.DeleteAsync`), the files are abandoned. SQLite's cascade-delete removes the DB rows (assuming `PRAGMA foreign_keys = ON` is set on every connection — a known .NET SQLite footgun) but the filesystem is not cascade-deleted.

**How to avoid:**
- `RecipePhotoService` (or the equivalent gallery service) must own file lifecycle: every `SaveAsync` call is paired with a corresponding `DeleteAsync(path)` call when the photo is removed.
- `RecipeService.DeleteAsync` must enumerate all `PhotoUrl` values for the recipe being deleted (including gallery entries) and delete each local file before or after the DB delete.
- Only delete files with paths matching the `/uploads/` prefix — do not attempt to delete external `http://` URLs. This is the same guard already in `LocalRecipePhotoStorage`.
- Add a background cleanup pass (triggered on startup or by an admin action) that finds files in `wwwroot/uploads/` with no corresponding DB row — a safety net for any missed deletion.

**Warning signs:**
- `RecipeService.DeleteAsync` deletes the `Recipe` row but contains no `File.Delete` call or photo-service call.
- `wwwroot/uploads/` grows without bound during UAT testing (delete a recipe, check if the file is still there).

**Phase to address:**
The photo enhancement phase. The lifecycle contract for local files must be in the plan before gallery implementation begins.

---

### Pitfall 14: Multiple-photo upload in Blazor Server hits SignalR MaximumReceiveMessageSize per-selection

**What goes wrong:**
Blazor Server's `InputFile` component transfers file bytes over the SignalR circuit. The current `Program.cs` sets `MaximumReceiveMessageSize = 12 * 1024 * 1024` (12 MB) to accommodate the single-hero-photo 10 MB per-file limit. When uploading multiple files at once (a gallery of 5 photos selected together), the browser sends the upload manifest as a single SignalR message. At 5 × up-to-10 MB, the manifest can exceed the 12 MB limit, dropping the circuit and displaying a blank page. The user loses their selection.

**Why it happens:**
`MaximumReceiveMessageSize` was sized for a single 10 MB file plus overhead. Multiple simultaneous file selections multiply the metadata (not the byte payload, which streams separately) but the selection manifest itself can still hit limits when many files are selected. GitHub issue dotnet/aspnetcore#42993 describes this as a known limitation: the InputFile component does not split upload manifests across messages.

**How to avoid:**
- Require sequential upload: the gallery upload UX must accept one file at a time (or in small batches of ≤3), not allow the user to select all photos simultaneously in a single file picker operation.
- Or: increase `MaximumReceiveMessageSize` to 64 MB but pair this with a per-upload cap (≤ 5 photos per recipe, ≤ 10 MB per photo) enforced before the stream is opened — never rely on the SignalR limit alone as the only size guard.
- Prefer sequential: it is simpler and mirrors the existing single-file upload UX without requiring `MaximumReceiveMessageSize` changes that increase DoS risk.

**Warning signs:**
- A `<InputFile multiple>` attribute is added without adjusting `MaximumReceiveMessageSize`.
- The upload UX allows selecting N files simultaneously with no count limit shown.
- UAT: select 5 large photos simultaneously and observe whether the SignalR circuit drops.

**Phase to address:**
The photo enhancement phase. The "sequential or small-batch" decision is a UX requirement that must be in the plan.

---

### Pitfall 15: Display-time service accidentally mutating CanonicalDocumentJson — violates the canonical-first invariant

**What goes wrong:**
The canonical-first invariant from v1.1/v1.3 is: UI surfaces consume `RecipeDocument` read-only; mutations happen only through `RecipeService.UpdateAsync` which writes back to `Recipe.CanonicalDocumentJson`. A new v1.4 display service (the Schema.org JSON-LD renderer, the nutrition panel, or the Cooklang exporter) that receives a `RecipeDocument` and also has access to a `Recipe` entity (through constructor injection, or because a Razor component passes both) can accidentally call `RecipeService.UpdateAsync` or directly set a property on the `Recipe` entity during rendering. Because `RecipeDocument` is a `sealed record`, property mutation requires `with` expressions — but the `Recipe.CanonicalDocumentJson` setter is not sealed.

**Why it happens:**
Display services that also need to "enrich" the view (e.g., "attach computed nutrition") are tempted to cache the enriched data by writing it back to the record. A developer writes: `recipe.CanonicalDocumentJson = JsonSerializer.Serialize(enrichedDoc)` inside a Blazor component's `OnInitializedAsync` to avoid recomputing nutrition on every render. This silently persists partially-computed data as canonical.

**How to avoid:**
- Nutrition enrichment data lives in a separate DB table (`RecipeNutrition`, keyed by `RecipeId`) — it is NOT written to `CanonicalDocumentJson`. The canonical doc never contains derived nutrition data.
- Schema.org JSON-LD and Cooklang export are stateless transformations: `SchemaOrgSerializer.Serialize(RecipeDocument)` → string. No entity access; no DB write.
- In code review: any new service that takes a `Recipe` entity as a dependency AND also calls any method on a mutating service (anything not `readonly` in `RecipeService`) is a red flag.
- Add a code convention note to `CONVENTIONS.md`: "Export and display services receive `RecipeDocument` (not `Recipe`) and are read-only. They never call `RecipeService.UpdateAsync`."

**Warning signs:**
- A new Razor component calls a service that takes `Recipe recipe` and also has `IRecipeService` injected.
- `CanonicalDocumentJson` is set anywhere outside of `RecipeService.CreateAsync` or `RecipeService.UpdateAsync`.
- Nutrition values appear in `Recipe.CanonicalDocumentJson` on inspection.

**Phase to address:**
Cross-cutting — mention in every phase plan that introduces a new display or export service.

---

### Pitfall 16: Scope creep across four concurrent themes causes interdependency chaos

**What goes wrong:**
Four themes (richer format, export, nutrition, photos) have cross-cutting dependencies. The schema bump (v4) must land before nutrition can store results against the new fields. Photos must land before Schema.org can emit a valid `image` field. If phases overlap or are planned as "do all at once", a bug in nutrition blocks the photo gallery, a bug in the schema bump blocks the Cooklang exporter, and the UAT harness runs against a partially-deployed state where some features are half-done.

**Why it happens:**
Milestone planning at the theme level feels efficient. Execution at the theme level produces half-done features that cannot be independently tested.

**How to avoid:**
- Phase ordering must respect the dependency chain: schema bump (v4) → richer-format fields in parser/validator → AI prompt update → nutrition (reads v4 fields) → photo gallery → Schema.org/Cooklang (reads v4 + photos).
- Each phase is independently deployable and UAT-verifiable before the next begins.
- The v4 schema bump phase must be the first phase, with green unit tests and a passing prompt snapshot before any other theme's code is written.

**Warning signs:**
- A plan file touches `Migration_V3_To_V4` AND `FdcNutritionService` in the same plan.
- The phase plan for Cooklang export assumes `RecipeDocument.Substitutions` is already available without explicitly depending on the v4 schema bump phase.

**Phase to address:**
Roadmap design phase — the dependency chain must be explicit in the roadmap before phase planning begins.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Inline FDC match without cache | Simpler first pass | Rate limit exhaustion at 70+ recipes; stale results after FDC updates | Never — cache is a day-one requirement |
| Store nutrition in CanonicalDocumentJson | No new table needed | Violates canonical-first invariant; pollutes the schema; round-trips derived data | Never |
| Use FDC Branded Foods without filtering | Wider food coverage | Brand-specific serving sizes pollute calculations; misleading precision | Never for default match; only after user-confirmed match |
| Cooklang import (not just export) | Feature parity with Cooklang ecosystem | Complex bidirectional mapping; format differences cause data loss on re-import | Defer to v1.5+ |
| Schema.org on every recipe page regardless of photo | "More SEO" | Invalid `image` field disqualifies the recipe from rich results | Only emit JSON-LD when image is an absolute HTTPS URL |
| AI provides photo URL directly | Feels magical | Hallucinated URLs, copyright risk, wrong food | Never — suggest search terms only |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| USDA FDC `/foods/search` | Accept top result without checking data type | Filter to `Foundation Foods,SR Legacy`; check `dataType` field in response |
| USDA FDC nutrition values | Use `value` field directly without checking `unitName` | Confirm `unitName == "KCAL"` / `"G"` before storing; do not assume units |
| USDA FDC serving size | Use `servingSize` for volume→mass conversion blindly | Check `servingSizeUnit`; prefer `foodPortions` with `gramWeight` for household measures |
| Schema.org `recipeInstructions` | Emit raw step text with `[name](#id)` links | Strip ingredient-link syntax; emit plain-text step instructions |
| Schema.org `totalTime` | Omit it when only `cookTime` or `prepTime` is set | Compute `totalTime = prepTime + cookTime` when both are set; emit all three |
| Cooklang `@ingredient` | Emit multi-word ingredients without `{}` | Multi-word ingredients require `@long ingredient name{}` — single-word can be bare `@word` |
| Cooklang section `==heading==` | Map v4 `SectionStep.Heading` directly | Check heading does not contain `=` characters; sanitize |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| FDC API called per-ingredient on every recipe view | Slow page load; rate limit exhaustion | Cache in DB keyed by `(normalizedName, fdcFoodId)` with TTL | Immediately at ~15 ingredients/recipe × multiple views |
| Gallery photo count unbounded | Disk fills in Docker volume; slow recipe-view load | Enforce ≤5 photos per recipe; show count in editor | At ~20 photos per recipe in a shared cookbook |
| Backfill all existing recipes with nutrition on deploy | Startup blocks; rate limit exhausted in one migration | Make backfill opt-in per-recipe; never auto-backfill | Any deployment with >10 existing recipes |
| JSON-LD rendered server-side on every request without caching | Unnecessary serialization work per page load | Cache the JSON-LD string alongside the canonical doc; invalidate on recipe save | Negligible at current scale — low priority |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| FDC API key committed to `appsettings.json` | Key deactivated by USDA on discovery (per terms); app stops fetching nutrition | Load from environment variable `FDC_API_KEY`; add `.gitignore` guard; document in README |
| AI-generated photo URL persisted without fetch-validation | Malicious URL in shared cookbook exposes users to SSRF or invalid content | HEAD-validate before persisting; existing `RecipePhotoUrlValidator` scheme-allowlist covers XSS; extend to validate resolution |
| Schema.org JSON-LD containing user-authored recipe name without HTML encoding | Unlikely XSS vector (JSON-LD is not HTML-parsed by browsers) | Use `System.Text.Json` serializer for the entire JSON-LD block — it escapes problematic characters automatically; never use string interpolation to build JSON-LD |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Nutrition panel shows zero for unmatched ingredients | User thinks zero-calorie; underestimates meal | Show "--" (dash) for unmatched; "≈" prefix for low-confidence matches |
| Cooklang download has no format warning | User expects re-import; data edit lost | Label "Export only (one-way)" and add tooltip explaining what is lost |
| Photo gallery order not persisted | User reorders photos; order resets on page reload | Store display order explicitly (e.g., `displayOrder int` column); not implicit by insert order |
| "Find me a photo" AI button with no disclaimer | User assumes AI photos are royalty-free | Button label: "Suggest search terms" not "Find photo"; disclaimer before any AI photo feature |

---

## "Looks Done But Isn't" Checklist

- [ ] **Upcaster v3→v4:** Does `RecipeUpcasterChain.CurrentVersion` = 4? Is `Migration_V3_To_V4` registered in DI? Does the fixture matrix include a v3 doc with all new fields absent?
- [ ] **AI schema:** Does the prompt snapshot test pass after adding new fields to `RecipeDocument`? Does a generated recipe include the new fields (even if null) in the structured output?
- [ ] **Nutrition FDC match:** Is the matched food description visible to the user? Is the match stored with its FDC food ID (not just the computed values)?
- [ ] **Nutrition density:** Does "1 cup all-purpose flour" compute to approximately 455 kcal (not 900 kcal)? Is the density table covered by unit tests?
- [ ] **FDC caching:** Does a second lookup for the same ingredient name skip the API call and use the cache?
- [ ] **Schema.org:** Does validation with Google Rich Results Test pass for a recipe that has `name`, `image` (absolute URL), `cookTime` (ISO 8601), and `recipeInstructions` (plain text)?
- [ ] **Cooklang:** Does a recipe with `@`, `#`, `~` in step text produce a parseable `.cook` file?
- [ ] **Photo orphan cleanup:** After deleting a recipe with gallery photos, are the local files removed from `wwwroot/uploads/`?
- [ ] **Multi-upload:** Uploading 3 photos simultaneously does not drop the SignalR circuit?
- [ ] **Canonical invariant:** Does `RecipeDocument` deserialized from `CanonicalDocumentJson` after save contain no nutrition or JSON-LD data?

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Wrong FDC match persisted in nutrition cache | LOW | Expose "re-match" action per ingredient; clear cache row and re-search |
| Upcaster gap missed in DI registration | LOW | Add the registration + restart; no data loss (upcasters run on read, not on save) |
| Orphaned photo files accumulated | LOW | Run the cleanup background pass; it is non-destructive (checks DB before deleting) |
| Nutrition stored in CanonicalDocumentJson | HIGH | Requires a data migration to strip nutrition keys from all canonical docs + schema revert |
| Hallucinated AI photo URL persisted in shared cookbook | MEDIUM | Add admin "clear photo" action; notify affected users if cookbook is shared |
| Cooklang with `@`/`#`/`~` chars exported incorrectly | LOW | Fix the sanitizer; re-export is the user action (no DB data is corrupted) |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Upcaster DI gap (P1) | Schema bump phase (first phase) | Gap-detection test + DI registration test pass |
| Bundle-throw upcaster (P2) | Schema bump phase | Fixture matrix covers partial-field v3 docs |
| AI schema drift (P3) | Schema bump phase | Prompt snapshot test updated and passing |
| FDC wrong food match (P4) | Nutrition phase | Match-review UI visible in UAT; FDC food ID stored in DB |
| Density conversion error (P5) | Nutrition phase | Density table unit test covers 20 common ingredients |
| Nutrition disclaimer missing (P6) | Nutrition phase | "Estimate" label and FDC attribution in every nutrition panel |
| FDC API blocking save (P7) | Nutrition phase | Nutrition is post-save; FDC outage does not block recipe save |
| Schema.org relative image URL (P8) | Schema.org export phase | JSON-LD omits `image` when `PhotoUrl` is a relative path |
| ISO 8601 duration format (P9) | Schema.org export phase | Unit test for `IsoFormatDuration` boundary cases |
| Cooklang round-trip implication (P10) | Cooklang export phase | "Export only" label present in UAT |
| Cooklang special chars (P11) | Cooklang export phase | Test with `@` `#` `~` in step text produces valid `.cook` output |
| AI photo hallucination/copyright (P12) | Photo enhancement phase | No AI-provides-URL flow; search-term-only mode implemented |
| Photo orphan files (P13) | Photo enhancement phase | UAT: delete recipe; verify `wwwroot/uploads/` file removed |
| Multi-upload SignalR limit (P14) | Photo enhancement phase | UAT: upload 3+ photos; circuit remains connected |
| Canonical mutation in display service (P15) | Every phase — code review gate | `CanonicalDocumentJson` never set outside `RecipeService` |
| Scope creep and interdependencies (P16) | Roadmap phase | Dependency chain explicit; each phase independently deployable |

---

## Sources

- USDA FoodData Central API Guide: https://fdc.nal.usda.gov/api-guide/ (rate limits: 1,000/hour/IP; CC0 license; citation requested)
- Google Search Central — Recipe structured data: https://developers.google.com/search/docs/appearance/structured-data/recipe
- Cooklang specification: https://cooklang.org/docs/spec/
- Cooklang spec GitHub discussion #46 (metadata limitations): https://github.com/cooklang/spec/discussions/46
- dotnet/aspnetcore issue #42993 — InputFile upload manifest vs SignalR MaximumReceiveMessageSize: https://github.com/dotnet/aspnetcore/issues/42993
- Existing codebase: `Migration_V2_To_V3.cs` PITFALLS C7 / M2 comments (per-field independence guard pattern)
- Existing codebase: `RecipeJsonSchemaProvider.cs` (Anthropic strict-mode anyOf externalization, ForbiddenInAnyOfBranch)
- Existing codebase: `LocalRecipePhotoStorage.cs` (PITFALL H1 / H2 / H3 — magic-byte sniff, path-traversal guard)
- Existing codebase: `AiRecipeGenerator.cs` (PITFALL H9 — telemetry write site pattern)
- Existing codebase: `.planning/PROJECT.md` — canonical-first invariant, trusted-LAN posture, no-Newtonsoft/no-MudBlazor constraints
- FAO/INFOODS Guidelines for Converting Units (density conversion references): https://www.fao.org/fileadmin/templates/food_composition/documents/1nutrition/Conversion_Guidelines-V1.0.pdf

---
*Pitfalls research for: v1.4 Recipe Data & Interoperability — schema bump, USDA nutrition, Schema.org/Cooklang export, photo gallery*
*Researched: 2026-06-05*
