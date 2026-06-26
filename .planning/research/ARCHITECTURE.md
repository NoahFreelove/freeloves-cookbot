# Architecture Research

**Domain:** MCP + REST API integration into existing Clean/Onion Blazor Server app (v1.5)
**Researched:** 2026-06-26
**Confidence:** HIGH — based on direct codebase inspection of all named files

---

## 1. Where Does the Agent-Operations Facade Live?

**Decision: Application layer — `CookBot.Application/Agent/AgentOperationsFacade.cs`**

The facade is a scoped Application-layer class that composes `PantryService` and `RecipeService`. It is the single entry point for every agent operation. Both the REST minimal-API and the MCP tool classes call it; neither layer contains any business logic or authorization logic of their own.

Rationale:

- `PantryService` and `RecipeService` already take explicit `userId` parameters and enforce ownership inside themselves (`cookbook.UserId != userId` throws `UnauthorizedAccessException`; `pantry.OwnerId != actingUserId` returns false). The authorization invariant is already in the right layer. The facade must pass the `userId` through; it must never bypass it.
- The Application layer has no framework references. The facade can be unit-tested without any HTTP or MCP stack.
- Web-layer services (`CurrentUserService`, `AiApiKeyResolutionService`) stay in `CookBot.Web`. The facade does NOT take a `CurrentUserService` dependency — it takes an explicit `int userId` obtained from the auth middleware before the call reaches it.
- Register the facade as `AddScoped<AgentOperationsFacade>()` inside `CookBot.Application/DependencyInjection.cs` alongside `RecipeService` and `PantryService`.

The facade surface (operations needed):

```
AgentOperationsFacade(PantryService, RecipeService, RecipeValidator,
                      JsonRecipeSerializer, RecipeUpcasterChain,
                      ILogger<AgentOperationsFacade>)

ListAccessiblePantriesAsync(int userId) → IReadOnlyList<Pantry>
ListPantryItemsAsync(int userId, int pantryId) → IReadOnlyList<PantryItem>
AddOrUpdatePantryItemAsync(int userId, int pantryId, int ingredientId,
                           double amount, string unit, DateTime? expiration)
DeductPantryItemAsync(int userId, int pantryId, int ingredientId,
                      double amount, string unit)
ResolveIngredientAsync(string name) → Ingredient?    (read-only name→id lookup)
SubmitRecipeAsync(int userId, int cookbookId, RecipeDocument doc) → Recipe
```

`SubmitRecipeAsync` owns the validation + conversion step:

1. If `doc.Version < RecipeUpcasterChain.CurrentVersion`, call `RecipeUpcasterChain.UpcastToCurrent(doc)` first.
2. Run `RecipeValidator.Validate(doc)` — if `!IsValid`, return a structured error (never throw).
3. Convert `RecipeDocument` → `ParsedRecipe` using `RecipeDocumentConverter.ToParsedRecipe(doc)` (new pure static helper in `CookBot.Application/Recipes/`).
4. Call `RecipeService.CreateAsync(cookbookId, userId, parsedRecipe)`.

The authorization for pantry write operations goes through the facade's ownership check:

- `ListPantryItemsAsync` — facade calls `PantryService.GetAccessiblePantriesAsync(userId)` to confirm `pantryId` is in that set before calling `GetPantryItemsAsync(pantryId)`. Without this check, any valid token could read any pantry by ID, because `PantryService.GetPantryItemsAsync` takes a bare `pantryId` with no caller-identity check.
- `AddOrUpdatePantryItemAsync` / `DeductPantryItemAsync` — same pattern: confirm `userId` owns or is a member of `pantryId` before calling the mutating methods.
- `RecipeService.CreateAsync` checks `cookbook.UserId != userId` internally — no extra check needed in the facade for recipe creation.

---

## 2. The Identity Problem — Recommendation

**Recommendation: A request-scoped `IAgentContext` accessor, populated by auth middleware. `CurrentUserService` is not touched.**

Reading `CurrentUserService.cs` directly: `CurrentUserId` is a mutable property on a scoped service. It is set once during a Blazor circuit's lifetime by `InitializeAsync()`, which picks the first user from the DB. There is no per-request injection path — the property is set imperatively by Blazor pages calling `InitializeAsync()`.

