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
}
