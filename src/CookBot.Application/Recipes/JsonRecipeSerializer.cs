using System.Text.Json;
using System.Text.Json.Nodes;
using CookBot.Application.Recipes.Converters;
using CookBot.Domain.Recipes;

namespace CookBot.Application.Recipes;

/// <summary>
/// Canonical (de)serializer for <see cref="RecipeDocument"/>. Compact output for the
/// SQLite <c>CanonicalDocumentJson</c> column; indented output for human-readable export
/// per D-discretion.
///
/// Callers passing untrusted input to <see cref="Deserialize(string)"/> SHOULD size-limit
/// the input themselves; STJ's default <see cref="JsonSerializerOptions.MaxDepth"/> of 64
/// is sufficient for canonical recipes (3 levels of nesting).
/// </summary>
public sealed class JsonRecipeSerializer
{
    private readonly JsonSerializerOptions _compact;
    private readonly JsonSerializerOptions _indented;

    public JsonRecipeSerializer()
    {
        _compact = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        _indented = new JsonSerializerOptions(_compact)
        {
            WriteIndented = true,
        };
        _indented.Converters.Add(new StepTemperatureJsonConverter());
    }

    /// <summary>Compact JSON, suitable for the DB column.</summary>
    public string Serialize(RecipeDocument doc) => JsonSerializer.Serialize(doc, _compact);

    /// <summary>Pretty-printed JSON, suitable for cookbook export and copy-to-clipboard surfaces.</summary>
    public string SerializeIndented(RecipeDocument doc) => JsonSerializer.Serialize(doc, _indented);

    /// <summary>Deserialize a canonical document from an in-memory <see cref="JsonNode"/>.</summary>
    public RecipeDocument Deserialize(JsonNode node) => node.Deserialize<RecipeDocument>(_compact)!;

    /// <summary>Deserialize a canonical document from a JSON string. Caller MUST size-limit untrusted input.</summary>
    public RecipeDocument Deserialize(string json) => JsonSerializer.Deserialize<RecipeDocument>(json, _compact)!;
}
