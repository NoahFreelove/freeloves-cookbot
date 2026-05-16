# Roadmap: FreelovesCookBot

## Milestones

- ✅ **v1.0 (pre-GSD existing app)** — codebase mapped in `.planning/codebase/`
- ⏸ **v1.1 Canonical Format & AI Conformance** — Phases 1–2 shipped 2026-04-25/26; Phase 3 absorbed into v1.2; Phase 4 deferred to v1.3+
- ✅ **v1.2 UI Redesign** — Phases 5–7 shipped 2026-04-27, 16 plans, 75/75 reqs ([archive](milestones/v1.2-ROADMAP.md))
- 📋 **v1.3 Production-Ready & Format Maturity** — Phases 8–10, 63 reqs

## Phases

<details>
<summary>✅ v1.2 UI Redesign (Phases 5–7) — SHIPPED 2026-04-27</summary>

- [x] Phase 5: Foundation — Design tokens, atoms, shell, dialogs (5/5 plans) — completed 2026-04-27
- [x] Phase 6: Marquee surfaces — Home, Cooking Mode, Recipe View, Recipe Editor (4/4 plans, absorbs v1.1 EDITOR-01..07) — completed 2026-04-27
- [x] Phase 7: Remaining surfaces, accessibility, MudBlazor strip (7/7 plans + 2 post-ship slices) — completed 2026-04-27

Full details: [`milestones/v1.2-ROADMAP.md`](milestones/v1.2-ROADMAP.md) · [requirements](milestones/v1.2-REQUIREMENTS.md) · [audit](milestones/v1.2-MILESTONE-AUDIT.md)

</details>

<details>
<summary>⏸ v1.1 Canonical Format & AI Conformance (Phases 1–4) — partial</summary>

- [x] Phase 1: Canonical Format Foundation (4/4 plans) — completed 2026-04-25
- [x] Phase 2: AI Structured Output & Conformance (5/5 plans) — completed 2026-04-26
- [~] Phase 3: Editor UX Without Special Syntax — absorbed into v1.2 Phase 6 (EDITOR-01..07 → ED-03..09)
- [→] Phase 4: Format-Driven New Field & Cleanup — deferred to v1.3+ (FUTURE-V1.1-01..05)

Phase artifacts remain in `.planning/phases/01-canonical-format-foundation/` and `.planning/phases/02-ai-structured-output-conformance/` (load-bearing for v1.2 surfaces).

</details>

### 📋 v1.3 Production-Ready & Format Maturity (Phases 8–10)

- [ ] **Phase 8: Format Foundation** — V2→V3 canonical schema bump, LegacyRecipeProjector deletion, TagsJson→RecipeTag migration, lint denylist update, parser and snapshot tests
- [ ] **Phase 9: Photos + Prod-Ready Infrastructure** — File upload pipeline + paste-URL safety, Docker + compose, encrypt-at-rest API key, token-cost telemetry write path, README deploy docs
- [ ] **Phase 10: QOL, Polish & Consumer Surfaces** — Smart pantry-match, AI Chat hardening, accent picker, Profile prompt editor, telemetry widget, cookbook reparenting, pantry quick-add, moon glyph, TopBar slot, live timer tick

## Phase Details

### Phase 8: Format Foundation

**Goal**: The canonical `RecipeDocument` advances to v3 — all three new nullable fields (PhotoUrl, Description, per-step Temperature) exist in the type system, the upcaster chain, the EF entity columns, the AI schema contract, the YAML/JSON wire format, and the parser — and the four v1.1 format-cleanup carry-forwards ship: LegacyRecipeProjector deleted, TagsJson migrated to a relational RecipeTag table, prompt snapshot regression test in place, and README format section added.
**Depends on**: Nothing (Phase 8 is the foundation for Phases 9 and 10)
**Requirements**: SCHEMA-01, SCHEMA-02, SCHEMA-03, SCHEMA-04, SCHEMA-05, SCHEMA-06, SCHEMA-07, SCHEMA-08, SCHEMA-09, SCHEMA-10, SCHEMA-11, SCHEMA-12, CLEAN-01, CLEAN-02, CLEAN-03, CLEAN-04
**Success Criteria** (what must be TRUE):

  1. A v2 `.cookbook.json` imported after this phase upcasts to v3 with all three new fields null — no data loss, no throw (SCHEMA-04, C7: null-coalescing per-field in the upcaster, not a bundle-throw)
  2. `RecipeJsonSchemaProvider` emits a JSON schema that includes `photoUrl`, `description`, and step-level `temperature` — the schema-assertion test (`SCHEMA-11`) passes as the first test written before any other schema code merges
  3. `RecipeFormatParserTests` cover all three new fields — round-trip fixtures for null value, valid value, and all three temperature units (F/C/gas) all pass; no existing test is deleted (SCHEMA-12, H11: parser tests audited before any schema code merges)
  4. `LegacyRecipeProjector` and `IRecipeProjector` files are deleted; `grep -r "LegacyRecipeProjector\|IRecipeProjector" src/` returns zero hits; startup null-canonical guard in `DatabaseSeeder.SeedAsync` fails loud if any row has null `CanonicalDocumentJson` (CLEAN-01)
  5. Home pantry-match dietary filtering can use a SQL JOIN against `RecipeTag` rows — `TagsJson` is superseded (CLEAN-02); prompt-snapshot test asserts `PromptBuilderService.BuildSystemPrompt` output is byte-stable (CLEAN-03); README "Recipe Format" section documents v3 YAML/JSON with worked example (CLEAN-04)

