---
gsd_state_version: 1.0
milestone: v1.1
milestone_name: Canonical Format & AI Conformance
status: executing
stopped_at: Completed 02-03-PLAN.md
last_updated: "2026-04-26T05:44:24.895Z"
last_activity: 2026-04-26
progress:
  total_phases: 4
  completed_phases: 1
  total_plans: 9
  completed_plans: 7
  percent: 78
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-25)

**Core value:** A durable home for the recipes the user actually cooks, captured in one standardized format that round-trips cleanly between AI generation, manual editing, cooking mode, and import/export — without the user (or the AI) having to know special syntax.
**Current focus:** Phase 02 — ai-structured-output-conformance

## Current Position

Phase: 02 (ai-structured-output-conformance) — EXECUTING
Plan: 4 of 5
Status: Ready to execute
Last activity: 2026-04-26

Progress: [████████░░] 78%

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
| Phase 02 P01 | 23 | 2 tasks | 4 files |
| Phase 02 P02-02 | 5 | 3 tasks | 6 files |
| Phase 02 P03 | 7 | 4 tasks | 12 files |

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
- Phase 2 Plan 1: SecretRedactor in Infrastructure/AI (caller-coloc), PromptInjectionGuard in Application/AI (callers in Application). Both pure-static; D-12 / D-16 / D-18 spec language committed verbatim — no refactor.
- Phase 2 Plan 1: ReDoS risk on SecretRedactor regexes explicitly accepted (T-02P01-04); no Timeout set; inputs bounded by Anthropic ≤256 KB response size.
- Phase 2 Plan 2: IStructuredAiService lives in Application/AI (NOT Domain) — JsonNode + ValidationResult cannot leak to Domain. AnthropicAiService now implements both IAiService and IStructuredAiService; DI factory aliases the second registration to the same scoped instance.
- Phase 2 Plan 2: SendStructuredAsync<T> never throws (except OperationCanceledException). Every error path routes through SecretRedactor.Redact (4 call sites: client-init, transport, non-success HTTP, JsonException). Refusal stop_reason short-circuits before deserialization to preserve the 2-retry repair budget for Wave 3.
- Phase 2 Plan 2: FakeHttpMessageHandler is the first HTTP-layer test fake in this codebase; pattern is reusable for Wave 3+ tests. Testability seam is  on AnthropicAiService — minimum-blast-radius hook avoiding IHttpClientFactory ceremony.
- Phase 2 Plan 3: AiRecipeGenerator orchestrator (AI-02/AI-03) registered Scoped, not Singleton — DI lifetime forbids Singleton consuming Scoped IStructuredAiService. Plan-text deviation; functionally equivalent.
- Phase 2 Plan 3: Microsoft.Extensions.Logging.Abstractions 10.0.3 added to CookBot.Application — first ILogger usage in src/. Foundational MS abstraction; not on forbidden-package list.
- Phase 2 Plan 3: AI-08 directive (D-14) appended to RecipeSchemaDocumentationProvider.FormatPrompt; PromptInjectionGuard.WrapRecipe wired into RecipeCookingAiContext.BuildUserMessage; AiConversation.FormatVersion column added (default 2; existing rows back-fill to 1 per D-22).
- Phase 2 Plan 3: Repair-loop hard cap MaxRepairAttempts = 2 const-locked (D-05); refusal/transport short-circuit returns immediately when Validation==null && SanitizedError!=null; minimal repair prompt is 2 user turns + validator error list (D-06).

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

Last session: 2026-04-26T05:44:12.264Z
Stopped at: Completed 02-03-PLAN.md
Resume file: None

**Planned Phase:** 2 (AI Structured Output & Conformance) — 5 plans — 2026-04-26T02:43:44.788Z
