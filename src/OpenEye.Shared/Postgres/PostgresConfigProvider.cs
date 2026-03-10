using System.Text.Json;
using Dapper;
using Npgsql;
using OpenEye.Shared.Models;

namespace OpenEye.Shared.Postgres;

public class PostgresConfigProvider(NpgsqlDataSource dataSource) : IConfigProvider
{
    public async Task<IReadOnlyList<CameraConfig>> GetCamerasAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<dynamic>(
            "SELECT id, name, stream_url, type, target_fps, enabled FROM cameras");
        return rows.Select(r => new CameraConfig(
            (string)r.id, (string)r.name, (string)r.stream_url,
            (string)r.type, (int)r.target_fps, (bool)r.enabled)).ToList();
    }

    public async Task<IReadOnlyList<Zone>> GetZonesAsync(string? sourceId = null, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var sql = sourceId is null
            ? "SELECT zone_id, source_id, polygon FROM zones"
            : "SELECT zone_id, source_id, polygon FROM zones WHERE source_id = @SourceId";
        var rows = await conn.QueryAsync<dynamic>(sql, new { SourceId = sourceId });
        return rows.Select(r => new Zone(
            (string)r.zone_id,
            (string)r.source_id,
            JsonSerializer.Deserialize<List<Point2D>>((string)r.polygon) ?? []
        )).ToList();
    }

    public async Task<IReadOnlyList<Tripwire>> GetTripwiresAsync(string? sourceId = null, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var sql = sourceId is null
            ? "SELECT tripwire_id, source_id, start_x, start_y, end_x, end_y FROM tripwires"
            : "SELECT tripwire_id, source_id, start_x, start_y, end_x, end_y FROM tripwires WHERE source_id = @SourceId";
        var rows = await conn.QueryAsync<dynamic>(sql, new { SourceId = sourceId });
        return rows.Select(r => new Tripwire(
            (string)r.tripwire_id,
            (string)r.source_id,
            new Point2D((double)r.start_x, (double)r.start_y),
            new Point2D((double)r.end_x, (double)r.end_y)
        )).ToList();
    }

    public async Task<IReadOnlyList<PrimitiveConfig>> GetPrimitivesAsync(string? sourceId = null, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var sql = sourceId is null
            ? "SELECT name, type, class_label, zone_id, tripwire_id, source_id FROM primitive_configs"
            : "SELECT name, type, class_label, zone_id, tripwire_id, source_id FROM primitive_configs WHERE source_id = @SourceId";
        var rows = await conn.QueryAsync<dynamic>(sql, new { SourceId = sourceId });
        return rows.Select(r => new PrimitiveConfig(
            (string)r.name,
            Enum.Parse<PrimitiveType>((string)r.type),
            (string)r.class_label,
            (string?)r.zone_id,
            (string?)r.tripwire_id,
            (string)r.source_id
        )).ToList();
    }

    public async Task<IReadOnlyList<RuleDefinition>> GetRulesAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<dynamic>(
            "SELECT rule_id, name, object_class, zone_id, conditions, logic, cooldown_seconds, tripwire_id, sustained_seconds, within_seconds, min_occurrences, evidence_type FROM rules");
        return rows.Select(r => new RuleDefinition(
            (string)r.rule_id,
            (string)r.name,
            (string)r.object_class,
            (string?)r.zone_id,
            (string?)r.tripwire_id,
            JsonSerializer.Deserialize<List<ConditionConfig>>((string)r.conditions) ?? [],
            Logic: (string?)r.logic ?? "all",
            Cooldown: r.cooldown_seconds is int cs ? TimeSpan.FromSeconds(cs) : null,
            Sustained: r.sustained_seconds is double ss ? TimeSpan.FromSeconds(ss) : null,
            Within: r.within_seconds is double ws ? TimeSpan.FromSeconds(ws) : null,
            MinOccurrences: (int?)r.min_occurrences,
            EvidenceType: r.evidence_type is string et ? Enum.Parse<EvidenceType>(et) : null
        )).ToList();
    }

    public async Task<IReadOnlyList<NotificationConfig>> GetNotificationsAsync(string? ruleId = null, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var sql = ruleId is null
            ? "SELECT rule_id, channels FROM notification_configs"
            : "SELECT rule_id, channels FROM notification_configs WHERE rule_id = @RuleId";
        var rows = await conn.QueryAsync<dynamic>(sql, new { RuleId = ruleId });
        return rows.Select(r => new NotificationConfig(
            (string)r.rule_id,
            JsonSerializer.Deserialize<List<NotificationChannel>>((string)r.channels) ?? []
        )).ToList();
    }
}
