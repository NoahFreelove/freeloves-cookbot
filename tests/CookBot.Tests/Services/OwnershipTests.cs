using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;
using CookBot.Infrastructure.Data;
using CookBot.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CookBot.Tests.Services;

public class OwnershipTests : IDisposable
{
    private readonly CookBotDbContext _db;

    public OwnershipTests()
    {
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task RecipeService_CreateAsync_ThrowsForWrongUser()
    {
        // Arrange
        var user1 = new User { DisplayName = "User1" };
        var user2 = new User { DisplayName = "User2" };
        _db.Users.AddRange(user1, user2);
        await _db.SaveChangesAsync();

        var cookbook = new Cookbook { UserId = user1.Id, Name = "User1 Cookbook" };
        _db.Cookbooks.Add(cookbook);
        await _db.SaveChangesAsync();

        var recipeRepo = new Repository<Recipe>(_db);
        var ingredientRepo = new Repository<Ingredient>(_db);
        var cookbookRepo = new Repository<Cookbook>(_db);
        var recipeTagRepo = new Repository<RecipeTag>(_db);
        var parser = new StubRecipeFormatParser();

        var canonicalSerializer = new JsonRecipeSerializer();
        var service = new RecipeService(parser, recipeRepo, ingredientRepo, cookbookRepo, recipeTagRepo, canonicalSerializer);

        var parsed = new ParsedRecipe
        {
            Name = "Test Recipe",
            Servings = 4,
            Ingredients = new List<ParsedIngredient>(),
            Steps = new List<ParsedStep>(),
        };

        // Act & Assert: user2 tries to create recipe in user1's cookbook
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateAsync(cookbook.Id, user2.Id, parsed));
    }

    [Fact]
    public async Task CookbookService_DeleteAsync_ThrowsForWrongUser()
    {
        // Arrange
        var user1 = new User { DisplayName = "User1" };
        var user2 = new User { DisplayName = "User2" };
        _db.Users.AddRange(user1, user2);
        await _db.SaveChangesAsync();

        var cookbook = new Cookbook { UserId = user1.Id, Name = "User1 Cookbook" };
        _db.Cookbooks.Add(cookbook);
        await _db.SaveChangesAsync();

        var cookbookRepo = new Repository<Cookbook>(_db);
        var service = new CookbookService(cookbookRepo);

        // Act & Assert: user2 tries to delete user1's cookbook
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.DeleteAsync(cookbook.Id, user2.Id));
    }

    [Fact]
    public async Task RecipeService_DeleteAsync_ThrowsForWrongUser()
    {
        // Arrange
        var user1 = new User { DisplayName = "User1" };
        var user2 = new User { DisplayName = "User2" };
        _db.Users.AddRange(user1, user2);
        await _db.SaveChangesAsync();

        var cookbook = new Cookbook { UserId = user1.Id, Name = "User1 Cookbook" };
        _db.Cookbooks.Add(cookbook);
        await _db.SaveChangesAsync();

        var recipe = new Recipe { CookbookId = cookbook.Id, Name = "Test Recipe", Servings = 4, TagsJson = "[]" };
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();

        var recipeRepo = new Repository<Recipe>(_db);
        var ingredientRepo = new Repository<Ingredient>(_db);
        var cookbookRepo = new Repository<Cookbook>(_db);
        var recipeTagRepo = new Repository<RecipeTag>(_db);
        var parser = new StubRecipeFormatParser();

        var canonicalSerializer = new JsonRecipeSerializer();
        var service = new RecipeService(parser, recipeRepo, ingredientRepo, cookbookRepo, recipeTagRepo, canonicalSerializer);

        // Act & Assert: user2 tries to delete recipe in user1's cookbook
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.DeleteAsync(recipe.Id, user2.Id));
    }

    [Fact]
    public async Task CookbookService_GetByIdAsync_ThrowsForUnauthorizedUser()
    {
        // Arrange
        var user1 = new User { DisplayName = "User1" };
        var user2 = new User { DisplayName = "User2" };
        _db.Users.AddRange(user1, user2);
        await _db.SaveChangesAsync();

        var cookbook = new Cookbook { UserId = user1.Id, Name = "User1 Cookbook" };
        _db.Cookbooks.Add(cookbook);
        await _db.SaveChangesAsync();

        var cookbookRepo = new Repository<Cookbook>(_db);
        var service = new CookbookService(cookbookRepo);

        // Act & Assert: user2 tries to access user1's cookbook (not shared)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetByIdAsync(cookbook.Id, user2.Id));
    }

    [Fact]
    public async Task CookbookService_UpdateAsync_ThrowsForWrongUser()
    {
        // Arrange
        var user1 = new User { DisplayName = "User1" };
        var user2 = new User { DisplayName = "User2" };
        _db.Users.AddRange(user1, user2);
        await _db.SaveChangesAsync();

        var cookbook = new Cookbook { UserId = user1.Id, Name = "User1 Cookbook" };
        _db.Cookbooks.Add(cookbook);
        await _db.SaveChangesAsync();

        var cookbookRepo = new Repository<Cookbook>(_db);
        var service = new CookbookService(cookbookRepo);

        // Act & Assert: user2 tries to update user1's cookbook
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.UpdateAsync(cookbook.Id, user2.Id, "Renamed", "New description"));
    }

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// Minimal stub parser for RecipeService constructor — not used in ownership tests.
    /// </summary>
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
}
