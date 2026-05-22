using System;
using System.Text.Json.Nodes;
using CookBot.Application.Recipes;

namespace CookBot.Tests.Recipes;

/// <summary>
/// SCHEMA-11 — schema contract gate for v3 fields.
/// These tests assert that <see cref="RecipeJsonSchemaProvider.GetSchema()"/> emits
/// <c>photoUrl</c>, <c>description</c>, and step-level <c>temperature</c>.
///
/// Per D-35 / SCHEMA-11 ordering: these tests are written FIRST and run RED until
/// Plan 02 (StepTemperature) + Plan 03 (RecipeDocument v3) land. After Plan 03,
/// all three tests MUST turn GREEN. No production code is modified in Plan 01.
/// </summary>
public class SchemaAssertionTests
{
    /// <summary>
    /// Verifies the top-level RecipeDocument schema properties include the v3 additions
    /// <c>photoUrl</c> and <c>description</c>.
    ///
    /// CURRENTLY RED — fields are added in Plan 03.
    /// </summary>
    [Fact]
    public void GetSchema_Includes_PhotoUrl_Description()
    {
        var schema = new RecipeJsonSchemaProvider().GetSchema().AsObject();
        var props = schema["properties"]!.AsObject();

        Assert.True(
            props.ContainsKey("photoUrl"),
            $"Expected 'photoUrl' in schema properties but it was absent. Schema: {schema.ToJsonString()}");

        Assert.True(
            props.ContainsKey("description"),
            $"Expected 'description' in schema properties but it was absent. Schema: {schema.ToJsonString()}");
    }

    /// <summary>
    /// Verifies that the <c>ContentStep</c> anyOf branch in the <c>steps</c> array schema
    /// contains a <c>temperature</c> property with a nullable shape (per PITFALLS M3 —
    /// nullable fields must carry <c>"null"</c> in their type clause or wrap in anyOf with null).
    ///
    /// Navigation: schema["properties"]["steps"]["items"]["anyOf"] -> find branch whose
    /// properties contains "text" (that is ContentStep) -> assert "temperature" is present
    /// in that branch's properties.
    ///
    /// CURRENTLY RED — temperature is added to ContentStep in Plan 02.
    /// </summary>
    [Fact]
    public void GetSchema_StepTemperature_NullableShape()
    {
        var schema = new RecipeJsonSchemaProvider().GetSchema().AsObject();
        var contentStepProps = FindContentStepProperties(schema);

        Assert.True(
            contentStepProps.ContainsKey("temperature"),
            $"Expected 'temperature' in ContentStep properties but it was absent. " +
            $"ContentStep branch properties: {contentStepProps.ToJsonString()}");

        // Per PITFALLS M3: nullable shape means the temperature property must allow null.
        // Either: "type": ["object","null"]  OR  anyOf: [{...}, {"type":"null"}]
        var temperatureSchema = contentStepProps["temperature"]!.AsObject();
        var isNullable = IsNullableSchema(temperatureSchema);

        Assert.True(
            isNullable,
            $"Expected 'temperature' to have a nullable shape per PITFALLS M3, " +
            $"but got: {temperatureSchema.ToJsonString()}");
    }

    /// <summary>
    /// Regression guard for the <see cref="RecipeJsonSchemaProvider"/>'s
    /// <c>SetAdditionalPropertiesFalse</c> walker: verifies that the temperature subschema
    /// (which is an object — <c>StepTemperature</c> with <c>value</c> + <c>unit</c>)
    /// carries <c>additionalProperties: false</c>.
    ///
    /// CURRENTLY RED — temperature is added to ContentStep in Plan 02.
    /// </summary>
    [Fact]
    public void GetSchema_AdditionalPropertiesFalse_OnStepTemperatureSubschema()
    {
        var schema = new RecipeJsonSchemaProvider().GetSchema().AsObject();
        var contentStepProps = FindContentStepProperties(schema);

        Assert.True(
            contentStepProps.ContainsKey("temperature"),
            $"Expected 'temperature' in ContentStep properties but it was absent. " +
            $"ContentStep branch: {contentStepProps.ToJsonString()}");

        // Locate the concrete object subschema for StepTemperature.
        // If temperature is nullable (anyOf), find the branch that has "properties" (the object shape).
        var temperatureSchema = contentStepProps["temperature"]!.AsObject();
        var objectSubschema = FindObjectSubschema(temperatureSchema);

        Assert.NotNull(
            objectSubschema);

        var hasAdditionalPropertiesFalse =
            objectSubschema!["additionalProperties"] is JsonValue apv
            && apv.TryGetValue<bool>(out var apBool)
            && !apBool;

        Assert.True(
            hasAdditionalPropertiesFalse,
            $"Expected StepTemperature object subschema to carry additionalProperties:false " +
            $"(SetAdditionalPropertiesFalse walker regression), but got: {objectSubschema.ToJsonString()}");
    }

