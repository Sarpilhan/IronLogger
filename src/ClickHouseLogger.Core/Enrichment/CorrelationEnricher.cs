using System.Diagnostics;
using ClickHouseLogger.Abstractions;

namespace ClickHouseLogger.Core.Enrichment;

/// <summary>
/// Enriches a <see cref="LogEvent"/> with distributed tracing correlation data
/// from <see cref="Activity.Current"/>:
/// <list type="bullet">
///   <item><see cref="LogEvent.TraceId"/> — W3C trace-id</item>
///   <item><see cref="LogEvent.SpanId"/> — W3C span-id</item>
/// </list>
/// <para>
/// <see cref="LogEvent.CorrelationId"/> is <b>not</b> set here — that is the
/// responsibility of the ASP.NET Core middleware enricher which reads
/// <c>HttpContext.TraceIdentifier</c>.
/// </para>
/// <para>Thread-safe — reads only from ambient <see cref="Activity.Current"/>.</para>
/// </summary>
internal sealed class CorrelationEnricher : ILogEventEnricher
{
    /// <summary>Shared singleton instance (stateless, safe to reuse).</summary>
    internal static readonly CorrelationEnricher Instance = new();

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var activity = Activity.Current;
        if (activity is null)
            return;

        var traceId = activity.TraceId.ToString();
        if (traceId is not "00000000000000000000000000000000")
            logEvent.TraceId = traceId;

        var spanId = activity.SpanId.ToString();
        if (spanId is not "0000000000000000")
            logEvent.SpanId = spanId;
    }
}
