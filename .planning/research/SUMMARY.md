# Project Research Summary

**Project:** FreelovesCookBot v1.5 — External Agent Interface (MCP + REST API)
**Domain:** Adding a headless agent-facing API (REST + MCP) to an existing .NET 10 Blazor Server app
**Researched:** 2026-06-26
**Confidence:** HIGH (all four files grounded in direct codebase inspection + verified package sources)

---

## Executive Summary

FreelovesCookBot v1.5 introduces the first stateless HTTP surface and the first machine-authenticated write path to an app that has operated exclusively on Blazor Server/SignalR circuits up to v1.4. The research is strongly convergent across all four agents: one new NuGet package (`ModelContextProtocol.AspNetCore` 1.4.0, Apache-2.0, GPL-3.0-compatible) is sufficient to host an in-process MCP server on the existing Kestrel instance alongside Blazor; REST is built-in minimal API; and bearer-token auth is built-in `AuthenticationHandler<TOptions>` with opaque SHA-256-hashed tokens — no JWT, no Identity, no OAuth packages required. The recommended build order is: token-auth plumbing → agent-operations facade (the ownership-enforcement layer) → pantry operations → structured recipe submission → REST endpoints → MCP server → admin token-management UI → UAT.

The single most load-bearing architectural decision in the milestone is the **headless-identity solution**: a new request-scoped `IAgentContext` (set by auth middleware, never touching the Blazor-owned `CurrentUserService`) that the agent-operations facade reads to obtain the acting `userId`. Every facade method accepts an explicit `int userId` parameter rather than resolving identity from ambient state — this keeps the facade unit-testable, avoids captive-dependency pitfalls, and prevents the `CurrentUserService` mutable-property pattern from leaking into the HTTP request pipeline. The Blazor circuit path and the agent HTTP path are entirely parallel identity lanes that converge only at the service layer (`PantryService`, `RecipeService`) via the same explicit `userId` threading pattern both paths already use.

The top security concern the research surfaces is not from MCP complexity but from a pre-existing gap in the Blazor safety net: `PantryService.AddOrUpdateAsync` and `GetPantryItemsAsync` accept a bare `pantryId` with no ownership check, because the Blazor UI enforced access implicitly by only displaying the user's own pantries. The agent facade is the first caller that can supply an arbitrary `pantryId`, making it the ownership-enforcement layer for all pantry mutations. The research is explicit: the facade must call `GetAccessiblePantriesAsync(userId)` and validate the incoming `pantryId` before every pantry read or write — this must be implemented before REST or MCP endpoints are wired up.

---

## Key Findings

### Recommended Stack

One new NuGet: `ModelContextProtocol.AspNetCore` 1.4.0 (Apache-2.0). It exposes `AddMcpServer().WithHttpTransport(o => o.Stateless = true).WithTools<T>()` for DI registration and `app.MapMcp("/mcp").RequireAuthorization("AgentPolicy")` for routing — identical pattern to the existing `app.MapHealthChecks("/healthz")`. The transitive `ModelContextProtocol` 1.4.0 package is pulled in automatically; do not add it explicitly. All `Microsoft.Extensions.*` transitive packages are already in `Microsoft.AspNetCore.App` — no version conflicts expected on .NET 10.

REST endpoints, bearer-token auth, token hashing, and constant-time comparison are all BCL/framework features: `app.MapGet/MapPost`, `AuthenticationHandler<TOptions>`, `SHA256.HashData`, and `CryptographicOperations.FixedTimeEquals`. `AuthenticationHandler<TOptions>` is required (not hand-rolled middleware) because it produces a `ClaimsPrincipal` that makes `RequireAuthorization("AgentPolicy")` work natively on both `MapMcp()` and `MapGet/Post` chains. Tokens are opaque 32-byte random values stored as SHA-256 hex — PBKDF2 is not appropriate here (that is for human passwords; these are full-entropy random tokens).

**Core technologies:**
- `ModelContextProtocol.AspNetCore` 1.4.0: in-process MCP server on existing Kestrel host — only supported mechanism without a separate process
- `AuthenticationHandler<TOptions>` (built-in): bearer-token auth scheme — integrates natively with `RequireAuthorization()`
- `SHA256.HashData` + `CryptographicOperations.FixedTimeEquals` (BCL): token hashing and constant-time comparison — mirrors `CurrentUserService.VerifyHash` idiom
- `app.MapGet/MapPost` (built-in minimal API): REST endpoints — same pattern as existing `/healthz`
- `JsonSchema.Net` (already installed): schema validation of agent-submitted `RecipeDocument` — no new package

