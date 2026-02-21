using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core.Redaction;
using FluentAssertions;
using Xunit;

namespace ClickHouseLogger.Tests.Unit.Redaction;

public class KeyBasedRedactorTests
{
    private static LogEvent CreateEvent(Dictionary<string, string>? props = null) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Level = LogEventLevel.Information,
        Message = "Test",
        Category = "Test",
        Props = props ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    };

    [Fact]
    public void Redact_MasksSensitiveKeys()
    {
        var redactor = new KeyBasedRedactor(Defaults.RedactKeys);
        var evt = CreateEvent(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["userId"] = "12345",
            ["password"] = "my-secret-pass",
            ["token"] = "abc123",
            ["orderId"] = "ORD-999"
        });

        redactor.Redact(evt);

        evt.Props["password"].Should().Be("***");
        evt.Props["token"].Should().Be("***");
        evt.Props["userId"].Should().Be("12345");
        evt.Props["orderId"].Should().Be("ORD-999");
    }

    [Fact]
    public void Redact_IsCaseInsensitive()
    {
        var redactor = new KeyBasedRedactor(new[] { "password", "TOKEN" });
        var evt = CreateEvent(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PASSWORD"] = "secret",
            ["token"] = "abc",
            ["Authorization"] = "Bearer xyz"
        });

        redactor.Redact(evt);

        evt.Props["PASSWORD"].Should().Be("***");
        evt.Props["token"].Should().Be("***");
        evt.Props["Authorization"].Should().Be("Bearer xyz"); // not in key list
    }

    [Fact]
    public void Redact_EmptyProps_DoesNothing()
    {
        var redactor = new KeyBasedRedactor(Defaults.RedactKeys);
        var evt = CreateEvent();

        redactor.Redact(evt);

        evt.Props.Should().BeEmpty();
    }

    [Fact]
    public void Redact_CustomKeyList()
    {
        var redactor = new KeyBasedRedactor(new[] { "ssn", "creditcard" });
        var evt = CreateEvent(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ssn"] = "123-45-6789",
            ["creditcard"] = "4111111111111111",
            ["name"] = "John Doe"
        });

        redactor.Redact(evt);

        evt.Props["ssn"].Should().Be("***");
        evt.Props["creditcard"].Should().Be("***");
        evt.Props["name"].Should().Be("John Doe");
    }

    [Fact]
    public void Redact_ThrowsOnNullEvent()
    {
        var redactor = new KeyBasedRedactor(Defaults.RedactKeys);

        var act = () => redactor.Redact(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
