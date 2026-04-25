namespace CookBot.Application.Recipes;

/// <summary>
/// Single source of truth for the format-spec prose embedded in both AI prompt sites
/// (PromptBuilderService.ResolveRecipeFormat and BuildCopyablePrompt).
/// </summary>
public interface IRecipeSchemaDocumentationProvider
{
    /// <summary>
    /// Returns the v2 recipe format-spec prose. Wired into both AI system-prompt builders.
    /// Implementation MUST NOT contain any "fallback / informal / plain numbered" opt-out
    /// language — see PromptDenylistTest (Plan 04).
    /// </summary>
    string GetFormatPrompt();
}
