using CookBot.Domain.Enums;

namespace CookBot.Domain.Entities;

public class PantryItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int IngredientId { get; set; }
    public double Amount { get; set; }
    public MeasurementUnit Unit { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
