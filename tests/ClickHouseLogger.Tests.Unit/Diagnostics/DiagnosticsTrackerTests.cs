using ClickHouseLogger.Core.Diagnostics;
using FluentAssertions;
using Xunit;

namespace ClickHouseLogger.Tests.Unit.Diagnostics;

public class DiagnosticsTrackerTests
{
    [Fact]
    public void InitialValues_AreZero()
    {
        var tracker = new DiagnosticsTracker();

        tracker.EnqueuedEvents.Should().Be(0);
        tracker.DroppedEvents.Should().Be(0);
        tracker.SentBatches.Should().Be(0);
        tracker.FailedBatches.Should().Be(0);
        tracker.QueueLength.Should().Be(0);
        tracker.LastSendUtc.Should().BeNull();
    }

    [Fact]
    public void IncrementEnqueued_IncrementsCounter()
    {
        var tracker = new DiagnosticsTracker();

        tracker.IncrementEnqueued();
        tracker.IncrementEnqueued();
        tracker.IncrementEnqueued();

        tracker.EnqueuedEvents.Should().Be(3);
    }

    [Fact]
    public void IncrementDropped_SingleAndBulk()
    {
        var tracker = new DiagnosticsTracker();

        tracker.IncrementDropped();
        tracker.IncrementDropped(5);

        tracker.DroppedEvents.Should().Be(6);
    }

    [Fact]
    public void RecordBatchSent_IncrementsAndSetsTimestamp()
    {
        var tracker = new DiagnosticsTracker();
        var before = DateTimeOffset.UtcNow;

        tracker.RecordBatchSent();

        tracker.SentBatches.Should().Be(1);
        tracker.LastSendUtc.Should().NotBeNull();
        tracker.LastSendUtc!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void RecordBatchFailed_IncrementsCounter()
    {
        var tracker = new DiagnosticsTracker();

        tracker.RecordBatchFailed();
        tracker.RecordBatchFailed();

        tracker.FailedBatches.Should().Be(2);
    }

    [Fact]
    public void SetQueueLength_UpdatesValue()
    {
        var tracker = new DiagnosticsTracker();

        tracker.SetQueueLength(42);
        tracker.QueueLength.Should().Be(42);

        tracker.SetQueueLength(0);
        tracker.QueueLength.Should().Be(0);
    }

    [Fact]
    public void ThreadSafety_ConcurrentIncrements()
    {
        var tracker = new DiagnosticsTracker();
        const int iterations = 10_000;

        Parallel.For(0, iterations, _ =>
        {
            tracker.IncrementEnqueued();
            tracker.IncrementDropped();
        });

        tracker.EnqueuedEvents.Should().Be(iterations);
        tracker.DroppedEvents.Should().Be(iterations);
    }
}
