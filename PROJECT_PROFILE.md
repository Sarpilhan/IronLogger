# ClickHouse Logger Factory for .NET — MVP Spec (Open Source + NuGet)
Version: **0.2 (MVP — Revised)**  
Date: **2026-02-22**  
Owner: **Sarp Yiğit İlhan**

> Amaç: .NET uygulamalarında **structured log/event** verisini **yüksek throughput** ile **ClickHouse**'a yazan, projeden projeye **tak-çalıştır** kullanılabilen bir **Logger Factory + Microsoft.Extensions.Logging provider** geliştirmek.  
> MVP; üretimde kullanılabilecek kadar sağlam, ama kapsamı net, "açık kapı" bırakmayacak şekilde tanımlıdır.

---

## 0) Tech Stack & Versions

| Teknoloji | Versiyon / Seçim | Not |
|---|---|---|
| .NET | **8.0 (LTS)** | `net8.0` target framework |
| C# | **12** | .NET 8 ile gelen dil sürümü |
| ClickHouse | **≥ 22.3** | `Map(String, String)` desteği için minimum sürüm |
| Serialization | **System.Text.Json + Utf8JsonWriter** | Low-allocation, source-gen destekli, GC pressure minimized |
| HTTP Client | **System.Net.Http.HttpClient** | `IHttpClientFactory` ile DI uyumlu |
| Test Framework | **xUnit 2.x** | + FluentAssertions + Moq |
| CI Platform | **GitHub Actions** | `ubuntu-latest` runner, ClickHouse Docker ile integration tests |
| Docker (Test) | **clickhouse/clickhouse-server:latest** | CI ve lokal integration test'ler için |
| Paket Yönetimi | **NuGet** | `dotnet pack` ile nupkg + snupkg üretimi |
| Lisans | **MIT** | Açık kaynak, NuGet ekosistemi ile uyumlu |

> **Serialization Notu:** `Utf8JsonWriter` doğrudan UTF-8 byte'lara yazar, string allocation oluşturmaz. `ArrayPool<byte>` ile sıfıra yakın GC pressure sağlanır. Source generator desteği sayesinde AOT-friendly, reflection-free serialization mümkündür. Bu seçim throughput hedefi (≥ 50K event/s) için kritiktir.

---

## 1) Özet
Bu doküman, ClickHouse'a log/event yazan bir .NET kütüphanesinin MVP gereksinimlerini tanımlar. Kütüphane:
- `Microsoft.Extensions.Logging` (MEL) ile **ILoggerProvider** olarak entegre olur.
- Logları **HTTP + JSONEachRow** ile ClickHouse'a **batch** olarak yazar.
- **Async queue**, **backpressure**, **gzip**, **retry**, **PII redaction** içerir.
- **Correlation** (TraceId/SpanId/RequestId) bilgisi ekler.
- Open source (GitHub) + NuGet paketleri olarak yayınlanır.

---

## 2) Hedefler ve Kapsam Dışı

### 2.1 Hedefler (MVP)
- **MEL provider**: `AddClickHouse(...)` ile eklenebilir.
- **Structured logging**: message + template (opsiyonel) + property set.
- **Yüksek throughput ingestion**: bounded async queue + batching + gzip.
- **Throughput hedefi**: Tek instance'da **≥ 50,000 event/s** sustained throughput.
- **Prod güvenliği**: backpressure policy, drop policy, internal counters.
- **PII masking/redaction**: key + regex tabanlı.
- **Correlation enrichment**: `System.Diagnostics.Activity` + `HttpContext.TraceIdentifier`.
- **ASP.NET Core integration**: Request lifecycle middleware + enrichers.
- **Operasyonel netlik**: tek tablo şeması + TTL + ClickHouse user permission önerileri.
- **Open source standardı**: CI, test, semantic versioning, NuGet publish.

