using ClickHouseLogger.Abstractions;

namespace ClickHouseLogger.Core.Redaction;

/// <summary>
/// Applies compiled regex patterns to string values in <see cref="LogEvent.Props"/>
/// and the <see cref="LogEvent.Message"/> to mask PII data.
/// <para>
/// Default patterns detect: email addresses, credit card numbers, TCKN, TR IBAN.
/// Only string values are processed — non-string values are ignored.
/// </para>
/// <para>Thread-safe — patterns are frozen after construction and <c>RegexOptions.Compiled</c>.</para>
/// </summary>
internal sealed class RegexRedactor : ILogEventRedactor
{
    private readonly RedactPattern[] _patterns;

    /// <summary>
    /// Initializes a new <see cref="RegexRedactor"/> with the given patterns.
    /// </summary>
    /// <param name="patterns">Compiled regex patterns with replacement masks.</param>
    public RegexRedactor(IEnumerable<RedactPattern> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        _patterns = patterns.ToArray();
    }

    /// <inheritdoc />
    public void Redact(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (_patterns.Length == 0)
            return;

        // Redact message
        if (!string.IsNullOrEmpty(logEvent.Message))
        {
            logEvent.Message = ApplyPatterns(logEvent.Message);
        }

        // Redact props values
        if (logEvent.Props.Count == 0)
            return;

        // Collect keys that need updating to avoid dictionary modification during enumeration
        List<KeyValuePair<string, string>>? updates = null;

        foreach (var kvp in logEvent.Props)
        {
            if (string.IsNullOrEmpty(kvp.Value))
                continue;

            var redacted = ApplyPatterns(kvp.Value);
            if (!ReferenceEquals(redacted, kvp.Value))
            {
                updates ??= [];
                updates.Add(new KeyValuePair<string, string>(kvp.Key, redacted));
            }
        }

        if (updates is not null)
        {
            foreach (var update in updates)
            {
                logEvent.Props[update.Key] = update.Value;
            }
        }
    }

    private string ApplyPatterns(string value)
    {
        var result = value;
        for (var i = 0; i < _patterns.Length; i++)
        {
            var pattern = _patterns[i];
            result = pattern.Pattern.Replace(result, pattern.Replacement);
        }

        return result;
    }
}
