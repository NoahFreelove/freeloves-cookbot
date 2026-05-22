using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
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
        // JsonSchemaExporter requires the options to carry a TypeInfoResolver before being
        // marked read-only; pin DefaultJsonTypeInfoResolver explicitly so reflection-based
        // metadata is available at schema-export time.
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
        };
        // NOTE: Do NOT set JsonUnmappedMemberHandling.Disallow on serializerOptions —
        // it would auto-emit additionalProperties:false but also reject unknown members
        // at deserialization, which contradicts FORMAT-09 Extras round-trip.
        var node = serializerOptions.GetJsonSchemaAsNode(typeof(RecipeDocument), exporterOptions);
        SetAdditionalPropertiesFalse(node);
        ExternalizeAnyOfBranches(node);
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

    // Keywords Anthropic structured-outputs forbids inside anyOf branches.
    // Branches carrying any of these must be externalized to $defs and replaced with $ref.
    private static readonly string[] ForbiddenInAnyOfBranch =
    {
        "type", "properties", "required", "additionalProperties",
    };

    /// <summary>
    /// Anthropic strict structured-outputs rejects inline <c>type</c>/<c>properties</c>/<c>required</c>/
    /// <c>additionalProperties</c> inside an <c>anyOf</c> branch — the branch must reduce to a
    /// <c>$ref</c> (or to a pure null sentinel). Walks the schema; for every <c>anyOf</c> array
    /// branch that carries any forbidden keyword, lifts the branch to <c>$defs/&lt;name&gt;</c> and
    /// replaces it with <c>{ "$ref": "#/$defs/&lt;name&gt;" }</c>. Idempotent — a second pass over
    /// branches that are already <c>$ref</c> is a no-op.
    /// </summary>
    private static void ExternalizeAnyOfBranches(JsonNode? root)
    {
        if (root is not JsonObject rootObj) return;

        var defs = rootObj["$defs"] as JsonObject;
        if (defs is null)
        {
            defs = new JsonObject();
            // Defer attaching $defs until we actually emit one — keeps the schema clean
            // when there are no anyOf branches to externalize.
        }

        var counter = 0;
        ExternalizeWalk(rootObj, defs, ref counter, isInsideDefs: false);

        if (defs.Count > 0 && rootObj["$defs"] is null)
        {
            rootObj["$defs"] = defs;
        }
    }

    private static void ExternalizeWalk(JsonNode? node, JsonObject defs, ref int counter, bool isInsideDefs)
    {
        if (node is JsonObject obj)
        {
            if (obj["anyOf"] is JsonArray anyOf)
            {
                for (int i = 0; i < anyOf.Count; i++)
                {
                    if (anyOf[i] is JsonObject branch && BranchNeedsExternalizing(branch))
                    {
                        var defName = ChooseDefName(branch, defs, ref counter);
                        // Detach by deep-cloning then dropping into $defs; replace branch with $ref.
                        defs[defName] = branch.DeepClone();
                        anyOf[i] = new JsonObject { ["$ref"] = $"#/$defs/{defName}" };
                    }
                }
            }

            foreach (var kvp in obj.ToList())
            {
                // Skip recursing into $defs at the root — those entries are intentionally
                // un-rewritten templates that anyOf branches point at via $ref.
                if (!isInsideDefs && ReferenceEquals(kvp.Value, defs))
                {
                    continue;
                }
                ExternalizeWalk(kvp.Value, defs, ref counter, isInsideDefs || kvp.Key == "$defs");
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var child in arr)
            {
                ExternalizeWalk(child, defs, ref counter, isInsideDefs);
            }
        }
    }

    private static bool BranchNeedsExternalizing(JsonObject branch)
    {
        foreach (var key in ForbiddenInAnyOfBranch)
        {
            if (branch.ContainsKey(key)) return true;
        }
        return false;
    }

    /// <summary>
    /// Picks a stable, readable name for an externalized anyOf branch. Prefers a discriminator
    /// const value (e.g. <c>kind: { const: "content" } → "kind_content"</c>); falls back to a
    /// monotonic counter. De-duplicates against existing <c>$defs</c> keys.
    /// </summary>
    private static string ChooseDefName(JsonObject branch, JsonObject defs, ref int counter)
    {
        string baseName = $"Variant_{counter++}";

        if (branch["properties"] is JsonObject props)
        {
            foreach (var kvp in props)
            {
                if (kvp.Value is JsonObject p
                    && p["const"] is JsonValue cv
                    && cv.TryGetValue<string>(out var s)
                    && !string.IsNullOrEmpty(s))
                {
                    baseName = $"{kvp.Key}_{s}";
                    break;
                }
            }
        }

        var name = baseName;
        var dedup = 2;
        while (defs.ContainsKey(name))
        {
            name = $"{baseName}_{dedup++}";
        }
        return name;
    }
}
