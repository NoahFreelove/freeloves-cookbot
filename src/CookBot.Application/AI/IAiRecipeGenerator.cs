using CookBot.Domain.Recipes;

namespace CookBot.Application.AI;

/// <summary>
/// AI-02 surface. Single entry point for recipe-emitting AI calls (AiChat
/// recipe-save flow, future "regenerate this recipe" affordances). Free-form
/// chat turns bypass this and use IAiService.StreamMessageAsync directly.
/// </summary>
public interface IAiRecipeGenerator
{
    /// <summary>
    /// Generates a recipe from a natural-language prompt. Runs the validate→repair→fail
    /// pipeline with a hard cap of 2 retries (3 total model calls). Never throws —
    /// all failure modes return a populated <see cref="StructuredResult{T}"/>.
    /// </summary>
    /// <param name="userPrompt">Free-form user request.</param>
    /// <param name="apiKey">Anthropic API key; falls back to host config when null.</param>
    /// <param name="modelId">CuratedModels id; falls back to <c>DefaultModelId</c> when null.</param>
    /// <param name="userId">
    /// Phase 9 / Plan 09-05 — id of the user who triggered the call. Required (along with
    /// <paramref name="keyOwnerId"/>) for telemetry; when either is null, the telemetry
    /// write is silently skipped (the v1.2 AI-off contract trumps PROD-15).
    /// </param>
    /// <param name="keyOwnerId">
    /// Id of the user whose API key paid for the call (PITFALL C2 owner-share semantics).
    /// Equal to <paramref name="userId"/> when the caller used their own key; differs
    /// when consuming a shared key.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<StructuredResult<RecipeDocument>> GenerateAsync(
        string userPrompt,
        string? apiKey = null,
        string? modelId = null,
        int? userId = null,
        int? keyOwnerId = null,
        CancellationToken ct = default);
}
