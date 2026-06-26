# Feature Research

**Domain:** External agent interface — MCP server + REST API for pantry management and recipe creation — v1.5 additive milestone on top of a self-hosted Blazor Server cooking tracker
**Researched:** 2026-06-26
**Confidence:** HIGH for pantry/recipe operations (grounded in actual PantryService.cs + RecipeService.cs + RecipeDocument.cs); MEDIUM for MCP tool design patterns (official spec fetched from modelcontextprotocol.io); MEDIUM for SDK mechanics (ModelContextProtocol C# SDK 1.4.0 docs)

---

## Grounding: What Already Exists in the Codebase

The feature design is constrained by the actual service method signatures. The agent-operations facade wraps these methods — no reimplementation.

### PantryService methods available to the facade

| Method | Signature summary | Notes |
|--------|------------------|-------|
| `GetAccessiblePantriesAsync` | `(int userId) → List<Pantry>` | Returns owned + member pantries, personal first. |
| `GetPantryItemsAsync` | `(int pantryId) → IReadOnlyList<PantryItem>` | Items for one pantry. No authz check — facade must verify access. |
| `GetAllUserAccessibleItemsAsync` | `(int userId) → List<PantryItem>` | Cross-pantry flattened item list. |
| `EnsurePersonalPantryAsync` | `(int userId) → Pantry` | Idempotent: creates personal pantry if absent. |
| `AddOrUpdateAsync` | `(int pantryId, int ingredientId, double amount, string unit, DateTime? expiration)` | Upsert: if compatible unit exists, adds to existing amount. If unit incompatible, replaces. |
| `DeductAsync` | `(int pantryId, int ingredientId, double amount, string unit)` | Deducts; converts units if compatible; clamps to 0 and deletes row if result ≤ 0. |
| `CheckAvailabilityForRecipeAsync` | `(int userId, ICollection<RecipeIngredient> recipeIngredients) → List<IngredientStatus>` | Cross-pantry; returns per-ingredient `Available / PartiallyAvailable / Missing / IncompatibleUnits`. |
| `GetPersonalPantryAsync` | `(int userId) → Pantry?` | Null if no personal pantry yet. |
| `CreateSharedPantryAsync` | `(int ownerUserId, string name) → Pantry` | Creates a new shared pantry. |
| `TryDeleteOwnedPantryAsync` | `(int pantryId, int actingUserId) → bool` | Only owner can delete. |
| `ClearPantryAsync` | `(int pantryId)` | Deletes all items in a pantry — destructive, no authz guard built-in. |
| `AddMemberAsync` / `RemoveMemberAsync` | member management | Shared-pantry membership management. |

**Ingredient resolution (`IngredientResolver`):** `Normalize(name)` lowercases, collapses hyphens/underscores to spaces, collapses whitespace. `RecipeService.ResolveIngredientAsync` does exact normalized-name match then creates a new `Ingredient` row if absent — there is no fuzzy match today.

### RecipeService.CreateAsync

Signature: `(int cookbookId, int userId, ParsedRecipe parsed) → Recipe`

Authorization: requires `cookbook.UserId == userId` — cookbook must be owned by the acting user. Returns the persisted `Recipe` entity with its auto-generated `Id`.

Ownership check: inline, not via middleware. The facade delegates ownership checks here.

### RecipeDocument (v4, the wire shape an agent submits)

Fields an agent submits in a `RecipeDocument`:

```
version: int (must be RecipeUpcasterChain.CurrentVersion — currently 4)
name: string (required, non-empty)
servings: int (required, > 0)
prepTimeMinutes: int? (optional)
cookTimeMinutes: int? (optional)
photoUrl: string? (optional, max 2048)
description: string? (optional, max 4096)
tags: string[] (optional)
equipment: string[] (optional)
provenance: { sourceUrl?, authorName?, sourceName? } (optional)
ingredients: IngredientEntry[] — each: { id: int, name: string, amount: double, unit: string, note?, substitutions[] }
steps: StepNode[] — each either:
  { kind: "content", text: string, timers?: TimerEntry[], temperature?: { value, unit: "F"|"C"|"Gas" }, donenessCue?: string }
  { kind: "section", heading: string }
```

`RecipeValidator` checks: non-empty name, servings > 0, unique ingredient ids, no dangling `[name](#id)` step references, valid temperature values (whole-degree for F/C; 0.5-steps [1.0–9.5] for Gas). Warnings (non-blocking): orphan ingredients, empty sections, invalid provenance URL, empty substitutions.

---

## Category 1: Auth / Token Management

This is the prerequisite for all agent operations. Without it no other feature can be built.

### Table Stakes

| Feature | Why Expected | Complexity | Existing service method | Notes |
|---------|--------------|------------|------------------------|-------|
| Per-agent bearer token issuance | Agents need a credential separate from UI sessions. Token → user mapping is the mechanism. | MEDIUM | None today — new `AgentToken` EF entity required. | Token stored as PBKDF2 hash (mirrors existing user password pattern). Plaintext shown once on creation; never retrievable again. The `UserProfile` owns zero or more tokens. |
| Token → user resolution middleware | Every API/MCP request must resolve the bearer token to a `UserProfile` before any service call. Establishes the acting user the same way `CurrentUserService` does for Blazor circuits. | MEDIUM | `CurrentUserService` (Blazor-only, not HTTP-request-scoped) | New `AgentAuthMiddleware` or `IAuthenticationHandler` that reads `Authorization: Bearer <token>`, hashes it, looks up `AgentToken`, loads the user, injects into `ICurrentUser` / `HttpContext.Items`. |
| Token management UI in Profile | Users need a way to create and revoke tokens without touching the DB. | LOW | None today | A new Profile card: list of tokens (name + created date, not the plaintext), create-token dialog (shows plaintext once), revoke button per token. |
| Scoped token permissions (read-only vs write) | Agents doing pantry reads vs writes benefit from least-privilege. | LOW | None | `AgentToken.Permissions` bitmask or enum: `PantryRead`, `PantryWrite`, `RecipeCreate`. Default: all three. Enforced in the facade layer. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Token name / label | Agents are identifiable in logs and the UI. "Home Assistant agent" vs "shopping app". | LOW | `AgentToken.Label` string. Shown in the token list in Profile. |
| Token last-used timestamp | Lets users spot stale or unused tokens to revoke. | LOW | `AgentToken.LastUsedAt` updated on each auth resolve. No performance concern — one write per request at most. |
| Token expiry | Reduces blast radius of leaked tokens. | LOW | `AgentToken.ExpiresAt` nullable. Middleware rejects expired tokens with 401. |

### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| OAuth / OIDC / JWT | Identity complexity far exceeds the trusted-LAN threat model. Adds redirect flows, discovery endpoints, key rotation. | Simple bearer token hashed and stored in SQLite, matching the existing PBKDF2 user-password pattern. |
| Per-token cookbook/pantry ACL | Fine-grained resource scoping requires a complex permission graph. | Token is scoped to a user; user's existing ownership/membership checks in `PantryService` / `RecipeService` enforce resource access naturally. |
| Shared tokens across users | A single token shared by multiple users breaks audit trails. | Each token belongs to exactly one user. |
| API keys visible in token list | Storing / displaying plaintext API keys is the security failure pattern CookBot already solved for AI keys. | Hash on creation; show plaintext once; never show again. |

---

## Category 2: Pantry Operations

The core agent-facing pantry surface. All operations are scoped to the acting user via the facade.

### Table Stakes

| Feature | Why Expected | Complexity | Existing service method | Expected behavior |
|---------|--------------|------------|------------------------|-------------------|
| List accessible pantries | An agent needs to know which pantry IDs exist before it can act on items. First tool any pantry agent calls. | LOW | `PantryService.GetAccessiblePantriesAsync(userId)` | Returns `[{ id, name, isPersonal, isOwner }]`. Order: personal first, then alpha. For agents, `isOwner` matters for knowing whether write ops are allowed. |
| List items in a pantry | Core read op — see what's stocked. | LOW | `PantryService.GetPantryItemsAsync(pantryId)` | Facade must verify that `pantryId` is in the acting user's accessible set before calling. Returns `[{ ingredientId, ingredientName, amount, unit, expirationDate? }]`. Include `ingredientName` — agents work in names, not database IDs. |
| Resolve ingredient name → id | Agents receive ingredient names (e.g. "all-purpose flour") and need to map them to the database `Ingredient.Id` for `AddOrUpdateAsync` / `DeductAsync`. | MEDIUM | `IngredientResolver.Normalize` + `_ingredientRepo.FindAsync` — no fuzzy match | Returns `{ id, name, normalizedName, category }` for exact normalized-name matches. Also returns a candidate list for near-matches (normalized contains-search) so the agent can pick the right one. Agents MUST pass the returned `id` into add/deduct — do not accept names directly on mutating ops. |
| Add / update pantry item | Core write op — stock the pantry. | LOW | `PantryService.AddOrUpdateAsync(pantryId, ingredientId, amount, unit, expiration?)` | Upsert semantics: if ingredient already exists in compatible unit, **adds** to existing amount. If unit incompatible, **replaces** amount and unit. The facade must surface which happened (`added` vs `replaced`). |
| Deduct pantry item | Core write op — consume from the pantry after cooking. | MEDIUM | `PantryService.DeductAsync(pantryId, ingredientId, amount, unit)` | Three cases: (1) sufficient stock: deducts, returns `{ remainingAmount, remainingUnit }`. (2) Insufficient stock: `DeductAsync` clamps to 0 and deletes the row — the facade must detect this and return `{ insufficientStock: true, availableBeforeDeduct, requestedAmount }` as an execution error (`isError: true` in MCP, 422 in REST). (3) Unit incompatible: `DeductAsync` skips conversion and deducts raw — facade must detect `CanConvert` result and return a unit-mismatch error before calling, or validate post-call. |
| Search ingredients (name lookup) | Agents need to search before add/deduct — ingredient name resolution is a prerequisite. | LOW | `_ingredientRepo.FindAsync` with normalized name | Separate from "list pantry items": searches the ingredient catalogue (600+ seed entries), not the pantry. Returns candidates sorted by match quality. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Set pantry item (absolute, not additive) | Agents doing a full pantry sync (e.g. smart fridge integration) need to set a known quantity, not add to it. The current `AddOrUpdateAsync` always adds when units are compatible. | LOW | Facade calls `AddOrUpdateAsync` with the desired amount after deducting the existing amount to zero first, or — cleaner — a new `SetAsync(pantryId, ingredientId, amount, unit)` method on the facade. | Implement in the facade; do not change `PantryService` behavior. |
| Batch add/deduct | Smart-home or meal-prep agents may need to update many items at once (e.g. after cooking a recipe — deduct all its ingredients). | MEDIUM | Loop over `AddOrUpdateAsync` / `DeductAsync` in a single facade transaction. | Returns per-item results so agents know which succeeded and which had errors. |
| Pantry availability check for a recipe | An agent asks "can I cook this recipe?" given the pantry contents. | LOW | `PantryService.CheckAvailabilityForRecipeAsync(userId, recipeIngredients)` | Returns `IngredientStatus[]` with `Available / PartiallyAvailable / Missing / IncompatibleUnits` per ingredient. Useful for agents planning grocery orders. |

### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| `ClearPantryAsync` exposed to agents | Wipes all items in a pantry — catastrophic if misfired. No safe recovery. | Agents may deduct individual items. If a full clear is needed, it goes through the UI only. |
| `TryDeleteOwnedPantryAsync` exposed to agents | Deletes the pantry entity itself — irreversible. | Pantry creation and deletion are UI-only operations. |
| `AddMemberAsync` / `RemoveMemberAsync` via agent | Membership management is a privileged social operation — sharing pantries with other users. Agents should not be able to alter who has access to data. | Membership is UI-only. |
| `CreateSharedPantryAsync` via agent | Creating pantries consumes storage and changes the user's data model structurally. | Agents operate on existing pantries only. |
| Accept ingredient names directly on add/deduct | Bypasses the explicit resolve step, creating ambiguous matches (is "flour" bread flour or all-purpose?). | Require a prior `resolve-ingredient` call; accept only `ingredientId` on mutating ops. Force the agent to be explicit. |
| Fuzzy-match auto-create unknown ingredients | `RecipeService.ResolveIngredientAsync` auto-creates new `Ingredient` rows on exact-name miss. This is correct behavior for recipe creation (the AI generates precise names) but wrong for pantry ops (the agent may misspell). | For pantry ops: resolve-ingredient returns candidates for near-misses. If no match, return an error with candidates rather than auto-creating. Auto-create only on recipe submission (preserving existing `RecipeService` behavior). |

---

## Category 3: Recipe Creation

The second core agent operation. An agent submits a fully-formed `RecipeDocument`; the system validates it and persists it.

### Table Stakes

| Feature | Why Expected | Complexity | Existing service method | Expected behavior |
|---------|--------------|------------|------------------------|-------------------|
| Submit a canonical `RecipeDocument` and get back a recipe ID | The entire point of the recipe creation surface. Agents (e.g. a Claude instance with memory) assemble a `RecipeDocument` and create a recipe for the user. | MEDIUM | `RecipeService.CreateAsync(cookbookId, userId, ParsedRecipe)` | Flow: (1) `JsonRecipeSerializer.Deserialize` the submitted JSON into `RecipeDocument`. (2) `RecipeUpcasterChain` upcasts if version < current. (3) `RecipeValidator.Validate` — if errors, return 422 + error list; do NOT persist. (4) Build `ParsedRecipe` from the validated doc. (5) Call `RecipeService.CreateAsync(cookbookId, userId, parsed)`. (6) Return `{ recipeId, name, cookbookId }`. |
| Schema validation errors as structured response | The agent must know exactly what is wrong to self-correct without a retry loop. | LOW | `RecipeValidator.Validate` returns `ValidationResult` with `ValidationError[]` | Response: `{ isValid: false, errors: [{ path, code, message }], warnings: [{ path, code, message }] }`. Errors block creation. Warnings are informational (included in a successful creation response too). |
| Target cookbook selection | Agents need to direct a recipe to the right cookbook. The agent specifies `cookbookId` OR a "default" fallback. | LOW | `RecipeService.CreateAsync(cookbookId, …)` — requires a valid cookbook owned by the user | If agent specifies `cookbookId`: validate the user owns it, 404 if not found, 403 if not owned. If agent omits it: use the user's first cookbook (alpha order). |
| List cookbooks | Agents need to know which cookbook IDs they can target before submitting. | LOW | `CookbookService` / `_cookbookRepo.FindAsync(c => c.UserId == userId)` — no dedicated agent facade method yet | Returns `[{ id, name, recipeCount }]`. Sorted alphabetically. Agent selects and passes `cookbookId` into recipe creation. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Echo back the canonical doc in the creation response | Lets the agent confirm exactly what was persisted (after upcasting + validation) without a separate read call. | LOW | Serialize the `RecipeDocument` that was persisted and include it in the creation response as `canonicalDocument`. |
| Canonical doc read by recipe ID | An agent that created a recipe (or was told a recipe ID by the user) can fetch the full canonical doc for display or further modification. | LOW | Read `Recipe.CanonicalDocumentJson` via the recipe repo; deserialize and return. |
| Upcasting transparency | If the agent submitted a v3 doc and the current version is v4, note in the response that the doc was upcasted. | LOW | Include `{ submittedVersion: 3, persistedVersion: 4, upcasted: true }` in the creation response. |
| Nutrition trigger after creation | After a recipe is persisted, optionally schedule (or immediately perform) a nutrition cache computation so the recipe's nutrition panel is populated without user action. | MEDIUM | `NutritionService.ComputeAsync(recipeId)` — currently a user-triggered CTA in the UI. | Include `computeNutritionAfterCreate: bool` as an optional request parameter. Blocks the response slightly but removes the manual step. Default: false (preserve the existing non-blocking save invariant). |

### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Recipe creation via agent = AI generation (agent calls Anthropic) | Could seem like the most "agentic" path | Creates a new inbound threat surface (prompt injection, API key management, latency, cost). The LOCKED decision is structured-submit only: the agent submits a finished `RecipeDocument`, not a freeform prompt. |
| Recipe update via agent | Agents editing existing recipes creates conflict risk if the user is editing the same recipe simultaneously. No concurrency model exists. | Recipe creation only. Updates are UI-only in v1.5. |
| Recipe deletion via agent | Irreversible and catastrophic if misfired (photos deleted from disk, all linked data removed). | Deletion is UI-only. |
| Accepting freeform text / YAML wire format from agents | The `IRecipeFormatParser` can parse freeform text, but the parsing is ambiguous and best-effort. Agents are expected to emit structured JSON, not freeform prose. | Accept only valid `RecipeDocument` JSON. If an agent emits YAML, it must first convert it locally. |
| Auto-creating a cookbook if none exists | If the user has no cookbooks, auto-creating one obscures the error and creates unsolicited data. | Return 422 with `{ code: "NO_COOKBOOK", message: "User has no cookbooks. Create one via the web UI first." }`. |
| Recipe photo upload via agent | Photo handling involves disk I/O, magic-byte validation, `wwwroot/uploads/` path management. Opening this surface to agents adds new file-system exposure. | `RecipeDocument.photoUrl` (external URL) is accepted on the canonical doc — the agent can supply a URL. Local file upload is UI-only. |

---

## Category 4: MCP Tool Surface

How the agent-operations facade is exposed as MCP tools. Each tool corresponds directly to a facade method.

### Table Stakes — Tool Naming and Shape

MCP tool names must be 1–128 chars, case-sensitive, only `A-Z a-z 0-9 _ - .` characters, no spaces. Preferred convention for this codebase: `snake_case` (consistent with the broader MCP ecosystem and Python/TS clients that agents commonly run).

| Tool name | Maps to facade | Input schema summary | Output shape | Error semantics |
|-----------|---------------|---------------------|-------------|-----------------|
| `list_pantries` | `GetAccessiblePantriesAsync(userId)` | `{}` (no params) | `{ pantries: [{ id, name, isPersonal, isOwner }] }` | Protocol error only (auth failure → 401 before tool reaches server). |
| `list_pantry_items` | `GetPantryItemsAsync(pantryId)` after access check | `{ pantryId: integer }` | `{ items: [{ ingredientId, ingredientName, amount, unit, expirationDate? }] }` | `isError: true` if `pantryId` not in user's accessible pantries (access denied). |
| `resolve_ingredient` | Normalized name lookup + contains-search | `{ name: string }` | `{ exactMatch?: { id, name, category }, candidates: [{ id, name, normalizedName, category }] }` | Never errors — returns empty candidates if nothing found. |
| `add_pantry_item` | `AddOrUpdateAsync(pantryId, ingredientId, amount, unit, expiration?)` | `{ pantryId: integer, ingredientId: integer, amount: number, unit: string, expirationDate?: string (ISO 8601) }` | `{ action: "added" \| "replaced", newAmount, unit }` | `isError: true` if pantry not accessible, ingredient not found, or amount ≤ 0. |
| `deduct_pantry_item` | `DeductAsync` after unit/availability pre-check | `{ pantryId: integer, ingredientId: integer, amount: number, unit: string }` | `{ deducted: true, remainingAmount, remainingUnit }` | `isError: true` with code `INSUFFICIENT_STOCK` or `UNIT_MISMATCH`. |
| `list_cookbooks` | Cookbook repo lookup by user | `{}` (no params) | `{ cookbooks: [{ id, name, recipeCount }] }` | Protocol error only. |
| `create_recipe` | Full submission pipeline | `{ document: RecipeDocument, cookbookId?: integer }` | `{ recipeId, name, cookbookId, warnings: [], canonicalDocument: {...} }` | `isError: true` with `errors: [{ path, code, message }]` for validation failures. |

**Tool descriptions must include (per MCP spec + empirical research):**
- Purpose: what the tool does
- When to use it: call order / prerequisites ("Call `resolve_ingredient` before `add_pantry_item`")
- Limitations: what it cannot do ("Cannot deduct more than is available")
- Parameter explanations: per-field descriptions in `inputSchema.properties[x].description`

**MCP C# SDK setup** (ModelContextProtocol.AspNetCore v1.4.0, Apache-2.0, .NET 10 compatible):

```csharp
// Program.cs (alongside existing Blazor + SignalR registration)
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();   // discovers [McpServerToolType] classes
app.MapMcp("/api/mcp").RequireAuthorization();
```

Tools are classes decorated `[McpServerToolType]` with `[McpServerTool, Description("...")]` methods. DI services (the agent-operations facade, ICurrentUser) are injected as method parameters.

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| `outputSchema` on tools | Lets the client/LLM know the exact shape of the response for type-safe consumption. The MCP spec supports an optional `outputSchema` alongside `inputSchema`. | LOW | Define an `outputSchema` for each tool. The C# SDK may not support this directly via attributes — may need manual `ToolDefinition` construction for `list_pantry_items` / `create_recipe`. |
| `structuredContent` in tool results | MCP spec supports returning both a text summary (human-readable) AND a `structuredContent` JSON value (machine-readable). Both should be returned — text for LLM context, structured for programmatic consumers. | LOW | Each tool method returns a text summary ("Added 2 cups flour to Personal Pantry") plus the JSON result in `structuredContent`. |
| MCP tool list caching hints (`ttlMs`) | `list_pantries` and `list_cookbooks` rarely change. Setting a TTL hint reduces repeated `tools/call` invocations. | LOW | MCP spec supports `ttlMs` in `tools/list` response. The C# SDK may need manual extension to set per-tool TTL hints. |

### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| `clear_pantry` as MCP tool | One misfired call wipes all items — no undo. | Excluded from the agent surface entirely. |
| `delete_pantry` as MCP tool | Structural change to user's data, irreversible. | UI-only. |
| `delete_recipe` as MCP tool | Irreversible, removes photos from disk. | UI-only. |
| `update_recipe` as MCP tool | Concurrency risk; no conflict model. | Creation only in v1.5. |
| Tools with overlapping purpose | If `add_pantry_item` and `set_pantry_item` both exist, agents will pick the wrong one. The empirical study found two similar tools cause model confusion. | Start with one semantic per operation. `add_pantry_item` is additive; implement `set_pantry_item` only if confirmed needed by actual agent workflows. |
| AI generation on the inbound MCP path | An MCP tool that calls Anthropic internally to generate a recipe would create circular AI-calls-AI invocations and unpredictable costs. | `create_recipe` accepts only a finished `RecipeDocument`, never a freeform prompt. |

---

## Category 5: REST API Surface

The same facade methods exposed as HTTP minimal-API endpoints. Same authorization, same business logic. REST is the second consumer of the shared facade — the MCP server is the first.

### Table Stakes — Resource Modeling and Endpoint Shape

| Endpoint | Method + Path | Request | Success response | Error responses |
|----------|--------------|---------|-----------------|-----------------|
| List pantries | `GET /api/agent/pantries` | — | 200 `{ pantries: [...] }` | 401 |
| List pantry items | `GET /api/agent/pantries/{pantryId}/items` | — | 200 `{ items: [...] }` | 401, 403 (not accessible), 404 |
| Resolve ingredient | `GET /api/agent/ingredients?name={name}` | query param | 200 `{ exactMatch?, candidates: [...] }` | 401 |
| Add/update item | `PUT /api/agent/pantries/{pantryId}/items/{ingredientId}` | `{ amount, unit, expirationDate? }` | 200 `{ action, newAmount, unit }` | 401, 403, 404, 422 (invalid amount/unit) |
| Deduct item | `POST /api/agent/pantries/{pantryId}/items/{ingredientId}/deduct` | `{ amount, unit }` | 200 `{ deducted: true, remainingAmount, remainingUnit }` | 401, 403, 404, 422 `INSUFFICIENT_STOCK` / `UNIT_MISMATCH` |
| List cookbooks | `GET /api/agent/cookbooks` | — | 200 `{ cookbooks: [...] }` | 401 |
| Create recipe | `POST /api/agent/recipes` | `{ document: RecipeDocument, cookbookId?: int }` | 201 + `Location: /api/agent/recipes/{id}` header + `{ recipeId, name, cookbookId, warnings: [], canonicalDocument }` | 401, 422 `{ errors: [...], warnings: [...] }`, 404 (cookbook not found), 403 (cookbook not owned) |
| Get recipe | `GET /api/agent/recipes/{recipeId}` | — | 200 `{ recipeId, name, cookbookId, canonicalDocument }` | 401, 403, 404 |

**Validation error shape (RFC 9457 / ASP.NET Core `ValidationProblemDetails`):**

```json
{
  "type": "https://cookbot/errors/validation",
  "title": "Recipe validation failed",
  "status": 422,
  "instance": "/api/agent/recipes",
  "errors": {
    "/name": ["REQUIRED: Recipe name is required."],
    "/steps/0/temperature/value": ["INVALID_TEMPERATURE: C temperature must be whole-degree."]
  },
  "warnings": [
    { "path": "/ingredients/0", "code": "OrphanIngredient", "message": "Ingredient 'salt' (id=1) is not referenced by any step." }
  ]
}
```

**Business logic error shape** (unit mismatch, insufficient stock, access denied with detail):

```json
{
  "type": "https://cookbot/errors/insufficient-stock",
  "title": "Insufficient pantry stock",
  "status": 422,
  "code": "INSUFFICIENT_STOCK",
  "availableAmount": 1.5,
  "availableUnit": "cup",
  "requestedAmount": 2.0,
  "requestedUnit": "cup"
}
```

**Pagination:** List endpoints for v1.5 do not need pagination — pantries and cookbooks per user are O(tens). Pantry items per pantry are O(hundreds) at most. Implement a simple `limit` + `offset` query parameter on `GET /api/agent/pantries/{pantryId}/items` for forward-compat, defaulting to returning all items (no cursor needed at this scale).

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| `GET /api/agent/recipes/{id}` | Agents can fetch a recipe they created (or that the user has) for display, editing elsewhere, or passing to another tool. | LOW | Reads `Recipe.CanonicalDocumentJson` + `RecipeUpcasterChain` to ensure current version. Authz: only cookbooks owned by or shared with the user. |
| `GET /api/agent/pantries/{id}/availability?recipeId={id}` | Check whether a specific recipe can be cooked from current pantry stock. Useful for meal-planning agents. | MEDIUM | Calls `PantryService.CheckAvailabilityForRecipeAsync`. Returns per-ingredient availability status. |
| `/healthz` extension for agent readiness | Agents benefit from a readiness check that validates the API token is valid and the service is up. | LOW | Extend the existing `/healthz` endpoint (shipped v1.3 PROD-11) with an authenticated variant: `GET /api/agent/health` returns 200 + `{ user: name, tokenLabel }` when auth succeeds. |
| `Content-Type: application/problem+json` on all error responses | Standard error content type for programmatic clients. ASP.NET Core `ProblemDetails` already produces this via `app.UseStatusCodePages()`. | LOW | Enable `app.UseStatusCodePages()` and ensure error middleware emits `application/problem+json`. |

### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| `DELETE /api/agent/pantries/{id}` | Pantry deletion via API exposes a destructive structural operation to an automated caller. | UI-only. |
| `DELETE /api/agent/recipes/{id}` | Irreversible, removes disk files. | UI-only. |
| `PATCH /api/agent/recipes/{id}` (partial update) | Partial update against the canonical document is complex (merge semantics, partial validation). | Not in v1.5. |
| Bulk recipe create endpoint | Accepting an array of `RecipeDocument` in one call creates partial-success complexity (which failed? which succeeded?). | Accept one recipe per POST; agents loop externally. |
| Streaming recipe creation | Server-sent events for recipe creation progress adds protocol complexity; creation is fast (< 50 ms). | Standard synchronous response. |
| `/api/agent/pantries/{id}/clear` | Clear-all via API is too destructive. | Not exposed. |

---

## Shared Behaviors: Deduct Edge Cases

The deduct operation has the most complex error surface. Both MCP and REST must handle these identically.

### Unit Mismatch (pre-call validation in facade)

The facade must call `IUnitConverter.CanConvert(requestedUnit, storedUnit)` **before** calling `PantryService.DeductAsync`. If incompatible:
- Return error: `code: "UNIT_MISMATCH"`, include `storedUnit` and `requestedUnit` in the response so the agent can retry with the correct unit or call `resolve_ingredient` to check what unit is stored.
- Do NOT call `DeductAsync` — the current implementation deducts raw amounts on mismatch, silently wrong.

### Insufficient Stock (detection in facade)

`PantryService.DeductAsync` clamps to 0 and deletes the row if stock runs out. The facade cannot detect "did this wipe the row?" after the call without a pre-call read. Pattern:
1. Fetch `PantryItem` before deduct.
2. Check `item.Amount` after unit conversion ≥ requested amount. If not, return `INSUFFICIENT_STOCK` error **without calling `DeductAsync`**.
3. If sufficient, call `DeductAsync` and return success.

This pattern requires the facade to hold the pre-read item, which is one extra repo call — acceptable.

### No Pantry Item Exists

`PantryService.DeductAsync` silently returns (line 129: `if (item == null) return;`) when the ingredient is not in the pantry. The facade must pre-check and return `code: "ITEM_NOT_FOUND"` rather than silently succeeding.

---

## Shared Behaviors: Ingredient Name Resolution

Resolution follows a two-step pattern that agents must adopt:

1. **Search** (`resolve_ingredient` / `GET /api/agent/ingredients?name=...`): submit the name string → returns an `exactMatch` and/or `candidates`. Normalized search is: lowercase, collapse hyphens/underscores to spaces, collapse whitespace. The candidate list does a normalized-contains query on the ingredient catalogue.

2. **Mutate** (`add_pantry_item` / `deduct_pantry_item`): submit the `ingredientId` from step 1. Never accept bare name strings on mutating operations.

If no match is found, the facade returns candidates from the contains-search. If candidates list is empty, the agent should inform the user that the ingredient is not in the catalogue. **Do not auto-create ingredients from pantry operations** — only `RecipeService.CreateAsync` auto-creates ingredients (its behavior is intentional: AI-generated recipe names are precise and expected to be new).

---

## Feature Dependencies

```
Token auth (Category 1)
    └──required by──> All other categories (no auth = no agent access)

Shared agent-operations facade (Application layer)
    └──required by──> MCP server (Category 4)
    └──required by──> REST API (Category 5)
    └──wraps──> PantryService (Category 2 behaviors)
    └──wraps──> RecipeService.CreateAsync (Category 3 behaviors)

Ingredient resolve (Category 2)
    └──required before──> add_pantry_item / deduct_pantry_item (must have ingredientId)

List pantries (Category 2)
    └──required before──> list_pantry_items / add / deduct (must have pantryId)

List cookbooks (Category 3)
    └──required before──> create_recipe with explicit cookbookId

RecipeDocument schema (existing v4)
    └──consumed by──> create_recipe (wire shape the agent submits)
    └──validated by──> RecipeValidator (existing, unchanged)
    └──upcasted by──> RecipeUpcasterChain (existing, unchanged)
```

---

## MVP Definition for v1.5

### Phase must-have (all categories must have table stakes to ship)

- Token auth: `AgentToken` entity, PBKDF2 hash, middleware resolution, Profile UI (create + revoke)
- Pantry ops: list pantries, list items, resolve ingredient, add/update item, deduct item (with all three error cases)
- Recipe creation: submit `RecipeDocument` → validate → persist → return `recipeId` + `canonicalDocument`; list cookbooks
- MCP surface: all 7 tools with correct descriptions (purpose + when + limitations + per-param descriptions)
- REST surface: all 8 endpoints with `application/problem+json` error shape; 201 + Location header on create

### Phase should-have (high value, low complexity — include in the same phases)

- Token name/label, last-used timestamp
- `structuredContent` alongside text content in MCP tool results
- Echo canonical doc in recipe creation response
- Upcasting transparency in creation response
- `GET /api/agent/recipes/{id}` (read a recipe by ID)

### Deferred to v1.5.x or v1.6

- Token expiry dates
- Batch add/deduct
- Pantry availability check for a recipe
- `outputSchema` on MCP tools (SDK support unclear)
- Nutrition trigger on recipe creation (`computeNutritionAfterCreate`)
- `GET /api/agent/pantries/{id}/availability?recipeId={id}`
- Token scoped permissions (read-only vs write)

---

## Sources

- `src/CookBot.Application/Services/PantryService.cs` — actual method signatures + unit conversion + deduct behavior (HIGH confidence — primary source)
- `src/CookBot.Application/Services/RecipeService.cs` — `CreateAsync` signature + ownership checks + ingredient resolution (HIGH confidence — primary source)
- `src/CookBot.Domain/Recipes/RecipeDocument.cs` + related records — exact canonical wire shape (HIGH confidence — primary source)
- `src/CookBot.Application/Services/IngredientResolver.cs` — normalization logic (HIGH confidence — primary source)
- `src/CookBot.Application/Recipes/RecipeValidator.cs` — validation error codes + paths (HIGH confidence — primary source)
- [MCP Tool Specification (draft)](https://modelcontextprotocol.io/specification/draft/server/tools) — tool naming, descriptions, error semantics (`isError`), `structuredContent`, `outputSchema` (MEDIUM confidence — official spec page, fetched 2026-06-26)
- [ModelContextProtocol C# SDK GitHub](https://github.com/modelcontextprotocol/csharp-sdk) — v1.4.0, Apache-2.0, `AddMcpServer().WithHttpTransport().WithToolsFromAssembly()`, `MapMcp`, `[McpServerToolType]` / `[McpServerTool]` attributes (MEDIUM confidence — GitHub README + NuGet page cross-checked)
- [MCP Tool Description Quality Study (arxiv 2602.14878)](https://arxiv.org/html/2602.14878v1) — 856 tools, six description components, 5.85pp task-success improvement, six description smells (MEDIUM confidence — academic preprint)
- [MCP Best Practices](https://mcp-best-practice.github.io/mcp-best-practice/best-practice/) — idempotency keys, single-responsibility tools, error taxonomy (MEDIUM confidence — community guide)
- [NearForm MCP Implementation Guide](https://nearform.com/digital-community/implementing-model-context-protocol-mcp-tips-tricks-and-pitfalls/) — similar-tool confusion, human-in-loop for destructive ops (MEDIUM confidence — practitioner blog)
- [RFC 9457 Problem Details for HTTP APIs](https://datatracker.ietf.org/doc/html/rfc9457) — `application/problem+json` shape, `errors` extension, `instance` field (HIGH confidence — IETF standard)
- [ASP.NET Core ProblemDetails / ValidationProblemDetails](https://codewithmukesh.com/blog/problem-details-in-aspnet-core/) — built-in support in .NET 8+ (MEDIUM confidence — community documentation)

---

*Feature research for: v1.5 External Agent Interface (MCP + REST API) — FreelovesCookBot*
*Researched: 2026-06-26*
