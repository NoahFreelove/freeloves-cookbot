using CookBot.Application.Services;

namespace CookBot.Tests.Services;

/// <summary>
/// Phase 9 / Plan 09-01 / PHOTO-07 — scheme-allowlist matrix for the paste-URL field.
/// PITFALL H5 mandates rejecting javascript:, data:, file:, ftp:, vbscript:, and
/// protocol-relative // schemes; accepting only http and https. Validator never throws
/// on any input including null / empty / whitespace (those are the "no photo" signal).
/// </summary>
public class RecipePhotoUrlValidatorTests
{
    private readonly RecipePhotoUrlValidator _sut = new();

    [Theory]
    // --- ACCEPT lanes ---
    [InlineData("https://example.com/photo.jpg", true, null)]
    [InlineData("http://example.com/photo.png", true, null)]
    [InlineData("HTTPS://Example.com/PHOTO.JPG", true, null)]              // Uri.Scheme is case-insensitive
    [InlineData("", true, null)]                                            // empty = no photo (nullable column)
    [InlineData(null, true, null)]                                          // null = no photo
    [InlineData("   ", true, null)]                                         // whitespace-only = no photo
    // --- REJECT lanes (PITFALL H5) ---
    [InlineData("javascript:alert(1)", false, "SCHEME_NOT_ALLOWED")]
    [InlineData("data:image/png;base64,iVBOR...", false, "SCHEME_NOT_ALLOWED")]
    [InlineData("file:///etc/passwd", false, "SCHEME_NOT_ALLOWED")]
    [InlineData("ftp://example.com/photo.jpg", false, "SCHEME_NOT_ALLOWED")]
    [InlineData("vbscript:msgbox(1)", false, "SCHEME_NOT_ALLOWED")]
    [InlineData("//example.com/photo.jpg", false, "PROTOCOL_RELATIVE_REJECTED")]
    [InlineData("/relative/path", false, "MALFORMED")]
    [InlineData("not a url", false, "MALFORMED")]
    public void TryValidate_PerH5Matrix(string? input, bool expectAccept, string? expectedErrorCode)
    {
        var actualAccept = _sut.TryValidate(input, out var normalized, out var errorCode);

        Assert.Equal(expectAccept, actualAccept);
        Assert.Equal(expectedErrorCode, errorCode);

        if (expectAccept && !string.IsNullOrWhiteSpace(input))
        {
            // Successful validation of a non-empty URL produces a normalized AbsoluteUri.
            Assert.False(string.IsNullOrEmpty(normalized));
        }
        else if (expectAccept)
        {
            // Null / empty / whitespace input → normalized = null (the "no photo" signal).
            Assert.Null(normalized);
        }
        else
        {
            // Rejected inputs always normalize to null.
            Assert.Null(normalized);
        }
    }

    [Fact]
    public void TryValidate_NormalizesWhitespace_Around_Url()
    {
        var accepted = _sut.TryValidate("  https://example.com/photo.jpg  ", out var normalized, out var errorCode);

        Assert.True(accepted);
        Assert.Null(errorCode);
        Assert.NotNull(normalized);
        // The leading and trailing whitespace must be trimmed before Uri.TryCreate.
        Assert.DoesNotContain(" ", normalized);
        Assert.Contains("example.com/photo.jpg", normalized);
    }

    [Fact]
    public void TryValidate_NeverThrows_OnAnyInput()
    {
        // Sample of pathological inputs — none should throw, all should return cleanly.
        var pathological = new[]
        {
            null,
            "",
            "   ",
            "javascript:" + new string('A', 10000),
            "http://" + new string('x', 5000),
            "\0\0\0",
            "🍕🍔🍟",
        };

        foreach (var input in pathological)
        {
            var ex = Record.Exception(() => _sut.TryValidate(input, out _, out _));
            Assert.Null(ex);
        }
    }
}
