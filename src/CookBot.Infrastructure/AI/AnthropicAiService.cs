using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CookBot.Application.DTOs;
using CookBot.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace CookBot.Infrastructure.AI;

public class AnthropicAiService : IAiService
{
    public static readonly List<AiModelInfo> CuratedModels = new()
    {
        new("claude-haiku-4-5-20251001", "Claude Haiku 4.5 (Fast)"),
        new("claude-sonnet-4-6-20250514", "Claude Sonnet 4.6 (Balanced)"),
        new("claude-opus-4-6-20250514", "Claude Opus 4.6 (Most Capable)")
    };

    public const string DefaultModelId = "claude-sonnet-4-6-20250514";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly CookBotSettings _settings;

    public AnthropicAiService(IOptions<CookBotSettings> settings)
    {
        _settings = settings.Value;
    }

    private HttpClient CreateHttpClient(string? apiKey)
    {
        var key = apiKey ?? _settings.AnthropicApiKey;
        if (string.IsNullOrEmpty(key))
            throw new InvalidOperationException("No Anthropic API key configured. Set it in your profile or appsettings.json.");

        var http = new HttpClient();
        http.DefaultRequestHeaders.Add("x-api-key", key);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        return http;
    }

    public async Task<List<AiModelInfo>> ListModelsAsync(string apiKey)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        var response = await http.GetAsync("https://api.anthropic.com/v1/models");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("data").EnumerateArray()
            .Select(m => new AiModelInfo(m.GetProperty("id").GetString()!, m.GetProperty("id").GetString()!))
            .OrderBy(m => m.Id)
            .ToList();
    }

    public async Task<string> SendMessageAsync(string systemPrompt, List<AiMessage> messages, string? apiKey = null, string? modelId = null)
    {
        using var http = CreateHttpClient(apiKey);
        var payload = BuildPayload(systemPrompt, messages, modelId, stream: false);
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        var response = await http.PostAsync("https://api.anthropic.com/v1/messages", content);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Anthropic API error: {body}");

        var doc = JsonDocument.Parse(body);
        return ExtractText(doc.RootElement);
    }

    public async IAsyncEnumerable<string> StreamMessageAsync(string systemPrompt, List<AiMessage> messages, string? apiKey = null, string? modelId = null)
    {
        using var http = CreateHttpClient(apiKey);
        var payload = BuildPayload(systemPrompt, messages, modelId, stream: true);
        var requestContent = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Content = requestContent;

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(default);
            throw new HttpRequestException($"Anthropic API error: {errorBody}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(default);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(default);
            if (line == null) break;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            string? textChunk = null;
            try
            {
                var evt = JsonDocument.Parse(data);
                var type = evt.RootElement.GetProperty("type").GetString();
                if (type == "content_block_delta")
                {
                    var delta = evt.RootElement.GetProperty("delta");
                    if (delta.TryGetProperty("text", out var text))
                        textChunk = text.GetString();
                }
            }
            catch (JsonException)
            {
                // Skip malformed events
            }

            if (textChunk != null)
                yield return textChunk;
        }
    }

    public async Task<bool> TestConnectionAsync(string? apiKey = null)
    {
        try
        {
            using var http = CreateHttpClient(apiKey);
            var payload = new Dictionary<string, object>
            {
                ["model"] = "claude-haiku-4-5-20251001",
                ["messages"] = new[] { new { role = "user", content = "Hello" } },
                ["max_tokens"] = 10,
            };
            var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
            var response = await http.PostAsync("https://api.anthropic.com/v1/messages", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<string, object> BuildPayload(string systemPrompt, List<AiMessage> messages, string? modelId, bool stream)
    {
        var payload = new Dictionary<string, object>
        {
            ["model"] = modelId ?? DefaultModelId,
            ["system"] = systemPrompt,
            ["messages"] = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            ["max_tokens"] = 4096,
        };
        if (stream)
            payload["stream"] = true;
        return payload;
    }

    private static string ExtractText(JsonElement root)
    {
        if (!root.TryGetProperty("content", out var content)) return "";
        var sb = new StringBuilder();
        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var type) && type.GetString() == "text"
                && block.TryGetProperty("text", out var text))
            {
                sb.Append(text.GetString());
            }
        }
        return sb.ToString();
    }
}
