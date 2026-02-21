using Microsoft.AspNetCore.Http;

namespace ClickHouseLogger.AspNetCore.Enrichers;

/// <summary>
/// Defines a contract for enriching HTTP Request logs with additional dynamic properties 
/// before they are routed into the IronLogger ClickHouse pipeline.
/// </summary>
public interface IClickHouseEnricher
{
    /// <summary>
    /// Enriches the property dictionary with additional key-value pairs.
    /// </summary>
    /// <param name="context">The active HTTP context of the request.</param>
    /// <param name="properties">The properties dictionary to be serialized into the logger map.</param>
    void Enrich(HttpContext context, IDictionary<string, object> properties);
}
