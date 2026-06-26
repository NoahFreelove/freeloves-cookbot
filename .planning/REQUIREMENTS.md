# Requirements — v1.5 External Agent Interface (MCP + REST API)

**Milestone:** v1.5 | **Defined:** 2026-06-26 | **Source:** user-locked decisions + parallel research (`research/SUMMARY.md`, `FEATURES.md`, `ARCHITECTURE.md`, `PITFALLS.md`, `STACK.md`)

Additive milestone on the stable v1.4 platform. Adds the app's **first stateless HTTP surface** and **first machine-authenticated write path**: external AI agents manage the pantry and create recipes through a shared agent-operations facade exposed as BOTH a REST minimal-API and an in-process MCP server.

**Locked decisions (from the user):**
- **Auth = per-agent bearer token → real user.** Token-hash table + auth handler resolves token→user. NOT ASP.NET Identity / OAuth / SSO. A deliberate, scoped exception to the no-auth invariant — trusted-LAN posture otherwise preserved.
- **Recipe creation = structured-submit only.** Agent POSTs a canonical `RecipeDocument`; server upcasts → validates (`JsonSchema.Net`/`RecipeValidator`, already present) → converts to `ParsedRecipe` → persists via `RecipeService.CreateAsync`. NO inbound Anthropic/AI call.
- **Surface = one shared facade, two transports.** A single Application-layer agent-operations facade over `PantryService`/`RecipeService`/cookbook+grocery reads, wrapped by a REST minimal-API AND an in-process MCP server (HTTP/SSE).
- **Token management = Profile UI card** (create + revoke; plaintext shown once).
- **Read surface = broad** — pantry + cookbooks + recipes + grocery lists.

**Research-locked technical decisions:**
- **One new NuGet only:** `ModelContextProtocol.AspNetCore` 1.4.0 (Apache-2.0 → GPL-3.0-compatible) hosts the MCP server in-process on the existing Kestrel host (`app.MapMcp`). REST = built-in minimal API. Auth = built-in `AuthenticationHandler<TOptions>`. Token hash = SHA-256 + `CryptographicOperations.FixedTimeEquals` (reuse the existing `CurrentUserService.VerifyHash` idiom).
- **Headless identity = new request-scoped `IAgentContext`** set by the auth handler. `CurrentUserService` (circuit-bound) is left untouched and unused on the agent path.
- **Pre-existing authz gap to close:** `PantryService.AddOrUpdateAsync` / `GetPantryItemsAsync` / `DeductAsync` take a bare `pantryId` with no ownership check — the facade is the first caller able to pass an arbitrary id, so it MUST guard via `GetAccessiblePantriesAsync(userId)`. `RecipeService.CreateAsync` already checks ownership.

---

## v1.5 Requirements

### Agent Authentication & Tokens (AAUTH)

- [ ] **AAUTH-01**: An `AgentToken` entity stores a one-way SHA-256 hash of an opaque bearer token plus a label, owning user, created-at, and last-used-at; the plaintext token is shown exactly once at creation and is never persisted or retrievable again (mirrors the existing AI-key "never show twice" pattern).
- [ ] **AAUTH-02**: Every agent REST/MCP request authenticates via `Authorization: Bearer <token>`; the token is hashed and constant-time compared (`CryptographicOperations.FixedTimeEquals`) to resolve the owning user. Missing/invalid/revoked tokens are rejected with 401 before any service call.
- [ ] **AAUTH-03**: The acting user for an agent request is carried by a new request-scoped `IAgentContext` set by the auth handler; `CurrentUserService` (Blazor circuit-bound) is neither read nor mutated on the agent path, and the existing Blazor identity path is unaffected.
- [ ] **AAUTH-04**: A user can create a named agent token and revoke any of their own tokens from a Profile token-management card; the card lists label + created date + last-used (never the plaintext) and shows the plaintext once on creation.
- [ ] **AAUTH-05**: `AgentToken.LastUsedAt` updates on each successful authentication so stale/unused tokens are identifiable for revocation.

### Agent Pantry Operations (APANTRY)

