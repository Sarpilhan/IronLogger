using System;
using System.IO;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using Testcontainers.ClickHouse;
using Xunit;

namespace ClickHouseLogger.Tests.Integration;

public class ClickHouseFixture : IAsyncLifetime
{
    private readonly ClickHouseContainer _clickHouseContainer;
    private readonly string _schemaPath;

    public ClickHouseFixture()
    {
        // The tests run from bin/Release/net8.0. We navigate to the root to find docs/schema.sql.
        _schemaPath = Path.GetFullPath("../../../../../docs/schema.sql");
        if (!File.Exists(_schemaPath))
        {
            throw new FileNotFoundException($"Schema file not found at {_schemaPath}");
        }

        _clickHouseContainer = new ClickHouseBuilder()
            .WithImage("clickhouse/clickhouse-server:latest")
            .WithUsername("default")
            .WithPassword("test_password123")
            .Build();
    }

    public string Endpoint => $"http://{_clickHouseContainer.Hostname}:{_clickHouseContainer.GetMappedPublicPort(8123)}";
    public string ConnectionString => _clickHouseContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _clickHouseContainer.StartAsync();
        
        using var connection = new ClickHouse.Client.ADO.ClickHouseConnection(ConnectionString);
        await connection.OpenAsync();

        var lines = await File.ReadAllLinesAsync(_schemaPath);
        var cleanSql = string.Join("\n", System.Linq.Enumerable.Where(lines, l => !l.TrimStart().StartsWith("--")));
        var statements = cleanSql.Split(';', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var statement in statements)
        {
            var stmt = statement.Trim();
            if (string.IsNullOrWhiteSpace(stmt))
                continue;

            using var cmd = connection.CreateCommand();
            cmd.CommandText = stmt;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public Task DisposeAsync()
    {
        return _clickHouseContainer.DisposeAsync().AsTask();
    }
}
