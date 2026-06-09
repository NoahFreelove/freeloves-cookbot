using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CookBot.Domain.Recipes;

/// <summary>
/// Source / provenance metadata for a recipe — all fields optional (D-12-07).
/// Modeled on <see cref="StepTemperature"/> conventions.
/// </summary>
public sealed record RecipeProvenance
{
    [JsonPropertyName("sourceUrl")]
    [MaxLength(2048)]
    public string? SourceUrl { get; init; }

    [JsonPropertyName("authorName")]
    [MaxLength(256)]
    public string? AuthorName { get; init; }

    [JsonPropertyName("sourceName")]
    [MaxLength(512)]
    public string? SourceName { get; init; }

    /// <summary>Forward-compat: unknown provenance-level keys round-trip per FORMAT-09.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extras { get; init; } = new();
}
