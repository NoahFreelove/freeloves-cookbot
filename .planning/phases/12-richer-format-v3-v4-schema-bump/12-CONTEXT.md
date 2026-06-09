# Phase 12: Richer Format + v3→v4 Schema Bump - Context

**Gathered:** 2026-06-05
**Status:** Ready for planning
**Mode:** `--auto` (gray areas auto-resolved with recommended defaults; review decisions below before planning)

<domain>
## Phase Boundary

Add the **four deferred format field-groups** to the canonical `RecipeDocument` via a **v3→v4 schema bump**, and make the whole stack stably v4 *before* any export or enrichment consumer (Phases 13–15) is written. Concretely:

1. **Ingredient substitutions** (FORMAT-01) — per-ingredient list.
2. **Equipment / tools list** (FORMAT-02) — recipe-level.
3. **Per-step doneness cue** (FORMAT-03) — alongside the existing per-step `Temperature`.
4. **Source / provenance** (FORMAT-04) — source URL + author credit.

Each field group must: round-trip through `RecipeFormatParser` + `JsonRecipeSerializer` (FORMAT-07); upcast cleanly from v3 with per-field null-guards (FORMAT-05); be in the AI JSON schema with a passing prompt-snapshot test (FORMAT-06); pass `RecipeValidator` rules (FORMAT-06); and be authored in `RecipeEditor` + displayed in `RecipeView` (SC5).

This phase is a **near-exact replay of the proven v2→v3 bump** (`Migration_V2_To_V3`, Phase 8). It follows the same pattern with no new external dependencies. **Out of scope:** any consumer of the v4 fields (JSON-LD, Cooklang, nutrition, photo gallery) — those are Phases 13–15.

</domain>

<decisions>
## Implementation Decisions

All six gray areas were auto-resolved in `--auto` mode. The recommended option (grounded in the locked v1.4 invariants + the v2→v3 precedent) was selected for each. **These are the lockable HOW decisions; the WHAT is fixed by FORMAT-01..07.**

### Substitutions (FORMAT-01)
- **D-12-01:** `IngredientSubstitution` is a new pure POCO record in `CookBot.Domain/Recipes/` with a **required freeform `Note`** (`string`) plus **optional structured `Name` (`string?`), `Amount` (`double?`), `Unit` (`string?`)**. Matches FORMAT-01 verbatim ("freeform note + optional structured name/amount/unit") and degrades gracefully to note-only when the AI/user only writes prose.
- **D-12-02:** Carried as `IReadOnlyList<IngredientSubstitution> Substitutions { get; init; } = []` on `IngredientEntry` (per-ingredient, default empty — never null, mirrors the `Ingredients`/`Tags` empty-list convention).
- **D-12-03:** `IngredientSubstitution` carries a `[JsonExtensionData] Extras` dictionary, identical to `IngredientEntry`/`StepNode`/`RecipeDocument`, for FORMAT-09 forward-compat consistency.
- **D-12-04 (scaling):** A structured substitution `Amount` is **display-only and does NOT scale** with servings. Honors the hard guardrail *"only `RecipeIngredient.Amount` scales"* — `RecipeScalingService` stays untouched. (Proportional substitution scaling is a noted v1.5 candidate, see Deferred.)

### Equipment (FORMAT-02)
- **D-12-05:** Equipment is a **recipe-level `IReadOnlyList<string> Equipment { get; init; } = []`** on `RecipeDocument` — *not* a structured `EquipmentEntry` record. Matches FORMAT-02 verbatim ("recipe-level `string[]`"); Cooklang `#cookware` (INTEROP-03, Phase 13) is name-only, so structured equipment buys nothing in v1.4. The research FEATURES.md `EquipmentEntry` suggestion is **rejected** as over-engineering beyond the requirement.

