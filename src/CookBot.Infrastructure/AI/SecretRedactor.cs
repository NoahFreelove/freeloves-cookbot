using System.Text.RegularExpressions;

namespace CookBot.Infrastructure.AI;

/// <summary>
/// AI-07 chokepoint. Strips API-key patterns and header values from any string before
/// it surfaces to the UI, log sinks, or telemetry. Called by every catch site in
/// <see cref="AnthropicAiService"/>. Pure static — no DI, no state, no I/O.
/// </summary>
public static class SecretRedactor
{
    // Anthropic API-key shape: sk-ant- followed by alphanumeric / dash / underscore.
    private static readonly Regex ApiKeyPattern =
        new(@"sk-ant-[A-Za-z0-9_\-]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // x-api-key or authorization header — name + delimiter + non-whitespace value.
    private static readonly Regex HeaderPattern =
        new(@"(?i)(x-api-key|authorization)\s*[:=]\s*\S+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Strips sk-ant-* substrings, configured-key verbatim matches, and
    /// x-api-key / authorization header values from <paramref name="raw"/>.
    /// </summary>
    /// <param name="raw">The error / log / response text to scrub.</param>
    /// <param name="resolvedKey">
    /// Optional: the verbatim API key resolved by AiApiKeyResolutionService for
    /// the active user. When provided, replaced with [REDACTED] before the regex
    /// passes run (more precise than regex-only).
    /// </param>
    public static string Redact(string raw, string? resolvedKey = null)
    {
        if (string.IsNullOrEmpty(raw)) return raw;

        var result = raw;

        // Verbatim resolved-key first (more precise than regex).
        if (!string.IsNullOrEmpty(resolvedKey))
            result = result.Replace(resolvedKey, "[REDACTED]", StringComparison.Ordinal);

        result = ApiKeyPattern.Replace(result, "[REDACTED]");
        result = HeaderPattern.Replace(result, "$1: [REDACTED]");
        return result;
    }
}
