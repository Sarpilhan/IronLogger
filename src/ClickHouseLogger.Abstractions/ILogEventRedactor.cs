namespace ClickHouseLogger.Abstractions;

/// <summary>
/// Redacts sensitive/PII data from a <see cref="LogEvent"/> before serialization.
/// <para>
/// Redactors run after enrichment and before queue insertion.
/// Implementations must be thread-safe and should minimize allocations
/// to avoid impacting throughput.
/// </para>
/// </summary>
public interface ILogEventRedactor
{
    /// <summary>
    /// Redacts sensitive data in the given log event in-place.
    /// <para>
    /// This includes masking values in <see cref="LogEvent.Props"/> by key name
    /// and applying regex patterns to string values.
    /// </para>
    /// </summary>
    /// <param name="logEvent">The log event to redact. Must not be <c>null</c>.</param>
    void Redact(LogEvent logEvent);
}
