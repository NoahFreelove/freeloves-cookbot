# Codebase Structure

**Analysis Date:** 2026-04-25

## Directory Layout

```
freeloves-cookbot/
├── FreelovesCookBot.sln           # Solution file — 4 src projects + 1 test project
├── README.md                      # Feature list and project rationale
├── LICENSE                        # GPL-3.0
├── run.sh                         # `dotnet run --project src/CookBot.Web`
├── .gitignore                     # Standard .NET ignores; *.db is excluded
├── .gitattributes
├── .planning/                     # GSD planning + codebase docs (this directory)
│   └── codebase/                  # ARCHITECTURE.md, STRUCTURE.md, STACK.md, ...
├── seeds/
│   └── ingredients.json           # 624-line seed list of ~600 ingredients with category + preferred units
├── src/
│   ├── CookBot.Domain/            # Entities, enums, interfaces — no framework deps
│   ├── CookBot.Application/       # Services, parsers, DTOs — pure business logic
│   ├── CookBot.Infrastructure/    # EF Core, migrations, Anthropic HTTP client
│   └── CookBot.Web/               # Blazor Server host, Razor components, JS, wwwroot
└── tests/
    └── CookBot.Tests/             # xUnit unit tests (per-service, no DB integration)
```

## Solution Layout

`FreelovesCookBot.sln` defines two solution folders (`src` and `tests`) and five projects:

| Project | Path | SDK |
|---|---|---|
| `CookBot.Domain` | `src/CookBot.Domain/CookBot.Domain.csproj` | `Microsoft.NET.Sdk` |
| `CookBot.Application` | `src/CookBot.Application/CookBot.Application.csproj` | `Microsoft.NET.Sdk` |
| `CookBot.Infrastructure` | `src/CookBot.Infrastructure/CookBot.Infrastructure.csproj` | `Microsoft.NET.Sdk` |
| `CookBot.Web` | `src/CookBot.Web/CookBot.Web.csproj` | `Microsoft.NET.Sdk.Web` |
| `CookBot.Tests` | `tests/CookBot.Tests/CookBot.Tests.csproj` | `Microsoft.NET.Sdk` |

All projects target `net10.0` with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.

Project reference chain: `Web → Infrastructure → Application → Domain`. The test project references all three non-Web projects directly.

## Directory Purposes

**`src/CookBot.Domain/`:**
- Purpose: Pure domain — entities, enums, interfaces. The only "framework-free" project; intentionally has zero NuGet PackageReferences.
- Contains:
  - `Entities/` — 16 EF aggregates (`Recipe.cs`, `RecipeStep.cs`, `RecipeIngredient.cs`, `StepTimer.cs`, `Cookbook.cs`, `CookbookShare.cs`, `User.cs`, `UserProfile.cs`, `AiConversation.cs`, `AiApiKeyShare.cs`, `Pantry.cs`, `PantryItem.cs`, `PantryMember.cs`, `Ingredient.cs`, `GroceryList.cs`, `GroceryListItem.cs`).
  - `Enums/` — `AuthMode.cs`, `ExperienceLevel.cs`, `IngredientCategory.cs`, `MeasurementUnit.cs`, `UnitSystem.cs`.
  - `Interfaces/` — `IAiService.cs` (with embedded `AiMessage`/`AiModelInfo`), `IRecipeFormatParser.cs` (with `ParsedRecipe`/`ParsedStep`/`ParsedTimer`/`ParsedIngredient`), `IRepository.cs`, `IUnitConverter.cs`, `IPricingProvider.cs`.
  - `Models/NutritionalInfo.cs`.

