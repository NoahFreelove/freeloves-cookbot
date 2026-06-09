using System.Text.Json;
using System.Text.Json.Serialization;
using CookBot.Application.Services;
using CookBot.Domain.Recipes;

namespace CookBot.Application.Recipes;

/// <summary>
/// Projects a <see cref="RecipeDocument"/> into a Schema.org Recipe JSON-LD string.
/// Pure static function — no DI, no data-service calls, no CanonicalDocumentJson access.
///
/// Security: uses the System.Text.Json DEFAULT (HTML-safe) encoder so &lt;, &gt;, and &amp;
/// in recipe content are unicode-escaped. NEVER set UnsafeRelaxedJsonEscaping here —
/// the output is rendered as a raw MarkupString inside a &lt;script&gt; block (Plan 03).
/// </summary>
public static class JsonLdRecipeProjector
{
    // DEFAULT encoder (NOT UnsafeRelaxedJsonEscaping) — escapes <,>,& to \uXXXX so the output
    // is safe inside a raw <script type="application/ld+json"> (MarkupString) block.
    private static readonly JsonSerializerOptions LdOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    // Curated allow-lists for deterministic tag → Schema.org field classification.
    // Tags case-insensitively matched; the curated spelling is emitted (not the tag itself).
    // NEVER fabricate these fields — only emit on a confirmed match (INTEROP-02 / Q2).
    private static readonly (string Key, string Display)[] CuisineList =
    [
        ("italian", "Italian"),
        ("mexican", "Mexican"),
        ("thai", "Thai"),
        ("french", "French"),
        ("chinese", "Chinese"),
        ("indian", "Indian"),
        ("japanese", "Japanese"),
        ("greek", "Greek"),
        ("spanish", "Spanish"),
        ("korean", "Korean"),
        ("mediterranean", "Mediterranean"),
        ("american", "American"),
    ];

    private static readonly (string Key, string Display)[] CategoryList =
    [
        ("breakfast", "Breakfast"),
        ("lunch", "Lunch"),
        ("dinner", "Dinner"),
        ("dessert", "Dessert"),
        ("appetizer", "Appetizer"),
        ("snack", "Snack"),
        ("side dish", "Side Dish"),
        ("main course", "Main Course"),
        ("salad", "Salad"),
        ("soup", "Soup"),
        ("beverage", "Beverage"),
        ("bread", "Bread"),
    ];

    /// <summary>
    /// Projects <paramref name="doc"/> to a Schema.org Recipe JSON-LD string.
    /// </summary>
    /// <param name="doc">The canonical recipe document to project.</param>
    /// <param name="absoluteImageUrl">
    /// An absolute HTTPS image URL resolved at the Web layer (via NavigationManager + RecipePhotoUrlValidator),
    /// or null. The image property is omitted entirely when null — the projector never derives the URL itself.
    /// </param>
    public static string Project(RecipeDocument doc, string? absoluteImageUrl)
    {
        // Keywords: ALL tags always go to keywords (comma-joined).
        var keywords = doc.Tags.Count > 0 ? string.Join(", ", doc.Tags) : null;

        // recipeCuisine: first tag case-insensitively matching the CUISINE allow-list; emit curated spelling.
        string? recipeCuisine = null;
        foreach (var tag in doc.Tags)
        {
            var match = Array.Find(CuisineList, c => string.Equals(c.Key, tag, StringComparison.OrdinalIgnoreCase));
            if (match.Key != null)
            {
                recipeCuisine = match.Display;
                break;
            }
        }

        // recipeCategory: first tag case-insensitively matching the COURSE/CATEGORY allow-list; emit curated spelling.
        string? recipeCategory = null;
        foreach (var tag in doc.Tags)
        {
            var match = Array.Find(CategoryList, c => string.Equals(c.Key, tag, StringComparison.OrdinalIgnoreCase));
            if (match.Key != null)
            {
                recipeCategory = match.Display;
                break;
            }
        }

        // ISO-8601 durations
        var prepTime = Iso8601DurationFormatter.ToIso8601Duration(doc.PrepTimeMinutes);
        var cookTime = Iso8601DurationFormatter.ToIso8601Duration(doc.CookTimeMinutes);
        var totalMinutes = (doc.PrepTimeMinutes ?? 0) + (doc.CookTimeMinutes ?? 0);
        var totalTime = totalMinutes > 0 ? Iso8601DurationFormatter.ToIso8601Duration(totalMinutes) : null;

        // Ingredients: one string per IngredientEntry
        var recipeIngredient = doc.Ingredients.Count > 0
            ? doc.Ingredients.Select(BuildIngredientLine).ToArray()
            : null;

        // Steps: walk with pattern-match, building HowToSection / HowToStep hierarchy
        var recipeInstructions = BuildInstructions(doc.Steps);

        // Author: only when AuthorName is non-null
        object? author = doc.Provenance?.AuthorName is { Length: > 0 } authorName
            ? new Dictionary<string, string>
            {
                ["@type"] = "Person",
                ["name"] = authorName,
            }
            : null;

        // Build the ordered model — @context and @type MUST come first.
        // Use an ordered dictionary and only add non-null entries so absent fields are omitted.
        // (DefaultIgnoreCondition = WhenWritingNull applies to typed properties, not Dictionary values.)
        var model = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Recipe",
            ["name"] = doc.Name,
        };

