---
gsd_state_version: 1.0
milestone: v1.4
milestone_name: Recipe Data & Interoperability
status: executing
stopped_at: Phase 16 (UAT + Integration) in progress — test16 integration harness added (nutrition panel + JSON-LD nutrition + Cooklang export); 8/15 Phase 15 nutrition UAT items now automated hands-free. Phase 15 shipped (commit b65e856).
last_updated: "2026-06-24"
last_activity: 2026-06-24 -- Phase 16 UAT automation: tests/uat-harness/tests/test16-integration.mjs added + wired into npm test
progress:
  total_phases: 6
  completed_phases: 4
  total_plans: 18
  completed_plans: 18
  percent: 67
---

# Project State

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-06-05)

**Core value:** A durable home for the recipes the user actually cooks, captured in one standardized format that round-trips cleanly between AI generation, manual editing, cooking mode, and import/export — without the user (or the AI) having to know special syntax.

**Current focus:** Phase 15 — Nutrition (Offline CNF — Canadian Nutrient File)

## Current Position

```
v1.4 ████████████████████████████████████░░░░░░ 85%
     Phase 12  Phase 13  Phase 14  Phase 15  Phase 16
     [✓]       [✓]       [✓]       [✓]       [~ UAT auto]
```

Phase: 16 (UAT + Integration) — IN PROGRESS (Tier-A automation done)
Status: test16 integration harness added — nutrition panel + JSON-LD nutrition + Cooklang export run hands-free under `npm test`
Next un-started phase: none (Phase 16 is the last v1.4 phase; remaining = a few non-automatable nutrition states + upload-blocked gallery items + optional format-fields-visible extension)
Last activity: 2026-06-24 -- Phase 16 UAT automation (test16-integration.mjs)

## Shipped milestones

| Milestone | Shipped | Phases | Plans | Reqs | Tag |
|-----------|---------|--------|-------|------|-----|
| v1.3 Production-Ready & Format Maturity | 2026-06-05 | 8–11 | 39 | all | `v1.3` |
| v1.2 UI Redesign | 2026-04-27 | 5–7 | 16 | 75/75 | `v1.2` |
| v1.1 Canonical Format & AI Conformance (PARTIAL) | 2026-04-25/26 | 1–2 of 4 (3 absorbed; 4 deferred) | 9 of TBD | 30/46 | — (no tag) |
| v1.0 (pre-GSD existing app) | pre-2026-04-25 | — | — | — | — |

## v1.4 Phase Summary

| Phase | Goal | Requirements | Status |
|-------|------|--------------|--------|
| 12. Richer Format + v3→v4 Schema Bump | Stable v4 canonical doc with substitutions, equipment, doneness cues, provenance | FORMAT-01..07 (7 reqs) | ✅ Complete (2026-06-06) |
| 13. Export & Interoperability | Schema.org JSON-LD + Cooklang one-way export | INTEROP-01..04 (4 reqs) | ✅ Complete + human-verified (2026-06-07) |
| 14. Photo Gallery | RecipePhoto entity, multi-upload, gallery UI, AI search-term helper | GALLERY-01..04 (4 reqs) | ✅ Code complete + verified; 10 browser-UAT items pending |
| 15. Nutrition (Offline CNF) | Bundled Canadian Nutrient File seed, NutritionService, per-serving panel, JSON-LD nutrition wire | NUTR-01..06 (6 reqs) | Not started (data source = CNF, decided 2026-06-07) |
| 16. UAT + Integration | Playwright harness extended for v1.4 + cross-theme integration | UATAUTO-02 (1 req) | Not started |

## Accumulated Context

### Hard Invariants (carry-forward from v1.3 + v1.4 additions)

- **Canonical-first reads:** UI surfaces consume `RecipeDocument` directly via `Recipe.CanonicalDocumentJson` + `JsonRecipeSerializer`. Never read `Recipe.IngredientsJson` / `StepsJson` / `IngredientRefs` / `TagsJson` from new code.
- **No auto-rewrite on save:** Step text is never modified by the save path. Explicit chips are the only persisted source of timers and ingredient links.
- **AI structured-output orchestrator:** `IAiRecipeGenerator` + `SecretRedactor` + `PromptInjectionGuard` preserved verbatim — UI consumes them; do not bypass.
- **Three-tier extractor stays deleted:** POLISH-01 invariant — `AiChat.ExtractRecipeContent` is permanently gone.
- **AI-off contract:** Host kill switch `CookBotSettings.AiFeaturesEnabled` AND per-user `UserProfile.AiEnabled` must both be true; gating enforced inside application/data services, not by middleware.
- **MudBlazor stays out:** No MudBlazor, no `Microsoft.Extensions.AI`, no `Newtonsoft.Json`, no `NJsonSchema`.
- **Trusted-LAN auth posture stays:** No Identity middleware, no OAuth, no public internet exposure.
- **Zero new NuGet packages:** All v1.4 themes hand-rolled on System.Text.Json / EF Core / HttpClient — research consensus is firm.
- **Display-only layers never mutate canonical:** Export projectors (`JsonLdRecipeProjector`, `CooklangRecipeProjector`) and the nutrition panel receive `RecipeDocument` and return a string / view model. They never call `RecipeService.UpdateAsync` or set `CanonicalDocumentJson`.
- **Nutrition never stored in CanonicalDocumentJson:** Nutrition is computed via `NutritionService` and cached in `RecipeNutritionCache` table. AI must never emit nutrition. `CanonicalDocumentJson` set only in `RecipeService`.
- **Photo paths never stored in CanonicalDocumentJson:** `RecipePhoto` entity table owns file paths. Photos are stripped from `.cookbook.json` exports (host-specific operational state).