### Doneness cue (FORMAT-03)
- **D-12-06:** Per-step `DonenessCue { get; init; }` is a freeform `string?` on **`ContentStep` only** (`SectionStep` never carries it), sitting **alongside the existing `Temperature`** — independent, not a replacement. No enum, no semantic validation beyond a `[MaxLength]` guard. Matches the `StepTemperature?`-on-`ContentStep` precedent.

### Provenance (FORMAT-04)
- **D-12-07:** `RecipeProvenance` is a new pure POCO record in `CookBot.Domain/Recipes/` with **all-optional** `SourceUrl` (`string?`), `AuthorName` (`string?`), and `SourceName` (`string?`, carries the "adapted from {source}" credit). The whole `RecipeProvenance? Provenance` on `RecipeDocument` is nullable. The research `AdaptedDate` field is **dropped** — FORMAT-04 does not ask for it (deferred).
- **D-12-08 (link safety):** When `SourceUrl` is rendered as a clickable link in `RecipeView`, it **MUST pass the existing `RecipePhotoUrlValidator` scheme-allowlist** (`http`/`https` only — defangs `javascript:`/`data:`). Reuse, do not reinvent. A URL that fails the allowlist renders as plain text (or is omitted), never as a live link.
- **D-12-09 (AI fabrication guard):** The AI **MUST NOT fabricate** `SourceUrl` or `AuthorName`. Provenance stays `null` unless the user explicitly supplied a real source. This parallels the photo-URL anti-feature (P12) — the prompt must say *"leave `provenance` null unless a source is explicitly provided; never invent a URL or author."*

### Surface scope + AI default-fill policy
- **D-12-10 (render surfaces):** Phase 12 surfaces the new fields in **`RecipeEditor` (authoring) + `RecipeView` (display) ONLY** — matching SC5 exactly (equipment checklist, per-ingredient substitution chips, per-step doneness cue, provenance author-credit + source link). **Cooking Mode surfacing** (pre-cook equipment checklist, doneness callout below the timer) is a research "should-have" → **DEFERRED** to a later v1.4 polish slice / backlog. This keeps Phase 12 to the schema bump + its required surfaces and avoids scope creep into `CookingMode.razor`.
- **D-12-11 (AI default-fill):** The updated prompt instructs Claude to **naturally populate `equipment` and per-step `donenessCue`** (high-value, zero hallucination cost) and to emit `substitutions` only when genuinely useful. Empty/null is always valid output (SC3: "even when null"). Combined with D-12-09, provenance is the only field the AI is told to leave empty by default.

### Upcaster + schema + validation (mechanical — follow the v2→v3 precedent exactly)
- **D-12-12:** `Migration_V3_To_V4 : IRecipeUpcaster` (`FromVersion => 3`, `ToVersion => 4`) with **four independent, per-field null-guard no-ops** (one per new field group), stamping `version: 4`. Copy the `Migration_V2_To_V3` structure verbatim (PITFALLS C7 — never bundle-throw).
- **D-12-13:** `RecipeUpcasterChain.CurrentVersion` → `4`; register `Migration_V3_To_V4` in `CookBot.Application/DependencyInjection.cs` (line ~31, after `Migration_V2_To_V3`) **in the same plan** as the migration class (prevents the P1 DI-gap startup crash). The existing `RecipeUpcasterChain_GapInVersions_ThrowsAtConstruction` test must be extended / a v3→v4 chain test added.
- **D-12-14:** `RecipeJsonSchemaProvider` needs **no code change** — it reflects `RecipeDocument` via `JsonSchemaExporter`. Adding the POCO fields auto-updates the schema. The **prompt-snapshot test (`tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` + `Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt`) MUST be regenerated and committed in the same change** (FORMAT-06, P3 — no AI schema drift). Watch the documented anyOf/`additionalProperties` strict-mode constraints when new nested records (`IngredientSubstitution`, `RecipeProvenance`) introduce new object subschemas.
- **D-12-15:** `RecipeValidator` gains rules for the new fields (warnings-not-errors style where ambiguous, mirroring temperature validation): e.g. provenance `SourceUrl` malformed/disallowed-scheme → warning; substitution with neither `Note` nor `Name` → warning. **Never throws** (FORMAT-07).

