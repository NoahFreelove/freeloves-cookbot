using CookBot.Application.Services;
using CookBot.Domain.Entities;

namespace CookBot.Tests.Services;

public class PantryAiPopulationServiceTests
{
    [Fact]
    public void ExtractJsonArray_StripsMarkdownFence()
    {
        var raw = """
            Here you go:
            ```json
            [{"ingredientName":"milk","amount":1,"unit":"cup","expiration":null}]
            ```
            """;
        var json = PantryAiPopulationService.ExtractJsonArray(raw);
        Assert.NotNull(json);
        Assert.Contains("milk", json);
    }

    [Fact]
    public void ExtractJsonArray_PrefersBalancedBrackets_WhenBracketInsideString()
    {
        var raw = """[{"ingredientName":"Pepper [hot]","amount":1,"unit":"piece","expiration":null}]""";
        var json = PantryAiPopulationService.ExtractJsonArray(raw);
        Assert.NotNull(json);
        Assert.Contains("Pepper [hot]", json);
        Assert.True(json!.EndsWith(']'));
    }

    [Fact]
    public void ExtractJsonArray_SkipsNonArrayFenceThenUsesJsonFence()
    {
        var raw = """
            Notes:
            ```text
            not json
            ```
            Data:
            ```json
            [{"ingredientName":"flour","amount":1,"unit":"cup","expiration":null}]
            ```
            """;
        var json = PantryAiPopulationService.ExtractJsonArray(raw);
        Assert.NotNull(json);
        Assert.Contains("flour", json);
    }

    [Fact]
    public void ExtractJsonArray_FindsArrayAfterPreambleWithoutFence()
    {
        var raw = """
            Sure — normalized list:
            [{"ingredientName":"sugar","amount":2,"unit":"cup","expiration":null}]
            Hope this helps!
            """;
        var json = PantryAiPopulationService.ExtractJsonArray(raw);
        Assert.NotNull(json);
        Assert.Contains("sugar", json);
        Assert.DoesNotContain("Hope", json);
    }

    [Fact]
    public void ExtractJsonArray_UnwrapsJsonObjectWithItemsArray()
    {
        const string raw = """{"items":[{"ingredientName":"milk","amount":1,"unit":"L","expiration":null}]}""";
        var json = PantryAiPopulationService.ExtractJsonArray(raw);
        Assert.NotNull(json);
        Assert.StartsWith("[", json);
        Assert.Contains("milk", json);
    }

    [Fact]
    public void ExtractJsonArray_UnwrapsNestedObject()
    {
        const string raw = """{"response":{"data":[{"ingredientName":"rice","amount":2,"unit":"cup","expiration":null}]}}""";
        var json = PantryAiPopulationService.ExtractJsonArray(raw);
        Assert.NotNull(json);
        Assert.Contains("rice", json);
    }

    [Fact]
    public void ExtractJsonArray_StripsThinkingTags()
    {
        var raw = """
            <thinking>planning...</thinking>
            [{"ingredientName":"oil","amount":1,"unit":"tbsp","expiration":null}]
            """;
        var json = PantryAiPopulationService.ExtractJsonArray(raw);
        Assert.NotNull(json);
        Assert.Contains("oil", json);
    }

    [Fact]
    public void TryDeserializeRows_AcceptsSnakeCaseIngredientName()
    {
        const string json = """[{"ingredient_name":"Honey","amount":1,"unit":"cup","expiration":null}]""";
        var ok = PantryAiPopulationService.TryDeserializeRows(json, out var rows, out var err);
        Assert.True(ok);
        Assert.Null(err);
        Assert.Single(rows);
        Assert.Equal("Honey", rows[0].IngredientName);
    }

    [Fact]
    public void TryDeserializeRows_AcceptsValidArray()
    {
        const string json = """[{"ingredientName":"Eggs","amount":6,"unit":"piece","expiration":"2026-06-01"}]""";
        var ok = PantryAiPopulationService.TryDeserializeRows(json, out var rows, out var err);
        Assert.True(ok);
        Assert.Null(err);
        Assert.Single(rows);
        Assert.Equal("Eggs", rows[0].IngredientName);
        Assert.Equal(6, rows[0].Amount);
        Assert.Equal("piece", rows[0].Unit);
        Assert.Equal("2026-06-01", rows[0].Expiration);
    }

    [Fact]
    public void TryDeserializeRows_RejectsMeasuredRowWithoutPositiveAmount()
    {
        const string json = """[{"ingredientName":"x","amount":0,"unit":"g","expiration":null}]""";
        var ok = PantryAiPopulationService.TryDeserializeRows(json, out _, out var err);
        Assert.False(ok);
        Assert.NotNull(err);
    }

    [Fact]
    public void TryDeserializeRows_StapleRow_OmitsAmountAndUnit()
    {
        const string json = """[{"ingredientName":"milk"}]""";
        var ok = PantryAiPopulationService.TryDeserializeRows(json, out var rows, out var err);
        Assert.True(ok);
        Assert.Null(err);
        Assert.Single(rows);
        Assert.Equal("milk", rows[0].IngredientName);
        Assert.Equal(1, rows[0].Amount);
        Assert.Equal(PantryAiImport.UnmeasuredUnit, rows[0].Unit);
    }

    [Fact]
    public void TryDeserializeRows_ExplicitStapleUnit_NormalizesAmount()
    {
        const string json = """[{"ingredientName":"salt","amount":99,"unit":"staple"}]""";
        var ok = PantryAiPopulationService.TryDeserializeRows(json, out var rows, out _);
        Assert.True(ok);
        Assert.Equal(1, rows[0].Amount);
        Assert.Equal(PantryAiImport.UnmeasuredUnit, rows[0].Unit);
    }

    [Fact]
    public void BuildSystemPrompt_IncludesJsonShape()
    {
        var s = PantryAiPopulationService.BuildSystemPrompt(CookBot.Domain.Enums.UnitSystem.Metric);
        Assert.Contains("ingredientName", s);
        Assert.Contains("JSON array", s);
        Assert.Contains("metric", s, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildStandardizeSystemPrompt_MentionsNormalizeAndSchema()
    {
        var s = PantryAiPopulationService.BuildStandardizeSystemPrompt(CookBot.Domain.Enums.UnitSystem.Imperial);
        Assert.Contains("normalize", s, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ingredientName", s);
        Assert.Contains("customary", s, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildStandardizeUserMessage_FormatsRows()
    {
        var items = new List<PantryItem>
        {
            new()
            {
                Id = 1,
                IngredientId = 10,
                Ingredient = new Ingredient { Name = "Tomatos" },
                Amount = 2,
                Unit = "lb",
                ExpirationDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Id = 2,
                IngredientId = 11,
                Ingredient = new Ingredient { Name = "Salt" },
                Amount = 1,
                Unit = PantryAiImport.UnmeasuredUnit,
            },
        };

        var msg = PantryAiPopulationService.BuildStandardizeUserMessage(items);
        Assert.Contains("Tomatos", msg);
        Assert.Contains("lb", msg);
        Assert.Contains("2026-05-01", msg);
        Assert.Contains("staple", msg);
        Assert.Contains("not measured", msg);
    }
}
