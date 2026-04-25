# Coding Conventions

**Analysis Date:** 2026-04-25

## Language and Project Settings

The codebase is C# targeting `net10.0` with `Nullable` and `ImplicitUsings` enabled in every project (see all `.csproj` files such as `src/CookBot.Application/CookBot.Application.csproj` line 14-17). No `.editorconfig` is committed at the repo root — formatting follows default Visual Studio / `dotnet format` behavior with the conventions documented below derived from the existing source.

## Naming Patterns

**Files:**
- One public top-level type per file. File name matches the type (e.g. `RecipeService.cs` defines `class RecipeService`).
- Razor pages and dialogs use `PascalCase.razor` (e.g. `src/CookBot.Web/Components/Pages/CookbookList.razor`, `CookbookFormDialog.razor`).
- Tests mirror the type under test with the suffix `Tests.cs` (e.g. `tests/CookBot.Tests/Services/RecipeScalingServiceTests.cs`).

**Types:**
- `PascalCase` for classes, records, structs, interfaces, enums and enum members.
- Interfaces are prefixed with `I` (`IRepository<T>`, `IRecipeFormatParser`, `IAiService`, `IUnitConverter`, `IPricingProvider` in `src/CookBot.Domain/Interfaces/`).
- Domain entities live in `src/CookBot.Domain/Entities/` with simple noun names (`Recipe`, `Cookbook`, `User`, `PantryItem`).
- Service classes end in `Service` (`RecipeService`, `CookbookService`, `PantryService`).
- Static helpers use a behavior-describing noun without `Service` (`FractionFormatter`, `IngredientResolver`, `RecipeStepTextFormatter`).
- DTOs / parsed models use plain nouns or `Parsed`-prefixed nouns (`ParsedRecipe`, `ParsedIngredient`, `ParsedStep`, `PantryAiImportRow`).
- Records are used for tiny immutable shape carriers (`public sealed record PantryAiImportRow(...)` in `src/CookBot.Application/Services/PantryAiPopulationService.cs:11`, `public record AiModelInfo(string Id, string DisplayName)` in `src/CookBot.Domain/Interfaces/IAiService.cs:9`).

**Members:**
- Public members: `PascalCase`. Public properties always use `{ get; set; }` auto-properties (see `src/CookBot.Domain/Entities/Recipe.cs`).
- Private fields: `_camelCase` prefixed with underscore (`_recipeRepo`, `_parser`, `_db`, `_settings`).
- Constants: `PascalCase` (`PantryAiImport.UnmeasuredUnit = "staple"`, `AnthropicAiService.DefaultModelId`).
- Local variables and parameters: `camelCase`.

**Async methods:** Always suffixed with `Async` (`CreateAsync`, `GetByIdAsync`, `LoadCookbooksAsync`, `SendMessageAsync`).

## File-Scoped Namespaces

All `.cs` files use C# 10 file-scoped namespace declarations terminated with a semicolon, followed by a blank line:

```csharp
namespace CookBot.Application.Services;

public class RecipeService
{
    ...
}
```

Block-style `namespace { }` is only seen in EF Core auto-generated migration files under `src/CookBot.Infrastructure/Migrations/` and should not be used for hand-written code.

## Project Layout / Layering

The solution follows a Clean Architecture / onion layering. Dependencies flow inward:

- `CookBot.Domain` — pure POCO entities, enums, interfaces. No external package references except `Microsoft.NET.Sdk`.
- `CookBot.Application` — services, DTOs, parsing/formatting logic. References `Domain` only. May reference `YamlDotNet`, `Markdig`, `Microsoft.Extensions.DependencyInjection.Abstractions`.
- `CookBot.Infrastructure` — EF Core `DbContext`, repositories, EF configurations, AI HTTP client. References `Application`.
- `CookBot.Web` — Blazor Server UI, page-level services. References `Infrastructure`.

Never let `Domain` reference `Application` or anything outward. Service classes in `Application` consume domain interfaces (`IRepository<T>`, `IAiService`) — implementations live in `Infrastructure`.

## Imports / Usings

`ImplicitUsings` is enabled, so common namespaces (`System`, `System.Collections.Generic`, `System.Linq`, `System.Threading.Tasks`, etc.) are NOT imported explicitly.

Explicit `using` directives appear at the top of the file before the namespace declaration. Order observed (no automated sorting enforced, but consistent in source):

1. `System.*` namespaces (e.g. `using System.Text.Json;`, `using System.Text.RegularExpressions;`).
2. Framework / Microsoft / third-party (`Microsoft.EntityFrameworkCore`, `MudBlazor.Services`, `YamlDotNet.Serialization`).
3. Project namespaces grouped by layer (`CookBot.Domain.*`, `CookBot.Application.*`, `CookBot.Infrastructure.*`, `CookBot.Web.*`).

