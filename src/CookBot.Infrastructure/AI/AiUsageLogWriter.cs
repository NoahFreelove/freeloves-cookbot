using CookBot.Application.AI;
using CookBot.Domain.Entities;
using CookBot.Infrastructure.Data;

namespace CookBot.Infrastructure.AI;

/// <summary>
/// Phase 9 / Plan 09-05 / PROD-14 — EF implementation of <see cref="IAiUsageLogWriter"/>.
/// Append-only: one Add + SaveChangesAsync per call. The orchestrator
/// (<c>AiRecipeGenerator</c>) calls this once per attempt at the END of GenerateAsync
/// so the loop body never holds the DbContext open (PITFALL H9 prevention by structure).
/// </summary>
public sealed class AiUsageLogWriter : IAiUsageLogWriter
{
    private readonly CookBotDbContext _db;

    public AiUsageLogWriter(CookBotDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(
        int userId,
        int keyOwnerId,
        string modelName,
        int inputTokens,
        int outputTokens,
        decimal estimatedCostUsd,
        bool isRetryAttempt,
        CancellationToken ct = default)
    {
        _db.AiUsageLogs.Add(new AiUsageLog
        {
            UserId = userId,
            KeyOwnerId = keyOwnerId,
            ModelName = modelName,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            EstimatedCostUsd = estimatedCostUsd,
            IsRetryAttempt = isRetryAttempt,
            Timestamp = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }
}
