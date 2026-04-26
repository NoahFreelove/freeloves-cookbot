---
gsd_state_version: 1.0
milestone: v1.1
milestone_name: Canonical Format & AI Conformance
status: planning
stopped_at: Phase 2 context gathered
last_updated: "2026-04-26T00:33:32.148Z"
last_activity: 2026-04-25
progress:
  total_phases: 4
  completed_phases: 1
  total_plans: 4
  completed_plans: 4
  percent: 100
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-25)

**Core value:** A durable home for the recipes the user actually cooks, captured in one standardized format that round-trips cleanly between AI generation, manual editing, cooking mode, and import/export — without the user (or the AI) having to know special syntax.
**Current focus:** Phase --phase — 1

## Current Position

Phase: 2
Plan: Not started
Status: Ready to plan
Last activity: 2026-04-25

Progress: [░░░░░░░░░░] 0%

## Performance Metrics

**Velocity:**

- Total plans completed: 4
- Average duration: —
- Total execution time: —

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 1 | 4 | - | - |

**Recent Trend:**

- Last 5 plans: —
- Trend: —

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work (resolved during requirements step, see SUMMARY.md §9):

- v1.1 / Q1: New format field is **per-step temperature** only (FEATURE-V2 section) — smallest blast radius, addresses CONCERNS §8 scaling silence.
- v1.1 / Q2: Markdown back-compat — text-backed model; `[name](#id)` stays under the hood as the wire-level representation, chips are a view-layer tokenization.
- v1.1 / Q3: AI repair aggressiveness — max 2 retries with minimal prompt (failure + format reminder), then "Edit and save anyway" affordance.
- v1.1 / Q4: Validation strictness — two-tier; schema-strict for storage, lenient with coercion for parse.
- v1.1 / Q5: Structured-output mechanism — native `output_config.format`; tool-use is FUTURE-09 fallback.
- v1.1 / Q6: Resumed AI conversations stamp `FormatVersion = 2`, prepend system note (POLISH-06).
- v1.1 / Q7: Encrypt-at-rest for API keys is deferred (FUTURE-01).
- v1.1 / Q8: Tags become a relational table (POLISH-04).
- v1.1 / Q9: Only `RecipeIngredient.Amount` scales — temperature/time fields never scale with servings.

### Pending Todos

[From .planning/todos/pending/ — ideas captured during sessions]

None yet.

### Blockers/Concerns

[Issues that affect future work]

- Phase 1 must finish before Phase 2 can start (AI structured output needs `RecipeJsonSchemaProvider` + `RecipeValidator`).
- Phase 3 (chip editor) is parallel-safe with Phase 2 once Phase 1 ships; coordinate to avoid both phases editing `RecipeEditor.razor` simultaneously.
- EF Core 10 JSON column behavior on SQLite is MEDIUM confidence per research SUMMARY §10 — recommend smoke test before Phase 1 persistence work lands (covered by MIGRATION-08).

## Deferred Items

Items captured in REQUIREMENTS.md "Future Requirements" — not in v1.1 scope:

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| Security | FUTURE-01: Encrypt-at-rest for `UserProfile.AiApiKey` | Deferred | 2026-04-25 (requirements) |
| Telemetry | FUTURE-02: Token-cost telemetry per key owner | Deferred | 2026-04-25 |
| Format fields | FUTURE-03..06: substitutions, equipment, doneness cues, source provenance | Deferred | 2026-04-25 |
| Export | FUTURE-07/11: Schema.org rich-results, Cooklang one-way export | Deferred | 2026-04-25 |
| Nutrition | FUTURE-08: USDA FDC nutrition computation | Deferred | 2026-04-25 |
| AI fallback | FUTURE-09: Tool-use fallback if Structured Outputs regresses | Deferred | 2026-04-25 |
| Maintenance | FUTURE-10: MudBlazor 9.x upgrade | Deferred | 2026-04-25 |

## Session Continuity

Last session: --stopped-at
Stopped at: Phase 2 context gathered
Resume file: --resume-file

**Planned Phase:** 1 (Canonical Format Foundation) — 4 plans — 2026-04-25T22:09:09.186Z
