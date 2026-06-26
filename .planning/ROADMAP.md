# Roadmap: FreelovesCookBot

## Milestones

- ✅ **v1.0 (pre-GSD existing app)** — codebase mapped in `.planning/codebase/`
- ⏸ **v1.1 Canonical Format & AI Conformance** — Phases 1–2 shipped 2026-04-25/26; Phase 3 absorbed into v1.2; Phase 4 deferred to v1.3+
- ✅ **v1.2 UI Redesign** — Phases 5–7 shipped 2026-04-27, 16 plans, 75/75 reqs ([archive](milestones/v1.2-ROADMAP.md))
- ✅ **v1.3 Production-Ready & Format Maturity** — Phases 8–11 shipped 2026-06-05, 39 plans ([archive](milestones/v1.3-ROADMAP.md))
- ✅ **v1.4 Recipe Data & Interoperability** — Phases 12–16 shipped 2026-06-25, 18 plans ([archive](milestones/v1.4-ROADMAP.md))
- 🔧 **v1.5 External Agent Interface (MCP + REST API)** — active / in-progress (Phases 17–22)

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

<details>
<summary>✅ v1.3 Production-Ready & Format Maturity (Phases 8–11) — SHIPPED 2026-06-05</summary>

- [x] Phase 8: Format Foundation (13/13 plans) — V2→V3 canonical schema bump, LegacyRecipeProjector deletion, TagsJson→RecipeTag, prompt-snapshot test, README format section — completed 2026-05-16
- [x] Phase 9: Photos + Prod-Ready Infrastructure (7/7 plans) — file upload + paste-URL safety, Docker + compose, encrypt-at-rest API key, token-cost telemetry, README deploy docs — completed 2026-05-16
- [x] Phase 10: QOL, Polish & Consumer Surfaces (14/14 plans) — scored pantry-match, AI-Chat raw-edit recovery, accent picker, prompt editor, token-cost widget, 5 polish items — completed 2026-05-17
- [x] Phase 11: v1.3 UAT Cleanup & Automated UAT Harness (5/5 plans) — CLEANUP-01..04 (Edit clip, responsive ≤720px, sidebar, unit-system display conversion) + reusable Playwright UAT harness — completed 2026-06-05

Full details: [`milestones/v1.3-ROADMAP.md`](milestones/v1.3-ROADMAP.md) · [requirements](milestones/v1.3-REQUIREMENTS.md)

</details>

<details>
<summary>✅ v1.4 Recipe Data & Interoperability (Phases 12–16) — SHIPPED 2026-06-25</summary>

- [x] Phase 12: Richer Format + v3→v4 Schema Bump (4/4 plans) — ingredient substitutions, equipment list, per-step doneness cues, source/provenance; upcaster chain to v4 + AI prompt/snapshot (FORMAT-01..07) — completed 2026-06-06
- [x] Phase 13: Export & Interoperability (3/3 plans) — Schema.org JSON-LD + Cooklang one-way export; pure display-only Application projectors (INTEROP-01..04) — completed 2026-06-06
- [x] Phase 14: Photo Gallery (4/4 plans) — RecipePhoto entity + multi-upload/reorder/set-hero + RecipeView strip + disk cleanup; EF backfill from Recipe.PhotoUrl; GALLERY-04 AI helper retired after UAT (GALLERY-01..03) — completed 2026-06-07
- [x] Phase 15: Nutrition (Offline CNF) (7/7 plans) — bundled Canadian Nutrient File seed + NutritionService + 5-state per-serving/total panel + Health Canada attribution + JSON-LD nutrition (NUTR-01..06) — completed 2026-06-08
- [x] Phase 16: UAT + Integration — `tests/uat-harness/test16-integration.mjs` runs JSON-LD + Cooklang + nutrition hands-free; UATAUTO-02 partial (format-fields-visible + gallery-upload deferred — Blazor InputFile not Playwright-drivable) — UAT executed via harness

Full details: [`milestones/v1.4-ROADMAP.md`](milestones/v1.4-ROADMAP.md) · [requirements](milestones/v1.4-REQUIREMENTS.md)

</details>

### v1.5 External Agent Interface (MCP + REST API)

