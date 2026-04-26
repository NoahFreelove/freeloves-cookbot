using CookBot.Application.Recipes;
using CookBot.Domain.Interfaces;
using CookBot.Domain.Recipes;
using Microsoft.Extensions.Logging;

namespace CookBot.Application.AI;

/// <summary>
/// AI-02 / AI-03 orchestrator. Drives the validate→repair→fail loop on top of
/// <see cref="IStructuredAiService"/>. Hard-capped at 2 repair attempts (3 model
/// calls per generation request) — D-05, not configurable. Refusals and transport
/// errors short-circuit out of the repair loop because they cannot converge under
/// re-prompting.
/// </summary>
public sealed class AiRecipeGenerator : IAiRecipeGenerator
{
    // D-05: Hard cap, not configurable. Up to 3 total model calls per generation request.
    private const int MaxRepairAttempts = 2;

    private readonly IStructuredAiService _ai;
    private readonly RecipeJsonSchemaProvider _schemaProvider;
    private readonly RecipeValidator _validator;
    private readonly IRecipeSchemaDocumentationProvider _docProvider;
    private readonly ILogger<AiRecipeGenerator> _logger;

    public AiRecipeGenerator(
        IStructuredAiService ai,
        RecipeJsonSchemaProvider schemaProvider,
        RecipeValidator validator,
        IRecipeSchemaDocumentationProvider docProvider,
        ILogger<AiRecipeGenerator> logger)
    {
        _ai = ai;
        _schemaProvider = schemaProvider;
        _validator = validator;
        _docProvider = docProvider;
        _logger = logger;
    }

    public async Task<StructuredResult<RecipeDocument>> GenerateAsync(
        string userPrompt,
        string? apiKey = null,
        string? modelId = null,
        CancellationToken ct = default)
    {
        var schema = _schemaProvider.GetSchema();
        var systemPrompt = _docProvider.GetFormatPrompt(); // includes AI-08 directive

        var messages = new List<AiMessage>
        {
            new() { Role = "user", Content = userPrompt }
        };

        _logger.LogInformation("AI recipe generation: initial call (model={Model})", modelId ?? "default");

        var result = await _ai.SendStructuredAsync<RecipeDocument>(
            systemPrompt, messages, schema, apiKey, modelId, ct: ct);

        if (result.Ok)
        {
            _logger.LogInformation("AI recipe generation: succeeded on first attempt.");
            return result;
        }

        // Critical-constraint #4 (refusal/transport short-circuit): if the failure
        // is a SanitizedError WITHOUT validation data, repair cannot help. Refusals,
        // 401s, transport failures all fall here. Return immediately — do not burn
        // the retry budget on a path that cannot converge.
        if (result.Validation is null && result.SanitizedError is not null)
        {
            _logger.LogInformation(
                "AI recipe generation: non-recoverable failure on first attempt; skipping repair loop.");
            return result;
        }

        // --- Repair loop (D-05, D-06) ---
        for (int attempt = 1; attempt <= MaxRepairAttempts; attempt++)
        {
            _logger.LogInformation(
                "AI recipe generation: repair attempt {Attempt}/{Max}.", attempt, MaxRepairAttempts);

            // D-06: Minimal repair prompt. NO prior assistant turn (P-6 incompatible with
            // output_config.format per P-7 anyway), NO full conversation history. Original
            // user prompt + new user-turn with errors only.
            var repairMessages = new List<AiMessage>
            {
                new() { Role = "user", Content = userPrompt },
                new() { Role = "user", Content = BuildRepairPrompt(result) }
            };

            result = await _ai.SendStructuredAsync<RecipeDocument>(
                systemPrompt, repairMessages, schema, apiKey, modelId, ct: ct);

            if (result.Ok)
            {
                _logger.LogInformation("AI recipe generation: repair attempt {Attempt} succeeded.", attempt);
                return result;
            }

            // Same short-circuit on the repair pass: if the new failure is non-recoverable,
            // do not continue the loop.
            if (result.Validation is null && result.SanitizedError is not null)
            {
                _logger.LogInformation(
                    "AI recipe generation: non-recoverable failure during repair attempt {Attempt}; aborting loop.",
                    attempt);
                return result;
            }
        }

        _logger.LogWarning(
            "AI recipe generation: repair budget exhausted after {Max} attempts.",
            MaxRepairAttempts);

        return result; // Ok=false; RawResponse + Validation populated.
    }

    /// <summary>
    /// D-06: Minimal repair prompt — failure mode + format reminder. NOT full history.
    /// </summary>
    private static string BuildRepairPrompt(StructuredResult<RecipeDocument> failed)
    {
        if (failed.Validation is null || failed.Validation.Errors.Count == 0)
        {
            // Should not normally reach here (the short-circuit catches the no-validation case),
            // but defensive: emit a generic re-emit prompt.
            return $"""
                Your previous response could not be parsed. {failed.SanitizedError ?? "Unknown error."}
                Re-emit the recipe in the required structured JSON format. Same constraints, same schema.
                """;
        }

        var errorLines = failed.Validation.Errors
            .Select(e => $"  - {e.Path}: {e.Message}");

        return $"""
            Your previous response did not match the required schema.
            Errors:
            {string.Join("\n", errorLines)}
            Re-emit the recipe in the structured format. Same constraints, same schema.
            """;
    }
}
