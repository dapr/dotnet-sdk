using Dapr.Actors;
using Dapr.Actors.Client;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Xunit.Abstractions;

namespace Dapr.E2E.Test.Actors.Generators;

public class GeneratedClientTests
{
    private readonly ITestOutputHelper testOutputHelper;

    public GeneratedClientTests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task TestGeneratedClientAsync()
    {
        var portManager = new PortManager();

        var reservedPorts = portManager.ReservePorts(5).ToArray();

        var appPort = reservedPorts[0];

        var serviceAppGrpcPort = reservedPorts[1];
        var serviceAppHttpPort = reservedPorts[2];

        var clientAppGrpcPort = reservedPorts[3];
        var clientAppHttpPort = reservedPorts[4];

        var loggerProvider = new XUnitLoggingProvider(this.testOutputHelper);
        var loggerFactory = new LoggerFactory();

        loggerFactory.AddProvider(loggerProvider);

        var serviceAppSidecarOptions = new DaprSidecarOptions("service-app")
        {
            AppPort = appPort,
            DaprGrpcPort = serviceAppGrpcPort,
            DaprHttpPort = serviceAppHttpPort,
            LoggerFactory = loggerFactory,
            LogLevel = "debug"
        };

        var clientAppSidecarOptions = new DaprSidecarOptions("client-app")
        {
            DaprGrpcPort = clientAppGrpcPort,
            DaprHttpPort = clientAppHttpPort,
            LoggerFactory = loggerFactory
        };

        await using var app = ActorWebApplicationFactory.Create(
            new ActorWebApplicationOptions(options =>
            {
                options.UseJsonSerialization = true;
                options.Actors.RegisterActor<RemoteActor>();
            })
            {
                ConfigureBuilder = builder =>
                {
                    builder.Logging.ClearProviders();
                    builder.Logging.AddProvider(loggerProvider);
                }
            });

        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        app.Urls.Add($"http://localhost:{appPort}");
        app.Configuration["DAPR_GRPC_PORT"] = serviceAppGrpcPort.ToString();
        app.Configuration["DAPR_HTTP_PORT"] = serviceAppHttpPort.ToString();

        await app.StartAsync(cancellationTokenSource.Token);

        // TODO: Start the service app sidecar

        await using var serviceAppSidecar = DaprSidecarFactory.Create(serviceAppSidecarOptions);

        await serviceAppSidecar.StartAsync(cancellationTokenSource.Token);

        // TODO: Start the client app sidecar
        await using var clientAppSidecar = DaprSidecarFactory.Create(clientAppSidecarOptions);

        await clientAppSidecar.StartAsync(cancellationTokenSource.Token);

        // TODO: Start the client
        var actorId = new ActorId("test-actor");

        var actorProxy = ActorProxy.Create(actorId, "RemoteActor",
            new ActorProxyOptions
            {
                HttpEndpoint = $"http://localhost:{clientAppHttpPort}",
            });

        var client = new ClientActorClient(actorProxy);

        var result = await client.GetStateAsync(cancellationTokenSource.Token);

        await client.SetStateAsync(new ClientState("updated state"), cancellationTokenSource.Token);
    }
}
