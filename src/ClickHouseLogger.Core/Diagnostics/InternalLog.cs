using System.Diagnostics;

namespace ClickHouseLogger.Core.Diagnostics;

/// <summary>
/// Internal self-diagnostics logger for the IronLogger pipeline.
/// <para>
/// ⚠️ This is NOT for user log events. This logs the pipeline's own errors
/// (batch failures, queue overflows, startup/shutdown) without circular dependency.
/// </para>
/// <para>
/// Default output: <c>Trace.WriteLine</c> and <c>Debug.WriteLine</c>.
/// Override with <see cref="SetCallback"/> for custom routing.
/// </para>
/// </summary>
internal static class InternalLog
{
    private static Action<string, string, Exception?>? _callback;

    /// <summary>
    /// Sets a custom callback for internal diagnostics. Pass <c>null</c> to reset to default.
    /// </summary>
    /// <param name="callback">Callback receiving (level, message, exception?).</param>
    public static void SetCallback(Action<string, string, Exception?>? callback)
    {
        _callback = callback;
    }

    /// <summary>Logs an informational internal message.</summary>
    public static void Info(string message) => Log("Info", message, null);

    /// <summary>Logs a warning internal message.</summary>
    public static void Warn(string message, Exception? ex = null) => Log("Warn", message, ex);

    /// <summary>Logs an error internal message.</summary>
    public static void Error(string message, Exception? ex = null) => Log("Error", message, ex);

    private static void Log(string level, string message, Exception? ex)
    {
        var formatted = ex is null
            ? $"[IronLogger] [{level}] {message}"
            : $"[IronLogger] [{level}] {message} — {ex.GetType().Name}: {ex.Message}";

        if (_callback is not null)
        {
            try
            {
                _callback(level, message, ex);
            }
            catch
            {
                // Never throw from internal diagnostics
            }

            return;
        }

        Trace.WriteLine(formatted);
        System.Diagnostics.Debug.WriteLine(formatted);
    }
}