- [ ] **Phase 17: Token Auth + Identity Plumbing** - `AgentToken` entity, SHA-256 hashing, `IAgentContext`, auth handler wired to `/api/*` + `/mcp/*`; no business operations exposed yet
- [ ] **Phase 18: Agent-Operations Facade + Pantry Ops** - `AgentOperationsFacade` with ownership-enforced pantry ops (list, items, resolve, add/update, deduct) and full read surface (cookbooks, recipes, grocery lists)
- [ ] **Phase 19: Structured Recipe Submission** - `SubmitRecipeAsync` on facade: upcast → validate → `RecipeDocumentConverter` → `RecipeService.CreateAsync`; SSRF-guard on `photoUrl`; XSS audit on step-text render path
- [ ] **Phase 20: REST Minimal-API Endpoints** - All 8 `/api/agent/` endpoints over the facade; RFC 9457 `application/problem+json` errors; 201 + `Location` on recipe create; 256 KB body cap
- [ ] **Phase 21: MCP Server** - `ModelContextProtocol.AspNetCore` 1.4.0; 7 in-process MCP tools mirroring the facade; `.RequireAuthorization("AgentPolicy")`; `structuredContent` in tool results
- [ ] **Phase 22: Token Management UI + UAT** - Profile token-management card (create/revoke/list); Playwright harness extended for full agent-API flows including 401/403 assertions and MCP smoke check

## Phase Details

### Phase 17: Token Auth + Identity Plumbing
**Goal**: The app's first machine-authenticated write surface is secured before any agent operation is exposed — every `/api/*` and `/mcp/*` request resolves a real user via a hashed bearer token, or returns 401
**Depends on**: Phase 16 (v1.4 complete)
**Requirements**: AAUTH-01, AAUTH-02, AAUTH-03, AAUTH-05, ASEC-01, ASEC-05
**Success Criteria** (what must be TRUE):
  1. An `AgentToken` row stores only a 64-char SHA-256 hex hash; `SELECT TokenHash FROM AgentTokens` never returns a raw token string; a DB file leak reveals no usable credential
  2. A request to `/api/agent/pantries` with a valid `Authorization: Bearer <token>` header resolves the owning user and proceeds; the same request without the header returns HTTP 401; a token supplied as `?token=` query-string is rejected with 401 (not 200)
  3. `AgentContext.ActingUserId` is set by the auth middleware and read by downstream handlers; `CurrentUserService` is neither read nor written on the agent path; 10 concurrent agent requests as two different users never return data for the wrong user
  4. `AgentToken.LastUsedAt` is updated on each successful authentication (stale tokens are identifiable)
  5. Blazor circuits (`/_blazor` WebSocket frames) are unaffected by the middleware — no latency regression on the existing UI
**Plans**: TBD

---

### Phase 18: Agent-Operations Facade + Pantry Ops
**Goal**: A single Application-layer `AgentOperationsFacade` enforces per-user ownership on all pantry and read operations — closing the `PantryService` authz gap — so that REST and MCP transports can be layered on top with no duplicated business logic
**Depends on**: Phase 17
**Requirements**: APANTRY-01, APANTRY-02, APANTRY-03, APANTRY-04, APANTRY-05, AREAD-01, AREAD-02, AREAD-03, ASEC-02
**Success Criteria** (what must be TRUE):
  1. The facade calls `PantryService.GetAccessiblePantriesAsync(userId)` before every pantry read or write; a token for User A targeting User B's pantry returns a 403-equivalent error (never 200 or silent data leak)
  2. An agent can list accessible pantries (personal first), list a pantry's items, and resolve an ingredient name to a catalogue id via two-step search (`exactMatch?` + `candidates[]`)
  3. An agent can add/update a pantry item (upsert); the response reports `added` vs `replaced`; a non-positive amount is rejected with a structured error
  4. An agent can deduct a pantry item; the facade returns `ITEM_NOT_FOUND`, `UNIT_MISMATCH` (pre-checked via `IUnitConverter.CanConvert`), or `INSUFFICIENT_STOCK` instead of the current silent no-op
  5. An agent can read the user's cookbooks, accessible recipes (by cookbook or flat), and grocery list(s) read-only; all scoped to owned/shared resources only
**Plans**: TBD

---

