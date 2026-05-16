using CookBot.Domain.Entities;
using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Tests.Services;

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
    public async Task UserCanAccessRecipeAsync_Owner_ReturnsTrue()
    {
        var owner = new User { DisplayName = "Owner" };
        _db.Users.Add(owner);
        await _db.SaveChangesAsync();

        var cookbook = new Cookbook { UserId = owner.Id, Name = "Mine" };
        _db.Cookbooks.Add(cookbook);
        await _db.SaveChangesAsync();

        var recipe = new Recipe { CookbookId = cookbook.Id, Name = "Secret", Servings = 2 };
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();

        var ok = await _db.UserCanAccessRecipeAsync(recipe.Id, owner.Id);
        Assert.True(ok);
    }

    [Fact]
    public async Task UserCanAccessRecipeAsync_SharedUser_ReturnsTrue()
    {
        var owner = new User { DisplayName = "Owner" };
        var guest = new User { DisplayName = "Guest" };
        _db.Users.AddRange(owner, guest);
        await _db.SaveChangesAsync();

        var cookbook = new Cookbook { UserId = owner.Id, Name = "Mine" };
        _db.Cookbooks.Add(cookbook);
        await _db.SaveChangesAsync();

        _db.CookbookShares.Add(new CookbookShare
        {
            CookbookId = cookbook.Id,
            SharedWithUserId = guest.Id,
        });
        await _db.SaveChangesAsync();

        var recipe = new Recipe { CookbookId = cookbook.Id, Name = "Shared", Servings = 2 };
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();

        var ok = await _db.UserCanAccessRecipeAsync(recipe.Id, guest.Id);
        Assert.True(ok);
    }

    [Fact]
    public async Task UserCanAccessRecipeAsync_Stranger_ReturnsFalse()
    {
        var owner = new User { DisplayName = "Owner" };
        var stranger = new User { DisplayName = "Stranger" };
        _db.Users.AddRange(owner, stranger);
        await _db.SaveChangesAsync();

        var cookbook = new Cookbook { UserId = owner.Id, Name = "Mine" };
        _db.Cookbooks.Add(cookbook);
        await _db.SaveChangesAsync();

        var recipe = new Recipe { CookbookId = cookbook.Id, Name = "Private", Servings = 2 };
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();

        var ok = await _db.UserCanAccessRecipeAsync(recipe.Id, stranger.Id);
        Assert.False(ok);
    }

    public void Dispose() => _db.Dispose();
}
