---
gsd_state_version: 1.0
milestone: v1.3
milestone_name: Production-Ready & Format Maturity
status: planning
stopped_at: null
paused_at: null
last_updated: "2026-05-15T20:30:00.000Z"
last_activity: "2026-05-15 — v1.3 milestone scaffolding started via `/gsd-new-milestone v1.3`. PROJECT.md updated (Current Milestone section + Active scope + Carry-forward pruned + Containerization OOS flipped). Next: research decision → REQUIREMENTS.md → ROADMAP.md."
progress:
  total_phases: 0
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
  percent: 0
---

# Project State

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-05-15)

**Core value:** A durable home for the recipes the user actually cooks, captured in one standardized format that round-trips cleanly between AI generation, manual editing, cooking mode, and import/export — without the user (or the AI) having to know special syntax.

**Current focus:** v1.3 Production-Ready & Format Maturity — scaffolding (PROJECT.md updated; REQUIREMENTS.md + ROADMAP.md pending).

## Current Position

Phase: Not started (defining requirements)
Plan: —
Status: Defining requirements
Last activity: 2026-05-15 — Milestone v1.3 started

**Next step:** REQUIREMENTS.md draft (with optional research first) → ROADMAP.md spawn → first phase planning.

**Phase numbering:** Continues from v1.2 — v1.3 starts at **Phase 8**.

## Shipped milestones

| Milestone | Shipped | Phases | Plans | Reqs | Tag |
|-----------|---------|--------|-------|------|-----|
| v1.2 UI Redesign | 2026-04-27 | 5–7 | 16 | 75/75 | `v1.2` |
| v1.1 Canonical Format & AI Conformance (PARTIAL) | 2026-04-25/26 | 1–2 of 4 (3 absorbed; 4 deferred) | 9 of TBD | 30/46 | — (no tag) |
| v1.0 (pre-GSD existing app) | pre-2026-04-25 | — | — | — | — |

## Paused / partial milestones

### v1.1 Canonical Format & AI Conformance

**Status:** PARTIAL — Phases 1+2 shipped under v1.1, Phase 3 absorbed into v1.2 Phase 6, Phase 4 deferred to v1.3 (now in scope).

| Phase | Status | Disposition |
|-------|--------|-------------|
| 1. Canonical Format Foundation | ✅ Shipped 2026-04-25 | Validated; load-bearing for v1.2 + v1.3 |
| 2. AI Structured Output & Conformance | ✅ Shipped 2026-04-26 | Validated; load-bearing for v1.2 AI Chat canvas + v1.3 AI schema update |
| 3. Editor UX Without Special Syntax | 🔁 Absorbed into v1.2 | Built as v1.2 ED-03..09 (chip composer in custom Razor) |
| 4. Format-Driven New Field & Cleanup | 🔄 Now in v1.3 | FEATURE-V2-* → v1.3 schema bucket; FUTURE-V1.1-01..05 → v1.3 schema + cleanup buckets |

## Accumulated Context

### Decisions

Decisions log lives in `.planning/PROJECT.md` Key Decisions table. Per-phase decision logs preserved in `.planning/phases/*/PHASE-SUMMARY.md` files and the milestone archive at `.planning/milestones/v1.2-ROADMAP.md`.

Hard invariants carried into v1.3:
- **Canonical-first reads:** UI surfaces consume `RecipeDocument` directly via `Recipe.CanonicalDocumentJson` + `JsonRecipeSerializer`. Never read `Recipe.IngredientsJson` / `StepsJson` / `IngredientRefs` / `TagsJson` from new code.
- **No auto-rewrite on save:** Step text is never modified by the save path. Explicit chips are the only persisted source of timers and ingredient links.
- **AI structured-output orchestrator:** `IAiRecipeGenerator` + `SecretRedactor` + `PromptInjectionGuard` are preserved verbatim — UI consumes them; do not bypass.
- **Three-tier extractor stays deleted:** POLISH-01 invariant — `AiChat.ExtractRecipeContent` is permanently gone.
- **AI-off contract:** Host kill switch `CookBotSettings.AiFeaturesEnabled` AND per-user `UserProfile.AiEnabled` must both be true; gating enforced inside application/data services, not by middleware.
- **MudBlazor stays out:** v1.3 work must not reintroduce MudBlazor or `Microsoft.Extensions.AI`; no `Newtonsoft.Json`; no `NJsonSchema` (use `JsonSchema.Net` if schema validation is needed).
- **Trusted-LAN auth posture stays:** "Self-hostable" in v1.3 means *runnable by others*, NOT *internet-exposed*. `CookBotSettings.AuthMode` reserved for future use; no Identity middleware yet.

### Blockers / concerns

None — milestone just started, no active phase. Two open questions captured for the requirements-drafting step:

1. **File-upload storage:** `wwwroot/uploads/` is the obvious default but bypasses the user-isolation that other persistence already has. Decide at requirements time: flat `uploads/{recipe-guid}.{ext}` vs per-user subdirectories. Affects backup/restore story.
2. **Encrypt-at-rest key derivation:** Where does the encryption key live? Env var only? Derived from a config file? This determines what "lose your key and the AI keys are gone" means for self-hosters.

### v1.3 scope buckets

| Bucket | Source items |
|--------|-------------|
| Schema v3 + Photos | IMG-01..13 (refined), FUTURE-V1.1-01 (per-step temp), D-25 (description column) — one V2→V3 upcaster bundles all three |
| Format cleanup | FUTURE-V1.1-02 (tags relational), FUTURE-V1.1-03 (projector deletion), FUTURE-V1.1-04 (prompt snapshot), FUTURE-V1.1-05 (README format) |
| QOL | FUTURE-13 (smart pantry-match), FUTURE-15 (AiChat edit-anyway), FUTURE-14 (accent picker), DEFERRED-PROF-AIPROMPT (Profile AI editor) |
| Small-stuff polish | D-26 (cookbook reparenting), D-37 (pantry quick-add), D-15 (moon glyph), D-16 (TopBar RightSlot), live JS tick |
| Prod-ready | Dockerfile + compose, FUTURE-01 (encrypt API key), FUTURE-02 (token-cost telemetry), README install/config/backup/upgrade |

## Session Continuity

Last session: 2026-05-15T20:30:00Z
Stopped at: v1.3 milestone scaffolding — PROJECT.md updated; STATE.md reset; REQUIREMENTS.md + ROADMAP.md pending.
Resume file: None

**Next:** Workflow continues inside `/gsd-new-milestone v1.3` — research decision → REQUIREMENTS.md → ROADMAP.md spawn → phase numbering verification.
