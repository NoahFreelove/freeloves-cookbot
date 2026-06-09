# Research Summary

**Project:** FreelovesCookBot v1.4 — Recipe Data & Interoperability
**Domain:** Recipe schema evolution, export interoperability (Schema.org JSON-LD + Cooklang), USDA nutrition pipeline, photo gallery
**Researched:** 2026-06-05
**Confidence:** HIGH

---

## Executive Summary

v1.4 is a purely additive milestone layered on top of the stable v1.3 canonical `RecipeDocument` platform. The research converged on a single non-negotiable structural constraint: **the v3→v4 schema bump must land first and stand alone as Phase 12** before any other theme writes a line of code. Every downstream feature — the export projectors, the photo gallery migration, and the nutrition pipeline — reads from `RecipeDocument` v4. Building any of them against v3 and then re-patching for v4 fields is avoidable rework. This dependency is explicit and unambiguous across all four research files.

The cross-cutting package constraint is equally firm: **zero new NuGet packages**. All five sub-themes are achievable on the existing dependency set. Hand-rolling `SchemaOrgRecipeSerializer` (~80 lines of System.Text.Json) and `CooklangExporter` (~150 lines) beats every library candidate reviewed — no GPL-compatible, STJ-native, actively-maintained Schema.org library exists; `CookLangNet` is parser-only, unmaintained since 2023, and cannot write `.cook` files. The USDA FDC has no .NET client library at all; `FdcClient` follows the `AnthropicAiService` HttpClient pattern exactly. The photo gallery extends the existing `LocalRecipePhotoStorage` pattern; vision AI extends the existing `AnthropicAiService` with an image-content overload.

The dominant risks are in Theme 4 (Nutrition) and are well-defined: USDA fuzzy-match returns the wrong food silently, volume→mass conversion uses a generic water density instead of ingredient-specific density, and FDC API calls are made synchronously on the recipe-save path. The nutrition theme also carries a mandatory disclaimer requirement — the panel must label results as estimates and attribute USDA FoodData Central. All three risks have clear, concrete preventions that must be acceptance criteria in the nutrition phase plan, not afterthoughts.

---

## Key Findings

### Recommended Stack

No new packages required for v1.4. All five themes extend existing infrastructure by adding ~80–200 lines of hand-rolled code per theme, following patterns already in the codebase.

**Technology decisions per theme:**

- **v4 schema bump:** Pure POCO + upcaster (`Migration_V3_To_V4`), following the identical v2→v3 pattern. `RecipeJsonSchemaProvider` auto-reflects the updated POCO via `JsonSchemaExporter` — no provider code change needed.
- **Schema.org JSON-LD:** `System.Text.Json` write path; new `JsonLdRecipeProjector` in `CookBot.Application/Recipes/`. ~80 lines. Injected into `RecipeView.razor` via `<HeadContent>` for server-rendered SEO.
- **Cooklang export:** Hand-rolled `CooklangRecipeProjector` in `CookBot.Application/Recipes/`. ~150 lines. `YamlDotNet` (already a dep) handles YAML frontmatter.
- **USDA FDC nutrition:** Named `HttpClient` via `IHttpClientFactory` in `CookBot.Infrastructure/Nutrition/FdcClient.cs`. Mirrors `AnthropicAiService` exactly. Key stored in `CookBotSettings.FdcApiKey` (nullable; null = graceful no-op). SR Legacy + Foundation Foods CSVs seeded into SQLite at startup for full offline nutrition — no live API calls for the ~8,600 Foundation + SR Legacy foods that cover essentially all recipe staples.
- **Photo gallery:** Extend `LocalRecipePhotoStorage` with a multi-file loop. New `RecipePhoto` EF entity table (see Architecture section for the entity-vs-canonical-array decision). New `RecipePhotoService` in Application.
- **AI photo assist:** Extend `AnthropicAiService` with a `SendWithImageAsync` overload. No new client; no new interface.

**Version compatibility:** EF Core migrations only. No package version changes. New migrations: `AddRecipePhotosTable`, `AddNutritionCacheTables`.

### Expected Features

