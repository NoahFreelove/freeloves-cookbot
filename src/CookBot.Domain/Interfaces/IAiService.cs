namespace CookBot.Domain.Interfaces;

public class AiMessage
{
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
}

public interface IAiService
{
    Task<string> SendMessageAsync(string systemPrompt, List<AiMessage> messages, string? apiKey = null);
    IAsyncEnumerable<string> StreamMessageAsync(string systemPrompt, List<AiMessage> messages, string? apiKey = null);
    Task<bool> TestConnectionAsync(string? apiKey = null);
}
