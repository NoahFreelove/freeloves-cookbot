# External Integrations

**Analysis Date:** 2026-04-25

## APIs & External Services

**LLM provider (Anthropic, only):**
- Anthropic Messages API — Used for in-app AI recipe generation, recipe parsing assistance, pantry standardization, and chat
  - Base endpoints (hardcoded in `src/CookBot.Infrastructure/AI/AnthropicAiService.cs`):
    - `GET https://api.anthropic.com/v1/models` (`ListModelsAsync`)
    - `POST https://api.anthropic.com/v1/messages` (`SendMessageAsync`, `StreamMessageAsync`, `TestConnectionAsync`)
  - SDK/Client: None — direct `HttpClient` with manual JSON. No official Anthropic .NET SDK is referenced
  - Auth: HTTP header `x-api-key: <key>` plus `anthropic-version: 2023-06-01`
  - Streaming: Server-Sent Events parsed line-by-line on `data: ` prefix; reads `content_block_delta` events to yield text chunks
  - API key sources (resolved by `src/CookBot.Web/Services/AiApiKeyResolutionService.cs`, in priority order):
    1. The current user's `UserProfile.AiApiKey` (`src/CookBot.Domain/Entities/UserProfile.cs`)
    2. A shared key from another user via `AiApiKeyShare` records (`src/CookBot.Domain/Entities/AiApiKeyShare.cs`); preferred owner picked via `UserProfile.AiSharedKeyOwnerUserId`
    3. Global fallback `CookBot:AnthropicApiKey` from `appsettings.json` (read into `CookBotSettings.AnthropicApiKey`)
  - Curated model list (in `AnthropicAiService.CuratedModels`):
    - `claude-haiku-4-5-20251001` ("Claude Haiku 4.5 (Fast)")
    - `claude-sonnet-4-6` ("Claude Sonnet 4.6 (Balanced)") — `DefaultModelId`
    - `claude-opus-4-7` ("Claude Opus 4.7 (Most Capable)")
  - Extended-thinking blocks (`thinking`, `redacted_thinking`) are filtered out in `AnthropicAiService.ExtractText`

**No other LLM providers integrated:**
- No OpenAI, Google Gemini, Mistral, Azure OpenAI, or local-model client libraries are referenced anywhere in `src/`. The abstraction `IAiService` (`src/CookBot.Domain/Interfaces/IAiService.cs`) has only one implementation: `AnthropicAiService`. README confirms Anthropic-only direct integration plus a "prompt generator" (see `src/CookBot.Web/Components/Pages/PromptBuilder.razor`) for users who prefer to paste prompts into any external LLM UI.

**Other web/CDN dependencies:**
- Google Fonts — `Inter` font loaded via `<link href="https://fonts.googleapis.com/css2?family=Inter...">` in `src/CookBot.Web/Components/App.razor`
- MudBlazor static assets served from in-process `_content/MudBlazor/MudBlazor.min.css` and `MudBlazor.min.js` (no external CDN)

## Data Storage

**Databases:**
- SQLite via Entity Framework Core 10
  - Provider: `Microsoft.EntityFrameworkCore.Sqlite`
  - Configured in `src/CookBot.Infrastructure/DependencyInjection.cs` using `options.UseSqlite(...)`
  - Connection string key: `ConnectionStrings:DefaultConnection` in `appsettings.json` — default `Data Source=cookbot.db`
  - DbContext: `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` exposes `DbSet`s for `Users`, `UserProfiles`, `Cookbooks`, `Recipes`, `Ingredients`, `RecipeIngredients`, `PantryItems`, `Pantries`, `PantryMembers`, `GroceryLists`, `GroceryListItems`, `AiConversations`, `CookbookShares`, `AiApiKeyShares`
  - Entity configurations applied via `ApplyConfigurationsFromAssembly` in `OnModelCreating`; per-entity classes live in `src/CookBot.Infrastructure/Data/Configurations/`
  - Migrations: `src/CookBot.Infrastructure/Migrations/` — current latest is `20260416175214_AiApiKeyShares` (matches the recent "API key sharing" commit)
  - Database is auto-migrated and seeded at startup by `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` invoked from `Program.cs`

**Seed Data:**
- `seeds/ingredients.json` (~624 lines, ~52 KB) — 600+ ingredients with `Name`, `Category`, `PreferredUnits`; loaded relative to `ContentRootPath` via `Path.Combine(contentRootPath, "..", "seeds", "ingredients.json")` in `DatabaseSeeder.LoadIngredientsFromSeedFile`

**File Storage:**
- Local filesystem only (SQLite database file in working directory, plus the bundled `seeds/` directory)
- No S3/Azure Blob/GCS clients referenced

**Caching:**
- None (no Redis, MemoryCache, or distributed cache configured)

## Authentication & Identity