### Key v1.4 Decisions

| Decision | Rationale |
|----------|-----------|
| v3→v4 schema bump is Phase 12 and stands alone | All downstream themes (export projectors, photo gallery migration, nutrition service) read from `RecipeDocument` v4. Building any of them against v3 then re-patching for v4 fields is avoidable rework. |
| `RecipePhoto` entity table, not canonical-doc array | Photo paths are host-specific operational state, not recipe format data. Must not travel in `.cookbook.json` exports. Must not be emitted by or fed to the AI. Consistent with `Recipe.PhotoUrl` precedent. |
| Nutrition fully offline (bundled SQLite seed) | No API key required for users; no live calls; the bundled food-composition DB covers recipe staples. Online API is optional (`CookBotSettings.FdcApiKey`) for a future fallback. |
| **Nutrition data source = Canadian Nutrient File (CNF), not USDA FDC** (decided 2026-06-07, pre-Phase-15) | User is Canadian (`UnitSystem=Canadian`). CNF is Health Canada's official offline food-composition DB (~5,993 foods, per-100 g, bilingual EN/FR), downloadable as relational CSV → fits the bundled-SQLite-seed plan 1:1. CNF ships household-measure→gram **Conversion Factors**, which removes most of the external "density table" need. License = **Open Government Licence – Canada**: redistribution allowed but requires attributing "Health Canada, Canadian Nutrient File" and forbids modifying nutrient values (per-serving re-expression is explicitly allowed) — differs from USDA CC0 (no attribution). USDA FDC optionally retained as a gap-fill fallback. **Implications:** (1) new attribution requirement in the nutrition panel; (2) seed stores CNF values verbatim, compute per-serving at display; (3) `CookBotSettings.FdcApiKey` → CNF (or keep FDC fallback); (4) density-table scope shrinks to CNF-unmatched ingredients only; (5) disclaimer text → "Data: Health Canada, Canadian Nutrient File (2015)"; (6) bulk download is the 2015 edition, CNF online tool/API intermittently down → offline bundle is correct. |
| `recipeCategory`/`recipeCuisine` derived from tags | No new v4 schema fields for these; derived at JSON-LD projection time from existing `RecipeTags`. Promote to first-class v4 fields only if tag-derivation proves too lossy (v4.1 candidate). |
| AI photo helper = search-term suggestion only | AI never emits or auto-embeds image URLs. Copyright + hallucination risk eliminated. User pastes their own URL; HEAD-validated before persist. |
| NUTR-06 assigned to Phase 15 (not Phase 13) | `nutrition.calories` in JSON-LD requires nutrition data to exist first. Phase 13 lays the JSON-LD scaffold; Phase 15 wires the nutrition fields into it. |
| Upcaster DI registration + gap-detection test in same Phase 12 plan | Prevents startup crash (P1 — chain gap at runtime). |
| Four independent null-guards in Migration_V3_To_V4 | Per-field independence prevents bundle-throw (P2). Follows V2→V3 pattern exactly. |
| Nutrition is post-save enrichment only | FDC API outage must never block recipe save (P7). "Calculate nutrition" CTA is explicit user action, never blocking `RecipeService.CreateAsync`/`UpdateAsync`. |

### Build Order Dependency Chain

```
Phase 12 (v4 schema) → Phase 13 (export projectors read v4 fields)
                     → Phase 14 (photo gallery reads v4 RecipeDocument)
Phase 13 + Phase 14 → Phase 15 (nutrition wires into JSON-LD from Phase 13; hero photo from Phase 14)
Phase 12–15         → Phase 16 (UAT + integration)
```

### Pitfall Guard Summary (baked into success criteria)

