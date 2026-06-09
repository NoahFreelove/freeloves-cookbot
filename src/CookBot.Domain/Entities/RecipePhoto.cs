namespace CookBot.Domain.Entities;

/// <summary>
/// A single photo associated with a recipe (GALLERY-01 / Phase 14).
/// Ordered by <see cref="SortOrder"/>; exactly one photo per recipe should have <see cref="IsPrimary"/> = true.
/// <see cref="Url"/> holds either a local <c>/uploads/{guid}.ext</c> path (uploaded)
/// or an external <c>http(s)://</c> URL (pasted) — same dual-source model as <c>Recipe.PhotoUrl</c>.
/// </summary>
public class RecipePhoto
{
    public int Id { get; set; }
    public int RecipeId { get; set; }

    /// <summary>Local "/uploads/{guid}.ext" OR external "http(s)://" URL. Max 2048 chars (matches Recipe.PhotoUrl).</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Optional caption displayed beneath the photo. Max 512 chars.</summary>
    public string? Caption { get; set; }

    /// <summary>Zero-based display order within the recipe's gallery.</summary>
    public int SortOrder { get; set; }

    /// <summary>True for the one photo that mirrors <c>Recipe.PhotoUrl</c> as the hero.</summary>
    public bool IsPrimary { get; set; }

    public Recipe Recipe { get; set; } = null!;
}