**Must have (table stakes for v1.4):**

- `RecipeDocument` v4 fields: `Equipment []EquipmentEntry`, `RecipeProvenance?` (SourceUrl, OriginalAuthor, SourceName, AdaptedDate), per-ingredient `Substitutions []IngredientSubstitution`, per-step `DonenessCue string?`
- v3→v4 upcaster with per-field independent null-guards (never bundle-throw)
- AI prompt schema update and passing prompt-snapshot test
- Schema.org JSON-LD `<script>` block in RecipeView `<head>`: `name`, `image` (absolute URLs only), `recipeIngredient`, `recipeInstructions` as `HowToStep`, `prepTime`/`cookTime`/`totalTime` as ISO 8601 duration, `recipeYield`, `author`, `keywords`, `datePublished`
- Cooklang `.cook` download with correct `@ingredient{amount%unit}` tokens, `~{timer%unit}`, `==Section==`, YAML frontmatter, "Export only (one-way)" label
- `RecipePhoto` entity table + EF migration (backfills `Recipe.PhotoUrl` → primary photo row); multi-upload UI; gallery in RecipeView; hero designation
- Foundation Foods + SR Legacy CSVs seeded as SQLite lookup; `NutritionService` with ingredient name normalization + two-level cache (`FdcLookupCache` + `RecipeNutritionCache`); per-serving/total display toggle; "Estimated nutrition" disclaimer + FDC attribution

**Should have (differentiators within v1.4):**

- Schema.org `nutrition.calories` emitted once Theme 4 data exists (gated dependency)
- Equipment checklist pre-cook modal in Cooking Mode
- `DonenessCue` surfaced as highlighted callout in Cooking Mode (below timer)
- AI-assisted substitution generation (extend AI prompt; structured output already handles arrays)
- "Suggest search terms" AI feature for photos (Claude describes the dish visually; user pastes their own URL — no AI URL generation, no hallucination risk)
- Per-ingredient unmatched indicator ("--" for unmatched, not zero; "≈" for low-confidence)
- `RecipePhoto.DisplayOrder` with drag-reorder in RecipeEditor

**Defer to v1.4.x or v1.5+:**

- Per-step photo linking (HIGH complexity)
- Unsplash API bulk photo backfill (external API dependency; out-of-scope for trusted-LAN posture)
- Cooklang import / round-trip (large scope; no user demand evidence yet)
- Nutrition micronutrients beyond calorie/protein/fat/carbs
- AI-assisted unmatched ingredient resolution via Claude
- `recipeCategory` + `recipeCuisine` in Schema.org (requires new v4 fields + editor UI; v4.1 candidate)
- Manual calorie override per ingredient

**Anti-features — never do:**

- AI provides a photo URL directly (hallucination + copyright risk; suggest search terms only)
- Nutrition stored inside `CanonicalDocumentJson` (violates canonical-first invariant; AI must never emit nutrition)
- Photo paths stored in `CanonicalDocumentJson` (host-specific operational state; breaks on cookbook export/import)
- `aggregateRating` in Schema.org (no rating system; fabricating it violates Google policy)

### Architecture Approach

v1.4 extends the four-layer Clean/Onion architecture without adding a new project. New components follow established layer assignments: new Domain POCOs (`IngredientSubstitution`, `EquipmentEntry`, `RecipeProvenance`, `RecipePhoto`, `FdcLookupCache`, `RecipeNutritionCache`), new Application services (`JsonLdRecipeProjector`, `CooklangRecipeProjector`, `NutritionService`, `RecipePhotoService`), new Infrastructure adapters (`FdcClient`), and modified Web surfaces (`RecipeView.razor`, `RecipeEditor.razor`). The canonical-first / display-only invariant applies to every new service: projectors receive `RecipeDocument` and return a string; they never touch `Recipe.CanonicalDocumentJson`.

**Major new components:**

