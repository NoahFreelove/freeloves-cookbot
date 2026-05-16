namespace CookBot.Domain.Entities;

/// <summary>A tag attached to a <see cref="Recipe"/>; the relational replacement for the legacy TagsJson column (Phase 8 CLEAN-02).</summary>
public class RecipeTag
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public string Name { get; set; } = string.Empty;

    public Recipe Recipe { get; set; } = null!;
}
