using System.Net;
using System.Net.Http.Headers;
using CookBot.Application.Services;

namespace CookBot.Tests.Services;

/// <summary>
/// Phase 14 / Plan 14-02 / GALLERY-04 / D-14-10 — unit tests for
/// <see cref="PhotoUrlHeadValidator"/>. All six acceptance/reject/fallback/timeout/network
/// lanes are verified against a <see cref="FakeHttpMessageHandler"/> so no live network
/// is required.
///
/// Test design:
///   - <see cref="StubPhotoUrlHeadValidator"/> subclasses the SUT and overrides
///     <see cref="PhotoUrlHeadValidator.CreateClient"/> to return an HttpClient backed
///     by a <see cref="FakeHttpMessageHandler"/> scripted per test.
///   - The fake handler is keyed on (HttpMethod, hasRangeHeader) so the 405-fallback
///     test can assert that a second GET request with a Range header was actually issued.
/// </summary>
public class PhotoUrlHeadValidatorTests
{
    // ---------------------------------------------------------------------------
    // Test double
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Captures all requests sent through the fake transport so tests can
    /// assert the sequence/content of HTTP calls.
    /// </summary>
    private sealed class RecordingFakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public readonly List<HttpRequestMessage> Requests = new();

        public RecordingFakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }

    /// <summary>
    /// Variant that throws <see cref="TaskCanceledException"/> unconditionally —
    /// simulates a timeout on every request.
    /// </summary>
    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(new TaskCanceledException("simulated timeout"));
    }

    /// <summary>
    /// Variant that throws <see cref="HttpRequestException"/> unconditionally —
    /// simulates a network error (DNS failure, connection refused, etc.).
    /// </summary>
    private sealed class NetworkErrorHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(
                new HttpRequestException("simulated network error"));
    }

    /// <summary>
    /// SUT subclass that overrides <see cref="PhotoUrlHeadValidator.CreateClient"/>
    /// so tests inject a fake handler without touching the network.
    /// </summary>
    private sealed class StubPhotoUrlHeadValidator : PhotoUrlHeadValidator
    {
        private readonly HttpMessageHandler _handler;

        public StubPhotoUrlHeadValidator(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        protected override HttpClient CreateClient()
            // Replicate the 5-second timeout so the shape matches production.
            // AllowAutoRedirect is irrelevant for the fake handler but kept for fidelity.
            => new HttpClient(_handler) { Timeout = TimeSpan.FromSeconds(5) };
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static HttpResponseMessage ImageResponse(HttpStatusCode status = HttpStatusCode.OK)
    {
        var r = new HttpResponseMessage(status);
        r.Content = new StringContent(string.Empty);
        r.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return r;
    }

    private static HttpResponseMessage NonImageResponse(HttpStatusCode status = HttpStatusCode.OK)
    {
        var r = new HttpResponseMessage(status);
        r.Content = new StringContent("<html/>");
        r.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        return r;
    }

    private static HttpResponseMessage StatusOnlyResponse(HttpStatusCode status)
        => new HttpResponseMessage(status);

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ValidImage_ReturnsValid()
    {
        // HEAD → 200 + Content-Type: image/jpeg → should be Valid
        var handler = new RecordingFakeHandler(_ => ImageResponse());
        var sut = new StubPhotoUrlHeadValidator(handler);

        var result = await sut.ValidateAsync("http://example.com/photo.jpg");

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Head, handler.Requests[0].Method);
    }

    [Fact]
    public async Task NonImage_ContentType_ReturnsNotAnImage()
    {
        // HEAD → 200 + Content-Type: text/html → should be rejected
        var handler = new RecordingFakeHandler(_ => NonImageResponse());
        var sut = new StubPhotoUrlHeadValidator(handler);

        var result = await sut.ValidateAsync("http://example.com/page.html");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        // Error message must mention "image" so the user understands what's required
        Assert.Contains("image", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task On405_FallsBackToRangedGet_AndReturnsValid()
    {
        // HEAD → 405; then GET with Range header → 206 + Content-Type: image/png → Valid
        // This is the CDN 405 fallback lane (D-14-10, RESEARCH §Pattern 2).
        var handler = new RecordingFakeHandler(request =>
        {
            if (request.Method == HttpMethod.Head)
                return StatusOnlyResponse(HttpStatusCode.MethodNotAllowed); // 405

            // Must be the ranged GET fallback
            var partialResponse = new HttpResponseMessage(HttpStatusCode.PartialContent); // 206
            partialResponse.Content = new ByteArrayContent(new byte[512]);
            partialResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return partialResponse;
        });
        var sut = new StubPhotoUrlHeadValidator(handler);

        var result = await sut.ValidateAsync("http://cdn.example.com/image.png");

        Assert.True(result.IsValid);
        // Assert two requests were made: HEAD then ranged GET
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Head, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        // Assert the fallback GET carried a Range header (bytes=0-511)
        var rangeHeader = handler.Requests[1].Headers.Range;
        Assert.NotNull(rangeHeader);
        var range = Assert.Single(rangeHeader.Ranges);
        Assert.Equal(0, range.From);
        Assert.Equal(511, range.To);
    }

    [Fact]
    public async Task HttpError_ReturnsHttpErrorResult()
    {
        // HEAD → 404 → should be rejected with HTTP error message
        var handler = new RecordingFakeHandler(_ => StatusOnlyResponse(HttpStatusCode.NotFound));
        var sut = new StubPhotoUrlHeadValidator(handler);

        var result = await sut.ValidateAsync("http://example.com/missing.jpg");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        // Error message must include the HTTP status code
        Assert.Contains("404", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Timeout_ReturnsTimeoutResult_AndDoesNotThrow()
    {
        // Handler throws TaskCanceledException → validator must return Timeout, never rethrow
        var sut = new StubPhotoUrlHeadValidator(new TimeoutHandler());

        PhotoUrlValidationResult? result = null;
        var ex = await Record.ExceptionAsync(async () =>
            result = await sut.ValidateAsync("http://slow.example.com/photo.jpg"));

        Assert.Null(ex);    // must NOT throw to caller
        Assert.NotNull(result);
        Assert.False(result!.IsValid);
        Assert.Equal(PhotoUrlValidationResult.Timeout.ErrorMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task NetworkError_ReturnsNetworkErrorResult_AndDoesNotThrow()
    {
        // Handler throws HttpRequestException → validator must return NetworkError, never rethrow
        var sut = new StubPhotoUrlHeadValidator(new NetworkErrorHandler());

        PhotoUrlValidationResult? result = null;
        var ex = await Record.ExceptionAsync(async () =>
            result = await sut.ValidateAsync("http://unreachable.example.com/photo.jpg"));

        Assert.Null(ex);    // must NOT throw to caller
        Assert.NotNull(result);
        Assert.False(result!.IsValid);
        Assert.Equal(PhotoUrlValidationResult.NetworkError.ErrorMessage, result.ErrorMessage);
    }
}
