using System.Text.Json;
using CookBot.Application.AI;
using CookBot.Application.Recipes;
using CookBot.Application.DTOs;
using CookBot.Application.Services;
using CookBot.Domain.Recipes;
using CookBot.Infrastructure.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CookBot.Tests.AI;

/// <summary>
/// AI-SPEC §5 Reference Dataset eval — exercises 5 prompt fixtures against the
/// LIVE Anthropic API via <see cref="IAiRecipeGenerator"/>. Each fixture's
/// .golden.json file declares structural expectations (ingredient/step count
/// bounds, hasSections, hasTimers); this test asserts the generated
/// <see cref="RecipeDocument"/> matches.
///
/// Gated by the <c>RequiresApiKey</c> trait. The CI offline gate
/// (<c>--filter "Category!=RequiresApiKey"</c>) skips this class. Milestone
/// verification is the on-demand command:
///   <c>ANTHROPIC_API_KEY=sk-ant-... dotnet test FreelovesCookBot.sln</c>
///
/// No real API call is made unless <c>ANTHROPIC_API_KEY</c> is set in env.
/// </summary>
public class AiRecipeFixtureTests
{
    /// <summary>
    /// xUnit Theory data — discovered at test-collection time. Reads each
    /// .txt prompt file + .golden.json sibling under
    /// <c>AppContext.BaseDirectory/AI/Fixtures/RecipePrompts/</c>. If the
    /// fixtures dir is empty/missing, the Theory has zero rows (build does
    /// not fail).
    /// </summary>
    public static IEnumerable<object[]> FixturePrompts()
    {
        var fixturesDir = Path.Combine(
            AppContext.BaseDirectory, "AI", "Fixtures", "RecipePrompts");
        if (!Directory.Exists(fixturesDir))
            yield break;

        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        foreach (var promptFile in Directory.EnumerateFiles(fixturesDir, "*.txt").OrderBy(p => p))
        {
            var name = Path.GetFileNameWithoutExtension(promptFile);
            var goldenFile = Path.Combine(fixturesDir, $"{name}.golden.json");
            if (!File.Exists(goldenFile)) continue;

            var prompt = File.ReadAllText(promptFile).Trim();
            var golden = JsonSerializer.Deserialize<FixtureGolden>(
                File.ReadAllText(goldenFile), jsonOpts)!;

            yield return new object[] { name, prompt, golden };
        }
    }

    private static IAiRecipeGenerator BuildGenerator(out string apiKey)
    {
        apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                 ?? throw new InvalidOperationException(
                     "ANTHROPIC_API_KEY required for live API test. " +
                     "Use --filter \"Category!=RequiresApiKey\" to skip gated tests.");

        // Build the real production stack — same wiring as Program.cs DI.
        var settings = Options.Create(new CookBotSettings { AnthropicApiKey = apiKey });
        var validator = new RecipeValidator();
        var schemaProvider = new RecipeJsonSchemaProvider();
        var docProvider = new RecipeSchemaDocumentationProvider();

        var photoValidator = new RecipePhotoUrlValidator();
        var ai = new AnthropicAiService(settings, validator, photoValidator);
        return new AiRecipeGenerator(
            ai, schemaProvider, validator, docProvider,
            new NoOpAiUsageLogWriter(),
            settings,
            NullLogger<AiRecipeGenerator>.Instance);
    }

    /// <summary>Telemetry off for the live-API fixture tests (no userId plumbed).</summary>
    private sealed class NoOpAiUsageLogWriter : IAiUsageLogWriter
    {
        public Task WriteAsync(
            int userId, int keyOwnerId, string modelName,
            int inputTokens, int outputTokens, decimal estimatedCostUsd,
            bool isRetryAttempt, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    [Trait("Category", "RequiresApiKey")]
    [Theory]
    [MemberData(nameof(FixturePrompts))]
    public async Task Fixture_GeneratesStructurallyValidRecipe(
        string fixtureName, string prompt, FixtureGolden golden)
    {
        var sut = BuildGenerator(out var apiKey);

        var result = await sut.GenerateAsync(prompt, apiKey);

        // Primary gate: structured success. On failure, surface the validator
        // error list so the developer immediately sees what regressed.
        Assert.True(result.Ok,
            $"Fixture '{fixtureName}' did not produce a valid recipe. " +
            $"SanitizedError={result.SanitizedError}; " +
            $"Validation errors=[{string.Join("; ", result.Validation?.Errors.Select(e => $"{e.Path}: {e.Message}") ?? Array.Empty<string>())}]");

        Assert.NotNull(result.Value);
        var recipe = result.Value!;

        // Structural assertions against the golden file.
        Assert.True(recipe.Ingredients.Count >= golden.IngredientCountMin,
            $"Fixture '{fixtureName}': expected >= {golden.IngredientCountMin} ingredients, got {recipe.Ingredients.Count}");
        if (golden.IngredientCountMax is int imax)
            Assert.True(recipe.Ingredients.Count <= imax,
                $"Fixture '{fixtureName}': expected <= {imax} ingredients, got {recipe.Ingredients.Count}");

        Assert.True(recipe.Steps.Count >= golden.StepCountMin,
            $"Fixture '{fixtureName}': expected >= {golden.StepCountMin} steps, got {recipe.Steps.Count}");
        if (golden.StepCountMax is int smax)
            Assert.True(recipe.Steps.Count <= smax,
                $"Fixture '{fixtureName}': expected <= {smax} steps, got {recipe.Steps.Count}");

        var hasSections = recipe.Steps.OfType<SectionStep>().Any();
        Assert.Equal(golden.HasSections, hasSections);

        if (golden.HasTimers is bool expectedTimers)
        {
            var hasTimers = recipe.Steps.OfType<ContentStep>()
                .Any(s => s.Timers is not null && s.Timers.Count > 0);
            Assert.Equal(expectedTimers, hasTimers);
        }
    }
}