The xUnit test project enables a global `using Xunit;` via `tests/CookBot.Tests/CookBot.Tests.csproj:18-20`, so test files do not need to import `Xunit` explicitly.

## Class Design

**Constructor injection:** Services exclusively use constructor injection. Fields are `readonly` and assigned in the constructor:

```csharp
public class RecipeService
{
    private readonly IRecipeFormatParser _parser;
    private readonly IRepository<Recipe> _recipeRepo;
    private readonly IRepository<Ingredient> _ingredientRepo;
    private readonly IRepository<Cookbook> _cookbookRepo;

    public RecipeService(
        IRecipeFormatParser parser,
        IRepository<Recipe> recipeRepo,
        IRepository<Ingredient> ingredientRepo,
        IRepository<Cookbook> cookbookRepo)
    {
        _parser = parser;
        _recipeRepo = recipeRepo;
        _ingredientRepo = ingredientRepo;
        _cookbookRepo = cookbookRepo;
    }
}
```

`src/CookBot.Application/Services/RecipeService.cs:7-24`.

**Pure / stateless helpers:** Use `public static class` with `public static` methods (`FractionFormatter`, `TimerDetectionService`, `IngredientResolver`, `IngredientRefDetectionService`, `RecipeStepTextFormatter`). No instance state, no DI registration required.

**`sealed record` / `sealed class`:** Used for small immutable result / row types (`PantryAiImportRow`, `PantryAiPopulationResult`).

## Properties and Initialization

Entities default required strings and collections inline to avoid null:

```csharp
public class Recipe
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TagsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<RecipeStep> Steps { get; set; } = new();
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}
```

`src/CookBot.Domain/Entities/Recipe.cs:3-18`.

EF navigation properties that are guaranteed by the schema use the null-forgiving operator: `public Cookbook Cookbook { get; set; } = null!;`. Optional FKs use nullable types (`int?`, `string?`).

## Error Handling

**Throw early at the boundary.** Application services validate authorization and existence and throw standard BCL exceptions:

- `InvalidOperationException` for "not found" / inconsistent state (`?? throw new InvalidOperationException("Recipe not found.")`).
- `UnauthorizedAccessException` for ownership / permission failures (`throw new UnauthorizedAccessException("You do not own this cookbook.")`).
- `FormatException` for malformed user input parsing (`src/CookBot.Application/Services/RecipeFormatParser.cs:38`).
- `HttpRequestException` wrapping upstream API errors with the response body (`src/CookBot.Infrastructure/AI/AnthropicAiService.cs:70`).

Pattern with the null-coalescing throw operator is preferred over explicit `if (x == null) throw`:

```csharp
var cookbook = await _cookbookRepo.GetByIdAsync(cookbookId)
    ?? throw new InvalidOperationException("Cookbook not found.");

if (cookbook.UserId != userId)
    throw new UnauthorizedAccessException("You do not own this cookbook.");
```

`src/CookBot.Application/Services/RecipeService.cs:28-32`.

**UI-layer error handling.** Blazor pages catch exceptions at the call site and surface them via MudBlazor `ISnackbar`:

```csharp
try { ... }
catch (Exception ex)
{
    Snackbar.Add($"Import failed: {ex.Message}", Severity.Error);
}
```

`src/CookBot.Web/Components/Pages/ImportCookbookDialog.razor:62-63`. Severity choices: `Success`, `Info`, `Warning`, `Error`.

**Silent fallbacks.** A few intentional `try/catch` blocks swallow exceptions where they represent expected failure (e.g. `TestConnectionAsync` returning `false` on any error in `AnthropicAiService.cs:140-143`, JSON parse errors during SSE streaming in `AnthropicAiService.cs:116-119`). Comment the intent when doing this.

## Async Patterns

- All I/O is async. Service methods returning data are `async Task<T>` / `async Task`.
- Use `await` directly; do not use `.Result` or `.Wait()`.
- One-line async methods use expression-bodied form:
  ```csharp
  public async Task<IReadOnlyList<Cookbook>> GetUserCookbooksAsync(int userId) =>
      await _cookbookRepo.FindAsync(c => c.UserId == userId);
  ```
  `src/CookBot.Application/Services/CookbookService.cs:15-16`.
- Streaming AI responses use `IAsyncEnumerable<string>` with `yield return` (`AnthropicAiService.StreamMessageAsync`).
- The application is single-process Blazor Server; `ConfigureAwait(false)` is not used.

## Dependency Injection

DI is the standard ASP.NET Core `IServiceCollection`. Each layer exposes an extension method:

- `services.AddApplication()` in `src/CookBot.Application/DependencyInjection.cs:9` — registers parsers, services.
- `services.AddInfrastructure(IConfiguration)` in `src/CookBot.Infrastructure/DependencyInjection.cs:15` — registers `DbContext`, generic `IRepository<>`, `IAiService`, then calls `AddApplication()`.
- The web entry point composes everything in `src/CookBot.Web/Program.cs`.

