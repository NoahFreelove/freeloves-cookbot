using System.Net;
using System.Net.Http.Headers;

namespace CookBot.Application.Services;

/// <summary>
/// Phase 14 / Plan 14-02 / GALLERY-04 / D-14-10 — HEAD-with-405→ranged-GET image URL
/// validator. Accepts a URL that has already passed the <see cref="RecipePhotoUrlValidator"/>
/// scheme allowlist (step 1, defangs javascript:/data:/file:) and issues an HTTP HEAD
/// (or a ranged GET fallback on 405) to confirm the URL actually returns an image.
///
/// Never throws to its caller — all error lanes are mapped to <see cref="PhotoUrlValidationResult"/>
/// factory members (Timeout, NetworkError, HttpError). This is the hard gate required by
/// GALLERY-04: failure blocks persist.
///
/// Registered as a Singleton (stateless, no DI deps) in <c>AddApplication</c>.
/// </summary>
/// <remarks>
/// SSRF posture (D-14-10): <c>AllowAutoRedirect = false</c> prevents the validator from
/// following a redirect from an external URL to an internal host. Private-IP deny-listing
/// is out of scope for the trusted-LAN posture (see RESEARCH.md §Security Domain).
///
/// The caller MUST run <see cref="RecipePhotoUrlValidator.TryValidate"/> first (scheme
/// allowlist is step 1 — D-14-10). Passing a non-http/https URL directly is undefined
/// behavior.
///
/// The <see cref="CreateClient"/> method is <c>protected virtual</c> so unit tests can
/// override it to inject a <see cref="FakeHttpMessageHandler"/> without touching the
/// network (mirrors the <c>AnthropicAiService.CreateHttpClient</c> seam pattern).
/// </remarks>
public class PhotoUrlHeadValidator
{
    /// <summary>
    /// Creates the <see cref="HttpClient"/> used for validation.
    /// Override in tests to inject a fake <see cref="HttpMessageHandler"/>.
    /// </summary>
    protected virtual HttpClient CreateClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>
    /// Issues an HTTP HEAD (with 405→ranged-GET fallback) to confirm the URL returns
    /// an image. Returns <see cref="PhotoUrlValidationResult.Valid"/> only on 2xx +
    /// <c>Content-Type: image/*</c>.
    /// </summary>
    /// <param name="url">
    /// A fully-normalised http/https URL that has already passed
    /// <see cref="RecipePhotoUrlValidator.TryValidate"/> (step 1, scheme allowlist).
    /// </param>
    /// <param name="ct">Optional cancellation token.</param>
    public async Task<PhotoUrlValidationResult> ValidateAsync(string url, CancellationToken ct = default)
    {
        using var http = CreateClient();

        try
        {
            // Step 1: issue HTTP HEAD (ResponseHeadersRead avoids downloading the body)
            var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            var headResponse = await http.SendAsync(headRequest,
                HttpCompletionOption.ResponseHeadersRead, ct);

            // CDN 405 fallback: many CDNs reject HEAD — fall back to a tiny ranged GET
            // that fetches only the first 512 bytes (enough to read the Content-Type header)
            if ((int)headResponse.StatusCode == 405)
            {
                var rangeRequest = new HttpRequestMessage(HttpMethod.Get, url);
                rangeRequest.Headers.Range = new RangeHeaderValue(0, 511);
                var rangeResponse = await http.SendAsync(rangeRequest,
                    HttpCompletionOption.ResponseHeadersRead, ct);
                return EvaluateResponse(rangeResponse);
            }

            return EvaluateResponse(headResponse);
        }
        catch (TaskCanceledException)
        {
            return PhotoUrlValidationResult.Timeout;
        }
        catch (HttpRequestException)
        {
            return PhotoUrlValidationResult.NetworkError;
        }
    }

    private static PhotoUrlValidationResult EvaluateResponse(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            return PhotoUrlValidationResult.HttpError(response.StatusCode);

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        return mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? PhotoUrlValidationResult.Valid
            : PhotoUrlValidationResult.NotAnImage;
    }
}

/// <summary>
/// Result envelope for <see cref="PhotoUrlHeadValidator.ValidateAsync"/>. Never throws —
/// all error lanes are mapped to factory members with a user-facing <see cref="ErrorMessage"/>.
/// </summary>
public sealed record PhotoUrlValidationResult(bool IsValid, string? ErrorMessage)
{
    /// <summary>URL returned 2xx + Content-Type: image/*.</summary>
    public static PhotoUrlValidationResult Valid => new(true, null);

    /// <summary>Validation timed out (5-second client timeout exceeded).</summary>
    public static PhotoUrlValidationResult Timeout =>
        new(false, "URL validation timed out — check the URL and try again.");

    /// <summary>A network-level error prevented reaching the URL.</summary>
    public static PhotoUrlValidationResult NetworkError =>
        new(false, "Could not reach the photo URL — check connectivity.");

    /// <summary>URL returned 2xx but Content-Type is not image/*.</summary>
    public static PhotoUrlValidationResult NotAnImage =>
        new(false, "URL did not return an image — only image URLs are accepted.");

    /// <summary>URL returned a non-success HTTP status code.</summary>
    public static PhotoUrlValidationResult HttpError(HttpStatusCode sc) =>
        new(false, $"Could not reach that URL (HTTP {(int)sc}).");
}
