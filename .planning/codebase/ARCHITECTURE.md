# Architecture

**Analysis Date:** 2026-04-25

## Pattern Overview

**Overall:** ASP.NET Core Blazor Server (interactive) web application following a Clean / Onion Architecture variant with four layered .NET projects. Single-process, self-hostable, multi-user, SQLite-backed. No Web API controllers — all UI/state runs over Blazor's SignalR circuit.

**Key Characteristics:**
- Four-project layered solution: `CookBot.Domain` → `CookBot.Application` → `CookBot.Infrastructure` → `CookBot.Web`. Dependencies only point inward (the Web project references Infrastructure; Infrastructure references Application; Application references Domain).
- Blazor Server render mode (`InteractiveServer`) — every page declares `@rendermode InteractiveServer`. No client-side WebAssembly, no JS framework.
- MudBlazor (v8.15) is the exclusive component library; theming is configured in `src/CookBot.Web/Components/Layout/MainLayout.razor`.
- EF Core 10 with SQLite as the only persistence target. The connection string defaults to `Data Source=cookbot.db` (lives next to `Program.cs`).
- Repository pattern via a generic `IRepository<T>` (`src/CookBot.Domain/Interfaces/IRepository.cs`) and one implementation `Repository<T>` (`src/CookBot.Infrastructure/Data/Repositories/Repository.cs`). Several services bypass it and use `CookBotDbContext` directly when richer queries are needed.
- Multi-user with no real authentication — the "current user" is a session-scoped `int?` held in `CookBot.Web/Services/CurrentUserService.cs` and persisted to the browser's `sessionStorage` under key `cookbot_current_user`. `CookBotSettings.AuthMode` exists but the comment in `src/CookBot.Application/DTOs/CookBotSettings.cs` notes it is "Reserved for future use; not enforced".
- Authorization is enforced inside the application/data layer (e.g. `RecipeService.CreateAsync`, `CookbookService.GetByIdAsync`, `db.UserCanAccessRecipeAsync`), not by middleware.
- Two JS modules under `src/CookBot.Web/wwwroot/js/`: `cooking-timers.js` (timers + browser notifications, talks back via `DotNetObjectReference`) and `download.js` (base64 → Blob → `<a download>`).

## Layers

**CookBot.Domain (`src/CookBot.Domain/`):**
- Purpose: Pure C# entities, value-like models, enums, and abstract interfaces. No framework references.
- Contains: `Entities/` (15 EF aggregates including `Recipe.cs`, `RecipeStep.cs`, `StepTimer.cs`, `Cookbook.cs`, `User.cs`, `UserProfile.cs`, `AiConversation.cs`, `AiApiKeyShare.cs`, `CookbookShare.cs`, `Pantry.cs`, `PantryItem.cs`, `PantryMember.cs`, `Ingredient.cs`, `RecipeIngredient.cs`, `GroceryList.cs`, `GroceryListItem.cs`), `Enums/` (`AuthMode`, `ExperienceLevel`, `IngredientCategory`, `MeasurementUnit`, `UnitSystem`), `Interfaces/` (`IAiService`, `IRecipeFormatParser`, `IRepository<T>`, `IUnitConverter`, `IPricingProvider`), `Models/NutritionalInfo.cs`.
- Depends on: nothing (no `<PackageReference>` in `src/CookBot.Domain/CookBot.Domain.csproj`).
- Used by: every other project.