For HTTP requests (REST endpoints and MCP transport), there is no Blazor circuit. A new DI-scoped abstraction is needed that:

1. Does not modify `CurrentUserService` at all (preserves the Blazor path unchanged).
2. Is populated by middleware before any endpoint or tool handler runs.
3. Can be read in endpoint/tool handlers to extract the acting `userId` before calling the facade.

**Concrete recommendation:**

```csharp
// CookBot.Application/Agent/IAgentContext.cs
// Lives in Application because the facade can optionally take it as a dependency.
public interface IAgentContext
{
    int ActingUserId { get; }
    bool IsAuthenticated { get; }
}

// CookBot.Web/Agent/AgentContext.cs
// Mutable implementation — lives in Web because only middleware sets it.
public sealed class AgentContext : IAgentContext
{
    public int ActingUserId { get; set; }
    public bool IsAuthenticated => ActingUserId > 0;
}
```

Registration in `Program.cs`:

```csharp
builder.Services.AddScoped<AgentContext>();
builder.Services.AddScoped<IAgentContext>(sp => sp.GetRequiredService<AgentContext>());
```

Auth middleware sets `AgentContext.ActingUserId` from the resolved token:

```csharp
// CookBot.Web/Agent/AgentTokenMiddleware.cs
public class AgentTokenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, CookBotDbContext db,
                                   AgentContext agentCtx)
    {
        if (ctx.Request.Path.StartsWithSegments("/api") ||
            ctx.Request.Path.StartsWithSegments("/mcp"))
        {
            var raw = ctx.Request.Headers.Authorization
                .FirstOrDefault()?.Replace("Bearer ", "");
            if (raw != null)
            {
                var hash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
                var token = await db.AgentTokens
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TokenHash == hash
                                           && t.IsActive
                                           && (t.ExpiresAt == null
                                               || t.ExpiresAt > DateTime.UtcNow));
                if (token != null)
                    agentCtx.ActingUserId = token.UserId;
            }
        }
        await next(ctx);
    }
}
```

Note: injecting `CookBotDbContext` and `AgentContext` as `InvokeAsync` parameters (not constructor parameters) is the correct ASP.NET Core pattern for middleware that consumes scoped services. Constructor injection of scoped services into middleware creates a captive dependency.

The facade's endpoint handlers extract `actingUserId` from `IAgentContext`, not from `CurrentUserService`. The Blazor circuit path continues to call `CurrentUserService.InitializeAsync()` exactly as before — the two paths never share state.

**Why not the other options:**

- **Passing userId via query-string/header parameter (no middleware):** Requires endpoint handlers to extract and validate identity themselves before the service call — duplicates the auth concern across every endpoint and every MCP tool handler. Also pushes identity concern into business logic.
- **Mutating `CurrentUserService` from middleware:** `CurrentUserService.CurrentUserId` is set by Blazor pages. Middleware running before the Blazor circuit would race with `InitializeAsync()` and break the first-user auto-select logic the Blazor UI depends on.
- **ASP.NET `ClaimsPrincipal` / `HttpContext.User`:** Requires wiring a custom auth scheme via `AddAuthentication`/`AddScheme`. Works, but adds ASP.NET Identity concepts (claims, handlers, challenge/forbid) to a codebase with a deliberate no-Identity posture. The `IAgentContext` approach is structurally equivalent and significantly simpler.

**DI scoping: Blazor circuits vs HTTP requests**

Blazor Server uses per-circuit scopes created by the SignalR hub. HTTP requests use per-request scopes created by the ASP.NET request pipeline. Both are "scoped" in DI terms but are different scope roots. Because `AgentContext` is registered as `AddScoped`, it is a different instance for each HTTP request vs each Blazor circuit — they cannot collide. `CurrentUserService` is also `AddScoped`, carrying the same guarantee: the HTTP path gets its own `CurrentUserService` instance with `CurrentUserId == null` (never set by middleware), and the Blazor path gets its own `AgentContext` instance with `IsAuthenticated == false` (never touched by middleware for `/_blazor` paths).

---

## 3. How REST and MCP Share the Same DI Scope and Auth