- P1 (DI gap) → Phase 12 SC4: gap-detection test covers v3→v4 explicitly
- P2 (bundle-throw) → Phase 12 SC1: no throw across field guards; partial-field fixtures
- P3 (AI schema drift) → Phase 12 SC3: prompt-snapshot test updated and passing
- P4 (wrong CNF match) → Phase 15 SC2: matched CNF food description + CNF food code visible to user
- P5 (density error) → Phase 15 SC3: CNF conversion factors used first; fallback density table unit-tested; flour example verified
- P6 (disclaimer missing) → Phase 15 SC4: non-dismissable disclaimer + "Estimated nutrition" heading
- P7 (nutrition calc blocks save) → Phase 15 SC1: explicit CTA only, never blocking save
- P8 (relative image in JSON-LD) → Phase 13 SC1: `image` omitted when not absolute HTTPS
- P9 (ISO 8601 format) → Phase 13 SC1: durations as `PT30M` / `PT1H30M`
- P10 (Cooklang round-trip) → Phase 13 SC3: "Export only (one-way)" label present
- P11 (Cooklang special chars) → Phase 13 SC2: `@`/`#`/`~` sanitized before emission
- P12 (AI photo hallucination) → Phase 14 SC4: AI never emits URL; copyright disclaimer visible
- P13 (orphaned files) → Phase 14 SC3: delete removes file from `wwwroot/uploads/`
- P14 (SignalR multi-upload) → Phase 14 SC2: sequential upload; circuit remains connected
- P15 (canonical mutation) → Phases 13/14/15 SC (projectors receive RecipeDocument, never mutate)

### Open Questions (for /gsd-discuss-phase, not blockers)

- **Phase 15 — Density table source:** ~~RESOLVED by the CNF decision~~ — the Canadian Nutrient File ships per-food household-measure→gram Conversion Factors, so an external density table is now only a *fallback* for CNF-unmatched ingredients. Phase 15 plan: use CNF conversion factors first; name the fallback density source (FAO/INFOODS vs. USDA ARS vs. King Arthur) only for the gap set.
- **Phase 15 — Ingredient name normalization deny-list:** Which adjectives/modifiers to strip ("room-temperature", "good", "fresh", "packed") before CNF food-description search. Define the deny-list during Phase 15 planning.
- **Phase 14 — Photo count cap:** Named constant in `CookBotSettings` or service layer (research suggests ≤5 or ≤10 per recipe). Confirm at Phase 14 plan time.
- **Phase 14 — `.cookbook.json` photo export behavior:** Either omit photo rows or include an explicit note. Resolve in Phase 14 planning.

## Session Continuity

Last session: 2026-06-25 (human-driven UAT — /gsd-verify-work)
Stopped at: Phase 14 + 15 human UAT complete. All 10 Phase-14 items pass; 14/15 Phase-15 items pass (item 14 fail). 6 issues + 1 change request diagnosed (root causes in the two HUMAN-UAT.md files). Awaiting decision on the fix path.
Resume file: .planning/phases/14-photo-gallery/14-HUMAN-UAT.md (Gaps + Change Requests), .planning/phases/15-nutrition-offline-cnf-canadian-nutrient-file/15-HUMAN-UAT.md (Gaps)

**Session 2026-06-25 (human UAT) did:**

