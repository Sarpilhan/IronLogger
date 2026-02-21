using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core;
using ClickHouseLogger.Core.Diagnostics;
using ClickHouseLogger.Core.Enrichment;
using ClickHouseLogger.Extensions.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using MELClickHouseLogger = global::ClickHouseLogger.Extensions.Logging.ClickHouseLogger;

namespace ClickHouseLogger.Tests.Unit.MEL;

public class ClickHouseLoggerTests
{
    private class InMemorySink : ILogEventSink
    {
        public List<byte[]> WrittenBatches { get; } = new();
        public int TotalEventCount { get; private set; }

        public Task WriteBatchAsync(ReadOnlyMemory<byte> payload, int eventCount, CancellationToken cancellationToken)
        {
            WrittenBatches.Add(payload.ToArray());
            TotalEventCount += eventCount;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => default;
    }

    private ClickHouseLoggerOptions CreateOptions() => new()
    {
        Endpoint = "http://localhost:8123",
        Service = "TestService",
        Environment = "Testing",
        BatchSize = 10,
        FlushInterval = TimeSpan.FromSeconds(1)
    };

    [Fact]
    public void Log_WhenEnabled_EnqueuesEventToPipeline()
    {
        // Arrange
        var options = CreateOptions();
        var sink = new InMemorySink();
        var diagnostics = new DiagnosticsTracker();
        var enricher = new StaticEnricher(options);
        var pipeline = new LogPipeline(options, enricher, null, sink, diagnostics);

        var logger = new MELClickHouseLogger("TestCategory", pipeline, options.MaxPropsDepth);

        // Act
        logger.LogInformation("This is a test message {UserId}", 42);

        // Assert
        diagnostics.EnqueuedEvents.Should().Be(1);
    }

    [Fact]
    public void Log_WhenDisabled_DoesNotEnqueueEvent()
    {
        // Arrange
        var options = CreateOptions();
        options.MinLevel = LogEventLevel.Error; // Requires Error or Critical
        var sink = new InMemorySink();
        var diagnostics = new DiagnosticsTracker();
        var enricher = new StaticEnricher(options);
        var pipeline = new LogPipeline(options, enricher, null, sink, diagnostics);

        var logger = new MELClickHouseLogger("TestCategory", pipeline, options.MaxPropsDepth);

        // Act
        logger.LogWarning("This warning should be ignored");

        // Assert
        diagnostics.EnqueuedEvents.Should().Be(0);
    }

    [Fact]
    public async Task Log_WritesFormattedMessageAndProperties()
    {
        // Arrange
        var options = CreateOptions();
        var sink = new InMemorySink();
        var diagnostics = new DiagnosticsTracker();
        var enricher = new StaticEnricher(options);
        var pipeline = new LogPipeline(options, enricher, null, sink, diagnostics);

        var logger = new MELClickHouseLogger("TestCategory", pipeline, options.MaxPropsDepth);
        var eventId = new EventId(999, "UserLogin");

        // Act
        logger.LogInformation(eventId, "User {UserId} logged in from {IpAddress}", 42, "127.0.0.1");

        // Wait to process and dispose
        await Task.Delay(50);
        await pipeline.DisposeAsync();

        // Assert
        sink.WrittenBatches.Should().ContainSingle();
        var json = Encoding.UTF8.GetString(sink.WrittenBatches[0]);

        json.Should().Contain("\"message\":\"User 42 logged in from 127.0.0.1\"");
        json.Should().Contain("\"category\":\"TestCategory\"");
        json.Should().Contain("\"level\":\"Information\"");

        // Props verification
        json.Should().Contain("\"UserId\":\"42\"");
        json.Should().Contain("\"IpAddress\":\"127.0.0.1\"");
        json.Should().Contain("\"EventId\":\"999\"");
        json.Should().Contain("\"EventName\":\"UserLogin\"");
    }

    [Fact]
    public async Task Log_WithBeginScope_IncludesScopeProperties()
    {
        // Arrange
        var options = CreateOptions();
        var sink = new InMemorySink();
        var diagnostics = new DiagnosticsTracker();
        var enricher = new StaticEnricher(options);
        var pipeline = new LogPipeline(options, enricher, null, sink, diagnostics);

        var logger = new MELClickHouseLogger("TestCategory", pipeline, options.MaxPropsDepth);

        // Act
        using (logger.BeginScope(new Dictionary<string, object> { { "TenantId", "T-123" }, { "Correlation", "abc-123" } }))
        {
            using (logger.BeginScope(new Dictionary<string, object> { { "InnerScopeProp", "InnerValue" } }))
            {
                logger.LogWarning("Processing request");
            }
        }

        // Wait to process and dispose
        await Task.Delay(50);
        await pipeline.DisposeAsync();

        // Assert
        var json = Encoding.UTF8.GetString(sink.WrittenBatches[0]);

        // Props verification from scopes
        json.Should().Contain("\"TenantId\":\"T-123\"");
        json.Should().Contain("\"Correlation\":\"abc-123\"");
        json.Should().Contain("\"InnerScopeProp\":\"InnerValue\"");
    }
}
