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
public sealed record StructuredResult<T>(
    bool Ok,
    T? Value,
    JsonNode? RawResponse,
    ValidationResult? Validation,
    string? SanitizedError)
    where T : class;
