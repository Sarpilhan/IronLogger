using ClickHouseLogger.Abstractions;

namespace ClickHouseLogger.Core.Enrichment;

/// <summary>
/// Chains multiple <see cref="ILogEventEnricher"/> instances and applies them in order.
/// <para>Thread-safe if all wrapped enrichers are thread-safe.</para>
/// </summary>
internal sealed class CompositeEnricher : ILogEventEnricher
{
    private readonly ILogEventEnricher[] _enrichers;

    /// <summary>
    /// Initializes a new <see cref="CompositeEnricher"/> with the given enricher chain.
    /// </summary>
    /// <param name="enrichers">Enrichers to apply in order.</param>
    public CompositeEnricher(params ILogEventEnricher[] enrichers)
    {
        _enrichers = enrichers ?? throw new ArgumentNullException(nameof(enrichers));
    }

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent)
    {
        for (var i = 0; i < _enrichers.Length; i++)
        {
            _enrichers[i].Enrich(logEvent);
        }
    }
}