1. `Migration_V3_To_V4` (Application) — per-field independent null-guards; stamps `version: 4`; registered in DI alongside existing upcasters
2. `JsonLdRecipeProjector` (Application) — `Project(RecipeDocument, canonicalPageUrl) → string`; `IsoFormatDuration` helper; omits `image` when URL is not absolute HTTPS
3. `CooklangRecipeProjector` (Application) — `Project(RecipeDocument) → string`; resolves `[name](#id)` chips to `@name{amount%unit}`; sanitizes bare `@`, `#`, `~` in step text
4. `NutritionService` (Application) — orchestrates `FdcClient`, `UnitConversionService` (extended with density table), two-level SQLite cache; post-save enrichment only
5. `FdcClient` (Infrastructure) — named `HttpClient` via `IHttpClientFactory`; Foundation + SR Legacy dataType filter; graceful no-op when `FdcApiKey` is null
6. `RecipePhotoService` (Application) — full file lifecycle (delete cleans up `wwwroot/uploads/`); syncs `Recipe.PhotoUrl` to hero on every mutation

**Resolved architecture divergence — photos entity vs. canonical array:**

STACK.md favored `IReadOnlyList<string> Photos` in `RecipeDocument`. ARCHITECTURE.md recommended a `RecipePhoto` EF entity table. **The entity table is the correct choice** for this codebase: photo paths are host-specific operational state, not recipe format data. They must not travel in `.cookbook.json` exports. They must not be emitted by or fed to the AI. The `Recipe.PhotoUrl` precedent is already an EF column bridge set because photo display is a UI concern separate from the format — multiple photos follow the same reasoning.

### Critical Pitfalls

1. **USDA wrong-food match — silently wrong nutrition (Pitfall 4).** Filter to `dataType=Foundation Foods,SR Legacy` only. Store matched FDC food ID + description alongside every computed value. Show matched food names to the user. Show "--" for unmatched, never zero. Implement a confidence threshold.

2. **Volume→mass density error — wrong density doubles calorie count (Pitfall 5).** Build a density lookup table for ~50 common cooking ingredients in `UnitConversionService`. Never fall back to water density (1 g/mL). Prefer FDC `foodPortions.gramWeight` for household measures where available.

3. **FDC API call on save path — FDC outage loses recipe edit (Pitfall 7).** Nutrition is post-save enrichment triggered by explicit user action only, never blocking `RecipeService.CreateAsync`/`UpdateAsync`.

4. **Nutrition disclaimer missing — user treats panel as medical authority (Pitfall 6).** Every nutrition panel must show "Estimates based on USDA FoodData Central. Results are approximate and not suitable for medical dietary planning." Heading must say "Estimated nutrition," not "Calories."

5. **Schema.org relative image URL + ISO 8601 duration format (Pitfalls 8 + 9).** Omit `image` entirely when `PhotoUrl` is relative. All time fields must use `IsoFormatDuration` helper producing `"PT30M"` / `"PT1H30M"` format.

6. **Bundle-throw in Migration_V3_To_V4 (Pitfall 2).** Four new field groups = four independent null-guards. Follow the v2→v3 pattern exactly.

7. **Upcaster DI registration gap (Pitfall 1).** DI registration and gap-detection test in the same Phase 12 plan as the migration class.

8. **AI photo hallucination / copyright (Pitfall 12).** "Suggest search terms" only. No AI-provided URLs persisted. Copyright disclaimer on every photo input surface.

9. **Cooklang one-way label + special-character sanitization (Pitfalls 10 + 11).** Every Cooklang download affordance labeled "Export only (one-way)." Bare `@`, `#`, `~` sanitized before emission.

10. **Canonical invariant violation in display services (Pitfall 15).** Code-review gate on every new v1.4 service: projectors receive `RecipeDocument`, not `Recipe`; they never call `RecipeService.UpdateAsync`; `CanonicalDocumentJson` set only in `RecipeService`.

---

## Implications for Roadmap

### Phase 12: v3→v4 Schema Bump + Richer Format Fields

**Rationale:** All downstream themes depend on stable `RecipeDocument` v4. This theme follows a proven pattern (identical to v2→v3) and requires no external dependencies. Must be green and merged before Phase 13 begins.

