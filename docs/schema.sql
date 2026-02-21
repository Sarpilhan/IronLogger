-- ============================================================================
-- IronLogger — ClickHouse Schema (Map version)
-- Requires ClickHouse >= 22.3 for Map(String, String) support
-- ============================================================================
--
-- Usage:
--   docker exec -i ironlogger-clickhouse clickhouse-client < docs/schema.sql
--
-- Or directly via clickhouse-client:
--   clickhouse-client --host localhost --port 9000 < docs/schema.sql
--
-- ============================================================================

-- ── 1. Database ─────────────────────────────────────────────────────────────

CREATE DATABASE IF NOT EXISTS observability;

-- ── 2. Main logs table ──────────────────────────────────────────────────────
--
-- Column mapping from IronLogger NdjsonSerializer:
--   ts              → LogEvent.Timestamp  (UTC, DateTime64(3))
--   level           → LogEvent.Level      (Trace/Debug/Information/Warning/Error/Critical)
--   message         → LogEvent.Message    (rendered message string)
--   template        → LogEvent.Template   (original message template, optional)
--   category        → LogEvent.Category   (logger category / class name)
--   exception       → LogEvent.Exception  (full exception string, truncated to 8KB)
--   trace_id        → LogEvent.TraceId    (W3C trace id from Activity)
--   span_id         → LogEvent.SpanId     (W3C span id from Activity)
--   correlation_id  → LogEvent.CorrelationId (HttpContext.TraceIdentifier or custom)
--   service         → LogEvent.Service    (from ClickHouseLoggerOptions.Service)
--   env             → LogEvent.Environment (from ClickHouseLoggerOptions.Environment)
--   version         → LogEvent.Version    (app version, optional)
--   host            → LogEvent.Host       (machine name, optional)
--   props           → LogEvent.Props      (structured properties as Map)

CREATE TABLE IF NOT EXISTS observability.app_logs
(
    ts              DateTime64(3, 'UTC'),
    level           LowCardinality(String),
    message         String,
    template        String          DEFAULT '',
    category        LowCardinality(String),
    exception       String          DEFAULT '',
    trace_id        String          DEFAULT '',
    span_id         String          DEFAULT '',
    correlation_id  String          DEFAULT '',
    service         LowCardinality(String),
    env             LowCardinality(String),
    version         LowCardinality(String) DEFAULT '',
    host            LowCardinality(String) DEFAULT '',
    props           Map(String, String)
)
ENGINE = MergeTree
PARTITION BY toYYYYMM(ts)
ORDER BY (service, env, level, ts, trace_id)
TTL ts + INTERVAL 30 DAY DELETE
SETTINGS index_granularity = 8192;

-- ── 3. Secondary index for trace lookups (optional, improves trace queries) ─

ALTER TABLE observability.app_logs
    ADD INDEX IF NOT EXISTS idx_trace_id trace_id TYPE bloom_filter(0.01) GRANULARITY 4;

ALTER TABLE observability.app_logs
    ADD INDEX IF NOT EXISTS idx_correlation_id correlation_id TYPE bloom_filter(0.01) GRANULARITY 4;

-- ── 4. INSERT-only user for production ──────────────────────────────────────
--
-- Security best practice: the logger should use a restricted user
-- that can ONLY insert into the logs table.
--
-- ⚠️  Change the password before using in production!

-- CREATE USER IF NOT EXISTS logger_writer
--     IDENTIFIED BY 'CHANGE_ME_IN_PRODUCTION'
--     DEFAULT DATABASE observability;
--
-- GRANT INSERT ON observability.app_logs TO logger_writer;

-- ── 5. Materialized view for error alerting (optional) ──────────────────────
--
-- Aggregates error/critical events per service per minute.
-- Useful for dashboards and alerting.

-- CREATE MATERIALIZED VIEW IF NOT EXISTS observability.error_counts_per_minute
-- ENGINE = SummingMergeTree
-- PARTITION BY toYYYYMM(minute)
-- ORDER BY (service, env, level, minute)
-- AS
-- SELECT
--     toStartOfMinute(ts) AS minute,
--     service,
--     env,
--     level,
--     count()            AS event_count
-- FROM observability.app_logs
-- WHERE level IN ('Error', 'Critical')
-- GROUP BY minute, service, env, level;

-- ── 6. Sample queries ───────────────────────────────────────────────────────
--
-- Recent errors for a service:
--   SELECT ts, level, message, exception, trace_id
--   FROM observability.app_logs
--   WHERE service = 'payment-api' AND level IN ('Error', 'Critical')
--   ORDER BY ts DESC
--   LIMIT 100;
--
-- Trace lookup (distributed tracing):
--   SELECT ts, level, message, category, span_id, props
--   FROM observability.app_logs
--   WHERE trace_id = '0af7651916cd43dd8448eb211c80319c'
--   ORDER BY ts;
--
-- Log volume per service (last 24h):
--   SELECT service, env, level, count() AS cnt
--   FROM observability.app_logs
--   WHERE ts >= now() - INTERVAL 24 HOUR
--   GROUP BY service, env, level
--   ORDER BY cnt DESC;
--
-- Search by property value:
--   SELECT ts, message, props['userId'] AS user_id
--   FROM observability.app_logs
--   WHERE props['userId'] = '12345'
--   ORDER BY ts DESC
--   LIMIT 50;
--
-- Partition management (check sizes):
--   SELECT partition, table, rows, formatReadableSize(bytes_on_disk) AS size
--   FROM system.parts
--   WHERE database = 'observability' AND table = 'app_logs' AND active
--   ORDER BY partition;
--
-- Manual TTL cleanup (force merge expired partitions):
--   OPTIMIZE TABLE observability.app_logs FINAL;
