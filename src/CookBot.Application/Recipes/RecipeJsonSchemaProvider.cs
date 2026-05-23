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
        //
        // CRITICAL: do NOT use JsonSerializerDefaults.Web here. The Web preset turns on
        // NumberHandling.AllowReadingFromString, which causes the exporter to emit every
        // numeric field as a `"type": ["string","integer"]` (or "string"/"number") union
        // with a regex pattern. Anthropic's structured-outputs grammar compiler chokes on
        // these unions ("Grammar compilation timed out") — see Phase 10 UAT Test 4 retest 3.
        // We still want camelCase property naming, so set that explicitly.
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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
    /// Anthropic strict structured-outputs has two restrictions around <c>anyOf</c>:
    ///   1. Branches inside <c>anyOf</c> may not carry inline <c>type</c>/<c>properties</c>/
    ///      <c>required</c>/<c>additionalProperties</c> — they must reduce to a <c>$ref</c>
    ///      (or a pure null sentinel).
    ///   2. <c>type</c>/<c>required</c>/<c>additionalProperties</c> may not appear as
    ///      <i>siblings</i> of <c>anyOf</c> on the parent object either — the entire shape
    ///      must live in the referenced <c>$defs</c> entry, not split across parent + branches.
    /// This pass walks the schema, lifts each non-conforming branch into
    /// <c>$defs/&lt;name&gt;</c>, hoists the parent's sibling constraints into every branch's
    /// <c>$defs</c> entry, and strips them from the parent so it becomes a thin
    /// <c>{ "anyOf": [...] }</c>. Idempotent — a re-run over an already-normalized schema
    /// is a no-op.
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

        // Second pass: enforce the sibling-keyword restriction. Must run AFTER externalization
        // so the $defs targets exist and can absorb the hoisted constraints.
        if (rootObj["$defs"] is JsonObject finalDefs)
        {
            NormalizeAnyOfParents(rootObj, finalDefs, isInsideDefs: false);
        }
    }

    /// <summary>
    /// For every object with an <c>anyOf</c> array, hoists parent-level <c>type</c>/
    /// <c>required</c>/<c>additionalProperties</c> into each <c>$ref</c>'d <c>$defs</c>
    /// entry (so the constraints are preserved by the union) and then strips those keys
    /// from the parent. After this pass, no <c>anyOf</c> appears as a sibling of those
    /// keywords — the parent reduces to <c>{ "anyOf": [...] }</c>.
    /// </summary>
    private static void NormalizeAnyOfParents(JsonNode? node, JsonObject defs, bool isInsideDefs)
    {
        if (node is JsonObject obj)
        {
            if (obj["anyOf"] is JsonArray anyOf)
            {
                // Snapshot parent siblings before mutating.
                var parentType = obj["type"]?.DeepClone();
                var parentRequired = obj["required"] as JsonArray;
                var parentAdditional = obj["additionalProperties"]?.DeepClone();

                if (parentType is not null || parentRequired is not null || parentAdditional is not null)
                {
                    foreach (var branch in anyOf)
                    {
                        if (branch is JsonObject bo
                            && bo["$ref"] is JsonValue refVal
                            && refVal.TryGetValue<string>(out var refStr)
                            && refStr.StartsWith("#/$defs/", StringComparison.Ordinal))
                        {
                            var name = refStr["#/$defs/".Length..];
                            if (defs[name] is JsonObject target)
                            {
                                HoistSiblingsIntoBranch(target, parentType, parentRequired, parentAdditional);
                            }
                        }
                    }

                    obj.Remove("type");
                    obj.Remove("required");
                    obj.Remove("additionalProperties");
                }
            }

            foreach (var kvp in obj.ToList())
            {
                NormalizeAnyOfParents(kvp.Value, defs, isInsideDefs || kvp.Key == "$defs");
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var child in arr)
            {
                NormalizeAnyOfParents(child, defs, isInsideDefs);
            }
        }
    }

    /// <summary>
    /// Merges parent <c>type</c>/<c>required</c>/<c>additionalProperties</c> into a single
    /// <c>$defs</c> entry. Branch's own value wins where both are set; <c>required</c> is
    /// merged as a set union to preserve discriminator + branch-specific required-ness.
    /// </summary>
    private static void HoistSiblingsIntoBranch(
        JsonObject target,
        JsonNode? parentType,
        JsonArray? parentRequired,
        JsonNode? parentAdditional)
    {
        if (parentType is not null && target["type"] is null)
        {
            target["type"] = parentType.DeepClone();
        }

        if (parentAdditional is not null && target["additionalProperties"] is null)
        {
            target["additionalProperties"] = parentAdditional.DeepClone();
        }

        if (parentRequired is not null)
        {
            var existing = target["required"] as JsonArray;
            if (existing is null)
            {
                target["required"] = parentRequired.DeepClone();
            }
            else
            {
                var seen = new HashSet<string>();
                foreach (var item in existing)
                {
                    if (item is JsonValue iv && iv.TryGetValue<string>(out var s)) seen.Add(s);
                }
                foreach (var item in parentRequired)
                {
                    if (item is JsonValue iv && iv.TryGetValue<string>(out var s) && seen.Add(s))
                    {
                        existing.Add(s);
                    }
                }
            }
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
