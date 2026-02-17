using CookBot.Domain.Enums;

namespace CookBot.Domain.Entities;

public class RecipeIngredient
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public int IngredientId { get; set; }
    public int RecipeLocalId { get; set; }
    public double Amount { get; set; }
    public MeasurementUnit Unit { get; set; }
    public string? Note { get; set; }

    public Recipe Recipe { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
