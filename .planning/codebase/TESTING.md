# Testing Patterns

**Analysis Date:** 2026-04-25

## Test Framework

**Runner:**
- xUnit 2.9.2
- Test SDK: `Microsoft.NET.Test.Sdk` 17.12.0
- Visual Studio runner: `xunit.runner.visualstudio` 2.8.2
- Coverage collector: `coverlet.collector` 6.0.2
- Project file: `tests/CookBot.Tests/CookBot.Tests.csproj`

**Assertion library:** xUnit's built-in `Xunit.Assert` (no FluentAssertions, Shouldly, etc.).

**Global usings:** The test csproj registers `<Using Include="Xunit" />` (lines 18-20) so test files do not need an explicit `using Xunit;`.

**Run commands:**

```bash
dotnet test                                  # Run all tests in the solution
dotnet test tests/CookBot.Tests              # Run only the test project
dotnet test --collect:"XPlat Code Coverage"  # Collect coverage via coverlet
dotnet test --filter FullyQualifiedName~RecipeScalingServiceTests   # Filter by class
```

The repo's `run.sh` only starts the web app (`dotnet run --project src/CookBot.Web`); there is no dedicated test script.

## Test Project Structure

The single test project sits next to `src/` per the standard `src/` + `tests/` layout enforced by `FreelovesCookBot.sln`:

```
tests/
└── CookBot.Tests/
    ├── CookBot.Tests.csproj
    ├── UnitTest1.cs                         # Empty placeholder from `dotnet new xunit`
    ├── DTOs/
    │   └── CookBotSettingsTests.cs
    ├── Entities/
    │   └── UserProfileTests.cs
    └── Services/
        ├── FractionFormatterTests.cs
        ├── IngredientRefDetectionServiceTests.cs
        ├── OwnershipTests.cs
        ├── PantryAiPopulationServiceTests.cs
        ├── RecipeAccessExtensionsTests.cs
        ├── RecipeCookingAiContextTests.cs
        ├── RecipeFormatParserTests.cs
        ├── RecipeScalingServiceTests.cs
        ├── RecipeStepTextFormatterTests.cs
        ├── TimerDetectionServiceTests.cs
        ├── UnitConversionServiceTests.cs
        └── UnitParserTests.cs
```

Subdirectory layout under `tests/CookBot.Tests/` mirrors the *kind* of object under test (`Services/`, `DTOs/`, `Entities/`), not the namespace path of the source. Namespaces follow the folder: `CookBot.Tests.Services`, `CookBot.Tests.DTOs`, `CookBot.Tests.Entities`.

The test project references the three non-Web layers it needs to exercise (see `tests/CookBot.Tests/CookBot.Tests.csproj:22-26`):

- `CookBot.Domain`
- `CookBot.Application`
- `CookBot.Infrastructure`

`CookBot.Web` is not referenced by the test project — Razor pages and web-layer services (`CurrentUserService`, `CookbookPdfService`, etc.) are not unit tested.

## Test File Naming

- One test class per source class. File name is `{ClassUnderTest}Tests.cs` (e.g. `RecipeScalingServiceTests.cs` tests `RecipeScalingService`).
- The empty `tests/CookBot.Tests/UnitTest1.cs` is a holdover from the xUnit project template and should be deleted or replaced when adding new tests; do not add tests to it.

## Test Class Structure

Standard pattern: `public` class, named `{Subject}Tests`, with methods marked `[Fact]` or `[Theory]`. No base class, no constructor unless setup is needed.

```csharp
using CookBot.Application.Services;

namespace CookBot.Tests.Services;

public class FractionFormatterTests
{
    [Theory]
    [InlineData(1.0, "1")]
    [InlineData(0.5, "1/2")]
    [InlineData(1.5, "1 1/2")]
    public void Format_ReturnsReadableFraction(double value, string expected)
    {
        Assert.Equal(expected, FractionFormatter.Format(value));
    }

    [Fact]
    public void Format_OddValue_ReturnsDecimal()
    {
        var result = FractionFormatter.Format(1.137);
        Assert.Equal("1.14", result);
    }
}
```

`tests/CookBot.Tests/Services/FractionFormatterTests.cs`.

**Test method naming:** `MethodOrFeature_Scenario_ExpectedResult` with underscores. Examples observed:
- `Format_ReturnsReadableFraction`
- `ScaleAmount_DoublesServings_DoublesAmount`
- `RecipeService_CreateAsync_ThrowsForWrongUser`
- `UserCanAccessRecipeAsync_SharedUser_ReturnsTrue`
- `ExtractJsonArray_StripsMarkdownFence`