**Delivers:** `RecipeDocument` v4 with `Equipment`, `RecipeProvenance`, per-ingredient `Substitutions`, per-step `DonenessCue`; `Migration_V3_To_V4` in DI with per-field guards; `RecipeUpcasterChain.CurrentVersion = 4`; updated AI prompt + passing prompt-snapshot test; `RecipeValidator` v4 warnings; unit-test fixture matrix covering partial-field v3 documents.

**Features:** FUTURE-03 (substitutions), FUTURE-04 (equipment), FUTURE-05 (doneness cues), FUTURE-06 (provenance)

**Pitfalls to gate:** P1 (DI registration gap), P2 (bundle-throw), P3 (AI schema drift)

**Research flag:** Standard pattern. Skip `--research-phase`.

---

### Phase 13: Export & Interoperability (Schema.org JSON-LD + Cooklang)

**Rationale:** Both are pure read-only projections from `RecipeDocument` with no EF schema changes. Cheapest themes after v4 is stable. Bundled efficiently — same architectural pattern, no cross-dependency between them.

**Delivers:** `JsonLdRecipeProjector` with `IsoFormatDuration` helper and absolute-URL guard on `image`; `<script type="application/ld+json">` in RecipeView `<HeadContent>`; `CooklangRecipeProjector` with chip-to-`@token` resolution, `~timer` emission, `==Section==` headers, YAML frontmatter, `@`/`#`/`~` sanitization; "Export as .cook" button with "Export only (one-way)" label.

**Features:** FUTURE-07 (Schema.org), FUTURE-11 (Cooklang)

**Pitfalls to gate:** P8 (relative image URL), P9 (ISO 8601 format), P10 (Cooklang round-trip implication), P11 (Cooklang special characters)

**Research flag:** Standard patterns. Skip `--research-phase`.

---

### Phase 14: Photo Gallery

**Rationale:** Independent of nutrition. `RecipePhoto` entity migration has no dependency on nutrition tables. Placing photos before nutrition lets UAT exercise the gallery before the more complex nutrition panel arrives. File lifecycle correctness (orphan cleanup) is the primary acceptance-criteria concern.

**Delivers:** `RecipePhoto` EF entity + migration (backfills `Recipe.PhotoUrl` → primary row); `RecipePhotoService` with full file lifecycle; sequential multi-upload UI in RecipeEditor (not simultaneous — avoids SignalR manifest size limit); gallery strip in RecipeView; hero designation + reorder; "Suggest search terms" AI photo assist (Claude text-only, no URL generation); `Recipe.PhotoUrl` kept as denormalized hero-URL sync target.

**Pitfalls to gate:** P12 (AI photo hallucination/copyright), P13 (orphaned files on delete), P14 (SignalR multi-upload limit), P15 (canonical invariant)

**Research flag:** Standard pattern. Skip `--research-phase`. Acceptance criteria must explicitly cover file deletion lifecycle.

---

### Phase 15: Nutrition (USDA FoodData Central)

**Rationale:** Most complex theme — new external HTTP dependency, two new cache tables, unit conversion extension, graceful degradation logic. Benefits from stable v4 schema. Isolated from photos and export concerns. The fuzzy-match strategy and match-review UX are non-optional acceptance criteria, not implementation details.

**Delivers:** SR Legacy + Foundation Foods CSVs seeded into SQLite at startup; `FdcClient` Infrastructure service (named `HttpClient`, graceful no-op when key absent); `NutritionService` with ingredient name normalization, two-level caching, density table for ~50 common ingredients; nutrition panel on RecipeView (post-save-only CTA, per-serving/total toggle, "--" for unmatched, "≈" for low-confidence, matched FDC food IDs visible to user, "Estimated nutrition" heading, mandatory disclaimer + FDC attribution, absent-key graceful message); `CookBotSettings.FdcApiKey` optional config field.

**Features:** FUTURE-08 (nutrition auto-compute)

**Pitfalls to gate:** P4 (wrong FDC food match), P5 (density conversion error), P6 (disclaimer missing), P7 (FDC API blocking save), P15 (canonical invariant)

