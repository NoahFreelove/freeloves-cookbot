# Stack Research

**Domain:** .NET 10 Blazor Server — adding an in-process MCP server + REST minimal-API + per-agent bearer-token auth (v1.5 External Agent Interface)
**Researched:** 2026-06-26
**Confidence:** MEDIUM-HIGH (MCP SDK version and license verified against NuGet + GitHub release; auth pattern verified against MS Learn + official SDK sample `ProtectedMcpServer`; REST/auth are built-in framework features: HIGH certainty)

---

## Existing Stack (fixed — do not re-research)

| Technology | Version | Role |
|------------|---------|------|
| .NET / C# | 10 | Runtime |
| Blazor Server | InteractiveServer | UI (stays unchanged) |
| EF Core + SQLite | 10.* | Persistence |
| System.Text.Json | BCL (net10) | JSON everywhere |
| JsonSchema.Net | 9.2.* | `RecipeDocument` schema validation (already installed — reused for agent-submitted docs) |
| YamlDotNet | 16.3.0 | Recipe YAML wire format |
| Markdig | 0.45.0 | Markdown rendering |
| QuestPDF | 2025.1.0 | PDF cookbook export |
| AnthropicAiService | custom HttpClient | AI (Sonnet / Haiku / Opus) |

Hard constraints that override every recommendation below:
- System.Text.Json ONLY — no Newtonsoft.Json, no NJsonSchema
- No MudBlazor — custom Razor component system
- No `Microsoft.Extensions.AI` — existing HttpClient in `AnthropicAiService` is sufficient
- No official Anthropic SDK NuGet
- GPL-3.0-only — all dependencies must be license-compatible
- Dependency-averse posture — v1.3 and v1.4 added ZERO new packages; any new package must be justified

---

## New Package Requirements: Exactly Two New NuGet Packages

### Package 1: `ModelContextProtocol.AspNetCore` 1.4.0

| Attribute | Value |
|-----------|-------|
| NuGet ID | `ModelContextProtocol.AspNetCore` |
| Version | **1.4.0** (stable, released 2026-06-04) |
| License | Apache-2.0 |
| GPL-3.0 compatible | **YES** — FSF and Apache Software Foundation both confirm: Apache 2.0 library may be included in a GPL-3.0-only work. The result must be released under GPL-3.0, which this project already is. (Note: Apache 2.0 is NOT compatible with GPL-2.0; only GPL-3.0.) |
| .NET targets | net8.0, net9.0, net10.0 |
| Sole dependency | `ModelContextProtocol` >= 1.4.0 (pulled in automatically) |

**Why this package:** It is the only mechanism to host an in-process MCP server on the same Kestrel instance as Blazor Server without a separate process or gateway. It provides `AddMcpServer()`, `WithHttpTransport()`, `MapMcp()`, `[McpServerToolType]`, and `[McpServerTool]` — the complete server-side MCP stack. The alternative (hand-rolling the MCP JSON-RPC 2.0 protocol, SSE transport, capability negotiation, and session management) would add 1,000+ lines of protocol plumbing with no upside.

### Package 2: `ModelContextProtocol` 1.4.0 (transitive — do NOT add explicitly)

| Attribute | Value |
|-----------|-------|
| NuGet ID | `ModelContextProtocol` |
| Version | 1.4.0 (transitive, pulled in by `.AspNetCore`) |
| License | Apache-2.0 |
| .NET targets | net8.0, net9.0, net10.0, netstandard2.0 |
| Dependencies | `Microsoft.Extensions.Caching.Abstractions` >= 10.0.7, `Microsoft.Extensions.Hosting.Abstractions` >= 10.0.7, `ModelContextProtocol.Core` >= 1.4.0 |

Do NOT add this explicitly to the `.csproj`. The `.AspNetCore` package brings it. Listing it here for dependency-graph awareness only.

`ModelContextProtocol.Core` (the third SDK package) is also pulled in transitively. It is for client-only or low-level use — do not reference it directly.

---

## Built-in Features (zero new packages)

### REST Minimal API

**Mechanism:** `app.MapGet(...)` / `app.MapPost(...)` / `app.MapPut(...)` / `app.MapDelete(...)` built into ASP.NET Core.

