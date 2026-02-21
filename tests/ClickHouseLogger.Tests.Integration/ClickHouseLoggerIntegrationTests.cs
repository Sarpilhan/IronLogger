using System;
using System.Data;
using System.Threading.Tasks;
using ClickHouse.Client.ADO;
using ClickHouseLogger.Extensions.Logging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ClickHouseLogger.Tests.Integration;

[CollectionDefinition("ClickHouseIntegration")]
public class ClickHouseIntegrationCollection : ICollectionFixture<ClickHouseFixture> { }

[Collection("ClickHouseIntegration")]
public class ClickHouseLoggerIntegrationTests
{
    private readonly ClickHouseFixture _fixture;
    private readonly System.Text.StringBuilder _logBuffer = new();

    public ClickHouseLoggerIntegrationTests(ClickHouseFixture fixture)
    {
        _fixture = fixture;
        ClickHouseLogger.Core.Diagnostics.InternalLog.SetCallback((level, msg, exception) => 
        {
            var formatted = exception != null ? $"{level}: {msg} - {exception.Message}" : $"{level}: {msg}";
            lock (_logBuffer) _logBuffer.AppendLine(formatted);
        });
    }

    [Fact]
    public async Task Logs_AreWrittenTo_ClickHouseObservabilityTable_WithMapProperties()
    {
        // 1. Arrange - Setup Logger & DI
        var services = new ServiceCollection();
        var processId = Guid.NewGuid().ToString("N"); // To isolate this test run's data
        var endpoint = _fixture.Endpoint;

        services.AddLogging(builder =>
        {
            builder.AddClickHouse(options =>
            {
                options.Endpoint = endpoint;
                options.User = "default";
                options.Password = "test_password123";
                options.Service = "IntegrationTestService";
                options.Environment = "Testing";
                // Flush very quickly for the test if not manually disposed,
                // but we will manually dispose to force a flush.
                options.FlushInterval = TimeSpan.FromMilliseconds(500);
            });
        });

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<ClickHouseLoggerIntegrationTests>>();

        // 2. Act - Log a message
        var eventId = new EventId(777, "OrderCompleted");
        long userId = 10005;

        using (logger.BeginScope(new System.Collections.Generic.Dictionary<string, object>
        {
            { "ProcessId", processId },
            { "Operation", "EndToEndTest" }
        }))
        {
            logger.LogInformation(eventId, "Order {OrderId} was completed by user {UserId}", 99, userId);
        }

        // Force a flush by disposing the provider
        if (serviceProvider.GetRequiredService<ILoggerProvider>() is ClickHouseLoggerProvider clickHouseProvider)
        {
            await clickHouseProvider.DisposeAsync();
        }

        // 3. Assert - Query ClickHouse
        using var connection = new ClickHouseConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        // Give ClickHouse some time to settle the row in MergeTree
        var rowsFound = 0;
        for (int i = 0; i < 20; i++)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT 
                    level,
                    message,
                    category,
                    service,
                    env,
                    props['ProcessId'] AS parsed_process_id,
                    props['UserId'] AS parsed_user_id,
                    props['OrderId'] AS parsed_order_id,
                    props['EventId'] AS parsed_event_id,
                    toString(props) AS raw_props
                FROM observability.app_logs
            ";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var level = reader.GetString(0);
                var message = reader.GetString(1);
                var category = reader.GetString(2);
                var service = reader.GetString(3);
                var env = reader.GetString(4);
                var readProcessId = reader.GetString(5);
                var readUserId = reader.GetString(6);
                var readOrderId = reader.GetString(7);
                var readEventId = reader.GetString(8);
                var rawProps = reader.GetString(9);

                System.Console.WriteLine($"[DB_ROW] MSG: {message}, PROPS: {rawProps}");

                if (readProcessId == processId) 
                {
                    rowsFound++;
                    
                    level.Should().Be("Information");
                    message.Should().Be("Order 99 was completed by user 10005");
                    category.Should().Be(typeof(ClickHouseLoggerIntegrationTests).FullName);
                    service.Should().Be("IntegrationTestService");
                    env.Should().Be("Testing");
                    readUserId.Should().Be("10005");
                    readOrderId.Should().Be("99");
                    readEventId.Should().Be("777");
                }
            }

            if (rowsFound > 0)
                break;

            await Task.Delay(250); // Polling delay
        }

        if (rowsFound == 0)
        {
            throw new Exception("No rows found! Internal logs:\n" + _logBuffer.ToString());
        }

        rowsFound.Should().Be(1, "Exactly one log entry should be found for the given ProcessId within 5 seconds.");
    }
}
