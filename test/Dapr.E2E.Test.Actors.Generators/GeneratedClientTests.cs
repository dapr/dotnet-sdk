using Dapr.Actors;
using Dapr.Actors.Client;
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

        var reservedPorts = portManager.ReservePorts(2).ToArray();

        var appPort = reservedPorts[0];
        var clientAppHttpPort = reservedPorts[1];

        var loggerProvider = new XUnitLoggingProvider(this.testOutputHelper);
        var loggerFactory = new LoggerFactory();

        loggerFactory.AddProvider(loggerProvider);

        var serviceAppSidecarOptions = new DaprSidecarOptions("service-app")
        {
            AppPort = appPort,
            LoggerFactory = loggerFactory,
            LogLevel = "debug"
        };

        var clientAppSidecarOptions = new DaprSidecarOptions("client-app")
        {
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

        await app.StartAsync(cancellationTokenSource.Token);

        await using var serviceAppSidecar = DaprSidecarFactory.Create(serviceAppSidecarOptions);

        await serviceAppSidecar.StartAsync(cancellationTokenSource.Token);

        await using var clientAppSidecar = DaprSidecarFactory.Create(clientAppSidecarOptions);

        await clientAppSidecar.StartAsync(cancellationTokenSource.Token);

        var actorId = ActorId.CreateRandom();
        var actorType = "RemoteActor";
        var actorOptions = new ActorProxyOptions { HttpEndpoint = $"http://localhost:{clientAppHttpPort}" };

        var pingProxy = ActorProxy.Create<IRemoteActor>(actorId, actorType, actorOptions);

        while (true)
        {
            try
            {
                await pingProxy.Ping();

                break;
            }
            catch (DaprApiException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationTokenSource.Token);
            }
        }

        var actorProxy = ActorProxy.Create(actorId, actorType, actorOptions);

        var client = new ClientActorClient(actorProxy);

        var result = await client.GetStateAsync(cancellationTokenSource.Token);

        await client.SetStateAsync(new ClientState("updated state"), cancellationTokenSource.Token);
    }
}
