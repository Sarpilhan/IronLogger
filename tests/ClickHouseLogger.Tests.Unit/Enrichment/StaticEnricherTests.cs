using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core.Enrichment;
using FluentAssertions;
using Xunit;

namespace ClickHouseLogger.Tests.Unit.Enrichment;

public class StaticEnricherTests
{
    private static LogEvent CreateEvent() => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Level = LogEventLevel.Information,
        Message = "Test message",
        Category = "TestCategory"
    };

    [Fact]
    public void Enrich_SetsRequiredFields()
    {
        var enricher = new StaticEnricher("payment-api", "prod", "1.0.0", "web-01", "eu-west");
        var evt = CreateEvent();

        enricher.Enrich(evt);

        evt.Service.Should().Be("payment-api");
        evt.Environment.Should().Be("prod");
        evt.Version.Should().Be("1.0.0");
        evt.Host.Should().Be("web-01");
        evt.Props.Should().ContainKey("region").WhoseValue.Should().Be("eu-west");
    }

    [Fact]
    public void Enrich_OmitsNullVersion()
    {
        var enricher = new StaticEnricher("svc", "dev", null, "host-1", null);
        var evt = CreateEvent();

        enricher.Enrich(evt);

        evt.Version.Should().BeNull();
        evt.Props.Should().NotContainKey("region");
    }

    [Fact]
    public void Enrich_DoesNotOverrideExistingRegion()
    {
        var enricher = new StaticEnricher("svc", "prod", null, "host-1", "us-east");
        var evt = CreateEvent();
        evt.Props["region"] = "already-set";

        enricher.Enrich(evt);

        evt.Props["region"].Should().Be("already-set");
    }

    [Fact]
    public void Enrich_FromOptions_UseMachineName()
    {
        var options = new ClickHouseLoggerOptions
        {
            Endpoint = "http://localhost:8123",
            Service = "test-svc",
            Environment = "test",
            Version = "2.0.0"
        };
        var enricher = new StaticEnricher(options);
        var evt = CreateEvent();

        enricher.Enrich(evt);

        evt.Service.Should().Be("test-svc");
        evt.Host.Should().Be(System.Environment.MachineName);
    }

    [Fact]
    public void Enrich_ThrowsOnNullEvent()
    {
        var enricher = new StaticEnricher("svc", "dev", null, "host", null);

        var act = () => enricher.Enrich(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