### 2.2 Kapsam Dışı (MVP'de yok)
- Monitoring UI (dashboard/alerting).
- ClickHouse native TCP protocol (MVP sadece HTTP).
- Çoklu backend (sadece ClickHouse).
- "Guaranteed delivery" (disk spool MVP'de yok; v0.2+ backlog).
- Gelişmiş OTel exporter (v0.2+).
- Standalone `ClickHouseLoggerFactory` (v0.2+; MVP'de yalnızca MEL provider).
- Outgoing HTTP call logger (`HttpClient` integration, v0.2+).

---

## 3) Hedef Kullanıcılar ve Senaryolar
**Hedef kullanıcılar:**
- ASP.NET Core API / worker service ekipleri
- ClickHouse'u analitik için kullanan, logları da aynı yerde toplamak isteyen ekipler
- Birden çok .NET serviste standardize logging isteyen developerlar

**Use case'ler:**
1. Uygulama logları (Info/Warn/Error) + properties
2. Request lifecycle log (duration/status) — ASP.NET Core middleware ile
3. Business event'ler (MVP'de aynı tabloya, structured log gibi)

---

## 4) Paketleme Stratejisi (NuGet)
MVP'de aşağıdaki paketler **üretilmiş ve yayınlanabilir** olmalıdır:

| Paket | Amaç | MVP |
|---|---|---|
| `ClickHouseLogger.Abstractions` | Core modeller & interface'ler | ✅ Zorunlu |
| `ClickHouseLogger.Core` | Pipeline: queue/batch/redact/serialize | ✅ Zorunlu |
| `ClickHouseLogger.Sinks.ClickHouse` | HTTP writer (JSONEachRow + gzip) | ✅ Zorunlu |
| `ClickHouseLogger.Extensions.Logging` | MEL provider | ✅ Zorunlu |
| `ClickHouseLogger.Integrations.AspNetCore` | Middleware + enrichers | ✅ Zorunlu |
| `ClickHouseLogger.Integrations.HttpClient` | Outgoing request logging | ❌ v0.2+ |

> MVP "tamam" sayılması için **ilk 5 paket** zorunludur.

---

## 5) Dışa Açık API (MVP)

### 5.1 MEL Provider
Kullanım örneği:

```csharp
builder.Logging.ClearProviders();
builder.Logging.AddClickHouse(o =>
{
    o.Endpoint = "https://clickhouse.mycorp.com:8443";
    o.Database = "observability";
    o.Table = "app_logs";
    o.User = "logger";
    o.Password = "***";
    o.Service = "payment-api";
    o.Environment = "prod";

    // MVP defaults (değiştirilebilir)
    o.BatchSize = 2000;
    o.FlushInterval = TimeSpan.FromSeconds(1);
    o.MaxQueueItems = 200_000;
    o.Compression = ClickHouseCompression.Gzip;
    o.DropPolicy = DropPolicy.DropDebugWhenBusy;
    o.MinLevel = LogLevel.Information;
});
```

### 5.2 ASP.NET Core Integration
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddClickHouse(o => { /* ... */ });

var app = builder.Build();
app.UseClickHouseRequestLogging(); // middleware
app.Run();
```

---

## 6) Konfigürasyon (ClickHouseLoggerOptions)
MVP'de aşağıdaki alanlar **tam** implement edilmelidir.

### 6.1 Bağlantı / hedef
- `Endpoint` (string) — ClickHouse HTTP(S) base URL
- `Database` (string)
- `Table` (string)
- `User` (string) + `Password` (string) — Basic Auth (zorunlu destek)
- `AuthToken` (string?) — Bearer/custom token header (MVP'de **implement** edilecek, `User/Password` ile mutual-exclusive)

### 6.2 Performans
- `BatchSize` (int) — default **2000**
- `FlushInterval` (TimeSpan) — default **1s**
- `MaxQueueItems` (int) — default **200_000**
- `Compression` — `None | Gzip` (default **Gzip**)

### 6.3 Logging kontrolü
- `MinLevel` (LogLevel) — default **Information**
- `DropPolicy` — `DropDebugWhenBusy | BlockWhenFull` (default **DropDebugWhenBusy**)

### 6.4 Static dimensions (her event'e eklenecek)
- `Service` (string) — **required**
- `Environment` (string) — **required**
- `Version` (string) — optional
- `Region` (string) — optional

### 6.5 Redaction
- `RedactionEnabled` (bool) — default **true**
- `RedactKeys` (string[]) — default: `password, token, authorization, secret, apiKey, iban, tckn`
- `RedactPatterns` (Regex[]) — default aşağıdaki pattern set:
  - Email: `[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}` → `***@***.***`
  - Kredi kartı (16 hane, opsiyonel ayraçlı): `\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b` → `****-****-****-****`
  - TCKN (11 hane): `\b\d{11}\b` → `***********`
  - IBAN (TR): `\bTR\d{24}\b` → `TR**************************`

> **Not:** Regex masking yalnızca `string` değerlere uygulanır. Kullanıcılar custom pattern ekleyebilir.

### 6.6 Retry
- `MaxRetries` (int) — default **5**
- `BaseDelayMs` (int) — default **200**
- `MaxDelayMs` (int) — default **5000**
- Policy: exponential backoff + jitter → `delay = min(MaxDelay, BaseDelay * 2^attempt) + Random(0..BaseDelay)`

### 6.7 HTTP
- `HttpTimeout` (TimeSpan) — default **10s**
- Custom `HttpMessageHandler` injection (test ve proxy senaryoları için)

---

## 7) Veri Modeli (LogEvent)
MVP'de internal model aşağıdaki alanları **taşıyacak** ve serializer bunu JSONEachRow'a çevirecektir.

**Zorunlu alanlar**
- `ts` (UTC) — DateTime64(3)
- `level`
- `message` (rendered)
- `category`
- `service`
- `env`

**Opsiyonel alanlar**
- `template`
- `exception`
- `trace_id`
- `span_id`
- `correlation_id`
- `version`
- `host`
- `props` (map / json)

**Props kuralları**
- MEL structured state ve scope içindeki key/value'lar `props`'a taşınır.
- `props`'a girmeden önce redaction uygulanır.
- `props` değerleri **string'e normalize** edilir (MVP).  
  - Complex object → JSON string, **maksimum object graph derinliği 4** (nested object/array → derinlik 4'ten sonra `"[...]"` olarak truncate edilir).
  - `null` → boş string `""`
  - Primitives → `ToString()` / `InvariantCulture`

---

## 8) ClickHouse Tablo Şeması (MVP)

### 8.1 Önerilen tablo
**Tek tablo**: `observability.app_logs`

**Engine / partition / order**
- Engine: `MergeTree`
- Partition: `toYYYYMM(ts)`
- Order by: `(service, env, level, ts, trace_id)`
- TTL: `ts + INTERVAL 30 DAY` (README'de retention önerileri verilecek)

### 8.2 Kolonlar (MVP)
> Bu şema "minimum sürpriz" hedefler. Map desteği yoksa `props_json String` alternatifi sunulur.

**Tercih edilen (Map — ClickHouse ≥ 22.3)**
- `ts DateTime64(3)`
- `level LowCardinality(String)`
- `message String`
- `template String`
- `category LowCardinality(String)`
- `exception String`
- `trace_id String`
- `span_id String`
- `correlation_id String`
- `service LowCardinality(String)`
- `env LowCardinality(String)`
- `version LowCardinality(String)`
- `host LowCardinality(String)`
- `props Map(String, String)`

**Alternatif (Map yoksa — ClickHouse < 22.3)**
- Yukarıdaki `props` yerine: `props_json String` (JSON string)

### 8.3 SQL (README'ye birebir konacak)
MVP repo'da `docs/schema.sql` dosyası olarak yer almalı (Map'li ve Map'siz iki versiyon).

---

## 9) Ingestion Protokolü (HTTP + JSONEachRow)

### 9.1 Insert formatı
- HTTP endpoint: ClickHouse HTTP interface
- Query: `INSERT INTO {db}.{table} FORMAT JSONEachRow`
- Body: newline-delimited JSON objects (NDJSON), UTF-8

### 9.2 HTTP request (MVP)
- `POST /?database={db}&query={urlencoded_insert_query}`
- Header:
  - `Content-Type: application/json`
  - `Content-Encoding: gzip` (compression enabled ise)
- Auth:
  - **Basic Auth** (user/password) — zorunlu destek
  - **Bearer Token** (`Authorization: Bearer {token}`) — zorunlu destek, `AuthToken` config'i ile

---

## 10) İç Mimari (Pipeline)

### 10.1 Aşamalar
1. **Capture**: MEL `ILogger.Log(...)` çağrısından `LogEvent` üret
2. **Enrich**:
   - static: service/env/version/host/region
   - runtime: `Activity.TraceId/SpanId`, `correlation_id`
3. **Redact**: key + regex masking
4. **Queue**: bounded `Channel<LogEvent>`
5. **Batch**: size/time trigger
6. **Serialize**: `Utf8JsonWriter` ile JSONEachRow NDJSON (pooled buffers, minimal allocations)
7. **Send**: `HttpClient` POST + retry

### 10.2 Queue & backpressure
- Queue: `Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(MaxQueueItems){ SingleReader=true, SingleWriter=false, FullMode=... })`
- **Default**: `DropDebugWhenBusy`
  - Queue full ise `LogLevel.Debug` ve altı drop
  - Info+ için enqueue attempt; başarısızsa drop + counter
- Alternatif: `BlockWhenFull` (kuyruk doluysa yazan thread bekler) — prod'da dikkatli kullanılacak, doc'ta riskleri yazılacak.

### 10.3 Batching
- Trigger:
  - `BatchSize` dolunca flush
  - `FlushInterval` dolunca flush
- Batch format: NDJSON string (her event 1 satır)
- Buffer: `ArrayPool<byte>` ile pooled buffer, GC pressure minimize

### 10.4 Retry & hata yönetimi
- Retry conditions:
  - timeout
  - HTTP 5xx
  - HTTP 429 (Too Many Requests)
- Retry policy:
  - exponential backoff: `delay = min(MaxDelay, BaseDelay * 2^attempt)`
  - jitter: `delay += Random(0..BaseDelay)`
- Exhaust olursa:
  - batch drop
  - `FailedBatches++`
  - `OnBatchFailed(Exception ex, int batchSize)` callback tetiklenir (opsiyonel user callback)
  - Internal diagnostics log yazılır (bkz. §10.6)

### 10.5 Internal counters (MVP)
MVP'de diagnostics için aşağıdaki metrikler **mutlaka** tutulacak:
- `EnqueuedEvents`
- `DroppedEvents`
- `SentBatches`
- `FailedBatches`
- `QueueLength` (anlık)
- `LastSendUtc`

Expose yöntemi: `EventCounters` **ve** `IDiagnosticsSnapshot` interface'i — her ikisi de implement edilecek.

### 10.6 Internal Diagnostics (Self-Logging)
Kütüphanenin kendi hatalarını loglaması için ayrı bir mekanizma (circular dependency riski nedeniyle **kendi ILogger'ını kullanaMAZ**):

- `InternalLog` static sınıfı:
  - Default: `Trace.WriteLine` + `Debug.WriteLine` (stderr'e yakın seviye)
  - Opsiyonel: `InternalLogCallback(string level, string message, Exception? ex)` delegate ile override edilebilir
  - Kapsam: batch failure, connection error, queue overflow, startup/shutdown events
- Bu kanal **sadece** kütüphanenin kendi diagnostik mesajları içindir, kullanıcı logları buraya yazılmaz.

---

## 11) Redaction / PII Kuralları (MVP)

### 11.1 Key-based masking (zorunlu)
- Key listesi case-insensitive eşleşir.
- Mask: `***`
- Default keys: `password, token, authorization, secret, apiKey, iban, tckn`

### 11.2 Regex masking (zorunlu)
- Config ile regex listesi
- Default patterns (bkz. §6.5 detaylı liste):
  - Email adresleri
  - Kredi kartı numaraları (16 hane)
  - TCKN (11 hane)
  - TR IBAN
- Regex sadece **string** değerlere uygulanır.
- Compiled regex kullanılacak (`RegexOptions.Compiled`) — throughput'u etkilememeli.

### 11.3 Ne loglanmaz (MVP policy)
- Request/response body **default olarak loglanmaz**
- Header'lar default loglanmaz (Authorization kesinlikle loglanmaz)

---

## 12) ASP.NET Core Integration (MVP — Zorunlu)

### 12.1 Middleware davranışı
- Her request için **1 event** yazar:
  - `method`, `path`, `status_code`, `duration_ms`
  - `correlation_id = HttpContext.TraceIdentifier`
  - `trace_id/span_id` Activity'den
- Body yok, header yok (güvenlik ve maliyet)

### 12.2 Exception
- Unhandled exception yakalanırsa:
  - Error level log + exception string
  - Response'a detay basılmaz (uygulamanın kendi exception handling'ine saygı)

### 12.3 Enricher
- `IClickHouseEnricher` interface'i ile custom enrichment desteklenir.
- Built-in enrichers: `MachineName`, `ThreadId`, `ProcessId`.

---

## 13) Test Stratejisi

### 13.1 Unit tests (zorunlu)
- Framework: **xUnit** + **FluentAssertions** + **Moq**
- Redaction: key ve regex testleri
- Batching: size/time flush deterministik (`FakeTimeProvider` ile — .NET 8 TimeProvider API)
- DropPolicy: queue dolu senaryosu
- Serializer: NDJSON format valid mi
- Internal diagnostics: self-log callback doğrulama

### 13.2 Integration tests (zorunlu)
- CI'de ClickHouse Docker ile ayağa kalkar (`docker-compose.yml` repo'da mevcut)
- `schema.sql` uygulanır
- Insert yapılır, row count doğrulanır
- Auth senaryoları (Basic + Token) test edilir

### 13.3 Load test harness (zorunlu)
- `samples/LoadGen` ile throughput ölçümü
- **Hedef: ≥ 50,000 event/s** sustained (tek instance, standart payload)
- Ölçüm metrikleri: events/s, p99 latency, GC allocations, peak memory
- CI'da opsiyonel regression check (alert seviyesi: %20 düşüş)

---

## 14) Repo Yapısı (Monorepo)

```
/src
  /ClickHouseLogger.Abstractions          → Core modeller & interface'ler
  /ClickHouseLogger.Core                  → Pipeline: queue/batch/redact/serialize
  /ClickHouseLogger.Sinks.ClickHouse      → HTTP writer (JSONEachRow + gzip)
  /ClickHouseLogger.Extensions.Logging    → MEL provider
  /ClickHouseLogger.Integrations.AspNetCore → Middleware + enrichers

