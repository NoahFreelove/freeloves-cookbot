using System.Text.Json.Nodes;
using CookBot.Application.AI;
using CookBot.Application.Recipes;
using CookBot.Domain.Interfaces;
using CookBot.Domain.Recipes;
using Microsoft.Extensions.Logging.Abstractions;

namespace CookBot.Tests.AI;

/// <summary>
/// Unit tests for the recipe-generation orchestrator. Covers the four state-machine
/// branches called out in the plan: success-on-first-attempt, repair-converges-on-attempt-1,
/// budget-exhaustion (3 calls total), refusal short-circuit, transport-failure short-circuit.
/// Also asserts the minimal repair prompt shape (D-06).
/// </summary>
public class AiRecipeGeneratorTests
{
    // ---------- Fake IStructuredAiService that records every call ----------

    private sealed class RecordingFakeStructuredAi : IStructuredAiService
    {
        public List<(List<AiMessage> Messages, JsonNode Schema)> Calls { get; } = new();
        public Queue<Func<StructuredResult<RecipeDocument>>> Responses { get; } = new();

        public Task<StructuredResult<T>> SendStructuredAsync<T>(
            string systemPrompt, List<AiMessage> messages, JsonNode schema,
            string? apiKey = null, string? modelId = null, int maxTokens = 4096,
            CancellationToken ct = default)
            where T : class
        {
            // Snapshot the messages list so later mutations by the orchestrator
            // (which reuses the variable) don't affect what we recorded.
            Calls.Add((new List<AiMessage>(messages.Select(m => new AiMessage { Role = m.Role, Content = m.Content })), schema));
            var fn = Responses.Dequeue();
            // Tests only exercise T=RecipeDocument; cast through object is safe at runtime.
            var result = fn();
            return Task.FromResult((StructuredResult<T>)(object)result);
        }
    }

    // ---------- Fixtures ----------

    private static RecipeDocument ValidRecipe() => new()
    {
        Version = 2,
        Name = "Test Cake",
        Servings = 4,
        Ingredients = new List<IngredientEntry>
        {
            new() { Id = 1, Name = "flour", Amount = 1.0, Unit = "cup" }
        },
        Steps = new List<StepNode>
        {
            new ContentStep { Text = "Mix.", Timers = new List<TimerEntry>() }
        }
    };

    private static StructuredResult<RecipeDocument> Success() =>
        new(Ok: true, Value: ValidRecipe(), RawResponse: null, Validation: null, SanitizedError: null);

    private static StructuredResult<RecipeDocument> ValidationFailure() =>
        new(Ok: false, Value: null,
            RawResponse: JsonNode.Parse("""{"version":2,"name":""}""")!,
            Validation: new ValidationResult(
                new[] { new ValidationError("/name", "REQUIRED", "Recipe name is required.") },
                Array.Empty<ValidationWarning>()),
            SanitizedError: null);

    private static StructuredResult<RecipeDocument> RefusalFailure() =>
        new(Ok: false, Value: null, RawResponse: null, Validation: null,
            SanitizedError: "The AI declined to produce a recipe for this request.");

    private static StructuredResult<RecipeDocument> TransportFailure() =>
        new(Ok: false, Value: null, RawResponse: null, Validation: null,
            SanitizedError: "AI transport failure: simulated network error.");

    // ---------- System under test ----------

    private static AiRecipeGenerator MakeOrchestrator(RecordingFakeStructuredAi fake)
    {
        return new AiRecipeGenerator(
            fake,
            new RecipeJsonSchemaProvider(),
            new RecipeValidator(),
            new RecipeSchemaDocumentationProvider(),
            NullLogger<AiRecipeGenerator>.Instance);
    }

    // ---------- Tests ----------

    [Fact]
    public async Task GenerateAsync_SuccessFirstAttempt_ReturnsImmediately()
    {
        var fake = new RecordingFakeStructuredAi();
        fake.Responses.Enqueue(Success);
        var sut = MakeOrchestrator(fake);

        var result = await sut.GenerateAsync("make a cake");

        Assert.True(result.Ok);
        Assert.NotNull(result.Value);
        Assert.Single(fake.Calls);
    }

    [Fact]
    public async Task GenerateAsync_RepairConvergesOnAttempt1_Returns2CallsTotal()
    {
        var fake = new RecordingFakeStructuredAi();
        fake.Responses.Enqueue(ValidationFailure);
        fake.Responses.Enqueue(Success);
        var sut = MakeOrchestrator(fake);

        var result = await sut.GenerateAsync("make a cake");

        Assert.True(result.Ok);
        Assert.Equal(2, fake.Calls.Count);

        // The second call's messages: minimal repair shape per D-06.
        var repairCall = fake.Calls[1];
        Assert.Equal(2, repairCall.Messages.Count);
        Assert.All(repairCall.Messages, m => Assert.Equal("user", m.Role));
        Assert.Contains("did not match the required schema", repairCall.Messages[1].Content);
        Assert.Contains("/name", repairCall.Messages[1].Content);
    }

    [Fact]
    public async Task GenerateAsync_BudgetExhausted_Returns3CallsAndOkFalse()
    {
        var fake = new RecordingFakeStructuredAi();
        fake.Responses.Enqueue(ValidationFailure);
        fake.Responses.Enqueue(ValidationFailure);
        fake.Responses.Enqueue(ValidationFailure);
        var sut = MakeOrchestrator(fake);

        var result = await sut.GenerateAsync("make a cake");

        Assert.False(result.Ok);
        Assert.Equal(3, fake.Calls.Count);   // 1 initial + 2 retries (D-05 hard cap)
        Assert.NotNull(result.RawResponse);
        Assert.NotNull(result.Validation);
        Assert.False(result.Validation!.IsValid);
    }

    [Fact]
    public async Task GenerateAsync_RefusalShortCircuit_Returns1CallOnly()
    {
        var fake = new RecordingFakeStructuredAi();
        fake.Responses.Enqueue(RefusalFailure);
        var sut = MakeOrchestrator(fake);

        var result = await sut.GenerateAsync("disallowed prompt");

        Assert.False(result.Ok);
        Assert.Single(fake.Calls);   // refusals don't converge — no repair attempted
        Assert.NotNull(result.SanitizedError);
        Assert.Contains("declined", result.SanitizedError);
    }

    [Fact]
    public async Task GenerateAsync_TransportFailureShortCircuit_Returns1CallOnly()
    {
        var fake = new RecordingFakeStructuredAi();
        fake.Responses.Enqueue(TransportFailure);
        var sut = MakeOrchestrator(fake);

        var result = await sut.GenerateAsync("make a cake");

        Assert.False(result.Ok);
        Assert.Single(fake.Calls);   // transport errors don't converge — no repair attempted
        Assert.NotNull(result.SanitizedError);
        Assert.Contains("transport failure", result.SanitizedError);
    }
}
