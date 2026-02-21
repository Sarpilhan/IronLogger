# 04 — Risks

| # | Risk | Olasılık | Etki | Mitigasyon | Faz |
|---|---|---|---|---|---|
| R1 | ClickHouse Map tipi + JSONEachRow uyumsuzluğu | Orta | Yüksek | İlk işlerden biri olarak Docker'da smoke test yapılacak. Alternatif `props_json String` şeması hazır. | P1 |
| R2 | Utf8JsonWriter ile NDJSON batching buffer overflows | Düşük | Orta | ArrayPool ile pooled buffer + max batch size limiti. Unit test ile edge case doğrulama. | P1 |
| R3 | ≥ 50K event/s throughput hedefine ulaşamama | Orta | Yüksek | Phase 1'de erken benchmark. Bottleneck tespiti (serialize vs gzip vs network). Gzip seviyesi ayarlanabilir, batch size optimize. | P4 |
| R4 | MEL DI container dispose sırası ve graceful shutdown | Düşük | Orta | IHostApplicationLifetime hook + explicit flush timeout (5s). Integration test ile doğrulama. | P2 |
| R5 | Docker ClickHouse CI'da stabil çalışmaması | Düşük | Orta | GitHub Actions service container kullanımı. Retry mekanizması test startup'ta. | P2 |
| R6 | Regex redaction'un throughput'u düşürmesi | Orta | Orta | `RegexOptions.Compiled` kullanımı. Pattern sayısı sınırlı (default 4). Benchmark ile doğrulama. | P1 |
| R7 | ASP.NET Core middleware exception pipeline etkileşimi | Düşük | Düşük | Middleware sırasını dokümante et. UseExceptionHandler'dan sonra yerleştir. | P3 |
| R8 | NuGet publish CI/CD secrets yapılandırması | Düşük | Düşük | Release workflow'u ayrı, manual trigger opsiyonu. | P4 |
