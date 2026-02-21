namespace ClickHouseLogger.Abstractions;

/// <summary>
/// Represents a single structured log event flowing through the IronLogger pipeline.
/// <para>
/// This is the central data model: captured from <c>ILogger.Log()</c>, enriched with
/// correlation/static dimensions, redacted for PII, serialized to NDJSON, and sent to ClickHouse.
/// </para>
/// </summary>
public sealed class LogEvent
{
    /// <summary>UTC timestamp of the event. Maps to ClickHouse <c>DateTime64(3)</c>.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Severity level of the log event.</summary>
    public LogEventLevel Level { get; set; }

    /// <summary>Rendered (formatted) log message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Logger category name (typically the fully qualified class name).
    /// Maps to ClickHouse <c>category</c> column.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Service name from static configuration. Required.</summary>
    public string Service { get; set; } = string.Empty;

    /// <summary>Environment name from static configuration (e.g., prod, staging). Required.</summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// Original message template with placeholders (e.g., <c>"Payment started {OrderId}"</c>).
    /// Optional — may be empty if not available.
    /// </summary>
    public string? Template { get; set; }

    /// <summary>
    /// Full exception string including message, type, and stack trace.
    /// Truncated to <see cref="Defaults.MaxExceptionLength"/> bytes.
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>W3C Trace ID from <see cref="System.Diagnostics.Activity"/>.</summary>
    public string? TraceId { get; set; }

    /// <summary>W3C Span ID from <see cref="System.Diagnostics.Activity"/>.</summary>
    public string? SpanId { get; set; }

    /// <summary>
    /// Application-level correlation ID. Typically <c>HttpContext.TraceIdentifier</c>
    /// in ASP.NET Core scenarios.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>Application version from static configuration.</summary>
    public string? Version { get; set; }

    /// <summary>Host/machine name.</summary>
    public string? Host { get; set; }

    /// <summary>
    /// Structured properties extracted from MEL state/scope.
    /// <para>
    /// All values are string-normalized before insertion:
    /// <list type="bullet">
    ///   <item>Primitives → <c>ToString(InvariantCulture)</c></item>
    ///   <item>Complex objects → JSON string (max depth: <see cref="Defaults.MaxPropsDepth"/>)</item>
    ///   <item><c>null</c> → empty string</item>
    /// </list>
    /// </para>
    /// <para>Redaction is applied before values enter this dictionary.</para>
    /// </summary>
    public Dictionary<string, string> Props { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
