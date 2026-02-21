using System.Threading.Channels;
using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core.Diagnostics;

namespace ClickHouseLogger.Core.Pipeline;

/// <summary>
/// Bounded async queue backed by <see cref="Channel{T}"/>.
/// Implements <see cref="DropPolicy"/> for backpressure control.
/// <para>Multi-producer, single-consumer (<c>SingleReader=true</c>).</para>
/// </summary>
internal sealed class BoundedLogQueue
{
    private readonly Channel<LogEvent> _channel;
    private readonly DropPolicy _dropPolicy;
    private readonly DiagnosticsTracker _diagnostics;

    /// <summary>
    /// Initializes a new bounded queue.
    /// </summary>
    /// <param name="capacity">Maximum items in the queue.</param>
    /// <param name="dropPolicy">Behavior when queue is full.</param>
    /// <param name="diagnostics">Diagnostics tracker for counter updates.</param>
    public BoundedLogQueue(int capacity, DropPolicy dropPolicy, DiagnosticsTracker diagnostics)
    {
        _dropPolicy = dropPolicy;
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));

        // Always use Wait mode — we control drop logic manually via TryWrite.
        // TryWrite returns false when full (non-blocking), letting us apply DropDebugWhenBusy.
        // For BlockWhenFull, EnqueueAsync uses WriteAsync which blocks.
        var options = new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        };

        _channel = Channel.CreateBounded<LogEvent>(options);
    }

    /// <summary>
    /// Current approximate number of items in the queue.
    /// </summary>
    public int Count => _channel.Reader.Count;

    /// <summary>
    /// Attempts to enqueue a log event, applying the configured <see cref="DropPolicy"/>.
    /// </summary>
    /// <param name="logEvent">The event to enqueue.</param>
    /// <returns><c>true</c> if the event was enqueued; <c>false</c> if dropped.</returns>
    public bool TryEnqueue(LogEvent logEvent)
    {
        if (_dropPolicy == DropPolicy.DropDebugWhenBusy)
        {
            return TryEnqueueWithDropPolicy(logEvent);
        }

        // BlockWhenFull: just try / Channel will handle blocking on the write side
        if (_channel.Writer.TryWrite(logEvent))
        {
            _diagnostics.IncrementEnqueued();
            _diagnostics.SetQueueLength(_channel.Reader.Count);
            return true;
        }

        _diagnostics.IncrementDropped();
        return false;
    }

    /// <summary>
    /// Enqueues with blocking (for <see cref="DropPolicy.BlockWhenFull"/>).
    /// </summary>
    /// <param name="logEvent">The event to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask EnqueueAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(logEvent, cancellationToken).ConfigureAwait(false);
        _diagnostics.IncrementEnqueued();
        _diagnostics.SetQueueLength(_channel.Reader.Count);
    }

    /// <summary>
    /// Reads all available items from the queue asynchronously.
    /// </summary>
    public ChannelReader<LogEvent> Reader => _channel.Reader;

    /// <summary>
    /// Signals that no more items will be written to the queue.
    /// </summary>
    public void Complete() => _channel.Writer.TryComplete();

    private bool TryEnqueueWithDropPolicy(LogEvent logEvent)
    {
        // Always try to write
        if (_channel.Writer.TryWrite(logEvent))
        {
            _diagnostics.IncrementEnqueued();
            _diagnostics.SetQueueLength(_channel.Reader.Count);
            return true;
        }

        // Queue is full — apply DropDebugWhenBusy policy
        if (logEvent.Level <= LogEventLevel.Debug)
        {
            // Debug and below: silently drop
            _diagnostics.IncrementDropped();
            InternalLog.Warn($"Dropped {logEvent.Level} event (queue full, DropDebugWhenBusy)");
            return false;
        }

        // Info+: try once more (channel may have drained a slot)
        if (_channel.Writer.TryWrite(logEvent))
        {
            _diagnostics.IncrementEnqueued();
            _diagnostics.SetQueueLength(_channel.Reader.Count);
            return true;
        }

        // Still full — drop with counter
        _diagnostics.IncrementDropped();
        InternalLog.Warn($"Dropped {logEvent.Level} event (queue full, backpressure)");
        return false;
    }
}
