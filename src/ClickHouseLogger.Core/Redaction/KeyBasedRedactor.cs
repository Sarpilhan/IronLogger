using ClickHouseLogger.Abstractions;

namespace ClickHouseLogger.Core.Redaction;

/// <summary>
/// Masks values in <see cref="LogEvent.Props"/> whose keys match
/// a configured set of sensitive key names (case-insensitive).
/// <para>
/// Matched values are replaced with <c>***</c>.
/// Default keys: password, token, authorization, secret, apiKey, iban, tckn.
/// </para>
/// <para>Thread-safe — the key set is frozen after construction.</para>
/// </summary>
internal sealed class KeyBasedRedactor : ILogEventRedactor
{
    private const string Mask = "***";
    private readonly HashSet<string> _sensitiveKeys;

    /// <summary>
    /// Initializes a new <see cref="KeyBasedRedactor"/> with the given sensitive key names.
    /// </summary>
    /// <param name="sensitiveKeys">Key names to mask (case-insensitive matching).</param>
    public KeyBasedRedactor(IEnumerable<string> sensitiveKeys)
    {
        ArgumentNullException.ThrowIfNull(sensitiveKeys);
        _sensitiveKeys = new HashSet<string>(sensitiveKeys, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public void Redact(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (logEvent.Props.Count == 0)
            return;

        // Collect keys to redact (avoid modifying dictionary during enumeration)
        foreach (var kvp in logEvent.Props)
        {
            if (_sensitiveKeys.Contains(kvp.Key))
            {
                logEvent.Props[kvp.Key] = Mask;
            }
        }
    }
}
