namespace CookBot.Application.Recipes;

/// <summary>
/// Converts an optional integer minute count to an ISO-8601 duration string (PT#H#M).
/// Returns null when the input is null or non-positive so the caller can omit the JSON-LD
/// property entirely (schema.org Recipe prepTime/cookTime/totalTime).
/// </summary>
public static class Iso8601DurationFormatter
{
    /// <summary>
    /// Converts <paramref name="minutes"/> to an ISO-8601 PT#H#M string, or null when
    /// <paramref name="minutes"/> is null or &lt;= 0.
    /// </summary>
    /// <remarks>
    /// Hours are intentionally NOT rolled into days. 1500 minutes emits "PT25H" rather than
    /// "P1DT1H". ISO-8601 and schema.org consumers (including Google Rich Results) accept
    /// PT##H for values over 24h. The date-duration form (P#DT#H) would require knowledge of
    /// calendar days and is not more correct for cooking times.
    /// </remarks>
    /// <example>
    /// 30   → "PT30M"
    /// 60   → "PT1H"
    /// 90   → "PT1H30M"
    /// 125  → "PT2H5M"
    /// 1500 → "PT25H"
    /// </example>
    public static string? ToIso8601Duration(int? minutes)
    {
        if (minutes is null or <= 0) return null;

        int h = minutes.Value / 60;
        int m = minutes.Value % 60;

        var sb = new System.Text.StringBuilder("PT");
        if (h > 0) sb.Append(h).Append('H');
        if (m > 0) sb.Append(m).Append('M');
        return sb.ToString();
    }
}
