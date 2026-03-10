-- docker/init.sql
-- PostgreSQL schema for OpenEye
-- Creates all tables needed by backend services (PostgresConfigProvider) and the event router.
-- Prisma migrations may also manage these tables for the dashboard; CREATE TABLE IF NOT EXISTS
-- ensures they exist even before Prisma runs.

-- ── Cameras ──────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS cameras (
    id           TEXT PRIMARY KEY,
    name         TEXT NOT NULL,
    stream_url   TEXT NOT NULL,
    type         TEXT NOT NULL DEFAULT 'rtsp',
    target_fps   INT NOT NULL DEFAULT 5,
    enabled      BOOLEAN NOT NULL DEFAULT TRUE,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ── Zones ────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS zones (
    zone_id      TEXT PRIMARY KEY,
    name         TEXT NOT NULL,
    source_id    TEXT NOT NULL,
    polygon      JSONB NOT NULL,
    type         TEXT NOT NULL DEFAULT 'zone',
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ── Tripwires ────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS tripwires (
    tripwire_id  TEXT PRIMARY KEY,
    source_id    TEXT NOT NULL,
    start_x      DOUBLE PRECISION NOT NULL,
    start_y      DOUBLE PRECISION NOT NULL,
    end_x        DOUBLE PRECISION NOT NULL,
    end_y        DOUBLE PRECISION NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ── Rules ────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS rules (
    rule_id          TEXT PRIMARY KEY,
    name             TEXT NOT NULL,
    camera_id        TEXT NOT NULL,
    object_class     TEXT NOT NULL,
    zone_id          TEXT,
    tripwire_id      TEXT,
    enabled          BOOLEAN NOT NULL DEFAULT TRUE,
    conditions       JSONB NOT NULL DEFAULT '[]',
    logic            TEXT NOT NULL DEFAULT 'all',
    cooldown_seconds INT NOT NULL DEFAULT 30,
    sustained_seconds DOUBLE PRECISION,
    within_seconds   DOUBLE PRECISION,
    min_occurrences  INT,
    evidence_type    TEXT,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ── Primitive Configs ────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS primitive_configs (
    name         TEXT PRIMARY KEY,
    type         TEXT NOT NULL,
    class_label  TEXT NOT NULL,
    zone_id      TEXT,
    tripwire_id  TEXT,
    source_id    TEXT NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ── Notification Configs ─────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS notification_configs (
    rule_id      TEXT PRIMARY KEY,
    channels     JSONB NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ── Events ───────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS events (
    event_id     TEXT PRIMARY KEY,
    event_type   TEXT NOT NULL,
    timestamp    TIMESTAMPTZ NOT NULL,
    source_id    TEXT NOT NULL,
    zone_id      TEXT,
    rule_id      TEXT NOT NULL,
    tracked_objects JSONB NOT NULL DEFAULT '[]',
    metadata     JSONB,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_events_source_id ON events (source_id);
CREATE INDEX IF NOT EXISTS idx_events_timestamp ON events (timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_events_event_type ON events (event_type);
