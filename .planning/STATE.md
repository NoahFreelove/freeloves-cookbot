---
gsd_state_version: 1.0
milestone: v1.4
milestone_name: Recipe Data & Interoperability
status: verifying
stopped_at: Phase 14 UI-SPEC approved
last_updated: "2026-06-07T13:20:23.566Z"
last_activity: 2026-06-07
progress:
  total_phases: 6
  completed_phases: 3
  total_plans: 11
  completed_plans: 11
  percent: 50
---

# Project State

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-06-05)

**Core value:** A durable home for the recipes the user actually cooks, captured in one standardized format that round-trips cleanly between AI generation, manual editing, cooking mode, and import/export — without the user (or the AI) having to know special syntax.

**Current focus:** Phase 14 — Photo Gallery

## Current Position

```
v1.4 █████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ 20%
     Phase 12  Phase 13  Phase 14  Phase 15  Phase 16
     [✓]       [NEXT]    [ ]       [ ]       [ ]
```

Phase: 14 (Photo Gallery) — EXECUTING
Plan: 4 of 4
Status: Phase complete — ready for verification
Last activity: 2026-06-07

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
| 13. Export & Interoperability | Schema.org JSON-LD + Cooklang one-way export | INTEROP-01..04 (4 reqs) | Not started |
| 14. Photo Gallery | RecipePhoto entity, multi-upload, gallery UI, AI search-term helper | GALLERY-01..04 (4 reqs) | Not started |
| 15. Nutrition (Offline USDA) | Bundled FDC seed, NutritionService, per-serving panel, JSON-LD nutrition wire | NUTR-01..06 (6 reqs) | Not started |
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
| Nutrition fully offline (bundled SQLite seed) | No API key required for users; no live calls; USDA Foundation Foods + SR Legacy is CC0 and covers recipe staples. FDC API key is optional (`CookBotSettings.FdcApiKey`) for future online fallback. |
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
- P4 (wrong FDC match) → Phase 15 SC2: matched food description + FDC food ID visible to user
- P5 (density error) → Phase 15 SC3: density table unit tests; flour example verified
- P6 (disclaimer missing) → Phase 15 SC4: non-dismissable disclaimer + "Estimated nutrition" heading
- P7 (FDC blocks save) → Phase 15 SC1: explicit CTA only, never blocking save
- P8 (relative image in JSON-LD) → Phase 13 SC1: `image` omitted when not absolute HTTPS
- P9 (ISO 8601 format) → Phase 13 SC1: durations as `PT30M` / `PT1H30M`
- P10 (Cooklang round-trip) → Phase 13 SC3: "Export only (one-way)" label present
- P11 (Cooklang special chars) → Phase 13 SC2: `@`/`#`/`~` sanitized before emission
- P12 (AI photo hallucination) → Phase 14 SC4: AI never emits URL; copyright disclaimer visible
- P13 (orphaned files) → Phase 14 SC3: delete removes file from `wwwroot/uploads/`
- P14 (SignalR multi-upload) → Phase 14 SC2: sequential upload; circuit remains connected
- P15 (canonical mutation) → Phases 13/14/15 SC (projectors receive RecipeDocument, never mutate)

### Open Questions (for /gsd-discuss-phase, not blockers)

- **Phase 15 — Density table source:** FAO/INFOODS vs. USDA ARS measurement conversion tables vs. King Arthur Flour. Name the authoritative source and enumerate the 20+ ingredients covered in the phase plan.
- **Phase 15 — Ingredient name normalization deny-list:** Which adjectives/modifiers to strip ("room-temperature", "good", "fresh", "packed") before FDC search. Define the deny-list during Phase 15 planning.
- **Phase 14 — Photo count cap:** Named constant in `CookBotSettings` or service layer (research suggests ≤5 or ≤10 per recipe). Confirm at Phase 14 plan time.
- **Phase 14 — `.cookbook.json` photo export behavior:** Either omit photo rows or include an explicit note. Resolve in Phase 14 planning.

## Session Continuity

Last session: 2026-06-07T13:20:23.559Z
Stopped at: Phase 14 UI-SPEC approved
Resume file: None

**Next:** Plan Phase 13 — Export & Interoperability (Schema.org JSON-LD in RecipeView + Cooklang one-way `.cook` export; INTEROP-01..04). No CONTEXT.md yet — discuss-phase optional. Note: `/gsd:secure-phase 12` never run (no SECURITY.md anywhere in project history; trusted-LAN posture; the one security item D-12-08 javascript: defang was human-verified).
