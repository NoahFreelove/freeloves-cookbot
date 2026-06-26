# Pitfalls Research — v1.5 External Agent Interface (MCP + REST API)

**Domain:** Adding per-agent token auth + stateless HTTP API + in-process MCP server to a trusted-LAN Blazor Server app that has no existing auth middleware and no existing non-Blazor HTTP endpoints.
**Researched:** 2026-06-26
**Confidence:** HIGH (identity mismatch, structured-submit, ownership bypass — grounded in actual `CurrentUserService`, `RecipeService`, `PantryService` source); HIGH (token storage, SSRF reuse — grounded in `RecipePhotoUrlValidator` + `PhotoUrlHeadValidator` source); MEDIUM (MCP SDK transport gotchas — based on C# SDK docs + ASP.NET minimal-API patterns).

---

## Threat-Model Shift

v1.0–v1.4 threat model: trusted-LAN users operating through a browser session. No auth middleware. No HTTP endpoints other than Blazor SignalR and `/healthz`. The only write surface is the Blazor circuit where `CurrentUserService.CurrentUserId` is set by `MainLayout.razor` from `sessionStorage` at circuit start — a browser-bound, human-in-the-loop path.

v1.5 changes three things simultaneously:
1. **First stateless HTTP surface** — REST minimal-API + MCP SSE transport on the same Kestrel host.
2. **First machine-authenticated writes** — a bearer token grants write access to pantry and recipe data without a human browser session.
3. **First un-supervised structured data ingest** — an agent can POST a full `RecipeDocument` without a human reviewing it in a UI.

Each change carries its own pitfall class. The five sections below address them in severity order.

---

## Critical Pitfalls

### Pitfall A1: Bearer token stored as plaintext in the database

**What goes wrong:**
If the `AgentToken` table stores the raw token string, a SQLite file leak (backup exfiltration, Docker volume copy, compromised host) exposes every active credential immediately. Tokens can be replayed to gain write access to every user's pantry and recipes.

**Why it happens:**
The existing `UserProfile.AiApiKey` was stored plaintext until v1.3 Phase 9 (PROD-06) added Data Protection encryption. The token table is new and there is no historical precedent in this codebase for a "must hash" credential — the password path in `CurrentUserService` uses PBKDF2, but that code path is in `CurrentUserService`, not in the token resolution middleware. Developers writing the new token table copy the simpler pattern.

**Why it is worse here:**
AI API keys are per-user and optional — a leak loses one user's billing token. Agent tokens are bearer credentials: a single leaked token grants cross-session write access with no per-request challenge, so the window of exploitation is the full token lifetime rather than a single session.

**Prevention:**
- Store only `SHA-256(token)` in the DB. Generate the raw token at issue time with `RandomNumberGenerator.GetBytes(32)` (256-bit entropy), return it once to the caller, and never store the raw value anywhere — not in logs, not in the debug response body.
- Compare at authentication time using `CryptographicOperations.FixedTimeEquals(SHA256.HashData(incomingBytes), storedHash)` — the same constant-time pattern already used in `CurrentUserService.VerifyHash` for passwords. Do not use `==` or `string.Compare`.
- The `DataProtection` provider (already wired for AI keys) is for reversible encryption; do not use it for tokens that never need decryption. One-way SHA-256 hash is correct here.

**Phase:** Token auth phase (first phase of v1.5). Token storage and verification are gate zero — no other v1.5 feature ships without them.

---

### Pitfall A2: Token passed in the URL (query-string) rather than the Authorization header

**What goes wrong:**
`/api/pantry?token=abc123` causes the raw token to appear in server access logs, reverse-proxy logs, browser history, and any monitoring tool that logs request URLs. Any of those surfaces becomes a token leak vector. Even on a trusted LAN, local log files are often less protected than the SQLite DB itself.

**Why it happens:**
Query-string auth is easy to demo and easy to test with a browser address bar. Developers under time pressure choose it first and never revisit it.

**Prevention:**
- Accept only `Authorization: Bearer <token>` header. The middleware that extracts the token must read `HttpContext.Request.Headers["Authorization"]` exclusively.
- Return HTTP 401 for any request missing the header or using a malformed scheme — even if a query-string token is present.
- In the MCP transport layer: the MCP C# SDK's SSE transport passes the HTTP headers for the initial handshake through to the ASP.NET pipeline — the same `Authorization` header check applies to the SSE upgrade request.

**Phase:** Token auth phase.

---

### Pitfall A3: Token resolves an identity but the agent facade does not re-enforce per-user ownership — cross-user pantry/recipe access

**What goes wrong:**
A token is mapped to User A. The agent sends `POST /api/pantry/{pantryId}/items` where `pantryId` belongs to User B. If the facade passes the `pantryId` directly to `PantryService.AddOrUpdateAsync(pantryId, ...)` without checking that the resolved user owns or is a member of that pantry, User A can write to User B's pantry.

This is the most dangerous pitfall in v1.5 because the Blazor path has never needed this check at the HTTP level — `CurrentUserService.CurrentUserId` was set from the user's own browser session, so the question of "does this user own this pantry?" was answered implicitly by the session. The agent path breaks that implicit assumption: a bearer token proves identity but does not restrict the scope of resource IDs the agent is allowed to supply.

**Why it happens:**
`PantryService.AddOrUpdateAsync` (line 94–123 of the actual source) takes `(int pantryId, int ingredientId, double amount, string unit, DateTime? expiration)`. It has NO ownership check. The caller is expected to have already verified access. In the Blazor path, ownership was enforced in the Razor component by only showing the user their own pantries. The agent facade has no equivalent UI constraint — any `pantryId` integer can be sent.

Similarly, `PantryService.GetPantryItemsAsync(int pantryId)` (line 46) has no ownership check. An agent can enumerate any pantry by ID if the facade does not guard it.

**What is in `PantryService` vs what is not:**
`GetAccessiblePantriesAsync(int userId)` (lines 55–72) is the access list. It returns owned + member pantries for `userId`. The check must be: confirm that the requested `pantryId` appears in `GetAccessiblePantriesAsync(resolvedUserId)` before passing it to any mutation method.

**Prevention:**
- The agent-operations facade must call `PantryService.GetAccessiblePantriesAsync(resolvedUserId)` and validate the incoming `pantryId` is in the returned set before every pantry mutation. Do not bypass this with a direct `GetByIdAsync`.
- For recipe operations: `RecipeService.CreateAsync(cookbookId, userId, parsed)` already checks `cookbook.UserId == userId` (lines 50–54 of actual source). The agent-submitted `cookbookId` must be validated against the same resolved `userId` — this check is already in `RecipeService` but only if `userId` is the *resolved token user*, not an agent-supplied one. Never allow the agent to supply a `userId` field that overrides the resolved token identity.
- Add an integration test: issue a token for User A; send a request targeting User B's pantry ID; assert HTTP 403.

**Phase:** Agent-operations facade phase (same phase as or immediately after token auth). This is the ownership-enforcement layer — it must exist before the REST and MCP transports are wired up.

---

### Pitfall A4: `CurrentUserService` (a circuit-scoped, mutable singleton per Blazor circuit) is reused directly for stateless HTTP requests — identity cross-wiring under concurrency

**What goes wrong:**
`CurrentUserService` is registered `AddScoped` (line 55 of `Program.cs`). In the Blazor Server model, "scoped" means per-circuit — the DI container creates one instance per SignalR circuit and it lives for the circuit lifetime. `CurrentUserId` is a plain mutable property (`public int? CurrentUserId { get; set; }`). MainLayout sets it once at circuit start from `sessionStorage`.

For an ASP.NET minimal-API request, "scoped" means per-HTTP-request — one fresh instance per request. But if the v1.5 middleware writes `currentUserService.CurrentUserId = resolvedUserId` into the scoped instance, and that instance is also injected into a service shared with the Blazor circuit (e.g., a Singleton or a service that captures a reference), the assignment can leak across requests.

The specific failure mode: if the agent-operations facade or the MCP tool handlers are registered Singleton or Transient-that-captures-a-Scoped-service, and they hold a reference to a `CurrentUserService` instance from a different request scope, concurrent agent calls can set each other's user IDs.

**Why it is particularly dangerous in this app:**
The Blazor SignalR pipeline and the HTTP minimal-API pipeline share the same Kestrel host but run on different middleware stacks. A `CurrentUserService` resolved in the Blazor scope and a `CurrentUserService` resolved in the HTTP scope are different instances IF both are properly scoped. But any singleton that captures a `CurrentUserService` at construction time breaks this.

**Prevention:**
- Do NOT reuse `CurrentUserService` for the agent path. The agent path needs a separate identity accessor — call it `IAgentIdentityContext` or similar — that is resolved per-HTTP-request (scoped) and populated by the token-resolution middleware.
- The agent-operations facade should take `int actingUserId` as a parameter (passed explicitly from the controller/MCP handler) rather than resolving it from `CurrentUserService`. This makes the identity explicit and testable.
- If `CurrentUserService` must be shared (e.g., because Application-layer services already depend on it), verify that it is NOT captured by any singleton service. Search for `IServiceProvider.GetRequiredService<CurrentUserService>()` in Singleton constructors before wiring the agent stack.
- Write a concurrency test: issue 10 simultaneous agent requests as User A and User B, assert that each response reflects the correct user's data with no cross-wiring.

**Phase:** Token auth + identity plumbing phase. The `IAgentIdentityContext` abstraction (or equivalent parameter-passing pattern) must be defined before the facade is implemented.

---

### Pitfall A5: Agent-submitted `RecipeDocument` contains ownership fields (`userId`, `cookbookId`) that the agent should not control — authz bypass via the request body

**What goes wrong:**
The structured-submit flow is: agent POSTs a `RecipeDocument` JSON body → schema-validate → convert to `ParsedRecipe` → `RecipeService.CreateAsync(cookbookId, userId, parsed)`. The `cookbookId` and `userId` are meant to come from the *token-resolved identity*, not from the request body. But if the endpoint binds a request DTO that includes `cookbookId` as a JSON field, the agent can supply any `cookbookId` — even one belonging to another user — and the `RecipeService.CreateAsync` ownership check (`cookbook.UserId != userId`) will pass because the `userId` parameter is the resolved token user, but the `cookbookId` may belong to a different user.

**Concrete scenario:**
User A has a token. User A knows User B's cookbook ID (e.g., from a shared cookbook URL they were given access to read). User A's agent sends `POST /api/recipes` with `cookbookId: <User B's ID>` in the body. `RecipeService.CreateAsync` checks `cookbook.UserId == userId` where `userId = User A`. User B's cookbook has `UserId = User B`. The check fails (throws `UnauthorizedAccessException`) — so this particular scenario IS caught by the existing guard.

But what about: the agent submits a body where `RecipeDocument.Extras` (the `[JsonExtensionData]` dictionary on line 53 of `RecipeDocument.cs`) contains `"userId": 2` or `"cookbookId": 7`? These extra fields round-trip through the canonical doc but are not acted on by `RecipeService`. However, if the v1.5 endpoint binding DTO is separate from `RecipeDocument` and maps fields by name, a developer might accidentally wire up `dto.CookbookId` from the body.

**The actual risk:**
The structured-submit endpoint must accept `cookbookId` from somewhere — the agent must tell the app where to create the recipe. If `cookbookId` comes from the request body, the agent controls it. If it comes from a path parameter (`/api/cookbooks/{cookbookId}/recipes`), it is still agent-controlled but is structurally distinct from the canonical doc.

Either way, the `RecipeService.CreateAsync` ownership guard (`cookbook.UserId != userId`) is load-bearing. It must be the final gate, not the only gate. Add a pre-check in the facade.

**Prevention:**
- The recipe creation endpoint must validate `cookbookId` against the resolved user's accessible cookbooks before calling `RecipeService.CreateAsync`. Do not rely solely on `RecipeService`'s internal check as the security boundary — treat it as defense-in-depth.
- The `RecipeDocument` JSON body must NEVER be the source of `userId`, `cookbookId`, or any ownership-related ID. Strip or ignore these if present in `RecipeDocument.Extras`. The facade derives `userId` from the token exclusively.
- `ParsedRecipe` has no `UserId` or `CookbookId` field — this is the correct design. Ensure the conversion from `RecipeDocument` → `ParsedRecipe` stays this way and does not add ownership fields.
- Schema validation via `JsonSchema.Net` (already wired) enforces the `RecipeDocument` shape — add a check that `userId` is NOT a valid key in the top-level schema to surface agent mistakes early.

**Phase:** Structured-submit phase (recipe creation endpoint). The "no ownership fields in the body" rule must be in the spec before implementation.

---

### Pitfall A6: Structured-submit bypasses the existing Markdig `DisableHtml` + `PromptInjectionGuard` lockdown — stored XSS via agent-submitted recipe text

**What goes wrong:**
The existing AI chat path wraps all AI-emitted content in `Markdig.Markdown.ToHtml(content, AssistantContentPipeline)` where `AssistantContentPipeline` uses `.DisableHtml()` (lines 372–373 of `AiChat.razor`). This strips raw HTML tags from AI output before they reach the browser. Agent-submitted recipe text (step text, ingredient notes, description, doneness cues) enters through a different path: `RecipeDocument` JSON → `ParsedRecipe` → `RecipeService.CreateAsync` → `RecipeStep.Text` stored in DB. When displayed in `RecipeView.razor` or `CookingMode.razor`, step text is rendered — if HTML is not stripped before render, a malicious agent can submit `<script>alert(1)</script>` in a step and have it execute in any browser that views the recipe.

**Why the existing guard does not cover this path:**
`PromptInjectionGuard.WrapRecipe` (in `RecipeCookingAiContext.cs`) wraps content going INTO the AI, not content coming in from an agent. `Markdig DisableHtml` is applied at AI chat render time, not at recipe-view render time. The `RecipeView.razor` and `CookingMode.razor` components render step text directly from `RecipeStep.Text` — they use Blazor's default rendering, which HTML-encodes plain strings. This means a `<script>` tag would be entity-escaped by Blazor and not execute if step text is rendered as a string. BUT: if step text is rendered as `MarkupString` (raw HTML), the protection breaks.

**Audit finding from the actual codebase:**
`CookingMode.razor` uses `@using Markdig` (line 12). If step text passes through Markdig's HTML parser with HTML enabled, embedded `<script>` tags in step text would survive. The safe rendering path is: step text → `Markdig.ToHtml(text, pipeline with DisableHtml)` → `MarkupString`. Verify that `CookingMode.razor` and `RecipeView.razor` use the DisableHtml pipeline, not the default pipeline, for step text.

**Prevention:**
- Audit every Razor component that renders `RecipeStep.Text` or any other agent-submitted text field as HTML. The DisableHtml pipeline must be applied before `new MarkupString(html)`.
- For step text rendered as plain string (not HTML), Blazor's default encoding is sufficient — but confirm no component uses `@((MarkupString)step.Text)` without a sanitization pass first.
- Add a UAT assertion: submit a recipe with `<script>alert(1)</script>` as step text via the agent API; verify the script does not execute when the recipe is viewed in a browser.
- The existing `Markdig DisableHtml` pipeline already exists in `AiChat.razor` — extract it to a shared `RecipeTextRenderer` service so step-text rendering uses the same pipeline consistently.

**Phase:** Structured-submit phase, with a code-review gate also applied in the MCP tool handler phase.

---

## High-Priority Pitfalls

### Pitfall B1: `PhotoUrl` in the agent-submitted `RecipeDocument` is not validated through the existing SSRF-aware pipeline — bypasses `RecipePhotoUrlValidator`

**What goes wrong:**
The existing `RecipePhotoUrlValidator` (scheme allowlist) + `PhotoUrlHeadValidator` (HEAD request confirming the URL resolves to an image) are wired in `RecipePhotoService.AddPhotoAsync` and in `AnthropicAiService` after structured output returns. An agent can submit a `RecipeDocument` with `"photoUrl": "file:///etc/passwd"` or `"photoUrl": "http://169.254.169.254/latest/meta-data/"` (AWS metadata endpoint). If `RecipeService.CreateAsync` persists `parsed.PhotoUrl` directly without passing it through `RecipePhotoUrlValidator.TryValidate`, the disallowed URL ends up in `Recipe.PhotoUrl` and `CanonicalDocumentJson`.

The `RecipePhotoUrlValidator` already exists in `CookBot.Application.Services` — the fix is to call it, not to write new validation logic.

**Prevention:**
- The agent-operations facade must call `RecipePhotoUrlValidator.TryValidate(doc.PhotoUrl, out var normalized, out _)` before constructing the `ParsedRecipe`. If validation fails, reject the request with HTTP 422 and an error indicating which field is invalid. Set `parsed.PhotoUrl = normalized` (which is the sanitized, trimmed `AbsoluteUri`).
- Do NOT call `PhotoUrlHeadValidator.ValidateAsync` in the synchronous submit path — it makes an outbound HTTP call and adds latency. The scheme-allowlist check alone is sufficient for agent submissions; HEAD validation is a UI affordance.
- Note: `RecipePhotoUrlValidator.TryValidate` returns `true` with `normalized = null` for null/empty input — this is the correct "no photo" signal. Preserve this: an agent that omits `photoUrl` produces a recipe with no photo, not a validation error.

**Phase:** Structured-submit phase.

---

### Pitfall B2: The agent-operations facade introduces a second copy of pantry mutation logic — authz drift between the REST API and the Blazor path

**What goes wrong:**
The Blazor path enforces pantry ownership by showing only the user's own pantries in the UI (implicit, not code-level). If the agent facade duplicates ownership-check logic inline (e.g., `if (pantry.OwnerId != resolvedUserId) return Forbidden`) rather than routing through `PantryService.GetAccessiblePantriesAsync`, the two paths diverge when `PantryService` adds new access patterns (e.g., shared pantry membership). A future change to membership logic in `PantryService` will not propagate to the duplicated check in the facade.

**Prevention:**
- The facade must NOT duplicate ownership checks. It must call `PantryService.GetAccessiblePantriesAsync(resolvedUserId)` to get the access set, then validate the incoming `pantryId` against that set. `PantryService` is the single source of truth for access logic.
- The same applies to cookbook access: use the pattern already in `RecipeService.CreateAsync` (load cookbook, check `cookbook.UserId == userId`) — do not add a separate "does this user own a cookbook" check in the facade that could diverge.

**Phase:** Agent-operations facade phase.

---

### Pitfall B3: MCP session transport defaults to no authentication — the MCP SSE endpoint is open to any LAN host without a token

**What goes wrong:**
The `ModelContextProtocol` C# SDK's HTTP/SSE transport sets up an SSE endpoint (typically `/sse` or `/mcp`) using `app.MapMcp()` or equivalent. If the token-resolution middleware is not applied to this endpoint, any host on the LAN can establish an MCP session and invoke all tools — including pantry mutations and recipe creation — without a token.

The existing `/healthz` endpoint (line 104 of `Program.cs`) is intentionally open. The MCP endpoint must not inherit the same open posture.

**Why it happens:**
The MCP SDK's getting-started examples do not show auth middleware because they assume the developer will add it. A developer wiring up `app.MapMcp()` after `app.MapHealthChecks("/healthz")` may not realize the route is unauthenticated.

**Prevention:**
- Apply the token-resolution middleware (or a `RequireAuthorization` policy) to the MCP endpoint specifically. In ASP.NET minimal API, this is done by chaining `.RequireAuthorization("AgentPolicy")` on the `MapMcp()` call or by placing the token middleware before `app.MapMcp()` in the pipeline so that it runs for that route.
- Alternatively, wrap all MCP tool handlers to call the token validation logic at the start of each tool invocation — defense-in-depth, but do not rely on this as the primary gate.
- Write a test: connect to the MCP SSE endpoint without an `Authorization` header; assert HTTP 401.

**Phase:** MCP server phase.

---

### Pitfall B4: MCP SSE keep-alive and session ID handling — duplicate session creation and reconnect storms

**What goes wrong:**
The MCP HTTP/SSE transport maintains a long-lived SSE connection for each client session. If the Kestrel response timeout or an intermediate reverse proxy (nginx, Caddy) closes idle SSE connections, the MCP client reconnects and may create a new session rather than resuming the old one. In the worst case, a misconfigured proxy with a 60-second read timeout generates a new session every minute — each session consumes a Scoped DI scope on the server side, and if those scopes are not disposed, the app leaks `CookBotDbContext` instances (EF Core contexts are registered Scoped in `AddInfrastructure`).

**Prevention:**
- Configure Kestrel's `KeepAliveTimeout` and `RequestHeadersTimeout` to accommodate long-lived SSE connections (several minutes at minimum, or disable the timeout on the MCP route).
- If running behind a reverse proxy, configure the proxy to set appropriate timeouts for SSE routes (e.g., nginx `proxy_read_timeout 3600s` for the MCP path).
- Confirm that each MCP session scope is disposed when the SSE connection closes — the SDK should handle this, but verify in a load test.
- MCP session IDs: the C# SDK generates a session ID for each SSE connection. Do not store session IDs as a security token — they are not secret. The bearer token is the authentication credential; the session ID is a transport routing key.

**Phase:** MCP server phase. Verify with a connection-drop test during UAT.

---

### Pitfall B5: Kestrel `MaxRequestBodySize` is set for Blazor uploads (12 MB) — agent recipe submissions have no separate cap and can be used for DoS

**What goes wrong:**
`Program.cs` line 25 sets `MaxRequestBodySize = 12 * 1024 * 1024` for Blazor photo uploads. This limit also applies to the new REST endpoints. An agent can POST a 12 MB JSON body as a `RecipeDocument`. `System.Text.Json` will deserialize it eagerly before schema validation runs — a 12 MB recipe with 100,000 ingredients will allocate significant memory before being rejected.

**Prevention:**
- Apply a per-route body size override on the agent endpoints: `.WithMetadata(new RequestSizeLimitAttribute(256 * 1024))` (256 KB is generous for a recipe; no legitimate `RecipeDocument` exceeds a few KB). This overrides the global Kestrel limit for that route without changing the photo upload limit.
- Run schema validation (via `JsonSchema.Net`) with a `MaxItems` constraint on the `ingredients` and `steps` arrays to reject structurally valid but abusively large documents before any DB work is done.
- Cap list/read endpoints: add an explicit page-size limit to pantry list responses (e.g., max 500 items) to prevent unbounded DB scans.

**Phase:** REST API phase (or combined with the agent facade phase). The 256 KB cap is a one-liner; add it in the first plan that introduces REST route registration.

---

### Pitfall B6: New `AgentToken` migration runs at startup (via `DatabaseSeeder.SeedAsync`) and can deadlock if the Blazor circuit connects before migration completes

**What goes wrong:**
`DatabaseSeeder.SeedAsync` calls `context.Database.MigrateAsync()` at startup (line 117 of `Program.cs`). This is an established pattern and works. However, if the `AgentToken` migration is large (e.g., it backfills data) or if SQLite's WAL mode is not enabled and a Blazor circuit connects while migration is running, SQLite's serialized-writer model causes the circuit to block on a locked write. The current migrations are all schema-only (no data backfills), so this has not been a problem. Token table migration should stay schema-only for the same reason.

**Prevention:**
- The `AgentToken` EF Core migration must be schema-only: create the table, add indexes, done. No data backfill in the migration.
- If token issuance requires a seed (e.g., a default agent token for dev), put it in `DatabaseSeeder.SeedAsync` after `MigrateAsync` — not inside the migration itself.
- Keep `WAL` mode configured (already set in `AddInfrastructure` for concurrent reads) — this prevents reader starvation during the migration write.

**Phase:** Token auth phase (migration design is part of the first token implementation plan).

---

### Pitfall B7: Brute-force of short or predictable agent tokens — no rate limiting on token validation

**What goes wrong:**
The token resolution middleware validates `SHA-256(incomingToken)` against the stored hash on every request. If tokens are short (e.g., 8 hex characters), an attacker on the LAN can enumerate the token space with repeated requests. Even with 32-byte (256-bit) random tokens, if there is no rate limiting on the resolution endpoint, a compromised LAN host can issue thousands of requests per second to search for valid tokens.

**Why this matters on a trusted LAN:**
The trusted-LAN posture means no rate-limiting was needed for Blazor (human-speed interactions). Agent tokens change this: a compromised device on the LAN can issue machine-speed requests.

**Prevention:**
- Use 32-byte (256 bits) tokens from `RandomNumberGenerator.GetBytes(32)`, encoded as hex or Base64Url. This makes brute-force computationally infeasible regardless of rate limiting.
- Add a simple in-memory rate limiter on the token validation path: e.g., ASP.NET `RateLimiter` middleware (built into .NET 7+, available on .NET 10) configured with a fixed-window policy (e.g., 100 requests/minute per IP). This is not a full-scale rate-limiting solution but is sufficient for the trusted-LAN threat model.
- Log failed token validation attempts at Warning level with the source IP. Even a small number of failures from an unexpected source is a meaningful signal on a trusted LAN.

**Phase:** Token auth phase.

---

### Pitfall B8: The agent-operations facade allows `RecipeDocument.Extras` (the `[JsonExtensionData]` bag) to be persisted, which could contain large or unexpected data

**What goes wrong:**
`RecipeDocument` has `[JsonExtensionData] Dictionary<string, JsonElement> Extras` for forward-compat round-tripping. When an agent submits a `RecipeDocument` with unexpected top-level keys, those keys are stored in `Extras` and round-trip into `CanonicalDocumentJson`. A malicious agent could embed large binary data in an `Extras` key, bypassing the 256 KB body limit (after schema validation strips known fields, Extras is not validated for size). The stored `CanonicalDocumentJson` would then be bloated with arbitrary data.

**Prevention:**
- After `JsonSchema.Net` schema validation passes (which validates known fields), check `doc.Extras.Count == 0` or that `Extras` contains only known forward-compat keys. For the agent path, reject any document with non-empty `Extras` until the app explicitly supports forward-compat agent submissions. An agent submitting an unknown field is either a schema version mismatch (return 422 with schema version error) or a probing attempt (return 422 with "unexpected fields").
- Alternatively: deserialize with `JsonSerializer.Deserialize<RecipeDocument>` and then re-serialize before storing — the round-trip will drop `Extras` if they are not explicitly mapped. But this silently drops data; explicit rejection is safer.

**Phase:** Structured-submit phase.

---

## Moderate Pitfalls

### Pitfall C1: CORS accidentally enabled app-wide — the Blazor SignalR circuit becomes cross-origin callable

**What goes wrong:**
If `app.UseCors()` is added globally to support the agent REST API (e.g., to allow an external agent dashboard), the Blazor Server SignalR connection also becomes cross-origin callable. This is unlikely to cause an immediate vulnerability (SignalR connections still require the server to accept them), but it violates the trusted-LAN posture and may expose the SignalR negotiation endpoint to a cross-origin attacker.

**Prevention:**
- Do not add a global CORS policy. If CORS is needed (it likely is not — agents running on the same LAN or in a local container do not need CORS), scope the policy to the specific agent routes: `.WithOrigins(...).RequireCors("AgentPolicy")`.
- For the trusted-LAN use case, the agent is typically a local process or a container on the same network — it does not need CORS. Omit CORS entirely unless a specific cross-origin agent deployment is identified.

**Phase:** REST API phase. If no CORS is added, this pitfall is avoided by omission.

---

### Pitfall C2: Accidentally binding the new REST/MCP endpoints to `0.0.0.0:443` or a public-facing port via `appsettings.Production.json` or Docker port mapping

**What goes wrong:**
The existing Kestrel configuration in `appsettings.json` binds to `http://localhost:7000` (trusted-LAN). If `appsettings.Production.json` adds a new Kestrel endpoint for the agent API (e.g., `https://0.0.0.0:7001`), or if the Docker `compose.yml` maps port 7001 to the host, the agent API is exposed to the public internet in Docker deployments.

**Prevention:**
- The agent API and MCP transport use the same port and host as the existing Blazor app (`localhost:7000` in dev, the configured Kestrel endpoint in production). Do not add a separate port.
- Document in `compose.yml` comments that only port 7000 should be mapped, and that mapping it to a public-facing address without a reverse proxy with TLS is unsupported.
- Add a startup warning log if the resolved bind address is `0.0.0.0` and `ASPNETCORE_ENVIRONMENT == Production` — this makes accidental public exposure visible in container logs.

**Phase:** REST API phase.

---

### Pitfall C3: Token privilege escalation — mapping an agent token to a CookBotAdmin user grants the agent admin capabilities

**What goes wrong:**
`CurrentUserService.IsCookBotAdminAsync` checks `User.IsCookBotAdmin`. If an agent token is mapped to the admin user, the agent can invoke admin-only operations (e.g., `DeleteUserAsAdminAsync`) through any path that reads `CurrentUserId` and calls `IsCookBotAdminAsync`. The agent should be able to manage pantry and recipes only — not user administration.

**Prevention:**
- Do not map agent tokens to admin users. Enforce this at token issuance time: reject token creation if the target `userId` has `IsCookBotAdmin = true`.
- The agent-operations facade should not expose any admin operations. Its surface is explicitly: list pantries, list items, add/update item, deduct item, resolve ingredient, create recipe. Nothing else.
- Add a note in the token issuance UI/CLI: "Tokens may only be issued for non-admin users."

**Phase:** Token auth phase.

---

### Pitfall C4: Token revocation requires DB lookup per request — no revocation propagation to in-flight MCP sessions

**What goes wrong:**
When a token is revoked (deleted from `AgentToken` table), in-flight MCP SSE sessions that authenticated with that token continue to exist. The token-resolution middleware runs on HTTP request authentication (for REST calls), but the MCP session was authenticated once at SSE connection time. If the SDK does not re-validate the token on every tool invocation, a revoked token's MCP session remains active until the SSE connection drops.

**Prevention:**
- The MCP tool handlers must validate the token (or the resolved identity) on every tool invocation, not only at SSE connection time. Pass the `IAgentIdentityContext` into each tool handler and verify the associated `UserId` is still valid (i.e., the user still exists and their token is not revoked) at the start of each call.
- For REST endpoints, per-request middleware re-validation is already the natural pattern — this is only a special concern for long-lived MCP sessions.
- Token revocation should also close active MCP sessions if the SDK provides a session-close API.

**Phase:** MCP server phase.

---

### Pitfall C5: EF Core change-tracker conflict between the Blazor circuit's `CookBotDbContext` and the agent HTTP request's `CookBotDbContext` on a shared pantry

**What goes wrong:**
`CookBotDbContext` is registered Scoped. In the Blazor circuit, the scoped context lives for the circuit lifetime. In an HTTP request, the scoped context lives for the request. These are separate instances and do not share the EF change tracker — so direct EF conflicts are unlikely. However, if a Blazor circuit and an agent request both write to the same `PantryItem` row (e.g., a shared pantry with a member whose Blazor circuit is open), SQLite's serialized writer will queue one behind the other. The later write wins; the earlier write is not an error but may be overwritten.

This is not a v1.5-specific bug, but v1.5 introduces the first concurrent non-browser writer, making it the first time this race condition is reachable.

**Prevention:**
- SQLite WAL mode (already enabled via `AddInfrastructure`) serializes writes at the DB level. The `SaveChangesAsync` that loses the race will either succeed (last-writer-wins, which is acceptable for pantry amounts) or throw a `DbUpdateConcurrencyException` if EF concurrency tokens are set on `PantryItem`.
- For pantry items, last-writer-wins is acceptable for the trusted-LAN use case — document this explicitly in the agent facade rather than adding optimistic concurrency tokens (which would require a new migration and a retry loop).
- If a stricter model is needed in the future, add a `RowVersion` concurrency token to `PantryItem` and handle `DbUpdateConcurrencyException` in the facade with a retry. Do not do this in v1.5 unless UAT reveals an actual problem.

**Phase:** Agent-operations facade phase (document the concurrency model; add the concurrency-token migration only if needed).

---

## Minor Pitfalls

### Pitfall D1: Token not included in the `RecipeDocument` structured-submit schema validation — `JsonSchema.Net` validates the body shape but not the request identity

**What goes wrong:**
This is not a security issue but a developer confusion issue: `JsonSchema.Net` validates the `RecipeDocument` JSON body. It does not validate that the `cookbookId` (supplied as a path/query parameter or separate field) matches the token-resolved user. Developers may assume that "passed schema validation" means "passed authorization." They are separate gates.

**Prevention:**
- Add a comment in the structured-submit handler: `// Schema validation above ensures shape; ownership check below ensures access`. The two checks are always both required.

**Phase:** Structured-submit phase.

---

### Pitfall D2: The ingredient-name resolver (`ResolveIngredientAsync`) creates new `Ingredient` rows for agent-submitted names — ingredient table pollution

**What goes wrong:**
`RecipeService.CreateAsync` calls `ResolveIngredientAsync(name)` for each ingredient (lines 83–90 of the actual source). This creates a new `Ingredient` row if the normalized name does not match an existing one. An agent submitting many recipes with slight name variations ("Butter", "Unsalted Butter", "butter unsalted") can create hundreds of near-duplicate ingredient rows, degrading the autocomplete quality and the pantry-match algorithm.

**Prevention:**
- The ingredient resolver already normalizes names via `IngredientResolver.Normalize(name)` — this is the existing deduplification mechanism. The quality of normalization (stemming, trim, lowercase) determines how many near-duplicates survive.
- Add a maximum-ingredient-count check per recipe in the structured-submit validation (e.g., reject documents with more than 50 ingredients) to cap the rate of ingredient row creation per submission.
- Consider adding an agent-specific audit log: each structured-submit logs the ingredient names that created new rows. This makes pollution visible without blocking the operation.

**Phase:** Structured-submit phase.

---

### Pitfall D3: The `ParsedRecipe` → `RecipeDocument` conversion in `RecipeService.CreateAsync` drops new fields if the POCO, parser DTO, and `RecipeService` are not all updated together

**What goes wrong:**
This is the "canonical three-boundary" pitfall documented in the project MEMORY.md. For v1.5, the agent path adds a new direction: `RecipeDocument` → `ParsedRecipe` → `RecipeService.CreateAsync`. If v1.5 extends `RecipeDocument` with a new field (e.g., an `agentNote` or a new v4 field), the field must also be added to `ParsedRecipe` AND wired in `RecipeService.CreateAsync`'s `RecipeDocument` construction block (the inverse mapping at lines 123–145 of the actual source). Missing either step causes the field to be silently dropped — it arrives in the POST body, passes schema validation, but is not persisted.

**Prevention:**
- Follow the existing "POCO + parser-DTO + RecipeService must all change together" rule from `MEMORY.md`.
- For the agent path specifically: the `RecipeDocument` → `ParsedRecipe` conversion step (which does not exist in the current Blazor path — the editor builds `ParsedRecipe` directly from the chip composer) must be added as an explicit mapping function, not an inline ad-hoc mapping. A separate `RecipeDocumentToParseRecipeConverter` makes the mapping visible and testable.
- Write a round-trip test: POST a `RecipeDocument` with all fields populated; GET the recipe back; assert all fields are present.

**Phase:** Structured-submit phase.

---

## Phase-Specific Warning Matrix

| Phase Topic | Pitfall | Severity | Mitigation |
|-------------|---------|----------|------------|
| Token table migration | Plaintext token storage (A1) | CRITICAL | SHA-256 hash only; no raw token in DB; constant-time compare |
| Token resolution middleware | Token in URL query-string (A2) | HIGH | Authorization header only; 401 for query-string tokens |
| Agent identity context | `CurrentUserService` reuse for stateless HTTP (A4) | HIGH | Separate `IAgentIdentityContext`; pass `actingUserId` explicitly |
| Agent-operations facade | Missing per-request authz on pantry ops (A3) | CRITICAL | Validate `pantryId` via `GetAccessiblePantriesAsync`; facade is the authz layer |
| Agent-operations facade | Authz drift from duplicated ownership logic (B2) | HIGH | Route through `PantryService`; never duplicate ownership checks |
| Structured-submit endpoint | Agent supplies ownership fields in body (A5) | HIGH | Strip/ignore `userId`/`cookbookId` from body; derive from token only |
| Structured-submit endpoint | Stored XSS via step text (A6) | HIGH | Audit `RecipeView`/`CookingMode` rendering; apply DisableHtml pipeline |
| Structured-submit endpoint | Agent-submitted `photoUrl` bypasses SSRF-aware validator (B1) | HIGH | Call `RecipePhotoUrlValidator.TryValidate` on `doc.PhotoUrl` before persisting |
| Structured-submit endpoint | `Extras` bag persisted without size/content check (B8) | MEDIUM | Reject non-empty `Extras` from agent submissions; return 422 with schema version hint |
| REST API route registration | Body-size DoS via oversized recipe payload (B5) | MEDIUM | `RequestSizeLimitAttribute(256 * 1024)` on agent endpoints |
| REST API route registration | Accidental public binding via Docker port mapping (C2) | MEDIUM | Single port; document compose port mapping; startup warning log |
| REST API route registration | CORS added globally (C1) | MEDIUM | No global CORS; scope to agent routes only if needed |
| MCP server wiring | MCP SSE endpoint unauthenticated by default (B3) | CRITICAL | `.RequireAuthorization("AgentPolicy")` on `MapMcp()` |
| MCP server wiring | In-flight MCP sessions survive token revocation (C4) | MEDIUM | Re-validate identity on every tool invocation |
| MCP server wiring | SSE keep-alive drops reconnect storm (B4) | MEDIUM | Set Kestrel/proxy timeouts; verify scope disposal on disconnect |
| Token auth design | Short or predictable tokens (B7) | HIGH | 32-byte `RandomNumberGenerator.GetBytes(32)`; in-memory rate limiter |
| Token auth design | Admin user mapped to agent token (C3) | MEDIUM | Reject token issuance for admin users |
| Token table migration | Migration deadlocks Blazor circuits (B6) | LOW | Schema-only migration; no data backfill |
| Ingredient resolver | Near-duplicate ingredient row creation (D2) | LOW | Max 50 ingredients per submission; agent audit log |
| Field mapping | Three-boundary field drop in `RecipeDocument`→`ParsedRecipe` (D3) | MEDIUM | Explicit converter; round-trip test covers all fields |

---

## "Looks Done But Isn't" Checklist

- [ ] **Token storage:** Does `SELECT token_hash FROM AgentTokens` return a 64-char hex SHA-256 hash, not a raw token string?
- [ ] **Token compare:** Is `CryptographicOperations.FixedTimeEquals` used (not `==`) for token hash comparison?
- [ ] **Token header:** Does the middleware return HTTP 401 when `Authorization` header is absent, and also when a valid token is supplied as a query-string parameter?
- [ ] **Pantry authz:** Does sending a pantry mutation request for a pantry belonging to a different user return HTTP 403, not 200 or 500?
- [ ] **Recipe authz:** Does sending a recipe creation request with a `cookbookId` belonging to a different user return HTTP 403?
- [ ] **XSS:** Does a recipe with `<script>alert(1)</script>` in step text render the script as escaped text (not executed) in `RecipeView.razor` and `CookingMode.razor`?
- [ ] **PhotoUrl SSRF:** Does `POST /api/recipes` with `"photoUrl": "file:///etc/passwd"` return HTTP 422 (not 200)?
- [ ] **MCP auth:** Does connecting to the MCP SSE endpoint without an `Authorization` header return HTTP 401?
- [ ] **CurrentUserService isolation:** Do 10 concurrent agent requests as two different users never return data for the wrong user?
- [ ] **Body size:** Does a POST with a 1 MB recipe body return HTTP 413 on the agent endpoint (while a photo upload of the same size succeeds on the Blazor endpoint)?
- [ ] **Token admin guard:** Does token issuance for the admin user return an error?
- [ ] **Extras rejection:** Does a `RecipeDocument` with an unknown top-level key (e.g., `"hack": "..."`) return HTTP 422?

---

## Sources

- Actual codebase: `src/CookBot.Web/Services/CurrentUserService.cs` — mutable `CurrentUserId` property; circuit-scoped registration in `Program.cs` line 55; `VerifyHash` uses `CryptographicOperations.FixedTimeEquals` (the correct pattern to copy for token comparison)
- Actual codebase: `src/CookBot.Application/Services/PantryService.cs` — `GetAccessiblePantriesAsync` is the access-list source; `AddOrUpdateAsync` and `GetPantryItemsAsync` have NO ownership checks
- Actual codebase: `src/CookBot.Application/Services/RecipeService.cs` — `CreateAsync` checks `cookbook.UserId != userId` (the ownership guard); `ParsedRecipe` has no `UserId`/`CookbookId` field (correct design)
- Actual codebase: `src/CookBot.Application/Services/RecipePhotoUrlValidator.cs` — scheme-allowlist validator; `TryValidate` is the reuse point for agent-submitted `photoUrl`
- Actual codebase: `src/CookBot.Application/Services/PhotoUrlHeadValidator.cs` — `AllowAutoRedirect = false` SSRF posture; already defangs redirect-to-internal-host attacks
- Actual codebase: `src/CookBot.Domain/Recipes/RecipeDocument.cs` — `[JsonExtensionData] Extras` (the forward-compat round-trip bag — the pitfall B8 vector)
- Actual codebase: `src/CookBot.Web/Components/Pages/AiChat.razor` lines 372–373 — `DisableHtml` pipeline (the guard to reuse for agent-submitted step text rendering)
- Actual codebase: `src/CookBot.Web/Program.cs` lines 25, 34 — existing `MaxRequestBodySize = 12 MB` (the per-route override target for agent endpoints)
- `.planning/PROJECT.md` — trusted-LAN posture statement; v1.5 goals; "scoped exception" framing; "no Identity/OAuth" constraint
- MEMORY.md: `canonical-doc-three-boundaries.md` — the field-drop pitfall for POCO + parser-DTO + RecipeService boundary

---

*Pitfalls research for: v1.5 External Agent Interface — per-agent token auth, agent-operations facade, REST minimal-API, in-process MCP server*
*Researched: 2026-06-26*
