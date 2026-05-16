---
gsd_state_version: 1.0
milestone: v1.3
milestone_name: Production-Ready & Format Maturity
status: executing
stopped_at: Phase 10 context gathered
last_updated: "2026-05-16T21:04:27.933Z"
last_activity: 2026-05-16 -- Phase 9 execution started
progress:
  total_phases: 3
  completed_phases: 2
  total_plans: 20
  completed_plans: 20
  percent: 67
---

# Project State

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-05-15)

**Core value:** A durable home for the recipes the user actually cooks, captured in one standardized format that round-trips cleanly between AI generation, manual editing, cooking mode, and import/export — without the user (or the AI) having to know special syntax.

**Current focus:** Phase 9 — photos-prod-ready-infrastructure

## Current Position

Phase: 9 (photos-prod-ready-infrastructure) — EXECUTING
Plan: 1 of 7
Status: Executing Phase 9
Last activity: 2026-05-16 -- Phase 9 execution started

**Next step:** `/gsd:plan-phase 8`

**Phase numbering:** Continues from v1.2 — v1.3 phases are 8, 9, 10.

## Shipped milestones

| Milestone | Shipped | Phases | Plans | Reqs | Tag |
|-----------|---------|--------|-------|------|-----|
| v1.2 UI Redesign | 2026-04-27 | 5–7 | 16 | 75/75 | `v1.2` |
| v1.1 Canonical Format & AI Conformance (PARTIAL) | 2026-04-25/26 | 1–2 of 4 (3 absorbed; 4 deferred) | 9 of TBD | 30/46 | — (no tag) |
| v1.0 (pre-GSD existing app) | pre-2026-04-25 | — | — | — | — |

## Paused / partial milestones

### v1.1 Canonical Format & AI Conformance

**Status:** Executing Phase 9

| Phase | Status | Disposition |
|-------|--------|-------------|
| 1. Canonical Format Foundation | ✅ Shipped 2026-04-25 | Validated; load-bearing for v1.2 + v1.3 |
| 2. AI Structured Output & Conformance | ✅ Shipped 2026-04-26 | Validated; load-bearing for v1.2 AI Chat canvas + v1.3 AI schema update |
| 3. Editor UX Without Special Syntax | 🔁 Absorbed into v1.2 | Built as v1.2 ED-03..09 (chip composer in custom Razor) |
| 4. Format-Driven New Field & Cleanup | 🔄 Now in v1.3 | FUTURE-V1.1-01..05 → v1.3 SCHEMA + CLEAN buckets |

## Accumulated Context

### Decisions

Decisions log lives in `.planning/PROJECT.md` Key Decisions table. Per-phase decision logs preserved in `.planning/phases/*/PHASE-SUMMARY.md` files and the milestone archive at `.planning/milestones/v1.2-ROADMAP.md`.

Hard invariants carried into v1.3:

- **Canonical-first reads:** UI surfaces consume `RecipeDocument` directly via `Recipe.CanonicalDocumentJson` + `JsonRecipeSerializer`. Never read `Recipe.IngredientsJson` / `StepsJson` / `IngredientRefs` / `TagsJson` from new code.
- **No auto-rewrite on save:** Step text is never modified by the save path. Explicit chips are the only persisted source of timers and ingredient links.
- **AI structured-output orchestrator:** `IAiRecipeGenerator` + `SecretRedactor` + `PromptInjectionGuard` are preserved verbatim — UI consumes them; do not bypass.
- **Three-tier extractor stays deleted:** POLISH-01 invariant — `AiChat.ExtractRecipeContent` is permanently gone. PHOTO-12 surfaces `_lastStructuredRecipe.Value.PhotoUrl` directly — no extractor revival.
- **AI-off contract:** Host kill switch `CookBotSettings.AiFeaturesEnabled` AND per-user `UserProfile.AiEnabled` must both be true; gating enforced inside application/data services, not by middleware. PROD-12..17 telemetry writes ONLY when both gates are open.
- **MudBlazor stays out:** v1.3 work must not reintroduce MudBlazor or `Microsoft.Extensions.AI`; no `Newtonsoft.Json`; no `NJsonSchema` (use `JsonSchema.Net` if schema validation is needed).
- **Trusted-LAN auth posture stays:** "Self-hostable" in v1.3 means *runnable by others*, NOT *internet-exposed*. `CookBotSettings.AuthMode` reserved for future use; no Identity middleware yet.

### v1.3 phase summary

| Phase | Goal | Requirements | Status |
|-------|------|--------------|--------|
| 8. Format Foundation | V2→V3 schema bump + format cleanup | SCHEMA-01..12, CLEAN-01..04 (16 reqs) | Not started |
| 9. Photos + Prod-Ready Infrastructure | File upload + Docker + encrypt-at-rest + telemetry write + README | PHOTO-01..14, PROD-01..21 (35 reqs) | Not started |
| 10. QOL, Polish & Consumer Surfaces | Smart pantry-match + AI Chat hardening + QOL + polish | QOL-01..07, POLISH-01..05 (12 reqs) | Not started |

### Open questions (for /gsd-discuss-phase, not blockers)

- ~~**Phase 8 — TagsJson column drop timing:**~~ RESOLVED 2026-05-15 (CONTEXT.md D-26) — drops in same phase via follow-up `DropTagsJsonColumn` migration after callsite switchover.
- **Phase 9 — Sentinel-prefix detection heuristic:** `CfDJ8...` prefix identifies Data Protection ciphertext; `sk-ant-` prefix identifies Anthropic plaintext keys. Confirm exact detection logic at discuss/plan time.
- **Phase 9 — Token pricing table values:** Verify current Anthropic per-million-token prices for Haiku 4.5, Sonnet 4.6, Opus 4.7 at Phase 9 plan time; embed with verification date.
- **Phase 10 — Pantry-match scoring weights:** Proposed formula `coverageScore - 0.3 * recentlyMadePenalty` is an engineering estimate; make configurable in `appsettings.json`, not hardcoded.

### Blockers / concerns

None — ready to begin Phase 8 planning.

## Session Continuity

Last session: 2026-05-16T21:04:27.926Z
Stopped at: Phase 10 context gathered
Resume file: .planning/phases/10-qol-polish-consumer-surfaces/10-CONTEXT.md

**Next:** `/gsd:plan-phase 8` — Format Foundation (16 requirements: SCHEMA-01..12 + CLEAN-01..04; 5 user-resolved decisions in CONTEXT.md D-26/D-27/D-31/D-34/D-35).
