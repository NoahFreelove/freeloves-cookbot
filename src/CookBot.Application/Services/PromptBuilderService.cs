using System.Text;
using System.Text.Json;
using CookBot.Domain.Entities;
using CookBot.Domain.Enums;

namespace CookBot.Application.Services;

public class PromptBuilderService
{
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

    public string BuildSystemPrompt(UserProfile profile, IEnumerable<PantryItem>? pantryItems = null)
    {
        return ResolveTemplate(DefaultTemplate, profile, pantryItems);
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

    /// <summary>System prompt for in-flow cooking step assistance (concise, step-focused).</summary>
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
                sb.AppendLine("Use Fahrenheit for oven temperatures.");
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

    private string ResolveRecipeFormat()
    {
        return @"IMPORTANT: When providing a recipe, ALWAYS use this exact format so it can be parsed and saved:
```recipe
---
name: ""Recipe Name""
servings: 4
prepTime: 15
cookTime: 30
tags: [tag1, tag2]
ingredients:
  - id: 1
    name: ""ingredient name""
    amount: 2
    unit: ""cups""
  - id: 2
    name: ""another ingredient""
    amount: 1
    unit: ""tbsp""
    note: ""optional note""
steps:
  - text: ""Step instruction with [ingredient name](#1).""
  - section: ""Section header""
  - text: ""Another step, bake for 25 minutes.""
---
```

Use [ingredient name](#id) links in step text to reference ingredients by their ID.
If you can't follow this exact format, plain numbered steps are fine — the app will parse them.";
    }

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
                    sb.AppendLine("- Units: Canadian-style — use cups/tbsp/tsp for volume, grams/kg for weight. NEVER use ounces or pounds. Use Fahrenheit for oven temps.");
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
        sb.AppendLine("When providing a recipe, please use this exact format so I can import it into my recipe manager:");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine("---");
        sb.AppendLine("name: \"Recipe Name\"");
        sb.AppendLine("servings: 4");
        sb.AppendLine("prepTime: 15");
        sb.AppendLine("cookTime: 30");
        sb.AppendLine("tags: [tag1, tag2]");
        sb.AppendLine("ingredients:");
        sb.AppendLine("  - id: 1");
        sb.AppendLine("    name: \"ingredient name\"");
        sb.AppendLine("    amount: 2");
        sb.AppendLine("    unit: \"cups\"");
        sb.AppendLine("  - id: 2");
        sb.AppendLine("    name: \"another ingredient\"");
        sb.AppendLine("    amount: 1");
        sb.AppendLine("    unit: \"tbsp\"");
        sb.AppendLine("    note: \"optional note\"");
        sb.AppendLine("steps:");
        sb.AppendLine("  - text: \"Step instruction with [ingredient name](#1).\"");
        sb.AppendLine("  - section: \"Section header\"");
        sb.AppendLine("  - text: \"Another step, bake for 25 minutes.\"");
        sb.AppendLine("---");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Each ingredient has a unique `id`. Use `[display name](#id)` links in step text to reference ingredients.");
        sb.AppendLine("If you can't follow this exact format, plain numbered steps are fine — the app will parse them.");
        sb.AppendLine();

        sb.AppendLine("## My Request");
        sb.AppendLine(userRequest);

        return sb.ToString();
    }
}
