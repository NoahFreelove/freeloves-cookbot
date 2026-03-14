namespace CookBot.Domain.Entities;

public class RecipeStep
{
    public int Order { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsSection { get; set; }
    public List<StepTimer> Timers { get; set; } = new();
    public List<int> IngredientRefs { get; set; } = new();
}