**What NOT to add:**
- `Microsoft.AspNetCore.Authentication.JwtBearer` — tokens are opaque, not JWTs
- `Microsoft.Extensions.AI` — hard project constraint; existing `AnthropicAiService` HttpClient is sufficient
- `ModelContextProtocol` (standalone, no AspNetCore) — pulled in transitively; do not reference directly
- Any OAuth/OIDC server package — explicit out-of-scope (PROJECT.md)

### Expected Features

**Must have (table stakes):**
- `AgentToken` entity: `TokenHash` (SHA-256 hex), `UserId` FK, `Label`, `CreatedAt`, `LastUsedAt?`, `RevokedAt?` — bearer token to user mapping
- `IAgentContext` / `AgentContext`: request-scoped identity accessor populated by auth middleware; `CurrentUserService` left untouched
- `AgentOperationsFacade` (Application layer): single entry point wrapping `PantryService` + `RecipeService` with explicit `int userId` threading and pantry-access ownership guard
- Pantry ops: list accessible pantries, list items (access-guarded), resolve ingredient name to id, add/update item (upsert), deduct item (with INSUFFICIENT_STOCK / UNIT_MISMATCH / ITEM_NOT_FOUND pre-checks)
- Recipe creation: `POST RecipeDocument` → upcast → `RecipeValidator` → `RecipeDocumentConverter.ToParsedRecipe` → `RecipeService.CreateAsync` → return `{ recipeId, name, cookbookId, warnings, canonicalDocument }`
- List cookbooks (needed before recipe create for `cookbookId`)
- MCP server: 7 tools (`list_pantries`, `list_pantry_items`, `resolve_ingredient`, `add_pantry_item`, `deduct_pantry_item`, `list_cookbooks`, `create_recipe`) with per-tool descriptions covering purpose, call order, limitations, and per-param descriptions
- REST API: 8 endpoints under `/api/agent/` with `application/problem+json` error shape (RFC 9457), 422 for business-logic errors, 201 + `Location` header on recipe create
- Token management UI in Profile: create (show plaintext once), list (label + created date), revoke

**Should have (include in same phases, high value / low complexity):**
- Token `Label` and `LastUsedAt` timestamp
- `structuredContent` alongside text content in MCP tool results
- Echo `canonicalDocument` in recipe creation response
- Upcasting transparency (`submittedVersion`, `persistedVersion`, `upcasted`) in creation response
- `GET /api/agent/recipes/{id}` to read a created recipe back

**Defer to v1.5.x or v1.6:**
- Token expiry dates (`ExpiresAt`)
- Batch add/deduct operations
- Pantry availability check for a recipe (`CheckAvailabilityForRecipeAsync`)
- `outputSchema` on MCP tools (SDK support unclear)
- `computeNutritionAfterCreate` option on recipe submission
- `GET /api/agent/pantries/{id}/availability?recipeId={id}`
- Scoped token permissions (read-only vs write bitmask)

**Anti-features (exclude entirely from v1.5):**
- Destructive ops on the agent surface: `ClearPantry`, `TryDeleteOwnedPantry`, delete recipe, update recipe
- AI generation on the inbound MCP/REST path (agent submits a finished `RecipeDocument`, never a freeform prompt)
- Fuzzy/auto-create ingredient resolution on pantry ops (two-step resolve-then-id only; auto-create is intentionally preserved for `RecipeService.CreateAsync`)
- Accepting ingredient names directly on mutating pantry operations (must pass `ingredientId` from a prior `resolve_ingredient` call)

### Architecture Approach

The architecture is a clean three-layer fan-out: a single `AgentOperationsFacade` in the Application layer receives calls from both `AgentEndpoints` (REST minimal-API) and `AgentMcpTools` (MCP tool class) in the Web layer. The facade takes explicit `int userId` parameters — never injecting `CurrentUserService` — and enforces pantry ownership by calling `PantryService.GetAccessiblePantriesAsync(userId)` before any pantry mutation. Auth middleware runs only for `/api/*` and `/mcp/*` paths to avoid adding DB round-trips to every Blazor SignalR frame. A new `RecipeDocumentConverter` static helper provides the lossless `RecipeDocument → ParsedRecipe` mapping the agent submit path needs (routing through the YAML text parser would be lossy and semantically wrong for structured input).

