using CookBot.Domain.Enums;

namespace CookBot.Application.DTOs;

public class CookBotSettings
{
    public AuthMode AuthMode { get; set; } = AuthMode.Disabled;
    public string AppName { get; set; } = "CookBot";
    public string AnthropicApiKey { get; set; } = string.Empty;
}
