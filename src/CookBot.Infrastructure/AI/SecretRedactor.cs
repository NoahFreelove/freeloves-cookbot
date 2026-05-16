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

    // PROD-10 / PITFALL C4 (Phase 9 / Plan 09-04) — ASP.NET Core Data Protection
    // ciphertext blobs start with the literal "CfDJ8" magic, followed by ≥40 base64url
    // characters. CryptographicException messages thrown by IDataProtector.Unprotect
    // can echo the ciphertext back; without this scrub, an attacker with log access
    // could pivot to a known-ciphertext attack. Pattern is bounded at the lower end
    // (40 chars) so short tokens that happen to start with CfDJ8 don't get scrubbed.
    private static readonly Regex CipherTextPattern =
        new(@"CfDJ8[A-Za-z0-9_\-]{40,}",
            RegexOptions.Compiled);

    /// <summary>
    /// Strips sk-ant-* substrings, configured-key verbatim matches,
    /// x-api-key / authorization header values, and Data Protection CfDJ8 ciphertext
    /// blobs from <paramref name="raw"/>.
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
        result = CipherTextPattern.Replace(result, "[REDACTED-CIPHERTEXT]");
        return result;
    }
}
