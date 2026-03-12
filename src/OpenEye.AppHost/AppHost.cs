var builder = DistributedApplication.CreateBuilder(args);

var redis = builder
    .AddRedis("redis")
    .WithHostPort(6379);

var postgresUserName = builder.AddParameter("postgresUserName", "openeye");
var postgresPassword = builder.AddParameter("postgresPassword", "openeye");
var postgres = builder.AddPostgres("postgres")
    .WithEnvironment("POSTGRES_DB", "openeye")
    .WithHostPort(5432)
    .WithUserName(postgresUserName)
    .WithPassword(postgresPassword)
    .WithInitFiles("../../docker/init.sql")
    .AddDatabase("openeye");

var roboflow = builder.AddContainer(
        name: "roboflow-inference",
        image: "roboflow/roboflow-inference-server-cpu",
        tag: "latest")
    .WithEndpoint(
        name: "http",
        port: 9001,
        targetPort: 9001,
        isExternal: true)
    .WithEnvironment("ROBOFLOW_API_KEY",
        builder.Configuration["ROBOFLOW_API_KEY"])
    .WithBindMount(
        source: Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".inference/cache"),
        target: "/tmp",
        isReadOnly: false)
    .WithContainerRuntimeArgs("--security-opt=no-new-privileges")
    .WithContainerRuntimeArgs("--cap-drop=ALL")
    .WithContainerRuntimeArgs("--cap-add=NET_BIND_SERVICE");

builder.AddProject<Projects.OpenEye_FrameCapture>("frame-capture")
    .WithReference(redis)
    .WaitFor(redis);

builder.AddProject<Projects.OpenEye_DetectionBridge>("detection-bridge")
    .WithReference(redis)
    .WaitFor(redis)
    .WaitFor(roboflow)
    .WithEnvironment("Roboflow__Url", roboflow.GetEndpoint("http"))
    .WithEnvironment("Roboflow__ModelId", "yolov8n-640");

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
    .WaitFor(postgres)
    .WithEnvironment("DATABASE_URL", postgres.Resource.UriExpression);

builder.Build().Run();
