using CookBot.Domain.Entities;
using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Tests.Recipes;

/// <summary>
/// CLEAN-02 / D-34 backfill SQL semantics verification.
/// Validates: trim whitespace, preserve case (Vegan/vegan coexist per D-34), skip empty/whitespace,
/// idempotency via ON CONFLICT DO NOTHING.
/// Uses a SQLite file context (not in-memory) because json_each is a SQLite extension.
/// </summary>
public class RecipeTagBackfillTests : IDisposable
{
    // The backfill SQL from the AddRecipeTagTable migration, extracted as a const for
    // direct execution in tests — same SQL that runs on app boot via MigrateAsync().
    private const string BackfillSql = @"
        INSERT INTO RecipeTags (RecipeId, Name)
        SELECT r.Id, TRIM(json_each.value)
        FROM Recipes r, json_each(r.TagsJson)
        WHERE TRIM(json_each.value) <> ''
        ON CONFLICT DO NOTHING;
    ";

    private readonly CookBotDbContext _db;
    private readonly string _dbPath;

    public RecipeTagBackfillTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cookbot-tag-backfill-{Path.GetRandomFileName()}.db");
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite($"DataSource={_dbPath}")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Backfill_TrimAndCasePreservation_WorksCorrectly()
    {
        // Arrange: seed User + Cookbook + Recipes with TagsJson
        var user = new User { DisplayName = "Test" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var cookbook = new Cookbook { Name = "Test", UserId = user.Id };
        _db.Cookbooks.Add(cookbook);
        await _db.SaveChangesAsync();

        // Recipe A: Vegan / vegan (case-coexistence per D-34) + " gluten-free " (trim)
        var recipeA = new Recipe
        {
            CookbookId = cookbook.Id,
            Name = "Recipe A",
            Servings = 4,
            TagsJson = """["Vegan","vegan"," gluten-free "]""",
        };
        // Recipe B: dairy-free + Vegan
        var recipeB = new Recipe
        {
            CookbookId = cookbook.Id,
            Name = "Recipe B",
            Servings = 2,
            TagsJson = """["dairy-free","Vegan"]""",
        };
        // Recipe C: edge cases — empty string, whitespace-only, null in array
        var recipeC = new Recipe
        {
            CookbookId = cookbook.Id,
            Name = "Recipe C",
            Servings = 1,
            TagsJson = """[""," "]""",
        };
        _db.Recipes.AddRange(recipeA, recipeB, recipeC);
        await _db.SaveChangesAsync();

        // Act: execute the backfill SQL (same SQL embedded in AddRecipeTagTable migration)
        await _db.Database.ExecuteSqlRawAsync(BackfillSql);

        // Assert — Recipe A: D-34 case coexistence
        var aVegan = _db.RecipeTags.Where(t => t.RecipeId == recipeA.Id && t.Name == "Vegan").Count();
        var aVeganLower = _db.RecipeTags.Where(t => t.RecipeId == recipeA.Id && t.Name == "vegan").Count();
        var aGlutenFree = _db.RecipeTags.Where(t => t.RecipeId == recipeA.Id && t.Name == "gluten-free").Count();
        var aGlutenFreeUntrimmed = _db.RecipeTags.Where(t => t.RecipeId == recipeA.Id && t.Name == " gluten-free ").Count();

        Assert.Equal(1, aVegan);              // D-34: Vegan stored
        Assert.Equal(1, aVeganLower);         // D-34: vegan coexists as distinct row
        Assert.Equal(1, aGlutenFree);         // D-34: trimmed to "gluten-free"
        Assert.Equal(0, aGlutenFreeUntrimmed); // D-34: untrimmed variant NOT stored

        // Assert — Recipe B: separate recipe, distinct rows
        var bDairy = _db.RecipeTags.Where(t => t.RecipeId == recipeB.Id && t.Name == "dairy-free").Count();
        var bVegan = _db.RecipeTags.Where(t => t.RecipeId == recipeB.Id && t.Name == "Vegan").Count();
        Assert.Equal(1, bDairy);
        Assert.Equal(1, bVegan);

        // Assert — Recipe C: empty/whitespace-only entries must be skipped (WHERE TRIM(...) <> '')
        var cAny = _db.RecipeTags.Any(t => t.RecipeId == recipeC.Id);
        Assert.False(cAny, "empty/whitespace tag values must be skipped by the backfill");
    }

    [Fact]
    public async Task Backfill_Idempotency_OnConflictDoNothingKeepsRowCountStable()
    {
        // Arrange
        var user = new User { DisplayName = "Idempotent" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var cookbook = new Cookbook { Name = "IdemTest", UserId = user.Id };
        _db.Cookbooks.Add(cookbook);
        await _db.SaveChangesAsync();

        var recipe = new Recipe
        {
            CookbookId = cookbook.Id,
            Name = "Idempotent Recipe",
            Servings = 1,
            TagsJson = """["Vegan","vegan"]""",
        };
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();

        // Act: run backfill twice — idempotency assertion
        await _db.Database.ExecuteSqlRawAsync(BackfillSql);
        var countAfterFirst = _db.RecipeTags.Count(t => t.RecipeId == recipe.Id);

        await _db.Database.ExecuteSqlRawAsync(BackfillSql);
        var countAfterSecond = _db.RecipeTags.Count(t => t.RecipeId == recipe.Id);

        // Assert: row count unchanged after second run (ON CONFLICT DO NOTHING)
        Assert.Equal(2, countAfterFirst);
        Assert.Equal(countAfterFirst, countAfterSecond); // idempotent
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
