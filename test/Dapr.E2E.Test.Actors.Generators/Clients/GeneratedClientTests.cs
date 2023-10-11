using Dapr.Actors;
using Dapr.Actors.Client;
using Xunit.Abstractions;

namespace Dapr.E2E.Test.Actors.Generators.Clients;

public class GeneratedClientTests
{
    private readonly ILoggerProvider testLoggerProvider;

    public GeneratedClientTests(ITestOutputHelper testOutputHelper)
    {
        this.testLoggerProvider = new XUnitLoggingProvider(testOutputHelper);
    }

    [Fact]
    public async Task TestGeneratedClientAsync()
    {
        var portManager = new PortManager();

        (int appPort, int clientAppHttpPort) = portManager.ReservePorts();

        var templateSidecarOptions = new DaprSidecarOptions("template-app")
        {
            LoggerFactory = new LoggerFactory(new[] { this.testLoggerProvider }),
            LogLevel = "debug"
        };

        var serviceAppSidecarOptions = templateSidecarOptions with
        {
            AppId = "service-app",
            AppPort = appPort
        };

        var clientAppSidecarOptions = templateSidecarOptions with
        {
            AppId = "client-app",
            DaprHttpPort = clientAppHttpPort
        };

        await using var app = ActorWebApplicationFactory.Create(
            new ActorWebApplicationOptions(options =>
            {
                options.UseJsonSerialization = true;
                options.Actors.RegisterActor<RemoteActor>();
            })
            {
                LoggerProvider = this.testLoggerProvider,
                Url = $"http://localhost:{appPort}"
            });

        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));

        //
        // Start application...
        //

        await app.StartAsync(cancellationTokenSource.Token);

        //
        // Start sidecars...
        //

        await using var serviceAppSidecar = DaprSidecarFactory.Create(serviceAppSidecarOptions);

        await serviceAppSidecar.StartAsync(cancellationTokenSource.Token);

        await using var clientAppSidecar = DaprSidecarFactory.Create(clientAppSidecarOptions);

        await clientAppSidecar.StartAsync(cancellationTokenSource.Token);

        //
        // Ensure actor is ready...
        //

        var actorId = ActorId.CreateRandom();
        var actorType = "RemoteActor";
        var actorOptions = new ActorProxyOptions { HttpEndpoint = $"http://localhost:{clientAppHttpPort}" };

        await ActorState.EnsureReadyAsync<IRemoteActor>(actorId, actorType, actorOptions, cancellationTokenSource.Token);

        //
        // Start test...
        //

        var actorProxy = ActorProxy.Create(actorId, actorType, actorOptions);

        var client = new ClientActorClient(actorProxy);

        var result = await client.GetStateAsync(cancellationTokenSource.Token);

        await client.SetStateAsync(new ClientState("updated state"), cancellationTokenSource.Token);
    }
}
