using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CookBot.Application.Recipes;
using CookBot.Domain.Interfaces;
using CookBot.Domain.Recipes;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CookBot.Application.Services;

/// <summary>
/// Implementation of <see cref="IRecipeFormatParser"/> that delegates to the canonical
/// schema stack (<see cref="RecipeUpcasterChain"/> -> <see cref="JsonRecipeSerializer"/>
/// -> <see cref="RecipeValidator"/>) introduced in Plan 01-01. Public surface is preserved
/// per D-10 — all existing callers continue to compile.
///
/// Pipeline (D-10 step list):
/// <list type="number">
///   <item>Detect YAML frontmatter vs raw JSON.</item>
///   <item>Convert YAML -> <see cref="JsonNode"/> via the in-tree adapter (Pattern 5; no second YAML library).</item>
///   <item>Stamp <c>version: 1</c> if absent (Pitfall H1).</item>
///   <item>Run through <see cref="RecipeUpcasterChain.UpcastToCurrent"/>.</item>
///   <item>Deserialize to <see cref="RecipeDocument"/> via <see cref="JsonRecipeSerializer"/>.</item>
///   <item>Run <see cref="RecipeValidator.Validate"/>.</item>
///   <item>Project the <see cref="RecipeDocument"/> back to the legacy flat
///         <see cref="ParsedRecipe"/> for back-compat with existing callers.</item>
/// </list>
/// </summary>
public class RecipeFormatParser : IRecipeFormatParser
{
    private static readonly Regex FrontmatterRegex = new(
        @"^---\s*\n(.*?)\n---\s*\n?(.*)$",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly IDeserializer _yamlDeserializer;
    private readonly ISerializer _yamlSerializer;
    private readonly RecipeUpcasterChain _upcasterChain;
    private readonly JsonRecipeSerializer _jsonSerializer;
    private readonly RecipeValidator _validator;

    public RecipeFormatParser(
        RecipeUpcasterChain upcasterChain,
        JsonRecipeSerializer jsonSerializer,
        RecipeValidator validator)
    {
        _upcasterChain = upcasterChain;
        _jsonSerializer = jsonSerializer;
        _validator = validator;

        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
    }

    public ParsedRecipe Parse(string rawContent)
    {
        if (TryParse(rawContent, out var parsed, out var errors) && parsed is not null)
        {
            return parsed;
        }
        throw new FormatException($"Failed to parse recipe: {string.Join("; ", errors)}");
    }

    public bool TryParse(string rawContent, out ParsedRecipe? recipe, out List<string> errors)
    {
        errors = new List<string>();
        recipe = null;

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            errors.Add("Recipe content is empty.");
            return false;
        }

        try
        {
            // 1. Detect format: YAML frontmatter vs raw JSON.
            JsonNode node;
            var trimmed = rawContent.TrimStart();
            if (trimmed.StartsWith("---"))
            {
                var match = FrontmatterRegex.Match(trimmed);
                if (!match.Success)
                {
                    errors.Add("Missing YAML frontmatter delimiters.");
                    return false;
                }
                node = YamlToJsonNode(match.Groups[1].Value);
            }
            else
            {
                node = JsonNode.Parse(rawContent)
                       ?? throw new FormatException("Empty JSON.");
            }

            // 2. Stamp version=1 if absent (Pitfall H1).
            if (node is JsonObject root && root["version"] is null)
            {
                root["version"] = 1;
            }

            // 3. Upcast to current.
            var upcasted = _upcasterChain.UpcastToCurrent(node);

            // 4. Deserialize.
            var doc = _jsonSerializer.Deserialize(upcasted);

            // 5. Validate semantically.
            var result = _validator.Validate(doc);
            if (!result.IsValid)
            {
                errors.AddRange(result.Errors.Select(e => $"{e.Path}: {e.Message}"));
                return false;
            }

            // 6. Project to legacy ParsedRecipe.
            recipe = ProjectToParsedRecipe(doc);
            return true;
        }
        catch (Exception ex)
        {
            errors.Add($"Parse error: {ex.Message}");
            return false;
        }
    }

    public string Serialize(ParsedRecipe recipe)
    {
        var frontmatter = new RecipeFrontmatter
        {
            Name = recipe.Name,
            Servings = recipe.Servings,
            PrepTime = recipe.PrepTimeMinutes,
            CookTime = recipe.CookTimeMinutes,
            PhotoUrl = recipe.PhotoUrl,
            Description = recipe.Description,
            Tags = recipe.Tags.Any() ? recipe.Tags : null,
            Ingredients = recipe.Ingredients.Select(i => new IngredientFrontmatter
            {
                Id = i.LocalId,
                Name = i.Name,
                Amount = i.Amount,
                Unit = i.Unit,
                Note = i.Note,
            }).ToList(),
            Steps = recipe.Steps.Select(s => s.IsSection
                ? new StepFrontmatter { Section = s.Text }
                : new StepFrontmatter
                {
                    Text = s.Text,
                    Timers = s.Timers?.Any() == true
                        ? s.Timers.Select(t => new TimerFrontmatter
                        {
                            Duration = t.Duration,
                            Unit = t.Unit,
                            Label = t.Label,
                        }).ToList()
                        : null,
                    Temperature = s.Temperature is null ? null : new TemperatureFrontmatter
                    {
                        Value = s.Temperature.Value,
                        Unit = s.Temperature.Unit.ToString().ToLowerInvariant(),
                    },
                }
            ).ToList(),
        };

        var yaml = _yamlSerializer.Serialize(frontmatter).TrimEnd();
        return $"---\n{yaml}\n---\n";
    }

