using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using CookBot.Domain.Recipes;

namespace CookBot.Application.Recipes;

/// <summary>
/// Generates and caches the JSON Schema 2020-12 document for <see cref="RecipeDocument"/>
/// using the BCL <c>JsonSchemaExporter</c>. After generation, post-walks the resulting
/// <see cref="JsonNode"/> setting <c>additionalProperties: false</c> on every object
/// subschema (Anthropic strict-mode requirement; STJ does not emit this by default).
/// </summary>
public sealed class RecipeJsonSchemaProvider
{
    private readonly Lazy<JsonNode> _schema;

    public RecipeJsonSchemaProvider()
    {
        _schema = new Lazy<JsonNode>(BuildSchema);
    }

    /// <summary>Returns the cached schema node. Lazy-built on first call.</summary>
    public JsonNode GetSchema() => _schema.Value;

    private static JsonNode BuildSchema()
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
        };
        // NOTE: Do NOT set JsonUnmappedMemberHandling.Disallow on serializerOptions —
        // it would auto-emit additionalProperties:false but also reject unknown members
        // at deserialization, which contradicts FORMAT-09 Extras round-trip.
        var node = serializerOptions.GetJsonSchemaAsNode(typeof(RecipeDocument), exporterOptions);
        SetAdditionalPropertiesFalse(node);
        return node;
    }

    /// <summary>
    /// Walks every object schema node in the tree and sets <c>additionalProperties: false</c>.
    /// Required by Anthropic Structured Outputs strict mode.
    /// </summary>
    private static void SetAdditionalPropertiesFalse(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            if (HasObjectType(obj))
            {
                if (obj["additionalProperties"] is null)
                {
                    obj["additionalProperties"] = false;
                }
            }

            foreach (var kvp in obj.ToList())
            {
                SetAdditionalPropertiesFalse(kvp.Value);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var child in arr)
            {
                SetAdditionalPropertiesFalse(child);
            }
        }
    }

    private static bool HasObjectType(JsonObject obj)
    {
        if (obj["type"] is JsonValue v && v.TryGetValue<string>(out var s))
        {
            return s == "object";
        }
        if (obj["type"] is JsonArray a)
        {
            foreach (var item in a)
            {
                if (item is JsonValue v2 && v2.TryGetValue<string>(out var s2) && s2 == "object")
                {
                    return true;
                }
            }
        }
        // anyOf branches and properties dictionaries can be object subschemas without a top-level type
        return obj.ContainsKey("properties");
    }
}
