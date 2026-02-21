using System.Threading.Channels;
using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core.Diagnostics;

namespace ClickHouseLogger.Core.Pipeline;

/// <summary>
/// Reads events from a <see cref="BoundedLogQueue"/> and groups them into batches
/// triggered by <see cref="ClickHouseLoggerOptions.BatchSize"/> or <see cref="ClickHouseLoggerOptions.FlushInterval"/>.
/// <para>
/// Designed to run on a single consumer thread (via <see cref="ReadBatchAsync"/>).
/// </para>
/// </summary>
internal sealed class LogBatcher
{
    private readonly ChannelReader<LogEvent> _reader;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;

    /// <summary>
    /// Initializes a new <see cref="LogBatcher"/>.
    /// </summary>
    /// <param name="reader">Channel reader from the bounded queue.</param>
    /// <param name="batchSize">Maximum events per batch.</param>
    /// <param name="flushInterval">Maximum time to wait before flushing a partial batch.</param>
    public LogBatcher(ChannelReader<LogEvent> reader, int batchSize, TimeSpan flushInterval)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _batchSize = batchSize > 0 ? batchSize : throw new ArgumentOutOfRangeException(nameof(batchSize));
        _flushInterval = flushInterval;
    }

    /// <summary>
    /// Reads the next batch of events from the queue.
    /// Returns when batch size is reached, flush interval
    /// expires, or the channel is completed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A list of log events (may be empty if the channel completed with no pending items).
    /// </returns>
    public async Task<IReadOnlyList<LogEvent>> ReadBatchAsync(CancellationToken cancellationToken = default)
    {
        var batch = new List<LogEvent>(_batchSize);

        // Wait for the first item (or cancellation/completion)
        try
        {
            if (!await _reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // Channel completed — drain remaining
                return DrainRemaining(batch);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down — drain what's left
            return DrainRemaining(batch);
        }

        // Got at least one item — start the flush timer
        using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        flushCts.CancelAfter(_flushInterval);

        try
        {
            while (batch.Count < _batchSize)
            {
                // Try to read synchronously first (fast path for busy queues)
                if (_reader.TryRead(out var item))
                {
                    batch.Add(item);
                    continue;
                }

                // No item ready — wait for next or timeout
                if (!await _reader.WaitToReadAsync(flushCts.Token).ConfigureAwait(false))
                {
                    // Channel completed
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (flushCts.IsCancellationRequested)
        {
            // Flush interval expired — return what we have
        }

        return batch;
    }

    /// <summary>
    /// Drains all remaining items from the channel (used during shutdown).
    /// </summary>
    /// <returns>List of remaining events.</returns>
    public IReadOnlyList<LogEvent> DrainRemaining()
    {
        return DrainRemaining(new List<LogEvent>());
    }

    private List<LogEvent> DrainRemaining(List<LogEvent> batch)
    {
        while (_reader.TryRead(out var item))
        {
            batch.Add(item);
        }

        return batch;
    }
}