**Arrange / Act / Assert:** Longer tests use `// Arrange`, `// Act & Assert` comments to delineate phases (`OwnershipTests.cs:27-55`). Short tests collapse arrange and act onto consecutive lines without comments.

## `[Fact]` vs `[Theory]`

- `[Fact]` for a single behavior with no parameters.
- `[Theory]` + `[InlineData(...)]` for table-driven tests over the same logic. Used in `FractionFormatterTests`, `UnitParserTests`. Prefer `[Theory]` over copy-pasting near-identical `[Fact]`s.

## Setup / Teardown for Database Tests

Tests that need EF Core use a per-test in-memory SQLite database, initialized in the constructor and disposed via `IDisposable`:

```csharp
public class RecipeAccessExtensionsTests : IDisposable
{
    private readonly CookBotDbContext _db;

    public RecipeAccessExtensionsTests()
    {
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task UserCanAccessRecipeAsync_Owner_ReturnsTrue() { ... }

    public void Dispose() => _db.Dispose();
}
```

`tests/CookBot.Tests/Services/RecipeAccessExtensionsTests.cs:7-19, 87`. Same pattern in `OwnershipTests.cs:10-22`.

Notes:
- xUnit instantiates a fresh test class per `[Fact]`, so each test gets a brand-new SQLite connection — no cross-test state leakage.
- `OpenConnection()` is required to keep the in-memory SQLite alive for the lifetime of the `DbContext`.
- `EnsureCreated()` materializes the schema directly from the EF model — migrations are not run.
- Use `:memory:` (not the file `cookbot.db` used by the running app).
- Do NOT use `Microsoft.EntityFrameworkCore.InMemory` — the chosen pattern uses SQLite in-memory because the app's queries depend on relational behavior. Stick with SQLite for new DB-backed tests.

When adding a database test, prefer composing concrete repositories (`new Repository<Recipe>(_db)`) and passing them into the service under test (`OwnershipTests.cs:37-42`).

## Mocking

There is no mocking framework (Moq, NSubstitute, FakeItEasy) referenced in the test project. The codebase prefers two patterns instead:

**1. Hand-written stubs as private nested classes.** Used when a service constructor requires an interface that the test does not exercise:

```csharp
private class StubRecipeFormatParser : IRecipeFormatParser
{
    public ParsedRecipe Parse(string rawContent) => new();
    public string Serialize(ParsedRecipe recipe) => "";
    public bool TryParse(string rawContent, out ParsedRecipe? recipe, out List<string> errors)
    {
        recipe = new ParsedRecipe();
        errors = new List<string>();
        return true;
    }
}
```

`tests/CookBot.Tests/Services/OwnershipTests.cs:154-164`. Stubs are nested inside the test class (private) and named with the `Stub` prefix.

**2. Real implementations with in-memory SQLite.** Repositories, the DB context, and services are wired up exactly as in production but pointed at SQLite `:memory:`. This integration-style approach is the default for service tests that touch persistence.

**What NOT to do:**
- Do not introduce Moq/NSubstitute without a strong reason — the project deliberately keeps test dependencies minimal.
- Do not test by reaching into private state via reflection.

**External services** (Anthropic API in `IAiService`) are not currently mocked — there are no tests that exercise `AnthropicAiService` directly. Tests for AI-adjacent code (`PantryAiPopulationServiceTests`, `RecipeCookingAiContextTests`) target the *static, deterministic* helpers (`ExtractJsonArray`, `BuildSystemPrompt`, `BuildUserMessage`, `ToParsedRecipe`) without invoking the real HTTP client. Follow this pattern: factor pure logic into `public static` helpers and unit-test those, leaving the HTTP integration uncovered or covered manually.

## Fixtures and Test Data

There are no `Fixtures/` or `TestData/` directories. Test data is built inline in each test using object initializers:

```csharp
var user1 = new User { DisplayName = "User1" };
var cookbook = new Cookbook { UserId = user1.Id, Name = "User1 Cookbook" };
var recipe = new Recipe { CookbookId = cookbook.Id, Name = "Test Recipe", Servings = 4, TagsJson = "[]" };
```

`tests/CookBot.Tests/Services/OwnershipTests.cs:28-93`.

When adding new tests with shared setup, prefer:
1. A private helper method on the test class (e.g. `private async Task<User> SeedUserAsync(string name)`).
2. Constructor-based setup that all tests in the class reuse (already used for `_db`).

Avoid xUnit's `IClassFixture<T>` / collection fixtures unless multiple test classes truly share expensive setup — none currently do.

## Assertions

Common assertion idioms in use:

