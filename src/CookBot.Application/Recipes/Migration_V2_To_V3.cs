using System.Linq;
using System.Text.Json.Nodes;

namespace CookBot.Application.Recipes;

/// <summary>
/// JSON-node-level rewrites moving a v2 recipe document to v3. Adds support for:
/// <list type="bullet">
///   <item><c>photoUrl</c> — optional recipe cover photo URL (<see cref="Domain.Recipes.RecipeDocument.PhotoUrl"/>)</item>
///   <item><c>description</c> — optional recipe description (<see cref="Domain.Recipes.RecipeDocument.Description"/>)</item>
///   <item>per-step <c>temperature</c> — optional oven/cooking temperature on each <c>ContentStep</c></item>
/// </list>
/// All three guards are <em>no-ops by design</em>: System.Text.Json maps absent JSON keys to
/// <see langword="null"/> on nullable C# properties, so explicit null-injection is unnecessary.
/// The explicit guards document the per-field contract (Phase 8 D-29) and defend against
/// future modifications that might bundle the additions (PITFALLS C7 — never bundle-throw).
/// Stamps <c>version: 3</c> on completion.
/// </summary>
/// <remarks>
/// PITFALLS M2: temperature is NEVER zero-filled. A step missing <c>temperature</c> keeps it
/// absent; the typed <see cref="Domain.Recipes.StepNode.ContentStep.Temperature"/> property
/// deserializes to <see langword="null"/>.
/// </remarks>
public sealed class Migration_V2_To_V3 : IRecipeUpcaster
{
    public int FromVersion => 2;
    public int ToVersion => 3;

    public JsonNode Upcast(JsonNode input)
    {
        var obj = input.AsObject();

        // Guard 1: photoUrl absent => stays absent (deserializes to null on RecipeDocument.PhotoUrl: string?).
        // PITFALLS C7 — independent from other guards; partial absence of one field cannot break the others.
        if (obj["photoUrl"] is null) { /* no-op: STJ maps absent -> null on PhotoUrl: string? */ }

        // Guard 2: description absent => stays absent (deserializes to null on RecipeDocument.Description: string?).
        // PITFALLS C7 — independent from Guard 1.
        if (obj["description"] is null) { /* no-op: STJ maps absent -> null on Description: string? */ }

        // Guard 3: per-step temperature absent => stays absent (NEVER zero-fill — PITFALLS M2).
        // PITFALLS C7 — independent from Guards 1 and 2.
        if (obj["steps"] is JsonArray steps)
        {
            foreach (var step in steps.OfType<JsonObject>())
            {
                // Only ContentSteps carry temperature; SectionSteps never do.
                if (step["kind"]?.GetValue<string>() == "content" && step["temperature"] is null)
                {
                    // no-op: ContentStep.Temperature is StepTemperature?; STJ maps absent -> null.
                }
            }
        }

        obj["version"] = 3;
        return obj;
    }
}
