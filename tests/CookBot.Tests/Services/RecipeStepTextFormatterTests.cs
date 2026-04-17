using CookBot.Application.Services;

namespace CookBot.Tests.Services;

public class RecipeStepTextFormatterTests
{
    [Fact]
    public void ToHtml_ReplacesIngredientLinksWithSpans()
    {
        var input = "Whisk [flour](#1) and [salt](#3).";
        var html = RecipeStepTextFormatter.ToHtml(input);
        Assert.Equal(
            "Whisk <span class=\"ingredient-ref\" data-ingredient-id=\"1\">flour</span> and " +
            "<span class=\"ingredient-ref\" data-ingredient-id=\"3\">salt</span>.",
            html);
    }

    [Fact]
    public void ToHtml_EncodesHtmlInPlainTextAndDisplayNames()
    {
        var input = "Mix <eggs> & [cream](#2).";
        var html = RecipeStepTextFormatter.ToHtml(input);
        Assert.Contains("&lt;eggs&gt; &amp;", html);
        Assert.Contains(">cream</span>", html);
    }

    [Fact]
    public void ToHtml_PreservesLineBreaksAsBr()
    {
        var input = "Line one\nLine two";
        var html = RecipeStepTextFormatter.ToHtml(input);
        Assert.Equal("Line one<br />Line two", html);
    }

    [Fact]
    public void ToHtml_NormalizesCrLf()
    {
        var input = "a\r\nb";
        var html = RecipeStepTextFormatter.ToHtml(input);
        Assert.Equal("a<br />b", html);
    }

    [Fact]
    public void ToHtml_Empty_ReturnsEmpty()
    {
        Assert.Equal("", RecipeStepTextFormatter.ToHtml(null));
        Assert.Equal("", RecipeStepTextFormatter.ToHtml(""));
    }
}
