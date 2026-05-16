using System.Text.Json.Serialization;

namespace CookBot.Domain.Recipes;

/// <summary>A per-step oven or hob temperature attached to a <see cref="ContentStep"/>.</summary>
public sealed record StepTemperature
{
    [JsonPropertyName("value")]
    public required decimal Value { get; init; }

    [JsonPropertyName("unit")]
    public required TemperatureUnit Unit { get; init; }
}

/// <summary>Temperature scale for a <see cref="StepTemperature"/> value.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TemperatureUnit>))]
public enum TemperatureUnit
{
    F,
    C,
    Gas,
}
