using System.Globalization;
using System.Text.RegularExpressions;
using CookBot.Domain.Entities;

namespace CookBot.Application.Services;

/// <summary>
/// Detects timer durations in step text. Phase 3 broadens detection to fractional
/// (e.g. "1 1/2 hours"), range ("20-25 minutes" — persists lowest), and multi-segment
/// ("1 hour 30 minutes") in addition to the legacy simple "N units" pattern.
/// Word-form numbers ("ten minutes") are deferred — see CONTEXT.md deferred ideas.
/// </summary>
public static class TimerDetectionService
{
    // Multi-segment must run first to avoid SimplePattern eating "1 hour" out of "1 hour 30 minutes".
    private static readonly Regex MultiSegmentPattern = new(
        @"(\d+(?:\.\d+)?)\s*(h|hr|hrs|hour|hours)\s+(\d+(?:\.\d+)?)\s*(m|min|mins|minute|minutes)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Range: "20-25 minutes", "20 to 25 minutes", "20–25 minutes" (en dash), "20—25 minutes" (em dash).
    private static readonly Regex RangePattern = new(
        @"(\d+(?:\.\d+)?)\s*(?:-|–|—|to)\s*(\d+(?:\.\d+)?)\s*(minutes?|mins?|hours?|hrs?|seconds?|secs?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Fractional: "1 1/2 hours", "1/2 hour". Decimals fall through to SimplePattern below.
    private static readonly Regex FractionalPattern = new(
        @"(?:(\d+)\s+)?(\d+)\s*/\s*(\d+)\s*(minutes?|mins?|hours?|hrs?|seconds?|secs?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Simple: "25 minutes", "0.5 hours".
    private static readonly Regex SimplePattern = new(
        @"(\d+(?:\.\d+)?)\s*(minutes?|mins?|hours?|hrs?|seconds?|secs?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Returns ALL detected timer substrings (ordered by Start ascending). Public surface used by
    /// <see cref="RecipeStepTextFormatter.ToHtmlWithTimerSuggestions"/>.
    /// </summary>
    public static IReadOnlyList<DetectedTimer> Detect(string? text)
    {
        var results = new List<DetectedTimer>();
        if (string.IsNullOrEmpty(text)) return results;

        var consumed = new bool[text.Length];

        ApplyPattern(MultiSegmentPattern, text, results, consumed, ParseMultiSegmentToSeconds);
        ApplyPattern(RangePattern, text, results, consumed, ParseRangeToSeconds);
        ApplyPattern(FractionalPattern, text, results, consumed, ParseFractionalToSeconds);
        ApplyPattern(SimplePattern, text, results, consumed, ParseSimpleToSeconds);

        return results.OrderBy(d => d.Start).ToList();
    }

    public static List<StepTimer> DetectTimers(string text)
    {
        var detected = Detect(text);
        var timers = new List<StepTimer>();
        foreach (var d in detected)
        {
            var (duration, unit) = SplitToDurationAndUnit(d.TotalSeconds);
            timers.Add(new StepTimer { Duration = duration, Unit = unit });
        }
        return timers;
    }

    private static void ApplyPattern(
        Regex regex, string text,
        List<DetectedTimer> results, bool[] consumed,
        Func<Match, int> toSeconds)
    {
        foreach (Match m in regex.Matches(text))
        {
            if (IsRangeConsumed(consumed, m.Index, m.Length)) continue;
            var seconds = toSeconds(m);
            if (seconds <= 0) continue;
            results.Add(new DetectedTimer(m.Index, m.Length, m.Value, seconds));
            MarkConsumed(consumed, m.Index, m.Length);
        }
    }

    private static bool IsRangeConsumed(bool[] consumed, int start, int length)
    {
        for (int i = start; i < start + length && i < consumed.Length; i++)
            if (consumed[i]) return true;
        return false;
    }

    private static void MarkConsumed(bool[] consumed, int start, int length)
    {
        for (int i = start; i < start + length && i < consumed.Length; i++)
            consumed[i] = true;
    }

    private static int ParseMultiSegmentToSeconds(Match m)
    {
        var hours = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var minutes = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        return (int)(hours * 3600 + minutes * 60);
    }

    private static int ParseRangeToSeconds(Match m)
    {
        // Range persists as the LOWEST bound (RESEARCH.md Item 6 / Assumption A4).
        var lower = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var unit = m.Groups[3].Value.ToLowerInvariant();
        return UnitsToSeconds(lower, unit);
    }

    private static int ParseFractionalToSeconds(Match m)
    {
        var whole = m.Groups[1].Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        var num = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var den = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        if (den == 0) return 0;
        var totalUnits = whole + (double)num / den;
        var unit = m.Groups[4].Value.ToLowerInvariant();
        return UnitsToSeconds(totalUnits, unit);
    }

    private static int ParseSimpleToSeconds(Match m)
    {
        var n = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var unit = m.Groups[2].Value.ToLowerInvariant();
        return UnitsToSeconds(n, unit);
    }

    private static int UnitsToSeconds(double n, string unitLower)
    {
        if (unitLower.StartsWith("hr") || unitLower.StartsWith("hour")) return (int)(n * 3600);
        if (unitLower.StartsWith("sec")) return (int)n;
        return (int)(n * 60); // minutes default
    }

    private static (int Duration, string Unit) SplitToDurationAndUnit(int seconds)
    {
        // Pick the most natural unit. Prefer hr if exact multiple of 3600, then min, else sec.
        if (seconds >= 3600 && seconds % 3600 == 0) return (seconds / 3600, "hr");
        if (seconds >= 60 && seconds % 60 == 0) return (seconds / 60, "min");
        if (seconds % 60 == 0) return (seconds / 60, "min");
        return (seconds, "sec");
    }

    public sealed record DetectedTimer(int Start, int Length, string Substring, int TotalSeconds);
}