**Research flag:** Needs careful phase planning. The match-review UX surface (showing users which FDC food was matched with its ID) is a non-optional feature. The density table source and ingredient scope must be named as acceptance criteria. The "post-save enrichment only" architectural decision must be stated in the plan before implementation begins.

---

### Phase 16: UAT + Integration

**Rationale:** Reuse the Playwright harness from Phase 11. Focuses on cross-theme integration scenarios: Schema.org `nutrition.calories` field appears only after Phase 15 data exists; gallery hero photo flows into Schema.org `image`; cookbook export/import round-trip with v4 documents.

**Delivers:** UAT run across all v1.4 themes; Google Rich Results Test validation; cross-theme integration scenarios; v1.4 milestone sign-off.

**Research flag:** Standard pattern. Skip `--research-phase`.

---

### Phase Ordering Rationale

The build order is driven by two hard constraints visible across all four research files:

1. **v4 schema first.** All export services and the photo gallery migration coordinate on stable `RecipeDocument` v4. The upcaster chain must be complete and tested before any consumer of v4 fields writes code.

2. **Nutrition last.** Most complex theme, most isolated. Placing it last gives the most time for the offline-seed approach to be validated and lets it build on a fully-stable schema. The architecture researcher was explicit: "benefits from stable RecipeDocument v4 to read from. Isolated from photo and export concerns."

**Where the researchers diverged — photos vs. nutrition ordering:**

FEATURES.md treated photos (Theme 5) and nutrition (Theme 4) as roughly co-equal. ARCHITECTURE.md explicitly recommended Phase 14 Photos before Phase 15 Nutrition because the `RecipePhoto` table migration is simpler and the gallery UI can be UAT'd independently before the complex nutrition panel arrives. This summary adopts the ARCHITECTURE.md ordering. If timeline pressure requires parallelism, photos and nutrition can be planned concurrently but must be executed sequentially after the v4 schema lands.

### Research Flags

**Needs careful phase planning attention (not more research, but sharp acceptance criteria):**
- Phase 15 (Nutrition): fuzzy-match strategy, density table source and scope, match-review UX, "post-save enrichment only" gate — all must be acceptance criteria in the phase plan, not left to implementation discretion.

**Standard patterns — skip `/gsd:plan-phase --research-phase`:**
- Phase 12 (v4 schema bump): v2→v3 precedent; pitfall checklist covers the risks
- Phase 13 (exports): official specs are complete and verified; both projectors follow existing Application-layer pattern
- Phase 14 (photos): extends existing `LocalRecipePhotoStorage` pattern
- Phase 16 (UAT): reuse Phase 11 harness

---

## Open Questions for Requirements Definition

The researchers flagged four unresolved decisions the user must make before or during requirements authoring:

**1. FDC API key: host-global vs. per-user**

STACK.md placed `FdcApiKey` in `CookBotSettings` (global, same pattern as `AiFeaturesEnabled`). For a trusted-LAN self-hosted app where all users share the same USDA data source, a single host-global key is almost certainly correct. The per-user AI key model exists because individual users pay for their own Anthropic usage; FDC is free and rate-limited per IP, not per user. Recommend: host-global. The user should confirm explicitly.

**2. Density table source and coverage scope**

PITFALLS requires a density lookup table for ~50 common cooking ingredients to avoid the water-density error. STACK.md suggested ~20 as minimum. Neither file named the authoritative source (USDA ARS measurement conversion tables, King Arthur Flour, FAO/INFOODS). The phase plan must name the source and enumerate the ingredients covered — this is a verifiable acceptance criterion.

**3. `RecipeCategory` / `RecipeCuisine` as v4 fields vs. derived from tags**

Schema.org's `recipeCategory` and `recipeCuisine` have no equivalent in v3 or v4 as currently designed. Option (a): add `Category` and `Cuisine` string fields to v4 with editor UI (adds scope to Phase 12). Option (b): derive from `RecipeTags` at projection time (no new fields, less precise). Option (b) is the lower-risk path for v1.4; option (a) defers to v4.1. The user should decide before Phase 12 requirements are authored.