**Auth Provider:**
- Custom, lightweight, "designed for self-hosting on a trusted network" (per README)
- The `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.* package is referenced in `src/CookBot.Infrastructure/CookBot.Infrastructure.csproj` but ASP.NET Core Identity is NOT wired up: `Program.cs` calls neither `AddIdentity*` nor `UseAuthentication`/`UseAuthorization`
- Session/identity is tracked via a scoped `CurrentUserService` (`src/CookBot.Web/Services/CurrentUserService.cs`) that holds `CurrentUserId` for the request scope; selection is driven by UI flows (e.g. `AddUserDialog.razor`, `PasswordPromptDialog.razor`)
- Passwords are optional per user (`User.PasswordHash` is nullable in `src/CookBot.Domain/Entities/User.cs`) and hashed with PBKDF2-HMAC-SHA256 (100,000 iterations, 16-byte salt, 32-byte key) using `Microsoft.AspNetCore.Cryptography.KeyDerivation` in `CurrentUserService.HashPassword`/`VerifyHash`
- Authorization at the application level: `User.IsCookBotAdmin` flag plus per-resource ownership checks (e.g. `CookbookTransferService.CanAccessAsync`, `RecipeAccessExtensions` in `src/CookBot.Infrastructure/Data/RecipeAccessExtensions.cs`)
- Reserved-but-unused `AuthMode` enum (`src/CookBot.Domain/Enums/AuthMode.cs`) with `Disabled`/`Required` and `CookBotSettings.AuthMode` config flag — comment notes "not enforced by the app yet. Do not rely on this for security."

## Monitoring & Observability

**Error Tracking:**
- None — no Sentry, App Insights, OpenTelemetry, or third-party error reporter referenced

**Logs:**
- Default ASP.NET Core logging, configured via `Logging:LogLevel` in `appsettings.json` (`Default: Information`, `Microsoft.AspNetCore: Warning`)

## CI/CD & Deployment

**Hosting:**
- Self-hosted (README explicitly states "This app can be self hosted completely")
- No Dockerfile, docker-compose, Kubernetes manifests, Helm charts, or platform-specific deployment configs in the repo

**CI Pipeline:**
- None — no `.github/workflows/`, `.gitlab-ci.yml`, `azure-pipelines.yml`, or other CI configuration found

## Environment Configuration

**Required env vars:**
- None strictly required to start the app (sensible defaults in `appsettings.json` allow unauthenticated, AI-disabled operation)
- `ASPNETCORE_ENVIRONMENT` set to `Development` in `src/CookBot.Web/Properties/launchSettings.json`

**Optional/runtime config (set via `appsettings.json` or environment overrides):**
- `ConnectionStrings__DefaultConnection` — Override SQLite path
- `CookBot__AnthropicApiKey` — Global Anthropic key fallback (most users set theirs in their profile instead)
- `CookBot__AiFeaturesEnabled` — Host-wide AI on/off
- `CookBot__AppName`, `CookBot__AuthMode` — App branding and reserved auth toggle

**Secrets location:**
- No `.env`, `secrets.json`, or User Secrets configuration is wired up
- Per-user Anthropic API keys are stored in plaintext in the SQLite database column `UserProfile.AiApiKey` (`src/CookBot.Domain/Entities/UserProfile.cs`) — the README and `AiApiKeyResolutionService` comments emphasize the key never leaves the server in API responses

## Webhooks & Callbacks

**Incoming:**
- None — no webhook endpoints, controllers, or minimal-API routes registered (only Razor Components are mapped via `MapRazorComponents<App>()`)

**Outgoing:**
- Anthropic API only (see above)

## File Formats Handled

**Recipe input parsing (`src/CookBot.Application/Services/RecipeFormatParser.cs`):**
- YAML frontmatter delimited by `---` (parsed with `YamlDotNet` using `CamelCaseNamingConvention`)
- Numbered/free-text recipe bodies via regex
- Markdown rendering of AI replies and step text via `Markdig.Markdown.ToHtml` (`src/CookBot.Web/Components/Pages/AiChat.razor`, `CookingMode.razor`)

**Cookbook export/import:**
- JSON document, schema version `1` (`src/CookBot.Application/DTOs/CookbookTransferDtos.cs` — `CookbookTransferDocument`)
  - Serialized/deserialized in `src/CookBot.Web/Services/CookbookTransferService.cs` with camelCase, indented, case-insensitive `JsonSerializerOptions`
  - Client-side download triggered through `wwwroot/js/download.js` (base64 → Blob)
- PDF export (`src/CookBot.Web/Services/CookbookPdfService.cs`) — generated with QuestPDF Fluent API, A4 page size

**Ingredient seed:**
- `seeds/ingredients.json` — list of `{ Name, Category, PreferredUnits[] }` records consumed at first-run database seeding

---

*Integration audit: 2026-04-25*
