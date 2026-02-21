using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace ClickHouseLogger.AspNetCore.Enrichers;

/// <summary>
/// Enriches log properties with the host process ID.
/// </summary>
public sealed class ProcessIdEnricher : IClickHouseEnricher
{
    private static readonly string _cachedProcessId = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public void Enrich(HttpContext context, IDictionary<string, object> properties)
    {
        properties["ProcessId"] = _cachedProcessId;
    }
}
