using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core.Diagnostics;
using ClickHouseLogger.Core.Pipeline;
using FluentAssertions;
using Xunit;

namespace ClickHouseLogger.Tests.Unit.Pipeline;

[Collection("InternalLog")] // Shares static InternalLog state
public class BoundedLogQueueTests
{
    private static LogEvent CreateEvent(LogEventLevel level = LogEventLevel.Information) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Level = level,
        Message = "Test",
        Category = "Test"
    };

    // ── Basic Enqueue ───────────────────────────────────────────

    [Fact]
    public void TryEnqueue_SuccessfullyEnqueues()
    {
        var diagnostics = new DiagnosticsTracker();
        var queue = new BoundedLogQueue(100, DropPolicy.DropDebugWhenBusy, diagnostics);

        var result = queue.TryEnqueue(CreateEvent());

        result.Should().BeTrue();
        queue.Count.Should().Be(1);
        diagnostics.EnqueuedEvents.Should().Be(1);
    }

    [Fact]
    public void TryEnqueue_MultipleItems()
    {
        var diagnostics = new DiagnosticsTracker();
        var queue = new BoundedLogQueue(100, DropPolicy.DropDebugWhenBusy, diagnostics);

        for (var i = 0; i < 10; i++)
            queue.TryEnqueue(CreateEvent());

        queue.Count.Should().Be(10);
        diagnostics.EnqueuedEvents.Should().Be(10);
        diagnostics.DroppedEvents.Should().Be(0);
    }

    // ── DropDebugWhenBusy Policy ────────────────────────────────

    [Fact]
    public void TryEnqueue_QueueFull_DropsDebugEvents()
    {
        var diagnostics = new DiagnosticsTracker();
        var queue = new BoundedLogQueue(2, DropPolicy.DropDebugWhenBusy, diagnostics);

        // Fill the queue
        queue.TryEnqueue(CreateEvent());
        queue.TryEnqueue(CreateEvent());

        // Queue full — debug should be dropped
        var result = queue.TryEnqueue(CreateEvent(LogEventLevel.Debug));

        result.Should().BeFalse();
        diagnostics.DroppedEvents.Should().Be(1);
    }

    [Fact]
    public void TryEnqueue_QueueFull_DropsTraceEvents()
    {
        var diagnostics = new DiagnosticsTracker();
        var queue = new BoundedLogQueue(2, DropPolicy.DropDebugWhenBusy, diagnostics);

        queue.TryEnqueue(CreateEvent());
        queue.TryEnqueue(CreateEvent());

        var result = queue.TryEnqueue(CreateEvent(LogEventLevel.Trace));

        result.Should().BeFalse();
        diagnostics.DroppedEvents.Should().Be(1);
    }

    [Fact]
    public void TryEnqueue_QueueFull_InfoEventAlsoDroppedWhenNoSpace()
    {
        var diagnostics = new DiagnosticsTracker();
        var queue = new BoundedLogQueue(2, DropPolicy.DropDebugWhenBusy, diagnostics);

        queue.TryEnqueue(CreateEvent());
        queue.TryEnqueue(CreateEvent());

        // Info+ gets a second try but if still full, it's dropped too
        var result = queue.TryEnqueue(CreateEvent(LogEventLevel.Error));

        result.Should().BeFalse();
        diagnostics.DroppedEvents.Should().Be(1);
    }

    // ── Complete ────────────────────────────────────────────────

    [Fact]
    public void Complete_SignalsNoMoreItems()
    {
        var diagnostics = new DiagnosticsTracker();
        var queue = new BoundedLogQueue(100, DropPolicy.DropDebugWhenBusy, diagnostics);

        queue.TryEnqueue(CreateEvent());
        queue.Complete();

        // Should still be able to read existing items
        queue.Reader.TryRead(out var item).Should().BeTrue();
        item.Should().NotBeNull();
    }

    // ── Diagnostics Updates ─────────────────────────────────────

    [Fact]
    public void TryEnqueue_UpdatesQueueLength()
    {
        var diagnostics = new DiagnosticsTracker();
        var queue = new BoundedLogQueue(100, DropPolicy.DropDebugWhenBusy, diagnostics);

        queue.TryEnqueue(CreateEvent());
        queue.TryEnqueue(CreateEvent());
        queue.TryEnqueue(CreateEvent());

        diagnostics.QueueLength.Should().Be(3);
    }

    // ── Async Enqueue (BlockWhenFull) ───────────────────────────

    [Fact]
    public async Task EnqueueAsync_SuccessfullyEnqueues()
    {
        var diagnostics = new DiagnosticsTracker();
        var queue = new BoundedLogQueue(100, DropPolicy.BlockWhenFull, diagnostics);

        await queue.EnqueueAsync(CreateEvent());

        queue.Count.Should().Be(1);
        diagnostics.EnqueuedEvents.Should().Be(1);
    }
}
