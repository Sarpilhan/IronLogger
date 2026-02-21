using ClickHouseLogger.AspNetCore;
using ClickHouseLogger.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Purge default providers
builder.Logging.ClearProviders();

// Optional Console fallback
builder.Logging.AddConsole();

// Add ClickHouse via IConfiguration mapping
builder.Logging.AddClickHouse(options =>
{
    builder.Configuration.GetSection("Logging:ClickHouse").Bind(options);
});

// Add the ASP.NET Core integrations: ThreadId, ProcessId, MachineName
builder.Services.AddClickHouseRequestEnrichers();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Ensure the Request Logging Middleware is added _before_ the endpoints to capture the entire HTTP lifecycle.
app.UseClickHouseRequestLogging();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", (ILogger<Program> logger) =>
{
    logger.LogInformation("Generating a fresh weather forecast for the client.");

    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
        
    using (logger.BeginScope(new Dictionary<string, object> { ["ForecastCount"] = forecast.Length }))
    {
        logger.LogWarning("Forecast calculation scope activated. {Count} items generated.", forecast.Length);
    }

    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.MapPost("/simulate-error", () =>
{
    throw new InvalidOperationException("This is a simulated critical crash designed to test ASP.NET Core Middleware 500 error interception and ClickHouse Error logging.");
})
.WithName("SimulateError")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
