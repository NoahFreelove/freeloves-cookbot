using System.Text.Json.Serialization;

namespace CookBot.Domain.Recipes;

/// <summary>A timer attached to a <see cref="ContentStep"/>: a duration with a unit and optional label.</summary>
public sealed record TimerEntry
{
    [JsonPropertyName("duration")]
    public required int Duration { get; init; }

    [JsonPropertyName("unit")]
    public string Unit { get; init; } = "min";

    [JsonPropertyName("label")]
    public string? Label { get; init; }
}
