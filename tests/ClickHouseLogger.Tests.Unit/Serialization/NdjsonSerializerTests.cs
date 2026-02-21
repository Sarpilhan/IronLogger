using System.Text;
using System.Text.Json;
using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core.Serialization;
using FluentAssertions;
using Xunit;

namespace ClickHouseLogger.Tests.Unit.Serialization;

public class NdjsonSerializerTests
{
    private readonly NdjsonSerializer _serializer = new();

    private static LogEvent CreateEvent(
        LogEventLevel level = LogEventLevel.Information,
        string message = "Test message",
        string category = "TestCategory",
        Dictionary<string, string>? props = null) => new()
        {
            Timestamp = new DateTimeOffset(2026, 2, 22, 10, 30, 45, 123, TimeSpan.Zero),
            Level = level,
            Message = message,
            Category = category,
            Service = "test-service",
            Environment = "test",
            Props = props ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

    // ── Format Validation ───────────────────────────────────────

    [Fact]
    public void Serialize_SingleEvent_ProducesValidNdjson()
    {
        var batch = new[] { CreateEvent() };

        var bytes = _serializer.Serialize(batch);
        var text = Encoding.UTF8.GetString(bytes);

        // Should end with newline
        text.Should().EndWith("\n");

        // Should be valid JSON
        var line = text.TrimEnd('\n');
        var act = () => JsonDocument.Parse(line);
        act.Should().NotThrow();
    }

    [Fact]
    public void Serialize_MultiplEvents_ProducesOneJsonPerLine()
    {
        var batch = new[]
        {
            CreateEvent(message: "Event 1"),
            CreateEvent(message: "Event 2"),
            CreateEvent(message: "Event 3")
        };

        var bytes = _serializer.Serialize(batch);
        var text = Encoding.UTF8.GetString(bytes);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCount(3);

        foreach (var line in lines)
        {
            var act = () => JsonDocument.Parse(line);
            act.Should().NotThrow($"Each line should be valid JSON: {line}");
        }
    }

    [Fact]
    public void Serialize_EmptyBatch_ReturnsEmptyArray()
    {
        var bytes = _serializer.Serialize([]);

        bytes.Should().BeEmpty();
    }

    // ── Required Fields ─────────────────────────────────────────

    [Fact]
    public void Serialize_ContainsAllRequiredFields()
    {
        var batch = new[] { CreateEvent() };
        var doc = DeserializeFirst(batch);

        doc.RootElement.GetProperty("ts").GetString().Should().Be("2026-02-22 10:30:45.123");
        doc.RootElement.GetProperty("level").GetString().Should().Be("Information");
        doc.RootElement.GetProperty("message").GetString().Should().Be("Test message");
        doc.RootElement.GetProperty("category").GetString().Should().Be("TestCategory");
        doc.RootElement.GetProperty("service").GetString().Should().Be("test-service");
        doc.RootElement.GetProperty("env").GetString().Should().Be("test");
    }

    // ── Timestamp Format ────────────────────────────────────────

    [Fact]
    public void Serialize_TimestampFormat_ClickHouseCompatible()
    {
        var evt = CreateEvent();
        evt.Timestamp = new DateTimeOffset(2026, 1, 5, 9, 3, 7, 42, TimeSpan.Zero);

        var doc = DeserializeFirst(new[] { evt });
        var ts = doc.RootElement.GetProperty("ts").GetString();

        ts.Should().Be("2026-01-05 09:03:07.042");
    }

    // ── Level Names ─────────────────────────────────────────────

    [Theory]
    [InlineData(LogEventLevel.Trace, "Trace")]
    [InlineData(LogEventLevel.Debug, "Debug")]
    [InlineData(LogEventLevel.Information, "Information")]
    [InlineData(LogEventLevel.Warning, "Warning")]
    [InlineData(LogEventLevel.Error, "Error")]
    [InlineData(LogEventLevel.Critical, "Critical")]
    [InlineData(LogEventLevel.None, "None")]
    public void Serialize_LevelNames_Correct(LogEventLevel level, string expected)
    {
        var evt = CreateEvent(level: level);
        var doc = DeserializeFirst(new[] { evt });

        doc.RootElement.GetProperty("level").GetString().Should().Be(expected);
    }

    // ── Optional Fields ─────────────────────────────────────────

    [Fact]
    public void Serialize_OptionalFields_OmittedWhenNull()
    {
        var evt = CreateEvent();
        // All optional fields are null by default
        var doc = DeserializeFirst(new[] { evt });

        doc.RootElement.TryGetProperty("template", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("exception", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("trace_id", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("span_id", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("correlation_id", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("version", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("host", out _).Should().BeFalse();
    }

    [Fact]
    public void Serialize_OptionalFields_IncludedWhenSet()
    {
        var evt = CreateEvent();
        evt.Template = "Payment started {OrderId}";
        evt.Exception = "System.Exception: boom";
        evt.TraceId = "abcdef1234567890abcdef1234567890";
        evt.SpanId = "1234567890abcdef";
        evt.CorrelationId = "req-123";
        evt.Version = "1.0.0";
        evt.Host = "web-01";

        var doc = DeserializeFirst(new[] { evt });

        doc.RootElement.GetProperty("template").GetString().Should().Be("Payment started {OrderId}");
        doc.RootElement.GetProperty("exception").GetString().Should().Be("System.Exception: boom");
        doc.RootElement.GetProperty("trace_id").GetString().Should().Be("abcdef1234567890abcdef1234567890");
        doc.RootElement.GetProperty("span_id").GetString().Should().Be("1234567890abcdef");
        doc.RootElement.GetProperty("correlation_id").GetString().Should().Be("req-123");
        doc.RootElement.GetProperty("version").GetString().Should().Be("1.0.0");
        doc.RootElement.GetProperty("host").GetString().Should().Be("web-01");
    }

    // ── Props ────────────────────────────────────────────────────

    [Fact]
    public void Serialize_Props_AsJsonObject()
    {
        var evt = CreateEvent(props: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["orderId"] = "ORD-123",
            ["amount"] = "49.99",
            ["currency"] = "TRY"
        });

        var doc = DeserializeFirst(new[] { evt });
        var props = doc.RootElement.GetProperty("props");

        props.ValueKind.Should().Be(JsonValueKind.Object);
        props.GetProperty("orderId").GetString().Should().Be("ORD-123");
        props.GetProperty("amount").GetString().Should().Be("49.99");
        props.GetProperty("currency").GetString().Should().Be("TRY");
    }

    [Fact]
    public void Serialize_EmptyProps_EmptyObject()
    {
        var evt = CreateEvent();
        var doc = DeserializeFirst(new[] { evt });
        var props = doc.RootElement.GetProperty("props");

        props.ValueKind.Should().Be(JsonValueKind.Object);
        props.EnumerateObject().Should().BeEmpty();
    }

    // ── Exception Truncation ────────────────────────────────────

    [Fact]
    public void Serialize_LongException_Truncated()
    {
        var serializer = new NdjsonSerializer(maxExceptionLength: 50);
        var evt = CreateEvent();
        evt.Exception = new string('X', 200);

        var doc = DeserializeFirst(new[] { evt }, serializer);
        var exception = doc.RootElement.GetProperty("exception").GetString();

        exception.Should().HaveLength(50);
    }

    [Fact]
    public void Serialize_ShortException_NotTruncated()
    {
        var serializer = new NdjsonSerializer(maxExceptionLength: 500);
        var evt = CreateEvent();
        evt.Exception = "Short error";

        var doc = DeserializeFirst(new[] { evt }, serializer);

        doc.RootElement.GetProperty("exception").GetString().Should().Be("Short error");
    }

    // ── Special Characters ──────────────────────────────────────

    [Fact]
    public void Serialize_SpecialCharsInMessage_ProperlyEscaped()
    {
        var evt = CreateEvent(message: "Line1\nLine2\tTabbed \"quoted\"");

        var bytes = _serializer.Serialize(new[] { evt });
        var text = Encoding.UTF8.GetString(bytes);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Should still be one valid JSON object (escape sequences, not raw newlines in JSON values)
        lines.Should().HaveCount(1);

        var doc = JsonDocument.Parse(lines[0]);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Line1\nLine2\tTabbed \"quoted\"");
    }

    [Fact]
    public void Serialize_UnicodeInMessage_Preserved()
    {
        var evt = CreateEvent(message: "Ödeme başarılı — sipariş №12345 🎉");

        var doc = DeserializeFirst(new[] { evt });

        doc.RootElement.GetProperty("message").GetString().Should().Be("Ödeme başarılı — sipariş №12345 🎉");
    }

    // ── Large Batch ─────────────────────────────────────────────

    [Fact]
    public void Serialize_LargeBatch_AllEventsPresent()
    {
        var batch = Enumerable.Range(0, 1000)
            .Select(i => CreateEvent(message: $"Event {i}"))
            .ToList();

        var bytes = _serializer.Serialize(batch);
        var text = Encoding.UTF8.GetString(bytes);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCount(1000);

        // Spot-check first and last
        var first = JsonDocument.Parse(lines[0]);
        first.RootElement.GetProperty("message").GetString().Should().Be("Event 0");

        var last = JsonDocument.Parse(lines[999]);
        last.RootElement.GetProperty("message").GetString().Should().Be("Event 999");
    }

    // ── Helpers ──────────────────────────────────────────────────

    private JsonDocument DeserializeFirst(IReadOnlyList<LogEvent> batch, NdjsonSerializer? serializer = null)
    {
        var bytes = (serializer ?? _serializer).Serialize(batch);
        var text = Encoding.UTF8.GetString(bytes);
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0];
        return JsonDocument.Parse(line);
    }
}
