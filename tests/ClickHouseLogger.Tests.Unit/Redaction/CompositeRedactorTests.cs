using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core.Redaction;
using FluentAssertions;
using Xunit;

namespace ClickHouseLogger.Tests.Unit.Redaction;

public class CompositeRedactorTests
{
    [Fact]
    public void FromOptions_RedactionEnabled_CreatesRedactor()
    {
        var options = new ClickHouseLoggerOptions
        {
            Endpoint = "http://localhost:8123",
            Service = "svc",
            Environment = "test",
            RedactionEnabled = true
        };

        var redactor = CompositeRedactor.FromOptions(options);

        redactor.Should().NotBeNull();
    }

    [Fact]
    public void FromOptions_RedactionDisabled_ReturnsNull()
    {
        var options = new ClickHouseLoggerOptions
        {
            Endpoint = "http://localhost:8123",
            Service = "svc",
            Environment = "test",
            RedactionEnabled = false
        };

        var redactor = CompositeRedactor.FromOptions(options);

        redactor.Should().BeNull();
    }

    [Fact]
    public void Redact_AppliesKeyThenRegex()
    {
        var options = new ClickHouseLoggerOptions
        {
            Endpoint = "http://localhost:8123",
            Service = "svc",
            Environment = "test",
            RedactionEnabled = true
        };
        var redactor = CompositeRedactor.FromOptions(options)!;

        var evt = new LogEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = LogEventLevel.Information,
            Message = "Contact admin@test.com",
            Category = "Test",
            Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["password"] = "my-secret",
                ["email"] = "user@domain.com",
                ["orderId"] = "ORD-001"
            }
        };

        redactor.Redact(evt);

        // Key-based
        evt.Props["password"].Should().Be("***");
        // Regex-based
        evt.Props["email"].Should().Be("***@***.***");
        // Clean
        evt.Props["orderId"].Should().Be("ORD-001");
        // Message redacted
        evt.Message.Should().Contain("***@***.***");
    }

    [Fact]
    public void FromOptions_EmptyKeysAndPatterns_ReturnsNull()
    {
        var options = new ClickHouseLoggerOptions
        {
            Endpoint = "http://localhost:8123",
            Service = "svc",
            Environment = "test",
            RedactionEnabled = true,
            RedactKeys = new List<string>(),
            RedactPatterns = new List<RedactPattern>()
        };

        var redactor = CompositeRedactor.FromOptions(options);

        redactor.Should().BeNull();
    }
}
