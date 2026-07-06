using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.BackendApp>("be")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppPort = 5247,
        DaprHttpPort = 50001
    });

builder.AddProject<Projects.FrontendApp>("fe")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppPort = 5054,
        DaprHttpPort = 50000
    });

builder.Build().Run();
