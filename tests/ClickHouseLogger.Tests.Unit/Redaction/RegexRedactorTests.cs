using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core.Redaction;
using FluentAssertions;
using Xunit;

namespace ClickHouseLogger.Tests.Unit.Redaction;

public class RegexRedactorTests
{
    private static LogEvent CreateEvent(
        string message = "Test",
        Dictionary<string, string>? props = null) => new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = LogEventLevel.Information,
            Message = message,
            Category = "Test",
            Props = props ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

    [Fact]
    public void Redact_MasksEmailInProps()
    {
        var redactor = new RegexRedactor(Defaults.RedactPatterns);
        var evt = CreateEvent(props: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["contact"] = "user@example.com"
        });

        redactor.Redact(evt);

        evt.Props["contact"].Should().Be("***@***.***");
    }

    [Fact]
    public void Redact_MasksCreditCardInProps()
    {
        var redactor = new RegexRedactor(Defaults.RedactPatterns);
        var evt = CreateEvent(props: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["card"] = "4111 1111 1111 1111"
        });

        redactor.Redact(evt);

        evt.Props["card"].Should().Be("****-****-****-****");
    }

    [Fact]
    public void Redact_MasksCreditCardWithDashes()
    {
        var redactor = new RegexRedactor(Defaults.RedactPatterns);
        var evt = CreateEvent(props: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["card"] = "4111-1111-1111-1111"
        });

        redactor.Redact(evt);

        evt.Props["card"].Should().Be("****-****-****-****");
    }

    [Fact]
    public void Redact_MasksTcknInProps()
    {
        var redactor = new RegexRedactor(Defaults.RedactPatterns);
        var evt = CreateEvent(props: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["nationalId"] = "12345678901"
        });

        redactor.Redact(evt);

        evt.Props["nationalId"].Should().Be("***********");
    }

    [Fact]
    public void Redact_MasksTrIbanInProps()
    {
        var redactor = new RegexRedactor(Defaults.RedactPatterns);
        var evt = CreateEvent(props: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["iban"] = "TR123456789012345678901234"
        });

        redactor.Redact(evt);

        evt.Props["iban"].Should().Be("TR**************************");
    }

    [Fact]
    public void Redact_MasksEmailInMessage()
    {
        var redactor = new RegexRedactor(Defaults.RedactPatterns);
        var evt = CreateEvent(message: "Contact us at admin@company.com for help");

        redactor.Redact(evt);

        evt.Message.Should().Be("Contact us at ***@***.*** for help");
    }

    [Fact]
    public void Redact_PreservesCleanValues()
    {
        var redactor = new RegexRedactor(Defaults.RedactPatterns);
        var evt = CreateEvent(props: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["orderId"] = "ORD-12345",
            ["status"] = "completed"
        });

        redactor.Redact(evt);

        evt.Props["orderId"].Should().Be("ORD-12345");
        evt.Props["status"].Should().Be("completed");
    }

    [Fact]
    public void Redact_EmptyProps_DoesNothing()
    {
        var redactor = new RegexRedactor(Defaults.RedactPatterns);
        var evt = CreateEvent();

        redactor.Redact(evt);

        evt.Props.Should().BeEmpty();
    }

    [Fact]
    public void Redact_NoPatterns_DoesNothing()
    {
        var redactor = new RegexRedactor([]);
        var evt = CreateEvent(props: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["email"] = "user@example.com"
        });

        redactor.Redact(evt);

        evt.Props["email"].Should().Be("user@example.com");
    }

    [Fact]
    public void Redact_MultipleMatchesInSameValue()
    {
        var redactor = new RegexRedactor(Defaults.RedactPatterns);
        var evt = CreateEvent(props: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["contacts"] = "a@b.com and c@d.com"
        });

        redactor.Redact(evt);

        evt.Props["contacts"].Should().Be("***@***.*** and ***@***.***");
    }
}