**4. `RecipePhoto` entity table confirmed as architecture decision**

STACK.md favored `IReadOnlyList<string> Photos` in `RecipeDocument`. ARCHITECTURE.md recommended a `RecipePhoto` EF entity table. This summary recommends the entity table. The user should explicitly confirm before Phase 14 is planned, as it determines whether an EF migration is needed and whether photo paths are stripped from `.cookbook.json` exports.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All five themes verified against official docs; zero-new-packages conclusion firm across all four research files |
| Features | HIGH | Google Rich Results spec current (Dec 2025 verified); Cooklang spec canonical; USDA FDC API guide verified with live call; feature scope derived from official specs not community inference |
| Architecture | HIGH | Direct codebase read; all new components follow established patterns; one divergence (photos entity vs. array) resolved with clear rationale grounded in codebase evidence |
| Pitfalls | HIGH (schema/photos), MEDIUM (nutrition matching) | Schema bump and photo pitfalls grounded in codebase evidence and codebase precedents; FDC ingredient matching accuracy is inherently non-deterministic — graceful degradation and match-review UX are the mitigations, not algorithmic confidence |

**Overall confidence:** HIGH for architecture decisions and build order. MEDIUM for nutrition matching accuracy in practice — the fuzzy-match problem has no deterministic solution; the density table quality and normalization coverage are only fully known at implementation time.

### Gaps to Address

- **Ingredient name normalization strategy:** Which adjectives/modifiers to strip ("room-temperature", "good", "fresh", "packed") before FDC search is implementation-defined. A deny-list approach is recommended but the exact list needs authoring during Phase 15 planning.
- **FDC offline seed data freshness policy:** SR Legacy is a 2018 final release. Foundation Foods refreshes bi-annually. The seed data will drift from the live API over time. A "refresh seed data" admin action is a reasonable v1.5 scope item; v1.4 ships the seed data current at release date. This should be documented in the v1.4 README.
- **Photo count cap as a named constant:** FEATURES.md recommended "≤10 photos per recipe" as a default cap. The exact limit should be a named constant in `CookBotSettings` or at the service layer, not hardcoded in the UI validator. Define it in Phase 14 requirements.
- **`.cookbook.json` export behavior with gallery photos:** Photo paths (`/uploads/{guid}.jpg`) are host-specific and will 404 on the recipient's instance. The export format should either omit photo rows or include an explicit note. Resolve in Phase 14 planning.

---

## Sources

### Primary (HIGH confidence)
- Google Search Central — Recipe structured data (Dec 2025 update): https://developers.google.com/search/docs/appearance/structured-data/recipe
- Cooklang specification: https://cooklang.org/docs/spec/
- USDA FoodData Central API Guide: https://fdc.nal.usda.gov/api-guide/
- USDA FDC Download Datasets + Foundation Foods documentation: https://fdc.nal.usda.gov/download-datasets/
- USDA FDC OpenAPI (live DEMO_KEY call — nutrient IDs 1003/1004/1005/1008 confirmed in response body)
- Anthropic Vision documentation: https://platform.claude.com/docs/en/build-with-claude/vision
- Existing codebase (direct read): `RecipeDocument.cs`, `Migration_V2_To_V3.cs`, `RecipeUpcasterChain.cs`, `LocalRecipePhotoStorage.cs`, `AnthropicAiService.cs`, `RecipeJsonSchemaProvider.cs`, `.planning/PROJECT.md`, `.planning/codebase/ARCHITECTURE.md`

### Secondary (MEDIUM confidence)
- CookLangNet NuGet (v0.4.0, 2023-05-21, ~9k downloads): unmaintained, parser-only confirmed
- Mealie GitHub discussions (#694, #2264, #4311) — ingredient substitution demand evidence
- Paprika app documentation — multiple photos, drag-reorder, per-step photo patterns
- FAO/INFOODS Guidelines for Converting Units — density conversion references
- dotnet/aspnetcore issue #42993 — InputFile + SignalR MaximumReceiveMessageSize multi-file behavior

---

*Research completed: 2026-06-05*
*Ready for roadmap: yes*
