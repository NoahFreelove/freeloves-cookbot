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
}
