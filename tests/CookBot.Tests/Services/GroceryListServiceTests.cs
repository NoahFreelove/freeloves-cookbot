using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Infrastructure.Data;
using CookBot.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CookBot.Tests.Services;

/// <summary>
/// Unit tests for GroceryListService — Phase 10 / Plan 10-11 / POLISH-02.
/// Covers EnsurePrimaryListAsync and AddItemAsync (double amount — B-02 corrected).
/// Bootstrap mirrors OwnershipTests: in-memory SQLite + OpenConnection + EnsureCreated.
/// </summary>
public class GroceryListServiceTests : IDisposable
{
    private readonly CookBotDbContext _db;

    public GroceryListServiceTests()
    {
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    private GroceryListService BuildService()
    {
        var groceryRepo = new Repository<GroceryList>(_db);
        // PantryService has complex deps but none of the tested methods call through it.
        // Provide minimal stub repositories backed by the same in-memory DbContext.
        var pantryItemRepo = new Repository<PantryItem>(_db);
        var pantryEntityRepo = new Repository<Pantry>(_db);
        var memberRepo = new Repository<PantryMember>(_db);
        var ingredientRepo = new Repository<Ingredient>(_db);
        var unitConverter = new StubUnitConverter();
        var pantryService = new PantryService(pantryItemRepo, pantryEntityRepo, memberRepo, ingredientRepo, unitConverter);
        return new GroceryListService(groceryRepo, pantryService);
    }

    [Fact]
    public async Task EnsurePrimaryListAsync_ReturnsMostRecent_WhenExisting()
    {
        // Arrange: seed a User (required by FK) then two GroceryLists with different CreatedAt.
        var user = new User { DisplayName = "TestUser" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var userId = user.Id;

        var older = new GroceryList { UserId = userId, Name = "Old list", CreatedAt = DateTime.UtcNow.AddDays(-5) };
        var newer = new GroceryList { UserId = userId, Name = "New list", CreatedAt = DateTime.UtcNow.AddDays(-1) };
        _db.GroceryLists.AddRange(older, newer);
        await _db.SaveChangesAsync();

        var service = BuildService();

        // Act
        var result = await service.EnsurePrimaryListAsync(userId);

        // Assert: should return the newer list (most recently created)
        Assert.Equal(newer.Id, result.Id);
    }

    [Fact]
    public async Task EnsurePrimaryListAsync_CreatesPantryQuickAdd_WhenNone()
    {
        // Arrange: seed a User (required by FK) but no grocery lists.
        var user = new User { DisplayName = "TestUser" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var userId = user.Id;

        var service = BuildService();

        // Act
        var result = await service.EnsurePrimaryListAsync(userId);

        // Assert: a new list was created with the canonical name and correct UserId.
        Assert.Equal("Pantry quick-add", result.Name);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(1, _db.GroceryLists.Count());
    }

    [Fact]
    public async Task AddItemAsync_AppendsGroceryListItem_WithIsPurchasedFalse()
    {
        // Arrange: seed a User (FK), one GroceryList, and one Ingredient.
        var user = new User { DisplayName = "TestUser" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var userId = user.Id;

        var list = new GroceryList { UserId = userId, Name = "Test list" };
        _db.GroceryLists.Add(list);
        var ingredient = new Ingredient { Name = "Flour", Category = CookBot.Domain.Enums.IngredientCategory.Grains };
        _db.Ingredients.Add(ingredient);
        await _db.SaveChangesAsync();

        var service = BuildService();

        // Act: amount is a double literal 2.0 (B-02 — NOT 2m or (decimal)2).
        await service.AddItemAsync(list.Id, ingredient.Id, amount: 2.0, unit: "cups");

        // Assert: the list contains one new GroceryListItem with correct fields.
        var savedList = await _db.GroceryLists
            .Include(gl => gl.Items)
            .FirstAsync(gl => gl.Id == list.Id);

        Assert.Single(savedList.Items);
        var item = savedList.Items.First();
        Assert.Equal(ingredient.Id, item.IngredientId);
        Assert.Equal(2.0, item.Amount);
        Assert.Equal("cups", item.Unit);
        // PATTERNS.md correction #3: the real column is IsPurchased (default false).
        Assert.False(item.IsPurchased);
    }

    /// <summary>
    /// Minimal stub IUnitConverter for PantryService construction.
    /// The tested GroceryListService methods do not exercise unit conversion.
    /// </summary>
    private class StubUnitConverter : CookBot.Domain.Interfaces.IUnitConverter
    {
        public bool CanConvert(string fromUnit, string toUnit) => false;
        public double? Convert(double amount, string fromUnit, string toUnit) => null;
        public bool IsVolume(string unit) => false;
        public bool IsWeight(string unit) => false;
    }
}
