using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CookBot.Tests.Prompts;

/// <summary>
/// D-22 anti-regression. Reads the source files of <c>PromptBuilderService.cs</c> and
/// <c>RecipeSchemaDocumentationProvider.cs</c> at test time and fails if any
/// case-insensitive match for the opt-out denylist regex appears. Closes Pitfall H6.
/// </summary>
public class PromptDenylistTests
{
    private static readonly Regex Denylist =
        new(@"\b(fallback|informal|plain numbered|If you can'?t follow)\b",
            RegexOptions.IgnoreCase);

    [Theory]
    [InlineData("src/CookBot.Application/Services/PromptBuilderService.cs")]
    [InlineData("src/CookBot.Application/Recipes/RecipeSchemaDocumentationProvider.cs")]
    public void PromptSourceFiles_ContainNoOptOutPhrases(string relativePath)
    {
        var repoRoot = TestHost.FindRepoRoot();
        var full = Path.Combine(repoRoot, relativePath);
        Assert.True(File.Exists(full), $"Source file not found: {full}");

        var src = File.ReadAllText(full);
        var matches = Denylist.Matches(src).Select(m => m.Value).ToList();
        Assert.True(
            matches.Count == 0,
            $"Found opt-out phrases in {relativePath}: {string.Join(", ", matches)}");
    }
}
