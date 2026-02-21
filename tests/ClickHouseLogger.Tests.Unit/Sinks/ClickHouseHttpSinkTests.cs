using System.Net;
using System.Net.Http.Headers;
using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core.Diagnostics;
using ClickHouseLogger.Sinks.ClickHouse;
using FluentAssertions;
using Xunit;

namespace ClickHouseLogger.Tests.Unit.Sinks;

public class ClickHouseHttpSinkTests
{
    private static DiagnosticsTracker CreateTracker() => new();

    // ── Successful Send ─────────────────────────────────────────

    [Fact]
    public async Task WriteBatchAsync_Success_RecordsBatchSent()
    {
        var tracker = CreateTracker();
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "");
        var client = new HttpClient(handler);

        var sink = new ClickHouseHttpSink(
            client,
            insertUrl: "http://localhost:8123/?query=INSERT+INTO+test+FORMAT+JSONEachRow",
            compression: ClickHouseCompression.None,
            maxRetries: 0,
            baseDelayMs: 100,
            maxDelayMs: 1000,
            diagnostics: tracker);

        var payload = """{"ts":"2026-01-01 00:00:00.000","level":"Information"}"""u8.ToArray();

        await sink.WriteBatchAsync(payload, 1);

        tracker.SentBatches.Should().Be(1);
        tracker.FailedBatches.Should().Be(0);
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task WriteBatchAsync_Success_SendsCorrectUrl()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "");
        var client = new HttpClient(handler);
        var insertUrl = "http://localhost:8123/?query=INSERT+INTO+observability.app_logs+FORMAT+JSONEachRow";

        var sink = new ClickHouseHttpSink(
            client, insertUrl, ClickHouseCompression.None,
            0, 100, 1000, CreateTracker());

        await sink.WriteBatchAsync(new byte[] { 0x7B, 0x7D }, 1);

        handler.LastRequest!.RequestUri!.ToString().Should().Be(insertUrl);
    }

    // ── Gzip Compression ────────────────────────────────────────

    [Fact]
    public async Task WriteBatchAsync_GzipCompression_SetsContentEncoding()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "");
        var client = new HttpClient(handler);

        var sink = new ClickHouseHttpSink(
            client,
            insertUrl: "http://localhost:8123/",
            compression: ClickHouseCompression.Gzip,
            maxRetries: 0,
            baseDelayMs: 100,
            maxDelayMs: 1000,
            diagnostics: CreateTracker());

        var payload = """{"message":"test"}"""u8.ToArray();
        await sink.WriteBatchAsync(payload, 1);

        handler.LastRequest!.Content!.Headers.ContentEncoding.Should().Contain("gzip");
    }

    [Fact]
    public async Task WriteBatchAsync_NoCompression_NoContentEncoding()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "");
        var client = new HttpClient(handler);

        var sink = new ClickHouseHttpSink(
            client, "http://localhost:8123/", ClickHouseCompression.None,
            0, 100, 1000, CreateTracker());

        await sink.WriteBatchAsync("""{"message":"test"}"""u8.ToArray(), 1);

        handler.LastRequest!.Content!.Headers.ContentEncoding.Should().BeEmpty();
    }

    // ── Retry Logic ─────────────────────────────────────────────

    [Fact]
    public async Task WriteBatchAsync_TransientError_RetriesAndSucceeds()
    {
        var tracker = CreateTracker();
        var handler = new MockHttpMessageHandler(new[]
        {
            (HttpStatusCode.ServiceUnavailable, "busy"),
            (HttpStatusCode.ServiceUnavailable, "busy"),
            (HttpStatusCode.OK, "")
        });
        var client = new HttpClient(handler);

        var sink = new ClickHouseHttpSink(
            client, "http://localhost:8123/", ClickHouseCompression.None,
            maxRetries: 3, baseDelayMs: 10, maxDelayMs: 50, diagnostics: tracker);

        await sink.WriteBatchAsync(new byte[] { 0x7B, 0x7D }, 1);

        handler.RequestCount.Should().Be(3); // 2 failures + 1 success
        tracker.SentBatches.Should().Be(1);
        tracker.FailedBatches.Should().Be(0);
    }

    [Fact]
    public async Task WriteBatchAsync_AllRetriesExhausted_RecordsBatchFailed()
    {
        var tracker = CreateTracker();
        var handler = new MockHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "down");
        var client = new HttpClient(handler);

        var sink = new ClickHouseHttpSink(
            client, "http://localhost:8123/", ClickHouseCompression.None,
            maxRetries: 2, baseDelayMs: 10, maxDelayMs: 50, diagnostics: tracker);

        await sink.WriteBatchAsync(new byte[] { 0x7B, 0x7D }, 1);

        handler.RequestCount.Should().Be(3); // initial + 2 retries
        tracker.SentBatches.Should().Be(0);
        tracker.FailedBatches.Should().Be(1);
    }

    [Fact]
    public async Task WriteBatchAsync_NonTransientError_NoRetry()
    {
        var tracker = CreateTracker();
        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, "syntax error");
        var client = new HttpClient(handler);

        var sink = new ClickHouseHttpSink(
            client, "http://localhost:8123/", ClickHouseCompression.None,
            maxRetries: 3, baseDelayMs: 10, maxDelayMs: 50, diagnostics: tracker);

        await sink.WriteBatchAsync(new byte[] { 0x7B, 0x7D }, 1);

        handler.RequestCount.Should().Be(1); // no retries for 400
        tracker.FailedBatches.Should().Be(1);
    }

    // ── BatchFailed Callback ────────────────────────────────────

    [Fact]
    public async Task WriteBatchAsync_FailedBatch_InvokesCallback()
    {
        Exception? capturedEx = null;
        int capturedSize = 0;

        var handler = new MockHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "down");
        var client = new HttpClient(handler);

        var sink = new ClickHouseHttpSink(
            client, "http://localhost:8123/", ClickHouseCompression.None,
            maxRetries: 0, baseDelayMs: 10, maxDelayMs: 50,
            diagnostics: CreateTracker(),
            onBatchFailed: (ex, size) => { capturedEx = ex; capturedSize = size; });

        await sink.WriteBatchAsync(new byte[] { 0x7B, 0x7D }, 42);

        capturedEx.Should().NotBeNull();
        capturedSize.Should().Be(42);
    }

    // ── Auth Configuration ──────────────────────────────────────

    [Fact]
    public async Task WriteBatchAsync_BasicAuth_SetsHeaders()
    {
        var tracker = CreateTracker();
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "");
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("X-ClickHouse-User", "admin");
        client.DefaultRequestHeaders.Add("X-ClickHouse-Key", "secret123");

        var sink = new ClickHouseHttpSink(
            client, "http://localhost:8123/", ClickHouseCompression.None,
            0, 100, 1000, tracker);

        await sink.WriteBatchAsync(new byte[] { 0x7B, 0x7D }, 1);

        handler.LastRequest!.Headers.GetValues("X-ClickHouse-User").Should().Contain("admin");
        handler.LastRequest!.Headers.GetValues("X-ClickHouse-Key").Should().Contain("secret123");
    }

    [Fact]
    public async Task WriteBatchAsync_TokenAuth_SetsBearerHeader()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "");
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "my-token");

        var sink = new ClickHouseHttpSink(
            client, "http://localhost:8123/", ClickHouseCompression.None,
            0, 100, 1000, CreateTracker());

        await sink.WriteBatchAsync(new byte[] { 0x7B, 0x7D }, 1);

        handler.LastRequest!.Headers.Authorization.Should().NotBeNull();
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("my-token");
    }

    // ── Dispose ─────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "");
        var client = new HttpClient(handler);

        var sink = new ClickHouseHttpSink(
            client, "http://localhost:8123/", ClickHouseCompression.None,
            0, 100, 1000, CreateTracker());

        var act = async () => await sink.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    // ── Mock Handler ────────────────────────────────────────────

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly (HttpStatusCode Status, string Body)[] _responses;
        private int _callIndex;

        public int RequestCount => _callIndex;
        public HttpRequestMessage? LastRequest { get; private set; }

        public MockHttpMessageHandler(HttpStatusCode status, string body)
        {
            _responses = [(status, body)];
        }

        public MockHttpMessageHandler((HttpStatusCode Status, string Body)[] responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var index = Math.Min(_callIndex, _responses.Length - 1);
            _callIndex++;

            var (status, body) = _responses[index];
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            };

            return Task.FromResult(response);
        }
    }
}
