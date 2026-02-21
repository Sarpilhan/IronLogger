using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core.Diagnostics;
using ClickHouseLogger.Core.Enrichment;
using ClickHouseLogger.Core.Pipeline;
using ClickHouseLogger.Core.Redaction;
using ClickHouseLogger.Core.Serialization;

namespace ClickHouseLogger.Core;

/// <summary>
/// The central pipeline orchestrator for IronLogger.
/// <para>
/// Binds all pipeline stages together:
/// <c>Enrich → Redact → Queue → Batch → Serialize → Sink</c>
/// </para>
/// <para>
/// Implements <see cref="IAsyncDisposable"/> for graceful shutdown:
/// completes the queue, drains remaining events, flushes final batch, and disposes the sink.
/// </para>
/// </summary>
internal sealed class LogPipeline : IAsyncDisposable
{
    private readonly ILogEventEnricher _enricher;
    private readonly ILogEventRedactor? _redactor;
    private readonly BoundedLogQueue _queue;
    private readonly LogBatcher _batcher;
    private readonly NdjsonSerializer _serializer;
    private readonly ILogEventSink _sink;
    private readonly DiagnosticsTracker _diagnostics;
    private readonly ClickHouseLoggerOptions _options;
    private readonly CancellationTokenSource _shutdownCts;
    private readonly Task _backgroundTask;
    private bool _disposed;

    /// <summary>
    /// Initializes and starts the log pipeline with the given components.
    /// The background consumer loop starts immediately.
    /// </summary>
    public LogPipeline(
        ClickHouseLoggerOptions options,
        ILogEventEnricher enricher,
        ILogEventRedactor? redactor,
        ILogEventSink sink,
        DiagnosticsTracker diagnostics)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(enricher);
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(diagnostics);

        _options = options;
        _enricher = enricher;
        _redactor = redactor;
        _sink = sink;
        _diagnostics = diagnostics;

        _queue = new BoundedLogQueue(options.MaxQueueItems, options.DropPolicy, diagnostics);
        _batcher = new LogBatcher(_queue.Reader, options.BatchSize, options.FlushInterval);
        _serializer = new NdjsonSerializer(options.MaxExceptionLength);
        _shutdownCts = new CancellationTokenSource();

        // Start the background consumer loop
        _backgroundTask = Task.Run(() => ConsumerLoopAsync(_shutdownCts.Token));

        InternalLog.Info($"Pipeline started (batch={options.BatchSize}, flush={options.FlushInterval.TotalMilliseconds}ms, queue={options.MaxQueueItems})");
    }

    /// <summary>
    /// Provides read-only access to pipeline diagnostics.
    /// </summary>
    public IDiagnosticsSnapshot Diagnostics => _diagnostics;

    /// <summary>
    /// Returns <c>true</c> if the event level is below the configured minimum.
    /// </summary>
    public bool IsLevelDisabled(LogEventLevel level) => level < _options.MinLevel;

    /// <summary>
    /// Processes and enqueues a log event into the pipeline.
    /// <para>
    /// This method is called from the hot path (<c>ILogger.Log()</c>).
    /// It enriches, redacts, and enqueues the event synchronously.
    /// </para>
    /// </summary>
    /// <param name="logEvent">The log event to process.</param>
    public void Enqueue(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        // Level check happens at the ILogger level, not here — but double-check
        if (IsLevelDisabled(logEvent.Level))
            return;

        // Enrich → Redact (hot path — must be fast)
        _enricher.Enrich(logEvent);
        _redactor?.Redact(logEvent);

        // Enqueue (may drop based on DropPolicy)
        _queue.TryEnqueue(logEvent);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        InternalLog.Info("Pipeline shutdown initiated");

        // 1. Signal no more events
        _queue.Complete();

        // 2. Wait for background task to finish (with timeout)
        try
        {
            using var timeoutCts = new CancellationTokenSource(_options.FlushOnDisposeTimeout);

            // Give the consumer loop a chance to drain
            await _backgroundTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            InternalLog.Warn($"Background loop did not finish within {_options.FlushOnDisposeTimeout.TotalSeconds}s, cancelling");
            await _shutdownCts.CancelAsync().ConfigureAwait(false);

            // Try to drain remaining events synchronously
            await DrainRemainingAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            InternalLog.Error("Error waiting for background task", ex);
        }

        // 3. Dispose sink
        try
        {
            await _sink.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            InternalLog.Error("Error disposing sink", ex);
        }

        _shutdownCts.Dispose();

        var snapshot = _diagnostics;
        InternalLog.Info($"Pipeline shutdown complete — enqueued={snapshot.EnqueuedEvents}, dropped={snapshot.DroppedEvents}, sent={snapshot.SentBatches}, failed={snapshot.FailedBatches}");
    }

    private async Task ConsumerLoopAsync(CancellationToken cancellationToken)
    {
        InternalLog.Info("Consumer loop started");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var batch = await _batcher.ReadBatchAsync(cancellationToken).ConfigureAwait(false);

                if (batch.Count == 0)
                {
                    // Channel completed and no items left
                    break;
                }

                await SendBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            InternalLog.Info("Consumer loop cancelled");
        }
        catch (Exception ex)
        {
            InternalLog.Error("Consumer loop crashed", ex);
        }

        // Drain any remaining items after loop exit
        await DrainRemainingAsync().ConfigureAwait(false);

        InternalLog.Info("Consumer loop stopped");
    }

    private async Task DrainRemainingAsync()
    {
        var remaining = _batcher.DrainRemaining();
        if (remaining.Count > 0)
        {
            InternalLog.Info($"Draining {remaining.Count} remaining events");
            await SendBatchAsync(remaining, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task SendBatchAsync(IReadOnlyList<LogEvent> batch, CancellationToken cancellationToken)
    {
        try
        {
            var payload = _serializer.Serialize(batch);
            await _sink.WriteBatchAsync(payload, batch.Count, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down — expected
        }
        catch (Exception ex)
        {
            // Sink already handles retries + diagnostics, but log any unexpected errors
            InternalLog.Error($"Unexpected error sending batch of {batch.Count} events", ex);
        }
    }
}
