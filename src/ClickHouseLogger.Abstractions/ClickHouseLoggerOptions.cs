using System.Net.Http;

namespace ClickHouseLogger.Abstractions;

/// <summary>
/// Configuration options for the IronLogger ClickHouse logging pipeline.
/// <para>
/// All properties have sensible defaults (see <see cref="Defaults"/>).
/// Only <see cref="Endpoint"/>, <see cref="Service"/>, and <see cref="Environment"/>
/// are required to be set explicitly.
/// </para>
/// </summary>
/// <example>
/// <code>
/// builder.Logging.AddClickHouse(o =>
/// {
///     o.Endpoint = "https://clickhouse.mycorp.com:8443";
///     o.Service = "payment-api";
///     o.Environment = "prod";
/// });
/// </code>
/// </example>
public sealed class ClickHouseLoggerOptions
{
    // ── Connection / Target ──────────────────────────────────────

    /// <summary>
    /// ClickHouse HTTP(S) base URL (e.g., <c>https://clickhouse.mycorp.com:8443</c>).
    /// <b>Required.</b>
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>ClickHouse database name. Default: <c>observability</c>.</summary>
    public string Database { get; set; } = Defaults.Database;

    /// <summary>ClickHouse table name. Default: <c>app_logs</c>.</summary>
    public string Table { get; set; } = Defaults.Table;

    /// <summary>
    /// ClickHouse user for Basic Auth. Mutually exclusive with <see cref="AuthToken"/>.
    /// </summary>
    public string? User { get; set; }

    /// <summary>
    /// ClickHouse password for Basic Auth. Mutually exclusive with <see cref="AuthToken"/>.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Bearer token for token-based authentication.
    /// Sent as <c>Authorization: Bearer {token}</c>.
    /// Mutually exclusive with <see cref="User"/>/<see cref="Password"/>.
    /// </summary>
    public string? AuthToken { get; set; }

    // ── Performance ──────────────────────────────────────────────

    /// <summary>Number of events per batch before automatic flush. Default: 2000.</summary>
    public int BatchSize { get; set; } = Defaults.BatchSize;

    /// <summary>Maximum time between flushes. Default: 1 second.</summary>
    public TimeSpan FlushInterval { get; set; } = Defaults.FlushInterval;

    /// <summary>Maximum events in the bounded queue. Default: 200,000.</summary>
    public int MaxQueueItems { get; set; } = Defaults.MaxQueueItems;

    /// <summary>HTTP body compression mode. Default: Gzip.</summary>
    public ClickHouseCompression Compression { get; set; } = Defaults.Compression;

    // ── Logging Control ──────────────────────────────────────────

    /// <summary>Minimum severity level to accept. Events below this level are ignored. Default: Information.</summary>
    public LogEventLevel MinLevel { get; set; } = Defaults.MinLevel;

    /// <summary>Behavior when the internal queue is full. Default: DropDebugWhenBusy.</summary>
    public DropPolicy DropPolicy { get; set; } = Defaults.DropPolicy;

    // ── Static Dimensions ────────────────────────────────────────

    /// <summary>
    /// Service name added to every log event. <b>Required.</b>
    /// </summary>
    public string Service { get; set; } = string.Empty;

    /// <summary>
    /// Environment name added to every log event (e.g., prod, staging, dev). <b>Required.</b>
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>Application version added to every log event. Optional.</summary>
    public string? Version { get; set; }

    /// <summary>Deployment region added to every log event. Optional.</summary>
    public string? Region { get; set; }

    // ── Redaction ────────────────────────────────────────────────

    /// <summary>Enable PII key + regex redaction. Default: true.</summary>
    public bool RedactionEnabled { get; set; } = Defaults.RedactionEnabled;

    /// <summary>
    /// Key names whose values are replaced with <c>***</c> (case-insensitive).
    /// Default: password, token, authorization, secret, apiKey, iban, tckn.
    /// </summary>
    public IList<string> RedactKeys { get; set; } = new List<string>(Defaults.RedactKeys);

    /// <summary>
    /// Regex patterns applied to string values for PII masking.
    /// Default patterns: email, credit card, TCKN, TR IBAN.
    /// </summary>
    public IList<RedactPattern> RedactPatterns { get; set; } = new List<RedactPattern>(Defaults.RedactPatterns);