- [ ] **APANTRY-01**: An agent can list the acting user's accessible pantries (owned + member), each with `{ id, name, isPersonal, isOwner }`, personal first.
- [ ] **APANTRY-02**: An agent can list the items of an accessible pantry (`{ ingredientId, ingredientName, amount, unit, expirationDate? }`); the facade rejects any `pantryId` outside the acting user's accessible set (closing the `PantryService` ownership gap).
- [ ] **APANTRY-03**: An agent resolves an ingredient name → catalogue id via a two-step pattern — search returns `exactMatch?` + `candidates[]`; mutating ops accept only `ingredientId` (never bare names); pantry ops never auto-create ingredients (only recipe creation does).
- [ ] **APANTRY-04**: An agent can add/update a pantry item (upsert); the response states whether the amount was `added` (compatible unit) or `replaced` (incompatible unit); a non-positive amount is rejected.
- [ ] **APANTRY-05**: An agent can deduct a pantry item, with the facade returning explicit errors — `ITEM_NOT_FOUND`, `UNIT_MISMATCH` (pre-checked via `IUnitConverter.CanConvert` before calling `DeductAsync`), and `INSUFFICIENT_STOCK` (pre-read) — instead of the current silent no-op / raw-deduct behavior.

### Agent Recipe Creation (ARECIPE)

- [ ] **ARECIPE-01**: An agent can submit a canonical `RecipeDocument` JSON; it is deserialized, upcasted via `RecipeUpcasterChain` if below current version, validated by `RecipeValidator`, and on success persisted via `RecipeService.CreateAsync`; the response returns `{ recipeId, name, cookbookId, warnings[], canonicalDocument }` (echoing the persisted doc + upcasting transparency).
- [ ] **ARECIPE-02**: Validation failures return structured machine-readable errors (`{ path, code, message }`) and do NOT persist; warnings are non-blocking and included in successful responses too.
- [ ] **ARECIPE-03**: An agent targets a cookbook by `cookbookId` (validated owned by the acting user → 404 if missing, 403 if not owned); if omitted, the user's first cookbook is used; if the user has none, a `NO_COOKBOOK` error is returned — a cookbook is never auto-created.
- [ ] **ARECIPE-04**: The agent recipe-creation path never invokes the AI/Anthropic API and never accepts freeform text/YAML — only valid `RecipeDocument` JSON (structured-submit invariant).

### Agent Read Surface (AREAD) — cookbooks, recipes, grocery

- [ ] **AREAD-01**: An agent can list the acting user's cookbooks (`{ id, name, recipeCount }`).
- [ ] **AREAD-02**: An agent can list recipes accessible to the user (by cookbook and/or flat) and fetch a single recipe's current-version canonical document by id, scoped to recipes the user owns or that are shared with them.
- [ ] **AREAD-03**: An agent can read the acting user's grocery list(s) read-only (items + quantities); no grocery mutation in v1.5.

### MCP Server (AMCP)

- [ ] **AMCP-01**: An in-process MCP server is hosted on the existing Kestrel host at a dedicated route (e.g. `/api/mcp`) via `ModelContextProtocol.AspNetCore`, exposing the agent-operations facade as `snake_case` tools — no separate process or port.
- [ ] **AMCP-02**: The MCP endpoint requires authentication (`MapMcp(...).RequireAuthorization()`) — it is never reachable unauthenticated; every tool invocation runs under the request's resolved acting user via the same `IAgentContext`.
- [ ] **AMCP-03**: Each tool carries a complete description (purpose, call-order prerequisites, limitations) and per-parameter input-schema descriptions; tool results return a human-readable text summary plus `structuredContent`.
- [ ] **AMCP-04**: Destructive/structural operations are NOT exposed as tools (no clear-pantry, delete pantry/recipe, update recipe, membership management, or create-pantry).

### REST API (AREST)

- [ ] **AREST-01**: The same facade is exposed as minimal-API endpoints under `/api/agent/...` (pantries, items, ingredients, cookbooks, recipes, grocery), sharing identical auth + authorization with the MCP surface (no duplicated business logic).
- [ ] **AREST-02**: Errors use RFC 9457 `application/problem+json` — validation → 422 with an `errors` map + `warnings`; `INSUFFICIENT_STOCK` / `UNIT_MISMATCH` → 422 with a `code`; access → 401/403/404; recipe create returns 201 + a `Location` header.
- [ ] **AREST-03**: Destructive/partial endpoints are NOT exposed (no DELETE pantry/recipe, no clear, no PATCH recipe, no bulk-create).

### Security Hardening (ASEC) — trusted-LAN → headless-write threat pass

- [ ] **ASEC-01**: Agent tokens are stored only as hashes and compared in constant time; plaintext tokens never appear in the DB, logs, or error surfaces.
- [ ] **ASEC-02**: The facade enforces per-user ownership on every pantry and recipe operation — a valid token grants access only to the mapped user's own/shared resources, never cross-user (verified by an unauthorized-access test).
- [ ] **ASEC-03**: Agent-submitted recipe text renders without HTML/script injection on all recipe surfaces (`RecipeView`, `CookingMode`) — the Markdig `DisableHtml` lockdown (today only in `AiChat.razor`) covers the agent-submitted path (stored-XSS prevention).
- [ ] **ASEC-04**: Agent-submitted `photoUrl` (and any URL inputs) pass the existing SSRF-aware `RecipePhotoUrlValidator` before persistence.
- [ ] **ASEC-05**: Agent endpoints enforce a request body-size cap and the new HTTP/MCP surface inherits the trusted-LAN posture (no new *required* public/internet exposure; bind + document as LAN-only).

