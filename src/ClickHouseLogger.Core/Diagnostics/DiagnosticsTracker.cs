using ClickHouseLogger.Abstractions;

namespace ClickHouseLogger.Core.Diagnostics;

/// <summary>
/// Thread-safe internal diagnostics counter implementation.
/// <para>
/// Uses <see cref="Interlocked"/> operations for lock-free counter updates.
/// Exposed via <see cref="IDiagnosticsSnapshot"/> for consumer access.
/// </para>
/// </summary>
internal sealed class DiagnosticsTracker : IDiagnosticsSnapshot
{
    private long _enqueuedEvents;
    private long _droppedEvents;
    private long _sentBatches;
    private long _failedBatches;
    private int _queueLength;
    private long _lastSendTicks; // stored as UTC ticks; 0 = never sent

    /// <inheritdoc />
    public long EnqueuedEvents => Interlocked.Read(ref _enqueuedEvents);

    /// <inheritdoc />
    public long DroppedEvents => Interlocked.Read(ref _droppedEvents);

    /// <inheritdoc />
    public long SentBatches => Interlocked.Read(ref _sentBatches);

    /// <inheritdoc />
    public long FailedBatches => Interlocked.Read(ref _failedBatches);

    /// <inheritdoc />
    public int QueueLength => Volatile.Read(ref _queueLength);

    /// <inheritdoc />
    public DateTimeOffset? LastSendUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastSendTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    /// <summary>Increments the enqueued events counter.</summary>
    internal void IncrementEnqueued() => Interlocked.Increment(ref _enqueuedEvents);

    /// <summary>Increments the dropped events counter.</summary>
    internal void IncrementDropped() => Interlocked.Increment(ref _droppedEvents);

    /// <summary>Increments the dropped events counter by a given amount.</summary>
    internal void IncrementDropped(long count) => Interlocked.Add(ref _droppedEvents, count);

    /// <summary>Records a successful batch send.</summary>
    internal void RecordBatchSent()
    {
        Interlocked.Increment(ref _sentBatches);
        Interlocked.Exchange(ref _lastSendTicks, DateTimeOffset.UtcNow.Ticks);
    }

    /// <summary>Records a failed batch send.</summary>
    internal void RecordBatchFailed() => Interlocked.Increment(ref _failedBatches);

    /// <summary>Updates the instantaneous queue length.</summary>
    internal void SetQueueLength(int length) => Volatile.Write(ref _queueLength, length);
}
