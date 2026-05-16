using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CookBot.Tests.Prompts;

// D-22 anti-regression. Reads the source files of PromptBuilderService.cs and
// RecipeSchemaDocumentationProvider.cs at test time and fails if any case-insensitive
// match for the opt-out / alias-token denylist regex appears. Closes Pitfall H6 and SCHEMA-10.
// D-36: extended with seven photo/description/temperature alias tokens (image, imageUrl,
// picture, summary, desc, temp, oven). Word-boundary anchors (\b) exclude substrings:
// "temperature" does not match \btemp\b; "imageUrl" matches \bimage\b (image is a prefix
// without a word boundary after it only if followed by a word char — but imageUrl: U is
// a word char, so \bimage\b does NOT match inside "imageUrl"). Both "image" and "imageUrl"
// independently appear in the alternation so either standalone use is caught.
public class PromptDenylistTests
{
    internal static readonly Regex Denylist =
        new(@"\b(fallback|informal|plain numbered|If you can'?t follow|image|imageUrl|picture|summary|desc|temp|oven)\b",
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

    /// <summary>
    /// Self-check: the extended denylist regex fires on an alias token in synthetic input.
    /// This proves the regex is operational and would catch a future regression where someone
    /// types an alias (e.g., "imageUrl") into a prompt source file.
    /// </summary>
    [Fact]
    public void Denylist_FiresOn_AliasToken_InSyntheticInput()
    {
        // Positive assertion: a synthetic source string containing an alias token must match.
        // The regex uses \\bimageUrl\\b to catch the exact alias token with word boundaries.
        var syntheticWithAlias = "// dev note: use the imageUrl field instead of photoUrl";
        Assert.True(
            Denylist.Matches(syntheticWithAlias).Count > 0,
            "Denylist regex must catch alias token 'imageUrl' in synthetic source string.");

        // Negative assertion: legitimate English prose containing "temperature" must NOT match
        // because "temperature" contains the substring "temp" but \btemp\b requires a word
        // boundary — the 'e' in "temperature" is a word character, so \btemp\b does not match.
        var syntheticLegitimate = "the temperature is 350 degrees";
        Assert.False(
            Denylist.Matches(syntheticLegitimate).Count > 0,
            "Denylist regex uses \\btemp\\b — 'temperature' must not match (no word boundary after 'temp' in 'temperature').");
    }
}