### UAT Automation (UATAUTO)

- [ ] **UATAUTO-03**: The `tests/uat-harness/` harness is extended to exercise the agent surface hands-free — provision/seed a token, drive the REST endpoints (list pantries, resolve ingredient, add/deduct with each error case, list cookbooks, create recipe, read recipe back), assert 401 without a token and 403 on cross-user access, plus an MCP smoke check where automatable.

---

## Future Requirements (deferred to v1.5.x / v1.6)

- **Token scoped permissions** (read-only vs write vs recipe-create bitmask) — v1.5 token is user-scoped; per-token least-privilege is a follow-up.
- **Token expiry dates** (`ExpiresAt`) — reduces leaked-token blast radius; defer.
- **Batch add/deduct** pantry ops in one call — useful for "deduct all of a recipe's ingredients"; defer (partial-success complexity).
- **Pantry availability check for a recipe** via agent (`CheckAvailabilityForRecipeAsync`) — meal-planning agents; defer.
- **`set_pantry_item`** (absolute set vs additive) — add only if real agent workflows need it (avoid two-similar-tools confusion).
- **`outputSchema` / `ttlMs` on MCP tools** — SDK attribute support unclear; revisit when the SDK confirms.
- **Nutrition trigger on recipe creation** (`computeNutritionAfterCreate`) — preserve the non-blocking-save invariant by default; defer.
- **Grocery-list mutation** via agent (add/check items) — v1.5 grocery is read-only.

## Out of Scope

- **Recipe update / delete via agent** — concurrency risk (no conflict model) + irreversible disk-file deletion. UI-only.
- **Pantry delete / clear / membership / create-shared-pantry via agent** — destructive or privileged social operations. UI-only.
- **Recipe local-file photo upload via agent** — disk I/O + magic-byte handling stays UI-only; agents may supply an external `photoUrl` on the canonical doc.
- **AI generation on the inbound agent path** — the locked structured-submit decision; no agent endpoint calls Anthropic.
- **OAuth / OIDC / JWT / ASP.NET Identity** — identity complexity exceeds the trusted-LAN threat model; opaque hashed bearer tokens only.
- **SPA / WebAssembly client** — Blazor Server `InteractiveServer` stays the UI; v1.5 adds only a headless agent surface (per PROJECT.md Out-of-Scope, partially reversing the prior "no Web API" line for the agent API/MCP only).
- **Second AI provider / `Microsoft.Extensions.AI` / `Newtonsoft.Json`** — unchanged; System.Text.Json only.
- **Public-internet hardening** (rate-limit gateways, WAF, multi-tenant) — trusted-LAN posture; not in scope.

## Traceability

*(Filled by the roadmapper — every REQ-ID maps to exactly one phase.)*

| REQ-ID | Phase | Status |
|--------|-------|--------|
| AAUTH-01 | TBD | Pending |
| AAUTH-02 | TBD | Pending |
| AAUTH-03 | TBD | Pending |
| AAUTH-04 | TBD | Pending |
| AAUTH-05 | TBD | Pending |
| APANTRY-01 | TBD | Pending |
| APANTRY-02 | TBD | Pending |
| APANTRY-03 | TBD | Pending |
| APANTRY-04 | TBD | Pending |
| APANTRY-05 | TBD | Pending |
| ARECIPE-01 | TBD | Pending |
| ARECIPE-02 | TBD | Pending |
| ARECIPE-03 | TBD | Pending |
| ARECIPE-04 | TBD | Pending |
| AREAD-01 | TBD | Pending |
| AREAD-02 | TBD | Pending |
| AREAD-03 | TBD | Pending |
| AMCP-01 | TBD | Pending |
| AMCP-02 | TBD | Pending |
| AMCP-03 | TBD | Pending |
| AMCP-04 | TBD | Pending |
| AREST-01 | TBD | Pending |
| AREST-02 | TBD | Pending |
| AREST-03 | TBD | Pending |
| ASEC-01 | TBD | Pending |
| ASEC-02 | TBD | Pending |
| ASEC-03 | TBD | Pending |
| ASEC-04 | TBD | Pending |
| ASEC-05 | TBD | Pending |
| UATAUTO-03 | TBD | Pending |
