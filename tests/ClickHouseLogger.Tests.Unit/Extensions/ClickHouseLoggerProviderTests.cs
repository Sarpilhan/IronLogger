using System;
using System.Linq;
using System.Threading.Tasks;
using ClickHouseLogger.Abstractions;
using ClickHouseLogger.Core;
using ClickHouseLogger.Extensions.Logging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using MELClickHouseLogger = global::ClickHouseLogger.Extensions.Logging.ClickHouseLogger;

namespace ClickHouseLogger.Tests.Unit.MEL;

// Force sequential execution with InternalLog since Provider initializes LogPipeline.
[Collection("InternalLog")]
public class ClickHouseLoggerProviderTests
{
    private ClickHouseLoggerOptions CreateOptions() => new()
    {
        Endpoint = "http://localhost:8123",
        Service = "TestService",
        Environment = "Testing"
    };

    [Fact]
    public void AddClickHouse_RegistersProvider_AsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddLogging(builder =>
        {
            builder.AddClickHouse(options =>
            {
                options.Endpoint = "http://localhost:8123";
                options.Service = "TestService";
                options.Environment = "Testing";
            });
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var providers = serviceProvider.GetServices<ILoggerProvider>().ToList();

        providers.Should().ContainSingle(p => p is ClickHouseLoggerProvider);
        providers.OfType<ClickHouseLoggerProvider>().Single().Should().BeSameAs(
            serviceProvider.GetServices<ILoggerProvider>().OfType<ClickHouseLoggerProvider>().Single()
        );
    }

    [Fact]
    public void AddClickHouse_OverloadWithBasicArgs_RegistersProviderCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddLogging(builder =>
        {
            builder.AddClickHouse("http://localhost:8123", "TestService", "Testing");
        });

        // Assert
        var provider = services.BuildServiceProvider().GetServices<ILoggerProvider>().OfType<ClickHouseLoggerProvider>().FirstOrDefault();
        provider.Should().NotBeNull();
    }

    [Fact]
    public void CreateLogger_ReturnsClickHouseLogger_Instance()
    {
        // Arrange
        var provider = new ClickHouseLoggerProvider(CreateOptions());

        // Act
        var logger = provider.CreateLogger("TestApp.Controllers.HomeController");

        // Assert
        logger.Should().NotBeNull();
        logger.Should().BeOfType<MELClickHouseLogger>();
    }

    [Fact]
    public void CreateLogger_WithSameCategory_ReturnsSameInstance()
    {
        // Arrange
        var provider = new ClickHouseLoggerProvider(CreateOptions());

        // Act
        var logger1 = provider.CreateLogger("TestApp.Controllers.HomeController");
        var logger2 = provider.CreateLogger("TestApp.Controllers.HomeController");

        // Assert
        logger1.Should().BeSameAs(logger2);
    }

    [Fact]
    public async Task DisposeAsync_DisposesPipelineWithoutThrowing()
    {
        // Arrange
        var provider = new ClickHouseLoggerProvider(CreateOptions());

        // Act & Assert
        // We ensure that disposing it normally doesn't leave lingering problems.
        await provider.DisposeAsync();

        // Already disposed calls shouldn't throw
        await provider.DisposeAsync();
    }
}
