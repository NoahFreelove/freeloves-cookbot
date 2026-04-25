using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using CookBot.Application.Recipes;

namespace CookBot.Tests.Recipes;

/// <summary>
/// D-07 / Anthropic strict-mode — verifies every object subschema in the generated
/// <see cref="RecipeJsonSchemaProvider"/> tree carries <c>additionalProperties: false</c>.
/// Also covers the Lazy&lt;JsonNode&gt; cache contract.
/// </summary>
public class RecipeJsonSchemaProviderTests
{
    [Fact]
    public void GetSchema_RootObjectHasAdditionalPropertiesFalse()
    {
        var schema = new RecipeJsonSchemaProvider().GetSchema().AsObject();
        Assert.NotNull(schema["additionalProperties"]);
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());
    }

    [Fact]
    public void GetSchema_AllObjectSubschemasHaveAdditionalPropertiesFalse()
    {
        var schema = new RecipeJsonSchemaProvider().GetSchema();
        var bad = new List<string>();
        WalkAndCheck(schema, "$", bad);
        Assert.True(
            bad.Count == 0,
            $"Object schemas missing additionalProperties:false at: {string.Join(", ", bad)}");
    }

    [Fact]
    public void GetSchema_IsCachedAcrossCalls()
    {
        var provider = new RecipeJsonSchemaProvider();
        Assert.Same(provider.GetSchema(), provider.GetSchema());
    }

    private static void WalkAndCheck(JsonNode? node, string path, List<string> bad)
    {
        if (node is JsonObject obj)
        {
            var isObjectSchema = false;
            if (obj["type"] is JsonValue tv && tv.TryGetValue<string>(out var ts) && ts == "object")
            {
                isObjectSchema = true;
            }
            else if (obj["type"] is JsonArray ta && ta.Any(x =>
                         x is JsonValue v && v.TryGetValue<string>(out var s) && s == "object"))
            {
                isObjectSchema = true;
            }
            else if (obj.ContainsKey("properties"))
            {
                isObjectSchema = true;
            }

            if (isObjectSchema && obj["additionalProperties"]?.GetValue<bool>() != false)
            {
                bad.Add(path);
            }

            foreach (var kvp in obj.ToList())
            {
                WalkAndCheck(kvp.Value, $"{path}.{kvp.Key}", bad);
            }
        }
        else if (node is JsonArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                WalkAndCheck(arr[i], $"{path}[{i}]", bad);
            }
        }
    }
}
