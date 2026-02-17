using CookBot.Domain.Enums;

namespace CookBot.Domain.Entities;

public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public IngredientCategory Category { get; set; } = IngredientCategory.Other;

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<PantryItem> PantryItems { get; set; } = new List<PantryItem>();
}