**Plans**: 13 plans in 7 waves

- [x] 08-01-PLAN.md — Wave 1: Schema-assertion test FIRST (RED gate) + RecipeFormatParserTests audit (SCHEMA-11, SCHEMA-12)
- [x] 08-02-PLAN.md — Wave 1: StepTemperature record + enum + round-trip tests (SCHEMA-03)
- [x] 08-03-PLAN.md — Wave 2: RecipeDocument + ContentStep v3 fields + RecipeValidator per-unit rules (SCHEMA-01/02/06/07; turns Plan 01 GREEN)
- [x] 08-04-PLAN.md — Wave 3: Migration_V2_To_V3 upcaster + CurrentVersion bump + DI registration + per-field fixture matrix (SCHEMA-04, SCHEMA-05)
- [x] 08-05-PLAN.md — Wave 3: RecipeFormatParser + JsonRecipeSerializer round-trip + StepTemperatureJsonConverter + v3 fixtures (SCHEMA-08, SCHEMA-09, SCHEMA-12)
- [x] 08-06-PLAN.md — Wave 3: SCHEMA-10 denylist extension + RecipeSchemaDocumentationProvider v3 example + self-checking negative-path test (SCHEMA-10)
- [x] 08-07-PLAN.md — Wave 3: AddRecipePhotoUrlAndDescription EF migration + entity columns + dynamic backup-label fix in DatabaseSeeder (SCHEMA-01/02/05/06)
- [x] 08-08-PLAN.md — Wave 4: AddRecipeTagTable migration with embedded backfill + entity + configuration + four callsite switchovers + RecipeTagBackfillTests (CLEAN-02)
- [x] 08-09-PLAN.md — Wave 4: Verify.Xunit 31.12.5 + ModuleInitializer + REPLACE PromptSnapshotTests + delete legacy fixture (CLEAN-03)
- [x] 08-12-PLAN.md — Wave 4: AddPantryMatchIndexes migration (Phase 10 perf readiness; D-31 #4)
- [x] 08-10-PLAN.md — Wave 5: D-32 5-step LegacyRecipeProjector deletion (permanent guard FIRST, then file deletion) (CLEAN-01)
- [x] 08-11-PLAN.md — Wave 6: DropTagsJsonColumn migration + entity/configuration/RecipeService cleanup (CLEAN-02 finalization)
- [ ] 08-13-PLAN.md — Wave 7: README "Recipe Format" inline section with v3 YAML/JSON examples + V1->V2->V3 lineage (CLEAN-04)

**UI hint**: no

### Phase 9: Photos + Prod-Ready Infrastructure

**Goal**: Users can attach a hero photo to any recipe (file upload or paste-URL); the app is shippable to other self-hosters via Docker with persistent volumes; AI API keys are encrypted at rest with a migration path for existing plaintext keys; token-cost telemetry is written per-call; and the README has complete install/config/backup/upgrade documentation.
**Depends on**: Phase 8 (PhotoUrl field exists on RecipeDocument v3; RecipeTag table in place for dietary filter groundwork)
**Requirements**: PHOTO-01, PHOTO-02, PHOTO-03, PHOTO-04, PHOTO-05, PHOTO-06, PHOTO-07, PHOTO-08, PHOTO-09, PHOTO-10, PHOTO-11, PHOTO-12, PHOTO-13, PHOTO-14, PROD-01, PROD-02, PROD-03, PROD-04, PROD-05, PROD-06, PROD-07, PROD-08, PROD-09, PROD-10, PROD-11, PROD-12, PROD-13, PROD-14, PROD-15, PROD-16, PROD-17, PROD-18, PROD-19, PROD-20, PROD-21
**Success Criteria** (what must be TRUE):

  1. `wwwroot/uploads/` is in `.gitignore` as the FIRST commit of this phase — before any upload code is written (PHOTO-01, C5); uploading a JPEG via the Recipe Editor succeeds, persists a GUID filename, and shows a live `<img>` preview; uploading a 15 MB file or a non-image file (e.g. PDF) is rejected with a descriptive toast, not a silent SignalR circuit disconnect (PHOTO-02, PHOTO-03, H1: all three size limits — Kestrel, FormOptions, SignalR MaximumReceiveMessageSize — raised to 12 MB)
  2. A paste-URL using a `javascript:` or `data:` scheme is rejected by `RecipePhotoUrlValidator`; the same validator runs on AI-emitted PhotoUrl in the structured-output return path (PHOTO-07); `<img>` tags with a broken URL fall back to `<StripedPlaceholder>` exactly once — no infinite `onerror` loop (PHOTO-08, H4)
  3. `docker compose up` brings the app online on port 7000 reachable from the LAN; `docker stop && docker start` preserves all data and all encrypted AI keys decrypt successfully — the key ring survives a container restart (PROD-01..07, C1: key ring is on the named `/data` volume via `PersistKeysToDbContext`)
  4. Existing plaintext `AiApiKey` rows are re-encrypted idempotently on first boot — re-running `DatabaseSeeder.SeedAsync` on already-encrypted rows is a no-op; AI key sharing works after encryption — an integration test for the share-then-resolve round-trip passes (PROD-08, PROD-09, PROD-11, C2: single shared protector scope, not per-user; C3: sentinel-prefix idempotency)
  5. The 2-retry repair loop writes rows with `IsRetryAttempt = true`; aggregation queries surface retry rows separately so the repair loop does not double-count in cost totals (PROD-14, PROD-15, H9); per-model pricing lives in `appsettings.json`, not hardcoded (PROD-16, H10)

**Plans**: TBD
**UI hint**: yes

### Phase 10: QOL, Polish & Consumer Surfaces

**Goal**: The Home "Tonight from your pantry" section uses a scored pantry-match algorithm instead of a deterministic stub; AI Chat failures surface a raw-edit recovery dialog; users can pick an accent color variant; Profile exposes the AI system prompt template editor and a rolling token-cost widget; and the five small-stuff polish items (cookbook reparenting, pantry quick-add, moon glyph, TopBar RightSlot, live timer tick) all ship.
**Depends on**: Phase 8 (RecipeTag table for dietary pre-filter; RecipeDocument v3 for reading temperature/description in consumer surfaces), Phase 9 (AiUsageLog rows exist for the Profile widget)
**Requirements**: QOL-01, QOL-02, QOL-03, QOL-04, QOL-05, QOL-06, QOL-07, POLISH-01, POLISH-02, POLISH-03, POLISH-04, POLISH-05
**Success Criteria** (what must be TRUE):

  1. Home "Tonight from your pantry" sort is deterministic on reload (stable sort by score desc, recipeId asc) — no result volatility; composite DB indexes on `RecipeIngredient(RecipeId, IngredientId)` and `PantryItem(UserId, IngredientId)` are in place so Home load is O(n log n), not O(n²) (QOL-01, QOL-03, H7); recipes cooked in the last 7 days score lower than fresher candidates (QOL-01 recency debounce)
  2. When AI generation fails validation and the repair loop exhausts retries, a `RawRecipeEditorDialog` opens with the raw response in a textarea — user can attempt re-parse or copy to clipboard; no silent degraded-toast-only path remains (QOL-04)
  3. Profile accent picker (terracotta / sage / default orange) persists across browser sessions via `localStorage`; the selection applies before first paint via `data-accent` on `<html>`; no new `UserProfile` column or EF migration is required (QOL-05)
  4. All five polish items are closed: Recipe Editor cookbook picker routes through `RecipeService.UpdateAsync` with destination-cookbook ownership validation (POLISH-01); Pantry per-row cart icon wires to `GroceryListService.AddItemAsync` with toast on success (POLISH-02); dark-mode toggle shows Moon glyph when dark, Sun when light (POLISH-03); `RecipeView.razor` RV-05 actions reach `TopBar.RightSlot` via the new `ICbTopBarService` scoped service (POLISH-04); Home active-timer band updates every second via `setInterval` JS tick that tears down on page unload (POLISH-05)

**Plans**: TBD
**UI hint**: yes

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Canonical Format Foundation | v1.1 | 4/4 | Complete | 2026-04-25 |
| 2. AI Structured Output & Conformance | v1.1 | 5/5 | Complete | 2026-04-26 |
| 3. Editor UX Without Special Syntax | v1.1 → v1.2 | 0/8 | Absorbed into v1.2 Phase 6 | — |
| 4. Format-Driven New Field & Cleanup | v1.1 → v1.3+ | 0/TBD | Deferred | — |
| 5. Foundation — Design tokens, atoms, shell, dialogs | v1.2 | 5/5 | Complete | 2026-04-27 |
| 6. Marquee surfaces — Home, CookingMode, RecipeView, RecipeEditor | v1.2 | 4/4 | Complete | 2026-04-27 |
| 7. Remaining surfaces, accessibility, MudBlazor strip | v1.2 | 7/7 | Complete | 2026-04-27 |
| 8. Format Foundation | v1.3 | 12/13 | In Progress|  |
| 9. Photos + Prod-Ready Infrastructure | v1.3 | 0/TBD | Not started | — |
| 10. QOL, Polish & Consumer Surfaces | v1.3 | 0/TBD | Not started | — |

---

*v1.2 milestone archived 2026-05-15. v1.3 roadmap created 2026-05-15 — 3 phases (8–10), 63 requirements mapped. See `.planning/MILESTONES.md` for the historical record.*
