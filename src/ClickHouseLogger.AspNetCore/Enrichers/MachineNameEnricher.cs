using Microsoft.AspNetCore.Http;

namespace ClickHouseLogger.AspNetCore.Enrichers;

/// <summary>
/// Enriches log properties with the executing machine name.
/// </summary>
public sealed class MachineNameEnricher : IClickHouseEnricher
{
    private static readonly string _cachedMachineName = Environment.MachineName;

    /// <inheritdoc />
    public void Enrich(HttpContext context, IDictionary<string, object> properties)
    {
        properties["MachineName"] = _cachedMachineName;
    }
}
