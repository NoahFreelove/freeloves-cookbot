using System.Text.Json.Nodes;
using CookBot.Application.AI;
using CookBot.Application.DTOs;
using CookBot.Application.Recipes;
using CookBot.Domain.Entities;
using CookBot.Domain.Enums;
using CookBot.Domain.Interfaces;
using CookBot.Domain.Recipes;
using CookBot.Infrastructure.AI;
using CookBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CookBot.Tests.AI;

/// <summary>
/// Phase 9 / Plan 09-05 / PROD-15 + PITFALL H9.
///
/// Validates AiRecipeGenerator's per-attempt telemetry write contract:
///   1. Success on first attempt → one row, IsRetryAttempt=false.
///   2. Repair converges on attempt 2 → two rows; the second is IsRetryAttempt=true.
///   3. Budget exhaustion (3 calls, all fail validation) → three rows; only the first
///      is IsRetryAttempt=false.
///   4. Aggregation excluding retries (the Phase 10 widget's expected query)
///      returns only the success-path cost — no double-counting from repair calls.
///
/// Uses RecordingFakeStructuredAi (same shape as AiRecipeGeneratorTests) with
/// InputTokens/OutputTokens stamped on each StructuredResult so the cost math is
/// deterministic.
/// </summary>
public class TokenTelemetryTests : IDisposable
{
    private const string SonnetId = "claude-sonnet-4-6";
    private const int OwnerUserId = 2;
    private const int CallerUserId = 1;

    private readonly CookBotDbContext _db;
    private readonly string _dbPath;

