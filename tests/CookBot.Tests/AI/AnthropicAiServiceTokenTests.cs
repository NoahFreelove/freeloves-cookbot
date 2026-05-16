using System.Text;
using System.Text.Json.Nodes;
using CookBot.Application.AI;
using CookBot.Application.DTOs;
using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Interfaces;
using CookBot.Domain.Recipes;
using CookBot.Infrastructure.AI;
using Microsoft.Extensions.Options;

namespace CookBot.Tests.AI;

/// <summary>
/// Phase 9 / Plan 09-05 / PROD-12 + PITFALL: cumulative-output-tokens semantic.
///
/// Validates the SSE parse loop in <see cref="AnthropicAiService.SendStructuredAsync{T}"/>:
///   1. <c>message_start.message.usage.input_tokens</c> is captured into
///      <see cref="StructuredResult{T}.InputTokens"/>.
///   2. <c>message_delta.usage.output_tokens</c> is CUMULATIVE per the Anthropic streaming
///      spec — the parser must capture the LAST observed value (overwrite), never sum.
///      A naive <c>+=</c> implementation would yield n*(n+1)/2 over-counting.
///
/// The HttpMessageHandler shim returns a hand-crafted SSE byte stream so the parse loop is
/// exercised end-to-end without touching the network. We deliberately emit two
/// message_delta events with usage.output_tokens=100 then 250; a correct parser yields
/// final OutputTokens=250 (NOT 350).
/// </summary>
public class AnthropicAiServiceTokenTests
{
    /// <summary>
    /// Subclass that overrides CreateHttpClient to return a client backed by a canned
    /// SSE message stream. We bypass the real api-key requirement by always supplying
    /// the shim handler regardless of the key value.
    /// </summary>
    private sealed class StubAnthropicAiService : AnthropicAiService
    {
        private readonly Func<HttpResponseMessage> _responseFactory;

        public StubAnthropicAiService(
            IOptions<CookBotSettings> settings,
            RecipeValidator validator,
            RecipePhotoUrlValidator photoValidator,
            Func<HttpResponseMessage> responseFactory)
            : base(settings, validator, photoValidator)
        {
            _responseFactory = responseFactory;
        }

        protected override HttpClient CreateHttpClient(string? apiKey)
        {
            var handler = new StubHandler(_responseFactory);
            return new HttpClient(handler);
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpResponseMessage> _factory;
            public StubHandler(Func<HttpResponseMessage> factory) => _factory = factory;
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_factory());
        }
    }

    private static StubAnthropicAiService MakeService(string sseBody)
    {
        var settings = Options.Create(new CookBotSettings { AnthropicApiKey = "stub-key" });
        return new StubAnthropicAiService(
            settings,
            new RecipeValidator(),
            new RecipePhotoUrlValidator(),
            () => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(sseBody, Encoding.UTF8, "text/event-stream"),
            });
    }

    private static string ValidRecipeJson() =>
        """{"version":2,"name":"Test","servings":4,"ingredients":[{"id":1,"name":"flour","amount":1,"unit":"cup"}],"steps":[{"kind":"content","text":"Mix.","timers":[]}]}""";

    [Fact]
    public async Task Parser_CapturesInputTokensFromMessageStart()
    {
        // SSE protocol — each event is "data: {json}\n\n".
        var json = ValidRecipeJson();
        var sse = string.Join("\n",
            $"data: {{\"type\":\"message_start\",\"message\":{{\"usage\":{{\"input_tokens\":123,\"output_tokens\":0}}}}}}",
            "",
            $"data: {{\"type\":\"content_block_delta\",\"delta\":{{\"text\":{System.Text.Json.JsonSerializer.Serialize(json)}}}}}",
            "",
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\",\"usage\":{\"output_tokens\":42}}}",
            "",
            "data: [DONE]",
            "");

        var svc = MakeService(sse);
        var schema = JsonNode.Parse("{}")!;
        var result = await svc.SendStructuredAsync<RecipeDocument>(
            "system",
            new List<AiMessage> { new() { Role = "user", Content = "make a cake" } },
            schema,
            apiKey: "stub");

        Assert.True(result.Ok, $"expected Ok; sanitized: {result.SanitizedError}");
        Assert.Equal(123, result.InputTokens);
        Assert.Equal(42, result.OutputTokens);
    }

    [Fact]
    public async Task Parser_CapturesCumulativeOutputTokens_OverwritesNotSums()
    {
        var json = ValidRecipeJson();
        var sse = string.Join("\n",
            "data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":50,\"output_tokens\":0}}}",
            "",
            $"data: {{\"type\":\"content_block_delta\",\"delta\":{{\"text\":{System.Text.Json.JsonSerializer.Serialize(json)}}}}}",
            "",
            // First message_delta — cumulative output_tokens snapshot = 100.
            "data: {\"type\":\"message_delta\",\"delta\":{\"usage\":{\"output_tokens\":100}}}",
            "",
            // Second message_delta — cumulative output_tokens snapshot = 250.
            // Naive `outputTokens += 250` would yield 350; correct overwrite yields 250.
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\",\"usage\":{\"output_tokens\":250}}}",
            "",
            "data: [DONE]",
            "");

        var svc = MakeService(sse);
        var schema = JsonNode.Parse("{}")!;
        var result = await svc.SendStructuredAsync<RecipeDocument>(
            "system",
            new List<AiMessage> { new() { Role = "user", Content = "make a cake" } },
            schema,
            apiKey: "stub");

        Assert.True(result.Ok, $"expected Ok; sanitized: {result.SanitizedError}");
        Assert.Equal(50, result.InputTokens);
        // CRITICAL: cumulative semantic. Last observed wins; never sum.
        Assert.Equal(250, result.OutputTokens);
    }
}
