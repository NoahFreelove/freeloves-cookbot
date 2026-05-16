using System.Text.Json.Nodes;
using CookBot.Application.Recipes;

namespace CookBot.Application.AI;

/// <summary>
/// Envelope returned by <see cref="IStructuredAiService.SendStructuredAsync{T}"/>.
/// Per D-02, never throws — all failure paths return a populated result.
/// </summary>
/// <param name="Ok">True iff the model returned a valid, schema-conformant, semantically-validated value.</param>
/// <param name="Value">Populated when Ok=true; null otherwise.</param>
/// <param name="RawResponse">Populated when validation failed (for repair-loop or "edit and save anyway" path); null on transport / auth errors.</param>
/// <param name="Validation">Populated when the structured response deserialized but failed semantic validation; null on transport / auth / deserialization errors.</param>
/// <param name="SanitizedError">Populated on transport, auth, deserialization, or refusal errors. Already passed through <see cref="CookBot.Infrastructure.AI.SecretRedactor"/>.</param>
/// <param name="InputTokens">
/// Phase 9 / Plan 09-05 / PROD-12 — input tokens consumed by this call, captured from
/// <c>message_start.message.usage.input_tokens</c> on the SSE stream. Defaults to 0 so
/// existing call sites that pre-date Phase 9 (and test fakes that don't care about
/// telemetry) compile unchanged.
/// </param>
/// <param name="OutputTokens">
/// Phase 9 / Plan 09-05 / PROD-12 — output tokens emitted by this call, captured from the
/// LAST <c>message_delta.usage.output_tokens</c> on the SSE stream. NOTE: the streaming
/// protocol delivers this value cumulatively; the parser MUST overwrite (never sum).
/// </param>
public sealed record StructuredResult<T>(
    bool Ok,
    T? Value,
    JsonNode? RawResponse,
    ValidationResult? Validation,
    string? SanitizedError,
    int InputTokens = 0,
    int OutputTokens = 0)
    where T : class;
