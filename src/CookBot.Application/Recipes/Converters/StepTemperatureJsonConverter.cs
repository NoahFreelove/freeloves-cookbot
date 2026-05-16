using System.Text.Json;
using System.Text.Json.Serialization;
using CookBot.Domain.Recipes;

namespace CookBot.Application.Recipes.Converters;

/// <summary>
/// Custom <see cref="JsonConverter{T}"/> for <see cref="StepTemperature"/> used in
/// <c>SerializeIndented</c> export output only. Renders gas half-stops (e.g. 4.5, 7.5)
/// as the human-readable Unicode string <c>"4½"</c>. Whole-degree gas and all F/C values
/// are emitted as the standard <c>{ "value": N, "unit": "X" }</c> object per D-27.
///
/// The compact wire format (<c>Serialize</c> for the DB column) always uses the standard
/// object form — this converter is NOT added to the compact <c>JsonSerializerOptions</c>.
/// </summary>
public sealed class StepTemperatureJsonConverter : JsonConverter<StepTemperature>
{
    public override StepTemperature Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // String form: "4½", "7½", etc. — only Gas units use this form (D-27).
        // Defensive: a user editing the indented JSON might keep the string form.
        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString() ?? string.Empty;

            // Strip the optional trailing ½ glyph (U+00BD) and parse the numeric prefix.
            bool hasHalf = raw.EndsWith('½');
            var numericPart = hasHalf ? raw[..^1] : raw;

            if (!decimal.TryParse(numericPart, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var numericValue))
            {
                throw new JsonException($"Cannot parse StepTemperature string '{raw}' — expected format like '4½' or '7'.");
            }

            var value = numericValue + (hasHalf ? 0.5m : 0m);
            return new StepTemperature { Value = value, Unit = TemperatureUnit.Gas };
        }

        // Object form: { "value": N, "unit": "X" } — read manually to avoid recursion.
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var v = doc.RootElement.GetProperty("value").GetDecimal();
            var unitStr = doc.RootElement.GetProperty("unit").GetString() ?? "F";
            var u = Enum.Parse<TemperatureUnit>(unitStr, ignoreCase: true);
            return new StepTemperature { Value = v, Unit = u };
        }

        throw new JsonException($"Unexpected token type {reader.TokenType} when deserializing StepTemperature.");
    }

    public override void Write(Utf8JsonWriter writer, StepTemperature value, JsonSerializerOptions options)
    {
        // Gas half-stops render as human-readable string "4½", "7½", etc. (D-27 export shape).
        if (value.Unit == TemperatureUnit.Gas && value.Value % 1m != 0m)
        {
            var humanReadable = ((int)value.Value).ToString() + "½";
            writer.WriteStringValue(humanReadable);
            return;
        }

        // All other cases: standard { "value": N, "unit": "X" } object with lowercase unit.
        writer.WriteStartObject();
        writer.WriteNumber("value", value.Value);
        writer.WriteString("unit", value.Unit.ToString().ToLowerInvariant());
        writer.WriteEndObject();
    }
}
