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

    // ─────────────────────────── helpers ───────────────────────────

    /// <summary>
    /// Navigates into the schema to find the ContentStep anyOf branch (identified by the
    /// presence of a "text" property), then returns that branch's "properties" object.
    /// Throws <see cref="Xunit.Sdk.XunitException"/> with a diagnostic fragment if not found.
    /// </summary>
    private static JsonObject FindContentStepProperties(JsonObject rootSchema)
    {
        // steps -> items -> anyOf -> branch containing "text"
        var stepsSchema = rootSchema["properties"]?["steps"]?.AsObject()
            ?? throw new Xunit.Sdk.XunitException(
                $"Could not navigate to schema['properties']['steps']. Root: {rootSchema.ToJsonString()}");

        // steps may be: { "type": "array", "items": { "anyOf": [...] } }
        // or items may itself be the anyOf object
        var stepsItems = stepsSchema["items"]?.AsObject()
            ?? throw new Xunit.Sdk.XunitException(
                $"Could not navigate to schema['properties']['steps']['items']. Steps schema: {stepsSchema.ToJsonString()}");

        // Under items, look for "anyOf"
        var anyOf = stepsItems["anyOf"]?.AsArray()
            ?? throw new Xunit.Sdk.XunitException(
                $"Expected 'anyOf' under steps.items for polymorphic StepNode. " +
                $"Items schema: {stepsItems.ToJsonString()}");

        // Find the ContentStep branch: the one whose "properties" contains "text"
        foreach (var branch in anyOf)
        {
            if (branch is not JsonObject branchObj)
                continue;

            var branchProps = branchObj["properties"]?.AsObject();
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
