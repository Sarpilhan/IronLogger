using ClickHouseLogger.Abstractions;

namespace ClickHouseLogger.Core.Redaction;

/// <summary>
/// Chains <see cref="KeyBasedRedactor"/> and <see cref="RegexRedactor"/>
/// into a single <see cref="ILogEventRedactor"/>.
/// <para>Key-based redaction runs first (faster), then regex patterns.</para>
/// <para>Thread-safe if all wrapped redactors are thread-safe.</para>
/// </summary>
internal sealed class CompositeRedactor : ILogEventRedactor
{
    private readonly ILogEventRedactor[] _redactors;

    /// <summary>
    /// Initializes a new <see cref="CompositeRedactor"/> with the given redactor chain.
    /// </summary>
    /// <param name="redactors">Redactors to apply in order.</param>
    public CompositeRedactor(params ILogEventRedactor[] redactors)
    {
        _redactors = redactors ?? throw new ArgumentNullException(nameof(redactors));
    }

    /// <summary>
    /// Creates a <see cref="CompositeRedactor"/> from <see cref="ClickHouseLoggerOptions"/>.
    /// Returns <c>null</c> if redaction is disabled.
    /// </summary>
    /// <param name="options">Logger configuration.</param>
    /// <returns>A composite redactor, or <c>null</c> if <see cref="ClickHouseLoggerOptions.RedactionEnabled"/> is <c>false</c>.</returns>
    public static CompositeRedactor? FromOptions(ClickHouseLoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.RedactionEnabled)
            return null;

        var redactors = new List<ILogEventRedactor>(2);

        if (options.RedactKeys.Count > 0)
            redactors.Add(new KeyBasedRedactor(options.RedactKeys));

        if (options.RedactPatterns.Count > 0)
            redactors.Add(new RegexRedactor(options.RedactPatterns));

        return redactors.Count > 0 ? new CompositeRedactor(redactors.ToArray()) : null;
    }

    /// <inheritdoc />
    public void Redact(LogEvent logEvent)
    {
        for (var i = 0; i < _redactors.Length; i++)
        {
            _redactors[i].Redact(logEvent);
        }
    }
}
