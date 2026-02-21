# 03 — Scope

## MVP Scope (In)

### Paketler (5 zorunlu)
1. **ClickHouseLogger.Abstractions** — Core modeller, interface'ler, enum'lar, options
2. **ClickHouseLogger.Core** — Pipeline: queue, batch, redact, serialize, enrich, diagnostics
3. **ClickHouseLogger.Sinks.ClickHouse** — HTTP writer (JSONEachRow + gzip + retry)
4. **ClickHouseLogger.Extensions.Logging** — MEL ILoggerProvider
5. **ClickHouseLogger.Integrations.AspNetCore** — Request lifecycle middleware

### Altyapı
- Solution yapısı (monorepo, Directory.Build.props, .editorconfig)
- docker-compose.yml (ClickHouse lokal)
- docs/schema.sql + schema_no_map.sql
- CI/CD (GitHub Actions)
- README, LICENSE, CONTRIBUTING.md

### Test
- Unit tests (xUnit + FluentAssertions + Moq)
- Integration tests (ClickHouse Docker)
- Load test harness (LoadGen — ≥ 50K event/s)

### Samples
- Sample.AspNetCore — Web API örneği
- Sample.Worker — BackgroundService örneği
- LoadGen — Throughput benchmark

---

## Out of Scope (v0.2+)
- Standalone `ClickHouseLoggerFactory`
- Outgoing `HttpClient` call logger
- Disk spool (durable buffer)
- OpenTelemetry exporter
- RowBinary serialization
- Console/file fallback sinks
- Monitoring UI
- ClickHouse native TCP protocol