### Claude's Discretion
- Exact `[MaxLength]` caps for new string fields (follow the existing `PhotoUrl=2048` / `Description=4096` precedents; pick proportionate caps).
- Editor authoring affordances (sub-rows under ingredient chips for substitutions, a tag-style input for equipment, a per-step text field for doneness, a recipe-meta form block for provenance) — refine at plan / `ui-phase` time. SC5 fixes *that* they appear, not the pixel layout.
- Internal naming of the four upcaster guards and test fixture filenames (follow `tests/CookBot.Tests/Fixtures/Recipes/upcaster/` conventions).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents (researcher, planner) MUST read these before planning or implementing.**

### Requirements & milestone decisions (authoritative)
- `.planning/REQUIREMENTS.md` §"Richer Recipe Format (FORMAT)" — FORMAT-01..07, the locked WHAT for this phase.
- `.planning/ROADMAP.md` §"Phase 12" — goal + 5 success criteria (SC1–SC5).
- `.planning/STATE.md` §"Accumulated Context" — Hard Invariants, Key v1.4 Decisions, Pitfall Guard Summary (P1/P2/P3 gate this phase), Build-Order chain.
- `.planning/research/SUMMARY.md` §"Phase 12" — research disposition: *standard pattern, skip `--research-phase`*; v4 field list; pitfalls P1/P2/P3.

### Codebase precedents to copy (the v2→v3 bump is the template)
- `src/CookBot.Application/Recipes/Migration_V2_To_V3.cs` — **the exact pattern** for `Migration_V3_To_V4` (per-field independent no-op guards, version stamp).
- `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` — `CurrentVersion` const (→ 4) + gap-validation at construction.
- `src/CookBot.Application/DependencyInjection.cs` (lines 29–31) — where to register the new upcaster.
- `src/CookBot.Application/Recipes/RecipeJsonSchemaProvider.cs` — reflection-based schema (no change needed) + the strict-mode anyOf/`additionalProperties` constraints to respect when new nested records appear.
- `src/CookBot.Application/Recipes/RecipeValidator.cs` — validation pattern (warnings vs errors; the temperature-validation block is the model for new-field rules).
- `src/CookBot.Domain/Recipes/RecipeDocument.cs`, `IngredientEntry.cs`, `StepNode.cs` (`ContentStep`), `StepTemperature.cs` — the POCO + `[JsonExtensionData] Extras` + `JsonPropertyName` conventions the new types must match.
- `src/CookBot.Application/Services/RecipePhotoUrlValidator.cs` — **reuse** for provenance `SourceUrl` link-safety (D-12-08).

### Tests to update (in the same change)
- `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` + `tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt` — regenerate the byte-stable snapshot (FORMAT-06 / P3).
- `tests/CookBot.Tests/Recipes/RecipeUpcasterTests.cs` (`...GapInVersions_ThrowsAtConstruction`, version-dispatch) — extend for v3→v4.
- `tests/CookBot.Tests/Recipes/Migration_V2_To_V3_Tests.cs` + `..._ChainTests.cs` — the template for a new `Migration_V3_To_V4_Tests` / chain test (partial-field v3 fixtures → no throw, all four new groups null/empty: SC1).
- `tests/CookBot.Tests/Fixtures/Recipes/upcaster/` — fixture-file conventions for the v3-doc → v4 cases.

