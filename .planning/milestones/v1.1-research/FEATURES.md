# Feature Landscape

**Domain:** Self-hosted recipe authoring + AI-assisted cooking app (FreelovesCookBot)
**Researched:** 2026-04-25
**Milestone scope:** Subsequent milestone — recipe-mode UX without special syntax, canonical versioned format, AI conformance to format, format-driven new features
**Overall confidence:** HIGH for ecosystem patterns and Anthropic capabilities (verified against official docs); MEDIUM for product-specific implementation details

This document maps the feature landscape relevant to the four locked milestone goals (PROJECT.md → Active). It does **not** re-research already-shipped features (recipe authoring, cooking mode, scaling, cookbook export/PDF, pantry, AI chat, prompt builder, key sharing). Categories below answer "what is the minimum to credibly hit this milestone, what would make us stand out, and what should we deliberately not chase."

---

## Table Stakes

Features the milestone goal collapses without. Each is mandatory for "AI conformance + canonical format + intuitive UX" to mean anything beyond marketing copy.

| Feature | Why It's Table Stakes | Complexity | Depends On Format First? |
|---------|----------------------|------------|------------------------|
| **Single canonical recipe schema as source of truth** (one C# record graph; YAML wire format, JSON export DTO, and DB shape all generated from / map onto it) | The current 3-shape divergence (Concerns 1–4) is the proximate cause of every other pain point — AI is taught one shape, export uses another, DB stores a third. Until this is one schema, everything else is patching symptoms. | **L** | — (this IS the format work) |
| **Explicit `version` (or `schemaVersion`) field on the canonical recipe** — semver-style integer at the top of the document, parser dispatches on it | Industry standard practice (Confluent, JSON Schema community, Protobuf). Without it there is no safe forward-compatible evolution path; today only the JSON envelope has `SchemaVersion = 1` and the YAML has nothing. Required before any new field lands. | S | — |
| **Migration / upcaster pipeline** — `RecipeUpcaster.Upcast(json, fromVersion) → currentVersion`. Old `.cookbook.json` exports remain importable forever (PROJECT.md constraint). | Without this, every schema change is a breaking change for users who have files on disk. The pattern is well-established (event-sourcing upcasters, Liquibase-style migrations). | M | YES |
| **Ingredient-chip insertion in step editor** — clicking an ingredient (or typing `@` / `/ingredient`) inserts a chip representing the `[name](#id)` link; user never types markdown brackets | Every modern recipe app surveyed solves this with a UI affordance: Mealie has structured ingredient sections with click-to-link; Tandoor has an "interaction menu" that copies a reference; Cooklang has dedicated `@ingredient` syntax that authoring tools render as chips. The current `MudTextField Lines="3"` plain textarea is the entire user-flagged "special syntax" pain (Concern 5). | M | Partial — chips can render today's `[name](#id)` immediately; format work refines the underlying token but UI ships independently |
| **Section vs. step disambiguation in editor** — a step is a clearly chosen "Step" or "Section header" (radio/toggle), not a YAML key the user has to know about | Concern 6: today both `text:` and `section:` can coexist and the parser silently picks one. AI sometimes emits both. Editor must make this a single choice with no ambiguity, then the format follows. | S | NO |
| **AI output validated against the canonical schema** — every AI-emitted recipe runs through the parser and either succeeds or triggers repair | Concerns 9, 11. Today three increasingly loose extractors run, the third can swallow prose into the recipe body. Without strict validation the "AI uses the format" goal is empty. | S | YES |
| **Anthropic structured outputs for recipe emission** — use `output_config.format` with `type: "json_schema"` and the canonical recipe JSON Schema; Claude is grammar-constrained at the token level so output is *guaranteed* valid against the schema (HIGH confidence: Anthropic Sonnet 4.5+, Opus 4.5+, Haiku 4.5 all support it as GA, including streaming, per `platform.claude.com/docs/en/build-with-claude/structured-outputs`). The opt-out clause and three-extractor fallback in `AiChat.ExtractRecipeContent` go away. | This is the single highest-leverage change in the entire milestone. The current `CuratedModels` list (Sonnet 4.6 / Opus 4.7 / Haiku 4.5) all support it. Industry consensus across multiple sources: tool_use with `strict: true` or native `output_config.format` give ~99.5% valid output, vs. retry-prompted JSON which sits ~85–95%. | M | YES (need the schema first) |
| **Strict-mode system prompt without opt-out** — remove the "plain numbered steps are fine" clause (Concern 10) and replace with "you must emit recipes via the structured-output schema; if you cannot, ask the user a clarifying question instead" | The opt-out clause is the second-biggest reason AI recipes don't round-trip cleanly. Once structured outputs are wired, the prompt should reinforce, not contradict, the constraint. | S | NO (can change today) |
| **Single source of truth for the format spec** — Concern 13. Today `PromptBuilderService.ResolveRecipeFormat()` (lines 168–201) and `BuildCopyablePrompt(...)` (lines 262–296) each contain a hand-written copy of the format example. One C# constant feeds both, plus parser-error help text and developer docs. | If the spec is written twice, it will drift. Drift is what produced the current concern in the first place. Cheap to fix. | S | NO |
| **Repair pass when AI output is unparseable (fallback for non-structured callers)** — when paste-raw-text or an external-LLM-prompt-builder user pastes a malformed recipe, the app re-prompts with the parser error message and asks the model (or surfaces an editable text area) to fix it | Concern 11. With native structured outputs the in-app chat path becomes "always valid", but the paste-raw-text and prompt-builder paths still receive arbitrary text. The Self-Refine / Reflection pattern (HIGH confidence — see Madaan et al. 2023, widely deployed) recovers up to 90% of failed batches in production reports. | M | YES (need parser errors as structured output) |
| **Tests for `ExtractRecipeContent` and the schema upcaster** | Concerns 34, 36. The most heuristic, format-fragile method has zero coverage. Adding the new schema without tests guarantees regression. | S | YES |

---

## Differentiators

Features that would set FreelovesCookBot apart from the surveyed ecosystem (Paprika, Mealie, Tandoor, AnyList, Whisk, NYT Cooking, KitchenPal, Cooklist, NoWaste.ai). Pick **one or two**, not all — the milestone is about format consolidation first.

| Feature | Value Proposition | Complexity | Depends On Format First? |
|---------|-------------------|------------|------------------------|
| **Per-step temperature with unit + scaling-aware "do not scale" badge** — adds `temperature: { value: 350, unit: F }` to step schema; cooking mode shows it as a chip alongside timer chips; UI explicitly shows "350°F (not scaled)" if servings change. Cooklang treats temperatures as first-class via the `~` time / cookware syntax; Paprika auto-detects them; Tandoor surfaces them in its templating. None of them solve the "shouldn't scale linearly" problem (verified — Newton's Law of Cooling, larger volumes need lower temps + longer times, MEDIUM confidence per kitchen science sources). | A small but substantive evolution of step semantics that *exercises versioning* (PROJECT.md goal #4) and addresses Concern 8. The "honest about non-scaling" UI is actually rare in the surveyed apps and is a real differentiator. | M | YES (new field) |
| **Ingredient substitutions baked into the format** — add `substitutions: [{ for: "buttermilk", use: "1 cup milk + 1 tbsp lemon juice", note: "let stand 5 min" }]` at the recipe level OR per-ingredient; AI chat suggests context-aware swaps. KitchenPal, NoWaste.ai, and Mr. Cook all surface substitutions but as separate lookup features, not as a structured field that travels with the recipe. | Differentiator because most apps bolt this on as an external lookup. Embedding it in the canonical format means it round-trips through export/import and the AI can be taught to author and respect it. Strong "format-driven new feature" candidate. | M | YES |
| **Equipment list as first-class field** — `equipment: ["dutch oven", "instant-read thermometer"]` at recipe level, optional per-step `usesEquipment: ["dutch oven"]`. Cooklang has it (`#cookware` syntax). Tandoor doesn't. Mealie doesn't. None of the consumer apps surveyed make this prominent. UserProfile already has an "equipment" token in the system prompt, so the AI can match recipes to what the user owns. | Plays directly to the existing pantry + profile-equipment infrastructure. Lets the AI say "you don't have a stand mixer; here's a hand-mixer-friendly version." Genuine differentiator vs. surveyed apps. | M | YES (new field) |
| **Structured doneness cues per step** — `doneness: { internalTempF: 165, visual: "deeply browned", touch: "springs back" }` as an optional sub-object on a step. Today this lives buried in step text. Surfaces it as a checkable cue in cooking mode. NYT Cooking gestures at this with prose; no consumer app surveyed makes it structured. | Differentiator. Surfaces what professional kitchens already know (temperature is the most reliable measure — multiple culinary sources, HIGH confidence) and aligns with the trend of smart-thermometer integration (Maverick, ThermoWorks, 2026 cooking-appliance trend reports MEDIUM confidence). Doesn't require integration; just structures the cue. | M | YES |
| **Computed nutrition (derived, not authored)** — runtime calculation from ingredients × USDA FDC database; persists nothing in the canonical format itself, just a `nutrition` *output* that mirrors `schema.org/NutritionInformation`. Cooklist / KitchenPal already expose this via barcode scanning (which we don't do). Required for Google rich-results parity but optional for Schema.org Recipe (HIGH confidence per developers.google.com). | Adds significant value if we can plug in a public ingredient database; complexity is in the data plumbing, not the format. Could be deferred to a later milestone since it's *computed* (doesn't require new format fields beyond ingredient amounts/units which we have). | L | NO (computed, not stored) |
| **`source: { url, importedAt, originalText }` provenance block** — every recipe carries where it came from (URL, AI-conversation-id, "manual entry", "imported from cookbook X v2"). Schema.org Recipe has `mainEntityOfPage` / `isBasedOn` for this. Almost no consumer app surfaces it. | Cheap to add (one optional sub-object), useful for debugging "why does this recipe taste different from what I had at the restaurant" — track-back to source. Differentiator with negligible complexity. | S | YES |
| **Schema.org/Recipe export endpoint** — given a recipe, render it as JSON-LD compliant with schema.org/Recipe. Lets users publish their cookbook to a static site that gets Google rich-results. Almost no self-hosted recipe app does this cleanly. | Power-user differentiator. Mealie has partial schema.org support on import; export is rarer. Requires the canonical format to map cleanly to schema.org/Recipe (it largely will if we follow the field names below). | S–M | YES (need stable canonical format) |
| **Schema.org Recipe on import** — paste a URL, fetch the page, prefer JSON-LD `Recipe` over scraping. This is what every paid app (Paprika, Mealie URL-import, Whisk) does. Currently CookBot has no URL-import. | This is genuinely useful but adds web-fetch / HTML-parse complexity. Most ecosystem apps support it; not having it is the gap. Probably a *next* milestone, not this one. | M–L | YES |

---

## Anti-Features

Features to deliberately NOT build. The milestone is about consolidation, not feature breadth. The user explicitly flagged "general usability improvements" as scoped during requirements — these are candidates to push back on.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|---------------------|
| **A from-scratch DSL like Cooklang's `@flour{500%g}` syntax** | Cooklang is *the* well-engineered open-source recipe markup language (HIGH confidence, see cooklang.org spec). Reinventing it would be a multi-month rabbit hole, conflicts with the user's "users shouldn't have to know special syntax" goal, and the AI would have to be taught yet another format. The whole milestone is about *removing* syntax burden, not adding it. | Adopt JSON/YAML with chip-based UI on top — what Mealie, Tandoor, and most modern apps do. If integration with Cooklang is ever desired, it can be an *export-target* feature later. |
| **Linear timer/temperature scaling** | Newton's Law of Cooling: larger volumes don't bake in proportional time, and temperatures often need to *go down* as size increases (HIGH confidence per kitchen-science sources). Auto-scaling them creates wrong recipes. The current code does NOT scale them (Concern 8) — that's actually correct behavior, just undocumented. | Display original times/temps verbatim with a small "(timing unchanged from original servings)" note when servings ≠ original. Optionally, surface a "Scaling tip" link that cites the surface-area-to-volume principle. |
| **Multi-LLM provider abstraction** | Already in PROJECT.md Out of Scope. The milestone is hard enough without dragging in OpenAI's tool-use / Gemini's function-calling differences. Anthropic's structured outputs are GA and sufficient. | Keep `IAiService → AnthropicAiService` as the only implementation. Document the schema-as-a-tool-input pattern in case a second provider lands later. |
| **Computed nutrition with macro tracking + calorie counting UI** | Crowded category (Cooklist, MyFitnessPal, Nutrola, KitchenPal). The "nutrition" *field* in the schema is fine; building a calorie-tracking dashboard is a different product. | Compute and expose `nutrition` as a derived output if a public ingredient DB is available. No tracking UI, no daily totals, no goals. |
| **Barcode scanning for pantry expiration** | KitchenPal, NoWaste.ai, Cookklist, ShelfSmart all do this with native mobile cameras. We're a Blazor Server app on a self-hosted LAN. The native-camera UX is poor in a web context, and the value-prop overlaps with their core competency. | If expiration tracking is added, do manual entry only. Pantry already has AI-population (`PantryAiPopulationService`); extend that with optional `expiresOn` field. |
| **"My sauce is breaking, help me" real-time cooking troubleshooting as a unique feature** | The "Ask about this step" button already exists (`RecipeCookingAiContext.BuildUserMessage`). Inflating it into a marketed feature would gold-plate something that's already shipped. | Polish the existing affordance: better default question prompts, history per cooking session. Polish, not new feature. |
| **Identity / OAuth / multi-tenant SaaS** | PROJECT.md Out of Scope. Trusted-LAN model is intentional. | If hosting hardening is wanted, the existing `CookBotSettings.AuthMode` placeholder is the right hook — unrelated to this milestone. |
| **A separate Web API / REST endpoints for recipe schema** | PROJECT.md Out of Scope ("there is no driver to expose a separate API"). The canonical format is for *file* interchange, not a public API. | The schema doubles as a contract for the JSON export file (`.cookbook.json`); that is sufficient interchange. |
| **A "schemaless" / "free-form text" recipe mode that bypasses validation** | This is the *opt-out* the milestone is trying to delete (Concern 10). Re-introducing it under a new name defeats the goal. | If the user pastes free-form text, parse it best-effort and immediately route to the structured editor for confirmation. Never persist a recipe that doesn't conform. |

---

## Feature Dependencies

```
[1] Single canonical recipe schema (C# record + JSON Schema)
        |
        +--> [2] Version field + upcaster pipeline
        |        |
        |        +--> Old .cookbook.json files remain importable
        |
        +--> [3] AI structured-output JSON schema (derived from same source)
        |        |
        |        +--> [4] Anthropic output_config.format wiring in AnthropicAiService
        |        |        |
        |        |        +--> [5] Strict system prompt (no opt-out)
        |        |        +--> [6] AiChat.ExtractRecipeContent simplified to "trust the schema"
        |        |        +--> [7] Repair pass for non-chat paths (paste-raw-text)
        |        |
        |        +--> [8] Single source of truth: format spec lives in one C# constant
        |
        +--> [9] Single Format → DB / YAML / JSON-export serializers
        |        |
        |        +--> Concerns 1–4 fixed
        |
        +--> [10] Format-driven new fields (temperature, substitutions, equipment, doneness, source)
                  |
                  +--> [11] Ingredient-chip step editor surfaces ALL the new fields cleanly
                  +--> [12] Cooking mode renders the new fields (temp chip, substitution dropdown, etc.)
```

**Critical path:** [1] → [2] → [9] is the unblocking minimum. [3] → [4] → [5] is the AI-conformance leg. [11] is the UX leg. They can run partially in parallel after [1] is settled.

**[10] format-driven new field** — pick exactly **one** for this milestone. Recommendation: **per-step temperature** with the "not scaled" badge. It's the smallest field, the most universally useful, and it directly addresses Concern 8 (scaling silence) which is on the user's friction list.

---

## MVP Recommendation

For this milestone, prioritize in this order:

1. **Define the canonical schema as JSON Schema** — covers the existing wire format fields plus `schemaVersion: 2` plus per-step `temperature?`. Place at `src/CookBot.Application/Schemas/recipe-v2.schema.json` plus a matching C# record graph in `CookBot.Domain` (`CanonicalRecipe`, `CanonicalStep`, etc.). Status: locks the contract.

2. **Generate / refactor the three serializers from one source** — YAML serializer, JSON-export serializer, EF Core mapper all read/write the canonical record. Concerns 1–4 close.

3. **Add Anthropic structured-outputs in `AnthropicAiService.StreamMessageAsync`** — extend signature with optional `JsonSchema` parameter; when present, pass `output_config.format`. AiChat passes the recipe schema for any turn that should produce a recipe. Streaming works. Tested via `claude-haiku-4-5` first (cheapest), then promoted to default.

4. **Strict system prompt** — delete opt-out clause; tell the model "use the recipe-emission tool / output format if and only if the user is asking for a recipe; otherwise reply normally." Single source-of-truth constant.

5. **Ingredient-chip step editor** — replace the plain textarea in `RecipeEditor.razor` with a Mud-based composer that lets the user pick a step type (Step / Section header) and insert ingredient chips by clicking ingredient rows. Chips render `[name](#id)` underneath but the user never sees brackets.

6. **One format-driven feature: per-step temperature** — adds the field, the editor chip, the cooking-mode chip, the "not scaled" tooltip. Exercises versioning end-to-end.

7. **Tests** — `ExtractRecipeContent`, upcaster v1→v2, structured-output happy-path, structured-output streaming.

**Defer to next milestone:**
- Equipment-list field, substitutions, doneness cues — pick one of these as the *next* "format-driven feature" in the next milestone.
- Schema.org/Recipe export endpoint — interesting but not in service of the four locked goals.
- URL-import (paste a URL, fetch JSON-LD) — separate milestone scope.
- Computed nutrition — needs a public ingredient DB; investigate USDA FDC API in a future research pass.

---

## Open Questions for Requirements

1. **Which one new field for the format-driven goal?** Recommendation: per-step temperature. Alternates: equipment, substitutions, doneness. The user picks one.
2. **JSON Schema vs. C#-first contract?** Recommendation: JSON Schema is the source of truth (because Anthropic structured outputs *needs* a JSON Schema), and the C# records are generated from it (or hand-maintained with a contract test). Confirm.
3. **YAML vs. JSON as primary wire format?** YAML is what users paste today; JSON is what export uses; AI structured-outputs returns JSON. Recommendation: keep YAML for paste-in (parse-only, lossless mapping to canonical), JSON for export and AI emission. Confirm.
4. **Strict tool_use vs. native `output_config.format`?** Recommendation: native output format (newer, GA, simpler control flow, supports streaming, no fake "tool" semantics). Confirm.
5. **What happens to recipes already in DB when v1→v2 lands?** Recommendation: lazy upgrade — read with upcaster, write back at v2 on any save. No big-bang migration. Confirm.

---

## Sources

### Recipe app ecosystem (HIGH confidence — official docs / product help pages)

- [Paprika Recipe Manager — User Guides (iOS / Mac / Windows / Android)](https://www.paprikaapp.com/help/ios/) — modified Markdown, automatic timer detection in directions, ingredient cross-off, recipe scaling, link insertion via toolbar.
- [Mealie — Features documentation](https://docs.mealie.io/documentation/getting-started/features/) and [GitHub README](https://github.com/mealie-recipes/mealie) — Vue frontend, markdown step support, ingredient parsing into Amount / Unit / Food / Note, ingredient + instruction sections via three-dots menu.
- [Tandoor — Templating documentation](https://docs.tandoor.dev/features/templating/) — Jinja2 `{{ ingredients[index] }}` template syntax inside step instructions; markdown editor in frontend; "interaction menu of the ingredient to copy its reference."
- [Cooklang — Specification](https://cooklang.org/docs/spec/) and [Language Overview](https://cooklang.org/docs/) — `@ingredient{quantity%unit}`, `#cookware`, `~timer{value%unit}`, `==Section==`, YAML front matter for metadata, `.cook` extension.
- [Cooklang — Recipe File Formats Compared](https://cooklang.org/blog/19-recipe-formats-compared/) and [Recipe Formats for Developers](https://cooklang.org/blog/41-recipe-formats-for-developers/) — comparative analysis vs. JSON-LD, MealMaster, RecipeML.
- [Open Recipe Format docs](https://open-recipe-format.readthedocs.io/en/latest/) and [GitHub spec](https://github.com/techhat/openrecipeformat) — YAML-based open spec for recipe storage.

### Schema.org Recipe + Google rich results (HIGH confidence)

- [Schema.org Recipe type](https://schema.org/Recipe) — current properties as of v30.0 (2026-03-19): `recipeIngredient`, `recipeInstructions` (HowToStep / HowToSection), `recipeYield`, `recipeCategory`, `cookTime`, `prepTime`, `totalTime`, `nutrition` (NutritionInformation), `suitableForDiet`, `keywords`, `tool`, `recipeCuisine`, `author`, `datePublished`, `image`, `video`, `aggregateRating`.
- [Google Search Central — Recipe structured data](https://developers.google.com/search/docs/appearance/structured-data/recipe) — `name` and `image` are the only strictly-required fields for rich results; nutrition / times / ratings are recommended.
- [hRecipe Microformat — current status](https://microformats.org/wiki/hrecipe) — still supported but Schema.org has displaced it for SEO; `h-recipe` is the modern microformats2 version.

### Anthropic structured outputs (HIGH confidence — official docs)

- [Anthropic — Structured outputs documentation](https://platform.claude.com/docs/en/build-with-claude/structured-outputs) — GA feature for Sonnet 4.5+, Opus 4.5+, Haiku 4.5; `output_config.format` with `type: "json_schema"` is grammar-constrained; streaming supported; recursive schemas not supported.
- [Anthropic Cookbook — Extracting structured JSON via tool use](https://github.com/anthropics/anthropic-cookbook/blob/main/tool_use/extracting_structured_json.ipynb) — predecessor pattern for older models / strict tool use.
- [Anthropic — Get structured output from agents](https://platform.claude.com/docs/en/agent-sdk/structured-outputs) — agent SDK perspective on the same feature.

### LLM self-correction patterns (MEDIUM confidence — well-attested in research)

- [Self-Refine: Iterative Refinement with Self-Feedback (Madaan et al.)](https://selfrefine.info/) — canonical "feedback → refine → feedback" loop reference.
- [Building Self-Correcting LLM Systems: The Evaluator-Optimizer Pattern](https://dev.to/clayroach/building-self-correcting-llm-systems-the-evaluator-optimizer-pattern-169p) — production pattern for parsing-error recovery.
- [Reflection Pattern for self-correcting agents](https://dev.to/programmingcentral/stop-llms-from-lying-build-self-correcting-agents-with-the-reflection-pattern-1df) — "generate-critique-refine" pipeline.
- [Implementing Self-Correction with LLM Validator (Instructor docs)](https://python.useinstructor.com/examples/self_critique/) — practical implementation guidance.

### Schema versioning + migration patterns (HIGH confidence)

- [Confluent — Schema Evolution and Compatibility](https://docs.confluent.io/platform/current/schema-registry/fundamentals/schema-evolution.html) — backward / forward / full compatibility taxonomy.
- [JSON Schema — Future of JSON Schema (stable schema discussion)](https://json-schema.org/blog/posts/future-of-json-schema) — best practices for evolving schemas.
- [Couchbase — Schema versioning tutorial](https://developer.couchbase.com/tutorial-schema-versioning) — `schemaVersion` field embedded in document is industry-standard.

### Ingredient substitution / pantry / nutrition apps (MEDIUM confidence — product pages and reviews)

- [KitchenPal](https://kitchenpalapp.com/en/), [Cooklist](https://apps.apple.com/us/app/cooklist-pantry-meals-recipes/id1352600944), [NoWaste.ai](https://nowaste.ai/), [ShelfSmart](https://apps.apple.com/us/app/shelfsmart-expiry-tracker/id6752867283) — pantry expiration + AI substitution + nutrition (barcode-scan-based mostly).
- [Mr. Cook ingredient replacement](https://www.mrcook.app/en/tools/ingredient-replacement) — substitution-as-feature reference.

### Recipe scaling science (MEDIUM confidence — kitchen-science sources)

- [Your Mother Was A Chemist — Scaling recipes](https://kitchenscience.scitoys.com/scaling) — Newton's Law of Cooling, why doubling a cake recipe means lower temperature + longer time, not the same temp + longer time.
- [Stonesoup — How to adjust cooking times for different temperatures](https://thestonesoup.com/adjust-cooking-times-for-different-temperatures/) — practical guidance on time/temp tradeoffs.
- [University of Wyoming Extension — Scaling: Up Or Down](https://uwyoextension.org/uwnutrition/newsletters/scaling-up-or-down/) — institutional reference on non-linear scaling.

### UI patterns: chip-token editors with autocomplete (HIGH confidence — major editor docs)

- [Tiptap rich text editor](https://tiptap.dev/product/editor) — framework-agnostic; standard slash-command + mention/chip patterns.
- [BlockNote / Plate.js / TinyMCE slash commands](https://www.tiny.cloud/blog/slash-commands-rich-text-editor/) — established `/` slash-menu pattern for inserting structured tokens.
- [Syncfusion blog — Adding Mentions and Slash Commands to React Rich Text Editor](https://www.syncfusion.com/blogs/post/react-rich-text-editor-mentions) — mention-style chip insertion as the de-facto UX for "click an item, get a structured reference."

---

*Research conducted 2026-04-25. Confidence levels reflect verification depth: HIGH = official docs or multiple authoritative sources agree; MEDIUM = single official source or multiple secondary sources; LOW = single secondary source (none in this document).*
