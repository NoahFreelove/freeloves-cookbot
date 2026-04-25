using System.Text.Json.Nodes;

namespace CookBot.Application.Recipes;

/// <summary>
/// One step in the recipe-version upcaster chain. Operates at the JSON-node layer (D-09)
/// to avoid the typed-deserialize-then-rebuild round-trip and to keep upcasters
/// dependency-free of the typed records.
/// </summary>
public interface IRecipeUpcaster
{
    /// <summary>Recipe version this upcaster reads as input.</summary>
    int FromVersion { get; }

    /// <summary>Recipe version this upcaster produces.</summary>
    int ToVersion { get; }

    /// <summary>Rewrite the input node and return the new shape. May mutate the input.</summary>
    JsonNode Upcast(JsonNode input);
}
