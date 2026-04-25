using System;
using System.IO;
using CookBot.Application.Services;
using CookBot.Domain.Entities;

namespace CookBot.Tests.Prompts;

/// <summary>
/// D-21 hand-rolled snapshot test. Asserts the assembled system prompt for a deterministic
/// fixture profile matches the committed text fixture. Set <c>UPDATE_SNAPSHOTS=1</c> to
/// regenerate the fixture (intentional changes only — the diff lands in PR review).
/// </summary>
public class PromptSnapshotTests
{
    [Fact]
    public void DefaultTemplate_AssembledPrompt_MatchesSnapshot()
    {
        var profile = TestHost.MakeProfile();
        var pantry = Array.Empty<PantryItem>();
        var svc = TestHost.GetPromptBuilderService();
        var actual = svc.ResolveTemplate(PromptBuilderService.DefaultTemplate, profile, pantry);

        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Prompts",
            "expected-system-prompt.txt");

        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)!);
            File.WriteAllText(fixturePath, actual);
            return;
        }

        Assert.True(
            File.Exists(fixturePath),
            $"Snapshot fixture missing at {fixturePath}. " +
            "Run with UPDATE_SNAPSHOTS=1 to generate, then commit the file.");

        var expected = File.ReadAllText(fixturePath);
        Assert.Equal(expected, actual);
    }
}
