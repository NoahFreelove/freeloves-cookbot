namespace CookBot.Application.DTOs;

/// <summary>
/// Phase 9 / Plan 09-05 / PROD-16 + PITFALL H10 — per-model Anthropic pricing
/// row, keyed in <see cref="CookBotSettings.AiPricing"/> by a <c>CuratedModels</c>
/// id (e.g. "claude-sonnet-4-6"). Values are decimal-typed because per-token cost
/// math is currency arithmetic; float/double silently rounds sub-cent results
/// to zero (a 100/50-token Haiku call is $0.00035, not 0).
/// </summary>
/// <remarks>
/// The keys in <c>appsettings.json</c> intentionally match the JSON property
/// names exactly — the planner explicitly chose self-documenting names over
/// terseness so a self-hoster updating prices doesn't have to cross-reference
/// the POCO.
/// </remarks>
public sealed class AiPricingEntry
{
    public decimal InputTokensPerMillionUsd { get; set; }
    public decimal OutputTokensPerMillionUsd { get; set; }
}
