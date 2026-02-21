using System.Diagnostics;
using System.Security.Claims;
using ClickHouseLogger.AspNetCore.Enrichers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ClickHouseLogger.AspNetCore;

/// <summary>
/// A high-performance request logging middleware tailored for the IronLogger pipeline.
/// Injects HTTP metrics such as URL, Method, Status Code, Elapsed, TraceId and generic claims.
/// </summary>
public sealed partial class ClickHouseRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ClickHouseRequestLoggingMiddleware> _logger;
    private readonly IClickHouseEnricher[] _enrichers;

    /// <summary>
    /// Initializes a new instance of <see cref="ClickHouseRequestLoggingMiddleware"/>.
    /// </summary>
    public ClickHouseRequestLoggingMiddleware(
        RequestDelegate next, 
        ILogger<ClickHouseRequestLoggingMiddleware> logger,
        IEnumerable<IClickHouseEnricher> enrichers)
    {
        _next = next;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _enrichers = enrichers?.ToArray() ?? [];
    }

    /// <summary>
    /// Executes the middleware, wrapping the next delegate execution with logging diagnostics.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var start = Stopwatch.GetTimestamp();
        Exception? caughtException = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            caughtException = ex;
            throw;
        }
        finally
        {
            // The finally block evaluates successfully logged requests and short-circuited exceptions alike.
            LogRequest(context, start, caughtException);
        }
    }

    private void LogRequest(HttpContext context, long startTimestamp, Exception? exception)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        
        // If an exception bubbled up, ASP.NET Core hasn't modified StatusCode to 500 yet.
        var statusCode = exception != null && context.Response.StatusCode == 200 ? 500 : context.Response.StatusCode;

        var requestPath = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;

        var props = new Dictionary<string, object>
        {
            ["RequestMethod"] = method,
            ["RequestPath"] = requestPath,
            ["StatusCode"] = statusCode,
            ["ElapsedMs"] = elapsedMs,
            ["TraceId"] = Activity.Current?.Id ?? context.TraceIdentifier
        };

        if (context.Request.QueryString.HasValue)
        {
            props["QueryString"] = context.Request.QueryString.Value!;
        }

        if (context.Connection.RemoteIpAddress != null)
        {
            props["RemoteIp"] = context.Connection.RemoteIpAddress.ToString();
        }

        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            props["UserId"] = userId;
        }

        foreach (var enricher in _enrichers)
        {
            try
            {
                enricher.Enrich(context, props);
            }
            catch (Exception ex)
            {
                // Never halt the application for an enrichment failure
                LogEnrichmentError(_logger, enricher.GetType().Name, ex);
            }
        }

        // Scope pushes all these HTTP-context dimensions down into ClickHouseLogger's Map<String, String>.
        using (_logger.BeginScope(props))
        {
            if (exception != null || statusCode >= 500)
            {
                LogHttpRequestError(_logger, method, requestPath, statusCode, elapsedMs, exception);
            }
            else
            {
                LogHttpRequestSuccess(_logger, method, requestPath, statusCode, elapsedMs);
            }
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:F2} ms")]
    private static partial void LogHttpRequestSuccess(ILogger logger, string method, string path, int statusCode, double elapsedMs);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:F2} ms")]
    private static partial void LogHttpRequestError(ILogger logger, string method, string path, int statusCode, double elapsedMs, Exception? exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "HTTP Logger property enricher {EnricherName} threw an exception. Skiping this enricher for current log.")]
    private static partial void LogEnrichmentError(ILogger logger, string enricherName, Exception? exception);
}
