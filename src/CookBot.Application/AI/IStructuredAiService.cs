using System.Text.Json.Nodes;
using CookBot.Domain.Interfaces;

namespace CookBot.Application.AI;

/// <summary>
/// AI-01 surface. The structured-output overload that wires Anthropic's
/// <c>output_config.format</c> with <c>strict: true</c>. Lives in Application
/// (not Domain) because <see cref="StructuredResult{T}"/> references
/// <see cref="System.Text.Json.Nodes.JsonNode"/> and
/// <see cref="CookBot.Application.Recipes.ValidationResult"/>, neither of
/// which Domain may reference. Implemented by
/// <c>CookBot.Infrastructure.AI.AnthropicAiService</c> alongside its existing
/// <see cref="IAiService"/> implementation.
/// </summary>
public interface IStructuredAiService
{
    Task<StructuredResult<T>> SendStructuredAsync<T>(
        string systemPrompt,
        List<AiMessage> messages,
        JsonNode schema,
        string? apiKey = null,
        string? modelId = null,
        int maxTokens = 4096,
        CancellationToken ct = default)
        where T : class;
}
