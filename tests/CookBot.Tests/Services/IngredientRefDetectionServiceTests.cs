using CookBot.Application.Services;
using CookBot.Domain.Interfaces;

namespace CookBot.Tests.Services;

public class IngredientRefDetectionServiceTests
{
    [Fact]
    public void DetectRefs_MarkdownLinks()
    {
        var ingredients = new List<ParsedIngredient>
        {
            new() { LocalId = 1, Name = "flour" },
            new() { LocalId = 2, Name = "butter" },
        };
        var refs = IngredientRefDetectionService.DetectRefs(
            "Mix [flour](#1) and [butter](#2) together.", ingredients);
        Assert.Equal(new[] { 1, 2 }, refs);
    }

    [Fact]
    public void DetectRefs_PlainTextMatch()
    {
        var ingredients = new List<ParsedIngredient>
        {
            new() { LocalId = 1, Name = "flour" },
            new() { LocalId = 2, Name = "butter" },
            new() { LocalId = 3, Name = "sugar" },
        };
        var refs = IngredientRefDetectionService.DetectRefs(
            "Add the flour and sugar to the bowl.", ingredients);
        Assert.Contains(1, refs);
        Assert.Contains(3, refs);
        Assert.DoesNotContain(2, refs);
    }

    [Fact]
    public void DetectRefs_NoMatches()
    {
        var ingredients = new List<ParsedIngredient>
        {
            new() { LocalId = 1, Name = "flour" },
        };
        var refs = IngredientRefDetectionService.DetectRefs(
            "Preheat oven to 350F.", ingredients);
        Assert.Empty(refs);
    }
}
