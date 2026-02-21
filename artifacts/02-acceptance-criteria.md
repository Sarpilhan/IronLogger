# 02 — Acceptance Criteria

## MVP Kabul Kriterleri (PROJECT_PROFILE.md §17'den)

| # | Kriter | Faz | Doğrulama Yöntemi |
|---|---|---|---|
| AC-1 | .NET 8 üzerinde 5 zorunlu paket build + test geçer | P4 | `dotnet build && dotnet test` |
| AC-2 | MEL provider ile ILogger çalışır, scope/state property'leri taşınır | P2 | Integration test |
| AC-3 | ClickHouse'a HTTP + JSONEachRow + gzip ile yazılır | P1 | Smoke test + integration test |
| AC-4 | Bounded queue + batching çalışır; DropDebugWhenBusy test ile doğrulanır | P1 | Unit test |
| AC-5 | Redaction (key + regex) çalışır ve test ile doğrulanır | P1 | Unit test |
| AC-6 | ASP.NET Core middleware ile request lifecycle logları yazılır | P3 | Integration test |
| AC-7 | docs/schema.sql ve schema_no_map.sql mevcut, README'de kullanım net | P1 | File exists + review |
| AC-8 | Auth: Basic Auth ve Token Auth implement ve test edilmiş | P1+P2 | Unit + integration test |
| AC-9 | CI pipeline nupkg artifacts üretir | P4 | GitHub Actions green |
| AC-10 | README: kurulum, config, örnekler, troubleshooting içerir | P4 | Manual review |
| AC-11 | Load test: ≥ 50K event/s sustained throughput (LoadGen ile) | P4 | LoadGen benchmark |
| AC-12 | Unit + Integration test coverage: ≥ %80 | P4 | Coverage report |
| AC-13 | Internal diagnostics: self-log mekanizması çalışır, batch failure raporlanır | P1 | Unit test |
