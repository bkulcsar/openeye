using Npgsql;
using OpenEye.Abstractions;
using OpenEye.EventRouter;
using OpenEye.Shared;
using OpenEye.Shared.Postgres;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var redisConn = builder.Configuration.GetConnectionString("redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConn));

var pgConn = builder.Configuration.GetConnectionString("openeye") ?? "";
builder.Services.AddSingleton(NpgsqlDataSource.Create(pgConn));
builder.Services.AddSingleton<PostgresEventStore>();
builder.Services.AddSingleton<IConfigProvider, PostgresConfigProvider>();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<INotificationDispatcher, WebhookNotificationDispatcher>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
