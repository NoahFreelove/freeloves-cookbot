---
gsd_state_version: 1.0
milestone: v1.5
milestone_name: External Agent Interface (MCP + REST API)
status: in_progress
last_updated: "2026-06-26T00:00:00.000Z"
last_activity: 2026-06-26
progress:
  total_phases: 6
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
  percent: 0
---

# Project State

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-06-25)

**Core value:** A durable home for the recipes the user actually cooks, captured in one standardized format that round-trips cleanly between AI generation, manual editing, cooking mode, and import/export — without the user (or the AI) having to know special syntax.

**Current focus:** v1.5 — External Agent Interface (MCP + REST API) — Phase 17 next

## Current Position

Phase: 17 (Token Auth + Identity Plumbing) — not started
Plan: —
Status: Roadmap defined; ready to plan Phase 17
Last activity: 2026-06-26 — Roadmap v1.5 written (Phases 17–22, 30 reqs, 6 phases)

```
v1.5 progress: [··········] 0% (0/6 phases)
Phase 17 ░░░ Phase 18 ░░░ Phase 19 ░░░ Phase 20 ░░░ Phase 21 ░░░ Phase 22 ░░░
```

## v1.5 Phase Summary

| Phase | Goal | Requirements | Status |
|-------|------|--------------|--------|
| 17. Token Auth + Identity Plumbing | First machine-auth surface secured — hashed bearer token → `IAgentContext` → acting user before any op | AAUTH-01, 02, 03, 05; ASEC-01, 05 (6 reqs) | Not started |
| 18. Agent-Operations Facade + Pantry Ops | Single Application-layer facade closes `PantryService` authz gap; full pantry + read surface | APANTRY-01..05; AREAD-01..03; ASEC-02 (9 reqs) | Not started |
| 19. Structured Recipe Submission | Agent POSTs `RecipeDocument` → upcast → validate → convert → persist; SSRF + XSS hardened | ARECIPE-01..04; ASEC-03, 04 (6 reqs) | Not started |
| 20. REST Minimal-API Endpoints | All facade ops reachable as HTTP endpoints; RFC 9457 errors; 256 KB cap | AREST-01..03 (3 reqs) | Not started |
| 21. MCP Server | In-process MCP tools on existing Kestrel host; scoped auth per invocation | AMCP-01..04 (4 reqs) | Not started |
| 22. Token Management UI + UAT | Profile token card (create/revoke/list); Playwright harness extended for agent flows | AAUTH-04; UATAUTO-03 (2 reqs) | Not started |

## Deferred Items

Items acknowledged and deferred at v1.4 milestone close on 2026-06-25:

| Category | Item | Status | Note |
|----------|------|--------|------|
| uat | Phase 13 13-HUMAN-UAT.md | passed | 0 pending scenarios — effectively clear; flagged only because the file carries a status. |
| verification | Phase 03 03-GOAL-VERIFICATION.md | gaps_found | Historical v1.1 "Editor UX" phase, absorbed into v1.2; out of v1.4 scope. Already deferred at v1.3 close. Retained as-is for the record. |
| verification | Phase 14 14-VERIFICATION.md | human_needed | Stale doc status. Human UAT ran 2026-06-25: all 10 gallery items PASS; 4 gaps fixed in commit 44db51c. VERIFICATION.md status flag not flipped. |
| verification | Phase 15 15-VERIFICATION.md | human_needed | Stale doc status. Human UAT ran 2026-06-25: 14/15 nutrition items PASS, item 14 (responsive macro grid) fixed; remaining residue is non-automatable (error-state needs fault injection, JSON-LD `image` needs HTTPS host). |

## Shipped Milestones

| Milestone | Shipped | Phases | Plans | Reqs | Tag |
|-----------|---------|--------|-------|------|-----|
| v1.4 Recipe Data & Interoperability | 2026-06-25 | 12–16 | 18 | 21/22 | `v1.4` |
| v1.3 Production-Ready & Format Maturity | 2026-06-05 | 8–11 | 39 | all | `v1.3` |
| v1.2 UI Redesign | 2026-04-27 | 5–7 | 16 | 75/75 | `v1.2` |
| v1.1 Canonical Format & AI Conformance (PARTIAL) | 2026-04-25/26 | 1–2 of 4 (3 absorbed; 4 deferred) | 9 of TBD | 30/46 | — (no tag) |
| v1.0 (pre-GSD existing app) | pre-2026-04-25 | — | — | — | — |

## Accumulated Context

### Hard Invariants (carry-forward)

- **Canonical-first reads:** UI surfaces consume `RecipeDocument` directly via `Recipe.CanonicalDocumentJson` + `JsonRecipeSerializer`. Never read `Recipe.IngredientsJson` / `StepsJson` / `IngredientRefs` / `TagsJson` from new code.
- **No auto-rewrite on save:** Step text is never modified by the save path. Explicit chips are the only persisted source of timers and ingredient links.
- **AI structured-output orchestrator:** `IAiRecipeGenerator` + `SecretRedactor` + `PromptInjectionGuard` preserved verbatim — UI consumes them; do not bypass.
- **Three-tier extractor stays deleted:** POLISH-01 invariant — `AiChat.ExtractRecipeContent` is permanently gone.
- **AI-off contract:** Host kill switch `CookBotSettings.AiFeaturesEnabled` AND per-user `UserProfile.AiEnabled` must both be true; gating enforced inside application/data services, not by middleware.
- **MudBlazor stays out:** No MudBlazor, no `Microsoft.Extensions.AI`, no `Newtonsoft.Json`, no `NJsonSchema`.
- **Trusted-LAN auth posture preserved:** No Identity middleware, no OAuth, no public internet exposure. v1.5 adds a deliberate scoped exception: per-agent hashed bearer tokens for the headless agent surface only.
- **Display-only layers never mutate canonical:** Export projectors and the nutrition panel receive `RecipeDocument` and return a string / view model. They never call `RecipeService.UpdateAsync` or set `CanonicalDocumentJson`.
- **Nutrition never stored in CanonicalDocumentJson:** Computed via `NutritionService`, cached in `RecipeNutritionCache`. AI must never emit nutrition.
- **Photo paths never stored in CanonicalDocumentJson:** `RecipePhoto` entity table owns file paths. Stripped from `.cookbook.json` exports.
- **One new NuGet for v1.5:** `ModelContextProtocol.AspNetCore` 1.4.0 (Apache-2.0 → GPL-3.0-compatible). REST + auth + hashing are all BCL/framework — no other new packages.

