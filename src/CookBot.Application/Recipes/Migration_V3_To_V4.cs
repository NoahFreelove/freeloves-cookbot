using System.Linq;
using System.Text.Json.Nodes;

namespace CookBot.Application.Recipes;

/// <summary>
/// JSON-node-level rewrites moving a v3 recipe document to v4. Adds support for:
/// <list type="bullet">
///   <item><c>equipment</c> — recipe-level equipment list (<see cref="Domain.Recipes.RecipeDocument.Equipment"/>)</item>
///   <item><c>provenance</c> — optional source / author credit (<see cref="Domain.Recipes.RecipeDocument.Provenance"/>)</item>
///   <item>per-step <c>donenessCue</c> — optional doneness cue on each <c>ContentStep</c></item>
///   <item>per-ingredient <c>substitutions</c> — optional substitution list on each ingredient</item>
/// </list>
/// All four guards are <em>no-ops by design</em>: System.Text.Json maps absent JSON keys to
/// <see langword="null"/> (or empty-list default) on the corresponding C# properties, so
/// explicit null-injection is unnecessary. The explicit guards document the per-field contract
/// and defend against future modifications that might bundle the additions
/// (PITFALLS C7 — never bundle-throw, P2).
/// Stamps <c>version: 4</c> on completion.
/// </summary>
public sealed class Migration_V3_To_V4 : IRecipeUpcaster
{
    public int FromVersion => 3;
    public int ToVersion => 4;

    public JsonNode Upcast(JsonNode input)
    {
        var obj = input.AsObject();

        // Guard 1: equipment absent => stays absent (IReadOnlyList<string> defaults to []).
        // PITFALLS C7 — independent from other guards; partial absence of one field cannot break the others.
        if (obj["equipment"] is null) { /* no-op: IReadOnlyList<string> defaults to [] */ }

        // Guard 2: provenance absent => stays absent (RecipeProvenance? defaults to null).
        // PITFALLS C7 — independent from Guard 1.
        if (obj["provenance"] is null) { /* no-op: RecipeProvenance? defaults to null */ }

        // Guard 3: per-step donenessCue absent => stays absent (NEVER zero-fill).
        // PITFALLS C7 — independent from Guards 1 and 2.
        if (obj["steps"] is JsonArray steps)
        {
            foreach (var step in steps.OfType<JsonObject>())
            {
                // Only ContentSteps carry donenessCue; SectionSteps never do.
                if (step["kind"]?.GetValue<string>() == "content" && step["donenessCue"] is null)
                {
                    // no-op: ContentStep.DonenessCue is string?; STJ maps absent -> null.
                }
            }
        }

        // Guard 4: per-ingredient substitutions absent => stays absent (IReadOnlyList defaults to []).
        // PITFALLS C7 — independent from Guards 1, 2, and 3.
        if (obj["ingredients"] is JsonArray ingredients)
        {
            foreach (var ingredient in ingredients.OfType<JsonObject>())
            {
                if (ingredient["substitutions"] is null)
                {
                    // no-op: IngredientEntry.Substitutions is IReadOnlyList<IngredientSubstitution>; defaults to [].
                }
            }
        }

        obj["version"] = 4;
        return obj;
    }
}