/tests
  /ClickHouseLogger.Tests.Unit            → xUnit unit tests
  /ClickHouseLogger.Tests.Integration     → ClickHouse Docker ile integration tests

/samples
  /Sample.AspNetCore                      → ASP.NET Core Web API örneği
  /Sample.Worker                          → BackgroundService / Worker örneği
  /LoadGen                                → Throughput benchmark aracı

/docs
  /schema.sql                             → Map'li tablo şeması
  /schema_no_map.sql                      → Map'siz alternatif şema

/.github
  /workflows
    /ci.yml                               → Build + Test + Pack
    /release.yml                          → Tag ile NuGet publish

docker-compose.yml                        → Lokal ClickHouse (test + development)
IronLogger.sln                            → Solution file
README.md
LICENSE                                   → MIT
CONTRIBUTING.md
.editorconfig
Directory.Build.props                     → Central package management, analyzers
```

---

## 15) CI/CD ve Release (Open Source Standardı)

### 15.1 GitHub Actions (zorunlu)
**ci.yml** (her PR + push to main):
```yaml
steps:
  - dotnet restore
  - dotnet build -c Release /p:TreatWarningsAsErrors=true
  - dotnet test -c Release --logger "trx"
  - dotnet pack -c Release --no-build -o ./artifacts