### v1.5 Hard Constraints (from research)

- **`IAgentContext` is the agent identity lane.** `CurrentUserService` (Blazor circuit-bound) is never touched on the agent HTTP path. Agent middleware must path-guard to `/api/*` and `/mcp/*` to avoid adding DB round-trips to every Blazor SignalR frame.
- **Facade is the ownership-enforcement layer.** `PantryService.AddOrUpdateAsync` / `GetPantryItemsAsync` / `DeductAsync` have NO ownership checks — the facade calls `GetAccessiblePantriesAsync(userId)` before every pantry mutation. This gap must close in Phase 18 before any HTTP surface exposes pantry ops.
- **Token = SHA-256 hex only.** `RandomNumberGenerator.GetBytes(32)` → hex-encode → show once → store hash. Never store raw token. Compare with `CryptographicOperations.FixedTimeEquals`.
- **`RecipeDocumentConverter` is required.** Agent-submitted `RecipeDocument` → `ParsedRecipe` must go through an explicit static helper, not re-serialized to YAML (lossy). Three-boundary rule applies: POCO + converter + `RecipeService` must all change together if a new field is added.
- **Structured-submit invariant:** Agent recipe creation never calls the Anthropic API; accepts only valid `RecipeDocument` JSON (never freeform text/YAML).

### Build Order Dependency Chain

```
Phase 17 (auth plumbing)
    → Phase 18 (facade + pantry ops — closes PantryService authz gap)
        → Phase 19 (recipe submission + security hardening on recipe path)
            → Phase 20 (REST endpoints — pure transport wiring over facade)
                → Phase 21 (MCP server — mirrors REST via AgentMcpTools)
                    → Phase 22 (token UI + full UAT)
```

### Pitfall Guard Summary (baked into success criteria)

- A1 (plaintext token) → Phase 17 SC1: `SELECT TokenHash` returns 64-char hex, not raw token
- A2 (token in URL) → Phase 17 SC2: query-string token returns 401
- A3 (pantry authz gap) → Phase 18 SC1: cross-user pantry access returns 403
- A4 (CurrentUserService reuse) → Phase 17 SC3: 10 concurrent agent requests never cross-wire users
- A5 (ownership fields in body) → Phase 19 SC3: `cookbookId` for another user returns 403
- A6 (stored XSS) → Phase 19 SC5: `<script>` in step text renders escaped
- B1 (SSRF on photoUrl) → Phase 19 SC4: `file:///etc/passwd` returns 422
- B2 (authz drift) → Phase 18 SC1: facade uses `PantryService.GetAccessiblePantriesAsync` as single source
- B3 (MCP SSE unauthenticated) → Phase 21 SC2: no-auth request returns 401
- B5 (body-size DoS) → Phase 20 SC3: 1 MB body returns 413 on agent endpoint
- B7 (weak tokens) → Phase 17: 32-byte entropy tokens
- B8 (Extras bag) → Phase 19 SC2: non-empty `Extras` returns 422
- C3 (admin token issuance) → Phase 22 SC2: token for admin user blocked
- C4 (in-flight MCP revocation) → Phase 21 SC2: re-validate per tool invocation
- D3 (three-boundary field drop) → Phase 19 SC1: round-trip test covers all fields

### Open Research Flags (for `/gsd-plan-phase`, not blockers)

- **Phase 21 (MCP Server):** Verify `ModelContextProtocol.AspNetCore` 1.4.0 creates a DI scope per tool invocation. If SDK uses root/singleton scope, the plan must include the `IServiceScopeFactory` mitigation.
- **Phase 22 (UAT):** Confirm which agent API flows can be automated via the existing Playwright harness vs. which require manual validation (MCP SSE connection particularly).
- **Phase 19 (Recipe Submission):** Audit `RecipeView.razor` and `CookingMode.razor` step text rendering to confirm `DisableHtml` Markdig pipeline coverage — the XSS gate for the phase.

## Session Continuity

Last session: 2026-06-26 — v1.5 roadmap created
Stopped at: Roadmap written (Phases 17–22); ready to plan Phase 17
Resume with: `/gsd-plan-phase 17`

**Next action:** `/gsd-discuss-phase 17` (or `/gsd-plan-phase 17` directly — config has `skip_discuss: false` but Phase 17 is well-specified by research; discuss to confirm plan granularity)

## Operator Next Steps

- Plan Phase 17 with `/gsd-plan-phase 17` (Token Auth + Identity Plumbing)
- Phase 17 has no open research flags — well-documented `AuthenticationHandler<TOptions>` pattern; `ProtectedMcpServer` SDK sample confirms wiring
