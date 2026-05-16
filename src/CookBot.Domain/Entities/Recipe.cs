namespace CookBot.Domain.Entities;

public class Recipe
{
    public int Id { get; set; }
    public int CookbookId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Servings { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public string TagsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// Canonical RecipeDocument JSON snapshot, recomputed on every save (Phase 1 / D-12).
    /// Nullable until backfill completes; once populated, this is the export/AI/import authority.
    /// </summary>
    public string? CanonicalDocumentJson { get; set; }
    /// <summary>URL of the hero photo for this recipe (phase 8 / SCHEMA-05). Nullable — existing recipes have no photo.</summary>
    public string? PhotoUrl { get; set; }
    /// <summary>Short description of the recipe (phase 8 / SCHEMA-06). Nullable — existing recipes have no description.</summary>
    public string? Description { get; set; }

    public Cookbook Cookbook { get; set; } = null!;
    public List<RecipeStep> Steps { get; set; } = new();
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}
