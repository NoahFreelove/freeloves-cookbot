using System.Text;
using System.Text.Json;
using CookBot.Domain.Entities;
using CookBot.Domain.Enums;

namespace CookBot.Application.Services;

public class PromptBuilderService
{
    public string BuildSystemPrompt(UserProfile profile, IEnumerable<PantryItem>? pantryItems = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are CookBot, an expert AI cooking assistant. You help users discover, create, and refine recipes.");
        sb.AppendLine();

        // Experience level adaptation
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
        sb.AppendLine();

        // Kitchen tools
        var tools = JsonSerializer.Deserialize<List<string>>(profile.KitchenToolsJson) ?? new();
        if (tools.Any())
        {
            sb.AppendLine($"Available kitchen tools: {string.Join(", ", tools)}.");
            sb.AppendLine("Only suggest recipes the user can make with their available equipment.");
        }

        // Dietary preferences
        var diets = JsonSerializer.Deserialize<List<string>>(profile.DietaryPreferencesJson) ?? new();
        if (diets.Any())
        {
            sb.AppendLine($"Dietary preferences/restrictions: {string.Join(", ", diets)}.");
            sb.AppendLine("All recipe suggestions MUST comply with these dietary requirements.");
        }

        // Pantry inventory
        if (pantryItems?.Any() == true)
        {
            sb.AppendLine();
            sb.AppendLine("Current pantry inventory:");
            foreach (var item in pantryItems)
            {
                sb.AppendLine($"  - {item.Ingredient.Name}: {item.Amount} {UnitParser.ToDisplayString(item.Unit)}");
            }
            sb.AppendLine("When possible, prioritize recipes using ingredients the user already has.");
        }

        sb.AppendLine();
        sb.AppendLine("IMPORTANT: When providing a recipe, ALWAYS use this exact format so it can be parsed and saved:");
        sb.AppendLine(@"```recipe
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
---

## Instructions

1. Step one using [ingredient name](#1)
2. Step two with [another ingredient](#2)
```");
        sb.AppendLine();
        sb.AppendLine("Use [ingredient name](#id) links in instructions to reference ingredients by their ID.");

        return sb.ToString();
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
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Instructions");
        sb.AppendLine();
        sb.AppendLine("1. Step one using [ingredient name](#1)");
        sb.AppendLine("2. Step two with [another ingredient](#2)");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("The YAML frontmatter between `---` defines the recipe metadata and ingredients. Each ingredient has a unique `id`.");
        sb.AppendLine("In the instructions, reference ingredients using `[display name](#id)` links.");
        sb.AppendLine();

        sb.AppendLine("## My Request");
        sb.AppendLine(userRequest);

        return sb.ToString();
    }
}
