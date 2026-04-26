using CookBot.Application.Recipes;
using CookBot.Web.Services;

namespace CookBot.Tests.Migration;

/// <summary>
/// MIGRATION-04 verification — CookbookTransferService.Deserialize routes per-recipe
/// through the upcaster chain + RecipeValidator. v1 cookbooks (legacy field names)
/// upcast cleanly; v2 cookbooks pass through; mixed cookbooks return per-recipe errors;
/// malformed JSON / unsupported schema versions return null with errors.
/// Plan 02-04 Task 1.
/// </summary>
public class CookbookUpcastImportTests
{
    private static CookbookTransferService MakeService()
    {
        var upcasterChain = new RecipeUpcasterChain(new IRecipeUpcaster[] { new Migration_V1_To_V2() });
        var validator = new RecipeValidator();
        // Deserialize does not touch DbContext / RecipeService / CookbookService; null-pass
        // is safe because no instance method on those is called from Deserialize.
        return new CookbookTransferService(
            db: null!, cookbookService: null!, recipeService: null!,
            upcasterChain: upcasterChain, validator: validator);
    }

    [Fact]
    public void Deserialize_V1Envelope_UpcastsAndValidates()
    {
        const string v1Json = """
            {
              "schemaVersion": 1,
              "cookbook": { "name": "Test Cookbook" },
              "recipes": [
                {
                  "name": "Cookies",
                  "servings": 12,
                  "prepTime": 10,
                  "cookTime": 12,
                  "ingredients": [
                    { "localId": 1, "name": "flour", "amount": 2.0, "unit": "cup" }
                  ],
                  "steps": [
                    { "isSection": false, "text": "Mix [flour](#1).", "timers": [] }
                  ]
                }
              ]
            }
            """;

        var svc = MakeService();
        var envelope = svc.Deserialize(v1Json, out var errors);

        Assert.NotNull(envelope);
        Assert.Empty(errors);
        Assert.Single(envelope!.Recipes);
    }

    [Fact]
    public void Deserialize_V2Envelope_AlreadyCanonical_ImportsCleanly()
    {
        const string v2Json = """
            {
              "schemaVersion": 2,
              "cookbook": { "name": "Test Cookbook v2" },
              "recipes": [
                {
                  "version": 2,
                  "name": "Already-canonical Cake",
                  "servings": 4,
                  "prepTimeMinutes": 15,
                  "cookTimeMinutes": 30,
                  "ingredients": [
                    { "id": 1, "name": "flour", "amount": 2.0, "unit": "cup" }
                  ],
                  "steps": [
                    { "kind": "content", "text": "Mix [flour](#1).", "timers": [] }
                  ]
                }
              ]
            }
            """;

        var svc = MakeService();
        var envelope = svc.Deserialize(v2Json, out var errors);

        Assert.NotNull(envelope);
        Assert.Empty(errors);
        Assert.Single(envelope!.Recipes);
    }

    [Fact]
    public void Deserialize_MixedCookbook_PartialSuccess()
    {
        const string mixedJson = """
            {
              "schemaVersion": 2,
              "cookbook": { "name": "Mixed" },
              "recipes": [
                {
                  "version": 2,
                  "name": "Valid Recipe",
                  "servings": 2,
                  "ingredients": [{ "id": 1, "name": "flour", "amount": 1, "unit": "cup" }],
                  "steps": [{ "kind": "content", "text": "Use [flour](#1).", "timers": [] }]
                },
                {
                  "version": 2,
                  "name": "",
                  "servings": 0,
                  "ingredients": [],
                  "steps": []
                }
              ]
            }
            """;

        var svc = MakeService();
        var envelope = svc.Deserialize(mixedJson, out var errors);

        Assert.NotNull(envelope);
        Assert.NotEmpty(errors);
        Assert.Equal(2, envelope!.Recipes.Count);
    }

    [Fact]
    public void Deserialize_MalformedJson_ReturnsNullWithError()
    {
        var svc = MakeService();
        var envelope = svc.Deserialize("{ this is not valid json", out var errors);

        Assert.Null(envelope);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Invalid JSON"));
    }

    [Fact]
    public void Deserialize_UnsupportedSchemaVersion_ReturnsNullWithError()
    {
        const string v3Json = """{ "schemaVersion": 3, "cookbook": { "name": "x" }, "recipes": [] }""";
        var svc = MakeService();
        var envelope = svc.Deserialize(v3Json, out var errors);

        Assert.Null(envelope);
        Assert.Contains(errors, e => e.Contains("Unsupported schema version"));
    }
}
