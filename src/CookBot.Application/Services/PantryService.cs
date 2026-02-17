using CookBot.Domain.Entities;
using CookBot.Domain.Enums;
using CookBot.Domain.Interfaces;

namespace CookBot.Application.Services;

public enum IngredientAvailability
{
    Available,
    PartiallyAvailable,
    Missing,
    IncompatibleUnits
}

public class IngredientStatus
{
    public RecipeIngredient RecipeIngredient { get; set; } = null!;
    public PantryItem? PantryItem { get; set; }
    public IngredientAvailability Availability { get; set; }
    public double NeededAmount { get; set; }
    public double AvailableAmount { get; set; }
    public double MissingAmount { get; set; }
}

public class PantryService
{
    private readonly IRepository<PantryItem> _pantryRepo;
    private readonly IRepository<Ingredient> _ingredientRepo;
    private readonly IUnitConverter _unitConverter;

    public PantryService(IRepository<PantryItem> pantryRepo, IRepository<Ingredient> ingredientRepo, IUnitConverter unitConverter)
    {
        _pantryRepo = pantryRepo;
        _ingredientRepo = ingredientRepo;
        _unitConverter = unitConverter;
    }

    public async Task<IReadOnlyList<PantryItem>> GetUserPantryAsync(int userId) =>
        await _pantryRepo.FindAsync(p => p.UserId == userId);

    public async Task AddOrUpdateAsync(int userId, int ingredientId, double amount, MeasurementUnit unit, DateTime? expiration)
    {
        var existing = (await _pantryRepo.FindAsync(p => p.UserId == userId && p.IngredientId == ingredientId)).FirstOrDefault();
        if (existing != null)
        {
            if (_unitConverter.CanConvert(unit, existing.Unit))
            {
                existing.Amount += _unitConverter.Convert(amount, unit, existing.Unit);
            }
            else
            {
                existing.Amount = amount;
                existing.Unit = unit;
            }
            existing.ExpirationDate = expiration ?? existing.ExpirationDate;
            existing.UpdatedAt = DateTime.UtcNow;
            await _pantryRepo.UpdateAsync(existing);
        }
        else
        {
            await _pantryRepo.AddAsync(new PantryItem
            {
                UserId = userId,
                IngredientId = ingredientId,
                Amount = amount,
                Unit = unit,
                ExpirationDate = expiration,
            });
        }
    }

    public async Task DeductAsync(int userId, int ingredientId, double amount, MeasurementUnit unit)
    {
        var item = (await _pantryRepo.FindAsync(p => p.UserId == userId && p.IngredientId == ingredientId)).FirstOrDefault();
        if (item == null) return;

        double deductAmount = amount;
        if (_unitConverter.CanConvert(unit, item.Unit))
        {
            deductAmount = _unitConverter.Convert(amount, unit, item.Unit);
        }

        item.Amount = Math.Max(0, item.Amount - deductAmount);
        item.UpdatedAt = DateTime.UtcNow;

        if (item.Amount <= 0)
            await _pantryRepo.DeleteAsync(item);
        else
            await _pantryRepo.UpdateAsync(item);
    }

    public async Task<List<IngredientStatus>> CheckAvailabilityForRecipeAsync(int userId, ICollection<RecipeIngredient> recipeIngredients)
    {
        var pantryItems = await _pantryRepo.FindAsync(p => p.UserId == userId);
        var result = new List<IngredientStatus>();

        foreach (var ri in recipeIngredients)
        {
            var pantryItem = pantryItems.FirstOrDefault(p => p.IngredientId == ri.IngredientId);
            var status = new IngredientStatus
            {
                RecipeIngredient = ri,
                PantryItem = pantryItem,
                NeededAmount = ri.Amount,
            };

            if (pantryItem == null)
            {
                status.Availability = IngredientAvailability.Missing;
                status.MissingAmount = ri.Amount;
            }
            else if (!_unitConverter.CanConvert(ri.Unit, pantryItem.Unit))
            {
                status.Availability = IngredientAvailability.IncompatibleUnits;
            }
            else
            {
                var availableInRecipeUnits = _unitConverter.Convert(pantryItem.Amount, pantryItem.Unit, ri.Unit);
                status.AvailableAmount = availableInRecipeUnits;

                if (availableInRecipeUnits >= ri.Amount)
                {
                    status.Availability = IngredientAvailability.Available;
                }
                else
                {
                    status.Availability = IngredientAvailability.PartiallyAvailable;
                    status.MissingAmount = ri.Amount - availableInRecipeUnits;
                }
            }

            result.Add(status);
        }

        return result;
    }
}
