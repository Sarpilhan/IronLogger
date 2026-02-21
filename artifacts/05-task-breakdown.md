# 05 — Task Breakdown

## Phase 1 — Foundation & Core Pipeline

### P1-T1: Solution & Proje Yapısı
**Owner:** Backend Developer  
**Effort:** Küçük  
- [ ] `IronLogger.sln` oluştur
- [ ] `Directory.Build.props` (central package management, nullable, TreatWarningsAsErrors)
- [ ] `.editorconfig` (C# conventions)
- [ ] `.gitignore`
- [ ] 3 src projesi oluştur: Abstractions, Core, Sinks.ClickHouse
- [ ] 1 test projesi oluştur: Tests.Unit
- [ ] Proje referansları bağla
- [ ] `dotnet build` başarılı

### P1-T2: Abstractions Paketi
**Owner:** Backend Developer  
**Effort:** Küçük  
- [ ] `LogEvent` record/class (tüm alanlar: ts, level, message, category, service, env, template, exception, trace_id, span_id, correlation_id, version, host, props)
- [ ] `ClickHouseLoggerOptions` class (tüm config alanları — §6 uyumlu)
- [ ] `DropPolicy` enum
- [ ] `ClickHouseCompression` enum
- [ ] `ILogEventSink` interface (batch write)
- [ ] `ILogEventEnricher` interface
- [ ] `ILogEventRedactor` interface
- [ ] `ILogEventSerializer` interface
- [ ] `IDiagnosticsSnapshot` interface
- [ ] `Defaults` static class (tüm default değerler)

### P1-T3: Core — Enricher
**Owner:** Backend Developer  
**Effort:** Küçük  
- [ ] `StaticEnricher` (service, env, version, host, region)
- [ ] `CorrelationEnricher` (Activity.TraceId, SpanId, correlation_id)
- [ ] Unit tests

### P1-T4: Core — Redactor
**Owner:** Backend Developer  
**Effort:** Orta  
- [ ] `KeyBasedRedactor` — case-insensitive key matching, mask `***`
- [ ] `RegexRedactor` — compiled regex patterns, string-only
- [ ] `CompositeRedactor` — key + regex zincirleme
- [ ] Default patterns (email, kredi kartı, TCKN, TR IBAN)
- [ ] Unit tests (positive + negative cases)

### P1-T5: Core — Serializer
**Owner:** Backend Developer  
**Effort:** Orta-Büyük  
- [ ] `NdjsonSerializer` — Utf8JsonWriter ile LogEvent → NDJSON satır
- [ ] Props serialization (Map<string,string> → JSON object)
- [ ] Complex object → JSON string (depth limit = 4)
- [ ] Null/empty handling
- [ ] ArrayPool<byte> ile buffer pooling
- [ ] Batch serialization (LogEvent[] → byte[])
- [ ] Unit tests (format validation, edge cases)

### P1-T6: Core — Queue & Batcher
**Owner:** Backend Developer  
**Effort:** Orta  
- [ ] `BoundedLogQueue` — Channel<LogEvent> wrapper
  - [ ] DropDebugWhenBusy policy
  - [ ] BlockWhenFull policy
  - [ ] Enqueue / TryEnqueue
  - [ ] DroppedEvents counter
- [ ] `LogBatcher` — size/time trigger
  - [ ] BatchSize trigger
  - [ ] FlushInterval trigger (Timer veya Task.Delay)
  - [ ] Batch output: LogEvent[]
- [ ] Unit tests (queue full, drop policy, batch triggers)

### P1-T7: Core — Internal Diagnostics
**Owner:** Backend Developer  
**Effort:** Küçük  
- [ ] `InternalLog` static class (Trace.WriteLine + Debug.WriteLine)
- [ ] `InternalLogCallback` delegate ile override
- [ ] `DiagnosticsSnapshot` class (counters: Enqueued, Dropped, Sent, Failed, QueueLength, LastSendUtc)
- [ ] `EventCounters` (System.Diagnostics.Tracing.EventCounter) publish
- [ ] Unit tests

### P1-T8: Sinks.ClickHouse — HTTP Writer
**Owner:** Backend Developer  
**Effort:** Orta-Büyük  
- [ ] `HttpClickHouseWriter` : ILogEventSink
  - [ ] POST request yapısı (database, query, JSONEachRow)
  - [ ] Gzip compression (GZipStream)
  - [ ] Basic Auth header
  - [ ] Token Auth header
  - [ ] Retry policy (exponential backoff + jitter)
  - [ ] HTTP 5xx / 429 / timeout retry
  - [ ] OnBatchFailed callback
  - [ ] Counter güncelleme (SentBatches, FailedBatches)
- [ ] Unit tests (mock HttpMessageHandler ile)

### P1-T9: Schema & Docker
**Owner:** Backend Developer  
**Effort:** Küçük  
- [ ] `docs/schema.sql` (Map versiyonu)
- [ ] `docs/schema_no_map.sql` (JSON string versiyonu)
- [ ] `docker-compose.yml` (clickhouse-server + port mapping)
- [ ] Smoke test: manual insert + query

### P1-T10: Pipeline Orchestrator (Core)
**Owner:** Backend Developer  
**Effort:** Orta  
- [ ] `LogPipeline` class — tüm bileşenleri birleştirir:
  - [ ] Capture → Enrich → Redact → Queue → Batch → Serialize → Send
  - [ ] BackgroundService / IHostedService olarak çalışır
  - [ ] StartAsync / StopAsync (graceful shutdown)
  - [ ] Flush on dispose (timeout: 5s)
- [ ] Unit tests (end-to-end pipeline mock sink ile)

---

## Phase 2 — MEL Provider & Integration Tests

### P2-T1: Extensions.Logging — Provider
**Owner:** Backend Developer  
**Effort:** Orta  
- [ ] `ClickHouseLoggerProvider` : ILoggerProvider, IAsyncDisposable
- [ ] `ClickHouseLogger` : ILogger (BeginScope, Log, IsEnabled)
- [ ] Scope/state property extraction → props
- [ ] `AddClickHouse(Action<ClickHouseLoggerOptions>)` extension method
- [ ] DI registration (IServiceCollection)
- [ ] Unit tests

### P2-T2: Integration Test Projesi
**Owner:** Backend Developer / QA  
**Effort:** Orta  
- [x] `ClickHouseLogger.Tests.Integration` proje oluştur
- [x] Test fixture: Docker ClickHouse start/stop
- [x] Schema apply
- [x] End-to-end: ILogger.Log → ClickHouse row doğrulama
- [x] Auth tests (Basic + Token)
- [x] Batch size / flush doğrulama
- [x] Graceful shutdown test

### [x] P2-T3: Sample.Worker
**Owner:** Backend Developer
**Goal:** Create a simple worker service demonstrating MEL configuration and logging.
**Tasks:**
- `dotnet new worker -n ClickHouseLogger.Sample.Worker`
- Add reference to `ClickHouseLogger.Extensions.Logging`
- Configure `appsettings.json` for ClickHouse.
- Write a background worker emitting logs in a loop.
**Dependencies:** P2-T1

---

## Phase 3 — ASP.NET Core Integration & Samples

### [x] P3-T1: Integrations.AspNetCore
**Owner:** Backend Developer  
**Effort:** Orta  
- [x] `ClickHouseRequestLoggingMiddleware`
  - [x] method, path, status_code, duration_ms, correlation_id, trace_id, span_id
  - [x] Exception handling (error log, no response body leak)
- [x] `UseClickHouseRequestLogging()` extension method
- [x] `IClickHouseEnricher` interface + enricher chain
- [x] Built-in enrichers: MachineName, ThreadId, ProcessId
- [x] Unit tests + integration tests

### [x] P3-T2: Sample.AspNetCore
**Owner:** Backend Developer  
**Effort:** Küçük  
- [x] Minimal Web API + middleware
- [x] appsettings.json
- [x] Custom enricher örneği
- [x] README.md

---

## Phase 4 — Polish, LoadGen, CI/CD & Release

### [x] P4-T1: LoadGen Benchmark
**Owner:** Backend Developer  
**Effort:** Orta  
- [x] Console app: configurable event count, concurrency
- [x] Ölçüm: events/s, p99 latency, GC allocations, peak memory
- [x] ≥ 50K event/s doğrulama (Sonuç: 287+ K/s)
- [x] Sonuç raporu (markdown output)

### [x] P4-T2: CI/CD Pipeline
**Owner:** Backend Developer  
**Effort:** Küçük-Orta  
- [x] `.github/workflows/ci.yml`
- [x] `.github/workflows/release.yml`
- [x] Coverage report entegrasyonu
- [x] NuGet paket metadata (.csproj: PackageId, Description, License, etc.)

### [x] P4-T3: Documentation
**Owner:** Backend Developer  
**Effort:** Küçük  
- [x] README.md (installation, config, examples, architecture, troubleshooting)
- [x] CONTRIBUTING.md
- [x] LICENSE (MIT)
- [x] API doc comments (tüm public members)

### [x] P4-T4: Final QA Gate
**Owner:** QA / Orchestrator  
**Effort:** Küçük  
- [x] Tüm projede sıfır hata, sıfır warning policy kontrolü
- [x] Test coverage en az `> 80%`
- [x] Handoff belgesi `12-handoff.md` (ve isteğe bağlı Release Notes) hazırlanmasışır
- [ ] README walkthrough works
