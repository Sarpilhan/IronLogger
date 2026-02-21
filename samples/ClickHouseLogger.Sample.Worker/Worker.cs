namespace ClickHouseLogger.Sample.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (_logger.BeginScope("Worker.Scope", "ClickHouseLogger.Sample.Worker Instance 1"))
        {
            _logger.LogInformation("ClickHouse Logger background worker has started execution.");

            int count = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    // Utilize inner scopes dynamically creating Event ID properties under the hood implicitly.
                    using (_logger.BeginScope("Worker Iteration {Iteration}", count))
                    {
                        _logger.LogInformation("Worker ping at: {time}. Emitting simulated logs...", DateTimeOffset.Now);
                        
                        if (count % 5 == 0)
                        {
                            var rand = Random.Shared.Next(1, 100);
                            _logger.LogWarning("Simulated warning condition with random metric: {RandomNumber}", rand);
                        }
                        
                        if (count > 0 && count % 20 == 0)
                        {
                            try
                            {
                                throw new InvalidOperationException($"Critical system error simulated at iteration {count}.");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(new EventId(999, "SimulatedCrash"), ex, "An unexpected crash occurred during iteration {Iteration}", count);
                            }
                        }
                    }
                }

                count++;
                await Task.Delay(1500, stoppingToken);
            }
        }
    }
}