**`src/CookBot.Application/`:**
- Purpose: Use-case-style services and pure-logic helpers. Knows about `IRepository<T>` and `IAiService` but never about EF or HTTP.
- Contains:
  - `Services/` — 16 service classes/static helpers (see ARCHITECTURE.md for the catalog). Includes the canonical `RecipeFormatParser.cs` and the AI prompt machinery (`PromptBuilderService.cs`, `RecipeCookingAiContext.cs`, `PantryAiPopulationService.cs`).
  - `Recipes/` — Pure projectors and formatters over `RecipeDocument`: `JsonRecipeSerializer.cs`, `RecipeUpcasterChain.cs`, `JsonLdRecipeProjector.cs` (Phase 13 — Schema.org Recipe JSON-LD), `CooklangRecipeProjector.cs` (Phase 13 — Cooklang .cook export), `Iso8601DurationFormatter.cs` (Phase 13 — PT#H#M formatter), upcaster migrations, schema validation helpers.
  - `DTOs/CookBotSettings.cs` — host config bound from `"CookBot"` JSON section.
  - `DTOs/CookbookTransferDtos.cs` — portable cookbook export schema (`SchemaVersion = 1`).
  - `DependencyInjection.cs` — `IServiceCollection.AddApplication()`.

**`src/CookBot.Infrastructure/`:**
- Purpose: EF Core, SQLite, and the Anthropic API client.
- Contains:
  - `Data/CookBotDbContext.cs` — single `DbContext`, 14 `DbSet`s.
  - `Data/Configurations/` — 14 fluent EF configurations (one per aggregate that needs config), auto-applied via `ApplyConfigurationsFromAssembly`.
  - `Data/Repositories/Repository.cs` — generic `IRepository<T>` implementation.
  - `Data/DatabaseSeeder.cs` — runs migrations + seeds default user/cookbook/pantry + ingredient catalog.
  - `Data/RecipeAccessExtensions.cs` — `db.UserCanAccessRecipeAsync` extension method.
  - `Migrations/` — five EF Core migrations (`20260314212609_V2InitialCreate`, `20260416012530_AiEnabledDefaultFalse`, `20260416021611_UserProfileAiUnitExceptions`, `20260416170415_UserCookBotAdminFlag`, `20260416175214_AiApiKeyShares`) plus `CookBotDbContextModelSnapshot.cs`.
  - `AI/AnthropicAiService.cs` — `IAiService` implementation; talks to `https://api.anthropic.com/v1/messages` directly.
  - `DependencyInjection.cs` — `IServiceCollection.AddInfrastructure(IConfiguration)`.

**`src/CookBot.Web/`:**
- Purpose: Blazor Server host. Razor components, web-only services, JS, CSS, configuration files, and the executable entry point.
- Contains:
  - `Program.cs` — composition root + `app.Run()`.
  - `appsettings.json` and `appsettings.Development.json` — `Logging`, `ConnectionStrings.DefaultConnection`, and `"CookBot": { AuthMode, AppName, AiFeaturesEnabled, AnthropicApiKey }`.
  - `Properties/launchSettings.json` — local dev profiles.
  - `Components/App.razor` — HTML document, MudBlazor and JS asset references.
  - `Components/Routes.razor` — `<Router>` with `MainLayout` default.
  - `Components/_Imports.razor` — global `@using` directives for every page (`MudBlazor`, `Microsoft.EntityFrameworkCore`, `CookBot.Web.Services`, `CookBot.Domain.Entities`, etc.).
  - `Components/Layout/` — `MainLayout.razor`, `NavMenu.razor`, `AddUserDialog.razor`, `AdminManageUsersDialog.razor`, `PasswordPromptDialog.razor`.
  - `Components/Shared/UserGuard.razor` — wraps page content; redirects to `/` when no current user.
  - `Components/Pages/` — 28 `.razor` files (routable pages + dialog components). Naming: pages use the noun (`CookbookList`, `RecipeView`); dialogs end in `Dialog` (`SaveRecipeDialog`, `ImportCookbookDialog`).
  - `Services/` — Web-layer services that need `CookBotDbContext` directly: `CurrentUserService.cs`, `AiApiKeyResolutionService.cs`, `AiApiKeyShareService.cs`, `CookbookTransferService.cs`, `CookbookPdfService.cs`, `CookbookDownloadHelper.cs`.
  - `wwwroot/app.css`, `wwwroot/favicon.png`.
  - `wwwroot/js/cooking-timers.js`, `wwwroot/js/download.js` — the only two custom JS files.

**`tests/CookBot.Tests/`:**
- Purpose: xUnit unit tests for pure-logic services. No fixtures touch the DB beyond a few in-memory SQLite tests in `OwnershipTests`/`RecipeAccessExtensionsTests`.
- Contains:
  - `Services/` — `FractionFormatterTests.cs`, `IngredientRefDetectionServiceTests.cs`, `OwnershipTests.cs`, `PantryAiPopulationServiceTests.cs`, `RecipeAccessExtensionsTests.cs`, `RecipeCookingAiContextTests.cs`, `RecipeFormatParserTests.cs`, `RecipeScalingServiceTests.cs`, `RecipeStepTextFormatterTests.cs`, `TimerDetectionServiceTests.cs`, `UnitConversionServiceTests.cs`, `UnitParserTests.cs`.
  - `Entities/UserProfileTests.cs`.
  - `DTOs/CookBotSettingsTests.cs`.
  - `UnitTest1.cs` — leftover scaffolding.

**`seeds/`:**
- Purpose: JSON fixtures loaded at first run by `DatabaseSeeder.LoadIngredientsFromSeedFile`.
- `ingredients.json` — array of `{ "name", "category", "preferredUnits": [] }`. ~600 entries spanning every `IngredientCategory`. Path resolved relative to `ContentRootPath` as `../seeds/ingredients.json`.

**`.planning/codebase/`:**
- Purpose: GSD-generated codebase reference docs. This directory holds STACK.md, STRUCTURE.md, ARCHITECTURE.md, etc., and is intended to be read by future GSD agents.

## Key File Locations

**Entry Points:**
- `src/CookBot.Web/Program.cs` — process entry, DI composition, DB seeding.
- `run.sh` — convenience wrapper (`dotnet run --project src/CookBot.Web`).

**Configuration:**
- `src/CookBot.Web/appsettings.json` — base config; binds to `CookBotSettings` via `"CookBot"` section. Contains `ConnectionStrings.DefaultConnection = "Data Source=cookbot.db"`.
- `src/CookBot.Web/appsettings.Development.json` — dev overrides.
- `src/CookBot.Web/Properties/launchSettings.json` — local launch profiles.
- `.gitignore` — note line 28 explicitly excludes `appsettings.*.json` except for `appsettings.json` and `appsettings.Development.json`, so secrets live in user-secrets or env vars.

**Composition Roots:**
- `src/CookBot.Web/Program.cs` — registers everything Web-layer + calls `AddInfrastructure`.
- `src/CookBot.Infrastructure/DependencyInjection.cs` — registers `CookBotDbContext`, `IRepository<>`, `IAiService → AnthropicAiService`, `PromptBuilderService`, then chains `AddApplication`.
- `src/CookBot.Application/DependencyInjection.cs` — registers `IRecipeFormatParser`, `IUnitConverter`, plus `CookbookService`, `RecipeService`, `PantryService`, `PantryAiPopulationService`, `GroceryListService`.

**Core Domain:**
- `src/CookBot.Domain/Entities/Recipe.cs`, `RecipeStep.cs`, `StepTimer.cs`, `RecipeIngredient.cs` — recipe aggregate.
- `src/CookBot.Domain/Entities/Cookbook.cs`, `CookbookShare.cs` — cookbook aggregate.
- `src/CookBot.Domain/Entities/User.cs`, `UserProfile.cs`, `AiApiKeyShare.cs` — identity + AI configuration.
- `src/CookBot.Domain/Entities/Pantry.cs`, `PantryItem.cs`, `PantryMember.cs` — pantry aggregate (with shared-pantry support).
- `src/CookBot.Domain/Interfaces/IRecipeFormatParser.cs` — `ParsedRecipe` is the in-memory recipe shape passed between parsing/saving.

**Recipe Format & AI:**
- `src/CookBot.Application/Services/RecipeFormatParser.cs` — YAML frontmatter parser/serializer.
- `src/CookBot.Application/Services/PromptBuilderService.cs` — system prompt template + canonical recipe-format text (lines 168–202 and 262–296 — these two literal strings define the format the AI emits).
- `src/CookBot.Application/Services/RecipeCookingAiContext.cs` — builds the cooking-mode user message.
- `src/CookBot.Application/Services/IngredientRefDetectionService.cs` — `[name](#id)` markdown link detection.
- `src/CookBot.Application/Services/RecipeStepTextFormatter.cs` — renders step text → HTML or plain text.
- `src/CookBot.Application/Services/TimerDetectionService.cs` — natural-language timer extraction.
- `src/CookBot.Infrastructure/AI/AnthropicAiService.cs` — Anthropic HTTP client with SSE streaming.

**Cookbook Export/Import:**
- `src/CookBot.Application/DTOs/CookbookTransferDtos.cs` — wire DTO (`SchemaVersion = 1`).
- `src/CookBot.Web/Services/CookbookTransferService.cs` — `BuildExportAsync` / `Deserialize` / `ImportAsNewCookbookAsync`.
- `src/CookBot.Web/Services/CookbookPdfService.cs` — QuestPDF rendering.
- `src/CookBot.Web/Services/CookbookDownloadHelper.cs` — JS download interop bridge.
- `src/CookBot.Web/Components/Pages/ImportCookbookDialog.razor` — `.json` upload handler.
- `src/CookBot.Web/Components/Pages/CookbookDetail.razor` — "Download PDF" / "Download JSON" menu.
- `src/CookBot.Web/wwwroot/js/download.js` — `window.cookBotDownloadFile(fileName, mimeType, base64)`.

**Cooking Mode (Recipe Mode):**
- `src/CookBot.Web/Components/Pages/CookingMode.razor` — single-page cooking flow with timers, AI step assist, ingredient highlighting.
- `src/CookBot.Web/wwwroot/js/cooking-timers.js` — timer interval + Web Notification + audio beep.

**API Key Storage:**
- `src/CookBot.Domain/Entities/UserProfile.cs` line 17 — `AiApiKey`.
- `src/CookBot.Domain/Entities/AiApiKeyShare.cs` — share link table.
- `src/CookBot.Web/Services/AiApiKeyResolutionService.cs` — resolution priority logic.
- `src/CookBot.Web/Services/AiApiKeyShareService.cs` — grant/revoke + preferred owner.
- `src/CookBot.Web/Components/Pages/SharedKeysDialog.razor` — UI for managing shares.
- `src/CookBot.Web/Components/Pages/EditProfile.razor` — UI for entering own key + model selection.
- `src/CookBot.Application/DTOs/CookBotSettings.cs` — `AnthropicApiKey` server fallback.

**Persistence:**
- `src/CookBot.Infrastructure/Data/CookBotDbContext.cs` — DbContext.
- `src/CookBot.Infrastructure/Data/Configurations/*.cs` — fluent configs.
- `src/CookBot.Infrastructure/Migrations/*.cs` — five migrations.
- `src/CookBot.Infrastructure/Data/DatabaseSeeder.cs` — auto-migrate + seed.
- `src/CookBot.Web/cookbot.db` — runtime SQLite file (gitignored).

## Naming Conventions

**Projects:**
- `CookBot.<Layer>` — PascalCase, dot-separated. `Domain`, `Application`, `Infrastructure`, `Web`, `Tests`.

**Namespaces:**
- Match folder structure: `CookBot.Application.Services`, `CookBot.Domain.Entities`, `CookBot.Web.Components.Pages`, etc.

**Files:**
- C# classes: `PascalCase.cs` matching the type name.
- Razor pages: `PascalCase.razor` (e.g. `CookingMode.razor`, `RecipeEditor.razor`).
- Razor dialogs: `PascalCaseDialog.razor` (e.g. `SaveRecipeDialog.razor`, `ImportCookbookDialog.razor`, `SharedKeysDialog.razor`).
- Migrations: `<UTC-yyyyMMddHHmmss>_<PascalCaseName>.cs` (e.g. `20260416175214_AiApiKeyShares.cs`).

**Directories:**
- PascalCase plurals for entity-style content (`Entities/`, `Services/`, `Migrations/`, `Components/Pages/`).
- Lowercase for static-asset-ish folders (`wwwroot/`, `wwwroot/js/`, `seeds/`, `tests/`).

**Methods:**
- Async methods always end in `Async` (`GetByIdAsync`, `BuildExportAsync`, `StreamMessageAsync`).
- Boolean-returning checks use `CanAccessAsync`/`UserCanAccessRecipeAsync`/`HasPasswordAsync`.

**Interfaces:**
- Prefixed `I` (`IAiService`, `IRecipeFormatParser`, `IRepository<T>`, `IUnitConverter`, `IPricingProvider`).

**Properties:**
- JSON columns end in `Json` (`TagsJson`, `KitchenToolsJson`, `DietaryPreferencesJson`, `MessagesJson`, `PreferredUnitsJson`, `NutritionalInfoJson`).

## Where to Add New Code

**New domain entity:**
- Create `src/CookBot.Domain/Entities/<Name>.cs`.
- Add a corresponding fluent config at `src/CookBot.Infrastructure/Data/Configurations/<Name>Configuration.cs`.
- Add a `DbSet<Name>` to `src/CookBot.Infrastructure/Data/CookBotDbContext.cs`.
- Generate a migration: `dotnet ef migrations add <Name> --project src/CookBot.Infrastructure --startup-project src/CookBot.Web`.

**New application service:**
- Add `src/CookBot.Application/Services/<Name>Service.cs`.
- Register in `src/CookBot.Application/DependencyInjection.cs` with the appropriate lifetime (most are `AddScoped`).

**New web-layer service** (needs `CookBotDbContext` or `IJSRuntime` directly):
- Add `src/CookBot.Web/Services/<Name>.cs`.
- Register in `src/CookBot.Web/Program.cs` (`builder.Services.AddScoped<...>()`).

**New page:**
- Add `src/CookBot.Web/Components/Pages/<Name>.razor` with `@page "/route"` and `@rendermode InteractiveServer`.
- Wrap interactive content in `<UserGuard> ... </UserGuard>` if the page requires a logged-in user.
- Add a nav entry in `src/CookBot.Web/Components/Layout/NavMenu.razor` if it should appear in the rail.

**New dialog:**
- Add `src/CookBot.Web/Components/Pages/<Name>Dialog.razor` (note: dialogs live alongside pages, not in a separate folder).
- Use `[CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;`.
- Open via `IDialogService.ShowAsync<NameDialog>(...)` from a parent page.

**New external integration / SDK:**
- Define an interface in `src/CookBot.Domain/Interfaces/I<Name>Service.cs`.
- Implement in `src/CookBot.Infrastructure/<Subfolder>/<Name>Service.cs` (parallel to `Infrastructure/AI/AnthropicAiService.cs`).
- Register in `src/CookBot.Infrastructure/DependencyInjection.cs`.

**New JS interop:**
- Add `src/CookBot.Web/wwwroot/js/<name>.js` with a `window.<Name> = { ... }` shape.
- Add a `<script src="js/<name>.js"></script>` line to `src/CookBot.Web/Components/App.razor` (after `blazor.web.js`).
- Call from C# via `IJSRuntime.InvokeVoidAsync("Name.method", args)`. Two-way callbacks need `DotNetObjectReference.Create(this)` and `[JSInvokable]` methods (see `CookingMode.razor` for the canonical pattern).

**New seed data:**
- For ingredients: edit `seeds/ingredients.json`. Fields: `name`, `category` (must match `IngredientCategory` enum), `preferredUnits` (array of strings).
- Loaded only on a fresh DB — `DatabaseSeeder.SeedAsync` skips seeding if any user already exists.

**New unit test:**
- Add `tests/CookBot.Tests/Services/<Name>Tests.cs` (or `Entities/`, `DTOs/`).
- xUnit + plain `Assert`. The test project already has `<Using Include="Xunit" />` so `[Fact]` / `[Theory]` are globally available.

## Special Directories

**`src/CookBot.Infrastructure/Migrations/`:**
- Purpose: EF Core schema migrations.
- Generated: Yes (`dotnet ef migrations add`).
- Committed: Yes — applied at startup by `DatabaseSeeder.SeedAsync`.
- Naming: `<UTC-yyyyMMddHHmmss>_<PascalCaseName>.cs` plus the `*.Designer.cs` snapshot side-files.

**`src/CookBot.Web/wwwroot/`:**
- Purpose: Static assets served by ASP.NET. Custom JS/CSS only — Blazor's framework JS comes from `_framework/blazor.web.js` (auto-served).
- Generated: No.
- Committed: Yes.

**`bin/` and `obj/` (in every project):**
- Generated: Yes. Gitignored (`.gitignore` lines 2–3).

**`*.db`, `*.db-shm`, `*.db-wal`:**
- Purpose: Runtime SQLite database files at `src/CookBot.Web/cookbot.db`.
- Generated: Yes (created on first run by `DatabaseSeeder.SeedAsync`).
- Committed: No — gitignored.

**`appsettings.*.json`:**
- Per `.gitignore` line 28: only `appsettings.json` and `appsettings.Development.json` are committed; all other environment-specific settings files (e.g. `appsettings.Production.json`) are intentionally excluded as they may carry secrets like `AnthropicApiKey`.

---

*Structure analysis: 2026-04-25*
