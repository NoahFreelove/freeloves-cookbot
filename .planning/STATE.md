---
gsd_state_version: 1.0
milestone: v1.1
milestone_name: Canonical Format & AI Conformance
status: planning
stopped_at: Phase 3 context gathered
last_updated: "2026-04-26T17:11:34.509Z"
last_activity: 2026-04-26
progress:
  total_phases: 4
  completed_phases: 2
  total_plans: 13
  completed_plans: 9
  percent: 69
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-25)

**Core value:** A durable home for the recipes the user actually cooks, captured in one standardized format that round-trips cleanly between AI generation, manual editing, cooking mode, and import/export — without the user (or the AI) having to know special syntax.
**Current focus:** Phase 02 — ai-structured-output-conformance

## Current Position

Phase: 3
Plan: Not started
Status: Ready to plan
Last activity: 2026-04-26

Progress: [██████████] 100%

## Performance Metrics

**Velocity:**

- Total plans completed: 9
- Average duration: —
- Total execution time: —

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 1 | 4 | - | - |
| 02 | 5 | - | - |

**Recent Trend:**

- Last 5 plans: —
- Trend: —

*Updated after each plan completion*
| Phase 02 P01 | 23 | 2 tasks | 4 files |
| Phase 02 P02-02 | 5 | 3 tasks | 6 files |
| Phase 02 P03 | 7 | 4 tasks | 12 files |
| Phase Phase 02 P04 P13 | 5 tasks (4 + 1 RED-GREEN split) | 8 files tasks | - files |
| Phase 02 P05 | 8 | 3 tasks | 16 files |

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
- Phase 2 Plan 4: Per-recipe upcast loop in CookbookTransferService.Deserialize operates on raw JsonNode from input string (not DTO round-trip) — the v1-shaped CookbookTransferRecipe DTO would silently drop v2-only fields (id/kind/heading) on a v2 envelope. Rule 1 correctness fix vs. plan-text path.
- Phase 2 Plan 4: AiChat.razor keeps both Send (free-form chat via IAiService.StreamMessageAsync) AND Generate Recipe (structured-output via IAiRecipeGenerator) buttons. Plan text noted intent-detection vs. explicit-button as executor's discretion; explicit buttons match UI-SPEC Surface 2 invariant ('Free-form chat turns never show the Save button') without heuristic intent detection.
- Phase 2 Plan 4: AI-09 formally moved to FUTURE-12 in REQUIREMENTS.md (active list, deferred list, traceability, phase summary, top-of-file count). AI-08 (XML-tag wrap) + AI-08-AUDIT (Markdig DisableHtml lockdown) are the load-bearing trusted-LAN mitigations replacing the dropped per-sharer consent banner. CONTEXT.md canonical_refs corrected to reference IStructuredAiService (Plan 02 layering deviation).
- Phase 2 Plan 4: Markdig pipeline lockdown via static readonly AssistantContentPipeline = MarkdownPipelineBuilder().DisableHtml().Build() field; RenderContent uses 2-arg Markdown.ToHtml(content, AssistantContentPipeline). DisableHtml() requires @using Markdig directive (not just Markdig.Renderers) because it is an extension method in the Markdig namespace. Reusable pattern for any future Razor surface rendering untrusted markdown.
- Phase 2 Plan 5: Fixture-driven Theory + AI-08 prompt-injection live test gate. 5 prompts + 5 goldens with FixtureGoldenSchema strong-typing; both live test classes [Trait('Category', 'RequiresApiKey')]-tagged. Offline CI gate (--filter 'Category!=RequiresApiKey') skips; milestone-verification command is ANTHROPIC_API_KEY=... dotnet test. Phase 2 success criterion #1 is now mechanically verifiable.
- Phase 2 Plan 5: AI-SPEC §1b validator warnings shipped (OrphanIngredient + EmptySection) — surface as ValidationWarning entries; never flip IsValid; preserve orchestrator repair-loop semantics. Reuses Phase 1 IngredientLink regex; ~67 LOC added to RecipeValidator.
- Phase 2 Plan 5: Live test stack constructs AnthropicAiService directly with Options.Create(CookBotSettings) + RecipeValidator (the real production constructor; plan-text TestStubCurrentUserService was wrong — no ICurrentUserService dep exists). Real-stack mocking is intentional: these tests are milestone-verification gates, not unit tests.

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
Stopped at: Phase 3 context gathered
Resume file: --resume-file

**Planned Phase:** 3 (Editor UX Without Special Syntax) — 4 plans — 2026-04-26T17:11:34.506Z
