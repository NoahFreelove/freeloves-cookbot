using Anthropic;
using CookBot.Application.DTOs;
using CookBot.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace CookBot.Infrastructure.AI;

public class AnthropicAiService : IAiService
{
    private readonly CookBotSettings _settings;

    public AnthropicAiService(IOptions<CookBotSettings> settings)
    {
        _settings = settings.Value;
    }

    private AnthropicApi CreateClient(string? apiKey)
    {
        var key = apiKey ?? _settings.AnthropicApiKey;
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException("No Anthropic API key configured. Set it in your profile or appsettings.json.");

        var api = new AnthropicApi();
        api.AuthorizeUsingApiKey(key);
        api.SetHeaders();
        return api;
    }

    public async Task<string> SendMessageAsync(string systemPrompt, List<AiMessage> messages, string? apiKey = null)
    {
        var api = CreateClient(apiKey);
        var request = BuildRequest(systemPrompt, messages);
        var response = await api.CreateMessageAsync(request);
        return ExtractText(response);
    }

    public async IAsyncEnumerable<string> StreamMessageAsync(string systemPrompt, List<AiMessage> messages, string? apiKey = null)
    {
        var api = CreateClient(apiKey);
        var request = BuildRequest(systemPrompt, messages);

        await foreach (var evt in api.CreateMessageAsStreamAsync(request))
        {
            if (evt.IsContentBlockDelta)
            {
                var delta = evt.ContentBlockDelta;
                if (delta != null && delta.Delta.IsValue1)
                {
                    var textDelta = delta.Delta.Value1;
                    if (textDelta?.Text != null)
                        yield return textDelta.Text;
                }
            }
        }
    }

    public async Task<bool> TestConnectionAsync(string? apiKey = null)
    {
        try
        {
            var api = CreateClient(apiKey);
            var request = new CreateMessageRequest
            {
                Model = CreateMessageRequestModel.Claude3Haiku20240307,
                Messages = new List<Message> { "Hello".AsUserMessage() },
                MaxTokens = 10,
            };
            await api.CreateMessageAsync(request);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static CreateMessageRequest BuildRequest(string systemPrompt, List<AiMessage> messages)
    {
        return new CreateMessageRequest
        {
            Model = CreateMessageRequestModel.Claude35Sonnet20240620,
            System = systemPrompt,
            Messages = messages.Select(m => m.Role == "user"
                ? m.Content.AsUserMessage()
                : m.Content.AsAssistantMessage()).ToList(),
            MaxTokens = 4096,
            Temperature = 0.7,
        };
    }

    private static string ExtractText(Message response)
    {
        if (response.Content.IsValue1)
            return response.Content.Value1 ?? "";
        if (response.Content.IsValue2)
        {
            var blocks = response.Content.Value2;
            if (blocks != null)
            {
                return string.Join("", blocks
                    .Where(b => b.IsText)
                    .Select(b => b.Text?.Text ?? ""));
            }
        }
        return "";
    }
}
