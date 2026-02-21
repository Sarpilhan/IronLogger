using ClickHouseLogger.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace ClickHouseLogger.Extensions.Logging;

/// <summary>
/// Extension methods for registering the ClickHouse logger provider
/// with <see cref="ILoggingBuilder"/>.
/// </summary>
public static class ClickHouseLoggingExtensions
{
    /// <summary>
    /// Adds the ClickHouse logger provider to the logging pipeline.
    /// <para>
    /// Usage:
    /// <code>
    /// builder.Logging.AddClickHouse(o =>
    /// {
    ///     o.Endpoint = "http://localhost:8123";
    ///     o.Service = "my-api";
    ///     o.Environment = "prod";
    /// });
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="configure">Action to configure <see cref="ClickHouseLoggerOptions"/>.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddClickHouse(
        this ILoggingBuilder builder,
        Action<ClickHouseLoggerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ClickHouseLoggerOptions();
        configure(options);

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider>(new ClickHouseLoggerProvider(options)));

        return builder;
    }

    /// <summary>
    /// Adds the ClickHouse logger provider with default options.
    /// You must set at least <see cref="ClickHouseLoggerOptions.Endpoint"/>,
    /// <see cref="ClickHouseLoggerOptions.Service"/>, and
    /// <see cref="ClickHouseLoggerOptions.Environment"/>.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="endpoint">ClickHouse HTTP endpoint URL.</param>
    /// <param name="service">Service name for log enrichment.</param>
    /// <param name="environment">Environment name (prod, staging, dev).</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddClickHouse(
        this ILoggingBuilder builder,
        string endpoint,
        string service,
        string environment)
    {
        return builder.AddClickHouse(o =>
        {
            o.Endpoint = endpoint;
            o.Service = service;
            o.Environment = environment;
        });
    }
}
