# Phase 10: QOL, Polish & Consumer Surfaces — Pattern Map

**Mapped:** 2026-05-16
**Files analyzed:** 30 (10 created, 19 modified, 1 deleted-by-replacement) + 12 analogs deeply read
**Analogs found:** 30 / 30 (every new/modified file has a strong existing template)

## File Classification

### Files CREATED (10)

| File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `src/CookBot.Application/Services/IPantryMatchService.cs` + `PantryMatchService.cs` | application-service | CRUD-read + scoring | `src/CookBot.Web/Services/RecipeMadeService.cs` (interface+impl pair, EF `.AsNoTracking()` reads, `CancellationToken ct`) | exact (cross-layer — see Layering Note 1) |
| `src/CookBot.Application/DTOs/PantryMatchOptions.cs` | options DTO | configuration binding | `src/CookBot.Application/DTOs/CookBotSettings.cs` + `AiPricingEntry.cs` (IOptions-bound POCO with defaults baked into property initializers) | exact |
| `src/CookBot.Application/DTOs/PantryMatchResult.cs` | result DTO | request-response (record) | `src/CookBot.Web/Components/Pages/Home.razor.cs` line 470 `HomePantryMatch` (sealed record with positional ctor) | exact |
| `src/CookBot.Web/Components/Pages/RawRecipeEditorDialog.razor` | dialog | request-response | `src/CookBot.Web/Components/Pages/SaveRecipeDialog.razor` (CbDialog via CbDialogHost, `[CascadingParameter] CbDialogInstance`, parser + Toast.Show) | exact |
| `src/CookBot.Web/Services/ICbTopBarService.cs` + `CbTopBarService.cs` | web-service | pub-sub (event-driven) | `src/CookBot.Web/Services/CbToastService.cs` (interface+sealed-internal-impl with `event Action<T>? OnXxx`) and `CbDialogService.cs` (DI + host-subscribed) | exact |
| `src/CookBot.Web/wwwroot/js/prompt-editor-insert.js` | JS interop helper | request-response | `src/CookBot.Web/wwwroot/js/recipe-chip-composer.js` (`window.<Namespace> = {...}` shape, selection/range API) | exact |
| `tests/CookBot.Tests/Services/PantryMatchServiceTests.cs` | unit-test | fixture-driven matrix | `tests/CookBot.Tests/Services/RecipePhotoUrlValidatorTests.cs` (Theory + InlineData scoring matrix) + `tests/CookBot.Tests/Services/OwnershipTests.cs` (in-memory SQLite DbContext for relational tests) | exact |
| `tests/CookBot.Tests/Services/GroceryListServiceTests.cs` | unit-test | CRUD + DbContext | `tests/CookBot.Tests/Services/OwnershipTests.cs` (`UseSqlite("DataSource=:memory:")` + `OpenConnection()` + `EnsureCreated()` + `IDisposable`) | exact |
| `tests/CookBot.Tests/Services/PromptBuilderServiceNullFallbackTests.cs` | unit-test | pure-function | `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` (uses `TestHost.GetPromptBuilderService()` + `TestHost.MakeProfile()`) | exact |
| `tests/CookBot.Tests/Services/CbTopBarServiceTests.cs` | unit-test | event-driven | `tests/CookBot.Tests/Services/FractionFormatterTests.cs` (Fact + Assert.Equal — service is pure C#, no DbContext) | role-match |

### Files MODIFIED (19)

| File | Role | Data Flow | Closest Analog (for the new code) | Match |
|---|---|---|---|---|
| `src/CookBot.Web/Components/Pages/Home.razor.cs` | page code-behind | injection swap + JS interop | self (lines 297-339 replaced); `OnAfterRenderAsync` JS hook mirrors lines 134-135 `CookbotSession.readInProgress` | exact |
| `src/CookBot.Web/Components/Pages/AiChat.razor` | page | dialog hop chain | `AiChat.razor:744-765` (Parser.TryParse → CbDialogService.ShowAsync<SaveRecipeDialog>) — mirror the success path | exact |
| `src/CookBot.Web/Components/Pages/EditProfile.razor` | page | CRUD + toggles | `EditProfile.razor:42-91` (CbCard + CbEyebrow + CbInput/CbToggle/CbButton pattern) | exact |
| `src/CookBot.Web/Components/Pages/PantryView.razor` | page | event handler | inside the same file line 361-367 (Trash button @onclick wiring; mirror its shape for Cart) | exact |
| `src/CookBot.Web/Components/Pages/RecipeView.razor` | page | TopBar slot integration | `RecipeView.razor:38-44` (existing inline action row to migrate); subscribe pattern from `CbToastHost.razor:30-33` | role-match |
| `src/CookBot.Web/Components/Pages/RecipeEditor.razor` | page | TopBar slot + CbSelect | self lines 38-61 (inline action row); `SaveRecipeDialog.razor:11-16` (CbSelect<int> bound to int) | exact |
| `src/CookBot.Web/Components/Layout/MainLayout.razor` | layout | scoped-service subscription | `CbToastHost.razor:30-33,77-82` (subscribe in OnInit, unsubscribe in Dispose) | role-match (layout, not host) |
| `src/CookBot.Web/Components/Layout/TopBar.razor` | layout | passthrough (no change) | self line 80 `[Parameter] RenderFragment? RightSlot` (already exists; just needs to be fed) | exact |
| `src/CookBot.Web/Components/Atoms/Icon.razor` | atom | static dispatch | self lines 49 + 89 (Sun constant + path); Moon is one-line addition in each section | exact |
| `src/CookBot.Application/Services/PromptBuilderService.cs` | application-service | template selection | self line 39 (DefaultTemplate constant → null-fallback ternary); `ResolveTemplate` already accepts `template` parameter (line 42) | exact |
| `src/CookBot.Application/Services/GroceryListService.cs` | application-service | CRUD-write | self lines 20-60 (`GenerateFromRecipeAsync` shape — build list, AddAsync via `_groceryRepo`) | exact |
| `src/CookBot.Application/Services/RecipeService.cs` | application-service | CRUD-update + authz | self lines 139-148 + `CookbookService.UserCanAccessCookbookAsync` style (already imported in RecipeAccessExtensions) | exact |
| `src/CookBot.Application/DependencyInjection.cs` | composition root | DI registration | self lines 15-30 (`AddScoped` for services, `AddSingleton` for stateless) + `Program.cs:61` (`services.Configure<T>(config.GetSection("CookBot:..."))`) | exact |
| `src/CookBot.Web/Program.cs` | composition root | DI registration | self lines 35-36 (`AddScoped<ICbDialogService, CbDialogService>()`) | exact |
| `src/CookBot.Web/appsettings.json` | config | static config | self lines 17-30 (`CookBot:AiPricing` block — same nesting depth and pattern) | exact |
| `src/CookBot.Web/wwwroot/js/cooking-session-state.js` | JS interop | timing + pagehide | self lines 65-107 (existing CookbotSession module to extend); pagehide listener is new but trivial | exact |
| `src/CookBot.Web/wwwroot/js/cookbot-shell.js` | JS bootstrap | localStorage + DOM | self lines 35-50 `applyDefaults()` (read localStorage, set data-* attribute on `<html>`) | exact |
| `src/CookBot.Web/wwwroot/css/cookbot-design.css` | stylesheet | media query | self lines 53-68 (`[data-density="compact"]` + `[data-accent="terracotta"]` — selectors with rule blocks) | role-match |
| `README.md` | docs | addendum only | n/a — non-code | — |

### Files DELETED

None. `Home.razor.cs` lines 297-339 (`BuildPantryMatchesAsync`) become a thin call into the new `IPantryMatchService` (or are removed if Home injects the service directly).

---

## Layering Notes (read FIRST — these are decisions the planner must lock)

### Layering Note 1 — `IPantryMatchService` depends on `IRecipeMadeService` (Web-layer → Application-layer inversion)

`PantryMatchService` lives in `CookBot.Application/Services/` per D-46 + canonical_refs. But it needs `IRecipeMadeService.GetLastCookAsync` (currently in `CookBot.Web/Services/`). The Application project **cannot reference Web**. Two paths (canonical_refs §"Reusable Assets" already calls this out and recommends path A):

**Path A (recommended):** Move the `IRecipeMadeService` **interface** to `src/CookBot.Application/Services/IRecipeMadeService.cs`. Keep the implementation `RecipeMadeService` in `CookBot.Web/Services/` (it consumes `CookBotDbContext` directly, which is Infrastructure-but-registered-by-Web). DI registration line in `Program.cs:60` is unchanged: `builder.Services.AddScoped<IRecipeMadeService, RecipeMadeService>()`. Application-layer code only sees the interface.

**Path B:** Move PantryMatchService to `CookBot.Web/Services/`. Cheaper (zero file moves) but breaks the canonical_refs "Source files this phase creates" path. Planner picks; recommendation per canonical_refs is **Path A**.

### Layering Note 2 — `ICbTopBarService.LocationChanged` subscription is a first-of-kind pattern

No existing Web-layer scoped service subscribes to `NavigationManager.LocationChanged` in its constructor. The closest analog is **CbToastHost** (subscribes to an event-raising service in `OnInitialized`, unsubscribes in `Dispose`). For `CbTopBarService` itself to subscribe to NavigationManager, it needs `IDisposable` and the unsubscription must happen on circuit disposal. The pattern is:

```csharp
internal sealed class CbTopBarService : ICbTopBarService, IDisposable
{
    private readonly NavigationManager _nav;
    public RenderFragment? RightSlot { get; private set; }
    public event Action? OnChanged;

    public CbTopBarService(NavigationManager nav)
    {
        _nav = nav;
        _nav.LocationChanged += OnLocationChanged;
    }

    public void SetRightSlot(RenderFragment? content)
    {
        RightSlot = content;
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        if (RightSlot is null) return;
        RightSlot = null;
        OnChanged?.Invoke();
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => Clear();

    public void Dispose() => _nav.LocationChanged -= OnLocationChanged;
}
```

Blazor disposes Scoped services with the circuit, so `Dispose` does fire. This is the first service in the project to register a NavigationManager handler in its ctor — flag for the planner so the Dispose contract is explicit in the plan body.

### Layering Note 3 — `GroceryListItem.IsCompleted` does not exist

CONTEXT D-supplement says "appends a `GroceryListItem` with `IsCompleted = false`". The actual column name in `src/CookBot.Domain/Entities/GroceryListItem.cs:10` is **`IsPurchased`** (default `false` — no explicit set needed). Planner must reconcile: use `IsPurchased = false` (or simply omit since default-false). Flag for the plan body.

---

## Pattern Assignments

### Application services — `IPantryMatchService` + `PantryMatchService`

**Analog:** `src/CookBot.Web/Services/RecipeMadeService.cs` (best match — same interface+impl + EF read shape + `CancellationToken ct` last parameter). Constructor-injection pattern from `src/CookBot.Application/Services/RecipeService.cs:7-31`.

**Interface + impl pair pattern** (`RecipeMadeService.cs:12-27`):
```csharp
public interface IRecipeMadeService
{
    Task<RecipeMade> LogMadeAsync(int recipeId, int userId, string? notes = null, CancellationToken ct = default);
    Task<RecipeMade?> GetLastCookAsync(int recipeId, int userId, CancellationToken ct = default);
}

public class RecipeMadeService : IRecipeMadeService
{
    private readonly CookBotDbContext _db;

    public RecipeMadeService(CookBotDbContext db) { _db = db; }
    // ...
}
```

**Constructor injection of repositories + options** (`RecipeService.cs:7-31` + `AnthropicAiService` IOptions pattern):
```csharp
public class PantryMatchService : IPantryMatchService
{
    private readonly IRepository<Recipe> _recipeRepo;       // existing IRepository<T>
    private readonly IRepository<PantryItem> _pantryRepo;
    private readonly IRecipeMadeService _recipeMade;
    private readonly PantryMatchOptions _opts;

    public PantryMatchService(
        IRepository<Recipe> recipeRepo,
        IRepository<PantryItem> pantryRepo,
        IRecipeMadeService recipeMade,
        Microsoft.Extensions.Options.IOptions<PantryMatchOptions> opts)
    {
        _recipeRepo = recipeRepo;
        _pantryRepo = pantryRepo;
        _recipeMade = recipeMade;
        _opts = opts.Value;
    }
}
```

**EF `.AsNoTracking()` + `.Include().ThenInclude()` read pattern** (currently in `Home.razor.cs:303-308` — move into the service verbatim):
```csharp
var recipes = await _db.Recipes
    .AsNoTracking()
    .Where(r => r.Cookbook.UserId == userId
                || r.Cookbook.Shares.Any(s => s.SharedWithUserId == userId))
    .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.Ingredient)
    .Include(r => r.Tags)  // NEW — needed for dietary filter (Phase 8 D-26 relational tags)
    .ToListAsync(ct);
```

Note: `PantryMatchService` does NOT inject `CookBotDbContext` directly (Application layer cannot reference Infrastructure). It uses `IRepository<Recipe>.FindAsync(...)` for the predicate, then iterates in memory. If the planner concludes the dataset is too large for in-memory diet filtering, **the cleanest cross-layer escape is to inject `CookBotDbContext` directly — the project already does this in `CookbookTransferService` (Web-layer) and EF8 conventions support it from Application via the `CookBot.Infrastructure` reference chain (Web → Infrastructure → Application).** Actually, Application cannot see `CookBotDbContext`. Two options:

1. **Use `IRepository<Recipe>` + `IRepository<RecipeTag>` + `IRepository<RecipeIngredient>` + filter in memory** — small dataset OK; risks O(n) memory cost for users with thousands of recipes.
2. **Inject a thin `IRecipeQueryService` abstraction defined in Application, implemented in Web** (consistent with the Layering Note 1 pattern for IRecipeMadeService).

Recommendation: Option 2 — same fix-shape as Layering Note 1. Planner picks.

**Authz pattern** (D-04 PRAGMATIC — owned OR shared cookbook): `Cookbook.UserId == userId OR Cookbook.Shares.Any(s => s.SharedWithUserId == userId)` — already canonical in `Home.razor.cs:196-198`, `RecipeMadeService.cs:75-77`, `EditProfile.razor` cookbook lookup. Reuse verbatim.

**Linear-decay scoring formula** (D-44):
```csharp
double Score(int matchedCount, int totalCount, DateTime? lastCookedUtc)
{
    var coverage = (double)matchedCount / totalCount;
    if (lastCookedUtc is null)
        return coverage;
    var daysSince = (DateTime.UtcNow - lastCookedUtc.Value).TotalDays;
    var penalty = _opts.RecencyPenaltyWeight * Math.Exp(-daysSince / _opts.RecencyHalfLifeDays);
    return coverage - penalty;
}
```

**Stable sort** (D-44, PITFALL H8):
```csharp
return survived
    .Select(r => (r, Score: Score(matchedFor(r), r.RecipeIngredients.Count, lastCookFor(r))))
    .Where(t => (double)matchedFor(t.r) / t.r.RecipeIngredients.Count >= _opts.MinCoverageRatio)
    .OrderByDescending(t => t.Score)
    .ThenBy(t => t.r.Id)
    .ThenBy(t => t.r.Name, StringComparer.OrdinalIgnoreCase)
    .Take(_opts.ResultCount)
    .Select(t => Project(t.r, t.Score))
    .ToList();
```

**Diet → category map** (D-47):
```csharp
private static readonly Dictionary<string, IngredientCategory[]> DietExcludeMap =
    new(StringComparer.OrdinalIgnoreCase)
    {
        ["vegan"]       = new[] { IngredientCategory.Meat, IngredientCategory.Seafood, IngredientCategory.Dairy },
        ["vegetarian"]  = new[] { IngredientCategory.Meat, IngredientCategory.Seafood },
        ["dairy-free"]  = new[] { IngredientCategory.Dairy },
        ["gluten-free"] = new[] { IngredientCategory.Grains, IngredientCategory.Bakery },
        // planner enumerates the full map in the plan body
    };
```

(NOTE: the actual `IngredientCategory` enum has Produce/Dairy/Meat/Seafood/Bakery/Pantry/Frozen/Spices/Condiments/Beverages/Grains/Canned/Snacks/Other — there is NO `Poultry` / `Fish` / `Eggs` category, so the CONTEXT D-47 example must be narrowed. Planner enumerates against the real enum in the plan body.)

**Error handling:** Service does not throw on "no matches" — returns empty list (Home renders the empty-state copy from `HeroBody()`).

---

### Application DTOs — `PantryMatchOptions` + `PantryMatchResult`

**Analog for `PantryMatchOptions`:** `src/CookBot.Application/DTOs/CookBotSettings.cs:5-46` + `AiPricingEntry.cs:16-20`. IOptions-bound POCO with defaults in property initializers.

```csharp
// src/CookBot.Application/DTOs/PantryMatchOptions.cs
namespace CookBot.Application.DTOs;

/// <summary>
/// Phase 10 / Plan 10-XX / D-46 — operator-tunable pantry-match knobs bound from
/// <c>CookBot:PantryMatch</c>. Defaults apply when the section is missing (Phase 9
/// PROD-19 env-var override pattern carries forward).
/// </summary>
public sealed class PantryMatchOptions
{
    /// <summary>D-44: linear-decay coefficient on the recency penalty term.</summary>
    public double RecencyPenaltyWeight { get; set; } = 0.3;

    /// <summary>D-44: half-life of the exponential decay, in days.</summary>
    public double RecencyHalfLifeDays { get; set; } = 7.0;

    /// <summary>Stub baseline preserved from Home.razor.cs:319.</summary>
    public double MinCoverageRatio { get; set; } = 0.6;

    /// <summary>Number of recipes returned to the Home hero.</summary>
    public int ResultCount { get; set; } = 3;
}
```

**Analog for `PantryMatchResult`:** `Home.razor.cs:470-477` `HomePantryMatch` sealed record. Match its shape so the Home swap is mechanical.

```csharp
// src/CookBot.Application/DTOs/PantryMatchResult.cs
namespace CookBot.Application.DTOs;

public sealed record PantryMatchResult(
    int RecipeId,
    string RecipeName,
    int MatchedCount,
    int TotalCount,
    double Score,
    string? PhotoUrl,
    string? FirstMissingIngredientName);
```

`Home.razor.cs` keeps `HomePantryMatch` as a view-layer projection (drives the `MetaLine` + badge shape); the projection is `_pantryMatches = (await PantryMatchService.GetMatchesAsync(...)).Select(r => new HomePantryMatch(...)).ToList();`.

---

### Application service modification — `PromptBuilderService.BuildSystemPrompt` null-fallback (D-52)

**Analog:** the existing method shape at `PromptBuilderService.cs:37-40` — one-line return calling `ResolveTemplate(DefaultTemplate, profile, pantryItems)`. The change is a single statement substitution.

```csharp
// BEFORE (PromptBuilderService.cs:37-40):
public string BuildSystemPrompt(UserProfile profile, IEnumerable<PantryItem>? pantryItems = null)
{
    return ResolveTemplate(DefaultTemplate, profile, pantryItems);
}

// AFTER (D-52 — corrects REQUIREMENTS QOL-06 "already loaded" claim):
public string BuildSystemPrompt(UserProfile profile, IEnumerable<PantryItem>? pantryItems = null)
{
    var template = string.IsNullOrWhiteSpace(profile.AiSystemPromptTemplate)
        ? DefaultTemplate
        : profile.AiSystemPromptTemplate;
    return ResolveTemplate(template, profile, pantryItems);
}
```

**Verify snapshot re-`verified`:** the existing `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs:11-18` uses `svc.ResolveTemplate(DefaultTemplate, profile, pantry)` directly — it does NOT call `BuildSystemPrompt`, so it is unaffected by the wiring change. **However**, `TestHost.MakeProfile()` line 61 sets `AiSystemPromptTemplate = "aisystemprompttemplate"` (non-null). If the planner adds a Verify snapshot for `BuildSystemPrompt(profile, pantry)`, it would now diff because the new override path is exercised. The clean recipe is: keep `PromptSnapshotTests.BuildSystemPrompt` calling `ResolveTemplate(DefaultTemplate, ...)` (preserves the existing baseline), and add a new test file `PromptBuilderServiceNullFallbackTests.cs` that exercises the new logic (see Tests section).

---

### Application service modification — `GroceryListService.EnsurePrimaryListAsync` + `AddItemAsync`

**Analog:** `GroceryListService.cs:20-60` — existing `GenerateFromRecipeAsync` shape (build a `GroceryList` POCO, `Items.Add(new GroceryListItem { ... })`, `await _groceryRepo.AddAsync(list)`).

```csharp
// Append below DeleteAsync (line 88-89):
public async Task<GroceryList> EnsurePrimaryListAsync(int userId)
{
    var existing = await _groceryRepo.FindAsync(g => g.UserId == userId);
    var open = existing
        .OrderByDescending(g => g.CreatedAt)
        .FirstOrDefault();
    if (open is not null)
        return open;

    var fresh = new GroceryList
    {
        UserId = userId,
        Name = "Pantry quick-add",
    };
    return await _groceryRepo.AddAsync(fresh);
}

public async Task AddItemAsync(int groceryListId, int ingredientId, double amount = 0, string unit = "")
{
    var list = await _groceryRepo.GetByIdAsync(groceryListId)
        ?? throw new InvalidOperationException("Grocery list not found.");
    list.Items.Add(new GroceryListItem
    {
        IngredientId = ingredientId,
        Amount = amount,
        Unit = unit,
        // GroceryListItem.IsPurchased defaults to false — no explicit set needed.
        // (Layering Note 3: CONTEXT D-supplement says "IsCompleted = false"; the actual column is IsPurchased.)
    });
    await _groceryRepo.UpdateAsync(list);
}
```

**Authz note:** `GenerateFromRecipeAsync` does NOT check authz today — it trusts the caller's `userId`. `AddItemAsync` follows that convention (PantryView already gates pantry access via PantryService). Planner may add a defensive `list.UserId == userId` check if the API signature includes `userId` — but this would diverge from `GenerateFromRecipeAsync`. Recommendation: do NOT add the check, mirror the existing pattern; flag in plan body.

**Tests analog:** `tests/CookBot.Tests/Services/OwnershipTests.cs:11-23` (in-memory SQLite `DbContext` + `EnsureCreated()` + `IDisposable`).

---

### Application service modification — `RecipeService.UpdateAsync` cookbook reparenting (POLISH-01)

**Analog:** `RecipeService.cs:139-148` (existing UpdateAsync signature + cookbook-ownership check). Already imports `_cookbookRepo` (line 13) and already has the cookbook lookup + UserId check at lines 144-148. The new `int? newCookbookId` parameter is a small addition.

```csharp
// BEFORE (RecipeService.cs:139):
public async Task<Recipe> UpdateAsync(int recipeId, int userId, ParsedRecipe parsed)

// AFTER (POLISH-01):
public async Task<Recipe> UpdateAsync(int recipeId, int userId, ParsedRecipe parsed, int? newCookbookId = null)
```

**Reparent logic** (insert AFTER the existing ownership check at line 148, BEFORE the `recipe.Name = parsed.Name` line):
```csharp
if (newCookbookId.HasValue && newCookbookId.Value != recipe.CookbookId)
{
    var destination = await _cookbookRepo.GetByIdAsync(newCookbookId.Value)
        ?? throw new InvalidOperationException("Destination cookbook not found.");
    if (destination.UserId != userId)
        throw new UnauthorizedAccessException("You do not own the destination cookbook.");
    recipe.CookbookId = newCookbookId.Value;
}
```

(`db.UserCanAccessCookbookAsync` mentioned in canonical_refs does NOT exist as a method; the project uses inline `cookbook.UserId == userId` checks. The pattern above is the canonical inline check from `RecipeService.cs:38-39` and `CookbookService.DeleteAsync`. Planner uses the inline check.)

---

### Web service — `ICbTopBarService` + `CbTopBarService`

**Analog:** `src/CookBot.Web/Services/CbToastService.cs` (full file — 28 lines). Same interface+sealed-internal-impl + event-based DI shape. **Add `IDisposable` + NavigationManager subscription** (Layering Note 2).

**Interface shape** (CbToastService.cs:12-16):
```csharp
public interface ICbTopBarService
{
    RenderFragment? RightSlot { get; }
    event Action? OnChanged;
    void SetRightSlot(RenderFragment? content);
    void Clear();
}
```

**Sealed-internal-impl shape** (CbToastService.cs:18-27 + add Dispose):
```csharp
internal sealed class CbTopBarService : ICbTopBarService, IDisposable
{
    private readonly NavigationManager _nav;
    public RenderFragment? RightSlot { get; private set; }
    public event Action? OnChanged;

    public CbTopBarService(NavigationManager nav)
    {
        _nav = nav;
        _nav.LocationChanged += HandleLocationChanged;
    }

    public void SetRightSlot(RenderFragment? content)
    {
        RightSlot = content;
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        if (RightSlot is null) return;
        RightSlot = null;
        OnChanged?.Invoke();
    }

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs e) => Clear();

    public void Dispose() => _nav.LocationChanged -= HandleLocationChanged;
}
```

**DI registration** (mirror `Program.cs:35`):
```csharp
builder.Services.AddScoped<ICbTopBarService, CbTopBarService>();
```

**MainLayout subscription pattern** (analog: `CbToastHost.razor:30-33,77-82`):
```razor
@inject ICbTopBarService TopBarService
@implements IDisposable
...
<TopBar IsDarkMode="_isDarkMode" ... RightSlot="@TopBarService.RightSlot" />
@code {
    protected override void OnInitialized()
    {
        TopBarService.OnChanged += HandleSlotChanged;
    }

    private void HandleSlotChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        TopBarService.OnChanged -= HandleSlotChanged;
    }
}
```

**Page consumer pattern** (analog: `Home.razor.cs:106-123` for `OnAfterRenderAsync` shape; new pattern for `OnInitializedAsync` SetRightSlot):
```csharp
protected override void OnInitialized()
{
    TopBarService.SetRightSlot(builder =>
    {
        builder.OpenComponent<CbButton>(0);
        builder.AddAttribute(1, "Variant", CbButton.CbButtonVariant.Ghost);
        builder.AddAttribute(2, "OnClick", EventCallback.Factory.Create(this, OpenShareDialog));
        builder.AddAttribute(3, "ChildContent", (RenderFragment)((b) => b.AddContent(4, "Share")));
        builder.CloseComponent();
        // ... etc
    });
}
```

(Alternatively, expose a Razor fragment via `RenderFragment` literal — see `Home.razor.cs:464` for the `Guid.NewGuid():N` element-id trick. Planner picks markup vs. builder API.)

**Lifecycle:** `CbTopBarService.LocationChanged` auto-clears on every URL change (D-57). Pages that want a slot must call `SetRightSlot` in `OnInitializedAsync` (re-runs on navigation because pages are re-instantiated by Blazor's `@page` router).

---

### Razor dialog — `RawRecipeEditorDialog`

**Analog:** `src/CookBot.Web/Components/Pages/SaveRecipeDialog.razor` (full file — 68 lines). Same `[CascadingParameter] CbDialogInstance` + `[Parameter]` + Parser.TryParse + DialogInstance.Close pattern.

**File header** (SaveRecipeDialog.razor:1-7):
```razor
@* Phase 10 / Plan 10-XX / QOL-04 / D-48..51: raw AI response edit-and-save dialog. *@
@inject CookBot.Domain.Interfaces.IRecipeFormatParser Parser
@inject ICbToastService Toast
@inject ICbDialogService CbDialogService
@inject CurrentUserService UserService
@inject CookBot.Infrastructure.Data.CookBotDbContext DbContext
```

**Cascading parameter + parameters** (SaveRecipeDialog.razor:29-31):
```csharp
[CascadingParameter] private CbDialogInstance? DialogInstance { get; set; }
[Parameter] public string RawJson { get; set; } = "";
```

**Debounced live validation pattern (D-50)** — new pattern, no direct codebase analog. Use a `System.Threading.Timer` reset on each `oninput`:
```csharp
private string _editedJson = "";
private bool _isValid;
private string? _validationMessage;
private System.Threading.Timer? _debounceTimer;

protected override void OnInitialized()
{
    _editedJson = RawJson;
    Validate();  // initial state
}

private async Task HandleInput(ChangeEventArgs e)
{
    _editedJson = e.Value as string ?? "";
    _debounceTimer?.Dispose();
    _debounceTimer = new System.Threading.Timer(_ =>
    {
        _ = InvokeAsync(() =>
        {
            Validate();
            StateHasChanged();
        });
    }, null, 500, System.Threading.Timeout.Infinite);
}

private void Validate()
{
    _isValid = Parser.TryParse(_editedJson, out _, out var errors);
    _validationMessage = _isValid
        ? "Valid recipe — ready to save"
        : $"Validation failed: {errors.FirstOrDefault() ?? "Unknown error"}";
}
```

**Two-dialog hop on success (D-51)** — mirror `AiChat.razor:744-765`:
```csharp
private async Task ParseAndSave()
{
    if (!_isValid) return;
    if (!UserService.CurrentUserId.HasValue) return;

    var cookbooks = await DbContext.Cookbooks
        .Where(c => c.UserId == UserService.CurrentUserId.Value)
        .ToListAsync();
    if (!cookbooks.Any())
    {
        Toast.Show("Create a cookbook first.", CbToastSeverity.Warning);
        return;
    }

    DialogInstance?.Close(CbDialogResult.Ok(true));

    var parameters = new CbDialogParameters
    {
        ["Cookbooks"] = cookbooks,
        ["RecipeContent"] = _editedJson,
    };
    var options = new CbDialogOptions(MaxWidth: CbDialogMaxWidth.Sm, FullWidth: true);
    await CbDialogService.ShowAsync<SaveRecipeDialog>("Save Recipe to Cookbook", parameters, options);
}
```

**Copy to clipboard (D-48)** — `AiChat.razor:716-729` is the exact pattern (`navigator.clipboard.writeText` via JSRuntime).

**AiChat.razor:769 wiring change** — replace the single Toast.Show line with:
```csharp
// D-09 fallback: parser couldn't coerce — open RawRecipeEditorDialog for manual edit (Phase 10 D-48).
var parameters = new CbDialogParameters { ["RawJson"] = rawJson };
var options = new CbDialogOptions(MaxWidth: CbDialogMaxWidth.Md, FullWidth: true);
await CbDialogService.ShowAsync<RawRecipeEditorDialog>("Edit raw AI response", parameters, options);
```

---

### Razor page modification — `EditProfile.razor` (three new cards)

**Analog:** `EditProfile.razor:42-91` (the existing "Display name" + "Account password" cards). Same `<CbCard><CbEyebrow>...</CbEyebrow><p style="...">...</p><div style="...">...</div></CbCard>` shape for each new card.

**Accent picker (QOL-05)** — CbRadio + `localStorage.setItem` + `cookbot.setAccent` (`cookbot-shell.js:10-15` already provides the function):
```razor
<CbCard>
    <CbEyebrow>Accent color</CbEyebrow>
    <div style="display:flex;gap:18px;margin-top:10px;">
        <CbRadio TValue="string" GroupName="accent" Value="@("orange")"
                 CurrentValue="@_accent" CurrentValueChanged="OnAccentChanged" Label="Default" />
        <CbRadio TValue="string" GroupName="accent" Value="@("terracotta")"
                 CurrentValue="@_accent" CurrentValueChanged="OnAccentChanged" Label="Terracotta" />
        <CbRadio TValue="string" GroupName="accent" Value="@("sage")"
                 CurrentValue="@_accent" CurrentValueChanged="OnAccentChanged" Label="Sage" />
    </div>
</CbCard>

@code {
    private string _accent = "orange";

    private async Task OnAccentChanged(string newAccent)
    {
        _accent = newAccent;
        try { await JS.InvokeVoidAsync("localStorage.setItem", "cookbot_accent", newAccent); } catch { }
        try { await JS.InvokeVoidAsync("cookbot.setAccent", newAccent); } catch { }
    }
}
```

**AI usage card (PROD-17 read surface)** — single rolling-30d card. Direct EF query analog: `AiChat.razor:687-689` cookbook query pattern.
```csharp
private long _aiInputTokens30d;
private long _aiOutputTokens30d;
private decimal _aiCost30d;

private async Task LoadAiUsageAsync(int userId)
{
    var cutoff = DateTime.UtcNow.AddDays(-30);
    var rows = DbContext.AiUsageLogs.AsNoTracking()
        .Where(r => r.KeyOwnerId == userId && !r.IsRetryAttempt && r.Timestamp >= cutoff);
    _aiInputTokens30d = await rows.SumAsync(r => (long)r.InputTokens);
    _aiOutputTokens30d = await rows.SumAsync(r => (long)r.OutputTokens);
    _aiCost30d = await rows.SumAsync(r => r.EstimatedCostUsd);
}
```

Cross-user disclosure (PITFALL M9): small footnote under the numbers — copy from canonical_refs ("Includes spending by users sharing your key"). Pricing footnote (PITFALL H10): `"Pricing as of @CookBotSettingsOptions.Value.AiPricingVerifiedDate"`.

**AI assistant instructions card (QOL-06 + QOL-07)** — CbTextarea + clickable chip row + warning CbCard:
```razor
<CbCard>
    <CbEyebrow>AI assistant instructions</CbEyebrow>
    <p style="margin:6px 0 14px 0;font-size:13px;color:var(--ink-3);">
        Customize how CookBot's AI introduces itself. Variables expand at request time.
    </p>

    @* Chip row — clickable variable tokens (D-53) *@
    <div id="@_promptTokenRowId" style="display:flex;gap:6px;flex-wrap:wrap;margin-bottom:8px;">
        @foreach (var token in PromptTokens)
        {
            <button type="button" class="cb-chip" style="cursor:pointer;border:0;"
                    @onclick="@(() => InsertToken(token))">@token</button>
        }
    </div>

    <CbTextarea Value="@_promptTemplate"
                ValueChanged="@((string? v) => _promptTemplate = v ?? "")"
                Rows="12"
                AriaLabel="Custom AI system prompt template" />

    @* Inline warning (D-55) — always visible while editing *@
    <CbCard Padding="14" Style="background:var(--accent-soft);margin-top:12px;">
        <strong style="font-size:13px;">About custom prompts</strong>
        <p style="margin:6px 0 0 0;font-size:12.5px;color:var(--ink-2);line-height:1.5;">
            Your custom template is injected verbatim into the system prompt. CookBot's
            PromptInjectionGuard wraps user-supplied <em>content</em>, but not the system
            template itself. Avoid instructions that tell the model to disregard or
            override the rest of the prompt.
        </p>
    </CbCard>

    <div style="display:flex;gap:8px;margin-top:12px;justify-content:flex-end;">
        <CbButton Variant="CbButton.CbButtonVariant.Ghost" OnClick="ConfirmResetPromptAsync">
            Reset to default
        </CbButton>
        <CbButton Variant="CbButton.CbButtonVariant.Primary" StartIcon="@Icon.Names.Save"
                  OnClick="SavePromptTemplate">Save</CbButton>
    </div>
</CbCard>

@code {
    private static readonly string[] PromptTokens =
    {
        "{{experience_level}}", "{{unit_system}}", "{{equipment}}",
        "{{dietary_preferences}}", "{{pantry}}", "{{recipe_format}}",
    };
    private string _promptTemplate = "";
    private readonly string _promptTokenRowId = $"prompt-tokens-{Guid.NewGuid():N}";
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private async Task InsertToken(string token)
    {
        // textareaElementId must be set on the underlying <textarea>; CbTextarea exposes Value/ValueChanged
        // but not ElementReference. The cleanest path is to attach an id via a parent wrapper.
        try { await JS.InvokeVoidAsync("CookbotPromptEditor.insertAtCursor", _promptTextareaId, token); } catch { }
    }
}
```

**Reset confirmation (D-54)** — CbDialog pattern; existing analog: `AdminManageUsersDialog`. Open a small confirm dialog before swapping textarea contents to `PromptBuilderService.DefaultTemplate`.

---

### Razor page modification — `PantryView.razor` cart button (POLISH-02)

**Analog:** within the same file lines 361-367 (the Trash button @onclick wiring). Mirror its shape for the Cart button at lines 354-360.

```razor
@* BEFORE (lines 354-360): *@
<button type="button"
        aria-label="@($"Add {item.Ingredient.Name} to grocery list")"
        title="Add to grocery list"
        disabled
        style="background:transparent;border:0;color:var(--ink-4);cursor:not-allowed;padding:4px;display:grid;place-items:center;">
    <Icon Name="@Icon.Names.Cart" Size="15" />
</button>

@* AFTER (POLISH-02): *@
<button type="button"
        aria-label="@($"Add {item.Ingredient.Name} to grocery list")"
        title="Add to grocery list"
        @onclick="@(() => AddToGroceryList(item))"
        style="background:transparent;border:0;color:var(--ink-3);cursor:pointer;padding:4px;display:grid;place-items:center;">
    <Icon Name="@Icon.Names.Cart" Size="15" />
</button>

@code {
    [Inject] private GroceryListService GroceryListService { get; set; } = default!;
    [Inject] private ICbToastService Toast { get; set; } = default!;

    private async Task AddToGroceryList(PantryItem item)
    {
        if (!UserService.CurrentUserId.HasValue) return;
        try
        {
            var list = await GroceryListService.EnsurePrimaryListAsync(UserService.CurrentUserId.Value);
            await GroceryListService.AddItemAsync(list.Id, item.IngredientId, item.Amount, item.Unit);
            Toast.Show($"Added {item.Ingredient.Name} to grocery list", CbToastSeverity.Success);
        }
        catch (Exception ex)
        {
            Toast.Show($"Could not add: {ex.Message}", CbToastSeverity.Error);
        }
    }
}
```

---

### Razor page modification — `RecipeView.razor` + `RecipeEditor.razor` (POLISH-04)

**Analog for both:** the existing inline-action rows at `RecipeView.razor:38-44` and `RecipeEditor.razor:38-61`. Move the markup into a `RenderFragment` parameter and feed via `TopBarService.SetRightSlot(slot)` in `OnInitialized`.

```razor
@* RecipeView.razor — replace the inline div at lines 38-44 with conditional fallback,
   then register the TopBar slot in OnInitialized. *@

@inject ICbTopBarService TopBarService
@implements IDisposable

@code {
    private RenderFragment? _topBarActions;

    protected override void OnInitialized()
    {
        _topBarActions = builder =>
        {
            // Mirror lines 38-44 contents — Edit/Share/Schedule/Cook this buttons
        };
        TopBarService.SetRightSlot(_topBarActions);
    }

    public void Dispose()
    {
        // Optional: explicit clear; LocationChanged auto-clears on navigation (D-57).
    }
}
```

Inline fallback for `<720px` (D-59) — keep the existing inline `<div>` at lines 38-44, wrap in a CSS class that the new media query hides on wide viewports. The CSS work is:
```css
/* cookbot-design.css — appended */
@media (max-width: 720px) {
    .topbar-right-slot { display: none !important; }
}
@media (min-width: 721px) {
    .recipe-actions-inline-fallback { display: none !important; }
}
```

The `<div>` at the top of `RecipeView.razor` gets `class="recipe-actions-inline-fallback"`; the corresponding TopBar wrapper gets `class="topbar-right-slot"`. (TopBar.razor line 45-46 wrapper `<div style="display:flex;align-items:center;gap:10px;">` — add `class="topbar-right-slot"` to the inner `@RightSlot` container or wrap it.)

**RecipeEditor.razor cookbook reparenting CbSelect (POLISH-01)** — analog: `SaveRecipeDialog.razor:11-16` (CbSelect<int> bound to int). Add inside the meta rail (right column lines 80+):
```razor
<CbCard>
    <CbEyebrow>Cookbook</CbEyebrow>
    <CbSelect TValue="int" Value="_selectedCookbookId" ValueChanged="@((int v) => _selectedCookbookId = v)">
        @foreach (var cb in _userCookbooks)
        {
            <CbOption TValue="int" Value="@cb.Id" Label="@cb.Name" />
        }
    </CbSelect>
</CbCard>
```

`SaveRecipe()` (existing handler) gains `await RecipeService.UpdateAsync(recipeId, userId, parsed, _selectedCookbookId)`. On a cookbook change, navigate to the destination's recipe view: `Navigation.NavigateTo($"/recipes/{recipeId}")` already triggers the routing handle.

---

### Razor layout modification — `MainLayout.razor` + Moon glyph

**Sun ↔ Moon swap (POLISH-03)** — `TopBar.razor:71` currently always shows `Icon.Names.Sun`. Change to:
```razor
<Icon Name="@(_isDarkMode ? Icon.Names.Moon : Icon.Names.Sun)" Size="16" />
```

**Icon.razor Moon constant** — `Icon.razor:49` (after `Sun`):
```csharp
public const string Moon = "moon";
```

**Icon.razor Moon path** — `Icon.razor:89` (next to Sun):
```csharp
"moon"     => "<path d=\"M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z\"/>",
```

---

### JS — `prompt-editor-insert.js`

**Analog:** `recipe-chip-composer.js:1-29` (window.<Name> = {...} shape + selection API). Same module pattern.

```js
// src/CookBot.Web/wwwroot/js/prompt-editor-insert.js
// Phase 10 / Plan 10-XX / QOL-06 / D-53 — variable-chip insertion at the textarea caret.
// Module shape mirrors recipe-chip-composer.js (window.<Name> = { ... }; no ES modules).
window.CookbotPromptEditor = {
    insertAtCursor(textareaId, token) {
        const el = document.getElementById(textareaId);
        if (!el || typeof el.selectionStart !== 'number') return false;
        const start = el.selectionStart;
        const end = el.selectionEnd;
        // setRangeText preserves undo history; direct value assignment does not.
        el.setRangeText(token, start, end, 'end');
        el.dispatchEvent(new Event('input', { bubbles: true }));
        el.focus();
        return true;
    }
};
```

**Script tag registration** — must add `<script src="js/prompt-editor-insert.js"></script>` to `src/CookBot.Web/Components/App.razor` (per STRUCTURE.md §"New JS interop"). Check that file in the plan body.

---

### JS — `cooking-session-state.js` startTickLoop (POLISH-05)

**Analog:** the existing `formatStartedAgo` function at lines 114-125 (same module, same string formatting). Add a new `setInterval`-based tick at module bottom:

```js
// Append below formatStartedAgo() (after line 125, inside the closing }):
    _tickHandles: {},  // elementId → intervalHandle

    startTickLoop(elementId, startedAtIso, durationSeconds) {
        const handle = this._tickHandles[elementId];
        if (handle) clearInterval(handle);

        const startedAtMs = Date.parse(startedAtIso);
        if (!Number.isFinite(startedAtMs)) return;

        const tick = () => {
            const el = document.getElementById(elementId);
            if (!el) {
                clearInterval(this._tickHandles[elementId]);
                delete this._tickHandles[elementId];
                return;
            }
            const elapsed = Math.floor((Date.now() - startedAtMs) / 1000);
            const remaining = Math.max(0, (durationSeconds | 0) - elapsed);
            const h = Math.floor(remaining / 3600);
            const m = Math.floor((remaining % 3600) / 60);
            const s = remaining % 60;
            const pad = (n) => String(n).padStart(2, '0');
            el.textContent = h > 0 ? `${pad(h)}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`;
            if (remaining <= 0) {
                clearInterval(this._tickHandles[elementId]);
                delete this._tickHandles[elementId];
            }
        };

        tick();
        this._tickHandles[elementId] = setInterval(tick, 1000);
    },

    stopTickLoop(elementId) {
        const handle = this._tickHandles[elementId];
        if (handle) {
            clearInterval(handle);
            delete this._tickHandles[elementId];
        }
    },
```

**pagehide teardown** (one-time at module load):
```js
// At bottom of file (outside the object literal):
window.addEventListener('pagehide', () => {
    const handles = window.CookbotSession._tickHandles || {};
    for (const id in handles) clearInterval(handles[id]);
    window.CookbotSession._tickHandles = {};
}, { once: false });
```

**Home.razor.cs `OnAfterRenderAsync` hook** — extend the existing method at lines 106-123:
```csharp
if (firstRender && _activeTimer != null)
{
    try
    {
        await JS.InvokeVoidAsync(
            "CookbotSession.startTickLoop",
            _activeTimerCountdownId,
            _activeTimer.StartedAtIso,
            _activeTimer.DurationSeconds);
    }
    catch (JSException) { }
    catch (Microsoft.JSInterop.JSDisconnectedException) { }
}
```

---

### JS — `cookbot-shell.js` accent bootstrap (QOL-05)

**Analog:** `cookbot-shell.js:35-50` `applyDefaults()` (already reads `localStorage.cookbot_density`, sets `data-density` on `<html>`). Same pattern for `cookbot_accent`. Edit `applyDefaults()`:

```js
window.cookbot.applyDefaults = function () {
    if (!document.documentElement.hasAttribute("data-accent")) {
        var accent = "orange";
        try {
            var stored = localStorage.getItem("cookbot_accent");
            if (stored === "orange" || stored === "terracotta" || stored === "sage") accent = stored;
        } catch (e) { /* ignore */ }
        document.documentElement.setAttribute("data-accent", accent);
    }
    if (!document.documentElement.hasAttribute("data-density")) {
        var density = "comfy";
        try {
            var stored = localStorage.getItem("cookbot_density");
            if (stored === "comfy" || stored === "compact") density = stored;
        } catch (e) { /* ignore */ }
        document.documentElement.setAttribute("data-density", density);
    }
};
```

`setAccent` already exists at lines 10-15; it's been waiting for a caller (EditProfile).

---

### CSS — media query for TopBar.RightSlot hide-below-720px (D-59)

**Analog:** `cookbot-design.css:53-68` (existing `[data-density="compact"]` + `[data-accent="terracotta"]` selectors — these are NOT media queries, but the file structure shows the file accepts new rules at any point). Append at end of file:

```css
@media (max-width: 720px) {
    .topbar-right-slot { display: none !important; }
}
```

---

### Configuration — `appsettings.json` PantryMatch block

**Analog:** `appsettings.json:17-30` (`CookBot:AiPricing` block — same nesting depth, same `CookBot:` parent). Append a sibling key inside `"CookBot"`:

```json
"CookBot": {
    "AuthMode": "Disabled",
    "AppName": "CookBot",
    ...,
    "AiPricingVerifiedDate": "2026-05-16",
    "PantryMatch": {
        "RecencyPenaltyWeight": 0.3,
        "RecencyHalfLifeDays": 7,
        "MinCoverageRatio": 0.6,
        "ResultCount": 3
    }
}
```

---

### DI registration — `AddApplication` + `Program.cs`

**Analog:** `DependencyInjection.cs:11-37` (existing `AddApplication`). Add:
```csharp
services.AddScoped<IPantryMatchService, PantryMatchService>();
```

And for IOptions binding — note `AddApplication` does NOT currently take `IConfiguration`. The cleanest fix is to take an optional `IConfiguration` overload:
```csharp
public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration? configuration = null)
{
    // existing registrations ...
    services.AddScoped<IPantryMatchService, PantryMatchService>();
    if (configuration is not null)
    {
        services.Configure<PantryMatchOptions>(configuration.GetSection("CookBot:PantryMatch"));
    }
    return services;
}
```

Or — simpler — register `PantryMatchOptions` in `Program.cs:61` alongside `CookBotSettings`:
```csharp
builder.Services.Configure<CookBotSettings>(builder.Configuration.GetSection("CookBot"));
builder.Services.Configure<PantryMatchOptions>(builder.Configuration.GetSection("CookBot:PantryMatch"));
```

The Program.cs path keeps `AddApplication` signature stable. **Recommendation: register in Program.cs** (zero-friction, matches existing CookBotSettings pattern).

**`Program.cs:35` analog for ICbTopBarService:**
```csharp
builder.Services.AddScoped<ICbTopBarService, CbTopBarService>();
```

---

## Tests

### `PantryMatchServiceTests.cs`

**Analog A:** `RecipePhotoUrlValidatorTests.cs:14-54` — `[Theory] [InlineData(...)]` matrix for the scoring matrix (days 0/1/3/7/30 with expected score within tolerance).
**Analog B:** `OwnershipTests.cs:11-23` — in-memory SQLite for the diet-filter integration cases (a real `CookBotDbContext` with seeded Recipe + RecipeTag + RecipeIngredient + UserProfile).

```csharp
public class PantryMatchServiceTests : IDisposable
{
    private readonly CookBotDbContext _db;

    public PantryMatchServiceTests()
    {
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite("DataSource=:memory:").Options;
        _db = new CookBotDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Theory]
    [InlineData(0, 0.3)]   // never cooked → no penalty applied (but the formula here uses lastCookedUtc=null, score = coverage)
    [InlineData(1, 0.26)]  // ~ 0.3 * exp(-1/7) ≈ 0.26
    [InlineData(7, 0.11)]  // 0.3 * exp(-1) ≈ 0.110
    [InlineData(30, 0.0040)]  // 0.3 * exp(-30/7) ≈ 0.0040
    public void RecencyPenalty_ExponentialDecay(int daysSinceCooked, double expectedPenalty)
    {
        var actual = 0.3 * Math.Exp(-daysSinceCooked / 7.0);
        Assert.InRange(Math.Abs(actual - expectedPenalty), 0, 0.01);
    }

    [Fact]
    public async Task StableSort_TieBreaksByRecipeIdAscending() { /* ... */ }

    [Fact]
    public async Task DietFilter_VeganExcludesMeatCategory() { /* ... */ }

    public void Dispose() => _db.Dispose();
}
```

### `GroceryListServiceTests.cs`

**Analog:** `OwnershipTests.cs:11-58` — full DbContext + Repository<T> construction pattern.

```csharp
[Fact]
public async Task EnsurePrimaryListAsync_ReturnsMostRecent_WhenExisting()
{
    // seed two GroceryLists for user; assert returned id matches the newer
}

[Fact]
public async Task EnsurePrimaryListAsync_CreatesPantryQuickAdd_WhenNone()
{
    // call against an empty DbContext; assert returned list.Name == "Pantry quick-add"
}

[Fact]
public async Task AddItemAsync_AppendsGroceryListItem()
{
    // seed list; call AddItemAsync; assert Items contains an entry with the right IngredientId/Amount/Unit/IsPurchased=false
}
```

### `PromptBuilderServiceNullFallbackTests.cs`

**Analog:** `PromptSnapshotTests.cs:11-18` + `TestHost.MakeProfile()`. Pure-function tests — no Verify, plain `Assert.Contains` / `Assert.DoesNotContain`.

```csharp
public class PromptBuilderServiceNullFallbackTests
{
    [Fact]
    public void BuildSystemPrompt_NullTemplate_UsesDefault()
    {
        var profile = TestHost.MakeProfile();
        profile.AiSystemPromptTemplate = null;
        var svc = TestHost.GetPromptBuilderService();
        var rendered = svc.BuildSystemPrompt(profile, Array.Empty<PantryItem>());
        Assert.Contains("CookBot, an expert AI cooking assistant", rendered);  // marker from DefaultTemplate line 20
    }

    [Fact]
    public void BuildSystemPrompt_WhitespaceTemplate_UsesDefault()
    {
        var profile = TestHost.MakeProfile();
        profile.AiSystemPromptTemplate = "   \n\t  ";
        var svc = TestHost.GetPromptBuilderService();
        var rendered = svc.BuildSystemPrompt(profile, Array.Empty<PantryItem>());
        Assert.Contains("CookBot, an expert AI cooking assistant", rendered);
    }

    [Fact]
    public void BuildSystemPrompt_CustomTemplate_RespectsOverride()
    {
        var profile = TestHost.MakeProfile();
        profile.AiSystemPromptTemplate = "You are Bob. {{recipe_format}}";
        var svc = TestHost.GetPromptBuilderService();
        var rendered = svc.BuildSystemPrompt(profile, Array.Empty<PantryItem>());
        Assert.Contains("Bob", rendered);
        Assert.DoesNotContain("CookBot, an expert AI cooking assistant", rendered);
    }
}
```

### `CbTopBarServiceTests.cs`

**Analog:** `FractionFormatterTests.cs:5-30` — pure Fact-based service test with no DbContext. NavigationManager needs a test double — Blazor provides `TestNavigationManager` via `Microsoft.AspNetCore.Components.WebAssembly.Authentication`, but simpler is to use `Bunit.TestContext` or a hand-rolled stub. Cleanest: a tiny `TestNavigationManager : NavigationManager` that exposes `NotifyLocationChanged(string uri)`.

```csharp
public class CbTopBarServiceTests
{
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() { Initialize("http://localhost/", "http://localhost/"); }
        public new void NotifyLocationChanged(string uri, bool isInternalNav)
            => base.NotifyLocationChanged(uri, isInternalNav);
    }

    [Fact]
    public void SetRightSlot_RaisesOnChanged()
    {
        var nav = new TestNavigationManager();
        var svc = new CbTopBarService(nav);
        var fired = 0;
        svc.OnChanged += () => fired++;
        svc.SetRightSlot(builder => builder.AddContent(0, "x"));
        Assert.Equal(1, fired);
        Assert.NotNull(svc.RightSlot);
    }

    [Fact]
    public void LocationChanged_AutoClearsRightSlot()
    {
        var nav = new TestNavigationManager();
        var svc = new CbTopBarService(nav);
        svc.SetRightSlot(builder => builder.AddContent(0, "x"));
        nav.NotifyLocationChanged("http://localhost/recipes/1", true);
        Assert.Null(svc.RightSlot);
    }

    [Fact]
    public void Clear_Idempotent_DoesNotFireWhenAlreadyEmpty()
    {
        var nav = new TestNavigationManager();
        var svc = new CbTopBarService(nav);
        var fired = 0;
        svc.OnChanged += () => fired++;
        svc.Clear();  // already null
        Assert.Equal(0, fired);
    }

    [Fact]
    public void Dispose_UnsubscribesFromNavigationManager()
    {
        var nav = new TestNavigationManager();
        var svc = new CbTopBarService(nav);
        svc.Dispose();
        // After dispose, location changes must not fire OnChanged or mutate state.
        var fired = 0;
        svc.OnChanged += () => fired++;
        nav.NotifyLocationChanged("http://localhost/x", true);
        Assert.Equal(0, fired);
    }
}
```

---

## Shared Patterns

### File header / namespace / nullable

**Source:** every existing `.cs` file in the project.
**Apply to:** every new `.cs` file.
- File-scoped namespace: `namespace CookBot.Application.Services;` (one trailing blank line, then the type).
- `using System.*` first, then framework, then project namespaces (`RecipeFormatParser.cs:1-7`).
- `#nullable enable` + implicit usings inherited from csproj — no `using System;` etc.
- Async methods end in `Async`; all I/O is `async Task`.

### Constructor injection + readonly fields

**Source:** `RecipeService.cs:7-31`, `RecipeMadeService.cs:21-27`, `PromptBuilderService.cs:10-16`.
**Apply to:** `PantryMatchService`, `CbTopBarService`, every new service.
```csharp
private readonly IRepository<T> _repo;
public XxxService(IRepository<T> repo) { _repo = repo; }
```

### Authz pattern — owned OR shared cookbook

**Source:** `Home.razor.cs:196-198`, `RecipeMadeService.cs:75-77`, `AiChat.razor:858-859`.
```csharp
.Where(r => r.Cookbook.UserId == userId
            || r.Cookbook.Shares.Any(s => s.SharedWithUserId == userId))
```
**Apply to:** `PantryMatchService.GetMatchesAsync` recipe lookup, AI usage widget queries (filter by `KeyOwnerId == userId`, not via cookbook share — that's a different scope; PROD-18 disclosure copy explains).

### Authz pattern — cookbook ownership check

**Source:** `RecipeService.cs:38-39` + `RecipeService.cs:146-148`.
```csharp
if (cookbook.UserId != userId)
    throw new UnauthorizedAccessException("You do not own this cookbook.");
```
**Apply to:** `RecipeService.UpdateAsync` POLISH-01 reparent path (destination cookbook ownership).

### Error handling — null-coalescing throw + InvalidOperationException

**Source:** `RecipeService.cs:35-36`, `CONVENTIONS.md:127`.
```csharp
var x = await _repo.GetByIdAsync(id) ?? throw new InvalidOperationException("X not found.");
```
**Apply to:** `GroceryListService.AddItemAsync` (list lookup), `RecipeService.UpdateAsync` destination cookbook lookup.

### EF read pattern — AsNoTracking + Include + filter

**Source:** `Home.razor.cs:303-308`, `AiChat.razor:851-867`, `RecipeMadeService.cs:71-81`.
```csharp
await _db.Recipes
    .AsNoTracking()
    .Where(...)
    .Include(r => r.RecipeIngredients).ThenInclude(ri => ri.Ingredient)
    .Include(r => r.Tags)
    .ToListAsync(ct);
```

### JS interop module pattern

**Source:** `recipe-chip-composer.js:4`, `cookbot-shell.js:8`, `cooking-session-state.js:12`.
```js
window.<Namespace> = {
    method(arg) { ... return ...; }
};
```
Script tag must be added to `src/CookBot.Web/Components/App.razor` (per STRUCTURE.md §"New JS interop"). Invocation via `IJSRuntime.InvokeVoidAsync("Namespace.method", arg)`.

### JS interop error handling — try/catch around InvokeVoidAsync

**Source:** `MainLayout.razor:71-103` (`try { ... } catch (InvalidOperationException) { /* prerender */ }`), `Home.razor.cs:171-172` (`catch (JSException) { }; catch (JSDisconnectedException) { }`).
**Apply to:** every new `JS.InvokeVoidAsync` call — prerender + disconnected circuits are normal failure modes.

### Toast feedback at Web layer

**Source:** `SaveRecipeDialog.razor:51,61`, `EditProfile.razor` save handlers.
```csharp
Toast.Show("Action succeeded.", CbToastSeverity.Success);
Toast.Show($"Error: {ex.Message}", CbToastSeverity.Error);
```

### CbDialog pattern — open + parameters + options + result

**Source:** `AiChat.razor:756-763`, `TopBar.razor:150-156`.
```csharp
var parameters = new CbDialogParameters { ["Key"] = value };
var options = new CbDialogOptions(MaxWidth: CbDialogMaxWidth.Sm, FullWidth: true);
var result = await CbDialogService.ShowAsync<TDialog>("Title", parameters, options);
if (result != null && !result.Canceled) { /* success path */ }
```

### Card layout pattern (Cb design system)

**Source:** `EditProfile.razor:42-59` (Display name card).
```razor
<CbCard>
    <CbEyebrow>Section name</CbEyebrow>
    <p style="margin:6px 0 14px 0;font-size:13px;color:var(--ink-3);">Description.</p>
    <div style="display:flex;gap:8px;align-items:center;">
        ...
    </div>
</CbCard>
```

### localStorage UI preference pattern

**Source:** `cookbot-shell.js:35-50` (density), `MainLayout.razor:83-89` (dark mode), `cooking-session-state.js:65-73`.
- Read: `await JS.InvokeAsync<string?>("localStorage.getItem", "cookbot_<key>")`.
- Write: `await JS.InvokeVoidAsync("localStorage.setItem", "cookbot_<key>", value)`.
- Bootstrap on `<html>` before first paint via `cookbot-shell.js applyDefaults()`.
- Wrap in try/catch — privacy mode + prerender both throw.

### IOptions<T> binding pattern

**Source:** `Program.cs:61`, `AnthropicAiService` IOptions injection, `Home.razor.cs:27` `[Inject] IOptions<CookBotSettings>`.
```csharp
// In Program.cs:
builder.Services.Configure<PantryMatchOptions>(builder.Configuration.GetSection("CookBot:PantryMatch"));

// In consumer:
public Service(IOptions<PantryMatchOptions> opts) { _opts = opts.Value; }
```

### Test bootstrap — in-memory SQLite

**Source:** `OwnershipTests.cs:11-23`, `RecipeAccessExtensionsTests.cs:11-19`.
```csharp
private readonly CookBotDbContext _db;
public XxxTests()
{
    var options = new DbContextOptionsBuilder<CookBotDbContext>()
        .UseSqlite("DataSource=:memory:").Options;
    _db = new CookBotDbContext(options);
    _db.Database.OpenConnection();
    _db.Database.EnsureCreated();
}
public void Dispose() => _db.Dispose();
```

### Test bootstrap — TestHost shared helpers

**Source:** `tests/CookBot.Tests/TestHost.cs:21-63`.
- `TestHost.GetPromptBuilderService()` — preconfigured PromptBuilderService.
- `TestHost.MakeProfile()` — deterministic UserProfile fixture (W4 rules).
- `TestHost.GetParser()` — preconfigured RecipeFormatParser.
**Apply to:** `PromptBuilderServiceNullFallbackTests` uses MakeProfile + GetPromptBuilderService verbatim.

---

## No Analog Found

Files / patterns with no prior in-codebase template:

| File / pattern | Why no analog | Recommendation |
|---|---|---|
| `CbTopBarService.LocationChanged` constructor subscription | No existing scoped service subscribes to NavigationManager events in its ctor | Use the analog template from Layering Note 2 (NavigationManager + IDisposable). Add explicit Dispose + unsubscribe. |
| Debounced live validation (500ms idle timer in a Razor dialog) | No existing dialog runs debounced validation; closest is the InsertSuggestion immediate-update pattern in AiChat:490-501 | Use `System.Threading.Timer` reset on each `oninput` (see RawRecipeEditorDialog excerpt above). |
| Per-tick JS DOM mutation (POLISH-05 live timer) | All current JS interop is fire-and-forget or read-once; no setInterval+DOM-mutate pattern | Use the analog template in `cooking-session-state.js startTickLoop` excerpt above. Add `pagehide` teardown. |
| RenderFragment built imperatively in C# (TopBar slot consumer) | Pages use inline Razor markup; no example of building a RenderFragment from C# | Recommend writing the slot content as a `RenderFragment` field that the page renders both into the slot (via `SetRightSlot`) AND inline (for the `<720px` fallback). Single source of truth. |

---

## Metadata

**Analog search scope:**
- `src/CookBot.Application/Services/` — 16 files scanned, 6 read in full
- `src/CookBot.Application/DTOs/` — 3 files, all read
- `src/CookBot.Web/Services/` — 12 files scanned, 4 read in full
- `src/CookBot.Web/Components/Pages/` — 28 files scanned, 5 read (full or key sections)
- `src/CookBot.Web/Components/Atoms/` — 17 files, 5 read in full
- `src/CookBot.Web/Components/Layout/` — 7 files, 3 read in full
- `src/CookBot.Web/Components/Dialogs/` — 2 files, 1 read in full
- `src/CookBot.Web/wwwroot/js/` — 6 files, 3 read in full
- `src/CookBot.Domain/Entities/` — 16 files scanned, 4 read in full
- `src/CookBot.Domain/Enums/` — 5 files, 1 read in full
- `src/CookBot.Domain/Interfaces/` — 5 files, 1 read in full
- `tests/CookBot.Tests/` — 60 .cs files scanned, 7 read in full

**Files scanned (total):** ~140 source files indexed; ~30 read in full; ~10 read in targeted sections.

**Pattern extraction date:** 2026-05-16.

**Phase 10 departures from existing patterns (flagged for planner):**
1. **Layering Note 1** — `IRecipeMadeService` interface needs to move Application-ward.
2. **Layering Note 2** — `CbTopBarService` is the first scoped service to subscribe to NavigationManager in its ctor; needs `IDisposable`.
3. **Layering Note 3** — `GroceryListItem.IsCompleted` does not exist; use `IsPurchased` (or omit; default-false).
4. **Diet → category map (D-47)** — CONTEXT lists `Poultry`/`Fish`/`Eggs` as categories; the real `IngredientCategory` enum has none of those. Planner narrows the map.
5. **CONTEXT mentions `db.UserCanAccessCookbookAsync` (POLISH-01)** — no such extension method exists. Use the inline `cookbook.UserId == userId` check from `RecipeService.cs:38-39`.
6. **`AddApplication` does not take `IConfiguration`** — register `PantryMatchOptions` in `Program.cs:61` alongside `CookBotSettings` instead, OR extend `AddApplication` signature. Planner picks; recommendation is `Program.cs` (zero friction).
