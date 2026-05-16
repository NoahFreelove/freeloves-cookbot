using System.Net;
using System.Text;
using System.Text.Json;
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

public class AnthropicStructuredOutputTests
{
    // ---------- Helpers ----------

    private static HttpResponseMessage SseResponse(params string[] eventJsonPayloads)
    {
        var body = new StringBuilder();
        foreach (var payload in eventJsonPayloads)
        {
            if (string.IsNullOrEmpty(payload)) continue;
            body.Append("data: ").Append(payload).Append("\n\n");
        }
        body.Append("data: [DONE]\n\n");

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body.ToString(), Encoding.UTF8, "text/event-stream")
        };
    }

    private static string ContentBlockDeltaEvent(string text)
    {
        // Use JsonSerializer to escape the text correctly.
        var escaped = JsonSerializer.Serialize(text);
        return "{\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":" + escaped + "}}";
    }

    private static string MessageDeltaWithStopReasonEvent(string stopReason)
    {
        return "{\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"" + stopReason + "\"}}";
    }

    private static IOptions<CookBotSettings> MakeSettings() =>
        Options.Create(new CookBotSettings
        {
            AnthropicApiKey = "test-key-not-used",
            AiFeaturesEnabled = true,
        });

    private sealed class TestableAnthropicAiService : AnthropicAiService
    {
        private readonly HttpMessageHandler _handler;

        public TestableAnthropicAiService(
            IOptions<CookBotSettings> settings,
            RecipeValidator validator,
            RecipePhotoUrlValidator photoValidator,
            HttpMessageHandler handler)
            : base(settings, validator, photoValidator)
        {
            _handler = handler;
        }

        protected override HttpClient CreateHttpClient(string? apiKey)
        {
            // disposeHandler:false — handler is owned by the test fixture
            return new HttpClient(_handler, disposeHandler: false);
        }
    }

    private static (TestableAnthropicAiService svc, JsonNode schema) MakeService(
        FakeHttpMessageHandler handler)
    {
        var validator = new RecipeValidator();
        var photoValidator = new RecipePhotoUrlValidator();
        var schemaProvider = new RecipeJsonSchemaProvider();
        var svc = new TestableAnthropicAiService(MakeSettings(), validator, photoValidator, handler);
        return (svc, schemaProvider.GetSchema());
    }

    private static List<AiMessage> SinglePrompt(string text) =>
        new() { new AiMessage { Role = "user", Content = text } };

    // ---------- Tests ----------

    [Fact]
    public async Task SendStructuredAsync_ValidRecipe_DeserializesAndValidates()
    {
        var validRecipeJson = """
            {"version":2,"name":"Test Cake","servings":4,
             "ingredients":[{"id":1,"name":"flour","amount":1.0,"unit":"cup"}],
             "steps":[{"kind":"content","text":"Mix flour with water.","timers":[]}]}
            """.ReplaceLineEndings("");

        using var handler = new FakeHttpMessageHandler(_ =>
            SseResponse(ContentBlockDeltaEvent(validRecipeJson)));

        var (svc, schema) = MakeService(handler);
        var result = await svc.SendStructuredAsync<RecipeDocument>(
            "system", SinglePrompt("make a cake"), schema, apiKey: "test-key");

        Assert.True(result.Ok, $"Expected Ok=true; SanitizedError={result.SanitizedError}; Validation={result.Validation}");
        Assert.NotNull(result.Value);
        Assert.Equal("Test Cake", result.Value!.Name);
        Assert.Equal(2, result.Value.Version);
        Assert.Null(result.SanitizedError);
    }

    [Fact]
    public async Task SendStructuredAsync_InvalidRecipe_ReturnsValidationFailure()
    {
        // Empty name fails RecipeValidator's REQUIRED check
        var invalidRecipeJson = """{"version":2,"name":"","servings":1,"ingredients":[],"steps":[]}""";

        using var handler = new FakeHttpMessageHandler(_ =>
            SseResponse(ContentBlockDeltaEvent(invalidRecipeJson)));

        var (svc, schema) = MakeService(handler);
        var result = await svc.SendStructuredAsync<RecipeDocument>(
            "system", SinglePrompt("make a cake"), schema);

        Assert.False(result.Ok);
        Assert.Null(result.Value);
        Assert.NotNull(result.RawResponse);
        Assert.NotNull(result.Validation);
        Assert.False(result.Validation!.IsValid);
        Assert.Null(result.SanitizedError);  // validation failure is not an error message
    }

    [Fact]
    public async Task SendStructuredAsync_Http401_ReturnsSanitizedErrorWithoutLeakingKey()
    {
        const string leakedKey = "sk-ant-leaked-secret-abc123";
        var errorBody = "{\"error\":{\"type\":\"authentication_error\",\"message\":\"x-api-key " + leakedKey + " is invalid\"}}";
        using var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(errorBody, Encoding.UTF8, "application/json")
            });

        var (svc, schema) = MakeService(handler);
        var result = await svc.SendStructuredAsync<RecipeDocument>(
            "system", SinglePrompt("make a cake"), schema);

        Assert.False(result.Ok);
        Assert.Null(result.Value);
        Assert.NotNull(result.SanitizedError);
        Assert.DoesNotContain("sk-ant-", result.SanitizedError);
        Assert.Contains("401", result.SanitizedError);
    }

    [Fact]
    public async Task SendStructuredAsync_RefusalStopReason_ShortCircuits()
    {
        using var handler = new FakeHttpMessageHandler(_ =>
            SseResponse(MessageDeltaWithStopReasonEvent("refusal")));

        var (svc, schema) = MakeService(handler);
        var result = await svc.SendStructuredAsync<RecipeDocument>(
            "system", SinglePrompt("disallowed prompt"), schema);

        Assert.False(result.Ok);
        Assert.Null(result.Value);
        Assert.NotNull(result.SanitizedError);
        Assert.Contains("declined", result.SanitizedError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendStructuredAsync_TruncatedJson_ReturnsSanitizedDeserializationError()
    {
        const string truncated = """{"version":2,"name":"""; // truncated mid-string
        using var handler = new FakeHttpMessageHandler(_ =>
            SseResponse(ContentBlockDeltaEvent(truncated)));

        var (svc, schema) = MakeService(handler);
        var result = await svc.SendStructuredAsync<RecipeDocument>(
            "system", SinglePrompt("make a cake"), schema);

        Assert.False(result.Ok);
        Assert.Null(result.Value);
        Assert.NotNull(result.SanitizedError);
        Assert.DoesNotContain("sk-ant-", result.SanitizedError);
    }

    [Fact]
    public async Task SendStructuredAsync_PreCancelledToken_ThrowsOperationCanceled()
    {
        using var handler = new FakeHttpMessageHandler(_ =>
            SseResponse(ContentBlockDeltaEvent("""{"version":2,"name":"x","servings":1,"ingredients":[],"steps":[]}""")));
        var (svc, schema) = MakeService(handler);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            svc.SendStructuredAsync<RecipeDocument>(
                "system", SinglePrompt("test"), schema, ct: cts.Token));
    }
}
