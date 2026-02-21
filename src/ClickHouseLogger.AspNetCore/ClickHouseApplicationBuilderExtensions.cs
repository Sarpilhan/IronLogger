using Microsoft.AspNetCore.Builder;

namespace ClickHouseLogger.AspNetCore;

/// <summary>
/// Contains extension methods for enabling the <see cref="ClickHouseRequestLoggingMiddleware"/>.
/// </summary>
public static class ClickHouseApplicationBuilderExtensions
{
    /// <summary>
    /// Adds a middleware to the pipeline that logs HTTP requests and their performance to ClickHouse 
    /// via the registered <see cref="Microsoft.Extensions.Logging.ILogger"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IApplicationBuilder"/>.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> so that additional calls can be chained.</returns>
    public static IApplicationBuilder UseClickHouseRequestLogging(this IApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseMiddleware<ClickHouseRequestLoggingMiddleware>();
    }
}
