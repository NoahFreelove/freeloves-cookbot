namespace CookBot.Domain.Entities;

/// <summary>
/// Log row written when an AI recipe-generation attempt completes (Phase 9 / Plan 09-05 /
/// PROD-14). Powers the Phase 10 per-user "AI usage" widget (PROD-17). One row per attempt:
/// the retry-loop in <c>AiRecipeGenerator.GenerateAsync</c> tags repair calls with
/// <see cref="IsRetryAttempt"/> = true so aggregation queries can sum cost across primary
/// attempts only (PITFALL H9 — never double-count repair calls into the success-cost total).
/// </summary>
/// <remarks>
/// Two foreign keys back to <c>User</c>: <see cref="UserId"/> is the user who triggered
/// the call; <see cref="KeyOwnerId"/> is the user whose API key paid for the call —
/// distinct when the trigger is a share recipient (PITFALL C2 owner-share semantics).
/// The composite index <c>IX_AiUsageLogs_KeyOwnerId_Timestamp</c> serves the Phase 10
/// widget's "spending by owner over the last 30 days" hot path.
/// </remarks>
public class AiUsageLog
{
    public int Id { get; set; }

    /// <summary>The user who triggered the AI call.</summary>
    public int UserId { get; set; }

    /// <summary>
    /// The user whose API key paid for the call. Equal to <see cref="UserId"/> when the
    /// caller used their own key; differs when a recipient consumed a shared key.
    /// </summary>
    public int KeyOwnerId { get; set; }

    /// <summary>
    /// The Anthropic model id used for the call (one of <c>CuratedModels</c> ids).
    /// Stored verbatim so historical rows survive future model-list changes.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }

    /// <summary>
    /// Decimal-typed currency math (column <c>decimal(18,6)</c> — Haiku sub-cent rows
    /// must not silently round to 0). Computed via
    /// <c>(InputTokens * Input$/1M + OutputTokens * Output$/1M) / 1_000_000m</c>.
    /// </summary>
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>
    /// PITFALL H9 — true when this row represents a repair attempt inside
    /// <c>AiRecipeGenerator</c>'s loop. Aggregation queries WHERE
    /// IsRetryAttempt = false return success-path cost only.
    /// </summary>
    public bool IsRetryAttempt { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public User KeyOwner { get; set; } = null!;
}
