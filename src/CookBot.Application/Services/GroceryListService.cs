using CookBot.Domain.Entities;
using CookBot.Domain.Enums;
using CookBot.Domain.Interfaces;

namespace CookBot.Application.Services;

public class GroceryListService
{
    private readonly IRepository<GroceryList> _groceryRepo;
    private readonly PantryService _pantryService;

    public GroceryListService(IRepository<GroceryList> groceryRepo, PantryService pantryService)
    {
        _groceryRepo = groceryRepo;
        _pantryService = pantryService;
    }

    public async Task<IReadOnlyList<GroceryList>> GetUserListsAsync(int userId) =>
        await _groceryRepo.FindAsync(g => g.UserId == userId);

    public async Task<GroceryList> GenerateFromRecipeAsync(int userId, Recipe recipe, double scaleFactor = 1.0)
    {
        var statuses = await _pantryService.CheckAvailabilityForRecipeAsync(userId, recipe.RecipeIngredients);

        var groceryList = new GroceryList
        {
            UserId = userId,
            Name = $"Shopping for: {recipe.Name}",
        };

        foreach (var status in statuses)
        {
            if (status.Availability == IngredientAvailability.Missing ||
                status.Availability == IngredientAvailability.PartiallyAvailable)
            {
                var neededAmount = status.MissingAmount * scaleFactor;
                groceryList.Items.Add(new GroceryListItem
                {
                    IngredientId = status.RecipeIngredient.IngredientId,
                    Amount = Math.Round(neededAmount, 2),
                    Unit = status.RecipeIngredient.Unit,
                });
            }
            else if (status.Availability == IngredientAvailability.IncompatibleUnits)
            {
                groceryList.Items.Add(new GroceryListItem
                {
                    IngredientId = status.RecipeIngredient.IngredientId,
                    Amount = Math.Round(status.RecipeIngredient.Amount * scaleFactor, 2),
                    Unit = status.RecipeIngredient.Unit,
                });
            }
        }

        if (groceryList.Items.Any())
        {
            return await _groceryRepo.AddAsync(groceryList);
        }

        return groceryList;
    }

    public async Task<GroceryList> GenerateAllFromRecipeAsync(int userId, Recipe recipe, double scaleFactor = 1.0)
    {
        var groceryList = new GroceryList
        {
            UserId = userId,
            Name = $"All items: {recipe.Name}",
        };

        foreach (var ri in recipe.RecipeIngredients)
        {
            groceryList.Items.Add(new GroceryListItem
            {
                IngredientId = ri.IngredientId,
                Amount = Math.Round(ri.Amount * scaleFactor, 2),
                Unit = ri.Unit,
            });
        }

        if (groceryList.Items.Any())
        {
            return await _groceryRepo.AddAsync(groceryList);
        }

        return groceryList;
    }

    public async Task DeleteAsync(GroceryList list) =>
        await _groceryRepo.DeleteAsync(list);
}
