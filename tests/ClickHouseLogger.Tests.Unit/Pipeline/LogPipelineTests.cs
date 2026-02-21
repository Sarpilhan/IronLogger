using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core;
using ClickHouseLogger.Core.Diagnostics;
using ClickHouseLogger.Core.Enrichment;
using FluentAssertions;
using Xunit;

namespace ClickHouseLogger.Tests.Unit.Pipeline;

[Collection("InternalLog")] // Uses static InternalLog state
public class LogPipelineTests
{
    private static ClickHouseLoggerOptions CreateOptions() => new()
    {
        Endpoint = "http://localhost:8123",
        Service = "test-svc",
        Environment = "test",
        BatchSize = 5,
        FlushInterval = TimeSpan.FromMilliseconds(50),
        MaxQueueItems = 100,
        FlushOnDisposeTimeout = TimeSpan.FromSeconds(5),
        RedactionEnabled = false
    };

    private static LogEvent CreateEvent(
        LogEventLevel level = LogEventLevel.Information,
        string message = "Test") => new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Message = message,
            Category = "TestCategory"
        };

    // ── Basic Flow ──────────────────────────────────────────────

    [Fact]
    public async Task Pipeline_EnqueueAndDispose_SinkReceivesBatch()
    {
        var options = CreateOptions();
        var sink = new InMemorySink();
        var diagnostics = new DiagnosticsTracker();
        var enricher = new StaticEnricher(options);

        var pipeline = new LogPipeline(options, enricher, null, sink, diagnostics);

        for (var i = 0; i < 3; i++)
            pipeline.Enqueue(CreateEvent(message: $"Event {i}"));

        // Small delay to let background consumer start processing
        await Task.Delay(100);

        // Dispose triggers flush + drain
        await pipeline.DisposeAsync();

        sink.TotalEventCount.Should().Be(3);
        diagnostics.EnqueuedEvents.Should().Be(3);
    }

    [Fact]
    public async Task Pipeline_BatchSizeTrigger_FlushesAutomatically()
    {
        var options = CreateOptions();
        options.BatchSize = 3;
        var sink = new InMemorySink();
        var diagnostics = new DiagnosticsTracker();
        var enricher = new StaticEnricher(options);

        var pipeline = new LogPipeline(options, enricher, null, sink, diagnostics);

        // Enqueue exactly batchSize items
        for (var i = 0; i < 3; i++)
            pipeline.Enqueue(CreateEvent(message: $"Event {i}"));

        // Wait for batch to be processed
        await Task.Delay(500);

        sink.TotalEventCount.Should().BeGreaterOrEqualTo(3);

        await pipeline.DisposeAsync();
    }

    // ── Level Filtering ─────────────────────────────────────────

    [Fact]
    public async Task Pipeline_BelowMinLevel_EventIgnored()
    {
        var options = CreateOptions();
        options.MinLevel = LogEventLevel.Warning;
        var sink = new InMemorySink();
        var diagnostics = new DiagnosticsTracker();
        var enricher = new StaticEnricher(options);

        var pipeline = new LogPipeline(options, enricher, null, sink, diagnostics);

        pipeline.Enqueue(CreateEvent(LogEventLevel.Debug, "should be ignored"));
        pipeline.Enqueue(CreateEvent(LogEventLevel.Information, "also ignored"));
        pipeline.Enqueue(CreateEvent(LogEventLevel.Warning, "should pass"));

        await pipeline.DisposeAsync();

        sink.TotalEventCount.Should().Be(1);
    }

    // ── Enrichment ──────────────────────────────────────────────

    [Fact]
    public async Task Pipeline_EnrichesEvents()
    {
        var options = CreateOptions();
        var sink = new InMemorySink();
        var diagnostics = new DiagnosticsTracker();
        var enricher = new StaticEnricher("my-svc", "prod", "1.0.0", "host-01", null);

        var pipeline = new LogPipeline(options, enricher, null, sink, diagnostics);

        pipeline.Enqueue(CreateEvent());
        await pipeline.DisposeAsync();

        diagnostics.EnqueuedEvents.Should().Be(1);
        sink.TotalEventCount.Should().Be(1);
    }

    // ── Diagnostics Exposure ────────────────────────────────────

    [Fact]
    public async Task Pipeline_ExposedDiagnostics()
    {
        var options = CreateOptions();
        var sink = new InMemorySink();
        var diagnostics = new DiagnosticsTracker();
        var enricher = new StaticEnricher(options);

        var pipeline = new LogPipeline(options, enricher, null, sink, diagnostics);

        pipeline.Diagnostics.Should().BeSameAs(diagnostics);

        await pipeline.DisposeAsync();
    }

    // ── IsLevelDisabled ─────────────────────────────────────────

    [Theory]
    [InlineData(LogEventLevel.Debug, LogEventLevel.Information, true)]
    [InlineData(LogEventLevel.Information, LogEventLevel.Information, false)]
    [InlineData(LogEventLevel.Warning, LogEventLevel.Information, false)]
    public async Task IsLevelDisabled_ReturnsCorrectResult(LogEventLevel eventLevel, LogEventLevel minLevel, bool expected)
    {
        var options = CreateOptions();
        options.MinLevel = minLevel;
        var sink = new InMemorySink();
        var diagnostics = new DiagnosticsTracker();
        var enricher = new StaticEnricher(options);

        var pipeline = new LogPipeline(options, enricher, null, sink, diagnostics);

        pipeline.IsLevelDisabled(eventLevel).Should().Be(expected);

        await pipeline.DisposeAsync();
    }

    // ── Helper Sinks ────────────────────────────────────────────

    /// <summary>Thread-safe event counter sink.</summary>
    private sealed class InMemorySink : ILogEventSink
    {
        private int _totalEventCount;

        public int TotalEventCount => _totalEventCount;

        public Task WriteBatchAsync(ReadOnlyMemory<byte> payload, int eventCount, CancellationToken cancellationToken = default)
        {
            Interlocked.Add(ref _totalEventCount, eventCount);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
