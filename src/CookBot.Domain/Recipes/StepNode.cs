using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CookBot.Domain.Recipes;

/// <summary>
/// Polymorphic step node. Discriminator <c>kind</c> selects between <see cref="ContentStep"/>
/// (instruction text + optional timers) and <see cref="SectionStep"/> (heading only).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ContentStep), typeDiscriminator: "content")]
[JsonDerivedType(typeof(SectionStep), typeDiscriminator: "section")]
public abstract record StepNode;

/// <summary>An instruction step with prose text and an optional timer list.</summary>
public sealed record ContentStep : StepNode
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("timers")]
    public IReadOnlyList<TimerEntry>? Timers { get; init; }

    [JsonPropertyName("temperature")]
    public StepTemperature? Temperature { get; init; }

    [JsonPropertyName("donenessCue")]
    [MaxLength(512)]
    public string? DonenessCue { get; init; }

    /// <summary>Forward-compat: unknown step-level keys round-trip per FORMAT-09.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extras { get; init; } = new();
}

/// <summary>A section break with a heading; carries no instruction text or timers.</summary>
public sealed record SectionStep : StepNode
{
    [JsonPropertyName("heading")]
    public required string Heading { get; init; }

    /// <summary>Forward-compat: unknown step-level keys round-trip per FORMAT-09.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extras { get; init; } = new();
}
