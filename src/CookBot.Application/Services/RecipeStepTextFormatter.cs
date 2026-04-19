using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CookBot.Application.Services;

/// <summary>
/// Renders recipe step text with <c>[display name](#ingredientId)</c> links as highlighted HTML.
/// </summary>
public static class RecipeStepTextFormatter
{
    private static readonly Regex IngredientLinkPattern = new(
        @"\[([^\]]*)\]\(#(\d+)\)",
        RegexOptions.Compiled);

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

        foreach (Match m in IngredientLinkPattern.Matches(normalized))
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
        return IngredientLinkPattern.Replace(normalized, m => m.Groups[1].Value);
    }

    private static string EncodeWithLineBreaks(ReadOnlySpan<char> span)
    {
        var s = span.ToString();
        return WebUtility.HtmlEncode(s).Replace("\n", "<br />", StringComparison.Ordinal);
    }
}