    // ---- YAML -> JsonNode adapter (Pattern 5 — no second YAML library) ----

    private JsonNode YamlToJsonNode(string yamlContent)
    {
        // YamlDotNet's untyped Deserialize materializes to int/long/double/string/bool/
        // List<object?>/Dictionary<object,object?>; ConvertGraph maps that onto JsonNode.
        var graph = _yamlDeserializer.Deserialize(yamlContent);
        return ConvertGraph(graph) ?? new JsonObject();
    }

    private static JsonNode? ConvertGraph(object? value) => value switch
    {
        null => null,
        string s => StringToJsonValue(s),
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        double d => JsonValue.Create(d),
        IDictionary<object, object?> dict => DictToObj(dict),
        IList<object?> list => ListToArr(list),
        _ => JsonValue.Create(value.ToString()),
    };

    /// <summary>
    /// YamlDotNet's untyped Deserialize returns every scalar as a <see cref="string"/>.
    /// To match YAML 1.2 Core Schema tag resolution (and what the canonical pipeline
    /// expects), coerce numeric/boolean-shaped strings back to typed JSON values. Quoted
    /// strings in the source survive because YamlDotNet returns them as <see cref="string"/>
    /// without surrounding quotes — meaning <c>"4"</c> in YAML and <c>4</c> in YAML both
    /// arrive here as the string "4". This is a known limitation of untyped YAML; the
    /// canonical `RecipeDocument` shape pins types via <see cref="JsonPropertyName"/>, so
    /// the few fields where this matters (e.g. <c>servings</c>) are unambiguous.
    /// </summary>
    private static JsonNode StringToJsonValue(string s)
    {
        if (bool.TryParse(s, out var b))
        {
            return JsonValue.Create(b);
        }
        if (int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i))
        {
            return JsonValue.Create(i);
        }
        if (long.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var l))
        {
            return JsonValue.Create(l);
        }
        if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
        {
            return JsonValue.Create(d);
        }
        return JsonValue.Create(s)!;
    }

    private static JsonObject DictToObj(IDictionary<object, object?> dict)
    {
        var obj = new JsonObject();
        foreach (var kvp in dict)
        {
            var key = kvp.Key.ToString();
            if (key is null)
            {
                continue;
            }
            obj[key] = ConvertGraph(kvp.Value);
        }
        return obj;
    }

    private static JsonArray ListToArr(IList<object?> list)
    {
        var arr = new JsonArray();
        foreach (var item in list)
        {
            arr.Add(ConvertGraph(item));
        }
        return arr;
    }

    // ---- RecipeDocument -> ParsedRecipe projection (legacy boundary) ----

    private static ParsedRecipe ProjectToParsedRecipe(RecipeDocument doc) => new()
    {
        Name = doc.Name,
        Servings = doc.Servings,
        PrepTimeMinutes = doc.PrepTimeMinutes,
        CookTimeMinutes = doc.CookTimeMinutes,
        PhotoUrl = doc.PhotoUrl,
        Description = doc.Description,
        Tags = doc.Tags.ToList(),
        Ingredients = doc.Ingredients.Select(i => new ParsedIngredient
        {
            LocalId = i.Id,
            Name = i.Name,
            Amount = i.Amount,
            Unit = i.Unit,
            Note = i.Note,
        }).ToList(),
        Steps = doc.Steps.Select(s => s switch
        {
            ContentStep c => new ParsedStep
            {
                Text = c.Text,
                IsSection = false,
                Timers = c.Timers?.Select(t => new ParsedTimer
                {
                    Duration = t.Duration,
                    Unit = t.Unit,
                    Label = t.Label,
                }).ToList(),
                Temperature = c.Temperature,
            },
            SectionStep sec => new ParsedStep
            {
                Text = sec.Heading,
                IsSection = true,
                Timers = null,
            },
            _ => throw new InvalidOperationException($"Unknown StepNode kind: {s.GetType().Name}"),
        }).ToList(),
    };

    // ---- back-compat YAML-out shape for Serialize(ParsedRecipe) ----

    private class RecipeFrontmatter
    {
        public string? Name { get; set; }
        public int Servings { get; set; } = 1;
        public int? PrepTime { get; set; }
        public int? CookTime { get; set; }
        public string? PhotoUrl { get; set; }
        public string? Description { get; set; }
        public List<string>? Tags { get; set; }
        public List<IngredientFrontmatter>? Ingredients { get; set; }
        public List<StepFrontmatter>? Steps { get; set; }
    }

    private class IngredientFrontmatter
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public double Amount { get; set; }
        public string? Unit { get; set; }
        public string? Note { get; set; }
    }

    private class StepFrontmatter
    {
        public string? Text { get; set; }
        public string? Section { get; set; }
        public List<TimerFrontmatter>? Timers { get; set; }
        public TemperatureFrontmatter? Temperature { get; set; }
    }

    private class TimerFrontmatter
    {
        public int Duration { get; set; }
        public string? Unit { get; set; }
        public string? Label { get; set; }
    }

    private class TemperatureFrontmatter
    {
        public decimal Value { get; set; }
        public string? Unit { get; set; }
    }
}