- `Assert.Equal(expected, actual)`
- `Assert.True(...)` / `Assert.False(...)`
- `Assert.Null(...)` / `Assert.NotNull(...)`
- `Assert.Single(collection)`
- `Assert.Empty(collection)`
- `Assert.Contains(value, collection)` and `Assert.Contains(substring, string)`
- `Assert.DoesNotContain(...)`
- `Assert.StartsWith(...)` / `Assert.EndsWith(...)`
- `Assert.InRange(value, low, high)` for floating-point ranges (`UnitConversionServiceTests.cs:14`)
- `Assert.Contains(substring, str, StringComparison.OrdinalIgnoreCase)` for case-insensitive substring checks (`PantryAiPopulationServiceTests.cs:158`)

For exception assertions (sync and async):

```csharp
await Assert.ThrowsAsync<UnauthorizedAccessException>(
    () => service.DeleteAsync(cookbook.Id, user2.Id));
```

`tests/CookBot.Tests/Services/OwnershipTests.cs:74-75`. Use `Assert.ThrowsAsync<TException>` for async, `Assert.Throws<TException>` for sync.

## Async Tests

Async tests return `Task` and are declared `public async Task`:

```csharp
[Fact]
public async Task RecipeService_CreateAsync_ThrowsForWrongUser()
{
    ...
    await Assert.ThrowsAsync<UnauthorizedAccessException>(
        () => service.CreateAsync(cookbook.Id, user2.Id, parsed));
}
```

`tests/CookBot.Tests/Services/OwnershipTests.cs:24-55`. Never block on `Task.Result` or `.Wait()` inside a test.

## Coverage

**Configured tooling:** `coverlet.collector` 6.0.2 is installed, so `dotnet test --collect:"XPlat Code Coverage"` produces a coverage report under `tests/CookBot.Tests/TestResults/`.

**No threshold is enforced.** There is no CI check, no `coverlet.runsettings`, and no minimum coverage rule. Coverage is best-effort.

**Current scope (15 test files, ~921 total lines):**

| Layer / Area | Coverage |
|---|---|
| `CookBot.Application/Services` pure helpers (parsing, formatting, unit conversion, scaling, timer detection, ingredient ref detection, fraction formatting) | Strong unit tests |
| `CookBot.Application/Services` AI-adjacent prompt builders + JSON extraction | Covered via static-method tests in `PantryAiPopulationServiceTests`, `RecipeCookingAiContextTests` |
| `CookBot.Application/Services` ownership / authorization (`CookbookService`, `RecipeService`) | Covered by `OwnershipTests` using SQLite in-memory |
| `CookBot.Infrastructure/Data/RecipeAccessExtensions` | Covered by `RecipeAccessExtensionsTests` |
| `CookBot.Domain/Entities` defaults | One smoke test (`UserProfileTests`) |
| `CookBot.Application/DTOs` defaults | One smoke test (`CookBotSettingsTests`) |

**Not covered:**
- `CookBot.Web` — Razor pages, dialogs, `CurrentUserService`, `CookbookPdfService`, `CookbookTransferService`, `AiApiKeyResolutionService`, `AiApiKeyShareService`.
- `CookBot.Infrastructure/AI/AnthropicAiService` — no integration tests against Anthropic.
- `CookBot.Application/Services/PromptBuilderService`, `GroceryListService`, most of `PantryService`, `CookbookTransferService` paths, `IngredientResolver` (only used indirectly).
- EF migrations (not typically tested).

When adding new tests, fill gaps in `Application/Services` first (highest leverage, no infrastructure cost), then service classes that need DB-backed coverage using the in-memory SQLite pattern above.

## Test Types

**Unit tests:** The vast majority. Target a single class or static helper with no I/O.

**Service-level integration tests:** `OwnershipTests` and `RecipeAccessExtensionsTests` exercise services + repositories + EF Core against in-memory SQLite. They are still fast (millisecond range) and run with the rest of `dotnet test`.

**E2E / browser tests:** None. Blazor Server pages are not exercised by automated tests.

## Common Patterns to Follow

**Adding a new pure-logic test:**

```csharp
using CookBot.Application.Services;

namespace CookBot.Tests.Services;

public class MyHelperTests
{
    [Fact]
    public void Method_Scenario_ExpectedResult()
    {
        var result = MyHelper.Method(input);
        Assert.Equal(expected, result);
    }
}
```

**Adding a new database-backed test:** Copy the `IDisposable` + SQLite pattern from `RecipeAccessExtensionsTests.cs:7-19` verbatim.

**Adding a new authorization test:** Follow the layout of `OwnershipTests.cs` — seed two users, seed the resource owned by user 1, and assert that user 2 receives `UnauthorizedAccessException`.

---

*Testing analysis: 2026-04-25*
