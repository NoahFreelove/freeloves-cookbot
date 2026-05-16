using CookBot.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace CookBot.Tests.Services;

/// <summary>
/// Phase 9 / Plan 09-01 / PHOTO-02/03/05 — magic-byte sniff matrix + path-traversal
/// defense for <see cref="LocalRecipePhotoStorage"/>. PITFALL H3 mandates that the
/// content-type is derived from the first 12 bytes (never from <c>IBrowserFile.ContentType</c>
/// nor the client filename); PITFALL H2 mandates a defense-in-depth path-prefix assertion
/// even though server-generated GUID filenames already prevent traversal in theory.
/// </summary>
public class LocalRecipePhotoStorageTests
{
    // ---- ACCEPTED magic-byte matrix (RESEARCH Item 5 / PATTERNS lines 673–682) ----

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 }, ".jpg")] // JPEG/JFIF
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, 0, 0, 0, 0, 0, 0, 0, 0 }, ".jpg")] // JPEG/Exif
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 }, ".png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61, 0, 0, 0, 0, 0, 0 }, ".gif")] // GIF87a
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0, 0, 0, 0, 0, 0 }, ".gif")] // GIF89a
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, ".webp")]
    public void DetectExtension_AcceptedTypes_ReturnsCorrectExt(byte[] head, string expected)
    {
        var actual = ImageMagicBytes.DetectExtension(head);
        Assert.Equal(expected, actual);
    }

    // ---- REJECTED magic-byte matrix (PITFALL H3 — SVG / HTML / short / truncated) ----

    [Theory]
    [InlineData(new byte[] { 0x3C, 0x3F, 0x78, 0x6D, 0x6C, 0, 0, 0, 0, 0, 0, 0 })] // "<?xml" — SVG
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67, 0, 0, 0, 0, 0, 0, 0, 0 })]    // "<svg"
    [InlineData(new byte[] { 0x3C, 0x21, 0x44, 0x4F, 0x43, 0, 0, 0, 0, 0, 0, 0 })] // "<!DOC" — HTML
    [InlineData(new byte[] { 0, 0, 0 })]                                            // length < 3 boundary; still rejected (no magic)
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0 })]                 // truncated WebP (length < 12)
    [InlineData(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0, 0, 0, 0, 0, 0, 0, 0 })]     // random bytes
    public void DetectExtension_Rejected_ReturnsNull(byte[] head)
    {
        var actual = ImageMagicBytes.DetectExtension(head);
        Assert.Null(actual);
    }

    [Fact]
    public void DetectExtension_EmptySpan_ReturnsNull()
    {
        Assert.Null(ImageMagicBytes.DetectExtension(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void DetectExtension_OneByteSpan_ReturnsNull()
    {
        Assert.Null(ImageMagicBytes.DetectExtension(new byte[] { 0xFF }));
    }

    // ---- Path-traversal defense (PITFALL H2) ----

    [Fact]
    public async Task SaveAsync_PathTraversalAttempt_ThrowsInvalidOperationException()
    {
        // Construct a fake IWebHostEnvironment pointing at a temp dir so SaveAsync's
        // ctor doesn't try to create a real wwwroot/uploads/ on the test machine.
        using var temp = new TempWebRoot();
        var env = new TestWebHostEnvironment(temp.Path);
        var storage = new LocalRecipePhotoStorage(env, NullLogger<LocalRecipePhotoStorage>.Instance);

        // A traversal-shaped filename can't be constructed from inside SaveAsync because
        // the filename is server-generated (Guid.NewGuid:N + magic-byte ext) — that's
        // PHOTO-05. To exercise the defense-in-depth prefix check, we use the public
        // helper that the implementation exposes for exactly this contract.
        var traversalPath = Path.Combine(temp.Path, "uploads", "..", "..", "evil.jpg");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            storage.AssertPathInsideUploadsDirectory(traversalPath));
        Assert.Contains("Path traversal", ex.Message, StringComparison.OrdinalIgnoreCase);

        await Task.CompletedTask;
    }

    [Fact]
    public void AssertPathInsideUploadsDirectory_LegitFile_DoesNotThrow()
    {
        using var temp = new TempWebRoot();
        var env = new TestWebHostEnvironment(temp.Path);
        var storage = new LocalRecipePhotoStorage(env, NullLogger<LocalRecipePhotoStorage>.Instance);

        var legitPath = Path.Combine(temp.Path, "uploads", "abc123.jpg");

        var ex = Record.Exception(() => storage.AssertPathInsideUploadsDirectory(legitPath));
        Assert.Null(ex);
    }

    // ---- Test plumbing ----

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string webRoot)
        {
            WebRootPath = webRoot;
            WebRootFileProvider = new PhysicalFileProvider(webRoot);
            ContentRootPath = webRoot;
            ContentRootFileProvider = new PhysicalFileProvider(webRoot);
            EnvironmentName = "Test";
            ApplicationName = "CookBot.Tests";
        }

        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
        public string EnvironmentName { get; set; }
    }

    private sealed class TempWebRoot : IDisposable
    {
        public string Path { get; }

        public TempWebRoot()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cookbot-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // best effort
            }
        }
    }
}
