using CookBot.Domain.Entities;
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

    /// <summary>
    /// Returns the most-recently-created grocery list for <paramref name="userId"/>,
    /// or creates a new list named "Pantry quick-add" if none exists.
    ///
    /// Phase 10 / Plan 10-11 / POLISH-02. Closes the design gap (no "primary list"
    /// concept existed before this method). The returned list is always owned by
    /// the caller's userId — the call site (PantryView) is responsible for passing
    /// the current user's id.
    /// </summary>
    public async Task<GroceryList> EnsurePrimaryListAsync(int userId)
    {
        var existing = await _groceryRepo.FindAsync(g => g.UserId == userId);
        var open = existing.OrderByDescending(g => g.CreatedAt).FirstOrDefault();
        if (open is not null) return open;
        var fresh = new GroceryList { UserId = userId, Name = "Pantry quick-add" };
        return await _groceryRepo.AddAsync(fresh);
    }

    /// <summary>
    /// Appends a new <see cref="GroceryListItem"/> to the specified grocery list.
    ///
    /// Phase 10 / Plan 10-11 / POLISH-02. Closes the design gap (no AddItem method
    /// existed before; GenerateFromRecipeAsync was the only item-population path).
    ///
    /// The <paramref name="amount"/> parameter is typed <c>double</c> to match
    /// <see cref="GroceryListItem.Amount"/> (B-02 corrected — the entity column is
    /// double, not decimal). IsPurchased defaults to false; no explicit set needed.
    /// Authorization: mirrors GenerateFromRecipeAsync which trusts the caller's userId;
    /// PantryView gates pantry access via PantryService before reaching this method.
    /// </summary>
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
            // PATTERNS.md correction #3: the column is IsPurchased (not a different name).
        });
        await _groceryRepo.UpdateAsync(list);
    }
}
