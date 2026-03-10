-- docker/init.sql
-- PostgreSQL schema for OpenEye events (used by Event Router)
-- Prisma manages camera/zone/rule tables; this handles the event-router's table.

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