**Major components:**
1. `AgentToken` entity (Domain) + EF migration — token hash storage, user mapping
2. `IAgentContext` / `AgentContext` (Application/Web) — request-scoped headless identity, separate lane from `CurrentUserService`
3. `AgentTokenAuthHandler` (Web, `AuthenticationHandler<TOptions>`) — token resolution middleware populating `ClaimsPrincipal` + `AgentContext`
4. `AgentOperationsFacade` (Application) — ownership-enforcement layer; wraps `PantryService` + `RecipeService`; all agent business logic lives here
5. `RecipeDocumentConverter` (Application, static helper) — lossless `RecipeDocument → ParsedRecipe` field mapping
6. `AgentEndpoints` (Web, minimal-API) — REST route registration; handlers auth-check, deserialize, call facade, map response
7. `AgentMcpTools` (Web, `[McpServerToolType]`) — MCP tool class mirroring `AgentEndpoints`; delegates to facade

**Key patterns:**
- Explicit `userId` threading through every facade method (never ambient identity)
- Path-prefix guard on auth middleware (`/api/*`, `/mcp/*` only — skip `/_blazor`)
- `PantryService.GetAccessiblePantriesAsync` as single source of truth for pantry access; never duplicate ownership checks inline
- `RecipeService.CreateAsync` ownership check (`cookbook.UserId != userId`) as defense-in-depth, with a pre-check in the facade
- Tokens stored as SHA-256 hex only; raw token shown once at issuance, never persisted
- `.WithTools<AgentMcpTools>()` explicit registration (not `WithToolsFromAssembly()`)

### Critical Pitfalls

1. **Pantry authz gap** (CRITICAL) — `PantryService.AddOrUpdateAsync` / `GetPantryItemsAsync` / `DeductAsync` have NO ownership check. The facade is the first caller that can supply an arbitrary `pantryId`. Prevention: facade calls `GetAccessiblePantriesAsync(userId)` and validates the incoming `pantryId` before every pantry op. Integration test: token for User A targeting User B's pantry must return 403.

2. **MCP SSE endpoint open by default** (CRITICAL) — `app.MapMcp("/mcp")` is unauthenticated unless `.RequireAuthorization("AgentPolicy")` is chained and `UseAuthentication()` + `UseAuthorization()` precede it in the pipeline. Prevention: chain `.RequireAuthorization("AgentPolicy")` on `MapMcp()`; test connecting without a token and assert HTTP 401.

3. **Bearer token stored as plaintext** (CRITICAL) — store only `SHA-256(token)` as hex in `AgentToken.TokenHash`. Generate with `RandomNumberGenerator.GetBytes(32)`, show raw value once, never persist raw token anywhere including logs. Compare with `CryptographicOperations.FixedTimeEquals` (copy `CurrentUserService.VerifyHash` idiom).

4. **`CurrentUserService` reuse for stateless HTTP** (HIGH) — `CurrentUserService.CurrentUserId` is a mutable property set by Blazor circuit initialization; it is null in the HTTP request pipeline. Introducing `IAgentContext` / `AgentContext` as a separate request-scoped identity accessor prevents identity cross-wiring.

5. **Stored XSS via agent-submitted recipe step text** (HIGH) — `Markdig DisableHtml` is applied in `AiChat.razor` but may not be applied in `RecipeView.razor` / `CookingMode.razor` for step text rendered as `MarkupString`. Prevention: audit all components that render step text as HTML; apply the existing `DisableHtml` Markdig pipeline consistently. UAT assertion: submit `<script>alert(1)</script>` as step text; verify it renders escaped.

6. **`PhotoUrl` SSRF bypass** (HIGH) — the existing `RecipePhotoUrlValidator` (scheme allowlist) is wired in `RecipePhotoService.AddPhotoAsync` and the AI path but NOT in the agent submit path. The facade must call `RecipePhotoUrlValidator.TryValidate(doc.PhotoUrl, ...)` before constructing `ParsedRecipe`. Do not call `PhotoUrlHeadValidator.ValidateAsync` (outbound HTTP; too slow for the submit path).

---

## Implications for Roadmap

The research is unanimous on build order: foundation before facade, facade before transports, transports before UI. No phase can safely swap position because each depends on the layer below it. Suggested 6-phase structure (continuing after Phase 16):

### Phase 17: Token Auth + Identity Plumbing

**Rationale:** Every other v1.5 feature requires an authenticated acting user. This is the prerequisite with zero alternatives — no REST, no MCP, no facade can exist without it. The threat model shift (first machine-authenticated writes) makes this the highest-risk phase.