    // ── Retry ────────────────────────────────────────────────────

    /// <summary>Maximum retry attempts per failed batch. Default: 5.</summary>
    public int MaxRetries { get; set; } = Defaults.MaxRetries;

    /// <summary>Base delay for exponential backoff in milliseconds. Default: 200.</summary>
    public int BaseDelayMs { get; set; } = Defaults.BaseDelayMs;

    /// <summary>Maximum delay cap for exponential backoff in milliseconds. Default: 5000.</summary>
    public int MaxDelayMs { get; set; } = Defaults.MaxDelayMs;

    // ── HTTP ─────────────────────────────────────────────────────

    /// <summary>HTTP request timeout for ClickHouse calls. Default: 10 seconds.</summary>
    public TimeSpan HttpTimeout { get; set; } = Defaults.HttpTimeout;

    /// <summary>
    /// Optional custom <see cref="HttpMessageHandler"/> for the internal <see cref="HttpClient"/>.
    /// Useful for testing (mock handler) or proxy scenarios.
    /// </summary>
    public HttpMessageHandler? HttpMessageHandler { get; set; }

    // ── Limits ───────────────────────────────────────────────────

    /// <summary>Maximum exception string length in characters. Default: 8192.</summary>
    public int MaxExceptionLength { get; set; } = Defaults.MaxExceptionLength;

    /// <summary>Maximum object graph depth for complex props serialization. Default: 4.</summary>
    public int MaxPropsDepth { get; set; } = Defaults.MaxPropsDepth;

    /// <summary>Timeout for flushing remaining events during shutdown. Default: 5 seconds.</summary>
    public TimeSpan FlushOnDisposeTimeout { get; set; } = Defaults.FlushOnDisposeTimeout;

    // ── Callbacks ────────────────────────────────────────────────

    /// <summary>
    /// Optional callback invoked when a batch fails after all retries.
    /// <para>
    /// ⚠️ This callback runs on the pipeline's background thread.
    /// Keep it fast and non-blocking. Do not throw exceptions.
    /// </para>
    /// </summary>
    public BatchFailedCallback? OnBatchFailed { get; set; }

    // ── Validation ───────────────────────────────────────────────

    /// <summary>
    /// Validates the options and throws <see cref="ArgumentException"/> if invalid.
    /// Called automatically during provider initialization.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when required options are missing or invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
            throw new ArgumentException("Endpoint is required.", nameof(Endpoint));

        if (string.IsNullOrWhiteSpace(Service))
            throw new ArgumentException("Service is required.", nameof(Service));

        if (string.IsNullOrWhiteSpace(Environment))
            throw new ArgumentException("Environment is required.", nameof(Environment));

        if (!string.IsNullOrEmpty(AuthToken) && !string.IsNullOrEmpty(User))
            throw new ArgumentException("AuthToken and User/Password are mutually exclusive. Use one or the other.");

        if (BatchSize <= 0)
            throw new ArgumentException("BatchSize must be > 0.", nameof(BatchSize));

        if (FlushInterval <= TimeSpan.Zero)
            throw new ArgumentException("FlushInterval must be positive.", nameof(FlushInterval));

        if (MaxQueueItems <= 0)
            throw new ArgumentException("MaxQueueItems must be > 0.", nameof(MaxQueueItems));

        if (MaxRetries < 0)
            throw new ArgumentException("MaxRetries must be >= 0.", nameof(MaxRetries));

        if (BaseDelayMs <= 0)
            throw new ArgumentException("BaseDelayMs must be > 0.", nameof(BaseDelayMs));

        if (MaxDelayMs < BaseDelayMs)
            throw new ArgumentException("MaxDelayMs must be >= BaseDelayMs.", nameof(MaxDelayMs));

        if (HttpTimeout <= TimeSpan.Zero)
            throw new ArgumentException("HttpTimeout must be positive.", nameof(HttpTimeout));

        if (MaxExceptionLength <= 0)
            throw new ArgumentException("MaxExceptionLength must be > 0.", nameof(MaxExceptionLength));

        if (MaxPropsDepth <= 0)
            throw new ArgumentException("MaxPropsDepth must be > 0.", nameof(MaxPropsDepth));
    }
}
