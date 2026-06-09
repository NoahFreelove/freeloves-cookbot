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
    /// Recursively walks the schema and, for every <c>anyOf</c> array encountered, asserts:
    ///   (a) every branch is either a pure <c>$ref</c> wrapper or carries only keywords
    ///       Anthropic permits inside an anyOf branch (NOT type/properties/required/
    ///       additionalProperties), AND
    ///   (b) the <c>anyOf</c>'s parent object has none of <c>type</c>/<c>required</c>/
    ///       <c>additionalProperties</c> as siblings of <c>anyOf</c> — Anthropic strict mode
    ///       rejects those at the same level as the union.
    /// </summary>
    private static void WalkAnyOf(JsonNode? node, string path, System.Collections.Generic.List<string> violations)
    {
        if (node is JsonObject obj)
        {
            if (obj["anyOf"] is JsonArray anyOf)
            {
                // (b) sibling-keyword check
                foreach (var forbiddenSibling in new[] { "type", "required", "additionalProperties" })
                {
                    if (obj.ContainsKey(forbiddenSibling))
                    {
                        violations.Add($"{path}.{forbiddenSibling} (sibling of anyOf)");
                    }
                }

                // (a) per-branch check
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

    // ─────────────────────── v4 field assertions (Phase 12 / 12-03) ───────────────────────

    /// <summary>
    /// Verifies the top-level RecipeDocument schema properties include the v4 additions
    /// <c>equipment</c> (string array) and <c>provenance</c> (nullable object).
    /// SC3 / FORMAT-06 / FLAG 4 — RecipeJsonSchemaProvider auto-reflects the v4 POCOs.
    /// </summary>
    [Fact]
    public void GetSchema_Includes_Equipment_And_Provenance()
    {
        var schema = new RecipeJsonSchemaProvider().GetSchema().AsObject();
        var props = schema["properties"]!.AsObject();

        Assert.True(
            props.ContainsKey("equipment"),
            $"Expected 'equipment' in top-level schema properties but it was absent. Schema: {schema.ToJsonString()}");

        Assert.True(
            props.ContainsKey("provenance"),
            $"Expected 'provenance' in top-level schema properties but it was absent. Schema: {schema.ToJsonString()}");
    }

    /// <summary>
    /// Verifies that the <c>IngredientEntry</c> schema (within <c>ingredients.items</c>)
    /// includes a <c>substitutions</c> property.
    /// SC3 / FORMAT-06 — per-ingredient substitution list auto-reflected from the v4 POCO.
    /// </summary>
    [Fact]
    public void GetSchema_IngredientSchema_Includes_Substitutions()
    {
        var schema = new RecipeJsonSchemaProvider().GetSchema().AsObject();
        var ingredientProps = FindIngredientItemProperties(schema);

        Assert.True(
            ingredientProps.ContainsKey("substitutions"),
            $"Expected 'substitutions' in ingredient item properties but it was absent. " +
            $"Ingredient item properties: {ingredientProps.ToJsonString()}");
    }

    /// <summary>
    /// Verifies that the <c>ContentStep</c> anyOf branch schema includes a
    /// <c>donenessCue</c> property.
    /// SC3 / FORMAT-06 — per-step doneness cue auto-reflected from the v4 POCO.
    /// </summary>
    [Fact]
    public void GetSchema_ContentStep_Includes_DonenessCue()
    {
        var schema = new RecipeJsonSchemaProvider().GetSchema().AsObject();
        var contentStepProps = FindContentStepProperties(schema);

        Assert.True(
            contentStepProps.ContainsKey("donenessCue"),
            $"Expected 'donenessCue' in ContentStep properties but it was absent. " +
            $"ContentStep branch properties: {contentStepProps.ToJsonString()}");
    }

    /// <summary>
    /// Verifies that the new nested record subschemas (<c>IngredientSubstitution</c> and
    /// <c>RecipeProvenance</c>) carry <c>additionalProperties: false</c>, confirming the
    /// <see cref="RecipeJsonSchemaProvider.SetAdditionalPropertiesFalse"/> pass handled
    /// them correctly for Anthropic strict mode (P4).
    /// </summary>
    [Fact]
    public void GetSchema_AdditionalPropertiesFalse_OnIngredientSubstitutionAndProvenanceSubschemas()
    {
        var schema = new RecipeJsonSchemaProvider().GetSchema().AsObject();

        // ── IngredientSubstitution ──────────────────────────────────
        // Path: properties -> ingredients -> items -> properties -> substitutions -> items
        var ingredientProps = FindIngredientItemProperties(schema);
        Assert.True(
            ingredientProps.ContainsKey("substitutions"),
            $"Expected 'substitutions' in ingredient schema. Properties: {ingredientProps.ToJsonString()}");

        var substitutionsSchema = ingredientProps["substitutions"]!.AsObject();

        // substitutions is an array; each item should be the IngredientSubstitution object schema
        var subItemSchema = substitutionsSchema["items"]?.AsObject()
            ?? throw new Xunit.Sdk.XunitException(
                $"Expected 'items' under substitutions schema. substitutions schema: {substitutionsSchema.ToJsonString()}");

        var subObjectSchema = FindObjectSubschema(subItemSchema);
        Assert.NotNull(subObjectSchema);

        var subHasAdditionalPropertiesFalse =
            subObjectSchema!["additionalProperties"] is JsonValue subApv
            && subApv.TryGetValue<bool>(out var subApBool)
            && !subApBool;

        Assert.True(
            subHasAdditionalPropertiesFalse,
            $"Expected IngredientSubstitution item subschema to carry additionalProperties:false " +
            $"(Anthropic strict mode / P4), but got: {subObjectSchema.ToJsonString()}");

        // ── RecipeProvenance ─────────────────────────────────────────
        // Path: properties -> provenance (may be anyOf with null branch)
        var topProps = schema["properties"]!.AsObject();
        Assert.True(
            topProps.ContainsKey("provenance"),
            $"Expected 'provenance' in top-level properties. Properties: {topProps.ToJsonString()}");

        var provenanceSchema = topProps["provenance"]!.AsObject();
        var provObjectSchema = FindObjectSubschema(provenanceSchema);

        // provenance may be null (RecipeProvenance?) so could be wrapped in anyOf with a null branch;
        // if no object subschema is found, resolve via $ref before asserting.
        if (provObjectSchema is null && provenanceSchema["$ref"] is JsonValue refVal
            && refVal.TryGetValue<string>(out var refStr)
            && refStr.StartsWith("#/$defs/", StringComparison.Ordinal))
        {
            var name = refStr["#/$defs/".Length..];
            provObjectSchema = schema["$defs"]?[name]?.AsObject();
            provObjectSchema = provObjectSchema is not null ? FindObjectSubschema(provObjectSchema) : null;
        }

        Assert.NotNull(provObjectSchema);

        var provHasAdditionalPropertiesFalse =
            provObjectSchema!["additionalProperties"] is JsonValue provApv
            && provApv.TryGetValue<bool>(out var provApBool)
            && !provApBool;

        Assert.True(
            provHasAdditionalPropertiesFalse,
            $"Expected RecipeProvenance object subschema to carry additionalProperties:false " +
            $"(Anthropic strict mode / P4), but got: {provObjectSchema.ToJsonString()}");
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

    /// <summary>
    /// Navigates into the schema to find the <c>IngredientEntry</c> item properties.
    /// Path: schema["properties"]["ingredients"]["items"]["properties"].
    /// Follows <c>$ref</c> into <c>$defs</c> if the items schema is externalized.
    /// </summary>
    private static JsonObject FindIngredientItemProperties(JsonObject rootSchema)
    {
        var ingredientsSchema = rootSchema["properties"]?["ingredients"]?.AsObject()
            ?? throw new Xunit.Sdk.XunitException(
                $"Could not navigate to schema['properties']['ingredients']. Root: {rootSchema.ToJsonString()}");

        var ingredientsItems = ingredientsSchema["items"]?.AsObject()
            ?? throw new Xunit.Sdk.XunitException(
                $"Could not navigate to schema['properties']['ingredients']['items']. " +
                $"Ingredients schema: {ingredientsSchema.ToJsonString()}");

        // items may be a $ref (externalized by ExternalizeAnyOfBranches)
        var resolved = ResolveRef(rootSchema, ingredientsItems);

        var props = resolved?["properties"]?.AsObject()
            ?? throw new Xunit.Sdk.XunitException(
                $"Could not navigate to ingredient items 'properties'. " +
                $"Resolved items schema: {(resolved?.ToJsonString() ?? "null")}");

        return props;
    }
}
