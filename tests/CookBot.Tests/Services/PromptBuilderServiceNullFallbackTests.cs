using CookBot.Domain.Entities;

namespace CookBot.Tests.Services;

/// <summary>
/// Tests that BuildSystemPrompt correctly handles the three branches of
/// profile.AiSystemPromptTemplate: null, whitespace-only, and non-empty custom.
/// Phase 10 / Plan 10-06 / D-52 — null-fallback correctness.
/// </summary>
public class PromptBuilderServiceNullFallbackTests
{
    [Fact]
    public void BuildSystemPrompt_NullTemplate_UsesDefault()
    {
        var profile = TestHost.MakeProfile();
        profile.AiSystemPromptTemplate = null;
        var svc = TestHost.GetPromptBuilderService();
        var rendered = svc.BuildSystemPrompt(profile, Array.Empty<PantryItem>());
        Assert.Contains("CookBot, an expert AI cooking assistant", rendered);
    }

    [Fact]
    public void BuildSystemPrompt_WhitespaceTemplate_UsesDefault()
    {
        var profile = TestHost.MakeProfile();
        profile.AiSystemPromptTemplate = "   \n\t  ";
        var svc = TestHost.GetPromptBuilderService();
        var rendered = svc.BuildSystemPrompt(profile, Array.Empty<PantryItem>());
        Assert.Contains("CookBot, an expert AI cooking assistant", rendered);
    }

    [Fact]
    public void BuildSystemPrompt_CustomTemplate_RespectsOverride()
    {
        var profile = TestHost.MakeProfile();
        profile.AiSystemPromptTemplate = "You are Bob. {{recipe_format}}";
        var svc = TestHost.GetPromptBuilderService();
        var rendered = svc.BuildSystemPrompt(profile, Array.Empty<PantryItem>());
        Assert.Contains("Bob", rendered);
        Assert.DoesNotContain("CookBot, an expert AI cooking assistant", rendered);
    }
}
