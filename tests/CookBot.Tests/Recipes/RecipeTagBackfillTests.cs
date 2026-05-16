using CookBot.Domain.Entities;
using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Tests.Recipes;

/// <summary>
/// CLEAN-02 / D-34 backfill SQL semantics verification.
/// Validates: trim whitespace, preserve case (Vegan/vegan coexist per D-34), skip empty/whitespace,
/// idempotency via ON CONFLICT DO NOTHING.
///
/// Plan 11 (CLEAN-02 finalization): Recipe.TagsJson was removed from the EF model and the Recipes table
/// was dropped via the DropTagsJsonColumn migration. To preserve regression value for the backfill SQL
/// (which runs in AddRecipeTagTable migration history and cannot be changed), tests now seed the
/// legacy TagsJson column via raw SQL ALTER TABLE / INSERT after EnsureCreated() re-adds the column to
/// a temp database, simulating the pre-drop DB state that existed when the backfill migration ran.
/// This approach matches the plan's "option (a)" decision — the SQL is still tested, just using raw
/// DDL seeding instead of C# property assignment.
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

    // DDL to re-add the TagsJson column to a freshly created test DB that no longer has it.
    // The column was dropped in production by the DropTagsJsonColumn migration (Plan 11),
    // but these tests simulate the state BEFORE that drop to validate the backfill SQL.
    private const string AddTagsJsonColumnSql = @"
        ALTER TABLE Recipes ADD COLUMN TagsJson TEXT NOT NULL DEFAULT '[]';
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

        // Re-add the TagsJson column that was dropped in production (Plan 11 / DropTagsJsonColumn).
        // This simulates the pre-drop DB state so the backfill SQL can be tested.
        _db.Database.ExecuteSqlRaw(AddTagsJsonColumnSql);
    }

    [Fact]
    public async Task Backfill_TrimAndCasePreservation_WorksCorrectly()
    {
        // Arrange: seed User + Cookbook + Recipes via EF (relational columns),
        // then UPDATE TagsJson via raw SQL to simulate pre-drop DB state.
        var user = new User { DisplayName = "Test" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var cookbook = new Cookbook { Name = "Test", UserId = user.Id };
        _db.Cookbooks.Add(cookbook);
        await _db.SaveChangesAsync();

        // Recipe A: Vegan / vegan (case-coexistence per D-34) + " gluten-free " (trim)
        var recipeA = new Recipe { CookbookId = cookbook.Id, Name = "Recipe A", Servings = 4 };
        // Recipe B: dairy-free + Vegan
        var recipeB = new Recipe { CookbookId = cookbook.Id, Name = "Recipe B", Servings = 2 };
        // Recipe C: edge cases — empty string, whitespace-only
        var recipeC = new Recipe { CookbookId = cookbook.Id, Name = "Recipe C", Servings = 1 };
        _db.Recipes.AddRange(recipeA, recipeB, recipeC);
        await _db.SaveChangesAsync();

        // Seed TagsJson via raw SQL (simulates pre-drop DB state)
        await _db.Database.ExecuteSqlRawAsync(
            $"UPDATE Recipes SET TagsJson = '[\"Vegan\",\"vegan\",\" gluten-free \"]' WHERE Id = {recipeA.Id}");
        await _db.Database.ExecuteSqlRawAsync(
            $"UPDATE Recipes SET TagsJson = '[\"dairy-free\",\"Vegan\"]' WHERE Id = {recipeB.Id}");
        await _db.Database.ExecuteSqlRawAsync(
            $"UPDATE Recipes SET TagsJson = '[\"\",\" \"]' WHERE Id = {recipeC.Id}");

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

        var recipe = new Recipe { CookbookId = cookbook.Id, Name = "Idempotent Recipe", Servings = 1 };
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();

        // Seed TagsJson via raw SQL (simulates pre-drop DB state)
        await _db.Database.ExecuteSqlRawAsync(
            $"UPDATE Recipes SET TagsJson = '[\"Vegan\",\"vegan\"]' WHERE Id = {recipe.Id}");

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