**Delivers:** `AgentToken` entity, EF migration (`AddAgentTokens`), `IAgentContext` / `AgentContext`, `AgentTokenAuthHandler` wired into `Program.cs` (`AddAuthentication`, `AddAuthorization`, `UseAuthentication`, `UseAuthorization`). Endpoints return 401 for unauthenticated requests to `/api/*` and `/mcp/*` paths. No agent operations exposed yet.

**Avoids:** Pitfalls A1 (plaintext storage), A2 (token in URL), A4 (CurrentUserService reuse), B7 (weak token entropy), C3 (admin token issuance), B6 (schema-only migration — no data backfill).

**Research flags:** Well-documented patterns (built-in `AuthenticationHandler<TOptions>`); no additional research needed.

---

### Phase 18: Agent-Operations Facade + Pantry Ops

**Rationale:** The facade is the ownership-enforcement layer and must exist before any transport exposes pantry operations. The pantry authz gap (Pitfall A3) is the most dangerous security issue in the milestone — it must be closed here, not deferred to the REST or MCP phases.

**Delivers:** `AgentOperationsFacade` (Application layer), `RecipeDocumentConverter` (static helper), pantry operation methods (`ListAccessiblePantries`, `ListPantryItems`, `AddOrUpdatePantryItem`, `DeductPantryItem`, `ResolveIngredient`) with ownership guards and deduct pre-checks (INSUFFICIENT_STOCK / UNIT_MISMATCH / ITEM_NOT_FOUND). Unit-tested in isolation (no HTTP stack needed).

**Avoids:** Pitfalls A3 (pantry authz gap via `GetAccessiblePantriesAsync`), B2 (authz drift — single source of truth in `PantryService`), D3 (three-boundary field drop — `RecipeDocumentConverter` as explicit mapping).

**Research flags:** No additional research needed — directly wraps existing `PantryService` methods whose signatures are fully documented.

---

### Phase 19: Structured Recipe Submission

**Rationale:** Recipe creation is independent of pantry ops at the facade level. Separating it allows focused attention on the validation pipeline (upcast → validate → convert → persist) and recipe-specific security surface (ownership fields in body, `photoUrl` SSRF, stored XSS in step text, `Extras` bag).

**Delivers:** `SubmitRecipeAsync` on the facade (upcast → `RecipeValidator` → `RecipeDocumentConverter` → `RecipeService.CreateAsync`), `photoUrl` SSRF guard via `RecipePhotoUrlValidator`, `Extras` rejection (return 422 for non-empty `Extras`), `list_cookbooks` / `ListCookbooks` on facade. Echo `canonicalDocument` and upcasting transparency in response.

**Avoids:** Pitfalls A5 (ownership fields in body), A6 (stored XSS — audit Markdig pipeline in `RecipeView`/`CookingMode`), B1 (SSRF on `photoUrl`), B8 (`Extras` bag persistence), D2 (max-ingredient count cap), D3 (three-boundary converter).

**Research flags:** Markdig `DisableHtml` audit in `RecipeView.razor` / `CookingMode.razor` needed during planning — this is the XSS gate for the phase.

---

### Phase 20: REST Minimal-API Endpoints

**Rationale:** With the facade complete and tested, the REST layer is pure transport wiring: deserialize request → call facade → map result to HTTP response shape. All business logic and ownership checks are already in the facade. This phase introduces the first non-Blazor HTTP endpoints in the app.

**Delivers:** `AgentEndpoints` extension method with all 8 REST endpoints under `/api/agent/`, `application/problem+json` error shape (RFC 9457), 201 + `Location` header on recipe create, per-route `RequestSizeLimitAttribute(256 * 1024)`, `GET /api/agent/recipes/{id}`, `GET /api/agent/health` (authenticated readiness check).

**Avoids:** Pitfalls B5 (body-size DoS), C1 (no global CORS), C2 (no separate port — same Kestrel endpoint as Blazor).

**Research flags:** Standard ASP.NET Core minimal-API patterns; no additional research needed.

---

### Phase 21: MCP Server

**Rationale:** The MCP server mirrors the REST API via `AgentMcpTools` but requires `ModelContextProtocol.AspNetCore` 1.4.0 and specific `Program.cs` wiring. Keeping it separate from REST allows MCP-specific concerns (SSE session management, scope-per-invocation verification, Kestrel/proxy timeout configuration, tool description quality) to be addressed without complicating the REST phase.

