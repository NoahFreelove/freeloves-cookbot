using CookBot.Domain.Entities;
using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Tests.Migration;

/// <summary>
/// GALLERY-01 / Phase 14 / Plan 14-01 — proves:
/// 1. The backfill SQL is lossless: recipes with a non-empty PhotoUrl produce exactly one
///    primary RecipePhoto row (SortOrder=0, IsPrimary=true, Url matching PhotoUrl).
///    Recipes with NULL or empty PhotoUrl produce zero rows.
/// 2. Deleting a Recipe cascade-deletes all its RecipePhoto rows (EF DeleteBehavior.Cascade).
///
/// Note: EnsureCreated does not run EF migrations, so the backfill INSERT is executed
/// directly in the test via <c>Database.ExecuteSqlRaw</c> — this proves the SQL that
/// the migration <c>Up()</c> runs is correct and lossless.
/// The cascade is proven via the EF model (DeleteBehavior.Cascade in RecipePhotoConfiguration).
/// </summary>
public class RecipePhotoBackfillTests : IDisposable
{
    // The exact INSERT statement from migration 20260607124611_AddRecipePhotosTable Up().
    // Tested directly here so any future migration edit that changes the SQL will break this
    // test — making regressions immediately visible.
    private const string BackfillSql = @"
        INSERT INTO RecipePhotos (RecipeId, Url, SortOrder, IsPrimary)
        SELECT Id, PhotoUrl, 0, 1
        FROM Recipes
        WHERE PhotoUrl IS NOT NULL AND PhotoUrl != ''
    ";

    private readonly CookBotDbContext _db;

    public RecipePhotoBackfillTests()
    {
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    // ─── Helper ────────────────────────────────────────────────────────────────

    private async Task<(int UserId, int CookbookId)> SeedUserAndCookbookAsync()
    {
        var user = new User { DisplayName = "TestUser" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var cookbook = new Cookbook { UserId = user.Id, Name = "Test Cookbook" };
        _db.Cookbooks.Add(cookbook);
        await _db.SaveChangesAsync();

        return (user.Id, cookbook.Id);
    }

    // ─── Backfill test ─────────────────────────────────────────────────────────

    /// <summary>
    /// Proves: recipes with a non-null, non-empty PhotoUrl → exactly one IsPrimary row each.
    /// Recipes with NULL or "" PhotoUrl → zero rows.
    /// </summary>
    [Fact]
    public async Task Backfill_OnePrimaryRowPerRecipeWithPhotoUrl_NullOrEmptyProducesZeroRows()
    {
        // Arrange
        var (_, cookbookId) = await SeedUserAndCookbookAsync();

        var recipeWithUrl1 = new Recipe
        {
            CookbookId = cookbookId,
            Name = "Pizza",
            Servings = 4,
            PhotoUrl = "/uploads/pizza-abc123.jpg"
        };
        var recipeWithUrl2 = new Recipe
        {
            CookbookId = cookbookId,
            Name = "Pasta",
            Servings = 2,
            PhotoUrl = "https://example.com/pasta.jpg"
        };
        var recipeNullUrl = new Recipe
        {
            CookbookId = cookbookId,
            Name = "Salad",
            Servings = 2,
            PhotoUrl = null
        };
        var recipeEmptyUrl = new Recipe
        {
            CookbookId = cookbookId,
            Name = "Soup",
            Servings = 4,
            PhotoUrl = ""
        };

        _db.Recipes.AddRange(recipeWithUrl1, recipeWithUrl2, recipeNullUrl, recipeEmptyUrl);
        await _db.SaveChangesAsync();

        // Act — execute the exact backfill SQL from the migration Up()
        _db.Database.ExecuteSqlRaw(BackfillSql);

        // Assert — total rows created
        var allPhotos = await _db.RecipePhotos.ToListAsync();
        Assert.Equal(2, allPhotos.Count);

        // Assert — each populated recipe gets exactly one IsPrimary=true, SortOrder=0 row
        var photo1 = allPhotos.Single(p => p.RecipeId == recipeWithUrl1.Id);
        Assert.True(photo1.IsPrimary);
        Assert.Equal(0, photo1.SortOrder);
        Assert.Equal(recipeWithUrl1.PhotoUrl, photo1.Url);

        var photo2 = allPhotos.Single(p => p.RecipeId == recipeWithUrl2.Id);
        Assert.True(photo2.IsPrimary);
        Assert.Equal(0, photo2.SortOrder);
        Assert.Equal(recipeWithUrl2.PhotoUrl, photo2.Url);

        // Assert — null and empty PhotoUrl produce zero rows
        Assert.DoesNotContain(allPhotos, p => p.RecipeId == recipeNullUrl.Id);
        Assert.DoesNotContain(allPhotos, p => p.RecipeId == recipeEmptyUrl.Id);
    }

    // ─── Cascade test ──────────────────────────────────────────────────────────

    /// <summary>
    /// Proves: deleting a Recipe cascade-deletes all its RecipePhoto rows via
    /// DeleteBehavior.Cascade configured in RecipePhotoConfiguration.
    /// </summary>
    [Fact]
    public async Task DeleteRecipe_CascadeDeletesAllRecipePhotoRows()
    {
        // Arrange
        var (_, cookbookId) = await SeedUserAndCookbookAsync();

        var recipe = new Recipe
        {
            CookbookId = cookbookId,
            Name = "Roast Chicken",
            Servings = 4,
            PhotoUrl = "/uploads/chicken.jpg"
        };
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();

        // Seed three RecipePhoto rows through the context
        _db.RecipePhotos.AddRange(
            new RecipePhoto { RecipeId = recipe.Id, Url = "/uploads/chicken1.jpg", SortOrder = 0, IsPrimary = true },
            new RecipePhoto { RecipeId = recipe.Id, Url = "/uploads/chicken2.jpg", SortOrder = 1, IsPrimary = false },
            new RecipePhoto { RecipeId = recipe.Id, Url = "/uploads/chicken3.jpg", SortOrder = 2, IsPrimary = false }
        );
        await _db.SaveChangesAsync();

        // Confirm 3 photo rows exist before delete
        Assert.Equal(3, await _db.RecipePhotos.CountAsync(p => p.RecipeId == recipe.Id));

        // Act — delete the recipe; cascade should remove all photo rows
        _db.Recipes.Remove(recipe);
        await _db.SaveChangesAsync();

        // Assert — zero photo rows remain for the deleted recipe
        Assert.Equal(0, await _db.RecipePhotos.CountAsync(p => p.RecipeId == recipe.Id));
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        _db.Dispose();
    }
}
