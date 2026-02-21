using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace ClickHouseLogger.AspNetCore.Enrichers;

/// <summary>
/// Enriches log properties with the execution Thread ID.
/// </summary>
public sealed class ThreadIdEnricher : IClickHouseEnricher
{
    /// <inheritdoc />
    public void Enrich(HttpContext context, IDictionary<string, object> properties)
    {
        properties["ThreadId"] = Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture);
    }
}