Lifetimes:
- `Singleton` for stateless pure helpers exposed through interfaces (`IRecipeFormatParser`, `IUnitConverter`).
- `Scoped` for everything that touches `DbContext` or per-request state (all `Service` classes, `IAiService`, `CurrentUserService`, repositories).
- `Transient` is not used.

The generic repository is registered open-generic: `services.AddScoped(typeof(IRepository<>), typeof(Repository<>));` — consume it as `IRepository<Recipe>`, `IRepository<Cookbook>`, etc.

Configuration binding: `builder.Services.Configure<CookBotSettings>(builder.Configuration.GetSection("CookBot"));` then inject `IOptions<CookBotSettings>` (see `AnthropicAiService` constructor).

## Logging

There is no logging framework in use anywhere in `src/`. There are zero references to `ILogger`, `Microsoft.Extensions.Logging`, `Console.WriteLine`, or `Debug.*` in production code. User-facing feedback is delivered via MudBlazor `ISnackbar` calls in Razor pages. When adding diagnostics, prefer:

1. Surface failures to the user via `Snackbar.Add(message, Severity.Error)`.
2. If true server-side logging is needed later, inject `ILogger<T>` from `Microsoft.Extensions.Logging` — do not introduce `Console.WriteLine` in services.

## Comments and Documentation

- XML doc comments (`/// <summary>...`) are used sparingly — only on non-obvious public members and on classes whose intent is not clear from the name. Examples: `CookBotSettings` properties (`src/CookBot.Application/DTOs/CookBotSettings.cs:7-17`), `User.IsCookBotAdmin`, `PantryAiImport.UnmeasuredUnit`, `PantryAiPopulationService.BuildSystemPrompt`.
- Inline `//` comments explain *why* (e.g. "Skip extended-thinking payloads; pantry import expects only the visible assistant reply." in `AnthropicAiService.cs:168`), not *what*.
- Never narrate temporal history (no "previously this did X" comments).
- The codebase is comfortable with self-documenting names + targeted XML on public surface area; do not add boilerplate XML to every member.

## String Handling

- Use raw string literals (`"""..."""`) for multi-line prompts and embedded JSON in tests and prompt builders (`PantryAiPopulationServiceTests.cs:11`, `PantryAiPopulationService.BuildSystemPrompt`).
- Use `$"..."` interpolation for short composed strings.
- Use `StringBuilder` for hot-path concatenation (`AnthropicAiService.ExtractText`).
- JSON property naming for external APIs is configured globally: Anthropic uses `JsonNamingPolicy.SnakeCaseLower` (`AnthropicAiService.cs:26`); pantry import uses `PropertyNameCaseInsensitive = true` to accept both `ingredientName` and `ingredient_name`.

## Collections and LINQ

- Prefer `IReadOnlyList<T>` / `ICollection<T>` on repository / public service return types; concrete `List<T>` only where mutation is required.
- Collection-expression initialization `new()` is used heavily for inline initialization.
- LINQ method-chain style preferred over query syntax. Multi-line LINQ chains are formatted one method per line (`CookbookList.razor:125-129`).
- Use `.AsNoTracking()` for read-only EF queries (`CurrentUserService.IsCookBotAdminAsync` line 44).

## Razor / Blazor Conventions

Razor pages live in `src/CookBot.Web/Components/Pages/` and follow this header pattern:

```razor
@page "/cookbooks"
@inject CurrentUserService UserService
@inject CookBot.Infrastructure.Data.CookBotDbContext DbContext
@inject IDialogService DialogService
@inject ISnackbar Snackbar
@inject NavigationManager Navigation
@rendermode InteractiveServer

<PageTitle>CookBot - Cookbooks</PageTitle>
```

`src/CookBot.Web/Components/Pages/CookbookList.razor:1-9`.

- `@code { ... }` block at the bottom holds private state (`_camelCase` fields) and event handlers.
- Authenticated UI is wrapped in a `<UserGuard>` component.
- Snackbar is the standard channel for user feedback (success/error/info).
- Dialogs are `*Dialog.razor` components launched via `IDialogService.ShowAsync<TDialog>(...)`.
- MudBlazor (`MudButton`, `MudCard`, `MudGrid`, etc.) is the UI toolkit; do not mix in Bootstrap or other CSS frameworks.

## Module Design

- One service per file. Avoid grouping unrelated services in one file.
- Public surface area is intentional — keep helpers private/internal unless tests or other layers need them.
- Service methods that need to be unit-tested without DB plumbing are exposed as `public static` (e.g. `PantryAiPopulationService.ExtractJsonArray`, `RecipeCookingAiContext.ToParsedRecipe`, `RecipeScalingService.ScaleAmount`). This is a deliberate testability pattern — keep pure logic static when feasible.
- No barrel/`index` files.

---

*Convention analysis: 2026-04-25*
