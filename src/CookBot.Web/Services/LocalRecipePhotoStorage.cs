using Microsoft.AspNetCore.Components.Forms;

namespace CookBot.Web.Services;

/// <summary>
/// Phase 9 / Plan 09-01 / PHOTO-02/03/05 — Scoped Web service that persists an
/// uploaded <see cref="IBrowserFile"/> to <c>wwwroot/uploads/</c> as
/// <c>{guid:N}{ext}</c> after a magic-byte sniff of the first 12 bytes. The
/// returned URL (<c>/uploads/{safeName}</c>) is what the editor binds to
/// <c>Recipe.PhotoUrl</c>.
/// </summary>
/// <remarks>
/// <para>
/// PITFALL H1 / PHOTO-04 — the 10 MB <c>maxAllowedSize</c> on
/// <see cref="IBrowserFile.OpenReadStream"/> is the per-file ceiling. The three
/// server-side size limits in <c>Program.cs</c> (Kestrel
/// <c>MaxRequestBodySize</c>, <c>FormOptions.MultipartBodyLengthLimit</c>, and the
/// Blazor Server <c>MaximumReceiveMessageSize</c>) are 12 MB to leave headroom
/// above the 10 MB per-file cap.
/// </para>
/// <para>
/// PITFALL H2 — defense-in-depth path-traversal guard. Even though the filename
/// is server-generated (<c>Guid.NewGuid().ToString("N")</c> + magic-byte ext) so
/// traversal isn't reachable in theory,
/// <see cref="AssertPathInsideUploadsDirectory"/> still asserts that the resolved
/// <see cref="Path.GetFullPath(string)"/> stays inside the uploads directory.
/// </para>
/// <para>
/// PITFALL H3 — extension comes from magic bytes, not from
/// <see cref="IBrowserFile.ContentType"/> nor from <see cref="IBrowserFile.Name"/>.
/// SVG and HTML-as-jpg uploads fail the sniff and surface an
/// <see cref="InvalidImageException"/>.
/// </para>
/// </remarks>
public sealed class LocalRecipePhotoStorage
{
    // PHOTO-03 — per-file ceiling. Three server-side limits in Program.cs are 12 MB
    // to leave headroom above this 10 MB cap (so a 10 MB file doesn't trip an outer
    // 10 MB-equal limit on the SignalR / form boundary first).
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    private readonly string _uploadsDir;
    private readonly ILogger<LocalRecipePhotoStorage> _logger;

    public LocalRecipePhotoStorage(IWebHostEnvironment env, ILogger<LocalRecipePhotoStorage> logger)
    {
        _uploadsDir = Path.Combine(env.WebRootPath, "uploads");
        Directory.CreateDirectory(_uploadsDir); // idempotent
        _logger = logger;
    }

    /// <summary>
    /// Persists <paramref name="file"/> to <c>wwwroot/uploads/</c> after a 12-byte
    /// magic-byte sniff. Returns the public URL (e.g. <c>/uploads/{guid}.jpg</c>)
    /// for binding to <c>Recipe.PhotoUrl</c>.
    /// </summary>
    /// <exception cref="InvalidImageException">
    /// First 12 bytes do not match JPEG / PNG / GIF / WebP magic signatures
    /// (SVG, HTML-as-jpg, truncated, garbage all reach this lane).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Defense-in-depth path-traversal check failed (should be unreachable given
    /// server-generated GUID filenames — PITFALL H2 belt-and-braces).
    /// </exception>
    public async Task<string> SaveAsync(IBrowserFile file, CancellationToken ct = default)
    {
        // First pass — read first 12 bytes for magic-byte sniff. Stream is disposed
        // before the second open below; IBrowserFile returns a fresh stream per call.
        string? ext;
        {
            await using var src = file.OpenReadStream(maxAllowedSize: MaxUploadBytes, ct);
            var head = new byte[12];
            var bytesRead = await src.ReadAtLeastAsync(head, 12, throwOnEndOfStream: false, ct);
            ext = ImageMagicBytes.DetectExtension(head.AsSpan(0, bytesRead));
        }

        if (ext is null)
        {
            // PITFALL H3 — rejection path. SVG / HTML / truncated / garbage land here.
            // Editor (Plan 09-02) catches this and shows a toast: "Only JPEG/PNG/GIF/WebP allowed."
            throw new InvalidImageException(
                "Uploaded file is not a recognized image (JPEG, PNG, GIF, or WebP).");
        }

        // PHOTO-05 — server-generated filename. NEVER use file.Name (client-controlled,
        // may contain traversal segments, spaces, unicode, executable extensions).
        var safeName = $"{Guid.NewGuid():N}{ext}";
        var savePath = Path.Combine(_uploadsDir, safeName);

        // PITFALL H2 — defense-in-depth. Should be unreachable given the GUID-only
        // filename above, but assert it anyway: a future refactor that takes ANY part
        // of the filename from the client must not break this invariant silently.
        AssertPathInsideUploadsDirectory(savePath);

        // Second pass — re-open the file from the start and stream the full payload
        // to disk. The first stream already consumed the first 12 bytes, so we can't
        // reuse it; IBrowserFile guarantees a fresh stream per OpenReadStream call.
        await using (var writeStream = File.Create(savePath))
        await using (var src2 = file.OpenReadStream(maxAllowedSize: MaxUploadBytes, ct))
        {
            await src2.CopyToAsync(writeStream, ct);
        }

        _logger.LogInformation(
            "Saved uploaded photo {SafeName} ({Length} bytes) as {Extension}",
            safeName, file.Size, ext);

        return $"/uploads/{safeName}";
    }

    /// <summary>
    /// Defense-in-depth path-traversal assertion (PITFALL H2). Public so the test
    /// suite can exercise it without constructing a full <see cref="IBrowserFile"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Resolved <paramref name="savePath"/> escapes the uploads directory.
    /// </exception>
    public void AssertPathInsideUploadsDirectory(string savePath)
    {
        var fullSavePath = Path.GetFullPath(savePath);
        var fullUploadsDir = Path.GetFullPath(_uploadsDir);

        // Append a trailing separator to fullUploadsDir for the StartsWith comparison
        // so "/tmp/uploads-evil" doesn't pass a "/tmp/uploads" prefix check.
        var prefix = fullUploadsDir.EndsWith(Path.DirectorySeparatorChar)
            ? fullUploadsDir
            : fullUploadsDir + Path.DirectorySeparatorChar;

        if (!fullSavePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Path traversal attempt detected: '{savePath}' resolves outside '{_uploadsDir}'.");
        }
    }
}

/// <summary>
/// Thrown by <see cref="LocalRecipePhotoStorage.SaveAsync"/> when the magic-byte
/// sniff fails. Surfaced to the editor as a toast.
/// </summary>
public sealed class InvalidImageException : Exception
{
    public InvalidImageException(string message) : base(message) { }
}