**Why no package:** Minimal APIs are part of `Microsoft.AspNetCore.App` — the framework meta-package implicitly referenced by every `Microsoft.NET.Sdk.Web` project. The app already uses `app.MapHealthChecks("/healthz")`; adding REST endpoints is exactly the same pattern.

**Confidence:** HIGH — this is a documented framework feature since .NET 6.

### Bearer-Token Authentication

**Mechanism:** `builder.Services.AddAuthentication().AddScheme<TOptions, THandler>(...)` using the built-in `AuthenticationHandler<TOptions>` base class.

**Why no package:** `AuthenticationHandler<TOptions>` lives in `Microsoft.AspNetCore.Authentication`, part of the framework meta-package. No JWT bearer, no Identity, no OAuth server packages needed. The tokens are opaque 32-byte random values (stored as SHA-256 hashes), not JWTs — `Microsoft.AspNetCore.Authentication.JwtBearer` would be the wrong tool and an unnecessary dependency.

### Token Hashing

**Mechanism:** `System.Security.Cryptography.SHA256.HashData(bytes)` + `CryptographicOperations.FixedTimeEquals(a, b)`.

**Why built-in is correct:** 32-byte cryptographically random tokens do not need PBKDF2 (which exists to slow down dictionary attacks on human passwords). SHA-256 is sufficient and fast for opaque random tokens. The BCL already provides constant-time compare via `CryptographicOperations.FixedTimeEquals` to prevent timing side-channels. The app already uses `PBKDF2-HMAC-SHA256` for `UserProfile.PasswordHash` and ASP.NET Core Data Protection for AI keys — the same BCL surface.

---

## MCP In-Process Hosting: Verdict

**YES — fully supported on the same Kestrel host alongside Blazor Server.**

`MapMcp("/mcp")` creates an isolated ASP.NET Core endpoint route group using standard endpoint routing. It coexists with `MapRazorComponents`, `MapHealthChecks`, and any `Map*` call because all of these register into the same `WebApplication` routing table. No port-sharing or reverse-proxy tricks needed. The official SDK ships an `AspNetCoreMcpServer` sample demonstrating CORS and OpenTelemetry alongside MCP on the same host.

**Transport detail:** `WithHttpTransport(o => o.Stateless = true)` exposes the MCP 2025-11-05 Streamable HTTP transport (and SSE simultaneously) on the `/mcp` route group. Stateless mode is the correct choice for pantry/recipe tools — there is no need for server-initiated sampling or elicitation.

**DI injection in tool methods:** Tool methods receive services directly as method parameters, resolved from the ASP.NET Core DI container per-request. This is identical to minimal-API handler injection. `IAgentFacade`, `ICurrentAgentUser`, `CancellationToken` — all work as parameters.

**`[Authorize]` on MCP routes:** `app.MapMcp("/mcp").RequireAuthorization("AgentPolicy")` applies the authorization policy to every MCP endpoint. This requires `app.UseAuthentication()` and `app.UseAuthorization()` to precede `MapMcp()` in the pipeline.

---

## Auth: Idiomatic Approach

**Use `AuthenticationHandler<TOptions>` — NOT hand-rolled middleware.**

### Why `AuthenticationHandler` over hand-rolled middleware

