namespace ClickHouseLogger.Abstractions;

/// <summary>
/// Writes serialized log event batches to a storage backend (e.g., ClickHouse HTTP interface).
/// <para>
/// The sink receives pre-serialized NDJSON bytes and is responsible for:
/// <list type="bullet">
///   <item>HTTP transport (POST with appropriate headers)</item>
///   <item>Compression (gzip, if configured)</item>
///   <item>Authentication (Basic or Token)</item>
///   <item>Retry logic (exponential backoff + jitter)</item>
/// </list>
/// </para>
/// </summary>
public interface ILogEventSink : IAsyncDisposable
{
    /// <summary>
    /// Writes a batch of serialized log events to the backend.
    /// </summary>
    /// <param name="payload">NDJSON-encoded log events as UTF-8 bytes.</param>
    /// <param name="eventCount">Number of events in the batch (for diagnostics).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the batch is acknowledged or all retries are exhausted.</returns>
    Task WriteBatchAsync(ReadOnlyMemory<byte> payload, int eventCount, CancellationToken cancellationToken = default);
}
