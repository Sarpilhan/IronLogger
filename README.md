# IronLogger — ClickHouse `Microsoft.Extensions.Logging` Provider

![CI](https://github.com/IronLogger/IronLogger/actions/workflows/ci.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/ClickHouseLogger.Extensions.Logging.svg)](https://www.nuget.org/packages/ClickHouseLogger.Extensions.Logging)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**IronLogger** is an extremely high-performance, strictly non-blocking `.NET 8` native `ILogger` provider designed exclusively for **ClickHouse**. It translates the standard Microsoft trace lifecycle into highly structured representations and dispatches them efficiently using ClickHouse's natively optimized HTTP interface (`JSONEachRow` standard).

Designed with microservices in mind, IronLogger abstracts all asynchronous chunking behind a dedicated thread-safe internal channel, providing safety against network partitioning and transient database limits (Timeout, 429, 500).

## 🚀 Key Features

*   **Zero-Allocation Focus:** Custom `NdjsonSerializer` using `Utf8JsonWriter` tied with `ArrayPool<byte>` entirely bypasses rigid memory bottlenecks typically enforced by `System.Text.Json` objects.
*   **Highly Structured Properties:** Naturally inherits complex `.BeginScope()` patterns mapping scalar formats transparently into ClickHouse's dynamic `Map(String, String)` engine natively.
*   **Bounded Asynchrony Pipeline:** Protects the host application thread's GC profile via discrete `System.Threading.Channels` mechanisms, guaranteeing non-hanging telemetry traces.
*   **Drop Policies:** Handles sudden spikes gracefully via options (`DropDebugWhenBusy`, `BlockWhenFull`).
*   **Compliance & PII Safety:** Granular data scrubbing interfaces `[KeyBasedRedactor]`, `[RegexRedactor]` obfuscating PCI/PII data out-of-the-box (Regex e.g. emails, credit cards, or key-matched headers like Authorization).
*   **ASP.NET Core Support:** Ultra-fast lightweight routing Request execution middleware (`ClickHouseRequestLoggingMiddleware`) coupled with pre-built Thread, MachineName and ProcessID enrichers.

---

## 📦 Installation

**via CLI:**

```bash
dotnet add package ClickHouseLogger.Extensions.Logging
# If you are integrating into a web app (API, MVC, MinimalAPI)
dotnet add package ClickHouseLogger.AspNetCore
```

---

## 🔧 Getting Started (General / Worker)

Modify `Program.cs` and pipe IronLogger to the container:

```csharp
using ClickHouseLogger.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Bind the provider to Configuration.
builder.Logging.AddClickHouse(builder.Configuration.GetSection("Logging:ClickHouse"));

var host = builder.Build();
host.Run();
```

Append configuration settings into `appsettings.json`:

```json
{
  "Logging": {
    "ClickHouse": {
      "Endpoint": "http://localhost:8123",
      "Database": "observability",
      "Table": "app_logs",
      "User": "default",
      "Password": "my_secure_password",
      "Service": "MyMicroservice",
      "Environment": "Production"
    }
  }
}
```

## 🌐 Setting Up ASP.NET Core Middleware

For web traffic load tracing, hook the **ClickHouseRequestLoggingMiddleware**:

```csharp
using ClickHouseLogger.AspNetCore;
using ClickHouseLogger.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Inject Core Extensions
builder.Logging.AddClickHouse(builder.Configuration.GetSection("Logging:ClickHouse"));

// Expose internal ASP.NET HTTP Enrichers (Process, Thread, Machine)
builder.Services.AddClickHouseRequestEnrichers();

var app = builder.Build();

// Intercepts the response streams immediately capturing Elapsed Execution
app.UseClickHouseRequestLogging(); 

app.MapGet("/", (ILogger<Program> logger) => 
{
    logger.LogInformation("Route hit!");
    return "OK";
});

app.Run();
```

---

## 🛠️ Architecture Overview

When you call `_logger.LogInformation(...)`, the payload enters the IronLogger fast-path:
1. **Enrichment:** Adds generic system metadata (Service, ENV) to `LogEvent`.
2. **Redaction:** Rapidly masks matches detected by the policy engine.
3. **Queueing:** Inserts item onto a `System.Threading.Channels`. Execution exits directly mapping back to logic.
4. **Batching:** A background thread (Managed heavily by `LogPipeline`) bundles elements per Time OR Size limits (e.g. `2000` items or `1.5s`).
5. **Serialization:** Evaluates `Utf8Json` encoding over `JSONEachRow`.
6. **Delivery:** Pushes the buffered array to ClickHouse HTTP, handling configured exponential retries.

---

## 📊 Benchmarks

Tests conducted sequentially over 20 Tasks asynchronously scaling `(1,000,000)` records:

*   **Throughput:**  287,838 events/sec
*   **End-to-end Transmission & Flush Timing:** 3,474 ms
*   **Peak Secondary Environment Memory Threshold:** 697 MB
*   **Data Integrity:** 0 Loss 

Hardware: Emulated `dotnet run` x64 2.8GHz.

## 🤝 Contributing

We welcome community pull requests! Refer to [CONTRIBUTING.md](./CONTRIBUTING.md) for more comprehensive test environment (Docker/Testcontainers) build details.

## ⚖️ License

IronLogger is released under the **MIT License**. Check the [LICENSE](./LICENSE) page for documentation rights.
