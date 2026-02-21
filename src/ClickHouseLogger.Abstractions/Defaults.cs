using System.Text.RegularExpressions;

namespace ClickHouseLogger.Abstractions;

/// <summary>
/// Default values for <see cref="ClickHouseLoggerOptions"/>.
/// These are the recommended production defaults documented in the README.
/// </summary>
public static class Defaults
{
    // ── Performance ──────────────────────────────────────────────

    /// <summary>Number of events per batch before flush. Default: 2000.</summary>
    public const int BatchSize = 2_000;

    /// <summary>Maximum time between flushes. Default: 1 second.</summary>
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);

    /// <summary>Maximum events in the bounded queue. Default: 200,000.</summary>
    public const int MaxQueueItems = 200_000;

    /// <summary>Default compression mode. Default: Gzip.</summary>
    public const ClickHouseCompression Compression = ClickHouseCompression.Gzip;

    // ── Logging Control ──────────────────────────────────────────

    /// <summary>Minimum log level to accept. Default: Information.</summary>
    public const LogEventLevel MinLevel = LogEventLevel.Information;

    /// <summary>Queue full behavior. Default: DropDebugWhenBusy.</summary>
    public const DropPolicy DropPolicy = Abstractions.DropPolicy.DropDebugWhenBusy;

    // ── Retry ────────────────────────────────────────────────────

    /// <summary>Maximum retry attempts per batch. Default: 5.</summary>
    public const int MaxRetries = 5;

    /// <summary>Base delay for exponential backoff in milliseconds. Default: 200 ms.</summary>
    public const int BaseDelayMs = 200;

    /// <summary>Maximum delay cap for exponential backoff in milliseconds. Default: 5000 ms.</summary>
    public const int MaxDelayMs = 5_000;

    // ── HTTP ─────────────────────────────────────────────────────

    /// <summary>HTTP request timeout. Default: 10 seconds.</summary>
    public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

    // ── Redaction ────────────────────────────────────────────────

    /// <summary>Whether PII redaction is enabled. Default: true.</summary>
    public const bool RedactionEnabled = true;

    /// <summary>
    /// Default key names whose values are masked with <c>***</c>.
    /// Case-insensitive matching.
    /// </summary>
    public static readonly IReadOnlyList<string> RedactKeys = new[]
    {
        "password",
        "token",
        "authorization",
        "secret",
        "apikey",
        "iban",
        "tckn"
    };

    /// <summary>
    /// Default regex patterns for value-based PII redaction.
    /// Applied only to string values in <see cref="LogEvent.Props"/>.
    /// </summary>
    public static readonly IReadOnlyList<RedactPattern> RedactPatterns = new[]
    {
        new RedactPattern("Email",       new Regex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled), "***@***.***"),
        new RedactPattern("CreditCard",  new Regex(@"\b\d{4}[\s\-]?\d{4}[\s\-]?\d{4}[\s\-]?\d{4}\b",    RegexOptions.Compiled), "****-****-****-****"),
        new RedactPattern("TCKN",        new Regex(@"\b\d{11}\b",                                         RegexOptions.Compiled), "***********"),
        new RedactPattern("TR_IBAN",     new Regex(@"\bTR\d{24}\b",                                       RegexOptions.Compiled), "TR**************************"),
    };

    // ── Limits ───────────────────────────────────────────────────

    /// <summary>Maximum exception string length in bytes. Default: 8192 (8 KB).</summary>
    public const int MaxExceptionLength = 8_192;

    /// <summary>Maximum object graph depth when serializing complex props values to JSON. Default: 4.</summary>
    public const int MaxPropsDepth = 4;

    /// <summary>Timeout for flushing remaining events during dispose/shutdown. Default: 5 seconds.</summary>
    public static readonly TimeSpan FlushOnDisposeTimeout = TimeSpan.FromSeconds(5);

    // ── Database ─────────────────────────────────────────────────

    /// <summary>Default ClickHouse database name.</summary>
    public const string Database = "observability";

    /// <summary>Default ClickHouse table name.</summary>
    public const string Table = "app_logs";
}
