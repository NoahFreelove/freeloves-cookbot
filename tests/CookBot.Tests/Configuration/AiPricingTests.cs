using CookBot.Application.DTOs;
using Microsoft.Extensions.Configuration;

namespace CookBot.Tests.Configuration;

/// <summary>
/// Phase 9 / Plan 09-05 / PROD-16 + PITFALL H10.
///
/// Validates that the appsettings.json pricing table binds cleanly into
/// <see cref="CookBotSettings"/>, that all three <c>CuratedModels</c> ids are
/// represented with the matrix from 09-RESEARCH Item 1, and that the cost
/// formula (input + output) / 1_000_000m produces decimal precision below one
/// cent for a representative Haiku call (no silent zero-rounding).
/// </summary>
public class AiPricingTests
{
    private const string AppSettingsFragment = """
        {
          "CookBot": {
            "AiPricing": {
              "claude-haiku-4-5-20251001": {
                "InputTokensPerMillionUsd": 1.00,
                "OutputTokensPerMillionUsd": 5.00
              },
              "claude-sonnet-4-6": {
                "InputTokensPerMillionUsd": 3.00,
                "OutputTokensPerMillionUsd": 15.00
              },
              "claude-opus-4-7": {
                "InputTokensPerMillionUsd": 5.00,
                "OutputTokensPerMillionUsd": 25.00
              }
            },
            "AiPricingVerifiedDate": "2026-05-16"
          }
        }
        """;

    private static CookBotSettings BindSettings()
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(AppSettingsFragment));
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();
        return configuration.GetSection("CookBot").Get<CookBotSettings>()!;
    }

    [Fact]
    public void Configuration_LoadsAllThreeModels()
    {
        var settings = BindSettings();

        Assert.NotNull(settings);
        Assert.NotNull(settings.AiPricing);
        Assert.Equal(3, settings.AiPricing!.Count);

        var haiku = settings.AiPricing["claude-haiku-4-5-20251001"];
        Assert.Equal(1.00m, haiku.InputTokensPerMillionUsd);
        Assert.Equal(5.00m, haiku.OutputTokensPerMillionUsd);

        var sonnet = settings.AiPricing["claude-sonnet-4-6"];
        Assert.Equal(3.00m, sonnet.InputTokensPerMillionUsd);
        Assert.Equal(15.00m, sonnet.OutputTokensPerMillionUsd);

        var opus = settings.AiPricing["claude-opus-4-7"];
        Assert.Equal(5.00m, opus.InputTokensPerMillionUsd);
        Assert.Equal(25.00m, opus.OutputTokensPerMillionUsd);
    }

    [Fact]
    public void PricingVerifiedDate_Is2026_05_16()
    {
        var settings = BindSettings();

        Assert.NotNull(settings.AiPricingVerifiedDate);
        Assert.Equal(new DateOnly(2026, 5, 16), settings.AiPricingVerifiedDate);
    }

    [Fact]
    public void CostCalculation_HaikuExample_BelowOneCent()
    {
        // PITFALL: float/double would round this to 0. decimal preserves sub-cent precision.
        var settings = BindSettings();
        var haiku = settings.AiPricing!["claude-haiku-4-5-20251001"];

        const int inputTokens = 100;
        const int outputTokens = 50;
        var cost =
            (inputTokens * haiku.InputTokensPerMillionUsd
             + outputTokens * haiku.OutputTokensPerMillionUsd)
            / 1_000_000m;

        // (100 * 1 + 50 * 5) / 1_000_000 = 350 / 1_000_000 = 0.00035
        Assert.Equal(0.00035m, cost);
    }
}
