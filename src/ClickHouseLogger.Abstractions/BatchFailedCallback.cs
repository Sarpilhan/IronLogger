namespace ClickHouseLogger.Abstractions;

/// <summary>
/// Callback invoked when a batch write to ClickHouse fails after all retries are exhausted.
/// </summary>
/// <param name="exception">The exception that caused the final failure.</param>
/// <param name="batchSize">Number of events in the failed batch.</param>
public delegate void BatchFailedCallback(Exception exception, int batchSize);
