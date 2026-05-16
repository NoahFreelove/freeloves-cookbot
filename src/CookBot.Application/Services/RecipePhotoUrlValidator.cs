namespace CookBot.Application.Services;

/// <summary>
/// Phase 9 / Plan 09-01 / PHOTO-07 — scheme-allowlist validator for the
/// <c>Recipe.PhotoUrl</c> paste-URL surface. Shared between the editor input,
/// <see cref="RecipeService"/> save path, and the AnthropicAiService AI return
/// path (wired in Plan 09-05). Never throws — returns a tri-out envelope so
/// every call site can branch on accept/reject without try/catch.
/// </summary>
/// <remarks>
/// Allows only <c>http</c> and <c>https</c> schemes. Rejects (with explicit
/// errorCode for telemetry / toast messaging) the entire PITFALL H5 matrix:
/// <c>javascript:</c>, <c>data:</c>, <c>file:</c>, <c>ftp:</c>, <c>vbscript:</c>,
/// and protocol-relative <c>//host</c> shapes. Null / empty / whitespace input
/// is the canonical "no photo" signal — returns accept with normalized=null.
///
/// Registered as a Singleton (no state, no DI deps) in <c>AddApplication</c>.
/// </remarks>
public sealed class RecipePhotoUrlValidator
{
    /// <summary>
    /// Validates a candidate photo URL. Returns <c>true</c> for accept lanes;
    /// out-of-band errorCode disambiguates the reject lanes.
    /// </summary>
    /// <param name="input">The raw input string (typically paste-URL field value).</param>
    /// <param name="normalized">
    /// On accept-with-value, the trimmed <see cref="Uri.AbsoluteUri"/>; on accept-empty
    /// (null / whitespace / empty), <c>null</c>; on reject, <c>null</c>.
    /// </param>
    /// <param name="errorCode">
    /// On reject: one of "SCHEME_NOT_ALLOWED", "PROTOCOL_RELATIVE_REJECTED", or
    /// "MALFORMED". On accept: <c>null</c>.
    /// </param>
    public bool TryValidate(string? input, out string? normalized, out string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            normalized = null;
            errorCode = null;
            return true;
        }

        var trimmed = input!.Trim();

        // Protocol-relative // is explicitly H5-blocked — browsers would resolve it
        // against the current page scheme, defeating the http/https allowlist intent.
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            normalized = null;
            errorCode = "PROTOCOL_RELATIVE_REJECTED";
            return false;
        }

        // Path-only inputs (e.g. "/relative/path") would be parsed by Uri.TryCreate
        // on Linux as the Unix absolute file path "file:///relative/path" — that's
        // structurally a valid URI but it isn't what the user meant. Classify as
        // MALFORMED before scheme-allowlist so the error code matches the documented
        // PHOTO-07 behavior matrix (and gives a more useful toast).
        if (trimmed.StartsWith('/'))
        {
            normalized = null;
            errorCode = "MALFORMED";
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            normalized = null;
            errorCode = "MALFORMED";
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            normalized = null;
            errorCode = "SCHEME_NOT_ALLOWED";
            return false;
        }

        normalized = uri.AbsoluteUri;
        errorCode = null;
        return true;
    }
}
