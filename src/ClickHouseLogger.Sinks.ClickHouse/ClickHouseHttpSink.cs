using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core.Diagnostics;

namespace ClickHouseLogger.Sinks.ClickHouse;

/// <summary>
/// Sends serialized NDJSON batches to ClickHouse via HTTP POST.
/// <para>
/// Handles:
/// <list type="bullet">
///   <item>HTTP POST to <c>{endpoint}/?query=INSERT INTO {db}.{table} FORMAT JSONEachRow</c></item>
///   <item>Basic Auth (<c>X-ClickHouse-User</c> / <c>X-ClickHouse-Key</c> headers) or Bearer token</item>
///   <item>Optional gzip compression (<c>Content-Encoding: gzip</c>)</item>
///   <item>Exponential backoff + jitter retry on transient failures</item>
/// </list>
/// </para>
/// </summary>
internal sealed class ClickHouseHttpSink : ILogEventSink
{
    private readonly HttpClient _httpClient;
    private readonly string _insertUrl;
    private readonly ClickHouseCompression _compression;
    private readonly int _maxRetries;
    private readonly int _baseDelayMs;
    private readonly int _maxDelayMs;
    private readonly DiagnosticsTracker _diagnostics;
    private readonly BatchFailedCallback? _onBatchFailed;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Initializes a new <see cref="ClickHouseHttpSink"/> from the given options.
    /// </summary>
    /// <param name="options">Logger configuration.</param>
    /// <param name="diagnostics">Diagnostics tracker for counter updates.</param>
    public ClickHouseHttpSink(ClickHouseLoggerOptions options, DiagnosticsTracker diagnostics)
    {
        ArgumentNullException.ThrowIfNull(options);
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));

        _compression = options.Compression;
        _maxRetries = options.MaxRetries;
        _baseDelayMs = options.BaseDelayMs;
        _maxDelayMs = options.MaxDelayMs;
        _onBatchFailed = options.OnBatchFailed;

        // Build INSERT URL
        var endpoint = options.Endpoint.TrimEnd('/');
        var query = $"INSERT INTO {options.Database}.{options.Table} FORMAT JSONEachRow";
        _insertUrl = $"{endpoint}/?query={Uri.EscapeDataString(query)}";

        // Create HttpClient
        if (options.HttpMessageHandler is not null)
        {
            _httpClient = new HttpClient(options.HttpMessageHandler, disposeHandler: false);
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = new HttpClient();
            _ownsHttpClient = true;
        }

        _httpClient.Timeout = options.HttpTimeout;

        // Set auth headers
        ConfigureAuth(options);
    }

    /// <summary>
    /// Internal constructor for testing with a pre-configured HttpClient.
    /// </summary>
    internal ClickHouseHttpSink(
        HttpClient httpClient,
        string insertUrl,
        ClickHouseCompression compression,
        int maxRetries,
        int baseDelayMs,
        int maxDelayMs,
        DiagnosticsTracker diagnostics,
        BatchFailedCallback? onBatchFailed = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _insertUrl = insertUrl;
        _compression = compression;
        _maxRetries = maxRetries;
        _baseDelayMs = baseDelayMs;
        _maxDelayMs = maxDelayMs;
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _onBatchFailed = onBatchFailed;
        _ownsHttpClient = false;
    }

    /// <inheritdoc />
    public async Task WriteBatchAsync(ReadOnlyMemory<byte> payload, int eventCount, CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    var delay = CalculateBackoff(attempt);
                    InternalLog.Warn($"Retry attempt {attempt}/{_maxRetries} after {delay.TotalMilliseconds:F0}ms");
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                await SendAsync(payload, cancellationToken).ConfigureAwait(false);

                _diagnostics.RecordBatchSent();
                InternalLog.Info($"Batch sent: {eventCount} events, {payload.Length} bytes");
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutdown — don't retry
                throw;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                lastException = ex;
                InternalLog.Warn($"Transient error on attempt {attempt + 1}/{_maxRetries + 1}", ex);
            }
            catch (Exception ex)
            {
                // Non-transient error — don't retry
                lastException = ex;
                InternalLog.Error($"Non-transient error sending batch ({eventCount} events)", ex);
                break;
            }
        }

        // All retries exhausted
        _diagnostics.RecordBatchFailed();
        InternalLog.Error($"Batch failed after {_maxRetries + 1} attempts ({eventCount} events)", lastException);

        if (_onBatchFailed is not null && lastException is not null)
        {
            try
            {
                _onBatchFailed(lastException, eventCount);
            }
            catch (Exception callbackEx)
            {
                InternalLog.Error("OnBatchFailed callback threw", callbackEx);
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        using var content = CreateContent(payload);

        using var response = await _httpClient.PostAsync(_insertUrl, content, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var truncatedBody = body.Length > 500 ? body[..500] : body;
            throw new HttpRequestException(
                $"ClickHouse returned {(int)response.StatusCode} {response.ReasonPhrase}: {truncatedBody}",
                inner: null,
                statusCode: response.StatusCode);
        }
    }

    private HttpContent CreateContent(ReadOnlyMemory<byte> payload)
    {
        if (_compression == ClickHouseCompression.Gzip)
        {
            var compressed = CompressGzip(payload.Span);
            var content = new ByteArrayContent(compressed);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Headers.ContentEncoding.Add("gzip");
            return content;
        }

        var rawContent = new ReadOnlyMemoryContent(payload);
        rawContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return rawContent;
    }

    private static byte[] CompressGzip(ReadOnlySpan<byte> data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(data);
        }

        return output.ToArray();
    }

    private TimeSpan CalculateBackoff(int attempt)
    {
        // Exponential backoff: baseDelay * 2^(attempt-1) + jitter
        var exponentialMs = _baseDelayMs * Math.Pow(2, attempt - 1);
        var jitterMs = Random.Shared.Next(0, _baseDelayMs);
        var totalMs = Math.Min(exponentialMs + jitterMs, _maxDelayMs);
        return TimeSpan.FromMilliseconds(totalMs);
    }

    private static bool IsTransient(Exception ex) => ex switch
    {
        HttpRequestException httpEx => httpEx.StatusCode is
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout or
            HttpStatusCode.BadGateway or
            HttpStatusCode.RequestTimeout
            || httpEx.StatusCode is null, // connection failure
        TaskCanceledException => false, // don't retry user cancellation
        TimeoutException => true,
        _ => false
    };

    private void ConfigureAuth(ClickHouseLoggerOptions options)
    {
        if (!string.IsNullOrEmpty(options.AuthToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.AuthToken);
        }
        else if (!string.IsNullOrEmpty(options.User))
        {
            // ClickHouse HTTP interface headers
            _httpClient.DefaultRequestHeaders.Add("X-ClickHouse-User", options.User);
            if (!string.IsNullOrEmpty(options.Password))
            {
                _httpClient.DefaultRequestHeaders.Add("X-ClickHouse-Key", options.Password);
            }
        }
    }
}
