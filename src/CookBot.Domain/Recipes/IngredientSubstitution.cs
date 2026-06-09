using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CookBot.Domain.Recipes;

/// <summary>
/// A substitution option for a single ingredient. Carries a required freeform <see cref="Note"/>
/// plus optional structured <see cref="Name"/>, <see cref="Amount"/>, and <see cref="Unit"/>.
/// Modeled on <see cref="IngredientEntry"/> conventions (FORMAT-01, D-12-01..03).
/// </summary>
public sealed record IngredientSubstitution
{
    [JsonPropertyName("note")]
    [MaxLength(512)]
    public required string Note { get; init; }

    [JsonPropertyName("name")]
    [MaxLength(256)]
    public string? Name { get; init; }

    [JsonPropertyName("amount")]
    public double? Amount { get; init; }

    [JsonPropertyName("unit")]
    [MaxLength(64)]
    public string? Unit { get; init; }

    /// <summary>Forward-compat: unknown substitution-level keys round-trip per FORMAT-09.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extras { get; init; } = new();
}
