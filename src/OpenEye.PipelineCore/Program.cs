using Npgsql;
using OpenEye.Abstractions;
using OpenEye.PipelineCore;
using OpenEye.Shared;
using OpenEye.PipelineCore.Features;
using OpenEye.PipelineCore.Pipeline;
using OpenEye.PipelineCore.Primitives;
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Rules.Conditions;
using OpenEye.Shared.Postgres;
using OpenEye.Shared.Redis;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var redisConn = builder.Configuration.GetConnectionString("redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConn));

var pgConn = builder.Configuration.GetConnectionString("openeye") ?? "";
builder.Services.AddSingleton(NpgsqlDataSource.Create(pgConn));
builder.Services.AddSingleton<PostgresConfigProvider>();
builder.Services.AddSingleton<IConfigProvider>(sp => sp.GetRequiredService<PostgresConfigProvider>());
builder.Services.AddSingleton<RedisConfigNotifier>();

// Feature extractors (stateless — shared across cameras)
builder.Services.AddSingleton<IFeatureExtractor, ObjectFeatureExtractor>();
builder.Services.AddSingleton<IFeatureExtractor, ZoneFeatureExtractor>();

// Primitive extraction (stateless)
builder.Services.AddSingleton<IPrimitiveExtractor, DefaultPrimitiveExtractor>();

// Rule conditions (stateless — needed to build per-camera rule engines)
builder.Services.AddSingleton<IRuleCondition, DurationCondition>();
builder.Services.AddSingleton<IRuleCondition, CountAboveCondition>();
builder.Services.AddSingleton<IRuleCondition, LineCrossCondition>();
builder.Services.AddSingleton<IRuleCondition, SpeedCondition>();
builder.Services.AddSingleton<IRuleCondition, PresenceCondition>();
builder.Services.AddSingleton<IRuleCondition, AbsenceCondition>();
builder.Services.AddSingleton<IConditionRegistry>(sp =>
    new ConditionRegistry(sp.GetServices<IRuleCondition>()));

// Pipeline (stateful per-camera state is managed inside PipelineOrchestrator)
builder.Services.AddSingleton<IGlobalEventBus, LocalEventBus>();
builder.Services.AddSingleton<PipelineOrchestrator>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
