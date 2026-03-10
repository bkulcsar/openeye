using System.Text.Json;
using Dapper;
using Npgsql;
using OpenEye.Shared.Models;

namespace OpenEye.Shared.Postgres;

public class PostgresEventStore(NpgsqlDataSource dataSource)
{
    public async Task SaveEventAsync(Event evt, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await conn.ExecuteAsync("""
            INSERT INTO events (event_id, event_type, timestamp, source_id, zone_id, rule_id, tracked_objects, metadata)
            VALUES (@EventId, @EventType, @Timestamp, @SourceId, @ZoneId, @RuleId, @TrackedObjects::jsonb, @Metadata::jsonb)
            ON CONFLICT (event_id) DO NOTHING
            """, new
        {
            evt.EventId,
            evt.EventType,
            evt.Timestamp,
            evt.SourceId,
            evt.ZoneId,
            evt.RuleId,
            TrackedObjects = JsonSerializer.Serialize(evt.TrackedObjects),
            Metadata = JsonSerializer.Serialize(evt.Metadata)
        });
    }

    public async Task<IReadOnlyList<Event>> QueryEventsAsync(
        string? sourceId = null, string? ruleId = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        int page = 1, int limit = 20, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var sql = """
            SELECT event_id, event_type, timestamp, source_id, zone_id, rule_id, tracked_objects, metadata
            FROM events WHERE 1=1
            """;
        if (sourceId is not null) sql += " AND source_id = @SourceId";
        if (ruleId is not null) sql += " AND rule_id = @RuleId";
        if (from is not null) sql += " AND timestamp >= @From";
        if (to is not null) sql += " AND timestamp <= @To";
        sql += " ORDER BY timestamp DESC LIMIT @Limit OFFSET @Offset";

        var rows = await conn.QueryAsync<dynamic>(sql, new
        {
            SourceId = sourceId, RuleId = ruleId, From = from, To = to,
            Limit = limit, Offset = (page - 1) * limit
        });

        return rows.Select(r => new Event(
            (string)r.event_id,
            (string)r.event_type,
            (DateTimeOffset)r.timestamp,
            (string)r.source_id,
            (string?)r.zone_id,
            JsonSerializer.Deserialize<List<TrackedObject>>((string)r.tracked_objects) ?? [],
            (string)r.rule_id,
            r.metadata is not null ? JsonSerializer.Deserialize<Dictionary<string, object>>((string)r.metadata) : null
        )).ToList();
    }
}