**Delivers:** `ModelContextProtocol.AspNetCore` 1.4.0 added to `CookBot.Web.csproj`, `AgentMcpTools` class with all 7 `[McpServerTool]` methods (each with purpose + when-to-use + limitations + per-param descriptions), `AddMcpServer().WithHttpTransport(o => o.Stateless = true).WithTools<AgentMcpTools>()` in `Program.cs`, `app.MapMcp("/mcp").RequireAuthorization("AgentPolicy")`, `structuredContent` alongside text in tool results, Kestrel SSE timeout configuration.

**Avoids:** Pitfalls B3 (MCP SSE unauthenticated — `.RequireAuthorization()` mandatory), B4 (SSE keep-alive / reconnect storm), C4 (revocation doesn't kill in-flight sessions — re-validate per tool invocation).

**Research flags:** Verify `ModelContextProtocol.AspNetCore` 1.4.0 creates a DI scope per tool invocation during phase planning. If SDK uses a root/singleton scope, the plan must include the `IServiceScopeFactory` mitigation.

---

### Phase 22: Admin Token-Management UI + UAT

**Rationale:** The token management UI is a prerequisite for operating the agent interface without DB access. Combined with UAT to validate the full end-to-end flow (token creation → pantry op → recipe creation via both REST and MCP), this is the natural close phase.

**Delivers:** Profile page token-management card (list tokens by label + created date, create dialog showing raw token once, revoke button), admin-user issuance guard in UI, extended Playwright UAT harness covering agent-API flows, "Looks Done But Isn't" checklist validation (12 items from PITFALLS.md).

**Avoids:** Pitfall C3 (admin-user guard in UI token issuance).

**Research flags:** Standard Blazor component work; no additional research needed. Note: Playwright cannot drive Blazor `<InputFile>` uploads (existing MEMORY.md constraint), but REST/MCP endpoint tests are fully Playwright/Node driveable.

---

### Phase Ordering Rationale

- Token auth must come first (Phase 17) — every other phase requires an authenticated acting user
- Facade before transports (Phase 18 before 20/21) — the pantry authz gap must be closed before any HTTP surface exposes pantry mutations; the facade is the enforcement layer
- Recipe submission before REST/MCP (Phase 19 before 20/21) — full facade surface (pantry + recipe) should be complete and tested before transport layers are wired, so transport phases are pure wiring with no business logic decisions
- REST before MCP (Phase 20 before 21) — REST is simpler (no new package) and establishes response shape contracts that MCP tools mirror; working REST endpoints make MCP tool testing easier
- UI + UAT last (Phase 22) — token management UI requires the full auth stack to be wired; UAT validates the complete system

### Research Flags

**Phases needing planning-time investigation:**
- **Phase 21 (MCP Server):** Verify `ModelContextProtocol.AspNetCore` 1.4.0 creates a DI scope per tool invocation. If it uses a root/singleton scope, all scoped services (`AgentOperationsFacade`, `CookBotDbContext`) would be miscaptured. Plan must include this verification and the `IServiceScopeFactory` mitigation if needed.
- **Phase 22 (UAT):** Confirm which agent API flows can be automated via the existing Playwright harness vs. which require manual validation (MCP SSE connection particularly).

**Phases with well-documented patterns (skip `--research-phase`):**
- **Phase 17 (Token Auth):** `AuthenticationHandler<TOptions>` is documented framework pattern; `ProtectedMcpServer` SDK sample confirms wiring.
- **Phase 18 (Facade + Pantry):** Direct codebase wrapping — method signatures fully known.
- **Phase 19 (Recipe Submit):** Reuses existing `RecipeValidator`, `RecipeUpcasterChain`, `RecipeService.CreateAsync` — no new domain unknowns.
- **Phase 20 (REST):** Standard ASP.NET Core minimal-API patterns; existing `/healthz` is the precedent.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Package version (1.4.0), license (Apache-2.0 → GPL-3.0 compatible), .NET targets, and DI wiring verified against NuGet + GitHub. Auth/REST/hashing are BCL/framework — no ambiguity. |
| Features | HIGH | Grounded in direct inspection of `PantryService.cs`, `RecipeService.cs`, `RecipeDocument.cs`, `RecipeValidator.cs`, `IngredientResolver.cs`. Method signatures are exact. MCP tool spec and description best-practices are MEDIUM (official spec + academic preprint). |
| Architecture | HIGH | Based on direct codebase inspection of all named files. The `IAgentContext` pattern is directly grounded in the `CurrentUserService` scoping model. One gap: MCP SDK scope-per-invocation behavior requires runtime verification. |
| Pitfalls | HIGH (security), MEDIUM (MCP transport) | Security pitfalls (A1–A6, B1–B2) are grounded in actual source lines with specific line numbers. MCP transport pitfalls (B3–B4, C4) are based on SDK docs and ASP.NET patterns — MEDIUM because runtime behavior needs verification. |

**Overall confidence: HIGH**

### Gaps to Address

- **MCP SDK scope-per-invocation behavior:** The C# SDK `[McpServerTool]` dispatch mechanism's scope creation behavior is not fully documented. Address during Phase 21 planning by inspecting the SDK source or running a minimal spike.
- **Markdig `DisableHtml` coverage in recipe render components:** Phase 19 planning must audit `RecipeView.razor` and `CookingMode.razor` step text rendering — the XSS gate for the phase.
- **MCP `outputSchema` SDK support:** Defer to v1.5.x; do not block Phase 21 on it.
- **In-flight MCP session revocation:** Pitfall C4 mitigation (re-validate identity per tool invocation) must be in the Phase 21 plan.

---

## Sources

### Primary (HIGH confidence — direct codebase inspection)

- `src/CookBot.Application/Services/PantryService.cs` — exact method signatures, ownership-check absence in `AddOrUpdateAsync` / `GetPantryItemsAsync`, access-list pattern in `GetAccessiblePantriesAsync`
- `src/CookBot.Application/Services/RecipeService.cs` — `CreateAsync` ownership guard, ingredient resolution behavior, `ParsedRecipe` shape
- `src/CookBot.Domain/Recipes/RecipeDocument.cs` — v4 canonical wire shape, `[JsonExtensionData] Extras` pitfall vector
- `src/CookBot.Application/Services/RecipeValidator.cs` — validation error codes and paths
- `src/CookBot.Application/Services/IngredientResolver.cs` — normalization logic
- `src/CookBot.Application/Services/RecipePhotoUrlValidator.cs` — scheme-allowlist validator; SSRF reuse point
- `src/CookBot.Web/Services/CurrentUserService.cs` — mutable `CurrentUserId` property; circuit-scoped DI model; `VerifyHash` constant-time pattern to copy
- `src/CookBot.Web/Program.cs` — existing `MaxRequestBodySize` (12 MB), `/healthz` wiring, DI registration patterns
- `src/CookBot.Web/Components/Pages/AiChat.razor` — `DisableHtml` Markdig pipeline (guard to audit/reuse for agent-submitted step text)

### Secondary (HIGH confidence — official/verified external sources)

- [NuGet: ModelContextProtocol.AspNetCore 1.4.0](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore/) — version, license, .NET targets, dependency chain (verified)
- [GitHub: modelcontextprotocol/csharp-sdk v1.4.0](https://github.com/modelcontextprotocol/csharp-sdk) — stable release 2026-06-04; `ProtectedMcpServer` sample auth pattern
- [MS Learn: Minimal APIs authentication and authorization (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/security?view=aspnetcore-10.0) — `AuthenticationHandler<TOptions>` as idiomatic pattern
- [Apache Software Foundation: Apache License v2.0 and GPL Compatibility](https://www.apache.org/licenses/GPL-compatibility.html) — Apache 2.0 to GPL-3.0-only confirmed compatible
- [RFC 9457 Problem Details for HTTP APIs](https://datatracker.ietf.org/doc/html/rfc9457) — `application/problem+json` shape

### Tertiary (MEDIUM confidence — community sources + SDK docs)

- [MCP Tool Specification (draft)](https://modelcontextprotocol.io/specification/draft/server/tools) — tool naming, descriptions, `isError`, `structuredContent`, `outputSchema`
- [MCP Tool Description Quality Study (arxiv 2602.14878)](https://arxiv.org/html/2602.14878v1) — 856 tools, six description components, 5.85pp task-success improvement
- [DEV.to: Add the MCP server to the ASP.NET Core minimal API](https://dev.to/ohalay/add-the-mcp-server-to-the-aspnet-core-minimal-api-4331) — coexistence with `app.MapEndpoints()` + `RequireAuthorization()` chaining confirmed
- [codewithmukesh.com: API Key Authentication ASP.NET Core .NET 10](https://codewithmukesh.com/blog/api-key-authentication-aspnet-core/) — full handler implementation pattern

---

*Research completed: 2026-06-26*
*Ready for roadmap: yes*
