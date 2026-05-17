using System.Text;
using System.Text.Json;
using CookBot.Application.Recipes;
using CookBot.Domain.Entities;
using CookBot.Domain.Enums;

namespace CookBot.Application.Services;

public class PromptBuilderService
{
    private readonly IRecipeSchemaDocumentationProvider _docs;

    public PromptBuilderService(IRecipeSchemaDocumentationProvider docs)
    {
        _docs = docs;
    }

    public static readonly string DefaultTemplate = string.Join("\n", new[]
    {
        "You are CookBot, an expert AI cooking assistant. You help users discover, create, and refine recipes.",
        "",
        "{{experience_level}}",
        "",
        "{{unit_system}}",
        "",
        "{{equipment}}",
        "Only suggest recipes the user can make with their available equipment.",
        "",
        "{{dietary_preferences}}",
        "All recipes MUST comply with these dietary requirements.",
        "",
        "{{pantry}}",
        "",
        "{{recipe_format}}"
    });

    /// <summary>
    /// Phase 10 / Plan 10-06 / D-52 — null-fallback override on profile.AiSystemPromptTemplate.
    /// Whitespace-only treated as null. Corrects REQUIREMENTS QOL-06 "already loaded" misclaim.
    /// </summary>
    public string BuildSystemPrompt(UserProfile profile, IEnumerable<PantryItem>? pantryItems = null)
    {
        var template = string.IsNullOrWhiteSpace(profile.AiSystemPromptTemplate)
            ? DefaultTemplate
            : profile.AiSystemPromptTemplate;
        return ResolveTemplate(template, profile, pantryItems);
    }

    public string ResolveTemplate(string template, UserProfile profile, IEnumerable<PantryItem>? pantryItems = null)
    {
        var tokenMap = new Dictionary<string, string>
        {
            ["{{experience_level}}"] = ResolveExperienceLevel(profile),
            ["{{unit_system}}"] = ResolveUnitSystem(profile),
            ["{{equipment}}"] = ResolveEquipment(profile),
            ["{{dietary_preferences}}"] = ResolveDietaryPreferences(profile),
            ["{{pantry}}"] = ResolvePantry(pantryItems),
            ["{{recipe_format}}"] = ResolveRecipeFormat()
        };

        var result = template;
        foreach (var (token, value) in tokenMap)
        {
            if (string.IsNullOrEmpty(value))
            {
                result = result.Replace(token + "\n", "");
                result = result.Replace(token, "");
            }
            else
            {
                result = result.Replace(token, value);
            }
        }

        while (result.Contains("\n\n\n"))
            result = result.Replace("\n\n\n", "\n\n");

        return result.Trim();
    }

    // System prompt for in-flow cooking step assistance (concise, step-focused).
    public string BuildCookingStepAssistSystemPrompt(UserProfile profile)
    {
        var prefs = ResolveTemplate(
            "{{experience_level}}\n{{unit_system}}\n{{equipment}}\n{{dietary_preferences}}",
            profile,
            pantryItems: null);

        return $"""
            {prefs}

            You are CookBot's **live cooking assistant**. The user is actively cooking from a recipe in the app.

            Each request includes the **full recipe** in CookBot YAML plus a **CURRENT STEP** section. Answer with that step in mind; use the rest of the recipe only for helpful context (timing, ingredients, order of operations).

            Be **concise** (short paragraphs or bullets). Use markdown when it helps. Cover techniques, timing, doneness cues, substitutions, and food safety when relevant. If something critical is ambiguous, ask **one** focused clarifying question.

            Do not dump the whole recipe back unless they explicitly ask for a recap.
            """.Trim();
    }

