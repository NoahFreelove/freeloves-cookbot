using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CookBot.Application.AI;
using CookBot.Application.DTOs;
using CookBot.Application.Recipes;
using CookBot.Application.Services;
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
    private readonly RecipePhotoUrlValidator _photoValidator;

    public AnthropicAiService(
        IOptions<CookBotSettings> settings,
        RecipeValidator validator,
        RecipePhotoUrlValidator photoValidator)
    {
        _settings = settings.Value;
        _validator = validator;
        // Phase 9 / Plan 09-05 / PHOTO-07 + PITFALL H5 — scrub AI-emitted PhotoUrl on the
        // structured-output return path so a model-emitted javascript:/data:/file: scheme
        // never reaches the editor or the DB.
        _photoValidator = photoValidator;
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

        // Phase 9 / Plan 09-05 / PROD-12 — token usage observed off the SSE stream.
        // message_start.message.usage.input_tokens fires once at the top of the stream and
        // is final at that point. message_delta.usage.output_tokens fires repeatedly and is
        // CUMULATIVE per the Anthropic streaming spec — we capture the LAST value (overwrite,
        // never +=) so a naive sum cannot produce n*(n+1)/2 over-counting.
        var inputTokens = 0;
        var outputTokens = 0;

        HttpClient http;
        try
        {
            http = CreateHttpClient(apiKey);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new StructuredResult<T>(
                Ok: false, Value: null, RawResponse: null, Validation: null,
                SanitizedError: SecretRedactor.Redact($"AI client init failure: {ex.Message}", resolvedKey),
                InputTokens: inputTokens, OutputTokens: outputTokens);
        }

        try
        {
            // D-10 superseded (Phase 10 UAT Test 4 retest 5): output_config.format with
            // type=json_schema sends the schema to Anthropic's structured-outputs grammar
            // compiler, which times out on the polymorphic StepNode + nested-nullable
            // (temperature/timers) shape — confirmed via "Grammar compilation timed out" 400.
            // Switching to the tool-use API: same cached JSON schema, but routed through
            // Anthropic's tool grammar compiler with strict=true. tool_choice forces the
            // model to emit exactly one emit_recipe call, so the response is always a
            // tool_use content block whose input is the structured RecipeDocument.
            const string emitRecipeToolName = "emit_recipe";
            var tools = new object[]
            {
                new
                {
                    name = emitRecipeToolName,
                    description = "Emit the structured recipe document conforming to the canonical schema. Always call this tool; never reply in prose.",
                    input_schema = schema,
                }
            };
            var toolChoice = new
            {
                type = "tool",
                name = emitRecipeToolName,
            };

            var payload = new Dictionary<string, object>
            {
                ["model"]       = modelId ?? DefaultModelId,
                ["system"]      = systemPrompt,
                ["messages"]    = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                ["max_tokens"]  = maxTokens,
                ["stream"]      = true,           // SSE under the hood (D-01)
                ["tools"]       = tools,
                ["tool_choice"] = toolChoice,
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
                    SanitizedError: SecretRedactor.Redact($"AI transport failure: {ex.Message}", resolvedKey),
                    InputTokens: inputTokens, OutputTokens: outputTokens);
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
                            resolvedKey),
                        InputTokens: inputTokens, OutputTokens: outputTokens);
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

                        // P-4 superseded (tool-use migration): structured-output JSON arrives in
                        // content_block_delta.delta.partial_json (input_json_delta event family)
                        // when the response is a tool_use block. The legacy text path is preserved
                        // because non-tool free-text fallbacks could still hit this method, but the
                        // tool_choice forces an emit_recipe call so partial_json is the live path.
                        if (type == "content_block_delta")
                        {
                            var delta = evt.RootElement.GetProperty("delta");
                            if (delta.TryGetProperty("partial_json", out var partial))
                                accumulated.Append(partial.GetString());
                            else if (delta.TryGetProperty("text", out var text))
                                accumulated.Append(text.GetString());
                        }
                        // PROD-12 — input_tokens fires once at the top of the stream in
                        // message_start.message.usage. Defensive TryGetProperty chain:
                        // missing fields silently keep inputTokens at 0.
                        else if (type == "message_start")
                        {
                            if (evt.RootElement.TryGetProperty("message", out var msg) &&
                                msg.TryGetProperty("usage", out var usage) &&
                                usage.TryGetProperty("input_tokens", out var inTok) &&
                                inTok.ValueKind == JsonValueKind.Number)
                            {
                                inputTokens = inTok.GetInt32();
                            }
                        }
                        // P-5: capture stop_reason from message_delta — short-circuit on refusal.
                        // PROD-12 (sibling capture): message_delta.delta.usage.output_tokens is
                        // CUMULATIVE per the Anthropic streaming spec. Overwrite, NEVER `+=`.
                        else if (type == "message_delta")
                        {
                            if (evt.RootElement.TryGetProperty("delta", out var d))
                            {
                                if (d.TryGetProperty("stop_reason", out var sr) &&
                                    sr.ValueKind == JsonValueKind.String)
                                {
                                    stopReason = sr.GetString();
                                }
                                if (d.TryGetProperty("usage", out var u) &&
                                    u.TryGetProperty("output_tokens", out var outTok) &&
                                    outTok.ValueKind == JsonValueKind.Number)
                                {
                                    // CRITICAL: cumulative — last value wins. PITFALL.
                                    outputTokens = outTok.GetInt32();
                                }
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
                        SanitizedError: "The AI declined to produce a recipe for this request.",
                        InputTokens: inputTokens, OutputTokens: outputTokens);
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
                            SanitizedError: "Model returned empty content.",
                            InputTokens: inputTokens, OutputTokens: outputTokens);
                    }
                    rawNode = JsonNode.Parse(json);
                    doc = JsonSerializer.Deserialize<T>(json, JsonOptions);
                }
                catch (JsonException ex)
                {
                    return new StructuredResult<T>(
                        Ok: false, Value: null, RawResponse: rawNode, Validation: null,
                        SanitizedError: SecretRedactor.Redact(
                            $"Deserialization failed: {ex.Message}", resolvedKey),
                        InputTokens: inputTokens, OutputTokens: outputTokens);
                }

                if (doc is null)
                {
                    return new StructuredResult<T>(
                        Ok: false, Value: null, RawResponse: rawNode, Validation: null,
                        SanitizedError: "Model returned empty content.",
                        InputTokens: inputTokens, OutputTokens: outputTokens);
                }

                // Phase 9 / Plan 09-05 / PHOTO-07 + PITFALL H5 — scrub AI-emitted PhotoUrl
                // through the scheme allowlist. A model may emit a javascript:/data:/file:
                // URL despite the system-prompt; null it out before the doc reaches the
                // editor or DB. RecipeDocument is an immutable record, so we replace via
                // `with`. Mutates the local `doc`; the validator never throws.
                if (doc is RecipeDocument photoCheckDoc && !string.IsNullOrWhiteSpace(photoCheckDoc.PhotoUrl))
                {
                    if (_photoValidator.TryValidate(photoCheckDoc.PhotoUrl, out var normalized, out _))
                    {
                        // On accept-with-value, normalize. On accept-empty (whitespace only,
                        // already covered by the IsNullOrWhiteSpace guard above) normalized is null.
                        if (!string.Equals(normalized, photoCheckDoc.PhotoUrl, StringComparison.Ordinal))
                        {
                            doc = (T)(object)(photoCheckDoc with { PhotoUrl = normalized });
                        }
                    }
                    else
                    {
                        // Reject lane — null out the emitted PhotoUrl. The rest of the doc
                        // is still usable; the editor will render the StripedPlaceholder fallback.
                        doc = (T)(object)(photoCheckDoc with { PhotoUrl = null });
                    }
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
                        SanitizedError: null,
                        InputTokens: inputTokens,
                        OutputTokens: outputTokens);
                }

                // For non-recipe Ts: deserialization success implies Ok.
                return new StructuredResult<T>(
                    Ok: true, Value: doc, RawResponse: rawNode, Validation: null, SanitizedError: null,
                    InputTokens: inputTokens, OutputTokens: outputTokens);
            }
        }
        finally
        {
            http.Dispose();
        }
    }
}