    /// <summary>
    /// Regression guard for the Anthropic strict structured-outputs constraint discovered during
    /// Phase 10 UAT Test 4 (POLISH/QOL-04): the API rejects requests whose schema contains
    /// <c>anyOf</c> branches carrying inline <c>type</c> / <c>properties</c> / <c>required</c> /
    /// <c>additionalProperties</c>. Branches must be externalized to <c>$defs</c> and reduced to
    /// <c>$ref</c> wrappers. See <see cref="RecipeJsonSchemaProvider.ExternalizeAnyOfBranches"/>.
    /// </summary>
    [Fact]
    public void GetSchema_AnyOfBranches_ContainOnlyRefs()
    {
        var schema = new RecipeJsonSchemaProvider().GetSchema();
        var violations = new System.Collections.Generic.List<string>();
        WalkAnyOf(schema, "$", violations);

        Assert.True(
            violations.Count == 0,
            $"Anthropic strict structured-outputs forbids type/properties/required/additionalProperties " +
            $"inside anyOf branches. Found violations at: {string.Join("; ", violations)}. " +
            $"Schema: {schema.ToJsonString()}");
    }

    /// <summary>
    /// Recursively walks the schema and, for every <c>anyOf</c> array encountered, asserts each
    /// branch is either a pure <c>$ref</c> wrapper or contains only the limited set of keywords
    /// Anthropic permits inside an anyOf branch (i.e. NOT type/properties/required/additionalProperties).
    /// </summary>
    private static void WalkAnyOf(JsonNode? node, string path, System.Collections.Generic.List<string> violations)
    {
        if (node is JsonObject obj)
        {
            if (obj["anyOf"] is JsonArray anyOf)
            {
                for (var i = 0; i < anyOf.Count; i++)
                {
                    if (anyOf[i] is JsonObject branch)
                    {
                        foreach (var forbidden in new[] { "type", "properties", "required", "additionalProperties" })
                        {
                            // Permit `type: "null"` — Anthropic accepts a null-sentinel branch.
                            if (forbidden == "type"
                                && branch["type"] is JsonValue tv
                                && tv.TryGetValue<string>(out var ts)
                                && ts == "null")
                            {
                                continue;
                            }
                            if (branch.ContainsKey(forbidden))
                            {
                                violations.Add($"{path}.anyOf[{i}].{forbidden}");
                            }
                        }
                    }
                }
            }
            foreach (var kvp in obj)
            {
                WalkAnyOf(kvp.Value, $"{path}.{kvp.Key}", violations);
            }
        }
        else if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                WalkAnyOf(arr[i], $"{path}[{i}]", violations);
            }
        }
    }

    /// <summary>
    /// Idempotency guard: calling the provider twice (or re-running the externalizer on its own
    /// output) must not produce nested <c>Variant_</c> / <c>kind_…</c> duplicates or alter the
    /// schema shape.
    /// </summary>
    [Fact]
    public void GetSchema_IsIdempotent_AcrossInstances()
    {
        var a = new RecipeJsonSchemaProvider().GetSchema().ToJsonString();
        var b = new RecipeJsonSchemaProvider().GetSchema().ToJsonString();
        Assert.Equal(a, b);
    }

    // ─────────────────────────── helpers ───────────────────────────

    /// <summary>
    /// Navigates into the schema to find the ContentStep anyOf branch (identified by the
    /// presence of a "text" property), then returns that branch's "properties" object.
    /// Follows <c>$ref</c> into <c>$defs</c> (Anthropic strict-mode externalization, see
    /// <see cref="RecipeJsonSchemaProvider"/>) so the navigation works whether the branch
    /// is inline or externalized.
    /// Throws <see cref="Xunit.Sdk.XunitException"/> with a diagnostic fragment if not found.
    /// </summary>
    private static JsonObject FindContentStepProperties(JsonObject rootSchema)
    {
        // steps -> items -> anyOf -> branch (possibly $ref) -> properties containing "text"
        var stepsSchema = rootSchema["properties"]?["steps"]?.AsObject()
            ?? throw new Xunit.Sdk.XunitException(
                $"Could not navigate to schema['properties']['steps']. Root: {rootSchema.ToJsonString()}");

        var stepsItems = stepsSchema["items"]?.AsObject()
            ?? throw new Xunit.Sdk.XunitException(
                $"Could not navigate to schema['properties']['steps']['items']. Steps schema: {stepsSchema.ToJsonString()}");

        var anyOf = stepsItems["anyOf"]?.AsArray()
            ?? throw new Xunit.Sdk.XunitException(
                $"Expected 'anyOf' under steps.items for polymorphic StepNode. " +
                $"Items schema: {stepsItems.ToJsonString()}");

        foreach (var branch in anyOf)
        {
            if (branch is not JsonObject branchObj)
                continue;

            var resolved = ResolveRef(rootSchema, branchObj);
            var branchProps = resolved?["properties"]?.AsObject();
            if (branchProps is not null && branchProps.ContainsKey("text"))
            {
                return branchProps;
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"Could not find ContentStep anyOf branch (expected branch with 'text' property). " +
            $"anyOf array: {anyOf.ToJsonString()}");
    }

    /// <summary>
    /// If <paramref name="node"/> is a <c>{ "$ref": "#/$defs/Foo" }</c> wrapper, resolves it
    /// against the root schema's <c>$defs</c>. Otherwise returns <paramref name="node"/> as-is.
    /// Only internal <c>#/$defs/…</c> refs are supported (matches the provider's externalizer).
    /// </summary>
    private static JsonObject? ResolveRef(JsonObject root, JsonObject node)
    {
        if (node["$ref"] is JsonValue refVal && refVal.TryGetValue<string>(out var refStr))
        {
            const string prefix = "#/$defs/";
            if (refStr.StartsWith(prefix, StringComparison.Ordinal))
            {
                var name = refStr[prefix.Length..];
                return root["$defs"]?[name]?.AsObject();
            }
            return null;
        }
        return node;
    }

    /// <summary>
    /// Returns true if the schema node represents a nullable type:
    /// either <c>"type": ["X","null"]</c> or <c>anyOf: [{{...}}, {{"type":"null"}}]</c>.
    /// </summary>
    private static bool IsNullableSchema(JsonObject schema)
    {
        // Pattern 1: "type": ["object","null"]
        if (schema["type"] is JsonArray typeArray)
        {
            foreach (var item in typeArray)
            {
                if (item is JsonValue v && v.TryGetValue<string>(out var s) && s == "null")
                    return true;
            }
        }

        // Pattern 2: anyOf: [ {...object schema...}, {"type":"null"} ]
        if (schema["anyOf"] is JsonArray anyOf)
        {
            foreach (var branch in anyOf)
            {
                if (branch is JsonObject branchObj
                    && branchObj["type"] is JsonValue bv
                    && bv.TryGetValue<string>(out var bs)
                    && bs == "null")
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Given a possibly-nullable schema node (may be wrapped in anyOf with a null branch),
    /// returns the concrete object subschema (the branch that has "properties").
    /// Returns the schema itself if it directly has "properties".
    /// </summary>
    private static JsonObject? FindObjectSubschema(JsonObject schema)
    {
        // Direct: schema has "properties"
        if (schema.ContainsKey("properties"))
            return schema;

        // Wrapped in anyOf
        if (schema["anyOf"] is JsonArray anyOf)
        {
            foreach (var branch in anyOf)
            {
                if (branch is JsonObject branchObj && branchObj.ContainsKey("properties"))
                    return branchObj;
            }
        }

        return null;
    }
}
