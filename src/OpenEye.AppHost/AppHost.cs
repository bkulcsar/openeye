var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");
var postgres = builder.AddPostgres("postgres")
    .WithEnvironment("POSTGRES_DB", "openeye")
    .WithInitFiles("../../docker/init.sql")
    .AddDatabase("openeye");

builder.AddProject<Projects.OpenEye_FrameCapture>("frame-capture")
    .WithReference(redis)
    .WaitFor(redis);

builder.AddProject<Projects.OpenEye_DetectionBridge>("detection-bridge")
    .WithReference(redis)
    .WaitFor(redis);

builder.AddProject<Projects.OpenEye_PipelineCore>("pipeline-core")
    .WithReference(redis)
    .WithReference(postgres)
    .WaitFor(redis)
    .WaitFor(postgres);

builder.AddProject<Projects.OpenEye_EventRouter>("event-router")
    .WithReference(redis)
    .WithReference(postgres)
    .WaitFor(redis)
    .WaitFor(postgres);

builder.AddViteApp("dashboard", "../../dashboard", "dev")
    .WithReference(postgres)
    .WithReference(redis)
    .WaitFor(postgres);

builder.Build().Run();
