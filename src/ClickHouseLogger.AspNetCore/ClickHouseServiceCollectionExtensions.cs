using ClickHouseLogger.AspNetCore.Enrichers;
using Microsoft.Extensions.DependencyInjection;

namespace ClickHouseLogger.AspNetCore;

/// <summary>
/// Extension methods for configuring ClickHouse HTTP Request Logger enrichments via <see cref="IServiceCollection"/>.
/// </summary>
public static class ClickHouseServiceCollectionExtensions
{
    /// <summary>
    /// Adds the built-in IronLogger HTTP Request log enrichers: MachineName, ProcessId, and ThreadId.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddClickHouseRequestEnrichers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IClickHouseEnricher, MachineNameEnricher>();
        services.AddSingleton<IClickHouseEnricher, ProcessIdEnricher>();
        services.AddSingleton<IClickHouseEnricher, ThreadIdEnricher>();

        return services;
    }
    
    /// <summary>
    /// Adds a custom HTTP Request log enricher.
    /// </summary>
    /// <typeparam name="TEnricher">The type of the enricher.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddClickHouseRequestEnricher<TEnricher>(this IServiceCollection services)
        where TEnricher : class, IClickHouseEnricher
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IClickHouseEnricher, TEnricher>();
        return services;
    }
}
