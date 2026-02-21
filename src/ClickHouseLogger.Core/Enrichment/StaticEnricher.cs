using ClickHouseLogger.Abstractions;

namespace ClickHouseLogger.Core.Enrichment;

/// <summary>
/// Enriches every <see cref="LogEvent"/> with static dimensions from configuration:
/// <c>Service</c>, <c>Environment</c>, <c>Version</c>, <c>Host</c>, and <c>Region</c>.
/// <para>Thread-safe — all state is read-only after construction.</para>
/// </summary>
internal sealed class StaticEnricher : ILogEventEnricher
{
    private readonly string _service;
    private readonly string _environment;
    private readonly string? _version;
    private readonly string _host;
    private readonly string? _region;

    /// <summary>
    /// Initializes a new <see cref="StaticEnricher"/> from the given options.
    /// </summary>
    /// <param name="options">Logger configuration containing static dimension values.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is <c>null</c>.</exception>
    public StaticEnricher(ClickHouseLoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _service = options.Service;
        _environment = options.Environment;
        _version = options.Version;
        _host = System.Environment.MachineName;
        _region = options.Region;
    }

    /// <summary>
    /// Initializes a new <see cref="StaticEnricher"/> with explicit values (for testing).
    /// </summary>
    internal StaticEnricher(string service, string environment, string? version, string host, string? region)
    {
        _service = service;
        _environment = environment;
        _version = version;
        _host = host;
        _region = region;
    }

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        logEvent.Service = _service;
        logEvent.Environment = _environment;
        logEvent.Host = _host;

        if (_version is not null)
            logEvent.Version = _version;

        if (_region is not null && !logEvent.Props.ContainsKey("region"))
            logEvent.Props["region"] = _region;
    }
}
