using System.Text.Json.Serialization;

namespace CookBot.Tests.AI;

/// <summary>
/// Strongly-typed shape for the .golden.json fixture expectation files.
/// Authored once; locks the .json shape against drift between fixtures and tests.
/// Optional fields default to null (no upper bound, no opinion).
/// </summary>
public sealed record FixtureGolden
{
    [JsonPropertyName("ingredientCountMin")]
    public int IngredientCountMin { get; init; }

    [JsonPropertyName("ingredientCountMax")]
    public int? IngredientCountMax { get; init; }

    [JsonPropertyName("stepCountMin")]
    public int StepCountMin { get; init; }

    [JsonPropertyName("stepCountMax")]
    public int? StepCountMax { get; init; }

    [JsonPropertyName("hasSections")]
    public bool HasSections { get; init; }

    [JsonPropertyName("hasTimers")]
    public bool? HasTimers { get; init; }
}