Both REST endpoints and MCP tool handlers run inside per-HTTP-request DI scopes. This is automatic for minimal-API endpoints in ASP.NET Core. The MCP in-process server (using the `ModelContextProtocol` C# SDK) must be wired so each tool invocation runs inside its own `IServiceScope`.

The practical wiring:

```
HTTP request arrives (REST or MCP transport)
    |
AgentTokenMiddleware runs for /api/* and /mcp/* paths
    - Sets AgentContext.ActingUserId in request scope
    |
Minimal-API endpoint handler OR MCP tool dispatcher
    |
Both inject AgentOperationsFacade from the same request scope
    |
AgentOperationsFacade injects IAgentContext (same scope = same ActingUserId)
    |
Calls PantryService / RecipeService (same scope = same CookBotDbContext transaction)
```

The `ModelContextProtocol` C# SDK's `[McpServerTool]` attribute approach registers tools but dispatches each invocation through a provided `IServiceProvider`/`IServiceScope`. Verify during planning that the SDK creates a scope per call — if it uses a root singleton scope for tool resolution, you must force a scope via `IServiceScopeFactory`. This is the primary MCP-specific integration risk to confirm during Phase planning.

For the HTTP/SSE MCP transport, register the MCP server endpoint alongside the REST endpoints in `Program.cs`:

```csharp
app.UseMiddleware<AgentTokenMiddleware>();   // must be before MapMcp / MapGroup

app.MapGroup("/api/v1").MapAgentEndpoints();
app.MapMcp("/mcp");
```

Both `/api/*` and `/mcp/*` path prefixes are intercepted by `AgentTokenMiddleware`. The middleware runs once per HTTP request regardless of which downstream handler picks it up.

---

## 4. New Components and Build Order

### New components

| Component | Project | Type | Notes |
|-----------|---------|------|-------|
| `AgentToken` entity | `CookBot.Domain/Entities/` | EF entity | `TokenHash` (SHA-256 hex, unique indexed), `UserId` FK→`Users`, `Label`, `IsActive`, `CreatedAt`, `ExpiresAt?` |
| `AgentTokenConfiguration` | `CookBot.Infrastructure/Data/Configurations/` | EF config | Unique index on `TokenHash`; cascade delete when user deleted |
| `AgentToken` DbSet in `CookBotDbContext` | `CookBot.Infrastructure/Data/` | EF DbSet | One line addition to existing context |
| EF migration `AddAgentTokens` | `CookBot.Infrastructure/Migrations/` | migration | `dotnet ef migrations add AddAgentTokens` |
| `IAgentContext` interface | `CookBot.Application/Agent/` | interface | `ActingUserId`, `IsAuthenticated` |
| `AgentContext` mutable impl | `CookBot.Web/Agent/` | scoped service | Set by middleware; satisfies `IAgentContext` |
| `AgentTokenMiddleware` | `CookBot.Web/Agent/` | ASP.NET middleware | Bearer token → SHA-256 hash → DB lookup → sets `AgentContext` |
| `AgentOperationsFacade` | `CookBot.Application/Agent/` | scoped service | Owns all agent business operations; takes explicit `userId` param |
| `RecipeDocumentConverter` | `CookBot.Application/Recipes/` | static helper class | `RecipeDocument → ParsedRecipe` field-by-field mapping; pure, no DI |
| `AgentEndpoints` | `CookBot.Web/Agent/` | minimal-API endpoint module | `MapAgentEndpoints()` extension method called in `Program.cs` |
| `AgentMcpTools` | `CookBot.Web/Agent/` | MCP tool class | One `[McpServerTool]` method per operation; delegates to `AgentOperationsFacade` |

### Modified files (minimal)

| File | Change |
|------|--------|
| `CookBot.Domain/Entities/` | Add `AgentToken.cs` |
| `CookBot.Infrastructure/Data/CookBotDbContext.cs` | Add `DbSet<AgentToken> AgentTokens` |
| `CookBot.Infrastructure/Data/Configurations/` | Add `AgentTokenConfiguration.cs` |
| `CookBot.Application/DependencyInjection.cs` | Add `services.AddScoped<AgentOperationsFacade>()` |
| `Program.cs` | Add `AgentContext`+`IAgentContext` registrations, `AddMcpServer().WithTools<AgentMcpTools>()`, `UseMiddleware<AgentTokenMiddleware>()`, `MapGroup("/api/v1").MapAgentEndpoints()`, `MapMcp("/mcp")` |

### Build order (dependency-respecting sequence)

**Phase A — Foundation (domain + auth plumbing; no HTTP surface yet)**

1. `AgentToken` entity in `CookBot.Domain/Entities/` — no dependencies.
2. `IAgentContext` interface in `CookBot.Application/Agent/` — no dependencies.
3. `AgentTokenConfiguration` + `DbSet<AgentToken>` in `CookBot.Infrastructure` — depends on step 1.
4. EF migration `AddAgentTokens` — depends on step 3; run `dotnet ef migrations add AddAgentTokens`.
5. `RecipeDocumentConverter` static helper in `CookBot.Application/Recipes/` — depends on `RecipeDocument` and `ParsedRecipe` (both already exist).
6. `AgentOperationsFacade` in `CookBot.Application/Agent/` — depends on steps 2, 5, and existing `PantryService`, `RecipeService`, `RecipeValidator`.
7. Register `AgentOperationsFacade` in `CookBot.Application/DependencyInjection.cs` — depends on step 6.

**Phase B — Auth layer**

8. `AgentContext` mutable class in `CookBot.Web/Agent/` — implements `IAgentContext` (step 2).
9. `AgentTokenMiddleware` in `CookBot.Web/Agent/` — depends on step 3 (DB lookup) and step 8 (to set `AgentContext`).
10. Wire `AgentContext`, `IAgentContext`, and `UseMiddleware<AgentTokenMiddleware>()` in `Program.cs` — depends on steps 8, 9. Auth plumbing is live; nothing serves it yet.

**Phase C — REST API surface**

11. `AgentEndpoints` extension method in `CookBot.Web/Agent/` — depends on step 6 (`AgentOperationsFacade`) and step 2 (`IAgentContext`). Each handler: confirm `agentCtx.IsAuthenticated` → 401 if false; extract `actingUserId`; call facade; map result to HTTP response.
12. Wire `MapGroup("/api/v1").MapAgentEndpoints()` in `Program.cs` — depends on step 11.

**Phase D — MCP server**

13. Add `ModelContextProtocol` NuGet to `CookBot.Web.csproj` — verify MIT license is compatible with GPL-3.0-only (it is: GPL may consume MIT).
14. `AgentMcpTools` class in `CookBot.Web/Agent/` — depends on step 6 (`AgentOperationsFacade`) and step 2 (`IAgentContext`). Each `[McpServerTool]`-attributed method mirrors one `AgentEndpoints` handler.
15. Register `AddMcpServer().WithTools<AgentMcpTools>()` and `MapMcp("/mcp")` in `Program.cs` — depends on steps 13, 14.

**Phase E — Token management UI (admin surface; optional but needed for operations)**

16. Minimal Blazor admin page for creating/revoking agent tokens — depends on all of the above; accesses `AgentToken` table via a scoped web-layer service. Token creation: `RandomNumberGenerator.GetBytes(32)` → base64url-encode → show once → SHA-256 → store hash only.

---

## 5. Data-Flow Diagrams

### Flow A: Agent pantry operation (add/deduct/list)

```
External agent
    | HTTP POST /api/v1/pantry/{pantryId}/items
    | Authorization: Bearer <raw-token>
    v
[Kestrel HTTP pipeline]
    v
AgentTokenMiddleware.InvokeAsync
    | hash = SHA256(raw-token)
    | AgentToken row = db.AgentTokens
    |     WHERE TokenHash=hash AND IsActive
    |     AND (ExpiresAt IS NULL OR ExpiresAt > utcnow)
    | if found: agentCtx.ActingUserId = token.UserId
    v
AgentEndpoints.AddOrUpdateItem handler
    | agentCtx.IsAuthenticated? no -> HTTP 401
    | int userId = agentCtx.ActingUserId
    | deserialize body -> (ingredientId, amount, unit, expiration?)
    v
AgentOperationsFacade.AddOrUpdatePantryItemAsync(userId, pantryId, ...)
    | Step 1: PantryService.GetAccessiblePantriesAsync(userId)
    |         -> confirm pantryId in accessible set
    |         -> if not: throw UnauthorizedAccessException -> HTTP 403
    | Step 2: PantryService.AddOrUpdateAsync(pantryId, ingredientId,
    |                                         amount, unit, expiration)
    |         -> IRepository<PantryItem>.FindAsync(p => p.PantryId==pantryId
    |                                              && p.IngredientId==ingredientId)
    |         -> if exists: update Amount (unit-convert if compatible)
    |            else:      IRepository<PantryItem>.AddAsync(new PantryItem{...})
    |         -> CookBotDbContext.SaveChangesAsync()
    v
AgentEndpoints handler
    | HTTP 200 with updated PantryItem as JSON
    v
External agent receives response
```

### Flow B: Agent structured recipe creation

```
External agent
    | HTTP POST /api/v1/recipes
    | Authorization: Bearer <raw-token>
    | Body: { "cookbookId": 7, "recipe": { "version": 4, "name": "...", ... } }
    v
AgentTokenMiddleware.InvokeAsync
    | resolves token -> agentCtx.ActingUserId = userId
    v
AgentEndpoints.SubmitRecipe handler
    | agentCtx.IsAuthenticated? no -> HTTP 401
    | deserialize body -> (int cookbookId, RecipeDocument doc)
    v
AgentOperationsFacade.SubmitRecipeAsync(userId, cookbookId, doc)
    | Step 0: if doc.Version < CurrentVersion:
    |         RecipeUpcasterChain.UpcastToCurrent(doc) -> current doc
    |
    | Step 1: RecipeValidator.Validate(doc)
    |         -> if !IsValid: return ValidationErrors -> HTTP 422
    |            (never throws; errors list names the failing fields)
    |
    | Step 2: RecipeDocumentConverter.ToParsedRecipe(doc)
    |         -> pure static mapping, field-by-field:
    |            doc.Name -> parsed.Name
    |            doc.Ingredients[i] -> ParsedIngredient (LocalId, Name, Amount,
    |                                   Unit, Note, Substitutions)
    |            doc.Steps[i] (ContentStep) -> ParsedStep (Text, Timers,
    |                                   Temperature, DonenessCue)
    |            doc.Steps[i] (SectionStep) -> ParsedStep (IsSection=true)
    |            doc.Equipment -> parsed.Equipment
    |            doc.Provenance -> parsed.Provenance
    |            etc.
    |
    | Step 3: RecipeService.CreateAsync(cookbookId, userId, parsedRecipe)
    |         -> cookbook = _cookbookRepo.GetByIdAsync(cookbookId)
    |            cookbook.UserId != userId -> UnauthorizedAccessException -> HTTP 403
    |         -> foreach ingredient: ResolveIngredientAsync(name)
    |            (creates Ingredient row if not found by NormalizedName)
    |         -> builds Recipe + RecipeIngredient + RecipeStep + RecipeTag entities
    |         -> constructs RecipeDocument from parsed (v4)
    |         -> recipe.CanonicalDocumentJson = JsonRecipeSerializer.Serialize(doc)
    |         -> MarkNutritionCacheStaleIfChangedAsync (no-op: recipe.Id==0 on create)
    |         -> IRepository<Recipe>.AddAsync(recipe) -> SaveChangesAsync
    |         -> returns Recipe entity with Id assigned
    v
AgentEndpoints handler
    | HTTP 201 Created
    | Location: /recipes/{recipe.Id}
    | Body: { "id": recipe.Id, "name": recipe.Name }
    v
External agent receives response
```

---

## System Overview

```
+---------------------------------------------------------------------+
|  CookBot.Web (ASP.NET Core host — existing + new)                   |
|                                                                     |
|  +------------------+  +-------------------+  +------------------+ |
|  | Blazor Server    |  | REST minimal API  |  | MCP Server       | |
|  | (SignalR /       |  | /api/v1/*         |  | /mcp (SSE)       | |
|  |  _blazor)        |  | AgentEndpoints    |  | AgentMcpTools    | |
|  | CurrentUser      |  +--------+----------+  +--------+---------+ |
|  | Service          |           |                      |           |
|  +------+-----------+           +----------+-----------+           |
|         |                                  |                       |
|         |                    AgentTokenMiddleware                   |
|         |                 (runs for /api/* + /mcp/* only)          |
|         |                 (token hash -> AgentContext.ActingUserId) |
|         |                                  |                       |
|         |              +-------------------+                       |
|         |              | IAgentContext (scoped, set by middleware)  |
+---------|--------------|-------------------------------------------+
          |              |
+---------|--------------|-----------------------------------------+
|  CookBot.Application                                              |
|         |              |                                         |
|         |     +--------v----------------------------------+      |
|         |     |       AgentOperationsFacade               |      |
|         |     |  (explicit userId param; never bypasses   |      |
|         |     |   ownership checks in sub-services)       |      |
|         |     +--------+--------------------------+--------+      |
|         |              |                          |              |
|  +------v------+  +----v--------+  +-------------v-----------+  |
|  |RecipeService|  |PantryService|  | RecipeValidator +        |  |
|  |(authz inside|  |(read access |  | RecipeDocumentConverter  |  |
|  | CreateAsync)|  | guard in    |  | (new static helper)      |  |
|  |             |  | facade)     |  | RecipeUpcasterChain      |  |
|  +------+------+  +----+--------+  +-------------------------+  |
+---------|--------------|-----------------------------------------+
          |              |
+---------|--------------|---------------------------------------+
|  CookBot.Infrastructure                                        |
|         |              |                                      |
|  +-------v--------------v------------------------------------+ |
|  |         CookBotDbContext (EF Core + SQLite)               | |
|  |  Existing tables: Users, Cookbooks, Recipes, Pantries,   | |
|  |                   PantryItems, PantryMembers, ...         | |
|  |  New table:       AgentTokens                             | |
|  +-----------------------------------------------------------+ |
+---------------------------------------------------------------+
```

---

## Architectural Patterns

### Pattern 1: Explicit userId threading — do not inject CurrentUserService into the facade

**What:** Every facade method takes `int userId` as a parameter. The endpoint handler or MCP tool extracts it from `IAgentContext.ActingUserId` before calling the facade. The facade never resolves the acting user itself.

**When to use:** Always, for agent operations.

**Trade-offs:** Slightly more boilerplate at call sites; avoids ambient identity coupling that would make the facade untestable in isolation without mocking HTTP context.

### Pattern 2: Ownership guard in facade for PantryService mutations

**What:** The facade calls `PantryService.GetAccessiblePantriesAsync(userId)` and confirms the target `pantryId` appears in the result before calling write methods. `PantryService.AddOrUpdateAsync` and `PantryService.DeductAsync` accept a raw `pantryId` without re-checking caller ownership.

**When to use:** Whenever the facade wraps a PantryService method that does not internally verify the acting user is authorized for the target resource. (`RecipeService.CreateAsync` does check `cookbook.UserId != userId`; `PantryService.AddOrUpdateAsync` does not check whether `userId` owns `pantryId`.)

**Trade-offs:** One extra DB round-trip per write (the accessible-pantries query). Negligible at self-hosted scale.

### Pattern 3: RecipeDocumentConverter as a pure static helper

**What:** `RecipeDocumentConverter.ToParsedRecipe(RecipeDocument doc) → ParsedRecipe` is a static method with no DI dependencies. It maps field-by-field from the canonical POCO to the `ParsedRecipe` DTO that `RecipeService.CreateAsync` already accepts.

**When to use:** Structured-submit path only. The human editor path produces `ParsedRecipe` via `RecipeFormatParser.Parse`; the AI path produces `RecipeDocument` then passes through an equivalent inline mapping inside `AiRecipeGenerator`. The agent path receives `RecipeDocument` over the wire, so an explicit converter is needed.

**Canonical boundary warning:** The canonical-doc three-boundaries rule applies to this converter. If a new `RecipeDocument` field is added in a future milestone, `RecipeDocumentConverter.ToParsedRecipe` must be updated alongside the POCO, `ParsedRecipe` DTO, and `RecipeService.CreateAsync`. Omitting the converter from the update checklist causes the agent submit path to silently drop the new field while every other path carries it correctly.

### Pattern 4: Middleware path-prefix guard

**What:** `AgentTokenMiddleware` only activates for `/api/*` and `/mcp/*` paths. All other paths (`/_blazor` SignalR, `/uploads` static files, `/healthz`) skip the middleware body and proceed immediately to `next(ctx)`.

**When to use:** Any middleware that should not run for the Blazor Server circuit.

**Trade-offs:** Without the guard, the middleware runs for every `/_blazor` WebSocket frame, adding a DB round-trip per frame — catastrophic for existing Blazor UI performance.

---

## Data Flow

### Blazor path (unchanged)

```
Browser (SignalR) -> /_blazor hub -> Blazor circuit scope
    |
CurrentUserService.InitializeAsync()  (called by Login.razor or App.razor)
    |
Sets CurrentUserService.CurrentUserId = firstUser.Id
    |
Razor components read CurrentUserService.CurrentUserId
    |
Services (RecipeService, PantryService) called with explicit userId from CurrentUserService
```

### Agent path (new)

```
Agent HTTP request -> Kestrel -> AgentTokenMiddleware
    |
AgentContext.ActingUserId set from token→UserId lookup
    |
AgentEndpoints / AgentMcpTools handler
    |
AgentOperationsFacade called with userId = agentCtx.ActingUserId
    |
PantryService / RecipeService called with explicit userId (same as Blazor path)
```

The two paths share `PantryService`, `RecipeService`, and `CookBotDbContext` registration — but each gets its own scoped instance. They are not wired together at the identity layer. The authorization invariant (ownership check inside services) fires identically for both paths.

---

## Integration Points

### External Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| REST API client <-> AgentEndpoints | HTTP/JSON on existing Kestrel host | First HTTP endpoints in the app; wired via minimal-API |
| MCP client <-> AgentMcpTools | HTTP/SSE on existing Kestrel host | `ModelContextProtocol` C# SDK manages transport |
| AgentTokenMiddleware <-> CookBotDbContext | EF Core `AsNoTracking` lookup per request | SHA-256 unique index on `AgentToken.TokenHash`; fast |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `AgentEndpoints` / `AgentMcpTools` <-> `AgentOperationsFacade` | Direct method call; same DI scope | Both in `CookBot.Web`; facade in `CookBot.Application` |
| `AgentOperationsFacade` <-> `PantryService` + `RecipeService` | Method call; same DI scope | Shared `CookBotDbContext` — mutations are atomic within the request |
| `AgentOperationsFacade` <-> `IAgentContext` | DI injection | Not needed if facade takes explicit `userId`; only needed if facade reads identity itself (not recommended — see Anti-Patterns) |

### Existing surfaces not modified

| Surface | Why unchanged |
|---------|---------------|
| `CurrentUserService` | Not referenced by agent path; Blazor circuits own it entirely |
| `AiApiKeyResolutionService` | Not used by agent path (agent recipe submit has no inbound AI call) |
| `AnthropicAiService` / `IAiRecipeGenerator` | Not invoked for structured-submit |
| `/_blazor` SignalR hub | Middleware path-prefix guard skips it |
| `RecipeDocument` POCO | Read-only input on agent path; no new fields needed for v1.5 |

---

## Anti-Patterns

### Anti-Pattern 1: Injecting CurrentUserService into AgentOperationsFacade

**What people do:** Inject `CurrentUserService` into `AgentOperationsFacade` and call `CurrentUserService.CurrentUserId` instead of taking an explicit `userId` parameter.

**Why it's wrong:** `CurrentUserService.CurrentUserId` is `null` in the headless HTTP path (`InitializeAsync` is never called). The facade would silently act as unauthenticated or would need defensive null-checks that duplicate the middleware's auth decision.

**Do this instead:** Pass explicit `int userId` to every facade method; extract from `IAgentContext.ActingUserId` in the endpoint or tool handler before calling the facade.

### Anti-Pattern 2: Business logic in AgentEndpoints or AgentMcpTools

**What people do:** Add ownership checks, validation, or pantry membership lookups directly in endpoint handler lambdas or MCP tool methods to save a layer.

**Why it's wrong:** REST and MCP handlers become two separate implementations of the same business logic. When a rule changes, it must be fixed in two places independently. Tests must cover both.

**Do this instead:** All business logic, ownership checks, and validation live in `AgentOperationsFacade`. Handlers do only: (a) confirm `IsAuthenticated`, (b) deserialize/map input, (c) call the facade, (d) map the result to a response shape.

### Anti-Pattern 3: Storing raw bearer tokens in AgentToken

**What people do:** Persist the raw bearer token string in `AgentToken.Token` for comparison on each request.

**Why it's wrong:** A DB compromise exposes every token, giving immediate write access to all users' data. Even on a trusted LAN, defense-in-depth is warranted.

**Do this instead:** Generate tokens as 32 random bytes (`RandomNumberGenerator.GetBytes(32)`) → base64url-encode → show to the admin exactly once → store only `SHA-256(raw-token)` as `TokenHash`. The middleware hashes the incoming bearer token and compares to the stored hash. The raw token is never persisted anywhere.

### Anti-Pattern 4: Routing agent recipe submission through RecipeService.CreateFromTextAsync

**What people do:** Serialize the incoming `RecipeDocument` back to YAML text and call `RecipeService.CreateFromTextAsync` to avoid writing `RecipeDocumentConverter`.

**Why it's wrong:** The YAML serialization is lossy for some fields (timer chip metadata, temperature precision, doneness cues, substitution detail). More importantly, `RecipeFormatParser.Parse` applies heuristics designed for human-authored free-form text — those heuristics are inappropriate for a structured validated JSON document. The agent submit path should be strict: validate, convert, persist.

**Do this instead:** `RecipeDocumentConverter.ToParsedRecipe(doc)` → `RecipeService.CreateAsync`. Direct, lossless, no parser heuristics on structured input.

### Anti-Pattern 5: Registering AgentMcpTools as a singleton

**What people do:** Register `AgentMcpTools` as a singleton to reduce object allocation per call.

**Why it's wrong:** `AgentMcpTools` must inject `AgentOperationsFacade` (scoped) and `IAgentContext` (scoped). A singleton cannot safely consume scoped services — the first request's `EF DbContext` and user identity would be captured permanently, causing data corruption and security failures on subsequent requests.

**Do this instead:** Register `AgentMcpTools` as transient (or scoped) and let the MCP SDK create a scope per tool invocation. Verify the SDK's default scope behavior during phase planning.

### Anti-Pattern 6: AgentTokenMiddleware running on all paths

**What people do:** Register `AgentTokenMiddleware` without a path guard, letting it run on every request including `/_blazor` WebSocket frames.

**Why it's wrong:** Each `/_blazor` frame triggers the middleware body, which performs a DB lookup. A typical Blazor Server session generates dozens of frames per second during active use. This adds an indexed DB read per frame — no functional breakage, but a measurable performance regression on the existing Blazor UI.

**Do this instead:** Guard with `if (ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Path.StartsWithSegments("/mcp"))` before any DB access.

---

## Scaling Considerations

This app is deliberately self-hosted, single-host, trusted-LAN. The agent interface does not change that posture.

| Concern | At self-host scale | Mitigation |
|---------|--------------------|------------|
| Token lookup per request | 1 indexed DB read per `/api` or `/mcp` request | SHA-256 hex unique index on `AgentToken.TokenHash`; SQLite handles this without contention |
| Concurrent agent + Blazor writes | Single SQLite writer lock serializes all writes | Acceptable at self-host scale; SQLite WAL mode or Postgres would be needed at multi-user concurrency scale |
| MCP per-invocation scope creation | Negligible for personal/household use | Not a concern until much higher call rates |

---

## Sources

- Direct codebase inspection: `CurrentUserService.cs`, `Program.cs`, `PantryService.cs`, `RecipeService.cs`, `CookBotDbContext.cs`, `AiApiKeyResolutionService.cs`, `CookBot.Application/DependencyInjection.cs`, `CookBot.Infrastructure/DependencyInjection.cs`, `IRecipeFormatParser.cs` (ParsedRecipe definition), `RecipeValidator.cs`
- Pattern precedent in this codebase: `AiApiKeyResolutionService` (Data Protection + DB lookup per request — structurally analogous to token lookup); `RecipePhotoService` (Infrastructure service that bypasses `IRepository<T>` for direct context access — same rationale applies to `AgentTokenMiddleware` using `CookBotDbContext` directly)
- ASP.NET Core middleware scoped service injection pattern: inject scoped deps as `InvokeAsync` method parameters, not constructor parameters, to avoid captive dependency

---
*Architecture research for: CookBot v1.5 External Agent Interface (MCP + REST API)*
*Researched: 2026-06-26*
