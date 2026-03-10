using OpenEye.FrameCapture;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var redisConn = builder.Configuration.GetConnectionString("redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConn));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