**CookBot.Application (`src/CookBot.Application/`):**
- Purpose: Pure business logic, parsing, formatting, prompt assembly. Contains all "what the system does" code, with no DB or HTTP awareness.
- Contains:
  - `Services/RecipeFormatParser.cs` — implements `IRecipeFormatParser`, the canonical YAML-frontmatter recipe parser/serializer. Singleton.
  - `Services/RecipeService.cs`, `Services/CookbookService.cs`, `Services/PantryService.cs`, `Services/GroceryListService.cs` — CRUD + authorization checks via `IRepository<T>`.
  - `Services/PromptBuilderService.cs` — composes the AI system prompt from a token template (see "AI Chat" below).
  - `Services/RecipeCookingAiContext.cs` — builds the per-step user message for the in-flow cooking assistant.
  - `Services/PantryAiPopulationService.cs` — strict-JSON pantry import via the AI service.
  - `Services/RecipeStepTextFormatter.cs` — converts `[name](#id)` ingredient links to highlighted HTML or plain text.
  - `Services/IngredientRefDetectionService.cs`, `Services/TimerDetectionService.cs` — parse step text for ingredient refs and durations.
  - `Services/RecipeScalingService.cs`, `Services/FractionFormatter.cs` — servings math + fraction display.
  - `Services/UnitParser.cs`, `Services/UnitConversionService.cs` — `MeasurementUnit` parsing and volume/weight conversion.
  - `Services/IngredientResolver.cs` — name normalization (`lower-case`, collapse whitespace/dashes).
  - `DTOs/CookBotSettings.cs` — host-level config bound to the `"CookBot"` JSON section.
  - `DTOs/CookbookTransferDtos.cs` — the **portable cookbook export format** (see "Cookbook export/import").
  - `DependencyInjection.cs` — `AddApplication()` extension.
- Depends on: `CookBot.Domain`, `Markdig` (markdown rendering for AI replies), `YamlDotNet` (recipe parser), `Microsoft.Extensions.DependencyInjection.Abstractions`.
- Used by: Infrastructure, Web.

**CookBot.Infrastructure (`src/CookBot.Infrastructure/`):**
- Purpose: All external-world adapters — EF Core DbContext + configurations, the SQLite migrations, the Anthropic HTTP client, repository implementation, and the `AddInfrastructure()` composition root.
- Contains:
  - `Data/CookBotDbContext.cs` — single `DbContext` with one `DbSet` per entity; auto-applies fluent configs from `Data/Configurations/*.cs`.
  - `Data/DatabaseSeeder.cs` — runs `MigrateAsync()` at startup, then on a fresh DB creates a `Home Chef` admin user, a personal pantry, a default `My Recipes` cookbook, and bulk-inserts ingredients from `seeds/ingredients.json`. Also back-fills missing personal pantries and ensures one admin exists.
  - `Data/RecipeAccessExtensions.cs` — `db.UserCanAccessRecipeAsync(recipeId, userId)` — the canonical authorization check used by Razor pages.
  - `Data/Repositories/Repository.cs` — generic EF repo.
  - `Migrations/` — five migrations from `20260314212609_V2InitialCreate` through `20260416175214_AiApiKeyShares` plus the `CookBotDbContextModelSnapshot.cs`.
  - `AI/AnthropicAiService.cs` — `IAiService` implementation that talks directly to `https://api.anthropic.com/v1/messages` using `HttpClient` with `x-api-key` and `anthropic-version: 2023-06-01` headers. Also exposes `CuratedModels` (Haiku 4.5, Sonnet 4.6, Opus 4.7) and `DefaultModelId = "claude-sonnet-4-6"`. Skips `thinking`/`redacted_thinking` content blocks when extracting text.
  - `DependencyInjection.cs` — `AddInfrastructure(IConfiguration)` registers the DbContext, `IRepository<>`, `IAiService → AnthropicAiService`, and `PromptBuilderService`, then chains `AddApplication()`.