### UI surfaces to modify
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` (+ `RecipeEditorParts/`) — authoring of all four field groups.
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — display (SC5: equipment checklist, substitution chips, doneness per step, provenance link/author credit).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`Migration_V2_To_V3`**: literal copy-template — four no-op guards documenting the per-field contract, then `obj["version"] = 4`.
- **`RecipePhotoUrlValidator`**: scheme-allowlist (`http`/`https`) already shipped for v1.3 paste-URL photos — reuse verbatim for provenance `SourceUrl`.
- **`[JsonExtensionData] Extras`** on every existing record — copy onto `IngredientSubstitution` and `RecipeProvenance` for FORMAT-09 forward-compat.
- **`JsonSchemaExporter`** in `RecipeJsonSchemaProvider` auto-reflects new POCO fields — schema "just works"; only the verified snapshot needs regen.
- **Empty-list-not-null convention** (`Ingredients`/`Tags`/`Steps` default `[]`) — apply to `Equipment` and `Substitutions`.

### Established Patterns
- **Schema bumps ride the versioned upcaster chain** with per-field null-coalescing; STJ maps absent JSON keys → null on nullable C# props, so guards are documentation + bundle-throw defense, not transformation logic.
- **Nullable optional fields** (`PhotoUrl?`, `Description?`, `Temperature?`) deserialize from absent keys to null automatically — `Provenance?`, `DonenessCue?` follow suit.
- **Validator returns a data envelope, never throws** (FORMAT-07); new rules go in as warnings unless the type system can't otherwise prevent corruption.
- **Polymorphic `StepNode`**: `DonenessCue` belongs on `ContentStep` only — `SectionStep` carries headings, not cooking metadata (mirrors `Temperature`).

### Integration Points
- `RecipeDocument` (new `Equipment`, `Provenance` props) → `IngredientEntry` (new `Substitutions`) → `ContentStep` (new `DonenessCue`).
- DI: `DependencyInjection.cs` registers the new upcaster; `RecipeUpcasterChain.CurrentVersion` gates the chain.
- AI: schema auto-updates → prompt snapshot regen → structured-output orchestrator emits v4 (untouched transport).
- Web: `RecipeEditor.razor` (write) + `RecipeView.razor` (read) are the only UI surfaces this phase touches.

</code_context>

<specifics>
## Specific Ideas

- Provenance display reads like an editorial credit: **"Adapted from {SourceName} by {AuthorName}"** with `SourceUrl` as the link target (when allowlist-valid). All parts optional — render only what's present.
- Substitutions display as **chips/sub-lines under their parent ingredient** in the scaled-ingredients sidebar (SC5 "substitution chips").
- Equipment displays as a **checklist** (SC5) — reuse existing checkbox atom styling; checklist state is ephemeral UI (not persisted), consistent with the cooking-mode highlight pattern.

</specifics>

<deferred>
## Deferred Ideas

- **Cooking Mode surfacing** of equipment (pre-cook checklist modal) and doneness cues (callout below the timer) — research "should-have"; deferred to a later v1.4 polish slice / backlog to keep Phase 12 scoped to schema + RecipeEditor/RecipeView.
- **Structured `EquipmentEntry`** (name + quantity/note) — rejected for v1.4 (FORMAT-02 says `string[]`); revisit only if a consumer needs structured equipment.
- **Proportional substitution-amount scaling** — substitution `Amount` is display-only in v1.4 (honors the "only RecipeIngredient.Amount scales" guardrail); scaling it in proportion to the parent ingredient is a v1.5 candidate.
- **Provenance `AdaptedDate`** — research suggested it; FORMAT-04 doesn't ask for it. Defer to v4.x.
- **First-class `recipeCategory`/`recipeCuisine` v4 fields** — already locked as *derived from tags at JSON-LD projection time* (Phase 13), not new v4 fields. Promote only if tag-derivation proves lossy (v4.1).
- **AI-assisted substitution generation as a dedicated feature** — Phase 12 only lets the prompt emit substitutions inline; a richer "suggest substitutions for this ingredient" affordance is future scope.

</deferred>

---

*Phase: 12-richer-format-v3-v4-schema-bump*
*Context gathered: 2026-06-05*
