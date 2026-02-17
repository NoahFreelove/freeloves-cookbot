namespace CookBot.Domain.Entities;

public class Recipe
{
    public int Id { get; set; }
    public int CookbookId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RawContent { get; set; } = string.Empty;
    public string MarkdownBody { get; set; } = string.Empty;
    public int Servings { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public string TagsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Cookbook Cookbook { get; set; } = null!;
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}
