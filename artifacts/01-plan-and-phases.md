# 01 — Plan & Phases

## Faz Stratejisi
Proje **4 faz**a bölünmüştür. Her faz kendi içinde tamamlanabilir, test edilebilir ve değer üretir.  
**Faz 1 en küçük dilim** olarak tasarlanmıştır: pipeline'ın uçtan uca çalıştığını kanıtlar.

---

## Phase 1 — Foundation & Core Pipeline  
**Hedef:** Abstractions + Core + Sink paketlerini oluştur. Bir `LogEvent` alıp ClickHouse'a HTTP ile yazabilen minimal pipeline'ı ayağa kaldır.  
**Süre Tahmini:** ~3–4 oturum  

### Kapsam
1. Solution & proje yapısı oluştur (monorepo, Directory.Build.props, .editorconfig)
2. `ClickHouseLogger.Abstractions` — modeller, interface'ler, enum'lar, options
3. `ClickHouseLogger.Core` — pipeline bileşenleri:
   - LogEvent modeli
   - Enricher (static + correlation)
   - Redactor (key + regex)
   - Serializer (Utf8JsonWriter → NDJSON)
   - Batcher (size/time trigger)
   - BoundedQueue (Channel<T> + drop policy)
   - InternalLog (self-diagnostics)
   - DiagnosticsSnapshot (counters)
4. `ClickHouseLogger.Sinks.ClickHouse` — HTTP writer:
   - HttpClickHouseWriter (POST + gzip + retry)
   - Auth (Basic + Token)
5. Unit tests — tüm Core bileşenleri için
6. `docs/schema.sql` + `docs/schema_no_map.sql`
7. `docker-compose.yml` (ClickHouse lokal)

### Deliverables
- 3 proje build eder
- Unit test'ler geçer
- Konsol test harness ile ClickHouse'a log yazılabilir (manual smoke test)

### Gate
- `dotnet build -c Release` → 0 hata
- `dotnet test` → tüm unit testler geçer
- Redaction unit test'leri: key + regex doğrulanır
- Serializer: valid NDJSON output doğrulanır
- Drop policy: queue dolu senaryosu test edilir

### Riskler
- ClickHouse tablo şeması + JSONEachRow uyumu (Map vs JSON string)
- Utf8JsonWriter ile NDJSON batching'de buffer yönetimi

---

## Phase 2 — MEL Provider & Integration Tests  
**Hedef:** `ILoggerProvider` implement et. Standart `ILogger` ile entegre çalışsın. ClickHouse'a gerçekten yazıldığını integration test ile doğrula.  
**Süre Tahmini:** ~2–3 oturum  

### Kapsam
1. `ClickHouseLogger.Extensions.Logging` — MEL provider:
   - `ClickHouseLoggerProvider` (ILoggerProvider, IAsyncDisposable)
   - `ClickHouseLogger` (ILogger)
   - `AddClickHouse(...)` extension method
   - Scope/state property extraction
   - Graceful shutdown (flush on dispose)
2. Integration test projesi:
   - Docker ClickHouse ile end-to-end test
   - Insert + query + row count doğrulama
   - Auth senaryoları (Basic + Token)
3. `samples/Sample.Worker` — minimal backup service örneği

### Deliverables
- 4 proje build eder
- `ILogger` ile log yazılır, ClickHouse'da satır görülür
- Integration test'ler CI'da çalışır

### Gate
- `dotnet test` → unit + integration test'ler geçer
- Sample.Worker çalışır, loglar ClickHouse'da görülür
- Graceful shutdown: queue'daki veriler flush edilir

### Riskler
- MEL lifetime yönetimi (DI container dispose sırası)
- Docker ClickHouse CI'da stabil calışması

---

## Phase 3 — ASP.NET Core Integration & Samples  
**Hedef:** ASP.NET Core middleware ekle. Sample projeler hazırla. Request lifecycle logları yazılsın.  
**Süre Tahmini:** ~2 oturum  

### Kapsam
1. `ClickHouseLogger.Integrations.AspNetCore`:
   - `UseClickHouseRequestLogging()` middleware
   - Request event: method, path, status_code, duration_ms, correlation_id
   - Exception handling (error level log)
   - `IClickHouseEnricher` interface + built-in enrichers
2. `samples/Sample.AspNetCore` — Web API örneği
3. Sample projelerin README'leri

### Deliverables
- 5 zorunlu paket build eder
- ASP.NET Core app request/response logları ClickHouse'da
- Enricher chain çalışır

### Gate
- Middleware integration test'i geçer
- Sample.AspNetCore çalışır, request logları görülür
- Custom enricher eklenebilir

### Riskler
- Middleware sırası ve exception pipeline etkileşimi

---

## Phase 4 — Polish, LoadGen, CI/CD & Release  
**Hedef:** Load test aracı, CI pipeline, NuGet packaging, README, ve tüm DoD maddelerini tamamla.  
**Süre Tahmini:** ~2–3 oturum  

### Kapsam
1. `samples/LoadGen` — throughput benchmark aracı
   - ≥ 50K event/s hedefi doğrulama
   - Ölçüm: events/s, p99 latency, GC allocations
2. CI/CD:
   - `.github/workflows/ci.yml` (build + test + pack)
   - `.github/workflows/release.yml` (tag → NuGet publish)
   - Code coverage raporu (≥ %80)
3. Documentation:
   - `README.md` — kurulum, config, örnekler, troubleshooting
   - `CONTRIBUTING.md`
   - `LICENSE` (MIT)
4. Final QA pass — tüm DoD maddeleri (13 madde) doğrulanır
5. NuGet paket metadata (.csproj bilgileri)

### Deliverables
- Tüm 13 DoD maddesi sağlanır
- `v0.1.0` tag ile release yapılabilir durumda
- README profesyonel ve eksiksiz

### Gate
- Load test: ≥ 50K event/s geçer
- Coverage: ≥ %80
- CI pipeline yeşil
- README: kurulum → ilk log → 5 dakikadan kısa

### Riskler
- Throughput hedefine ulaşamama (serializer/gzip optimizasyonu gerekebilir)
- NuGet publish token/CI secrets yapılandırması
