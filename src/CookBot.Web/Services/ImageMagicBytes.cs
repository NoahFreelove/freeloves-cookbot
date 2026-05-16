namespace CookBot.Web.Services;

/// <summary>
/// Phase 9 / Plan 09-01 / PHOTO-02 — pure-static magic-byte sniffer. Maps the first
/// 12 bytes of an uploaded file to one of the four accepted extensions
/// (<c>.jpg</c>, <c>.png</c>, <c>.gif</c>, <c>.webp</c>), or <c>null</c> for anything
/// else (SVG, HTML-as-jpg, truncated, garbage). PITFALL H3 mandates that the
/// content-type is derived ONLY from these bytes — never from
/// <see cref="Microsoft.AspNetCore.Components.Forms.IBrowserFile.ContentType"/> nor
/// from the client-supplied filename.
/// </summary>
/// <remarks>
/// Signatures verified against RFC 9649 (JPEG), Wikipedia "List of file signatures"
/// (PNG / GIF), and Google's WebP Container Specification (RIFF + WEBP at offset 8).
/// Buffer length must be at least 12 to detect WebP; shorter buffers fall through
/// to <c>null</c>.
/// </remarks>
public static class ImageMagicBytes
{
    // JPEG: FF D8 FF (any 4th byte — JFIF E0, Exif E1, etc.)
    public static bool IsJpeg(ReadOnlySpan<byte> head) =>
        head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF;

    // PNG: 89 50 4E 47 0D 0A 1A 0A (8-byte signature)
    private static ReadOnlySpan<byte> PngSig => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    public static bool IsPng(ReadOnlySpan<byte> head) =>
        head.Length >= 8 && head[..8].SequenceEqual(PngSig);

    // GIF: "GIF87a" or "GIF89a" (47 49 46 38 [37|39] 61)
    public static bool IsGif(ReadOnlySpan<byte> head) =>
        head.Length >= 6
        && head[0] == 0x47 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x38
        && (head[4] == 0x37 || head[4] == 0x39)
        && head[5] == 0x61;

    // WebP: "RIFF" at offset 0 (52 49 46 46) + "WEBP" at offset 8 (57 45 42 50).
    // The 4 bytes at offset 4..7 are a little-endian chunk length we don't validate here.
    public static bool IsWebp(ReadOnlySpan<byte> head) =>
        head.Length >= 12
        && head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x46
        && head[8] == 0x57 && head[9] == 0x45 && head[10] == 0x42 && head[11] == 0x50;

    /// <summary>
    /// Maps a 12-byte (or shorter) header to one of the four accepted extensions,
    /// or returns <c>null</c> for everything else.
    /// </summary>
    public static string? DetectExtension(ReadOnlySpan<byte> head) =>
        IsJpeg(head) ? ".jpg" :
        IsPng(head) ? ".png" :
        IsWebp(head) ? ".webp" :
        IsGif(head) ? ".gif" :
        null;
}
