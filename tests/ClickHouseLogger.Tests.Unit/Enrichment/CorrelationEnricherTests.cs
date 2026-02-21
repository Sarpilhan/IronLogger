using System.Diagnostics;
using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core.Enrichment;
using FluentAssertions;
using Xunit;

namespace ClickHouseLogger.Tests.Unit.Enrichment;

public class CorrelationEnricherTests
{
    private static LogEvent CreateEvent() => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Level = LogEventLevel.Information,
        Message = "Test",
        Category = "Test"
    };

    [Fact]
    public void Enrich_WithActiveActivity_SetsTraceAndSpanId()
    {
        using var activity = new Activity("test-operation");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        var enricher = CorrelationEnricher.Instance;
        var evt = CreateEvent();

        enricher.Enrich(evt);

        evt.TraceId.Should().NotBeNullOrEmpty();
        evt.TraceId.Should().HaveLength(32); // W3C trace-id is 32 hex chars
        evt.SpanId.Should().NotBeNullOrEmpty();
        evt.SpanId.Should().HaveLength(16); // W3C span-id is 16 hex chars
    }

    [Fact]
    public void Enrich_WithoutActivity_LeavesFieldsNull()
    {
        // Ensure no ambient Activity
        Activity.Current = null;

        var enricher = CorrelationEnricher.Instance;
        var evt = CreateEvent();

        enricher.Enrich(evt);

        evt.TraceId.Should().BeNull();
        evt.SpanId.Should().BeNull();
    }

    [Fact]
    public void Enrich_DoesNotSetCorrelationId()
    {
        // CorrelationId is set by ASP.NET Core middleware, not this enricher
        using var activity = new Activity("test-op");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        var enricher = CorrelationEnricher.Instance;
        var evt = CreateEvent();

        enricher.Enrich(evt);

        evt.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void Enrich_ThrowsOnNullEvent()
    {
        var enricher = CorrelationEnricher.Instance;

        var act = () => enricher.Enrich(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Instance_IsSingleton()
    {
        CorrelationEnricher.Instance.Should().BeSameAs(CorrelationEnricher.Instance);
    }
}
