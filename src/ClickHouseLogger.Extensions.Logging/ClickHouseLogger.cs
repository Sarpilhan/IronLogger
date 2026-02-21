using System.Globalization;
using System.Text.Json;
using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core;
using Microsoft.Extensions.Logging;

namespace ClickHouseLogger.Extensions.Logging;

/// <summary>
/// ILogger implementation that captures structured log events and forwards them
/// to the IronLogger pipeline for batching and delivery to ClickHouse.
/// <para>
/// Instances are created per-category by <see cref="ClickHouseLoggerProvider"/>.
/// Thread-safe — <see cref="Log{TState}"/> may be called concurrently from multiple threads.
/// </para>
/// </summary>
internal sealed class ClickHouseLogger : ILogger
{
    private readonly string _category;
    private readonly LogPipeline _pipeline;
    private readonly int _maxPropsDepth;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>Async-local scope stack for structured scope property propagation.</summary>
    private static readonly AsyncLocal<ScopeState?> CurrentScope = new();

    public ClickHouseLogger(string category, LogPipeline pipeline, int maxPropsDepth)
    {
        _category = category;
        _pipeline = pipeline;
        _maxPropsDepth = maxPropsDepth;
        _jsonOptions = new JsonSerializerOptions
        {
            MaxDepth = maxPropsDepth,
            WriteIndented = false
        };
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        var scope = new ScopeState(state, CurrentScope.Value);
        CurrentScope.Value = scope;
        return scope;
    }

    /// <inheritdoc />
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        if (logLevel == Microsoft.Extensions.Logging.LogLevel.None)
            return false;

        return !_pipeline.IsLevelDisabled(MapLevel(logLevel));
    }

    /// <inheritdoc />
    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        ArgumentNullException.ThrowIfNull(formatter);

        var message = formatter(state, exception);

        var logEvent = new LogEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = MapLevel(logLevel),
            Message = message,
            Category = _category,
            Exception = exception?.ToString()
        };

        // Extract template if available
        if (state is IReadOnlyList<KeyValuePair<string, object?>> stateValues)
        {
            ExtractProperties(logEvent.Props, stateValues);
        }

        // Extract scope properties
        ExtractScopeProperties(logEvent.Props);

        // Add EventId if present
        if (eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name))
        {
            logEvent.Props["EventId"] = eventId.Id.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(eventId.Name))
                logEvent.Props["EventName"] = eventId.Name;
        }

        _pipeline.Enqueue(logEvent);
    }

    /// <summary>
    /// Maps MEL LogLevel to our internal LogEventLevel.
    /// Both enums share the same ordinal values by design.
    /// </summary>
    internal static LogEventLevel MapLevel(Microsoft.Extensions.Logging.LogLevel logLevel) =>
        (LogEventLevel)(int)logLevel;

    /// <summary>
    /// Extracts structured properties from MEL state key-value pairs into the Props dictionary.
    /// </summary>
    private void ExtractProperties(Dictionary<string, string> props, IReadOnlyList<KeyValuePair<string, object?>> stateValues)
    {
        for (var i = 0; i < stateValues.Count; i++)
        {
            var kvp = stateValues[i];

            // Skip the "{OriginalFormat}" meta key — store it as Template on LogEvent
            if (kvp.Key == "{OriginalFormat}")
                continue;

            props[kvp.Key] = FormatValue(kvp.Value);
        }
    }

    /// <summary>
    /// Walks the scope chain and extracts all scope properties into the Props dictionary.
    /// Outer scopes are added first; inner scopes override on key collision.
    /// </summary>
    private void ExtractScopeProperties(Dictionary<string, string> props)
    {
        // Collect scopes in order (outermost first)
        var current = CurrentScope.Value;
        if (current is null)
            return;

        // Build list from innermost to outermost
        var scopes = new List<ScopeState>();
        while (current is not null)
        {
            scopes.Add(current);
            current = current.Parent;
        }

        // Process outermost first so inner scopes can override
        for (var i = scopes.Count - 1; i >= 0; i--)
        {
            var scope = scopes[i];
            if (scope.State is IEnumerable<KeyValuePair<string, object?>> nullableDict)
            {
                foreach (var kvp in nullableDict)
                {
                    if (kvp.Key == "{OriginalFormat}")
                        continue;
                    props[kvp.Key] = FormatValue(kvp.Value);
                }
            }
            else if (scope.State is IEnumerable<KeyValuePair<string, object>> notNullableDict)
            {
                foreach (var kvp in notNullableDict)
                {
                    if (kvp.Key == "{OriginalFormat}")
                        continue;
                    props[kvp.Key] = FormatValue(kvp.Value);
                }
            }
            else if (scope.State is not null)
            {
                // Scalar scope (e.g., BeginScope("processing order"))
                props[$"Scope_{i}"] = scope.State.ToString() ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// Formats a property value to a string suitable for ClickHouse Map(String, String).
    /// </summary>
    private string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s,
            bool b => b ? "true" : "false",
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => SerializeComplex(value)
        };
    }

    /// <summary>
    /// Serializes complex objects to JSON string with depth limiting.
    /// </summary>
    private string SerializeComplex(object value)
    {
        try
        {
            return JsonSerializer.Serialize(value, _jsonOptions);
        }
        catch
        {
            return value.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Lightweight scope state linked list node.
    /// Uses AsyncLocal for cross-async-boundary propagation.
    /// </summary>
    private sealed class ScopeState : IDisposable
    {
        public object? State { get; }
        public ScopeState? Parent { get; }

        public ScopeState(object? state, ScopeState? parent)
        {
            State = state;
            Parent = parent;
        }

        public void Dispose()
        {
            // Pop this scope — restore parent
            CurrentScope.Value = Parent;
        }
    }
}
