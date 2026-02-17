using CookBot.Domain.Enums;

namespace CookBot.Domain.Entities;

public class GroceryListItem
{
    public int Id { get; set; }
    public int GroceryListId { get; set; }
    public int IngredientId { get; set; }
    public double Amount { get; set; }
    public MeasurementUnit Unit { get; set; }
    public bool IsPurchased { get; set; }

    public GroceryList GroceryList { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
