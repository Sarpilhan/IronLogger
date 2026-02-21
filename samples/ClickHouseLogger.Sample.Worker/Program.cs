using ClickHouseLogger.Extensions.Logging;
using ClickHouseLogger.Sample.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Add standard hosted service background worker
builder.Services.AddHostedService<Worker>();

// Purge default logging engines to verify our engine properly handles everything via options pattern.
builder.Logging.ClearProviders();

// Optional: keep console alongside ClickHouse
builder.Logging.AddConsole();

builder.Logging.AddClickHouse(options => 
{
    builder.Configuration.GetSection("Logging:ClickHouse").Bind(options);
});

var host = builder.Build();
host.Run();
