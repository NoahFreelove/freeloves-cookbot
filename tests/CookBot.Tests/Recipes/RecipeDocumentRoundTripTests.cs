using System;
using System.Collections.Generic;
using System.IO;
using CookBot.Application.Recipes;

namespace CookBot.Tests.Recipes;

/// <summary>
/// FORMAT-10 / D-23 / D-24 CI gate. Drives the full canonical pipeline (Parser detect ->
/// upcaster chain -> JsonRecipeSerializer -> RecipeValidator -> ParsedRecipe projection)
/// against a filesystem-driven fixture set covering v1 YAML, v1 JSON-export, and v2
/// canonical shapes. Asserts non-zero <c>PrepTimeMinutes</c>/<c>CookTimeMinutes</c> where
/// the source carries them (Pitfall C2 — units in field name).
/// </summary>
public class RecipeDocumentRoundTripTests
{
    public static IEnumerable<object[]> V1YamlFixtures()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recipes", "v1-yaml");
        foreach (var path in Directory.GetFiles(dir, "*.yaml"))
        {
            yield return new object[] { Path.GetFileName(path), File.ReadAllText(path) };
        }
    }

    public static IEnumerable<object[]> V1JsonExportFixtures()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recipes", "v1-json-export");
        foreach (var path in Directory.GetFiles(dir, "*.json"))
        {
            yield return new object[] { Path.GetFileName(path), File.ReadAllText(path) };
        }
    }

    public static IEnumerable<object[]> V2CanonicalFixtures()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Recipes", "v2-canonical");
        foreach (var path in Directory.GetFiles(dir, "*.json"))
        {
            yield return new object[] { Path.GetFileName(path), File.ReadAllText(path) };
        }
    }

    [Theory]
    [MemberData(nameof(V1YamlFixtures))]
    public void V1Yaml_ParsesAndPopulatesTimeFields(string fixtureName, string yamlText)
    {
        var parser = TestHost.GetParser();
        var ok = parser.TryParse(yamlText, out var parsed, out var errors);
        Assert.True(ok, $"{fixtureName} failed to parse: {string.Join("; ", errors)}");
        Assert.NotNull(parsed);
        Assert.NotEqual(0, parsed!.PrepTimeMinutes ?? 0);
        Assert.NotEqual(0, parsed.CookTimeMinutes ?? 0);
        Assert.False(string.IsNullOrWhiteSpace(parsed.Name), "Name should be populated.");
    }

    [Theory]
    [MemberData(nameof(V1JsonExportFixtures))]
    public void V1JsonExport_UpcastsAndValidates(string fixtureName, string jsonText)
    {
        var parser = TestHost.GetParser();
        var ok = parser.TryParse(jsonText, out var parsed, out var errors);
        Assert.True(ok, $"{fixtureName} failed to parse: {string.Join("; ", errors)}");
        Assert.NotNull(parsed);
        Assert.NotEqual(0, parsed!.PrepTimeMinutes ?? 0);
        Assert.NotEqual(0, parsed.CookTimeMinutes ?? 0);
    }

    [Theory]
    [MemberData(nameof(V2CanonicalFixtures))]
    public void V2Canonical_RoundTripIsIdempotent(string fixtureName, string jsonText)
    {
        var serializer = new JsonRecipeSerializer();
        var validator = new RecipeValidator();

        var doc = serializer.Deserialize(jsonText);
        var roundTripped = serializer.Deserialize(serializer.Serialize(doc));

        Assert.True(
            validator.Validate(roundTripped).IsValid,
            $"{fixtureName} did not validate after round-trip.");
        Assert.Equal(2, doc.Version);
        Assert.Equal(doc.Version, roundTripped.Version);
        Assert.Equal(doc.Name, roundTripped.Name);
        Assert.Equal(doc.Servings, roundTripped.Servings);
        Assert.Equal(doc.PrepTimeMinutes, roundTripped.PrepTimeMinutes);
        Assert.Equal(doc.CookTimeMinutes, roundTripped.CookTimeMinutes);
        Assert.Equal(doc.Ingredients.Count, roundTripped.Ingredients.Count);
        Assert.Equal(doc.Steps.Count, roundTripped.Steps.Count);
    }
}
