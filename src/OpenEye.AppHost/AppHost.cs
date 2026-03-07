var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");
var postgres = builder.AddPostgres("postgres")
    .AddDatabase("openeye");

builder.AddProject<Projects.OpenEye_FrameCapture>("frame-capture")
    .WithReference(redis);

builder.AddProject<Projects.OpenEye_DetectionBridge>("detection-bridge")
    .WithReference(redis);

builder.AddProject<Projects.OpenEye_PipelineCore>("pipeline-core")
    .WithReference(redis)
    .WithReference(postgres);

builder.AddProject<Projects.OpenEye_EventRouter>("event-router")
    .WithReference(redis)
    .WithReference(postgres);

builder.Build().Run();