    public TokenTelemetryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cookbot-telemetry-{Path.GetRandomFileName()}.db");
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite($"DataSource={_dbPath}")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.Migrate();
        SeedTwoUsers();
    }

    private void SeedTwoUsers()
    {
        var caller = new User
        {
            Id = CallerUserId,
            DisplayName = "Caller",
            IsCookBotAdmin = true,
            Profile = new UserProfile
            {
                ExperienceLevel = ExperienceLevel.Intermediate,
                UnitSystem = UnitSystem.Canadian,
            },
        };
        var owner = new User
        {
            Id = OwnerUserId,
            DisplayName = "KeyOwner",
            Profile = new UserProfile
            {
                ExperienceLevel = ExperienceLevel.Intermediate,
                UnitSystem = UnitSystem.Canadian,
            },
        };
        _db.Users.AddRange(caller, owner);
        _db.SaveChanges();
    }

    // ---------- Fakes ----------

    private sealed class RecordingFakeStructuredAi : IStructuredAiService
    {
        public Queue<Func<StructuredResult<RecipeDocument>>> Responses { get; } = new();

        public Task<StructuredResult<T>> SendStructuredAsync<T>(
            string systemPrompt, List<AiMessage> messages, JsonNode schema,
            string? apiKey = null, string? modelId = null, int maxTokens = 4096,
            CancellationToken ct = default)
            where T : class
        {
            var fn = Responses.Dequeue();
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

    private static StructuredResult<RecipeDocument> Success(int input, int output) =>
        new(Ok: true, Value: ValidRecipe(), RawResponse: null, Validation: null,
            SanitizedError: null, InputTokens: input, OutputTokens: output);

    private static StructuredResult<RecipeDocument> ValidationFailure(int input, int output) =>
        new(Ok: false, Value: null,
            RawResponse: JsonNode.Parse("""{"version":2,"name":""}""")!,
            Validation: new ValidationResult(
                new[] { new ValidationError("/name", "REQUIRED", "Recipe name is required.") },
                Array.Empty<ValidationWarning>()),
            SanitizedError: null,
            InputTokens: input,
            OutputTokens: output);

    private static CookBotSettings PricingSettings() => new()
    {
        AiPricing = new Dictionary<string, AiPricingEntry>
        {
            [SonnetId] = new AiPricingEntry
            {
                InputTokensPerMillionUsd = 3.00m,
                OutputTokensPerMillionUsd = 15.00m,
            },
        },
        AiPricingVerifiedDate = new DateOnly(2026, 5, 16),
    };

    private AiRecipeGenerator MakeOrchestrator(RecordingFakeStructuredAi fake)
    {
        var writer = new AiUsageLogWriter(_db);
        return new AiRecipeGenerator(
            fake,
            new RecipeJsonSchemaProvider(),
            new RecipeValidator(),
            new RecipeSchemaDocumentationProvider(),
            writer,
            Options.Create(PricingSettings()),
            NullLogger<AiRecipeGenerator>.Instance);
    }

    // ---------- Tests ----------

    [Fact]
    public async Task Success_OnFirstAttempt_WritesOneRow_IsRetryAttemptFalse()
    {
        var fake = new RecordingFakeStructuredAi();
        fake.Responses.Enqueue(() => Success(100, 200));
        var sut = MakeOrchestrator(fake);

        var result = await sut.GenerateAsync(
            "make a cake", apiKey: null, modelId: SonnetId,
            userId: CallerUserId, keyOwnerId: OwnerUserId);

        Assert.True(result.Ok);

        var rows = await _db.AiUsageLogs.AsNoTracking().ToListAsync();
        var row = Assert.Single(rows);
        Assert.False(row.IsRetryAttempt);
        Assert.Equal(CallerUserId, row.UserId);
        Assert.Equal(OwnerUserId, row.KeyOwnerId);
        Assert.Equal(SonnetId, row.ModelName);
        Assert.Equal(100, row.InputTokens);
        Assert.Equal(200, row.OutputTokens);

        // (100*3 + 200*15) / 1_000_000 = 3300 / 1_000_000 = 0.0033
        Assert.Equal(0.0033m, row.EstimatedCostUsd);
    }

    [Fact]
    public async Task Repair_ConvergesOnAttempt2_WritesTwoRows_SecondHasIsRetryAttemptTrue()
    {
        var fake = new RecordingFakeStructuredAi();
        fake.Responses.Enqueue(() => ValidationFailure(50, 30));
        fake.Responses.Enqueue(() => Success(80, 120));
        var sut = MakeOrchestrator(fake);

        var result = await sut.GenerateAsync(
            "make a cake", apiKey: null, modelId: SonnetId,
            userId: CallerUserId, keyOwnerId: OwnerUserId);

        Assert.True(result.Ok);

        var rows = await _db.AiUsageLogs.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.False(rows[0].IsRetryAttempt);
        Assert.True(rows[1].IsRetryAttempt);
        Assert.Equal(50, rows[0].InputTokens);
        Assert.Equal(30, rows[0].OutputTokens);
        Assert.Equal(80, rows[1].InputTokens);
        Assert.Equal(120, rows[1].OutputTokens);
    }

    [Fact]
    public async Task BudgetExhaustion_3Calls_WritesThreeRows_RetriesTaggedCorrectly()
    {
        var fake = new RecordingFakeStructuredAi();
        fake.Responses.Enqueue(() => ValidationFailure(10, 5));
        fake.Responses.Enqueue(() => ValidationFailure(20, 15));
        fake.Responses.Enqueue(() => ValidationFailure(30, 25));
        var sut = MakeOrchestrator(fake);

        var result = await sut.GenerateAsync(
            "make a cake", apiKey: null, modelId: SonnetId,
            userId: CallerUserId, keyOwnerId: OwnerUserId);

        Assert.False(result.Ok);

        var rows = await _db.AiUsageLogs.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.False(rows[0].IsRetryAttempt);  // initial attempt
        Assert.True(rows[1].IsRetryAttempt);   // repair 1
        Assert.True(rows[2].IsRetryAttempt);   // repair 2
    }

    [Fact]
    public async Task Aggregation_ExcludesRetryAttempts_ReturnsOnlyPrimaryCost()
    {
        var fake = new RecordingFakeStructuredAi();
        fake.Responses.Enqueue(() => ValidationFailure(100, 100));  // primary — IsRetry=false
        fake.Responses.Enqueue(() => ValidationFailure(100, 100));  // repair — IsRetry=true
        fake.Responses.Enqueue(() => ValidationFailure(100, 100));  // repair — IsRetry=true
        var sut = MakeOrchestrator(fake);

        _ = await sut.GenerateAsync(
            "make a cake", apiKey: null, modelId: SonnetId,
            userId: CallerUserId, keyOwnerId: OwnerUserId);

        // Each call: (100*3 + 100*15)/1_000_000 = 1800/1_000_000 = 0.0018
        // Phase 10 widget's expected aggregation excludes retries:
        var primaryOnlyTotal = await _db.AiUsageLogs.AsNoTracking()
            .Where(l => !l.IsRetryAttempt)
            .SumAsync(l => l.EstimatedCostUsd);

        Assert.Equal(0.0018m, primaryOnlyTotal);

        // Full total (sanity): three rows × 0.0018 = 0.0054
        var fullTotal = await _db.AiUsageLogs.AsNoTracking().SumAsync(l => l.EstimatedCostUsd);
        Assert.Equal(0.0054m, fullTotal);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
