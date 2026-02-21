# 13 — Release Notes

## [v0.1.0] — 2026-02-22 (MVP Release)

**IronLogger**, .NET uygulamaları için geliştirilmiş, yüksek performanslı ve "tak-çalıştır" özellikli bir **ClickHouse Log Provider** kütüphanesidir. Bu sürüm, projenin MVP (Minimum Viable Product) hedeflerine ulaştığı ve üretim ortamına hazır hale getirildiği ilk resmi sürümdür.

### 🚀 Öne Çıkan Özellikler

#### 1. Yüksek Performanslı Pipeline
- **Non-blocking Kuyruk:** `System.Threading.Channels` kullanılarak tasarlanan asenkron kuyruk yapısı sayesinde uygulama akışı loglama operasyonlarından etkilenmez.
- **Sıfıra Yakın Allocation:** `ArrayPool<byte>` ve `Utf8JsonWriter` kullanımı ile GC (Garbage Collector) baskısı minimize edilmiştir.
- **Batching & Compression:** Loglar belirlenen boyutlarda batch'lenir ve **Gzip** ile sıkıştırılarak ClickHouse'a HTTP üzerinden (JSONEachRow formatında) gönderilir.

#### 2. Tam Entegrasyon
- **Microsoft.Extensions.Logging (MEL):** Standart `ILogger` arayüzü ile tam uyumludur. `AddClickHouse()` metodu ile saniyeler içinde projeye eklenebilir.
- **ASP.NET Core Middleware:** Request lifecycle loglarını (Path, Method, StatusCode, Duration) otomatik olarak yakalar.
- **Built-in Enrichers:** `MachineName`, `ProcessId`, `ThreadId` ve `Activity` (TraceId/SpanId) bilgilerini her loga otomatik ekler.

#### 3. Güvenlik ve Uyumluluk (PII Redaction)
- **Hassas Veri Maskeleme:** Email, Kredi Kartı, TCKN ve TR IBAN gibi veriler hem anahtar kelime bazlı hem de Regex ile otomatik olarak maskelenir (`***`).
- **Esnek Yapılandırma:** Kullanıcılar kendi Regex pattern'lerini ve maskelenecek anahtar kelimelerini kolayca tanımlayabilir.

#### 4. Dayanıklılık (Resilience)
- **Retry Policy:** Bağlantı hataları ve geçici ClickHouse sorunları için **Exponential Backoff + Jitter** stratejisi uygulanır.
- **Graceful Shutdown:** Uygulama kapanırken kuyruktaki logların güvenli bir şekilde flush edilmesini sağlar.

### 📊 Performans Değerleri (Benchmark Sonuçları)
Yapılan yük testlerinde (`LoadGen`), MVP hedefi olan 50.000 event/saniye değeri yaklaşık **5.7 kat** aşılmıştır:

| Metrik | Değer |
| --- | --- |
| **Throughput** | **287,838 event/saniye** |
| **Test Kapasitesi** | 3.47 saniyede 1 Milyon log |
| **Dropped Logs** | %0 (Sıfır kayıp) |
| **Peak Memory usage** | 697 MB (Sınırlandırılmış kuyruk ile) |

### 🛠️ Kurulum ve Kullanım

NuGet üzerinden ilgili paketleri yükleyerek başlayabilirsiniz:

```bash
dotnet add package ClickHouseLogger.Extensions.Logging
dotnet add package ClickHouseLogger.Integrations.AspNetCore
```

**appsettings.json yapılandırması:**
```json
{
  "Logging": {
    "ClickHouse": {
      "Endpoint": "http://localhost:8123",
      "Database": "observability",
      "Table": "app_logs",
      "Service": "MySampleApi",
      "Environment": "Production"
    }
  }
}
```

### 📋 Sırada Ne Var? (v0.2+ Backlog)
- **Disk Spooling:** ClickHouse erişilemez olduğunda logların geçici olarak diske yazılması.
- **OpenTelemetry Bridge:** OTel standartları ile tam uyum.
- **RowBinary Support:** Daha da yüksek performans için binary protokol desteği.

### 📄 Lisans
Bu proje **MIT** lisansı ile lisanslanmıştır. Açık kaynak kodlu ve ticari kullanıma uygundur.

---
**Geliştirici:** Sarp Yiğit İlhan & Antigravity AI
