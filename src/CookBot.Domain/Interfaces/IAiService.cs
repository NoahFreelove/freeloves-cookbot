namespace CookBot.Domain.Interfaces;

public class AiMessage
{
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
}

public record AiModelInfo(string Id, string DisplayName);

public interface IAiService
{
    Task<List<AiModelInfo>> ListModelsAsync(string apiKey);
    Task<string> SendMessageAsync(string systemPrompt, List<AiMessage> messages, string? apiKey = null, string? modelId = null, int maxTokens = 4096);
    IAsyncEnumerable<string> StreamMessageAsync(string systemPrompt, List<AiMessage> messages, string? apiKey = null, string? modelId = null);
    Task<bool> TestConnectionAsync(string? apiKey = null);
}
