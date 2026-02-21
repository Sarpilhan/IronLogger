using System.Security.Claims;
using ClickHouseLogger.AspNetCore.Enrichers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClickHouseLogger.AspNetCore.Tests;

public class ClickHouseRequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenRequestSuccessful_LogsInformationWithScope()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";
        context.Response.StatusCode = 200;

        var mockLogger = new MockLogger();

        var middleware = new ClickHouseRequestLoggingMiddleware(
            innerContext => Task.CompletedTask,
            mockLogger,
            Array.Empty<IClickHouseEnricher>());

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        mockLogger.LatestLevel.Should().Be(LogLevel.Information);
        mockLogger.Scopes.Should().NotBeEmpty();

        var scopeProps = mockLogger.Scopes.Last() as Dictionary<string, object>;
        scopeProps.Should().NotBeNull();
        scopeProps!["RequestMethod"].Should().Be("GET");
        scopeProps["RequestPath"].Should().Be("/api/test");
        scopeProps["StatusCode"].Should().Be(200);
        scopeProps.ContainsKey("ElapsedMs").Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenExceptionThrown_LogsErrorAndRethrows()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/fail";
        
        var expectedException = new InvalidOperationException("Test crash");
        var mockLogger = new MockLogger();

        var middleware = new ClickHouseRequestLoggingMiddleware(
            innerContext => throw expectedException,
            mockLogger,
            Array.Empty<IClickHouseEnricher>());

        // Act
        var act = () => middleware.InvokeAsync(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Test crash");

        mockLogger.LatestLevel.Should().Be(LogLevel.Error);
        mockLogger.LatestException.Should().Be(expectedException);

        var scopeProps = mockLogger.Scopes.Last() as Dictionary<string, object>;
        scopeProps!["StatusCode"].Should().Be(500); // Translated from default 200 since exception occurred
    }

    [Fact]
    public async Task InvokeAsync_WithEnrichers_ExecutesEnrichersSafely()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var mockLogger = new MockLogger();
        
        var mockEnricher1 = new Mock<IClickHouseEnricher>();
        mockEnricher1.Setup(e => e.Enrich(It.IsAny<HttpContext>(), It.IsAny<IDictionary<string, object>>()))
            .Callback<HttpContext, IDictionary<string, object>>((c, p) => p["TestKey1"] = "TestValue1");

        var badEnricher = new Mock<IClickHouseEnricher>();
        badEnricher.Setup(e => e.Enrich(It.IsAny<HttpContext>(), It.IsAny<IDictionary<string, object>>()))
            .Throws(new DivideByZeroException("Enricher failed"));

        var middleware = new ClickHouseRequestLoggingMiddleware(
            innerContext => Task.CompletedTask,
            mockLogger,
            new[] { mockEnricher1.Object, badEnricher.Object });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var scopeProps = mockLogger.Scopes.Last() as Dictionary<string, object>;
        scopeProps!["TestKey1"].Should().Be("TestValue1");
        
        // Ensure the middleware did not crash and logged the warning.
        // The first log is the warning about the bad enricher. The second log is the Info success.
        mockLogger.LatestLevel.Should().Be(LogLevel.Information);
    }
}

// A simple mock logger to capture state without complex Moq generic setups over Logger extensions.
internal class MockLogger : ILogger<ClickHouseRequestLoggingMiddleware>
{
    public LogLevel LatestLevel { get; private set; }
    public Exception? LatestException { get; private set; }
    public List<object> Scopes { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        Scopes.Add(state);
        return new DummyDisposable();
    }

    public bool IsEnabled(LogLevel LogLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LatestLevel = logLevel;
        LatestException = exception;
    }

    private class DummyDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