    private string ResolveExperienceLevel(UserProfile profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"The user's experience level is: {profile.ExperienceLevel}.");
        switch (profile.ExperienceLevel)
        {
            case ExperienceLevel.Beginner:
                sb.AppendLine("Provide detailed explanations for techniques. Define cooking terms. Suggest simple substitutions. Be encouraging.");
                break;
            case ExperienceLevel.Intermediate:
                sb.AppendLine("Explain advanced techniques briefly. Suggest variations and improvements.");
                break;
            case ExperienceLevel.Advanced:
                sb.AppendLine("Be concise. Focus on creative twists, flavor profiles, and technique optimization.");
                break;
            case ExperienceLevel.Professional:
                sb.AppendLine("Use professional culinary terminology. Discuss plating, technique refinement, and flavor chemistry.");
                break;
        }
        return sb.ToString().TrimEnd();
    }

    private string ResolveUnitSystem(UserProfile profile)
    {
        var sb = new StringBuilder();
        switch (profile.UnitSystem)
        {
            case UnitSystem.Canadian:
                sb.AppendLine("Preferred unit system: Canadian-style mixed units.");
                sb.AppendLine("Use cups, tablespoons, and teaspoons for volume measurements.");
                sb.AppendLine("Use grams and kilograms for weight measurements.");
                sb.AppendLine("NEVER use fluid ounces, ounces, or pounds — Canadians don't use those.");
                sb.AppendLine("Use Fahrenheit for baking temperatures.");
                break;
            case UnitSystem.Metric:
                sb.AppendLine("Preferred unit system: Metric.");
                sb.AppendLine("Use millilitres and litres for volume, grams and kilograms for weight, Celsius for temperatures.");
                break;
            case UnitSystem.Imperial:
                sb.AppendLine("Preferred unit system: Imperial.");
                sb.AppendLine("Use cups, tablespoons, teaspoons, fluid ounces for volume. Ounces and pounds for weight. Fahrenheit for temperatures.");
                break;
        }

        if (!string.IsNullOrWhiteSpace(profile.AiUnitExceptions))
        {
            sb.AppendLine();
            sb.AppendLine("User-specified unit exceptions (follow these even if they refine or partially override the preset above):");
            sb.AppendLine(profile.AiUnitExceptions.Trim());
        }

        return sb.ToString().TrimEnd();
    }

    private string ResolveEquipment(UserProfile profile)
    {
        var tools = JsonSerializer.Deserialize<List<string>>(profile.KitchenToolsJson) ?? new();
        if (!tools.Any()) return "";
        return $"Available kitchen tools: {string.Join(", ", tools)}.";
    }

    private string ResolveDietaryPreferences(UserProfile profile)
    {
        var diets = JsonSerializer.Deserialize<List<string>>(profile.DietaryPreferencesJson) ?? new();
        if (!diets.Any()) return "";
        return $"Dietary preferences/restrictions: {string.Join(", ", diets)}.";
    }

    private string ResolvePantry(IEnumerable<PantryItem>? pantryItems)
    {
        if (pantryItems?.Any() != true) return "";
        var sb = new StringBuilder();
        sb.AppendLine("Current pantry inventory:");
        foreach (var item in pantryItems)
        {
            sb.AppendLine($"  - {item.Ingredient.Name}: {item.Amount} {UnitParser.ToDisplayString(item.Unit)}");
        }
        sb.AppendLine("When possible, prioritize recipes using ingredients the user already has.");
        return sb.ToString().TrimEnd();
    }

    private string ResolveRecipeFormat() => _docs.GetFormatPrompt();

    public string BuildCopyablePrompt(
        string userRequest,
        UserProfile? profile,
        IEnumerable<PantryItem>? pantryItems,
        bool includeProfile,
        bool includePantry)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert cooking assistant. Help me with the following request.");
        sb.AppendLine();

        if (includeProfile && profile != null)
        {
            sb.AppendLine("## About Me");
            sb.AppendLine($"- Experience level: {profile.ExperienceLevel}");

            switch (profile.UnitSystem)
            {
                case UnitSystem.Canadian:
                    sb.AppendLine("- Units: Canadian-style — use cups/tbsp/tsp for volume, grams/kg for weight. NEVER use ounces or pounds. Use Fahrenheit for baking temps.");
                    break;
                case UnitSystem.Metric:
                    sb.AppendLine("- Units: Metric — use mL/L for volume, g/kg for weight, Celsius for temperatures.");
                    break;
                case UnitSystem.Imperial:
                    sb.AppendLine("- Units: Imperial — use cups/tbsp/tsp/fl oz for volume, oz/lbs for weight, Fahrenheit for temperatures.");
                    break;
            }

            if (!string.IsNullOrWhiteSpace(profile.AiUnitExceptions))
            {
                sb.AppendLine("- Additional unit preferences:");
                foreach (var line in profile.AiUnitExceptions.Trim()
                             .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    sb.AppendLine($"  - {line}");
            }

            var tools = JsonSerializer.Deserialize<List<string>>(profile.KitchenToolsJson) ?? new();
            if (tools.Any())
                sb.AppendLine($"- Kitchen tools I have: {string.Join(", ", tools)}");

            var diets = JsonSerializer.Deserialize<List<string>>(profile.DietaryPreferencesJson) ?? new();
            if (diets.Any())
                sb.AppendLine($"- Dietary restrictions: {string.Join(", ", diets)}. All recipes MUST comply.");

            sb.AppendLine();
        }

        if (includePantry && pantryItems?.Any() == true)
        {
            sb.AppendLine("## My Current Pantry");
            foreach (var item in pantryItems)
                sb.AppendLine($"- {item.Ingredient.Name}: {item.Amount} {UnitParser.ToDisplayString(item.Unit)}");
            sb.AppendLine();
            sb.AppendLine("Prioritize ingredients I already have when possible.");
            sb.AppendLine();
        }

        sb.AppendLine("## Recipe Format");
        sb.AppendLine();
        sb.AppendLine(_docs.GetFormatPrompt());
        sb.AppendLine();

        sb.AppendLine("## My Request");
        sb.AppendLine(userRequest);

        return sb.ToString();
    }
}
