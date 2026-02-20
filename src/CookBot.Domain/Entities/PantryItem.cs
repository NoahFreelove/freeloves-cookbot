using CookBot.Domain.Enums;

namespace CookBot.Domain.Entities;

public class PantryItem
{
    public int Id { get; set; }
    public int PantryId { get; set; }
    public int IngredientId { get; set; }
    public double Amount { get; set; }
    public MeasurementUnit Unit { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Pantry Pantry { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