- Depends on: `CookBot.Application`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.AspNetCore.App` framework reference.
- Used by: Web.

**CookBot.Web (`src/CookBot.Web/`):**
- Purpose: Blazor Server host, Razor components, web-only services (download/PDF/transfer/key resolution), `Program.cs` composition.
- Contains:
  - `Program.cs` — entry point. Wires up `AddRazorComponents().AddInteractiveServerComponents()`, `AddMudServices()`, `AddInfrastructure(builder.Configuration)`, scoped `CurrentUserService`, `AiApiKeyResolutionService`, `AiApiKeyShareService`, `CookbookTransferService`, `CookbookPdfService`, and `Configure<CookBotSettings>(...)` bound to the `"CookBot"` JSON section. Registers QuestPDF community license. Calls `DatabaseSeeder.SeedAsync` inside a created scope before `app.Run()`.
  - `Components/App.razor` — root document; loads MudBlazor CSS/JS, Inter font, `app.css`, `js/cooking-timers.js`, `js/download.js`.
  - `Components/Routes.razor` — `<Router>` with `MainLayout` as default.
  - `Components/Layout/MainLayout.razor` — MudBlazor layout, theme, top-bar user picker, dark-mode toggle stored in `localStorage` under `cookbot_dark_mode`, admin "Manage users" entry point.
  - `Components/Layout/NavMenu.razor` — left rail with Home, Cookbooks, Pantry, Grocery Lists, AI Assistant (conditional), Prompt Builder (conditional), Profile.
  - `Components/Shared/UserGuard.razor` — gating wrapper that redirects to `/` if `UserService.CurrentUserId` is null.
  - `Components/Pages/*.razor` — 28 routable pages and dialogs. Notable: `Home.razor`, `CookbookList.razor`, `CookbookDetail.razor`, `RecipeView.razor`, `RecipeEditor.razor`, `CookingMode.razor` (recipe mode), `AiChat.razor`, `PromptBuilder.razor`, `EditProfile.razor`, `PantryView.razor`, `GroceryListView.razor`, `ImportCookbookDialog.razor`, `ShareCookbookDialog.razor`, `SharedKeysDialog.razor`, `SaveRecipeDialog.razor`, `PasteRawTextDialog.razor`.
  - `Services/CurrentUserService.cs` — holds `CurrentUserId`, fetches `User`+`Profile`, password hashing with PBKDF2-HMAC-SHA256 (100k iterations, 16-byte salt, 32-byte key), admin delete logic.
  - `Services/AiApiKeyResolutionService.cs` — resolves the effective Anthropic key for a user (their own → preferred shared owner → only sharer if exactly one).
  - `Services/AiApiKeyShareService.cs` — grants/revokes share rows in `AiApiKeyShares`; never returns the key itself.
  - `Services/CookbookTransferService.cs` — JSON export/import (see "Cookbook export/import").
  - `Services/CookbookPdfService.cs` — QuestPDF rendering of a `CookbookTransferDocument` to A4 PDF.
  - `Services/CookbookDownloadHelper.cs` — bundles PDF/JSON download flows + `cookBotDownloadFile` JS interop.
  - `wwwroot/app.css`, `wwwroot/js/*.js`, `wwwroot/favicon.png`.
  - `appsettings.json`, `appsettings.Development.json`.
- Depends on: `CookBot.Infrastructure`, `MudBlazor`, `QuestPDF`, `Microsoft.EntityFrameworkCore.Design`.

## Data Flow

**Recipe authoring (manual editor):**

1. User opens `/cookbooks/{id}/recipes/new` or `/recipes/{id}/edit` (`src/CookBot.Web/Components/Pages/RecipeEditor.razor`).
2. Form fields edit metadata, ingredients (with `MudAutocomplete` over `Ingredients.NormalizedName`), and steps. "Paste Raw Text" button opens `PasteRawTextDialog.razor`, which calls `IRecipeFormatParser.TryParse` (YAML frontmatter) and falls back to numbered-line parsing.
3. Save calls `RecipeService.CreateAsync` / `UpdateAsync` (`src/CookBot.Application/Services/RecipeService.cs`). Cookbook ownership is validated; ingredients are resolved by `IngredientResolver.Normalize(name)` and created if new. Steps that omit explicit timers are scanned by `TimerDetectionService.DetectTimers` (regex over `(\d+)\s*(minutes?|mins?|hours?|hrs?|seconds?|secs?)`); ingredient refs are computed by `IngredientRefDetectionService.DetectRefs` (markdown `[name](#id)` first, then case-insensitive substring match for names ≥3 chars).
4. EF Core writes through `Repository<Recipe>` and persists `Recipe`, `RecipeIngredient`, `RecipeStep` (with embedded `StepTimer` list and `IngredientRefs` int list).

**Recipe Mode (cooking flow) — `src/CookBot.Web/Components/Pages/CookingMode.razor`:**

1. Route `/recipes/{RecipeId:int}/cook` loads the recipe via `DbContext.Recipes.Include(...)` after `db.UserCanAccessRecipeAsync(recipeId, userId)` passes.
2. `_allSteps` = ordered steps; `_navigableSteps` = only non-section steps. Section headers are surfaced as a sticky subtitle via `GetSectionHeader(currentStep)` which walks back through `_allSteps` to the most recent `IsSection == true`.
3. Servings are scaled live: `RecipeScalingService.FormatScaledAmount(amount, recipe.Servings, _targetServings)` formatted with `FractionFormatter.Format`. Step text is rendered with `RecipeStepTextFormatter.ToHtml` so `[name](#id)` links become `<span class="ingredient-ref" data-ingredient-id="...">`. Referenced ingredients are highlighted in the right-hand checkbox sidebar.
4. Timers: each `StepTimer` shows as a `MudButton`. Clicking calls `JS.InvokeVoidAsync("CookingTimers.start", timerId, durationSeconds, displayLabel)`. `cooking-timers.js` runs `setInterval(1000)` and calls back via `DotNetObjectReference` into `[JSInvokable] OnTimerTick` / `OnTimerComplete`. Completion plays an 800Hz oscillator beep and (if permitted) raises a Web Notification.
5. "Ask about this step" panel — visible only when `CookBotSettings.AiFeaturesEnabled && profile.AiEnabled && AiKeyResolver.ResolveAsync != null`. Sends one message: system prompt from `PromptBuilderService.BuildCookingStepAssistSystemPrompt(profile)`, user content from `RecipeCookingAiContext.BuildUserMessage(...)` (full scaled YAML + highlighted CURRENT STEP block + ingredient ID legend + question). Reply rendered with `Markdig.Markdown.ToHtml`.

**AI Chat (`/ai`) — `src/CookBot.Web/Components/Pages/AiChat.razor`:**

1. Gate sequence on entry: `CookBotSettings.AiFeaturesEnabled` → `profile.AiEnabled` → `AiApiKeyResolutionService.ResolveAsync` non-null. Each gate has its own empty-state with a CTA (Profile link or "Shared keys" button).
2. System prompt is assembled by `PromptBuilderService.ResolveTemplate(template, profile, pantryItems)` against a token template stored on `UserProfile.AiSystemPromptTemplate` (or `PromptBuilderService.DefaultTemplate` when null). Tokens: `{{experience_level}}`, `{{unit_system}}`, `{{equipment}}`, `{{dietary_preferences}}`, `{{pantry}}`, `{{recipe_format}}`, plus the dynamic `{{cookbook_recipes:<id>}}` token expanded server-side by `ExpandCookbookRecipeTokensAsync` in `AiChat.razor` (regex `\{\{cookbook_recipes:(\d+)\}\}`).
3. Conversation history is kept in `_messages` (`List<AiMessage>`). Each turn calls `IAiService.StreamMessageAsync(systemPrompt, _messages, apiKey, modelId)` — implemented in `src/CookBot.Infrastructure/AI/AnthropicAiService.cs` as a streaming SSE consumer that yields `content_block_delta.delta.text` chunks. Streaming chunks append to `_streamingContent` with `StateHasChanged()` per chunk.
4. After completion, `AiConversation` is persisted with `MessagesJson = JsonSerializer.Serialize(_messages)` and `Title = first message[..50]`.
5. **Recipe save-back from chat** — the assistant message is scanned by `ExtractRecipeContent` in `AiChat.razor` for, in order: a ` ```recipe ... ``` ` fenced block, then a `---\nname:` YAML block, then a loose YAML-ish block containing both `name:` and `ingredients:`. Each candidate is validated by `Parser.TryParse`. On hit a "Save Recipe to Cookbook" button opens `SaveRecipeDialog.razor`.

**Prompt Builder (`/prompt-builder`) — `src/CookBot.Web/Components/Pages/PromptBuilder.razor`:**

1. Same AI gates as `AiChat.razor`.
2. `PromptBuilderService.BuildCopyablePrompt(userRequest, profile, pantryItems, includeProfile, includePantry)` produces a self-contained prompt string with profile narrative + pantry list + the canonical recipe format instructions, designed to be pasted into any external LLM.

## Key Abstractions

**Recipe Format (CookBot YAML frontmatter):**
- Purpose: The **canonical recipe interchange format** — used by the AI to emit recipes, by the manual editor's "Paste Raw Text", and serialized by `IRecipeFormatParser.Serialize` for `RecipeCookingAiContext.BuildUserMessage`.
- Parser: `src/CookBot.Application/Services/RecipeFormatParser.cs` (registered as singleton via `IRecipeFormatParser`).
- Format definition lives in **two places that must be kept in sync** (both literal strings):
  - `PromptBuilderService.ResolveRecipeFormat()` at `src/CookBot.Application/Services/PromptBuilderService.cs` lines 168–202 (used inside the AI system prompt).
  - `PromptBuilderService.BuildCopyablePrompt(...)` at `src/CookBot.Application/Services/PromptBuilderService.cs` lines 262–296 (used by `/prompt-builder` for external LLMs).
- **Wire format** (YAML frontmatter delimited by `---`):
  ```yaml
  ---
  name: "Recipe Name"
  servings: 4
  prepTime: 15
  cookTime: 30
  tags: [tag1, tag2]
  ingredients:
    - id: 1
      name: "ingredient name"
      amount: 2
      unit: "cups"
    - id: 2
      name: "another ingredient"
      amount: 1
      unit: "tbsp"
      note: "optional note"
  steps:
    - text: "Step instruction with [ingredient name](#1)."
    - section: "Section header"
    - text: "Bake for 25 minutes."
      timers:
        - duration: 25
          unit: min
          label: "bake"
  ---
  ```
- Step text uses **`[display name](#id)`** markdown-style links to reference an ingredient by its local `id`. Detection regex: `\[([^\]]+)\]\(#(\d+)\)` (`src/CookBot.Application/Services/IngredientRefDetectionService.cs` line 9, also reused by `RecipeStepTextFormatter`).
- Steps may be either a **content step** (`text`, optional `timers`) or a **section header** (`section: "..."`). Sections are not navigable in cooking mode but display as sticky subtitles.
- Timer units in YAML: `min` | `hr` | `sec` (default `min`). `CookingMode.razor` `StartTimer` converts to seconds: `hr*3600`, `min*60`, `sec*1`.
- Fallback: when no `steps:` are provided, `RecipeFormatParser` falls back to numbered lines in the markdown body matching `^\d+\.\s*(.+)$`.
- AI fenced block: assistants are told to wrap recipes in ` ```recipe ... ``` `; `AiChat.ExtractRecipeContent` recognizes that fence specifically.
- **User-flagged concern:** "users shouldn't have to know our special syntax" — `PasteRawTextDialog.razor` already accepts loose numbered-line text, but the *structured* format is still required for: (a) the in-app "Paste Raw Text" YAML path, (b) AI emissions that the chat will recognize and offer to save, (c) the `[name](#id)` link syntax that powers ingredient highlighting in cooking mode and the cooking-step AI prompt. Any future "make it more forgiving" work happens in `RecipeFormatParser.cs` and `AiChat.ExtractRecipeContent`.

**Cookbook Transfer Document (portable export format):**
- Purpose: Portable JSON file for backup, sharing, and PDF generation. Independent of EF schema.
- DTOs: `src/CookBot.Application/DTOs/CookbookTransferDtos.cs` — `CookbookTransferDocument` (with `SchemaVersion = 1`, `ExportedAt`, `SourceApp = "CookBot"`, `Cookbook`, `Recipes`), `CookbookTransferCookbook`, `CookbookTransferRecipe`, `CookbookTransferIngredient`, `CookbookTransferStep`, `CookbookTransferTimer`.
- Builder/importer: `src/CookBot.Web/Services/CookbookTransferService.cs` — `BuildExportAsync(cookbookId, userId)`, `SerializeToUtf8Json(doc)` (camelCase, indented), `Deserialize(json, out errors)`, `ImportAsNewCookbookAsync(userId, doc, overrideName?)`.
- File extension: `.cookbook.json` (download) and `.pdf` (PDF download). Filename stem comes from `CookbookDownloadHelper.SafeFileStem(name)` which strips invalid path chars.
- Schema version: hard-coded `1`. Deserializer rejects any other value.
- **User-flagged concern:** "standardizing our file format" — the exchange format is `CookbookTransferDocument` with `SchemaVersion = 1`. The recipe content inside it is **not** the YAML wire format; it's a flat per-recipe JSON object with `Name`, `Servings`, `PrepTimeMinutes`, `CookTimeMinutes`, `Tags`, `Ingredients`, `Steps`. Step text in the JSON still contains `[name](#id)` markdown links since that's the same string stored in `RecipeStep.Text`. Any standardization work needs to address both this DTO shape and the YAML wire format above.

**Repositories:**
- `IRepository<T>` (`src/CookBot.Domain/Interfaces/IRepository.cs`) — `GetByIdAsync`, `GetAllAsync`, `FindAsync(predicate)`, `AddAsync`, `UpdateAsync`, `DeleteAsync`.
- One implementation: `Repository<T>` (`src/CookBot.Infrastructure/Data/Repositories/Repository.cs`) — eagerly saves on every Add/Update/Delete.
- Used by application services (`RecipeService`, `CookbookService`, `PantryService`, `GroceryListService`, `PantryAiPopulationService`).
- Razor pages and the Web-layer services (`CookbookTransferService`, `AiApiKeyResolutionService`, `CookingMode.razor`) bypass it and inject `CookBotDbContext` directly for `Include(...)`/`AsNoTracking()` queries.

## Entry Points

**Program.cs (`src/CookBot.Web/Program.cs`):**
- Triggers: `dotnet run --project src/CookBot.Web` (see `run.sh`).
- Composition root for the entire app. Standard ASP.NET Core builder pattern.
- After `app.Build()` runs `DatabaseSeeder.SeedAsync(context, app.Environment.ContentRootPath)` synchronously before serving. Seeder calls `Database.MigrateAsync()`, so schema is always up to date on boot.
- Maps Razor components with interactive server render mode at the root; no controllers, no API endpoints.

**Top-level routes (declared via `@page` in `src/CookBot.Web/Components/Pages/`):**
- `/` — `Home.razor`
- `/cookbooks` — `CookbookList.razor`
- `/cookbooks/{CookbookId:int}` — `CookbookDetail.razor`
- `/cookbooks/{CookbookId:int}/recipes/new` — `RecipeEditor.razor`
- `/recipes/{RecipeId:int}` — `RecipeView.razor`
- `/recipes/{RecipeId:int}/edit` — `RecipeEditor.razor`
- `/recipes/{RecipeId:int}/cook` — `CookingMode.razor`
- `/recipes/{RecipeId:int}/made` — `RecipeMade.razor`
- `/pantry` — `PantryView.razor`
- `/grocery-lists` — `GroceryListView.razor`
- `/ai` — `AiChat.razor`
- `/prompt-builder` — `PromptBuilder.razor`
- `/profile` — `EditProfile.razor`

## API Key Storage

API keys for Anthropic are stored at three possible levels, resolved by `src/CookBot.Web/Services/AiApiKeyResolutionService.cs`:

1. **Per-user (primary)** — `UserProfile.AiApiKey` (string, nullable) at `src/CookBot.Domain/Entities/UserProfile.cs` line 17. Persisted as a plain SQLite column. Set from `EditProfile.razor`. The user can also pick `UserProfile.AiModel` (defaults to `AnthropicAiService.DefaultModelId = "claude-sonnet-4-6"`).
2. **Shared between users** — `AiApiKeyShare` rows (`src/CookBot.Domain/Entities/AiApiKeyShare.cs`, table `AiApiKeyShares`) link an `OwnerUserId` to a `RecipientUserId`. The recipient never sees the key — `AiApiKeyResolutionService.ResolveAsync` joins `AiApiKeyShares` against the owner's `UserProfile.AiApiKey` server-side and returns an `EffectiveAiCredentials(ApiKey, ModelId, SharedFromUserId, SharedFromDisplayName)`. Recipients with multiple sharers pick a preferred owner via `UserProfile.AiSharedKeyOwnerUserId`. Granting/revoking happens in `AiApiKeyShareService.GrantAsync` / `RevokeAsync`; the UI is `SharedKeysDialog.razor`.
3. **Server fallback** — `CookBotSettings.AnthropicApiKey` from `appsettings.json` `"CookBot": { "AnthropicApiKey": "..." }` (`src/CookBot.Application/DTOs/CookBotSettings.cs` line 20). `AnthropicAiService.CreateHttpClient` uses it only when the per-call `apiKey` argument is null. In practice all callers pass `EffectiveAiCredentials.ApiKey`, so this is a developer-mode fallback.

The host can disable AI for everyone with `CookBotSettings.AiFeaturesEnabled = false`. Each user can additionally toggle `UserProfile.AiEnabled` (default `false`, see migration `20260416012530_AiEnabledDefaultFalse`) — when off, the AI Assistant and Prompt Builder nav links are hidden by `NavMenu.razor`.

## Error Handling

**Strategy:** Defensive try/catch at the UI boundary in Razor pages, surfacing problems through `MudBlazor.ISnackbar`. No global exception filter or middleware beyond `app.UseExceptionHandler("/Error")` in non-Development environments (`Components/Pages/Error.razor`).

**Patterns:**
- Application services throw `UnauthorizedAccessException` for ownership violations and `InvalidOperationException` for missing entities (e.g. `RecipeService.CreateAsync`, `CookbookService.GetByIdAsync`).
- AI/HTTP errors throw `HttpRequestException` with the raw response body; UI catches and shows `Snackbar.Add(ex.Message, Severity.Error)`.
- `IRecipeFormatParser.TryParse(content, out recipe, out errors)` is the non-throwing path used wherever user-supplied recipe text is involved.
- `CookbookTransferService.Deserialize` returns `null` and an error list rather than throwing.

## Cross-Cutting Concerns

**Logging:** Default ASP.NET Core `ILogger`. Levels in `src/CookBot.Web/appsettings.json` — `Default: Information`, `Microsoft.AspNetCore: Warning`. No structured logging, no Serilog/Seq.

**Validation:** Mostly inline in services and Razor forms. `MudBlazor` handles required-field UX. `RecipeFormatParser.TryParse` validates non-empty name, positive servings, ≥1 ingredient, unique ingredient IDs, ≥1 step.

**Authentication:** None. Browser-side `sessionStorage` selects which user the circuit acts as. `User.PasswordHash` is optional PBKDF2 hash; if set, switching to that user prompts `PasswordPromptDialog.razor` and `CurrentUserService.VerifyPasswordAsync`. Designed for trusted-LAN self-hosting; `CookBotSettings.AuthMode` is reserved for future hardening.

**Authorization:** Each service that mutates user data checks ownership against the cookbook owner (e.g. `RecipeService.CreateAsync` rejects when `cookbook.UserId != userId`). Read access uses the share table — `db.UserCanAccessRecipeAsync` and `CookbookService.GetByIdAsync` allow either ownership or a `CookbookShare` row.

**Persistence:** EF Core 10 + SQLite. Connection string in `appsettings.json → ConnectionStrings.DefaultConnection`. Migrations live in `src/CookBot.Infrastructure/Migrations/` and are applied automatically by `DatabaseSeeder.SeedAsync` at boot. The DB file `cookbot.db` lands next to `Program.cs` and is gitignored (`.gitignore` line 23 `*.db`).

**Theming/UI:** MudBlazor with a custom orange/green palette (`#E65100` primary, `#2E7D32` secondary) defined in `MainLayout.razor`. Inter font loaded from Google Fonts. Dark mode toggled via `body.dark-mode` class + `localStorage["cookbot_dark_mode"]`.

**JS Interop:** Two modules under `src/CookBot.Web/wwwroot/js/`:
- `cooking-timers.js` — exposes `window.CookingTimers.{init,start,stop,getRemaining,requestNotificationPermission,dispose}`, callbacks `_dotNetRef.invokeMethodAsync('OnTimerTick'|'OnTimerComplete', ...)`.
- `download.js` — exposes `window.cookBotDownloadFile(fileName, mimeType, base64)` for PDF/JSON export downloads.

---

*Architecture analysis: 2026-04-25*
