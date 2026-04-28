namespace CookBot.Domain.Entities;

/// <summary>
/// Log row written when a user finishes cooking a recipe (Plan 07-09 Feature 2).
/// Powers the v1.2 "Recently cooked" tile on Home and the "Made N×" stat in RecipeView,
/// closing FUTURE-Recently-Cooked from the v1.2 milestone summary.
/// </summary>
public class RecipeMade
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public int UserId { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public Recipe Recipe { get; set; } = null!;
    public User User { get; set; } = null!;
}
