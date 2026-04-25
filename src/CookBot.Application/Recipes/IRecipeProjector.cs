using CookBot.Domain.Entities;
using CookBot.Domain.Recipes;

namespace CookBot.Application.Recipes;

/// <summary>
/// Projects a relational <see cref="Recipe"/> entity onto a canonical
/// <see cref="RecipeDocument"/>. Defined in the Application layer so
/// <c>RecipeService</c> can consume it without referencing
/// <c>CookBot.Infrastructure</c> (no layer inversion). The Phase 1 implementation
/// is <c>LegacyRecipeProjector</c> in Infrastructure.
///
/// DELETE-AFTER-V1.1: this interface and its single Phase 1 implementation are
/// throwaway; Phase 4 (POLISH-03) drops both.
/// </summary>
public interface IRecipeProjector
{
    RecipeDocument Project(Recipe recipe);
}
