using ClickHouseLogger.Core.Diagnostics;
using FluentAssertions;
using Xunit;

namespace ClickHouseLogger.Tests.Unit.Diagnostics;

[Collection("InternalLog")] // Disable parallel execution — InternalLog uses static state

public class InternalLogTests
{
    [Fact]
    public void SetCallback_RoutesLogToCallback()
    {
        var logs = new List<(string Level, string Message, Exception? Ex)>();
        InternalLog.SetCallback((level, msg, ex) => logs.Add((level, msg, ex)));

        try
        {
            InternalLog.Info("test info");
            InternalLog.Warn("test warn");
            InternalLog.Error("test error", new InvalidOperationException("boom"));

            logs.Should().HaveCount(3);
            logs[0].Should().Be(("Info", "test info", null));
            logs[1].Level.Should().Be("Warn");
            logs[2].Level.Should().Be("Error");
            logs[2].Ex.Should().BeOfType<InvalidOperationException>();
        }
        finally
        {
            InternalLog.SetCallback(null); // cleanup
        }
    }

    [Fact]
    public void SetCallback_Null_ResetsToDefault()
    {
        InternalLog.SetCallback(null);

        // Should not throw — falls through to Trace.WriteLine
        var act = () => InternalLog.Info("default output");

        act.Should().NotThrow();
    }

    [Fact]
    public void Callback_Exception_IsSuppressed()
    {
        InternalLog.SetCallback((_, _, _) => throw new Exception("callback boom"));

        try
        {
            var act = () => InternalLog.Error("should not throw");

            act.Should().NotThrow();
        }
        finally
        {
            InternalLog.SetCallback(null);
        }
    }
}
