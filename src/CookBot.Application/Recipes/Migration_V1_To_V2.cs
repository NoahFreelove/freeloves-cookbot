using System.Text.Json.Nodes;

namespace CookBot.Application.Recipes;

/// <summary>
/// JSON-node-level rewrites moving a v1 recipe document to v2. Reconciles the
/// pre-canonical divergences:
/// <list type="bullet">
///   <item><c>prepTime</c>      -> <c>prepTimeMinutes</c> (Pitfall C2)</item>
///   <item><c>cookTime</c>      -> <c>cookTimeMinutes</c> (Pitfall C2)</item>
///   <item>ingredient <c>localId</c> -> <c>id</c> (D-06)</item>
///   <item><c>{ isSection: true, text: "X" }</c> step -> <c>{ kind: "section", heading: "X" }</c></item>
///   <item><c>{ section: "Z" }</c> legacy YAML step  -> <c>{ kind: "section", heading: "Z" }</c></item>
///   <item>plain step                                -> <c>{ kind: "content", text, timers, ... }</c></item>
/// </list>
/// Stamps <c>version: 2</c> on completion.
/// </summary>
public sealed class Migration_V1_To_V2 : IRecipeUpcaster
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public JsonNode Upcast(JsonNode input)
    {
        var obj = input.AsObject();

        // 1. Time-field rename (units in field name; Pitfall C2 / FORMAT-03)
        RenameKey(obj, "prepTime", "prepTimeMinutes");
        RenameKey(obj, "cookTime", "cookTimeMinutes");

        // 2. ingredients[].localId -> ingredients[].id (D-06)
        if (obj["ingredients"] is JsonArray ings)
        {
            foreach (var ing in ings.OfType<JsonObject>())
            {
                RenameKey(ing, "localId", "id");
            }
        }

        // 3. steps: rebuild each into a kind-discriminated shape
        if (obj["steps"] is JsonArray steps)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] is not JsonObject step)
                {
                    continue;
                }

                var isSection = step["isSection"]?.GetValue<bool>() == true
                                || step["section"] is not null;

                if (isSection)
                {
                    var heading = step["section"]?.GetValue<string>()
                                  ?? step["text"]?.GetValue<string>()
                                  ?? string.Empty;

                    steps[i] = new JsonObject
                    {
                        ["kind"] = "section",
                        ["heading"] = heading,
                    };
                }
                else
                {
                    step.Remove("isSection");
                    step.Remove("section");

                    // Insert kind discriminator first, then preserve text/timers/extras.
                    var rebuilt = new JsonObject { ["kind"] = "content" };
                    foreach (var kvp in step.ToList())
                    {
                        rebuilt[kvp.Key] = kvp.Value?.DeepClone();
                    }
                    steps[i] = rebuilt;
                }
            }
        }

        obj["version"] = 2;
        return obj;
    }

    /// <summary>
    /// If <paramref name="from"/> is absent, no-op. If <paramref name="to"/> already exists,
    /// drop the legacy key (explicit-wins precedence). Otherwise deep-clone the value into
    /// the new key and remove the old.
    /// </summary>
    private static void RenameKey(JsonObject obj, string from, string to)
    {
        if (!obj.ContainsKey(from))
        {
            return;
        }

        if (obj.ContainsKey(to))
        {
            obj.Remove(from);
            return;
        }

        var value = obj[from]?.DeepClone();
        obj.Remove(from);
        obj[to] = value;
    }
}