- Ran the automated harness first (`npm test`): 6 passed / 1 skip / 0 fail — all automatable items green. Server build (12:06) confirmed current (latest commit touched only the JS harness).
- Walked the 14 human-only items in a real browser. **Phase 14: all 10 enumerated items PASS.** **Phase 15: 14/15 PASS** (items 9 & 12 code-verified; item 14 FAILED).
- **6 issues found (all diagnosed with file:line root causes + fix directions in the UAT files):**
  1. `cookbook-listing-hero` (minor) — CookbookDetail.razor:108 hardcodes StripedPlaceholder; recipe hero never shown on the cookbook listing (pre-existing; likely also Home/CookbookList).
  2. `gallery-trash-overlap` (minor) — RecipePhotoGalleryManager.razor action-button row overflows the 180px card; trash sometimes covered by the next card → unclickable. Fix: flex-wrap.
  3. `gallery-overcap-batch-noop` (minor) — GetMultipleFiles(remaining) at ~line 321 throws outside the try/catch when selection > remaining → whole batch silently dropped, no toast.
  4. `gallery-stale-urlerror-after-delete` (minor) — `_urlError` "Maximum N photos" not cleared after a delete frees a slot → stale message; same-URL retry blocked.
  5. `nutrition-macro-grid-not-responsive` (minor, item 14 FAIL) — RecipeView.razor:460 inline `grid-template-columns:repeat(4,1fr)` with no class + no media rule → stays 4-across at ≤720px (inline can't be overridden by a media query).
  6. `nutrition-coverage-rows-ignore-toggle` (minor) — coverage rows render fixed `row.EnergyKcal` (totals) while the headline honors Per-serving/Total → mismatch in per-serving mode.
- **1 change request:** remove the "Suggest photo search terms" AI helper (user: not useful). Retires GALLERY-04 / Phase-14 item 6 — scope change, needs confirmation before executing.
- Items 3 & 7 (nutrition) confirmed live: flour ≈455 kcal/cup density correct; ≈ low-confidence + CNF description + [FoodId] all present.

**Session 2026-06-24 (Phase 16 UAT automation) did:**

- Added `tests/uat-harness/tests/test16-integration.mjs` (wired into `npm test`): one throwaway recipe (4 CNF-matchable staples + 1 unmatchable "edible gold flake") drives nutrition State 1 → Calculate → State 2 + JSON-LD before/after + Cooklang export. Idempotent (deletes the recipe; setup also clears leftovers).
- **Cleared 8/15 Phase 15 nutrition UAT items hands-free** (1, 2, 4, 5, 6, 8, 13, 15) — see 15-HUMAN-UAT.md. Also automated INTEROP-02 (Cooklang `.cook` export, previously human-only) and the nutrition half of the Phase 16 SC2 cross-theme check (`nutrition.calories` in JSON-LD).
- **Cooklang export seam note:** `download.js` revokes the blob URL synchronously after the anchor click, so Playwright's download artifact races to ENOENT — the test captures the base64 handed to `window.cookBotDownloadFile` instead (deterministic; tests the projector output reaching the seam).
- **Remaining (not automated):** nutrition items 3 (exact 455 kcal — unit-tested), 7 (≈ low-confidence), 9 (disclaimer in states 3/4/5), 10/11 (stale State 3 + recalc — automatable later), 12 (error state — needs injection), 14 (≤720px 2-col). The Phase 16 SC2 `image` half needs an HTTPS host. UATAUTO-02's "format fields visible" (Phase 12) + "gallery primary/reorder" (upload-blocked) not yet automated.
- **Uncommitted** (carried + new): `tests/uat-harness/run.mjs`, `tests/test14-photo-gallery.mjs`, `tests/test16-integration.mjs`, README.md, and `.planning` doc edits (STATE.md, REQUIREMENTS.md, PROJECT.md, 14-/15-HUMAN-UAT.md).

**Prior session (2026-06-08) did:**

- ⚠️ **Found & fixed a stale dev server:** the process on :7000 was a build from Jun 6 20:28 — *before* the Phase 14 merge (Jun 7 09:30+). It served the old single-hero composite and had no `RecipePhotos` table (`20260607124611_AddRecipePhotosTable` never applied). Restarted it → current code built, migration applied, gallery now live. **Restart the server after any redeploy.**
- Added an automated Phase 14 UAT module: `tests/uat-harness/tests/test14-photo-gallery.mjs` (wired into `npm test`). Creates/cleans a throwaway recipe; idempotent.
- **Cleared 3 of 10 Phase 14 UAT items automatically:** #5 paste-URL reject, #6 AI helper text-only/no-URL, #7 copyright disclaimer (all PASS). Harness now green (5 passed / 1 skipped / 0 failed).
- **7 items remain pending (real browser needed):** #1 multi-upload, #2 reorder/set-hero, #3 caption round-trip, #4 delete-confirm, #8 RecipeView gallery+swap, #9 photo cap, plus #5-accept/#10-WR-04. Reason: Blazor Server `<InputFile>` SignalR file streaming is **not drivable via Playwright** (headless or headed). Single upload was observed working once → pipeline is functional. See 14-HUMAN-UAT.md.
- Uncommitted: `tests/uat-harness/run.mjs` + `test14-photo-gallery.mjs`, and `.planning` doc edits (STATE.md, 14-HUMAN-UAT.md).

**Next (after 2026-06-24 session):** Phase 16 Tier-A automation is done. Options to close v1.4:
- (a) **Extend test16** to cover UATAUTO-02's remaining bullets that ARE automatable: "format fields visible" (author equipment/substitutions/doneness/provenance via paste-raw YAML — wire shape known, see `tests/.../upcaster/v3-to-v4-all-present.json` — and assert they render in RecipeView), plus nutrition states 10/11 (stale) and 14 (responsive 2-col).
- (b) **Build a test-only direct-upload seam** so the harness can drive photo uploads, unblocking the 6 stuck Phase 14 gallery items (the "gallery primary/reorder" UATAUTO-02 bullet). Touches production code — a deliberate design call.
- (c) Accept the irreducible manual residue (paste-URL accept needs outbound network; error-state injection; HTTPS-only JSON-LD `image`) and `/gsd-verify-work` → ship v1.4.

Carried low-pri hygiene from Phase 13: validate/omit non-http(s) schemes in the Cooklang `>> source:` line. Note: `/gsd:secure-phase` never run for any v1.4 phase (trusted-LAN posture; D-12-08 javascript: defang was human-verified).