```

**release.yml** (tag push `v*`):
```yaml
steps:
  - dotnet pack -c Release
  - dotnet nuget push **/*.nupkg --source nuget.org --api-key ${{ secrets.NUGET_KEY }}
```

### 15.2 Quality gates
- `<Nullable>enable</Nullable>` tüm src projelerinde
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` tüm src projelerinde
- Analyzers: `Microsoft.CodeAnalysis.NetAnalyzers` aktif
- Code coverage: minimum **%80** line coverage (unit + integration)

### 15.3 Versioning ve NuGet publish
- SemVer: `0.x` (stabil olana kadar)
- Tag ile publish: `v0.1.0`
- Release notes otomatik üretilebilir (opsiyonel)

---

## 16) Güvenlik ve Operasyonel Rehber (README'de zorunlu)
- Prod'da **HTTPS** şart
- ClickHouse'ta **INSERT-only** kullanıcı
- TTL ile retention (7–30 gün önerileri)
- Order by/partition önerileri
- Diagnostic counters nasıl okunur
- Internal diagnostics nasıl aktif edilir

---

## 17) MVP Kabul Kriterleri (Definition of Done)
MVP "tamam" sayılması için aşağıdakilerin **tamamı** sağlanmalı:

1. .NET 8 üzerinde **5 zorunlu paket** build + test geçer.
2. MEL provider ile standart `ILogger` çalışır, scope/state property'leri taşınır.
3. ClickHouse'a **HTTP + JSONEachRow + gzip** ile yazılır.
4. Bounded queue + batching çalışır; **DropDebugWhenBusy** test ile doğrulanır.
5. Redaction (key + regex) çalışır ve test ile doğrulanır.
6. ASP.NET Core middleware ile request lifecycle logları yazılır.
7. `docs/schema.sql` ve `docs/schema_no_map.sql` repo'da mevcut ve README'de kullanım net.
8. Auth: Basic Auth **ve** Token Auth implement ve test edilmiş.
9. CI pipeline nupkg artifacts üretir.
10. README: kurulum, config, örnek kodlar, troubleshooting içerir.
11. Load test: **≥ 50K event/s** sustained throughput sağlanır (LoadGen ile ölçülür).
12. Unit + Integration test coverage: **≥ %80**.
13. Internal diagnostics: self-log mekanizması çalışır, batch failure'lar raporlanır.

---

## 18) Post-MVP Backlog (v0.2+)
- Disk spool (durable buffer) — ClickHouse outage'larında kayıpsızlık
- Standalone `ClickHouseLoggerFactory` (DI dışı kullanım)
- Outgoing `HttpClient` call logger (DelegatingHandler integration)
- Logs vs business events ayrı tablo + typed event API
- OpenTelemetry bridge/exporter
- RowBinary serialization (daha yüksek throughput)
- Ek sink'ler (console/file) debug amaçlı

---

## 19) Architecture Constraints

### 19.1 Thread Safety
- `ILogger` instance'ları **thread-safe** olmalıdır (MEL kontratı gereği).
- `Channel<LogEvent>` multi-producer/single-consumer olarak yapılandırılır — ekstra lock gerekmez.
- Pipeline bileşenleri (enricher, redactor, serializer) **stateless veya thread-safe** olmalıdır.

### 19.2 Lifecycle & Dispose
- `ClickHouseLoggerProvider`: `IAsyncDisposable` implement eder.
- **Dispose sırası**:
  1. Queue'a yeni event kabul etmeyi durdur
  2. Queue'daki mevcut event'leri flush et (timeout: **5 saniye**)
  3. Son batch'i gönder
  4. `HttpClient` dispose et
- `IHostApplicationLifetime.ApplicationStopping` event'ine hook ile graceful shutdown sağlanır.

### 19.3 Graceful Shutdown (HostedService)
- Pipeline, `BackgroundService` olarak çalışır (veya `IHostedService` implement eder).
- `StopAsync(CancellationToken)` çağrıldığında kalan logları flush eder.
- Timeout aşılırsa kalan event'ler drop edilir + `DroppedEvents` counter güncellenir.

### 19.4 Memory Management
- `ArrayPool<byte>` ile buffer pooling — batch serialization sırasında allocation minimize.
- `LogEvent` struct **değil** class olacak (Channel ile uyumluluk), ancak mümkün olduğunca pooling uygulanacak.
- Büyük `exception` string'leri truncate edilecek (default max: **8 KB**).

### 19.5 Dependency İlkesi
- `Abstractions` paketi: **sıfır dış bağımlılık** (sadece BCL).
- `Core` paketi: yalnızca `Abstractions`'a bağımlı.
- `Sinks.ClickHouse`: `Core` + `System.Net.Http`.
- `Extensions.Logging`: `Core` + `Microsoft.Extensions.Logging.Abstractions`.
- `Integrations.AspNetCore`: `Extensions.Logging` + `Microsoft.AspNetCore.Http.Abstractions`.

---

## 20) Coding Conventions

### 20.1 Genel Kurallar
- **Nullable reference types**: Tüm projelerde `<Nullable>enable</Nullable>`.
- **Implicit usings**: Aktif (`<ImplicitUsings>enable</ImplicitUsings>`).
- **File-scoped namespaces**: `namespace X;` stili kullanılacak.
- **Primary constructors**: DI inject'leri için tercih edilir (C# 12).
- **EditorConfig**: Repo kökünde `.editorconfig` ile enforced.

### 20.2 Namespace Stratejisi
```
ClickHouseLogger.Abstractions        → Models, interfaces, enums
ClickHouseLogger.Core.Pipeline       → Queue, Batcher, Flusher
ClickHouseLogger.Core.Redaction      → Redactor, patterns
ClickHouseLogger.Core.Serialization  → Utf8JsonWriter-based serializer
ClickHouseLogger.Core.Diagnostics    → Counters, InternalLog
ClickHouseLogger.Sinks.ClickHouse    → HttpClickHouseWriter, RetryHandler
ClickHouseLogger.Extensions.Logging  → Provider, Logger
ClickHouseLogger.Integrations.AspNetCore → Middleware, Enrichers
```

### 20.3 Visibility
- Kullanıcıya açık API: `public`
- Pipeline iç bileşenleri: `internal` (test projeleri `InternalsVisibleTo` ile erişir)
- Tüm `public` API üzerinde **XML doc comment** zorunlu.

### 20.4 Naming
- Async metotlar: `...Async` suffix
- Cancellation token: her async metotta son parametre
- Options/Config: `ClickHouseLoggerOptions` (tek options sınıfı)
- Constants: `static class Defaults` içinde toplanır

### 20.5 Analyzers
- `Microsoft.CodeAnalysis.NetAnalyzers` — tüm kurallar aktif
- `CA1848` (Use `LoggerMessage.Define`) — **enforced** (high-performance logging patterns için)
- Src projelerinde **warnings as errors**, test projelerinde warnings izinli.

---

## 21) How to Run / How to Test

### 21.1 Ön Koşullar
- .NET 8 SDK
- Docker (ClickHouse integration test'leri ve lokal geliştirme için)

### 21.2 Lokal Geliştirme
```bash
# 1. Repo'yu klonla
git clone https://github.com/<owner>/IronLogger.git
cd IronLogger

# 2. ClickHouse'u Docker ile ayağa kaldır
docker-compose up -d

# 3. Şemayı oluştur
docker exec -i ironlogger-clickhouse clickhouse-client < docs/schema.sql

# 4. Build
dotnet build -c Debug

# 5. Sample çalıştır
dotnet run --project samples/Sample.AspNetCore
```

### 21.3 Test Komutları
```bash
# Unit tests
dotnet test tests/ClickHouseLogger.Tests.Unit -c Release

# Integration tests (Docker ClickHouse çalışıyor olmalı)
dotnet test tests/ClickHouseLogger.Tests.Integration -c Release

# Tüm testler
dotnet test -c Release

# Coverage raporu
dotnet test -c Release --collect:"XPlat Code Coverage" --results-directory ./coverage
```

### 21.4 NuGet Pack (Lokal)
```bash
dotnet pack -c Release -o ./artifacts
```

### 21.5 Load Test
```bash
# ClickHouse çalışıyor olmalı
dotnet run --project samples/LoadGen -- --events 1000000 --concurrency 4
```

---

## 22) Lisans

**MIT License**

Repo kökündeki `LICENSE` dosyası MIT License tam metni ile oluşturulacaktır. Tüm NuGet paketlerinin `.csproj` dosyalarında `<PackageLicenseExpression>MIT</PackageLicenseExpression>` tanımlı olacaktır.

---

# Ek: MVP Default Değerler (Kilit)
Bu değerler README'de "recommended defaults" olarak geçecek ve Options default'ları bunlar olacak:

| Parametre | Default Değer |
|---|---|
| `BatchSize` | `2000` |
| `FlushInterval` | `1s` |
| `MaxQueueItems` | `200_000` |
| `Compression` | `Gzip` |
| `DropPolicy` | `DropDebugWhenBusy` |
| `MinLevel` | `Information` |
| `MaxRetries` | `5` |
| `BaseDelayMs` | `200` |
| `MaxDelayMs` | `5000` |
| `HttpTimeout` | `10s` |
| `RedactionEnabled` | `true` |
| `RedactKeys` | `[password, token, authorization, secret, apiKey, iban, tckn]` |
| `MaxExceptionLength` | `8192` (8 KB) |
| `MaxPropsDepth` | `4` |
| `FlushOnDisposeTimeout` | `5s` |

---

# Revision History

| Tarih | Versiyon | Değişiklik |
|---|---|---|
| 2026-02-22 | 0.1 | İlk MVP spec taslağı |
| 2026-02-22 | 0.2 | PM Review sonrası revizyon: Tech Stack, Coding Conventions, Architecture Constraints, How to Run/Test, License bölümleri eklendi. ASP.NET Core integration MVP'ye dahil edildi. Regex defaults açıkça listelendi. Token Auth MVP'ye eklendi. Throughput hedefi (≥50K event/s) tanımlandı. Standalone Factory ve HttpClient integration v0.2+'ya taşındı. Internal Diagnostics mekanizması tanımlandı. |
