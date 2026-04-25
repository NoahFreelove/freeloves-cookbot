using System.Text.Json;
using System.Text.Json.Serialization;

namespace CookBot.Domain.Recipes;

/// <summary>
/// Canonical recipe document — the single source of truth for the recipe shape across the
/// AI prompt, JSON cookbook export, and the SQLite-backed canonical document column.
/// Pure POCO; no framework references; STJ attributes only.
/// </summary>
public sealed record RecipeDocument
{
    [JsonPropertyName("version")]
    public required int Version { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("servings")]
    public int Servings { get; init; } = 1;

    [JsonPropertyName("prepTimeMinutes")]
    public int? PrepTimeMinutes { get; init; }

    [JsonPropertyName("cookTimeMinutes")]
    public int? CookTimeMinutes { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    [JsonPropertyName("ingredients")]
    public IReadOnlyList<IngredientEntry> Ingredients { get; init; } = [];

    [JsonPropertyName("steps")]
    public IReadOnlyList<StepNode> Steps { get; init; } = [];

    /// <summary>Forward-compat: unknown top-level keys round-trip through serialize/deserialize (FORMAT-09).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extras { get; init; } = new();
}
