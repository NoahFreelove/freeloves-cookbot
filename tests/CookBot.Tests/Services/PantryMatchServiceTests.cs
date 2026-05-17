using CookBot.Application.DTOs;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Enums;
using CookBot.Infrastructure.Data;
using CookBot.Infrastructure.Data.Repositories;
using CookBot.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CookBot.Tests.Services;

/// <summary>
/// Phase 10 / Plan 10-03 — QOL-01..03: PantryMatchService scoring matrix + dietary filter tests.
/// Uses in-memory SQLite (same pattern as OwnershipTests) so EF navigation loads correctly.
/// </summary>
public class PantryMatchServiceTests : IDisposable
{
    private readonly CookBotDbContext _db;

    public PantryMatchServiceTests()
    {
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private PantryMatchService BuildService(PantryMatchOptions? opts = null)
    {
        var recipeRepo = new Repository<Recipe>(_db);
        var userProfileRepo = new Repository<UserProfile>(_db);
        var recipeIngredientRepo = new Repository<RecipeIngredient>(_db);
        var ingredientRepo = new Repository<Ingredient>(_db);
        var cookbookShareRepo = new Repository<CookbookShare>(_db);
        var pantryRepo = new Repository<PantryItem>(_db);
        var pantryEntityRepo = new Repository<Pantry>(_db);
        var memberRepo = new Repository<PantryMember>(_db);
        var unitConverter = new UnitConversionService();
        var pantryService = new PantryService(pantryRepo, pantryEntityRepo, memberRepo, ingredientRepo, unitConverter);
        var recipeMadeService = new RecipeMadeService(_db);
        var options = Options.Create(opts ?? new PantryMatchOptions());
        return new PantryMatchService(recipeRepo, userProfileRepo, recipeIngredientRepo, ingredientRepo, cookbookShareRepo, recipeMadeService, pantryService, options);
    }

    /// <summary>Seeds a user+cookbook+pantry and returns (userId, cookbookId, pantryId).</summary>
    private async Task<(int userId, int cookbookId, int pantryId)> SeedUserAsync(string displayName = "Alice")
    {
        var user = new User { DisplayName = displayName };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var profile = new UserProfile { UserId = user.Id, DietaryPreferencesJson = "[]" };
        _db.UserProfiles.Add(profile);

        var cookbook = new Cookbook { UserId = user.Id, Name = $"{displayName} Cookbook" };
        _db.Cookbooks.Add(cookbook);

        var pantry = new Pantry { OwnerId = user.Id, Name = "Personal", IsPersonal = true };
        _db.Pantries.Add(pantry);

        await _db.SaveChangesAsync();
        return (user.Id, cookbook.Id, pantry.Id);
    }

    /// <summary>Seeds an ingredient with a given category and adds it to the pantry.</summary>
    private async Task<Ingredient> SeedIngredientInPantryAsync(int pantryId, string name, IngredientCategory category = IngredientCategory.Produce)
    {
        var ingredient = new Ingredient { Name = name, NormalizedName = name.ToLower(), Category = category };
        _db.Ingredients.Add(ingredient);
        await _db.SaveChangesAsync();

        _db.PantryItems.Add(new PantryItem { PantryId = pantryId, IngredientId = ingredient.Id, Amount = 1, Unit = "unit" });
        await _db.SaveChangesAsync();
        return ingredient;
    }

    /// <summary>Seeds an ingredient NOT in the pantry.</summary>
    private async Task<Ingredient> SeedIngredientAsync(string name, IngredientCategory category = IngredientCategory.Produce)
    {
        var ingredient = new Ingredient { Name = name, NormalizedName = name.ToLower(), Category = category };
        _db.Ingredients.Add(ingredient);
        await _db.SaveChangesAsync();
        return ingredient;
    }

    /// <summary>Adds a recipe to the cookbook with the given ingredient ids as RecipeIngredients.</summary>
    private async Task<Recipe> SeedRecipeAsync(int cookbookId, string name, IEnumerable<int> ingredientIds, IEnumerable<string>? tagNames = null)
    {
        var recipe = new Recipe { CookbookId = cookbookId, Name = name, Servings = 4 };
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();

        int localId = 1;
        foreach (var ingId in ingredientIds)
        {
            _db.RecipeIngredients.Add(new RecipeIngredient
            {
                RecipeId = recipe.Id,
                IngredientId = ingId,
                RecipeLocalId = localId++,
                Amount = 1,
                Unit = "unit"
            });
        }

        if (tagNames != null)
        {
            foreach (var tag in tagNames)
                _db.RecipeTags.Add(new RecipeTag { RecipeId = recipe.Id, Name = tag });
        }

        await _db.SaveChangesAsync();
        return recipe;
    }

    // -------------------------------------------------------------------------
    // Test 1: Pure math sanity — recency penalty exponential decay
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0.30)]
    [InlineData(1, 0.260)]
    [InlineData(3, 0.197)]
    [InlineData(7, 0.110)]
    [InlineData(30, 0.0040)]
    public void RecencyPenalty_ExponentialDecay(double daysSinceCooked, double expectedPenalty)
    {
        // D-44 formula: RecencyPenaltyWeight * exp(-daysSinceCooked / RecencyHalfLifeDays)
        // weight = 0.3, halfLife = 7.0
        var actual = 0.3 * Math.Exp(-daysSinceCooked / 7.0);
        Assert.InRange(Math.Abs(actual - expectedPenalty), 0, 0.01);
    }

    // -------------------------------------------------------------------------
    // Test 2: Stable sort — tie-break by recipeId ascending
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMatchesAsync_StableSort_TieBreaksByRecipeIdAscending()
    {
        var (userId, cookbookId, pantryId) = await SeedUserAsync();
        var ing1 = await SeedIngredientInPantryAsync(pantryId, "flour");
        var ing2 = await SeedIngredientInPantryAsync(pantryId, "eggs");

        // Both recipes have identical 2/2 coverage, never cooked — same score
        var recipeA = await SeedRecipeAsync(cookbookId, "Recipe A", new[] { ing1.Id, ing2.Id });
        var recipeB = await SeedRecipeAsync(cookbookId, "Recipe B", new[] { ing1.Id, ing2.Id });
        // recipeA has lower Id (seeded first)

        var opts = new PantryMatchOptions { ResultCount = 10, MinCoverageRatio = 0.0 };
        var svc = BuildService(opts);
        var results = await svc.GetMatchesAsync(userId);

        Assert.True(results.Count >= 2);
        var idA = results.First(r => r.RecipeName == "Recipe A").RecipeId;
        var idB = results.First(r => r.RecipeName == "Recipe B").RecipeId;
        Assert.True(idA < idB, "Lower RecipeId should come first when scores are equal");
        Assert.Equal(results.First().RecipeId, Math.Min(recipeA.Id, recipeB.Id));
    }

    // -------------------------------------------------------------------------
    // Test 3: MinCoverageRatio — low-coverage recipe excluded
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMatchesAsync_AppliesMinCoverageRatio_ExcludesLowCoverage()
    {
        var (userId, cookbookId, pantryId) = await SeedUserAsync();
        var ing1 = await SeedIngredientInPantryAsync(pantryId, "flour");

        // Seed 10-ingredient recipe; only 1 in pantry → coverage = 0.1 (below default 0.6)
        var missingIngs = new List<int> { ing1.Id };
        for (int i = 0; i < 9; i++)
        {
            var mi = await SeedIngredientAsync($"missing{i}");
            missingIngs.Add(mi.Id);
        }

        var recipe = await SeedRecipeAsync(cookbookId, "Low Coverage Recipe", missingIngs);

        var svc = BuildService(new PantryMatchOptions { MinCoverageRatio = 0.6, ResultCount = 10 });
        var results = await svc.GetMatchesAsync(userId);

        Assert.DoesNotContain(results, r => r.RecipeId == recipe.Id);
    }

    // -------------------------------------------------------------------------
    // Test 4: Never cooked → no penalty, score = coverage
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMatchesAsync_NeverCooked_NoPenalty()
    {
        var (userId, cookbookId, pantryId) = await SeedUserAsync();
        var ings = new List<Ingredient>();
        for (int i = 0; i < 5; i++)
            ings.Add(await SeedIngredientInPantryAsync(pantryId, $"ing{i}"));

        await SeedRecipeAsync(cookbookId, "Full Match", ings.Select(i => i.Id));
        // No RecipeMade row — never cooked

        var svc = BuildService(new PantryMatchOptions { MinCoverageRatio = 0.0, ResultCount = 10 });
        var results = await svc.GetMatchesAsync(userId);

        Assert.NotEmpty(results);
        var result = results.First(r => r.RecipeName == "Full Match");
        // Score should be 1.0 (5/5 coverage, no penalty)
        Assert.InRange(Math.Abs(result.Score - 1.0), 0, 0.001);
    }

    // -------------------------------------------------------------------------
    // Test 5: Recently cooked → applies penalty
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMatchesAsync_RecentlyCooked_AppliesPenalty()
    {
        var (userId, cookbookId, pantryId) = await SeedUserAsync();
        var ings = new List<Ingredient>();
        for (int i = 0; i < 5; i++)
            ings.Add(await SeedIngredientInPantryAsync(pantryId, $"pingr{i}"));

        var recipe = await SeedRecipeAsync(cookbookId, "Recently Cooked", ings.Select(i => i.Id));

        // Seed RecipeMade: cooked 1 day ago
        _db.RecipeMades.Add(new RecipeMade
        {
            RecipeId = recipe.Id,
            UserId = userId,
            CompletedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _db.SaveChangesAsync();

        var svc = BuildService(new PantryMatchOptions { MinCoverageRatio = 0.0, ResultCount = 10 });
        var results = await svc.GetMatchesAsync(userId);

        Assert.NotEmpty(results);
        var r = results.First(x => x.RecipeId == recipe.Id);
        // score ≈ 1.0 - 0.3 * exp(-1/7) ≈ 0.7404
        double expected = 1.0 - 0.3 * Math.Exp(-1.0 / 7.0);
        Assert.InRange(Math.Abs(r.Score - expected), 0, 0.01);
    }

    // -------------------------------------------------------------------------
    // Test 6: Diet filter — vegan excludes meat category
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMatchesAsync_DietFilter_VeganExcludesMeatCategory()
    {
        var (userId, cookbookId, pantryId) = await SeedUserAsync();

        // Set vegan dietary preference
        var profile = await _db.UserProfiles.FirstAsync(p => p.UserId == userId);
        profile.DietaryPreferencesJson = "[\"vegan\"]";
        await _db.SaveChangesAsync();

        var produceIng = await SeedIngredientInPantryAsync(pantryId, "spinach", IngredientCategory.Produce);
        var meatIng = await SeedIngredientInPantryAsync(pantryId, "chicken", IngredientCategory.Meat);

        // Recipe A: all produce — should pass
        var produceRecipe = await SeedRecipeAsync(cookbookId, "Vegan Recipe", new[] { produceIng.Id }, tagNames: new[] { "vegan" });
        // Recipe B: has meat ingredient — should be excluded
        var meatRecipe = await SeedRecipeAsync(cookbookId, "Meat Recipe", new[] { meatIng.Id }, tagNames: new[] { "vegan" });

        var svc = BuildService(new PantryMatchOptions { MinCoverageRatio = 0.0, ResultCount = 10 });
        var results = await svc.GetMatchesAsync(userId);

        Assert.DoesNotContain(results, r => r.RecipeId == meatRecipe.Id);
        Assert.Contains(results, r => r.RecipeId == produceRecipe.Id);
    }

    // -------------------------------------------------------------------------
    // Test 7: Diet filter — vegetarian requires matching RecipeTag
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMatchesAsync_DietFilter_VegetarianRequiresMatchingRecipeTag()
    {
        var (userId, cookbookId, pantryId) = await SeedUserAsync();

        // Set vegetarian dietary preference
        var profile = await _db.UserProfiles.FirstAsync(p => p.UserId == userId);
        profile.DietaryPreferencesJson = "[\"vegetarian\"]";
        await _db.SaveChangesAsync();

        var ing = await SeedIngredientInPantryAsync(pantryId, "tomato", IngredientCategory.Produce);

        // Recipe A: tagged vegetarian → should pass positive tag filter
        var tagged = await SeedRecipeAsync(cookbookId, "Tagged Veg Recipe", new[] { ing.Id }, tagNames: new[] { "vegetarian" });
        // Recipe B: no vegetarian tag → should be excluded by positive tag filter
        var untagged = await SeedRecipeAsync(cookbookId, "Untagged Recipe", new[] { ing.Id });

        var svc = BuildService(new PantryMatchOptions { MinCoverageRatio = 0.0, ResultCount = 10 });
        var results = await svc.GetMatchesAsync(userId);

        Assert.Contains(results, r => r.RecipeId == tagged.Id);
        Assert.DoesNotContain(results, r => r.RecipeId == untagged.Id);
    }

    // -------------------------------------------------------------------------
    // Test 8: Unknown diet label — skips negative filter, keeps positive tag match
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMatchesAsync_UnknownDietLabel_SkipsNegativeFilter_KeepsPositiveTagMatch()
    {
        var (userId, cookbookId, pantryId) = await SeedUserAsync();

        // "keto" is not in DietExcludeMap — no negative category filter, only positive tag match
        var profile = await _db.UserProfiles.FirstAsync(p => p.UserId == userId);
        profile.DietaryPreferencesJson = "[\"keto\"]";
        await _db.SaveChangesAsync();

        var ing = await SeedIngredientInPantryAsync(pantryId, "avocado", IngredientCategory.Produce);

        // Recipe A: keto-tagged → survives positive tag match
        var ketoRecipe = await SeedRecipeAsync(cookbookId, "Keto Recipe", new[] { ing.Id }, tagNames: new[] { "keto" });
        // Recipe B: no keto tag → excluded by positive tag match
        var nonKetoRecipe = await SeedRecipeAsync(cookbookId, "Non-Keto Recipe", new[] { ing.Id });

        var svc = BuildService(new PantryMatchOptions { MinCoverageRatio = 0.0, ResultCount = 10 });
        var results = await svc.GetMatchesAsync(userId);

        Assert.Contains(results, r => r.RecipeId == ketoRecipe.Id);
        Assert.DoesNotContain(results, r => r.RecipeId == nonKetoRecipe.Id);
    }

    // -------------------------------------------------------------------------
    // Test 9: Only returns accessible recipes (owned OR shared, NOT other users')
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMatchesAsync_OnlyReturnsAccessibleRecipes()
    {
        // User (Alice)
        var (userId, cookbookId, pantryId) = await SeedUserAsync("Alice");

        // Other user (Bob) — unrelated cookbook
        var (bobId, bobCookbookId, _) = await SeedUserAsync("Bob");

        // Third user (Carol) — shares her cookbook with Alice
        var (carolId, carolCookbookId, _) = await SeedUserAsync("Carol");
        _db.CookbookShares.Add(new CookbookShare { CookbookId = carolCookbookId, SharedWithUserId = userId });
        await _db.SaveChangesAsync();

        var ing = await SeedIngredientInPantryAsync(pantryId, "garlic", IngredientCategory.Produce);

        // Recipe 1: Alice owns it — should appear
        var aliceRecipe = await SeedRecipeAsync(cookbookId, "Alice Recipe", new[] { ing.Id });
        // Recipe 2: Carol shared with Alice — should appear
        var carolRecipe = await SeedRecipeAsync(carolCookbookId, "Carol Recipe", new[] { ing.Id });
        // Recipe 3: Bob's unshared cookbook — should NOT appear
        var bobRecipe = await SeedRecipeAsync(bobCookbookId, "Bob Recipe", new[] { ing.Id });

        var svc = BuildService(new PantryMatchOptions { MinCoverageRatio = 0.0, ResultCount = 10 });
        var results = await svc.GetMatchesAsync(userId);

        Assert.Contains(results, r => r.RecipeId == aliceRecipe.Id);
        Assert.Contains(results, r => r.RecipeId == carolRecipe.Id);
        Assert.DoesNotContain(results, r => r.RecipeId == bobRecipe.Id);
    }

    // -------------------------------------------------------------------------
    // Test 10: ResultCount is respected
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetMatchesAsync_RespectsResultCount()
    {
        var (userId, cookbookId, pantryId) = await SeedUserAsync();
        var ing = await SeedIngredientInPantryAsync(pantryId, "rice");

        // Seed 10 recipes all with 1/1 coverage
        for (int i = 0; i < 10; i++)
            await SeedRecipeAsync(cookbookId, $"Recipe {i}", new[] { ing.Id });

        var svc = BuildService(new PantryMatchOptions { MinCoverageRatio = 0.0, ResultCount = 3 });
        var results = await svc.GetMatchesAsync(userId);

        Assert.Equal(3, results.Count);
    }

    public void Dispose() => _db.Dispose();
}
