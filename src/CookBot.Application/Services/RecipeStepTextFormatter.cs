using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CookBot.Application.Recipes;

namespace CookBot.Application.Services;

/// <summary>
/// Renders recipe step text with <c>[display name](#ingredientId)</c> links as highlighted HTML.
/// </summary>
public static class RecipeStepTextFormatter
{
    /// <summary>
    /// Converts step text to HTML-safe markup with ingredient references wrapped in
    /// <c>&lt;span class="ingredient-ref"&gt;</c>.
    /// </summary>
    public static string ToHtml(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        var sb = new StringBuilder();
        var last = 0;

        foreach (Match m in IngredientLinkPatterns.Pattern.Matches(normalized))
        {
            if (m.Index > last)
                sb.Append(EncodeWithLineBreaks(normalized.AsSpan(last, m.Index - last)));

            var display = WebUtility.HtmlEncode(m.Groups[1].Value);
            var id = WebUtility.HtmlEncode(m.Groups[2].Value);
            sb.Append("<span class=\"ingredient-ref\" data-ingredient-id=\"")
                .Append(id)
                .Append("\">")
                .Append(display)
                .Append("</span>");
            last = m.Index + m.Length;
        }

        if (last < normalized.Length)
            sb.Append(EncodeWithLineBreaks(normalized.AsSpan(last)));

        return sb.ToString();
    }

    /// <summary>Plain text for print/PDF: strips ingredient link markup to the visible label.</summary>
    public static string ToPlainText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        return IngredientLinkPatterns.Pattern.Replace(normalized, m => m.Groups[1].Value);
    }

    /// <summary>
    /// Phase 3 D-C1 / D-C3: Renders step text as HTML with detected timer substrings wrapped in
    /// <c>&lt;span class="timer-suggestion" data-duration-seconds="..."&gt;</c> for the click-to-convert
    /// popover. Skips substrings whose computed duration is in <paramref name="alreadyConvertedDurationsSeconds"/>
    /// (so a duration already promoted to an explicit timer chip is NOT re-suggested). Substrings that overlap
    /// an existing <c>[name](#id)</c> ingredient link are NOT wrapped (avoids double-wrapping inside chips).
    /// </summary>
    public static string ToHtmlWithTimerSuggestions(string? text, IReadOnlySet<int> alreadyConvertedDurationsSeconds)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Two-pass strategy with Unicode-bracket sentinels (⟦TS:N⟧…⟦/TS⟧, U+27E6 / U+27E7).
        // Pass 1 marks the original text BEFORE HTML encoding. The sentinels survive HTML
        // encoding unchanged (they aren't <>&"'), so the post-pass regex can find them and
        // emit literal <span> markup whose inner text has already been HTML-encoded by ToHtml.
        var wrappedSource = WrapTimerSuggestionsWithSentinels(text, alreadyConvertedDurationsSeconds);
        var html = ToHtml(wrappedSource);

        return SentinelToSpanPattern.Replace(
            html,
            m => $"<span class=\"timer-suggestion\" data-duration-seconds=\"{m.Groups[1].Value}\">{m.Groups[2].Value}</span>");
    }

    private static readonly Regex SentinelToSpanPattern = new(
        @"⟦TS:(\d+)⟧(.*?)⟦/TS⟧",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static string WrapTimerSuggestionsWithSentinels(string text, IReadOnlySet<int> alreadyConvertedDurationsSeconds)
    {
        var detected = TimerDetectionService.Detect(text);
        if (detected.Count == 0) return text;

        var sb = new StringBuilder();
        var last = 0;
        foreach (var d in detected)
        {
            if (alreadyConvertedDurationsSeconds.Contains(d.TotalSeconds)) continue;
            // Avoid wrapping inside an existing [name](#id) ingredient link range — that would corrupt
            // chip rendering (timer suggestion span nested inside ingredient-ref span).
            if (OverlapsIngredientLink(text, d.Start, d.Length)) continue;

            sb.Append(text, last, d.Start - last);
            sb.Append("⟦TS:").Append(d.TotalSeconds).Append("⟧")
              .Append(d.Substring)
              .Append("⟦/TS⟧");
            last = d.Start + d.Length;
        }
        sb.Append(text, last, text.Length - last);
        return sb.ToString();
    }

    private static bool OverlapsIngredientLink(string text, int start, int length)
    {
        foreach (Match m in IngredientLinkPatterns.Pattern.Matches(text))
        {
            // Half-open interval intersection.
            if (start < m.Index + m.Length && m.Index < start + length) return true;
        }
        return false;
    }

    private static string EncodeWithLineBreaks(ReadOnlySpan<char> span)
    {
        var s = span.ToString();
        return WebUtility.HtmlEncode(s).Replace("\n", "<br />", StringComparison.Ordinal);
    }
}
