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
    Task<StructuredResult<RecipeDocument>> GenerateAsync(
        string userPrompt,
        string? apiKey = null,
        string? modelId = null,
        CancellationToken ct = default);
}
