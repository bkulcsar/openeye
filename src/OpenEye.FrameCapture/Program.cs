using Npgsql;
using OpenEye.FrameCapture;
using OpenEye.Shared;
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

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
