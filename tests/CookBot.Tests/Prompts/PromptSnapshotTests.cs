using CookBot.Application.Services;
using CookBot.Domain.Entities;

namespace CookBot.Tests.Prompts;

// D-35: Verify-based snapshot test (replaces hand-rolled fixture-equality in Phase 1).
// [UseVerify] is injected at assembly level by the Verify.Xunit MSBuild target — no class attribute needed.
public class PromptSnapshotTests
{
    [Fact]
    public Task BuildSystemPrompt()
    {
        var profile = TestHost.MakeProfile();
        var pantry = Array.Empty<PantryItem>();
        var svc = TestHost.GetPromptBuilderService();
        var actual = svc.ResolveTemplate(PromptBuilderService.DefaultTemplate, profile, pantry);
        return Verifier.Verify(actual);
    }

    /// <summary>
    /// Self-checking test: if a developer embeds a SCHEMA-10 alias token (e.g., "imageUrl")
    /// into a prompt template, Verify will surface it as a diff against the .verified.txt
    /// baseline. This second Fact proves the rendering path picks up such tokens.
    /// Combined with PromptDenylistTests (source-scan), this gives two-layer protection.
    /// </summary>
    [Fact]
    public void BuildSystemPrompt_WithAliasInTemplate_DiffsAreVisible()
    {
        // Synthetic template that injects a SCHEMA-10 alias token.
        const string templateWithAlias = "use the imageUrl field instead of photoUrl\n{{recipe_format}}";

        var profile = TestHost.MakeProfile();
        var pantry = Array.Empty<PantryItem>();
        var svc = TestHost.GetPromptBuilderService();
        var rendered = svc.ResolveTemplate(templateWithAlias, profile, pantry);

        // The alias token must appear verbatim in the rendered output —
        // demonstrating that Verify.Verify (on BuildSystemPrompt) would diff it if the
        // DefaultTemplate ever picked up such an alias phrase.
        Assert.Contains("imageUrl", rendered);
    }
}