### Phase 19: Structured Recipe Submission
**Goal**: An agent can POST a canonical `RecipeDocument` JSON and have it schema-validated, upcasted if needed, converted losslessly to `ParsedRecipe`, and persisted via the existing `RecipeService.CreateAsync` — with no inbound AI call and no stored-XSS or SSRF exposure
**Depends on**: Phase 18
**Requirements**: ARECIPE-01, ARECIPE-02, ARECIPE-03, ARECIPE-04, ASEC-03, ASEC-04
**Success Criteria** (what must be TRUE):
  1. Posting a valid v4 `RecipeDocument` returns a success response with `{ recipeId, name, cookbookId, warnings[], canonicalDocument }` and the recipe is retrievable from the database with all fields intact; posting a v3 document succeeds after upcasting (response includes `submittedVersion`/`persistedVersion`/`upcasted` transparency)
  2. Posting an invalid document returns HTTP 422 with structured machine-readable errors (`{ path, code, message }`) and nothing is persisted; `warnings` are included in successful responses without blocking the operation
  3. A `cookbookId` belonging to a different user returns 403; an omitted `cookbookId` uses the user's first cookbook; no cookbook returns a `NO_COOKBOOK` structured error (never auto-creates a cookbook)
  4. A `photoUrl` of `"file:///etc/passwd"` or an `http://169.254.169.254/...` SSRF address returns 422 (not 200); a valid HTTPS image URL is accepted and normalized via `RecipePhotoUrlValidator`
  5. A recipe with `<script>alert(1)</script>` in step text renders the tag as escaped text (not executed) in both `RecipeView.razor` and `CookingMode.razor`; the `DisableHtml` Markdig pipeline covers the agent-submitted path
**Plans**: TBD

---

### Phase 20: REST Minimal-API Endpoints
**Goal**: All facade operations are reachable over HTTP as the app's first non-Blazor endpoints — consistent `application/problem+json` error shapes, a 256 KB body cap, and the same auth + ownership the facade provides; no business logic duplicated in handlers
**Depends on**: Phase 19
**Requirements**: AREST-01, AREST-02, AREST-03
**Success Criteria** (what must be TRUE):
  1. All 8+ endpoints exist under `/api/agent/` (pantries, items, ingredients/resolve, cookbooks, recipes, grocery); each handler does only: confirm `IsAuthenticated` → deserialize → call facade → map response; no ownership logic lives in the handlers
  2. Validation failures return HTTP 422 with `application/problem+json` and an `errors` map + `warnings`; business errors (`INSUFFICIENT_STOCK`, `UNIT_MISMATCH`) return 422 with a `code`; recipe create returns HTTP 201 with a `Location: /recipes/{id}` header; auth failures return 401/403/404 as appropriate
  3. A POST to an agent endpoint with a 1 MB body returns HTTP 413 while a photo upload of the same size still succeeds on the Blazor endpoint (per-route 256 KB cap, not the global 12 MB Kestrel limit)
**Plans**: TBD

---

### Phase 21: MCP Server
**Goal**: The same facade operations are exposed as a set of in-process MCP tools on the existing Kestrel host — authenticated, scoped per invocation, with complete tool descriptions and `structuredContent` results — without a separate process or port
**Depends on**: Phase 20
**Requirements**: AMCP-01, AMCP-02, AMCP-03, AMCP-04
**Success Criteria** (what must be TRUE):
  1. `ModelContextProtocol.AspNetCore` 1.4.0 is added; the MCP server is reachable at `/mcp` on the same Kestrel host and port as Blazor; no separate process or port is required
  2. Connecting to `/mcp` without an `Authorization: Bearer` header returns HTTP 401; a valid token resolves the acting user and all tool calls run under that user's identity; token revocation is re-validated on each tool invocation (not only at SSE connect time)
  3. All 7 tools (`list_pantries`, `list_pantry_items`, `resolve_ingredient`, `add_pantry_item`, `deduct_pantry_item`, `list_cookbooks`, `create_recipe`) carry complete descriptions (purpose, call-order prerequisites, limitations) and per-parameter input-schema descriptions; tool results include both a human-readable text summary and `structuredContent`
  4. Destructive or structural operations (clear-pantry, delete recipe, update recipe, membership management) are not exposed as tools
**Plans**: TBD
**Research flag**: Verify `ModelContextProtocol.AspNetCore` 1.4.0 creates a DI scope per tool invocation during phase planning — if SDK uses root/singleton scope, the plan must include the `IServiceScopeFactory` mitigation. Also confirm which MCP flows the Playwright harness can automate vs. what requires manual validation.

---

