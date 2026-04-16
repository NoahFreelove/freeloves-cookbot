using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CookBot.Domain.Entities;
using CookBot.Domain.Enums;
using CookBot.Domain.Interfaces;

namespace CookBot.Application.Services;

public sealed record PantryAiImportRow(string IngredientName, double Amount, string Unit, string? Expiration);

/// <summary>
/// Unit used when the user does not track a quantity or measure (always stocked, presence-only).
/// </summary>
public static class PantryAiImport
{
    public const string UnmeasuredUnit = "staple";
}

public sealed class PantryAiPopulationResult
{
    public bool Success { get; init; }
    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
    public string? Error { get; init; }

    public static PantryAiPopulationResult Failed(string error) => new() { Success = false, Error = error };

    public static PantryAiPopulationResult Ok(IReadOnlyList<string> messages) => new()
    {
        Success = true,
        Messages = messages,
    };
}

/// <summary>
/// Calls the configured AI with a strict JSON pantry schema, then merges rows into a pantry.
/// </summary>
public class PantryAiPopulationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IAiService _ai;
    private readonly IRepository<Ingredient> _ingredients;
    private readonly PantryService _pantry;

    public PantryAiPopulationService(
        IAiService ai,
        IRepository<Ingredient> ingredients,
        PantryService pantry)
    {
        _ai = ai;
        _ingredients = ingredients;
        _pantry = pantry;
    }

    /// <summary>
    /// Builds the system prompt that defines the required JSON format (included on every request).
    /// </summary>
    public static string BuildSystemPrompt(UnitSystem? unitSystem = null, string? aiUnitExceptions = null)
    {
        var unitHint = unitSystem switch
        {
            UnitSystem.Metric => "Prefer metric units (g, kg, ml, L, °C context) when ambiguous.",
            UnitSystem.Imperial => "Prefer US customary units (cup, tbsp, oz, lb) when ambiguous.",
            UnitSystem.Canadian => "Prefer Canadian common cooking units (cups, ml, g, lb) when ambiguous.",
            _ => "Use sensible home-cooking units (cup, tbsp, g, ml, piece, etc.).",
        };

        var unitExtra = FormatAiUnitExceptionsForPantryPrompt(aiUnitExceptions);

        return $"""
            You help maintain a home cooking pantry database. The user describes what they have in natural language.

            {unitHint}{unitExtra}

            CRITICAL output rules: Your entire reply must be a single JSON array only. The first non-whitespace character must be '['. No title, preamble, or markdown fences. No text after the closing ']'.

            Each array element is one object. Required: "ingredientName" (string). Optional keys (omit or use null):
            - "amount" (number or null): positive quantity when they track an amount; omit or null for "always have it / don't track quantity" — stored as a single presence-only line
            - "unit" (string or null): unit for amount (e.g. "cup", "ml", "g", "lb", "piece"); omit or null when not tracking a measure — stored with unit "{PantryAiImport.UnmeasuredUnit}"
            - "expiration" (string or null): ISO date "YYYY-MM-DD" if they gave or implied one; omit or null if unknown or non-perishable; you may use the string "none" for explicitly non-expiring staples

            Rules:
            - Only include ingredients the user clearly has on hand. If they say they ran out of something, omit it.
            - When they give a real amount, set both amount and unit. When they only say they always stock something without a measure, omit amount and unit (or set both to null).
            - If quantity is vague but they still think in amounts ("some flour"), pick a reasonable amount and unit.
            - Use one row per distinct ingredient; combine duplicates in your reasoning into a single row.
            - If the user gives no usable items, respond with an empty array [].
            """;
    }

    /// <summary>
    /// System prompt for normalizing an existing pantry snapshot into the same JSON schema as <see cref="BuildSystemPrompt"/>.
    /// </summary>
    public static string BuildStandardizeSystemPrompt(UnitSystem? unitSystem = null, string? aiUnitExceptions = null)
    {
        var unitHint = unitSystem switch
        {
            UnitSystem.Metric => "Prefer metric units (g, kg, ml, L) when consolidating rows.",
            UnitSystem.Imperial => "Prefer US customary units (cup, tbsp, oz, lb) when consolidating rows.",
            UnitSystem.Canadian => "Prefer Canadian common cooking units when consolidating rows.",
            _ => "Use sensible home-cooking units when consolidating rows.",
        };

        var unitExtra = FormatAiUnitExceptionsForPantryPrompt(aiUnitExceptions);

        return $"""
            You normalize a home pantry inventory. The user message lists current database rows (ingredient label, amount, unit, expiration). Those rows may use inconsistent spelling, duplicate the same real ingredient, or use awkward units.

            {unitHint}{unitExtra}

            Your job:
            - Produce ONE JSON array only (same schema as pantry import). The first non-whitespace character in your reply must be '['. No preamble, no markdown, no commentary before or after the array.
            - Merge rows that are clearly the same ingredient (synonyms, typos, plural/singular). Combine amounts when units can reasonably be treated as the same measurable ingredient; convert to one canonical unit.
            - For "staple" / presence-only rows (unit {PantryAiImport.UnmeasuredUnit} or no real measure), keep a single staple row unless the user data clearly has both a measured amount and a separate duplicate label for the same item — then prefer one measured row if the data supports it.
            - Use clear, canonical ingredient names (title case or natural English) suitable for a shopping list.
            - Expiration: when merging rows with different dates, use the soonest future date; if one row has no date, keep the other; use null or "none" when appropriate for non-perishables.
            - Every distinct ingredient in the final pantry must appear exactly once.
            - If the input list is empty, return [].

            Schema (same as import): each element has required "ingredientName" (string). Optional: "amount" (number or null), "unit" (string or null), "expiration" (string YYYY-MM-DD, null, or tokens like none/never for non-expiring).

            Rules for amounts: measured ingredients need positive amount and a concrete unit other than "{PantryAiImport.UnmeasuredUnit}". Always-stocked items omit amount and unit (or null) — stored as unit "{PantryAiImport.UnmeasuredUnit}".
            """;
    }

    private static string FormatAiUnitExceptionsForPantryPrompt(string? aiUnitExceptions)
    {
        if (string.IsNullOrWhiteSpace(aiUnitExceptions))
            return "";
        return $"""

            User-specific unit exceptions (always respect when choosing units and amounts):
            {aiUnitExceptions.Trim()}
            """;
    }

    /// <summary>
    /// Builds the user message sent to the model for standardization (plain text snapshot of current rows).
    /// </summary>
    public static string BuildStandardizeUserMessage(IReadOnlyList<PantryItem> items)
    {
        if (items.Count == 0)
            return "The pantry is empty.";

        var sb = new StringBuilder();
        sb.AppendLine("Normalize this pantry. One row per line. Preserve totals and expirations as described in your instructions.");
        foreach (var item in items
                     .OrderBy(i => i.Ingredient?.Name ?? "")
                     .ThenBy(i => i.Id))
        {
            var name = item.Ingredient?.Name?.Trim() ?? $"ingredient_id:{item.IngredientId}";
            string unitLabel;
            string amountLabel;
            if (string.Equals(item.Unit, PantryAiImport.UnmeasuredUnit, StringComparison.OrdinalIgnoreCase))
            {
                unitLabel = PantryAiImport.UnmeasuredUnit;
                amountLabel = "(in stock, not measured)";
            }
            else
            {
                unitLabel = item.Unit;
                amountLabel = item.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            var exp = item.ExpirationDate.HasValue
                ? item.ExpirationDate.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                : "none";
            sb.AppendLine($"- {name} | amount: {amountLabel} | unit: {unitLabel} | expiration: {exp}");
        }

        return sb.ToString().TrimEnd();
    }

    private static readonly string[] JsonObjectArrayPropertyHints =
    {
        "items", "ingredients", "pantry", "data", "results", "rows", "list", "entries",
        "output", "inventory", "normalized", "standardized", "products",
    };

    /// <summary>
    /// Pulls a pantry JSON array from model output: raw <c>[...]</c>, markdown fences, or a JSON object that wraps the array
    /// (e.g. <c>{"items":[...]}</c>). Handles preambles, balanced brackets inside strings, and nested wrappers.
    /// </summary>
    public static string? ExtractJsonArray(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = NormalizeJsonishText(raw);
        t = StripAngleBracketSections(t);

        if (TryExtractPantryJsonArray(t, out var array) && array != null)
            return array;

        foreach (var block in EnumerateMarkdownCodeBlocks(t))
        {
            var inner = NormalizeJsonishText(block);
            if (TryExtractPantryJsonArray(inner, out array) && array != null)
                return array;
        }

        return null;
    }

    private static string NormalizeJsonishText(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var t = s.Trim().TrimStart('\uFEFF');
        return t.Replace('\uFF3B', '[').Replace('\uFF3D', ']')
            .Replace('\u201C', '"').Replace('\u201D', '"');
    }

    private static string StripAngleBracketSections(string t)
    {
        if (string.IsNullOrEmpty(t)) return t;
        t = Regex.Replace(t, @"<thinking\b[^>]*>[\s\S]*?</thinking>", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        t = Regex.Replace(t, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return t;
    }

    private static bool TryExtractPantryJsonArray(string t, out string? arrayJson)
    {
        arrayJson = TryExtractBalancedJsonArray(t);
        if (arrayJson != null)
            return true;

        arrayJson = TryExtractArrayFromJsonObjects(t);
        return arrayJson != null;
    }

    private static string? TryExtractArrayFromJsonObjects(string t)
    {
        var trimmed = t.Trim();
        if (TryParseJsonRootForPantryArray(trimmed, out var fromRoot))
            return fromRoot;

        for (var i = 0; i < t.Length; i++)
        {
            if (t[i] != '{') continue;
            var end = FindMatchingJsonBraceClose(t, i);
            if (end < 0) continue;
            var slice = t[i..(end + 1)];
            if (TryParseJsonRootForPantryArray(slice, out var found))
                return found;
        }

        return null;
    }

    private static bool TryParseJsonRootForPantryArray(string json, out string? arrayJson)
    {
        arrayJson = null;
        json = NormalizeJsonishText(json);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var extracted = TryGetPantryArrayFromElement(doc.RootElement);
            if (extracted != null)
            {
                arrayJson = extracted;
                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    private static string? TryGetPantryArrayFromElement(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Array)
            return el.GetRawText();

        if (el.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var hint in JsonObjectArrayPropertyHints)
        {
            foreach (var prop in el.EnumerateObject())
            {
                if (!prop.Name.Equals(hint, StringComparison.OrdinalIgnoreCase)) continue;
                if (prop.Value.ValueKind == JsonValueKind.Array)
                    return prop.Value.GetRawText();
            }
        }

        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array) continue;
            if (LooksLikePantryImportArray(prop.Value))
                return prop.Value.GetRawText();
        }

        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array)
                return prop.Value.GetRawText();
        }

        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Object) continue;
            var nested = TryGetPantryArrayFromElement(prop.Value);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static bool LooksLikePantryImportArray(JsonElement arr)
    {
        if (arr.ValueKind != JsonValueKind.Array) return false;
        if (arr.GetArrayLength() == 0) return true;
        var first = arr[0];
        if (first.ValueKind != JsonValueKind.Object) return false;
        return first.TryGetProperty("ingredientName", out _)
               || first.TryGetProperty("ingredient_name", out _);
    }

    private static int FindMatchingJsonBraceClose(string t, int start)
    {
        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = start; i < t.Length; i++)
        {
            var c = t[i];
            if (inString)
            {
                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\')
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                    inString = false;

                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    private static IEnumerable<string> EnumerateMarkdownCodeBlocks(string t)
    {
        var searchFrom = 0;
        while (true)
        {
            var open = t.IndexOf("```", searchFrom, StringComparison.Ordinal);
            if (open < 0)
                yield break;

            var afterTicks = open + 3;
            var contentStart = afterTicks;
            if (contentStart < t.Length)
            {
                var lineBreak = t.AsSpan(contentStart).IndexOfAny("\r\n".AsSpan());
                if (lineBreak >= 0)
                {
                    var nlAt = contentStart + lineBreak;
                    contentStart = nlAt + 1;
                    if (nlAt < t.Length && t[nlAt] == '\r' && contentStart < t.Length && t[contentStart] == '\n')
                        contentStart++;
                }
            }

            var close = t.IndexOf("```", contentStart, StringComparison.Ordinal);
            if (close < 0)
                yield break;

            yield return t[contentStart..close];
            searchFrom = close + 3;
        }
    }

    private static string? TryExtractBalancedJsonArray(string t)
    {
        var start = t.IndexOf('[');
        if (start < 0)
            return null;

        var end = FindMatchingJsonArrayClose(t, start);
        if (end < 0)
            return null;

        return t[start..(end + 1)];
    }

    /// <summary>Finds the ']' that closes the array opened at <paramref name="start"/> (depth 0), respecting JSON string rules.</summary>
    private static int FindMatchingJsonArrayClose(string t, int start)
    {
        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = start; i < t.Length; i++)
        {
            var c = t[i];
            if (inString)
            {
                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\')
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                    inString = false;

                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '[')
                depth++;
            else if (c == ']')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    /// <summary>Maps common alternate keys so System.Text.Json can bind to <see cref="PantryAiImportDto"/>.</summary>
    private static string NormalizePantryImportPropertyNames(string jsonArray) =>
        jsonArray.Replace("\"ingredient_name\"", "\"ingredientName\"", StringComparison.Ordinal);

    public static bool TryDeserializeRows(string jsonArray, out List<PantryAiImportRow> rows, out string? error)
    {
        rows = new();
        error = null;
        try
        {
            jsonArray = NormalizePantryImportPropertyNames(NormalizeJsonishText(jsonArray));
            var list = JsonSerializer.Deserialize<List<PantryAiImportDto>>(jsonArray, JsonOptions);
            if (list == null)
            {
                error = "JSON parsed to null.";
                return false;
            }

            foreach (var dto in list)
            {
                if (string.IsNullOrWhiteSpace(dto.IngredientName))
                {
                    error = "Invalid row: ingredientName is required.";
                    return false;
                }

                var name = dto.IngredientName.Trim();
                var unitRaw = dto.Unit?.Trim();
                var isStaple = string.IsNullOrEmpty(unitRaw)
                    || unitRaw.Equals(PantryAiImport.UnmeasuredUnit, StringComparison.OrdinalIgnoreCase);

                double amount;
                string unit;
                if (isStaple)
                {
                    unit = PantryAiImport.UnmeasuredUnit;
                    amount = 1d;
                }
                else
                {
                    unit = unitRaw!;
                    amount = dto.Amount ?? 0;
                    if (amount <= 0)
                    {
                        error = $"Invalid row: '{name}' has unit '{unit}' but needs a positive amount (or use null unit for always-stocked items).";
                        return false;
                    }
                }

                rows.Add(new PantryAiImportRow(name, amount, unit, dto.Expiration));
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public async Task<PantryAiPopulationResult> PopulatePantryAsync(
        int pantryId,
        string userNaturalLanguage,
        string apiKey,
        string? modelId,
        UnitSystem unitSystem,
        string? aiUnitExceptions = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userNaturalLanguage))
            return PantryAiPopulationResult.Failed("Describe what you have in your pantry.");

        var system = BuildSystemPrompt(unitSystem, aiUnitExceptions);
        var messages = new List<AiMessage>
        {
            new() { Role = "user", Content = userNaturalLanguage.Trim() },
        };

        string raw;
        try
        {
            raw = await _ai.SendMessageAsync(system, messages, apiKey, modelId).WaitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return PantryAiPopulationResult.Failed(ex.Message);
        }

        var json = ExtractJsonArray(raw);
        if (json == null)
            return PantryAiPopulationResult.Failed("Could not find a JSON array in the assistant reply. Try again with a simpler list.");

        if (!TryDeserializeRows(json, out var dtos, out var parseErr))
            return PantryAiPopulationResult.Failed($"Invalid JSON: {parseErr}");

        var summaries = new List<string>();
        foreach (var dto in dtos)
        {
            DateTime? exp = null;
            if (!string.IsNullOrWhiteSpace(dto.Expiration))
            {
                var expStr = dto.Expiration.Trim();
                if (IsNonExpiringToken(expStr))
                    exp = null;
                else if (!DateTime.TryParse(expStr, System.Globalization.CultureInfo.InvariantCulture,
                             System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                             out var expDt))
                {
                    return PantryAiPopulationResult.Failed($"Bad expiration date for '{dto.IngredientName}': '{dto.Expiration}' (use YYYY-MM-DD, null, or non-expiring tokens like none/never).");
                }
                else
                    exp = expDt;
            }

            var ing = await ResolveIngredientAsync(dto.IngredientName);
            await _pantry.AddOrUpdateAsync(pantryId, ing.Id, dto.Amount, dto.Unit, exp);
            var qtyLabel = dto.Unit.Equals(PantryAiImport.UnmeasuredUnit, StringComparison.OrdinalIgnoreCase)
                ? "in stock (not measured)"
                : $"{dto.Amount} {dto.Unit}";
            summaries.Add($"{ing.Name}: {qtyLabel}" + (exp.HasValue ? $" (exp {exp:yyyy-MM-dd})" : ""));
        }

        if (summaries.Count == 0)
            return PantryAiPopulationResult.Ok(new[] { "No items to add — the model returned an empty list." });

        return PantryAiPopulationResult.Ok(summaries);
    }

    /// <summary>
    /// Replaces all rows in a pantry with an AI-normalized list (deduplicated names, consolidated amounts).
    /// </summary>
    public async Task<PantryAiPopulationResult> StandardizePantryAsync(
        int pantryId,
        IReadOnlyList<PantryItem> currentItems,
        string apiKey,
        string? modelId,
        UnitSystem unitSystem,
        string? aiUnitExceptions = null,
        CancellationToken cancellationToken = default)
    {
        if (currentItems.Count == 0)
            return PantryAiPopulationResult.Failed("This pantry is empty — nothing to standardize.");

        var system = BuildStandardizeSystemPrompt(unitSystem, aiUnitExceptions);
        var userContent = BuildStandardizeUserMessage(currentItems);
        var messages = new List<AiMessage>
        {
            new() { Role = "user", Content = userContent },
        };

        string raw;
        try
        {
            raw = await _ai.SendMessageAsync(system, messages, apiKey, modelId).WaitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return PantryAiPopulationResult.Failed(ex.Message);
        }

        var json = ExtractJsonArray(raw);
        if (json == null)
            return PantryAiPopulationResult.Failed("Could not find a JSON array in the assistant reply. Try again.");

        if (!TryDeserializeRows(json, out var dtos, out var parseErr))
            return PantryAiPopulationResult.Failed($"Invalid JSON: {parseErr}");

        if (dtos.Count == 0)
            return PantryAiPopulationResult.Failed("The model returned an empty list. Pantry was not changed.");

        await _pantry.ClearPantryAsync(pantryId);

        var summaries = new List<string>();
        foreach (var dto in dtos)
        {
            DateTime? exp = null;
            if (!string.IsNullOrWhiteSpace(dto.Expiration))
            {
                var expStr = dto.Expiration.Trim();
                if (IsNonExpiringToken(expStr))
                    exp = null;
                else if (!DateTime.TryParse(expStr, System.Globalization.CultureInfo.InvariantCulture,
                             System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                             out var expDt))
                {
                    return PantryAiPopulationResult.Failed($"Bad expiration date for '{dto.IngredientName}': '{dto.Expiration}' (use YYYY-MM-DD, null, or non-expiring tokens like none/never).");
                }
                else
                    exp = expDt;
            }

            var ing = await ResolveIngredientAsync(dto.IngredientName);
            await _pantry.AddOrUpdateAsync(pantryId, ing.Id, dto.Amount, dto.Unit, exp);
            var qtyLabel = dto.Unit.Equals(PantryAiImport.UnmeasuredUnit, StringComparison.OrdinalIgnoreCase)
                ? "in stock (not measured)"
                : $"{dto.Amount} {dto.Unit}";
            summaries.Add($"{ing.Name}: {qtyLabel}" + (exp.HasValue ? $" (exp {exp:yyyy-MM-dd})" : ""));
        }

        return PantryAiPopulationResult.Ok(summaries);
    }

    private async Task<Ingredient> ResolveIngredientAsync(string name)
    {
        var normalized = IngredientResolver.Normalize(name);
        var existing = await _ingredients.FindAsync(i => i.NormalizedName == normalized);
        if (existing.Count > 0)
            return existing[0];

        return await _ingredients.AddAsync(new Ingredient
        {
            Name = name.Trim(),
            NormalizedName = normalized,
        });
    }

    private static bool IsNonExpiringToken(string value) =>
        value.Equals("none", StringComparison.OrdinalIgnoreCase)
        || value.Equals("never", StringComparison.OrdinalIgnoreCase)
        || value.Equals("non-expiring", StringComparison.OrdinalIgnoreCase)
        || value.Equals("na", StringComparison.OrdinalIgnoreCase)
        || value.Equals("n/a", StringComparison.OrdinalIgnoreCase);

    private sealed class PantryAiImportDto
    {
        [JsonPropertyName("ingredientName")]
        public string IngredientName { get; set; } = "";

        [JsonPropertyName("amount")]
        public double? Amount { get; set; }

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }

        [JsonPropertyName("expiration")]
        public string? Expiration { get; set; }
    }
}
