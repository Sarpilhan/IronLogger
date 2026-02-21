using System.Diagnostics;
using ClickHouseLogger.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClickHouseLogger.LoadGen;

public static class Program
{
    private const int TargetEventCount = 1_000_000;
    private const int ConcurrentTasks = 20;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("IronLogger - ClickHouse Load Generator");
        Console.WriteLine("======================================");
        Console.WriteLine($"Target Events: {TargetEventCount:N0}");
        Console.WriteLine($"Concurrency: {ConcurrentTasks} Tasks");
        Console.WriteLine();

        var builder = Host.CreateApplicationBuilder(args);

        // Remove console, we only want to measure ClickHouse overhead.
        builder.Logging.ClearProviders();

        builder.Logging.AddClickHouse(o =>
        {
            o.Endpoint = "http://localhost:8123";
            o.Database = "observability";
            o.Table = "app_logs";
            o.User = "logger";
            o.Password = "logger_dev_pass";
            o.Service = "LoadGenerator";
            o.Environment = "Benchmark";
            o.BatchSize = 10000;         // Push 10k logs per batch
            o.FlushInterval = TimeSpan.FromSeconds(1);
            o.MaxQueueItems = 2_000_000; // Never run out of queue space for this test
        });

        // Suppress general MS Hosting logs unless critical to isolate memory
        builder.Logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);

        var host = builder.Build();

        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LoadGen.Benchmark");

        // Warm up the logger to JIT compile routing and mappings.
        logger.LogInformation("Warmup started: Payload string {Value}", "Hello World");

        var sw = Stopwatch.StartNew();
        var process = Process.GetCurrentProcess();
        var startMemoryMb = process.WorkingSet64 / 1024.0 / 1024.0;
        var startGc0 = GC.CollectionCount(0);
        var startGc1 = GC.CollectionCount(1);
        var startGc2 = GC.CollectionCount(2);

        int eventsPerTask = TargetEventCount / ConcurrentTasks;
        int completedEvents = 0;

        Console.WriteLine("Starting emission loop...");

        // Generate logs using high concurrency
        var tasks = new Task[ConcurrentTasks];
        for (int i = 0; i < ConcurrentTasks; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                var dict = new Dictionary<string, object>
                {
                    ["TransactionId"] = Guid.NewGuid().ToString(),
                    ["ThreadId"] = Environment.CurrentManagedThreadId
                };

                for (int j = 0; j < eventsPerTask; j++)
                {
                    using (logger.BeginScope(dict))
                    {
                        logger.LogInformation("Processing mock payload for iteration {Iteration} generating load via random {Metric}", j, Random.Shared.Next(0, 500));
                    }
                    Interlocked.Increment(ref completedEvents);
                }
            });
        }

        await Task.WhenAll(tasks);

        Console.WriteLine("Emission complete. Waiting for ClickHouse sink to flush bounded queue...");

        // Let the hosted service stop trigger FlushOnDispose gracefully draining Background Queue
        await host.StopAsync();
        host.Dispose(); // Forces the ILoggerProvider out (DisposeAsync inside)

        sw.Stop();

        process.Refresh();
        var endMemoryMb = process.WorkingSet64 / 1024.0 / 1024.0;
        var endGc0 = GC.CollectionCount(0);
        var endGc1 = GC.CollectionCount(1);
        var endGc2 = GC.CollectionCount(2);

        Console.WriteLine();
        Console.WriteLine("--- BENCHMARK RESULTS ---");
        Console.WriteLine($"Total Events:       {completedEvents:N0}");
        Console.WriteLine($"Total Time:         {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"Throughput:         {(completedEvents / sw.Elapsed.TotalSeconds):N0} events / sec");
        Console.WriteLine();
        Console.WriteLine($"Memory Deltas:");
        Console.WriteLine($"  Peak Addtl Mem:   {(endMemoryMb - startMemoryMb):F2} MB");
        Console.WriteLine($"  GC Gen 0:         {endGc0 - startGc0}");
        Console.WriteLine($"  GC Gen 1:         {endGc1 - startGc1}");
        Console.WriteLine($"  GC Gen 2:         {endGc2 - startGc2}");
        Console.WriteLine("-------------------------");
    }
}