### Phase 22: Token Management UI + UAT
**Goal**: A user can manage their own agent tokens from the Profile page without DB access, and the full end-to-end agent interface is validated hands-free (REST flows) and via smoke check (MCP)
**Depends on**: Phase 21
**Requirements**: AAUTH-04, UATAUTO-03
**Success Criteria** (what must be TRUE):
  1. The Profile page shows a token-management card listing all tokens by label + created date + last-used date (plaintext never shown after creation); a user can create a named token (plaintext shown once in a dialog), and revoke any of their own tokens
  2. Token creation is blocked for admin users (cannot map an agent token to a `CookBotAdmin` account)
  3. The Playwright harness provisions a token, drives all REST endpoints (list pantries, resolve ingredient, add/deduct with each error case, list cookbooks, create recipe, read recipe back), asserts 401 without a token, asserts 403 on cross-user pantry access, and includes an MCP SSE smoke check where Playwright-automatable
  4. All 12 "Looks Done But Isn't" checklist items from PITFALLS.md pass (token hash storage, constant-time compare, header-only auth, pantry authz, recipe authz, XSS, SSRF, MCP auth, CurrentUserService isolation, body-size cap, admin guard, Extras rejection)
**Plans**: TBD
**UI hint**: yes
**Research flag**: Confirm which MCP flows can be automated via Playwright/Node vs. which require manual validation (MCP SSE connection particularly).

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
| 8. Format Foundation | v1.3 | 13/13 | Complete | 2026-05-16 |
| 9. Photos + Prod-Ready Infrastructure | v1.3 | 7/7 | Complete | 2026-05-16 |
| 10. QOL, Polish & Consumer Surfaces | v1.3 | 14/14 | Complete | 2026-05-17 |
| 11. v1.3 UAT Cleanup & Automated UAT Harness | v1.3 | 5/5 | Complete | 2026-06-05 |
| 12. Richer Format + v3→v4 Schema Bump | v1.4 | 4/4 | Complete | 2026-06-06 |
| 13. Export & Interoperability | v1.4 | 3/3 | Complete | 2026-06-06 |
| 14. Photo Gallery | v1.4 | 4/4 | Complete | 2026-06-07 |
| 15. Nutrition (Offline CNF) | v1.4 | 7/7 | Complete | 2026-06-08 |
| 16. UAT + Integration | v1.4 | — | Complete (UAT via harness) | 2026-06-25 |
| 17. Token Auth + Identity Plumbing | v1.5 | 0/TBD | Not started | — |
| 18. Agent-Operations Facade + Pantry Ops | v1.5 | 0/TBD | Not started | — |
| 19. Structured Recipe Submission | v1.5 | 0/TBD | Not started | — |
| 20. REST Minimal-API Endpoints | v1.5 | 0/TBD | Not started | — |
| 21. MCP Server | v1.5 | 0/TBD | Not started | — |
| 22. Token Management UI + UAT | v1.5 | 0/TBD | Not started | — |

---

*v1.4 shipped 2026-06-25 (tag `v1.4`) — Phases 12–16, 18 plans, 21/22 requirements (GALLERY-04 retired; UATAUTO-02 partial). Full detail archived to [`milestones/v1.4-ROADMAP.md`](milestones/v1.4-ROADMAP.md). v1.5 roadmap defined 2026-06-26 (Phases 17–22, 30 requirements, 6 phases).*

## Backlog

### Phase 999.1: RecipeView Cook button missing — TopBarService navigation race ✅ RESOLVED 2026-05-23

**Goal:** Fix `CbTopBarService` so the TopBar.RightSlot survives a route change to a page that re-sets the slot in `OnInitialized` (RecipeView, RecipeEditor).
**Status:** Resolved 2026-05-23 — see commit history.

**Reproducer (now fixed):** Generate a recipe in AiChat → save → navigate to `/recipes/{id}` (RecipeView). Cook / Edit / Share / Schedule buttons were absent from both `TopBar.RightSlot` (≥721px viewport) and the inline `.recipe-actions-inline-fallback` row (≤720px viewport).

**Actual root cause** (opposite of original hypothesis): Diagnostic traces showed `NavigationManager.LocationChanged` fires ~4ms *AFTER* the new page's `OnInitialized` returns, not before. The original D-57 auto-clear was wiping the slot the new page had just set. Fix: `SetRightSlot` now stamps the URL it was called at, and `HandleLocationChanged` preserves the slot when the destination URL matches the stamp (slot belongs to this page); clears only when URL differs (stale slot from prior page).

> **999.2, 999.3, 999.4, 999.5 promoted to Phase 11** (2026-06-05, `/gsd-progress --next`).
> Full reproducers, suspects, and notes preserved in
> `phases/11-v1.3-uat-cleanup/11-BACKLOG-SOURCE.md` and summarized as Phase 11
> success criteria above (CLEANUP-01..04). Only the resolved 999.1 record is kept here.
