using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CookBot.Application.AI;
using CookBot.Application.DTOs;
using CookBot.Application.Recipes;
using CookBot.Domain.Interfaces;
using CookBot.Domain.Recipes;
using Microsoft.Extensions.Options;

namespace CookBot.Infrastructure.AI;

public class AnthropicAiService : IAiService, IStructuredAiService
{
    public static readonly List<AiModelInfo> CuratedModels = new()
    {
        new("claude-haiku-4-5-20251001", "Claude Haiku 4.5 (Fast)"),
        new("claude-sonnet-4-6", "Claude Sonnet 4.6 (Balanced)"),
        new("claude-opus-4-7", "Claude Opus 4.7 (Most Capable)")
    };

    public const string DefaultModelId = "claude-sonnet-4-6";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly CookBotSettings _settings;
    private readonly RecipeValidator _validator;

    public AnthropicAiService(IOptions<CookBotSettings> settings, RecipeValidator validator)
    {
        _settings = settings.Value;
        _validator = validator;
    }

    protected virtual HttpClient CreateHttpClient(string? apiKey)
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

    public async Task<string> SendMessageAsync(string systemPrompt, List<AiMessage> messages, string? apiKey = null, string? modelId = null, int maxTokens = 4096)
    {
        using var http = CreateHttpClient(apiKey);
        var payload = BuildPayload(systemPrompt, messages, modelId, stream: false, maxTokens);
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

        while (true)
        {
            var line = await reader.ReadLineAsync(default);
            if (line is null) break;
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

    private static Dictionary<string, object> BuildPayload(string systemPrompt, List<AiMessage> messages, string? modelId, bool stream, int maxTokens = 4096)
    {
        var payload = new Dictionary<string, object>
        {
            ["model"] = modelId ?? DefaultModelId,
            ["system"] = systemPrompt,
            ["messages"] = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            ["max_tokens"] = maxTokens,
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
            var typeStr = block.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
            // Skip extended-thinking payloads; pantry import expects only the visible assistant reply.
            if (typeStr is "thinking" or "redacted_thinking")
                continue;

            if (block.TryGetProperty("text", out var text))
            {
                var s = text.GetString();
                if (!string.IsNullOrEmpty(s))
                    sb.Append(s);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// AI-01 structured-output transport. Wires Anthropic's <c>output_config.format</c>
    /// with <c>strict: true</c>, accumulates SSE deltas, deserializes the JSON body,
    /// runs <see cref="RecipeValidator"/> for <see cref="RecipeDocument"/> Ts, and
    /// surfaces every failure as a <see cref="StructuredResult{T}"/> envelope (D-02).
    /// Never throws, except for <see cref="OperationCanceledException"/> from the
    /// supplied <paramref name="ct"/>.
    /// </summary>
    public async Task<StructuredResult<T>> SendStructuredAsync<T>(
        string systemPrompt,
        List<AiMessage> messages,
        JsonNode schema,
        string? apiKey = null,
        string? modelId = null,
        int maxTokens = 4096,
        CancellationToken ct = default)
        where T : class
    {
        // resolvedKey is the verbatim secret to scrub from any error message.
        // CreateHttpClient handles fallback to settings if apiKey is null.
        var resolvedKey = apiKey ?? _settings.AnthropicApiKey;

        HttpClient http;
        try
        {
            http = CreateHttpClient(apiKey);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new StructuredResult<T>(
                Ok: false, Value: null, RawResponse: null, Validation: null,
                SanitizedError: SecretRedactor.Redact($"AI client init failure: {ex.Message}", resolvedKey));
        }

        try
        {
            // D-10: output_config.format with type=json_schema, the cached schema node, strict=true.
            var outputConfig = new
            {
                format = new
                {
                    type = "json_schema",
                    schema = schema,
                    strict = true
                }
            };

            var payload = new Dictionary<string, object>
            {
                ["model"]         = modelId ?? DefaultModelId,
                ["system"]        = systemPrompt,
                ["messages"]      = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                ["max_tokens"]    = maxTokens,
                ["stream"]        = true,           // SSE under the hood (D-01)
                ["output_config"] = outputConfig,
            };

            var requestContent = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Content = requestContent;

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new StructuredResult<T>(
                    Ok: false, Value: null, RawResponse: null, Validation: null,
                    SanitizedError: SecretRedactor.Redact($"AI transport failure: {ex.Message}", resolvedKey));
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    return new StructuredResult<T>(
                        Ok: false, Value: null, RawResponse: null, Validation: null,
                        SanitizedError: SecretRedactor.Redact(
                            $"Anthropic API error {(int)response.StatusCode}: {errorBody}",
                            resolvedKey));
                }

                // SSE accumulation — match StreamMessageAsync line-reading discipline.
                var accumulated = new StringBuilder();
                string? stopReason = null;

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream);

                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    var line = await reader.ReadLineAsync(ct);
                    if (line is null) break;
                    if (!line.StartsWith("data: ")) continue;
                    var data = line["data: ".Length..];
                    if (data == "[DONE]") break;

                    try
                    {
                        using var evt = JsonDocument.Parse(data);
                        var type = evt.RootElement.GetProperty("type").GetString();

                        // P-4: structured-output JSON arrives in content_block_delta.delta.text.
                        if (type == "content_block_delta")
                        {
                            var delta = evt.RootElement.GetProperty("delta");
                            if (delta.TryGetProperty("text", out var text))
                                accumulated.Append(text.GetString());
                        }
                        // P-5: capture stop_reason from message_delta — short-circuit on refusal.
                        else if (type == "message_delta")
                        {
                            if (evt.RootElement.TryGetProperty("delta", out var d) &&
                                d.TryGetProperty("stop_reason", out var sr) &&
                                sr.ValueKind == JsonValueKind.String)
                            {
                                stopReason = sr.GetString();
                            }
                        }
                    }
                    catch (JsonException) { /* skip malformed SSE events */ }
                }

                // Critical-constraint #2: refusals do not converge under repair — short-circuit.
                if (stopReason == "refusal")
                {
                    return new StructuredResult<T>(
                        Ok: false, Value: null, RawResponse: null, Validation: null,
                        SanitizedError: "The AI declined to produce a recipe for this request.");
                }

                // Typed deserialize.
                T? doc;
                JsonNode? rawNode = null;
                try
                {
                    var json = accumulated.ToString();
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return new StructuredResult<T>(
                            Ok: false, Value: null, RawResponse: null, Validation: null,
                            SanitizedError: "Model returned empty content.");
                    }
                    rawNode = JsonNode.Parse(json);
                    doc = JsonSerializer.Deserialize<T>(json, JsonOptions);
                }
                catch (JsonException ex)
                {
                    return new StructuredResult<T>(
                        Ok: false, Value: null, RawResponse: rawNode, Validation: null,
                        SanitizedError: SecretRedactor.Redact(
                            $"Deserialization failed: {ex.Message}", resolvedKey));
                }

                if (doc is null)
                {
                    return new StructuredResult<T>(
                        Ok: false, Value: null, RawResponse: rawNode, Validation: null,
                        SanitizedError: "Model returned empty content.");
                }

                // Semantic validation runs only for RecipeDocument. Other Ts skip the validator.
                if (doc is RecipeDocument recipeDoc)
                {
                    var validation = _validator.Validate(recipeDoc);
                    return new StructuredResult<T>(
                        Ok: validation.IsValid,
                        Value: validation.IsValid ? doc : null,
                        RawResponse: rawNode,
                        Validation: validation,
                        SanitizedError: null);
                }

                // For non-recipe Ts: deserialization success implies Ok.
                return new StructuredResult<T>(
                    Ok: true, Value: doc, RawResponse: rawNode, Validation: null, SanitizedError: null);
            }
        }
        finally
        {
            http.Dispose();
        }
    }
}
