namespace ClickHouseLogger.Abstractions;

/// <summary>
/// Provides a read-only snapshot of the IronLogger pipeline's internal diagnostics counters.
/// <para>
/// These metrics are essential for production monitoring and troubleshooting.
/// All counters are monotonically increasing except <see cref="QueueLength"/> (instantaneous).
/// </para>
/// </summary>
public interface IDiagnosticsSnapshot
{
    /// <summary>Total number of events successfully enqueued into the pipeline.</summary>
    long EnqueuedEvents { get; }

    /// <summary>Total number of events dropped due to queue full or drop policy.</summary>
    long DroppedEvents { get; }

    /// <summary>Total number of batches successfully sent to ClickHouse.</summary>
    long SentBatches { get; }

    /// <summary>Total number of batches that failed after all retries were exhausted.</summary>
    long FailedBatches { get; }

    /// <summary>Current number of events waiting in the queue (instantaneous, not monotonic).</summary>
    int QueueLength { get; }

    /// <summary>UTC timestamp of the last successful batch send, or <c>null</c> if no batch has been sent yet.</summary>
    DateTimeOffset? LastSendUtc { get; }
}
