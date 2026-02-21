using System.Threading.Channels;
using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core.Pipeline;
using FluentAssertions;
using Xunit;

namespace ClickHouseLogger.Tests.Unit.Pipeline;

public class LogBatcherTests
{
    private static LogEvent CreateEvent(string message = "Test") => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Level = LogEventLevel.Information,
        Message = message,
        Category = "Test"
    };

    // ── Size Trigger ────────────────────────────────────────────

    [Fact]
    public async Task ReadBatchAsync_SizeTrigger_ReturnsBatchWhenFull()
    {
        var channel = Channel.CreateUnbounded<LogEvent>();
        var batcher = new LogBatcher(channel.Reader, batchSize: 3, TimeSpan.FromSeconds(10));

        // Write 5 events
        for (var i = 0; i < 5; i++)
            await channel.Writer.WriteAsync(CreateEvent($"Event {i}"));

        var batch = await batcher.ReadBatchAsync(CancellationToken.None);

        batch.Should().HaveCount(3);
        batch[0].Message.Should().Be("Event 0");
        batch[2].Message.Should().Be("Event 2");
    }

    // ── Time Trigger ────────────────────────────────────────────

    [Fact]
    public async Task ReadBatchAsync_TimeTrigger_FlushesPartialBatch()
    {
        var channel = Channel.CreateUnbounded<LogEvent>();
        var batcher = new LogBatcher(channel.Reader, batchSize: 100, TimeSpan.FromMilliseconds(100));

        // Write fewer than batchSize
        await channel.Writer.WriteAsync(CreateEvent("Event 0"));
        await channel.Writer.WriteAsync(CreateEvent("Event 1"));

        var batch = await batcher.ReadBatchAsync(CancellationToken.None);

        batch.Should().HaveCountGreaterThanOrEqualTo(1);
        batch.Should().HaveCountLessThanOrEqualTo(2);
    }

    // ── Channel Completed ───────────────────────────────────────

    [Fact]
    public async Task ReadBatchAsync_ChannelCompleted_ReturnsRemaining()
    {
        var channel = Channel.CreateUnbounded<LogEvent>();
        var batcher = new LogBatcher(channel.Reader, batchSize: 100, TimeSpan.FromSeconds(10));

        await channel.Writer.WriteAsync(CreateEvent("Last event"));
        channel.Writer.Complete();

        var batch = await batcher.ReadBatchAsync(CancellationToken.None);

        batch.Should().HaveCount(1);
        batch[0].Message.Should().Be("Last event");
    }

    [Fact]
    public async Task ReadBatchAsync_EmptyChannelCompleted_ReturnsEmpty()
    {
        var channel = Channel.CreateUnbounded<LogEvent>();
        var batcher = new LogBatcher(channel.Reader, batchSize: 100, TimeSpan.FromSeconds(10));

        channel.Writer.Complete();

        var batch = await batcher.ReadBatchAsync(CancellationToken.None);

        batch.Should().BeEmpty();
    }

    // ── Cancellation ────────────────────────────────────────────

    [Fact]
    public async Task ReadBatchAsync_Cancellation_ReturnsDrainedItems()
    {
        var channel = Channel.CreateUnbounded<LogEvent>();
        var batcher = new LogBatcher(channel.Reader, batchSize: 100, TimeSpan.FromSeconds(10));

        await channel.Writer.WriteAsync(CreateEvent("Before cancel"));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        var batch = await batcher.ReadBatchAsync(cts.Token);

        // Should have drained at least the one item before cancellation
        batch.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    // ── DrainRemaining ──────────────────────────────────────────

    [Fact]
    public async Task DrainRemaining_ReturnsAllQueuedItems()
    {
        var channel = Channel.CreateUnbounded<LogEvent>();
        var batcher = new LogBatcher(channel.Reader, batchSize: 100, TimeSpan.FromSeconds(10));

        for (var i = 0; i < 5; i++)
            await channel.Writer.WriteAsync(CreateEvent($"Item {i}"));

        channel.Writer.Complete();

        var remaining = batcher.DrainRemaining();

        remaining.Should().HaveCount(5);
    }

    // ── Constructor Validation ──────────────────────────────────

    [Fact]
    public void Constructor_InvalidBatchSize_Throws()
    {
        var channel = Channel.CreateUnbounded<LogEvent>();

        var act = () => new LogBatcher(channel.Reader, batchSize: 0, TimeSpan.FromSeconds(1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NullReader_Throws()
    {
        var act = () => new LogBatcher(null!, batchSize: 100, TimeSpan.FromSeconds(1));

        act.Should().Throw<ArgumentNullException>();
    }
}
