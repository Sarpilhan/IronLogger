# 00 — Intake

## Proje Adı
**IronLogger** — ClickHouse Logger Factory for .NET

## Talep Özeti
.NET uygulamalarında structured log/event verisini yüksek throughput ile ClickHouse'a yazan, projeden projeye tak-çalıştır kullanılabilen bir Logger Factory + Microsoft.Extensions.Logging provider geliştirmek.

## Talep Tarihi
2026-02-22

## Talep Eden
Sarp Yiğit İlhan (Proje Sahibi)

## Kaynak Doküman
- `PROJECT_PROFILE.md` v0.2 (Revised)

## Temel Kararlar (PM Review'dan)
1. **Lisans**: MIT
2. **ASP.NET Core Integration**: MVP'ye dahil (zorunlu)
3. **Throughput Hedefi**: ≥ 50,000 event/s sustained
4. **Test Framework**: xUnit + FluentAssertions + Moq
5. **Serialization**: System.Text.Json + Utf8JsonWriter (performans kritik)
6. **Standalone Factory**: v0.2+'ya ertelenmiş
7. **HttpClient Integration**: v0.2+'ya ertelenmiş

## MVP Zorunlu Paketler (5)
1. `ClickHouseLogger.Abstractions`
2. `ClickHouseLogger.Core`
3. `ClickHouseLogger.Sinks.ClickHouse`
4. `ClickHouseLogger.Extensions.Logging`
5. `ClickHouseLogger.Integrations.AspNetCore`
