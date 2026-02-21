namespace ClickHouseLogger.Abstractions;

/// <summary>
/// Enriches a <see cref="LogEvent"/> with additional contextual data.
/// <para>
/// Enrichers run after capture and before redaction in the pipeline.
/// Implementations must be thread-safe — multiple threads may call
/// <see cref="Enrich"/> concurrently.
/// </para>
/// </summary>
/// <example>
/// Built-in enrichers:
/// <list type="bullet">
///   <item><b>StaticEnricher</b>: service, environment, version, host, region</item>
///   <item><b>CorrelationEnricher</b>: trace_id, span_id from Activity</item>
/// </list>
/// </example>
public interface ILogEventEnricher
{
    /// <summary>
    /// Enriches the given log event in-place with additional properties.
    /// </summary>
    /// <param name="logEvent">The log event to enrich. Must not be <c>null</c>.</param>
    void Enrich(LogEvent logEvent);
}
