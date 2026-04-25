using System.Text.Json;
using System.Text.Json.Serialization;

namespace CookBot.Domain.Recipes;

/// <summary>
/// A single ingredient line on a recipe. <see cref="Id"/> is the per-recipe local id used by
/// step-text <c>[name](#id)</c> markdown links; it was named "local-id" in v1 and was
/// renamed per D-06.
/// </summary>
public sealed record IngredientEntry
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("amount")]
    public double Amount { get; init; }

    [JsonPropertyName("unit")]
    public string Unit { get; init; } = "";

    [JsonPropertyName("note")]
    public string? Note { get; init; }

    /// <summary>Forward-compat: unknown ingredient-level keys round-trip per FORMAT-09.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extras { get; init; } = new();
}
