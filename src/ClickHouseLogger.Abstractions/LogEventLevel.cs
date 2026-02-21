namespace ClickHouseLogger.Abstractions;

/// <summary>
/// Log event severity levels.
/// Mirrors Microsoft.Extensions.Logging.LogLevel for zero-dependency mapping.
/// </summary>
public enum LogEventLevel
{
    /// <summary>Logs that contain the most detailed messages. Disabled by default.</summary>
    Trace = 0,

    /// <summary>Logs used for interactive investigation during development.</summary>
    Debug = 1,

    /// <summary>Logs that track the general flow of the application.</summary>
    Information = 2,

    /// <summary>Logs that highlight an abnormal or unexpected event.</summary>
    Warning = 3,

    /// <summary>Logs that highlight when the current flow of execution stops due to a failure.</summary>
    Error = 4,

    /// <summary>Logs that describe an unrecoverable application or system crash.</summary>
    Critical = 5,

    /// <summary>Not used for writing log messages. Specifies that logging should be disabled.</summary>
    None = 6
}