        if (absoluteImageUrl is not null)        model["image"] = absoluteImageUrl;
        if (doc.Description is not null)         model["description"] = doc.Description;
        model["recipeYield"] = doc.Servings;
        if (prepTime is not null)                model["prepTime"] = prepTime;
        if (cookTime is not null)                model["cookTime"] = cookTime;
        if (totalTime is not null)               model["totalTime"] = totalTime;
        if (keywords is not null)                model["keywords"] = keywords;
        if (recipeCuisine is not null)           model["recipeCuisine"] = recipeCuisine;
        if (recipeCategory is not null)          model["recipeCategory"] = recipeCategory;
        if (recipeIngredient is not null)        model["recipeIngredient"] = recipeIngredient;
        if (recipeInstructions.Count > 0)        model["recipeInstructions"] = recipeInstructions;
        if (author is not null)                  model["author"] = author;
        // NEVER emit: aggregateRating, review, datePublished

        return JsonSerializer.Serialize(model, LdOptions);
    }

    private static string BuildIngredientLine(IngredientEntry ing)
    {
        // Build parts list and join on a single space, skipping empties, so a unit-less
        // ingredient (e.g. Amount=4, Unit="") emits "4 eggs" not "4  eggs" (WR-04).
        var parts = new List<string>(3);
        var amt = FractionFormatter.Format(ing.Amount);
        if (ing.Amount > 0) parts.Add(amt);
        if (!string.IsNullOrEmpty(ing.Unit)) parts.Add(ing.Unit);
        parts.Add(ing.Name);
        var line = string.Join(" ", parts);
        if (!string.IsNullOrEmpty(ing.Note)) line += $" ({ing.Note})";
        return line;
    }

    private static List<object> BuildInstructions(IReadOnlyList<StepNode> steps)
    {
        var result = new List<object>();
        List<object>? currentSection = null;
        string? currentSectionName = null;

        foreach (var step in steps)
        {
            switch (step)
            {
                case SectionStep s:
                    // Flush any preceding bare steps before opening a section
                    FlushSection(result, ref currentSection, ref currentSectionName);
                    currentSectionName = s.Heading;
                    currentSection = [];
                    break;

                case ContentStep c:
                    var howToStep = new Dictionary<string, string>
                    {
                        ["@type"] = "HowToStep",
                        ["text"] = RecipeStepTextFormatter.ToPlainText(c.Text),
                    };

                    if (currentSection is not null)
                    {
                        currentSection.Add(howToStep);
                    }
                    else
                    {
                        result.Add(howToStep);
                    }
                    break;
            }
        }

        // Flush the final open section
        FlushSection(result, ref currentSection, ref currentSectionName);

        return result;
    }

    private static void FlushSection(
        List<object> result,
        ref List<object>? currentSection,
        ref string? currentSectionName)
    {
        if (currentSection is null) return;
        // WR-06: skip empty sections (consecutive SectionSteps, or trailing SectionStep with
        // no following ContentStep). An empty HowToSection itemListElement is meaningless and
        // some validators warn on it.
        if (currentSection.Count == 0) { currentSection = null; currentSectionName = null; return; }

        var section = new Dictionary<string, object>
        {
            ["@type"] = "HowToSection",
            ["name"] = currentSectionName ?? string.Empty,
            ["itemListElement"] = currentSection,
        };
        result.Add(section);

        currentSection = null;
        currentSectionName = null;
    }
}