| Criterion | Hand-rolled `Use(next => async ctx => ...)` | `AuthenticationHandler<TOptions>` |
|-----------|---------------------------------------------|-----------------------------------|
| Produces `ClaimsPrincipal` | Must manually set `HttpContext.User` | Framework sets it automatically |
| `[Authorize]` attribute | Requires careful principal-setting order | Works natively |
| `RequireAuthorization("AgentPolicy")` | Fragile — policy checks run without a valid principal | Works natively |
| `MapMcp().RequireAuthorization()` | Breaks — endpoint filter sees no principal | Works natively (SDK's `ProtectedMcpServer` sample uses this pattern) |
| Scheme isolation | Impossible to scope to specific routes with catch-all middleware | Authentication schemes can be scoped per endpoint |
| Antiforgery bypass | Must manually skip for non-form routes; easy to miss | Auth runs separately; antiforgery only fires for Razor component endpoints |
| Test-ability | Requires full middleware pipeline in tests | `AuthenticateResult` is a value that can be unit-tested |

### Token storage design

| Field | Type | Notes |
|-------|------|-------|
| `AgentToken.Id` | `Guid` | PK |
| `AgentToken.TokenHash` | `string` (CHAR 64) | Hex-encoded SHA-256 of the raw bearer token |
| `AgentToken.UserId` | `int` (FK to `UserProfile`) | The acting user identity this token maps to |
| `AgentToken.Label` | `string` | Human-readable label set at token creation |
| `AgentToken.CreatedAt` | `DateTime` | Audit |
| `AgentToken.LastUsedAt` | `DateTime?` | Updated on successful auth |
| `AgentToken.RevokedAt` | `DateTime?` | Null = active; non-null = revoked |

**Token issuance:** `RandomNumberGenerator.GetBytes(32)` → base64url-encode → hand to agent as `Bearer <token>`. Store `Convert.ToHexString(SHA256.HashData(rawBytes))` in `TokenHash`.

**Token verification (in `HandleAuthenticateAsync`):**
1. Extract `Authorization: Bearer <token>` header.
2. `Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(rawToken)))` → candidate hash.
3. DB lookup: `await db.AgentTokens.FirstOrDefaultAsync(t => t.TokenHash == candidateHash && t.RevokedAt == null)`.
4. If found: build `ClaimsPrincipal` with `ClaimTypes.NameIdentifier = token.UserId.ToString()`.
5. Update `LastUsedAt` (fire-and-forget `_ = Task.Run(...)` or a queued background write to avoid adding latency to every request).

**Do NOT use `CryptographicOperations.FixedTimeEquals` for the hash lookup** — the DB lookup already provides constant-time-equivalent behavior because the hash string is fixed-length and the DB performs an equality check. `FixedTimeEquals` matters when comparing in-memory buffers where short-circuit evaluation could leak timing info; a DB lookup against a hashed index does not have this property.

---

## Program.cs Additions

The complete set of changes to `Program.cs`, with existing lines annotated for context:

```csharp
// --- ADDITIONS TO SERVICE REGISTRATION ---

// New: MCP server (in-process, HTTP transport)
builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<AgentPantryTools>()    // explicit registration; do not use WithToolsFromAssembly
    .WithTools<AgentRecipeTools>();   // to avoid scanning unrelated assemblies

// New: bearer-token authentication scheme
builder.Services
    .AddAuthentication("AgentToken")
    .AddScheme<AgentTokenAuthOptions, AgentTokenAuthHandler>("AgentToken", _ => { });

// New: authorization policy
builder.Services.AddAuthorization(o =>
    o.AddPolicy("AgentPolicy", p => p.RequireAuthenticatedUser()));

// New: agent operations facade (Application layer)
builder.Services.AddScoped<IAgentFacade, AgentFacade>();
builder.Services.AddScoped<IAgentTokenService, AgentTokenService>();

// ... existing registrations unchanged ...

// --- ADDITIONS TO MIDDLEWARE PIPELINE (after app = builder.Build()) ---

// New: must precede any endpoint that uses RequireAuthorization
app.UseAuthentication();
app.UseAuthorization();

// ... existing: app.UseAntiforgery() stays where it is (does not affect /mcp or /api routes) ...
// ... existing: app.MapStaticAssets() ...
// ... existing: app.UseStaticFiles(...) for /uploads ...

// New: MCP endpoint group
app.MapMcp("/mcp").RequireAuthorization("AgentPolicy");

// ... existing: app.MapRazorComponents<App>().AddInteractiveServerRenderMode() ...
// ... existing: app.MapHealthChecks("/healthz") ...

// New: REST minimal-API endpoints
app.MapGet("/api/v1/pantries", ...).RequireAuthorization("AgentPolicy");
app.MapGet("/api/v1/pantries/{id}/items", ...).RequireAuthorization("AgentPolicy");
app.MapPost("/api/v1/pantries/{id}/items", ...).RequireAuthorization("AgentPolicy");
app.MapPut("/api/v1/pantries/{id}/items/{itemId}", ...).RequireAuthorization("AgentPolicy");
app.MapPost("/api/v1/pantries/{id}/items/{itemId}/deduct", ...).RequireAuthorization("AgentPolicy");
app.MapGet("/api/v1/ingredients/resolve", ...).RequireAuthorization("AgentPolicy");
app.MapPost("/api/v1/recipes", ...).RequireAuthorization("AgentPolicy");
```

**Middleware ordering rationale:**
- `UseAuthentication` + `UseAuthorization` must precede `MapMcp` and `Map*` calls that use `RequireAuthorization` — this is the standard ASP.NET Core ordering requirement.
- `UseAntiforgery` stays where it is; it only fires for Razor component form submissions, not for JSON API or MCP routes.
- `MapMcp` before `MapRazorComponents` is fine; routing is non-exclusive — each `Map*` registers its own pattern.

---

## Installation

```xml
<!-- Add to src/CookBot.Web/CookBot.Web.csproj only -->
<PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.4.0" />
```

```bash
# Equivalent via CLI
dotnet add src/CookBot.Web/CookBot.Web.csproj package ModelContextProtocol.AspNetCore --version 1.4.0
```

`ModelContextProtocol` 1.4.0 is pulled in as a transitive dependency — do not add it explicitly.

No packages are needed for REST endpoints, bearer-token authentication, or token hashing.

---

## Alternatives Considered

| Area | Recommended | Alternative | Why Not |
|------|-------------|-------------|---------|
| MCP transport | `ModelContextProtocol.AspNetCore` 1.4.0 (in-process) | Separate MCP server process + reverse proxy (nginx/YARP) | Adds operational complexity: two processes to manage, inter-process auth, port management; contradicts single-binary self-host ethos |
| MCP transport | `ModelContextProtocol.AspNetCore` 1.4.0 | Hand-roll SSE + JSON-RPC 2.0 MCP protocol | MCP protocol is non-trivial: session management, JSON-RPC 2.0, capability negotiation, streaming partial JSON — 1,000+ lines of protocol plumbing with zero upside |
| Auth | `AuthenticationHandler<TOptions>` (built-in, zero package) | Hand-rolled `IMiddleware` | Doesn't integrate with `RequireAuthorization()` without manual principal-setting; breaks `MapMcp().RequireAuthorization()` (see comparison table above) |
| Auth | Custom opaque token (SHA-256 hashed) | JWT (`Microsoft.AspNetCore.Authentication.JwtBearer`) | JWT requires an extra NuGet package + signing key management. Opaque tokens are simpler, sufficient for trusted-LAN single-host, and keep auth dependencies at zero |
| Token hash | SHA-256 (BCL `SHA256.HashData`) | PBKDF2-HMAC-SHA256 | PBKDF2 is for human passwords (deliberate slowness to resist dictionary attacks). 32-byte random tokens are already full-entropy — SHA-256 is correct and fast |
| REST endpoints | Built-in minimal-API `MapGet/MapPost` | ASP.NET MVC controllers (`AddControllers`) | Controllers add training surface, `[ApiController]` attribute routing overhead, and an `AddControllers()` call. Minimal API already used by `MapHealthChecks`; matches existing style |
| REST endpoints | Built-in minimal-API | FastEndpoints / Carter / Ardalis.ApiEndpoints | External packages, add dependencies, incompatible with dependency-averse posture; unnecessary for ~6-8 agent endpoints |
| Tool registration | `.WithTools<T>()` (explicit per-class) | `.WithToolsFromAssembly()` (assembly scan) | Explicit is preferred for a dependency-averse project: it is obvious what is registered, avoids accidentally exposing internal classes, and does not require a whole-assembly reflection scan |

---

## What NOT to Add

| Package | Why Not | Already Handled By |
|---------|---------|-------------------|
| `Microsoft.Extensions.AI` | Hard project constraint (CLAUDE.md). Existing `AnthropicAiService` HttpClient pattern is sufficient; no agent-interface benefit justifies the refactor | Existing `IAiService` / `AnthropicAiService` |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Tokens are opaque random values, not JWTs; JWT introduces signing key management overhead; not needed for trusted-LAN bearer tokens | `AuthenticationHandler<TOptions>` (built-in) + SHA-256 |
| `OpenIddict` / `Duende.IdentityServer` / any OAuth server | Explicit out-of-scope (locked decision: no Identity/OAuth/SSO) | `AgentTokenAuthHandler` (hand-rolled) |
| `YARP` / `Ocelot` (API gateway) | Adds a routing layer in front of Kestrel; unnecessary for single-host self-hosted; `MapMcp("/mcp")` routes cleanly | Built-in endpoint routing |
| `Newtonsoft.Json` / `NJsonSchema` | Hard project constraint (CLAUDE.md). 100% STJ codebase. Agent-submitted `RecipeDocument` schema validation uses `JsonSchema.Net` which is already installed | `JsonSchema.Net` (already in project) |
| `ModelContextProtocol` (main, no AspNetCore) | Entry point for console/hosted-service MCP; lacks `MapMcp()` and `WithHttpTransport()`; the `.AspNetCore` package brings this transitively anyway | `ModelContextProtocol.AspNetCore` (which brings it) |
| `ModelContextProtocol.Core` (standalone) | Client-only / low-level API; do not reference alongside `.AspNetCore` to avoid version drift | `ModelContextProtocol.AspNetCore` (which brings Core transitively) |
| `Microsoft.AspNetCore.Identity` | Explicit out-of-scope (locked decision). Per-agent token auth is the scoped exception to the no-auth posture; it does not require the full Identity stack | `AgentTokenAuthHandler` + `AgentToken` EF entity |

---

## Version Compatibility

| Package | Version | .NET Targets | Transitive Deps |
|---------|---------|-------------|-----------------|
| `ModelContextProtocol.AspNetCore` | 1.4.0 | net8.0, net9.0, net10.0 | `ModelContextProtocol` >= 1.4.0 |
| `ModelContextProtocol` (transitive) | 1.4.0 | net8.0, net9.0, net10.0, netstandard2.0 | `Microsoft.Extensions.Caching.Abstractions` >= 10.0.7, `Microsoft.Extensions.Hosting.Abstractions` >= 10.0.7, `ModelContextProtocol.Core` >= 1.4.0 |

All `Microsoft.Extensions.*` transitive packages are already included via `Microsoft.AspNetCore.App`. No version conflicts expected on .NET 10.

---

## Sources

- [NuGet: ModelContextProtocol.AspNetCore 1.4.0](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore/) — version, Apache-2.0 license, .NET targets, dependency (verified)
- [NuGet: ModelContextProtocol 1.4.0](https://www.nuget.org/packages/ModelContextProtocol/) — version, license, transitive deps (verified)
- [GitHub: modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — v1.4.0 stable release confirmed 2026-06-04; `ProtectedMcpServer` sample auth pattern
- [MCP C# SDK Getting Started](https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html) — `AddMcpServer`, `WithHttpTransport`, `MapMcp`, `[McpServerToolType]`, `[McpServerTool]`, DI injection in tool parameters
- [DEV.to: Add the MCP server to the ASP.NET Core minimal API](https://dev.to/ohalay/add-the-mcp-server-to-the-aspnet-core-minimal-api-4331) — coexistence with existing `app.MapEndpoints()` + `app.MapMcp(pattern)` on same `WebApplication` confirmed; `RequireAuthorization()` chaining shown
- [MS Learn: Authentication and authorization in Minimal APIs (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/security?view=aspnetcore-10.0) — `AuthenticationHandler<TOptions>` as idiomatic pattern; `WebApplication` auto-registers auth middleware after `AddAuthentication`
- [codewithmukesh.com: API Key Authentication ASP.NET Core .NET 10](https://codewithmukesh.com/blog/api-key-authentication-aspnet-core/) — full handler implementation pattern; `CryptographicOperations.FixedTimeEquals` usage context
- [Apache Software Foundation: Apache License v2.0 and GPL Compatibility](https://www.apache.org/licenses/GPL-compatibility.html) — Apache 2.0 in GPL-3.0-only project confirmed compatible (one-way)

---

*Stack research for: FreelovesCookBot v1.5 External Agent Interface (MCP + REST API)*
*Researched: 2026-06-26*
