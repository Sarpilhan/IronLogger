using System.Collections.Concurrent;
using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core;
using ClickHouseLogger.Core.Diagnostics;
using ClickHouseLogger.Core.Enrichment;
using ClickHouseLogger.Core.Redaction;
using ClickHouseLogger.Sinks.ClickHouse;
using Microsoft.Extensions.Logging;

namespace ClickHouseLogger.Extensions.Logging;

/// <summary>
/// <see cref="ILoggerProvider"/> that creates <see cref="ClickHouseLogger"/> instances
/// backed by the IronLogger pipeline.
/// <para>
/// Owns the <see cref="LogPipeline"/> lifecycle:
/// pipeline is created during construction and disposed during <see cref="DisposeAsync"/>.
/// </para>
/// <para>
/// Register via <c>builder.Logging.AddClickHouse(options => { ... })</c>.
/// </para>
/// </summary>
[ProviderAlias("ClickHouse")]
public sealed class ClickHouseLoggerProvider : ILoggerProvider, IAsyncDisposable
{
    private readonly LogPipeline _pipeline;
    private readonly ClickHouseLoggerOptions _options;
    private readonly ConcurrentDictionary<string, ClickHouseLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>
    /// Initializes the provider, creating the full pipeline:
    /// Enricher → Redactor → Queue → Batcher → Serializer → ClickHouseHttpSink.
    /// </summary>
    /// <param name="options">Logger configuration.</param>
    public ClickHouseLoggerProvider(ClickHouseLoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _options = options;

        // ── Build pipeline components ───────────────────────────────────

        var diagnostics = new DiagnosticsTracker();

        // Enrichers
        var staticEnricher = new StaticEnricher(options);
        var correlationEnricher = new CorrelationEnricher();
        var enricher = new CompositeEnricher(staticEnricher, correlationEnricher);

        // Redactor (optional)
        ILogEventRedactor? redactor = null;
        if (options.RedactionEnabled)
        {
            redactor = CompositeRedactor.FromOptions(options);
        }

        // Sink
        var sink = new ClickHouseHttpSink(options, diagnostics);

        // Assemble pipeline
        _pipeline = new LogPipeline(options, enricher, redactor, sink, diagnostics);

        InternalLog.Info($"ClickHouseLoggerProvider initialized (service={options.Service}, env={options.Environment})");
    }

    /// <summary>
    /// Internal constructor for testing with a pre-built pipeline.
    /// </summary>
    internal ClickHouseLoggerProvider(ClickHouseLoggerOptions options, LogPipeline pipeline)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    /// <summary>
    /// Provides read-only access to pipeline diagnostics counters.
    /// </summary>
    public IDiagnosticsSnapshot Diagnostics => _pipeline.Diagnostics;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName,
            name => new ClickHouseLogger(name, _pipeline, _options.MaxPropsDepth));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Trigger async dispose synchronously (MEL calls Dispose, not DisposeAsync)
        // The host will call DisposeAsync if properly registered as IAsyncDisposable
        _pipeline.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _pipeline.DisposeAsync().ConfigureAwait(false);
    }
}
