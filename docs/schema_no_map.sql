-- ============================================================================
-- IronLogger — ClickHouse Schema (No Map version)
-- For ClickHouse versions < 22.3 without Map type support
-- Props are stored as a JSON string instead of Map(String, String)
-- ============================================================================
--
-- Usage:
--   docker exec -i ironlogger-clickhouse clickhouse-client < docs/schema_no_map.sql
--
-- Or directly via clickhouse-client:
--   clickhouse-client --host localhost --port 9000 < docs/schema_no_map.sql
--
-- NOTE: When using this schema, configure IronLogger to serialize props
--       as a JSON string to props_json instead of Map to props.
--       This is a future feature; MVP targets the Map schema.
--
-- ============================================================================

-- ── 1. Database ─────────────────────────────────────────────────────────────

CREATE DATABASE IF NOT EXISTS observability;

-- ── 2. Main logs table ──────────────────────────────────────────────────────
--
-- This schema uses a plain String column for properties instead of Map.
-- Querying individual properties requires JSON extraction functions:
--   JSONExtractString(props_json, 'userId')

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
    props_json      String          DEFAULT '{}'
)
ENGINE = MergeTree
PARTITION BY toYYYYMM(ts)
ORDER BY (service, env, level, ts, trace_id)
TTL ts + INTERVAL 30 DAY DELETE
SETTINGS index_granularity = 8192;

-- ── 3. Secondary index for trace lookups ────────────────────────────────────

ALTER TABLE observability.app_logs
    ADD INDEX IF NOT EXISTS idx_trace_id trace_id TYPE bloom_filter(0.01) GRANULARITY 4;

ALTER TABLE observability.app_logs
    ADD INDEX IF NOT EXISTS idx_correlation_id correlation_id TYPE bloom_filter(0.01) GRANULARITY 4;

-- ── 4. INSERT-only user for production ──────────────────────────────────────
--
-- ⚠️  Change the password before using in production!

-- CREATE USER IF NOT EXISTS logger_writer
--     IDENTIFIED BY 'CHANGE_ME_IN_PRODUCTION'
--     DEFAULT DATABASE observability;
--
-- GRANT INSERT ON observability.app_logs TO logger_writer;

-- ── 5. Sample queries ───────────────────────────────────────────────────────
--
-- Recent errors:
--   SELECT ts, level, message, exception
--   FROM observability.app_logs
--   WHERE service = 'payment-api' AND level IN ('Error', 'Critical')
--   ORDER BY ts DESC
--   LIMIT 100;
--
-- Search by property (JSON extraction):
--   SELECT ts, message, JSONExtractString(props_json, 'userId') AS user_id
--   FROM observability.app_logs
--   WHERE JSONExtractString(props_json, 'userId') = '12345'
--   ORDER BY ts DESC
--   LIMIT 50;
--
-- Note: JSON extraction queries are slower than Map key lookups.
-- Upgrade to ClickHouse >= 22.3 and use schema.sql (Map version) for
-- better query performance on structured properties.
