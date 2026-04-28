using CookBot.Application.Services;

namespace CookBot.Tests.Web;

public class TimerSuggestionTests
{
    [Fact]
    public void ToHtmlWithTimerSuggestions_WrapsDetectedSubstrings()
    {
        var html = RecipeStepTextFormatter.ToHtmlWithTimerSuggestions(
            "Bake for 25 minutes until set",
            alreadyConvertedDurationsSeconds: new HashSet<int>());
        Assert.Contains("class=\"timer-suggestion\"", html);
        Assert.Contains("data-duration-seconds=\"1500\"", html);
        Assert.Contains("25 minutes", html);
    }

    [Fact]
    public void ToHtmlWithTimerSuggestions_SkipsAlreadyConverted_DC3()
    {
        var alreadyConverted = new HashSet<int> { 1500 }; // 25 minutes already an explicit chip
        var html = RecipeStepTextFormatter.ToHtmlWithTimerSuggestions(
            "Bake for 25 minutes until set",
            alreadyConvertedDurationsSeconds: alreadyConverted);
        Assert.DoesNotContain("class=\"timer-suggestion\"", html);
    }

    [Fact]
    public void ToHtmlWithTimerSuggestions_DoesNotWrapInsideIngredientLink()
    {
        // Ingredient name "5 minute rice" — the "5 minute" substring is INSIDE [name](#id);
        // wrapping would corrupt the chip rendering.
        var html = RecipeStepTextFormatter.ToHtmlWithTimerSuggestions(
            "Add [5 minute rice](#1) and stir",
            alreadyConvertedDurationsSeconds: new HashSet<int>());
        // The rendered ingredient-ref span must not contain a nested timer-suggestion span.
        Assert.DoesNotContain("data-ingredient-id=\"1\"><span class=\"timer-suggestion\"", html);
        // Stronger guarantee: no timer-suggestion span at all in this case (the only duration
        // candidate was inside the ingredient link).
        Assert.DoesNotContain("class=\"timer-suggestion\"", html);
    }

    [Fact]
    public void ToHtmlWithTimerSuggestions_DetectsRange_PersistsLowest()
    {
        var html = RecipeStepTextFormatter.ToHtmlWithTimerSuggestions(
            "Roast 20-25 minutes",
            alreadyConvertedDurationsSeconds: new HashSet<int>());
        Assert.Contains("data-duration-seconds=\"1200\"", html); // lowest = 20 min
    }

    [Fact]
    public void ToHtmlWithTimerSuggestions_HtmlEncodesScriptInjection_T03P03_01()
    {
        // Threat T-03P03-01: ensure the inner substring is HTML-encoded so a step containing
        // a malicious "<script>" cannot inject HTML through the timer-suggestion wrap.
        var html = RecipeStepTextFormatter.ToHtmlWithTimerSuggestions(
            "Wait <script>alert(1)</script> 25 minutes",
            alreadyConvertedDurationsSeconds: new HashSet<int>());
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
        // The timer wrap still applies for the genuine duration.
        Assert.Contains("data-duration-seconds=\"1500\"", html);
    }
}
