namespace ClickHouseLogger.Abstractions;

/// <summary>
/// Defines the behavior when the internal queue is full.
/// </summary>
public enum DropPolicy
{
    /// <summary>
    /// When the queue is full, drop <see cref="LogEventLevel.Debug"/> and below.
    /// For <see cref="LogEventLevel.Information"/> and above, attempt enqueue; drop + increment counter on failure.
    /// This is the recommended default for production.
    /// </summary>
    DropDebugWhenBusy = 0,

    /// <summary>
    /// When the queue is full, the calling thread blocks until space is available.
    /// <para>
    /// ⚠️ Use with caution in production — may cause thread pool starvation
    /// if the sink (ClickHouse) is unreachable for extended periods.
    /// </para>
    /// </summary>
    BlockWhenFull = 1
}
