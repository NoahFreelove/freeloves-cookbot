using CookBot.Domain.Enums;

namespace CookBot.Application.DTOs;

public class CookBotSettings
{
    /// <summary>
    /// Reserved for future use; not enforced by the app yet. Do not rely on this for security.
    /// </summary>
    public AuthMode AuthMode { get; set; } = AuthMode.Disabled;

    public string AppName { get; set; } = "CookBot";

    /// <summary>
    /// When false, the host disables optional AI integration (assistant, prompt builder, profile AI controls)
    /// for all users regardless of profile.
    /// </summary>
    public bool AiFeaturesEnabled { get; set; } = true;

    public string AnthropicApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of `.pre-*.bak` files to retain alongside the SQLite DB. Default 3 (D-15).
    /// Effective range: clamped to [1, 10] at runtime by `DatabaseBackupService`.
    /// </summary>
    public int DatabaseBackupRetention { get; set; } = 3;

    /// <summary>
    /// Phase 9 / Plan 09-05 / PROD-16 — operator-editable per-model pricing matrix used
    /// to compute <c>AiUsageLog.EstimatedCostUsd</c>. Keys are <c>CuratedModels</c> ids
    /// (claude-haiku-4-5-20251001 / claude-sonnet-4-6 / claude-opus-4-7). Null when the
    /// host hasn't supplied the section — pricing falls back to 0 in that case (telemetry
    /// row still written, EstimatedCostUsd = 0). PITFALL H10: surface
    /// <see cref="AiPricingVerifiedDate"/> as a footnote so stale rates are visible.
    /// </summary>
    public Dictionary<string, AiPricingEntry>? AiPricing { get; set; }

    /// <summary>
    /// Phase 9 / Plan 09-05 / PITFALL H10 — date the <see cref="AiPricing"/> matrix was
    /// last verified against Anthropic's published prices. Surfaced as a footnote on
    /// the Phase 10 per-user AI-usage widget so operators know when to refresh the
    /// values in <c>appsettings.json</c>.
    /// </summary>
    public DateOnly? AiPricingVerifiedDate { get; set; }
}
