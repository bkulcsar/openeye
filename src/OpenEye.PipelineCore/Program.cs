using OpenEye.Abstractions;
using OpenEye.PipelineCore;
using OpenEye.PipelineCore.Features;
using OpenEye.PipelineCore.Pipeline;
using OpenEye.PipelineCore.Primitives;
using OpenEye.PipelineCore.Rules;
using OpenEye.PipelineCore.Rules.Conditions;
using OpenEye.PipelineCore.Tracking;
using OpenEye.PipelineCore.Zones;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var redisConn = builder.Configuration.GetConnectionString("redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConn));

// Tracking
builder.Services.AddSingleton<IObjectTracker, SortTracker>();

// Zone evaluation
builder.Services.AddSingleton<IZoneEvaluator, DefaultZoneEvaluator>();

// Feature extractors (registered as IEnumerable<IFeatureExtractor> via multiple registrations)
builder.Services.AddSingleton<IFeatureExtractor, ObjectFeatureExtractor>();
builder.Services.AddSingleton<IFeatureExtractor, ZoneFeatureExtractor>();
builder.Services.AddSingleton<IFeatureExtractor, TemporalFeatureExtractor>();

// Primitive extraction
builder.Services.AddSingleton<IPrimitiveExtractor, DefaultPrimitiveExtractor>();

// Rule conditions
builder.Services.AddSingleton<IRuleCondition, DurationCondition>();
builder.Services.AddSingleton<IRuleCondition, CountAboveCondition>();
builder.Services.AddSingleton<IRuleCondition, LineCrossCondition>();
builder.Services.AddSingleton<IRuleCondition, SpeedCondition>();
builder.Services.AddSingleton<IRuleCondition, PresenceCondition>();
builder.Services.AddSingleton<IRuleCondition, AbsenceCondition>();
builder.Services.AddSingleton<IConditionRegistry>(sp =>
    new ConditionRegistry(sp.GetServices<IRuleCondition>()));

// Rule engine
builder.Services.AddSingleton<IRuleStateStore, InMemoryRuleStateStore>();
builder.Services.AddSingleton<IRuleEngine, DefaultRuleEngine>();

// Pipeline
builder.Services.AddSingleton<IGlobalEventBus, LocalEventBus>();
builder.Services.AddSingleton<PipelineOrchestrator>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
