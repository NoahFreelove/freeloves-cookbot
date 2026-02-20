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
    private readonly IRepository<Pantry> _pantryEntityRepo;
    private readonly IRepository<PantryMember> _memberRepo;
    private readonly IRepository<Ingredient> _ingredientRepo;
    private readonly IUnitConverter _unitConverter;

    public PantryService(
        IRepository<PantryItem> pantryRepo,
        IRepository<Pantry> pantryEntityRepo,
        IRepository<PantryMember> memberRepo,
        IRepository<Ingredient> ingredientRepo,
        IUnitConverter unitConverter)
    {
        _pantryRepo = pantryRepo;
        _pantryEntityRepo = pantryEntityRepo;
        _memberRepo = memberRepo;
        _ingredientRepo = ingredientRepo;
        _unitConverter = unitConverter;
    }

    public async Task<IReadOnlyList<PantryItem>> GetPantryItemsAsync(int pantryId) =>
        await _pantryRepo.FindAsync(p => p.PantryId == pantryId);

    public async Task<Pantry?> GetPersonalPantryAsync(int userId)
    {
        var pantries = await _pantryEntityRepo.FindAsync(p => p.OwnerId == userId && p.IsPersonal);
        return pantries.FirstOrDefault();
    }

    public async Task<List<Pantry>> GetAccessiblePantriesAsync(int userId)
    {
        var owned = await _pantryEntityRepo.FindAsync(p => p.OwnerId == userId);
        var memberships = await _memberRepo.FindAsync(m => m.UserId == userId);
        var memberPantryIds = memberships.Select(m => m.PantryId).ToHashSet();

        var memberPantries = new List<Pantry>();
        foreach (var id in memberPantryIds)
        {
            var pantry = await _pantryEntityRepo.GetByIdAsync(id);
            if (pantry != null)
                memberPantries.Add(pantry);
        }

        return owned.Concat(memberPantries.Where(p => !owned.Any(o => o.Id == p.Id)))
            .OrderByDescending(p => p.IsPersonal)
            .ThenBy(p => p.Name)
            .ToList();
    }

    public async Task<List<PantryItem>> GetAllUserAccessibleItemsAsync(int userId)
    {
        var pantries = await GetAccessiblePantriesAsync(userId);
        var allItems = new List<PantryItem>();
        foreach (var pantry in pantries)
        {
            var items = await _pantryRepo.FindAsync(p => p.PantryId == pantry.Id);
            allItems.AddRange(items);
        }
        return allItems;
    }

    public async Task AddOrUpdateAsync(int pantryId, int ingredientId, double amount, MeasurementUnit unit, DateTime? expiration)
    {
        var existing = (await _pantryRepo.FindAsync(p => p.PantryId == pantryId && p.IngredientId == ingredientId)).FirstOrDefault();
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
                PantryId = pantryId,
                IngredientId = ingredientId,
                Amount = amount,
                Unit = unit,
                ExpirationDate = expiration,
            });
        }
    }

    public async Task DeductAsync(int pantryId, int ingredientId, double amount, MeasurementUnit unit)
    {
        var item = (await _pantryRepo.FindAsync(p => p.PantryId == pantryId && p.IngredientId == ingredientId)).FirstOrDefault();
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
        var allItems = await GetAllUserAccessibleItemsAsync(userId);
        var result = new List<IngredientStatus>();

        foreach (var ri in recipeIngredients)
        {
            var matchingItems = allItems.Where(p => p.IngredientId == ri.IngredientId).ToList();
            var status = new IngredientStatus
            {
                RecipeIngredient = ri,
                PantryItem = matchingItems.FirstOrDefault(),
                NeededAmount = ri.Amount,
            };

            if (!matchingItems.Any())
            {
                status.Availability = IngredientAvailability.Missing;
                status.MissingAmount = ri.Amount;
            }
            else
            {
                double totalAvailable = 0;
                bool anyCompatible = false;

                foreach (var pantryItem in matchingItems)
                {
                    if (_unitConverter.CanConvert(pantryItem.Unit, ri.Unit))
                    {
                        totalAvailable += _unitConverter.Convert(pantryItem.Amount, pantryItem.Unit, ri.Unit);
                        anyCompatible = true;
                    }
                }

                if (!anyCompatible)
                {
                    status.Availability = IngredientAvailability.IncompatibleUnits;
                }
                else
                {
                    status.AvailableAmount = totalAvailable;

                    if (totalAvailable >= ri.Amount)
                    {
                        status.Availability = IngredientAvailability.Available;
                    }
                    else
                    {
                        status.Availability = IngredientAvailability.PartiallyAvailable;
                        status.MissingAmount = ri.Amount - totalAvailable;
                    }
                }
            }

            result.Add(status);
        }

        return result;
    }

    public async Task<Pantry> CreateSharedPantryAsync(int ownerUserId, string name)
    {
        return await _pantryEntityRepo.AddAsync(new Pantry
        {
            Name = name,
            OwnerId = ownerUserId,
            IsPersonal = false,
        });
    }

    public async Task AddMemberAsync(int pantryId, int userId)
    {
        var existing = (await _memberRepo.FindAsync(m => m.PantryId == pantryId && m.UserId == userId)).FirstOrDefault();
        if (existing != null) return;

        await _memberRepo.AddAsync(new PantryMember
        {
            PantryId = pantryId,
            UserId = userId,
        });
    }

    public async Task RemoveMemberAsync(int pantryId, int userId)
    {
        var member = (await _memberRepo.FindAsync(m => m.PantryId == pantryId && m.UserId == userId)).FirstOrDefault();
        if (member != null)
            await _memberRepo.DeleteAsync(member);
    }

    public async Task<Pantry> EnsurePersonalPantryAsync(int userId)
    {
        var existing = await GetPersonalPantryAsync(userId);
        if (existing != null) return existing;

        return await _pantryEntityRepo.AddAsync(new Pantry
        {
            Name = "Personal Pantry",
            OwnerId = userId,
            IsPersonal = true,
        });
    }
}
