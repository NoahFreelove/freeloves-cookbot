namespace CookBot.Application.AI;

/// <summary>
/// Phase 9 / Plan 09-05 / PROD-14 — write-only sink for AI token-cost telemetry.
/// Implemented in Infrastructure (<c>AiUsageLogWriter</c>) which depends on
/// <c>CookBotDbContext</c>. AiRecipeGenerator (Application layer) only sees this
/// interface so the Application project never references Infrastructure directly.
/// </summary>
/// <remarks>
/// Each call writes one row per attempt — the orchestrator accumulates per-attempt
/// tuples through its retry loop and flushes them all at the END of GenerateAsync
/// (PITFALL H9 prevention by structure: the write site appears exactly once in the
/// caller's code, never inside the loop body).
/// </remarks>
public interface IAiUsageLogWriter
{
    /// <summary>
    /// Appends a single telemetry row. Implementations are expected to be additive
    /// (no upserts, no read-modify-write) and to swallow no exceptions — callers
    /// decide whether to surface a write failure or log-and-continue.
    /// </summary>
    /// <param name="userId">User who triggered the call.</param>
    /// <param name="keyOwnerId">User whose API key paid for the call (may equal userId).</param>
    /// <param name="modelName">The model id the call used.</param>
    /// <param name="inputTokens">From <c>StructuredResult.InputTokens</c>.</param>
    /// <param name="outputTokens">From <c>StructuredResult.OutputTokens</c>.</param>
    /// <param name="estimatedCostUsd">Pre-computed via pricing-config lookup at the call site.</param>
    /// <param name="isRetryAttempt">True for repair attempts (PITFALL H9).</param>
    Task WriteAsync(
        int userId,
        int keyOwnerId,
        string modelName,
        int inputTokens,
        int outputTokens,
        decimal estimatedCostUsd,
        bool isRetryAttempt,
        CancellationToken ct = default);
}
