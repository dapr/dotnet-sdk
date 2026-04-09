using Dapr.Client;
using Dapr.Extensions.Configuration;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Dapr.IntegrationTest.Configuration;

public sealed class SecretStoreOptionalTests
{
    [Fact]
    public async Task ShouldPopulateSecretsWhenSidecarStartsLate()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("secretstore-components");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildSecretStore();

        var httpPort = PortUtilities.GetAvailablePort();
        var grpcPort = PortUtilities.GetAvailablePort();
        harness.SetPorts(httpPort, grpcPort);

        // Write component files before harness init (harness also writes them, but we need them for the client)
        SecretStoreHarness.WriteSecretsFile(componentsDir);
        SecretStoreHarness.WriteComponentYaml(componentsDir);

        using var client = new DaprClientBuilder()
            .UseHttpEndpoint($"http://localhost:{httpPort}")
            .UseGrpcEndpoint($"http://localhost:{grpcPort}")
            .Build();

        var config = new ConfigurationBuilder()
            .AddDaprSecretStore(
                SecretStoreHarness.SecretStoreComponentName,
                client,
                TimeSpan.FromSeconds(2),
                optional: true)
            .Build();

        // Sidecar not running yet — config should be empty.
        Assert.Null(config["secret1"]);

        // Register reload callback before starting the sidecar.
        var reloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ChangeToken.OnChange(config.GetReloadToken, () => reloaded.TrySetResult());

        // Start the sidecar on the pre-assigned ports.
        await harness.InitializeAsync(TestContext.Current.CancellationToken);

        // Wait for the background loader to populate config.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await reloaded.Task.WaitAsync(timeoutCts.Token).WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal("value1", config["secret1"]);
        Assert.Equal("value2", config["secret2"]);

        await harness.DisposeAsync();
    }

    [Fact]
    public async Task ShouldBlockAndPopulateSecretsWhenNotOptional()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("secretstore-components");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildSecretStore();

        await harness.InitializeAsync(TestContext.Current.CancellationToken);

        using var client = new DaprClientBuilder()
            .UseHttpEndpoint($"http://localhost:{harness.DaprHttpPort}")
            .UseGrpcEndpoint($"http://localhost:{harness.DaprGrpcPort}")
            .Build();

        // optional defaults to false, so Build() blocks until sidecar responds.
        var config = new ConfigurationBuilder()
            .AddDaprSecretStore(
                SecretStoreHarness.SecretStoreComponentName,
                client,
                TimeSpan.FromSeconds(30))
            .Build();

        Assert.Equal("value1", config["secret1"]);
        Assert.Equal("value2", config["secret2"]);

        await harness.DisposeAsync();
    }

    [Fact]
    public async Task ShouldExitCleanlyWhenDisposedDuringBackgroundLoad()
    {
        // Pick free ports — no sidecar will run on them.
        var httpPort = PortUtilities.GetAvailablePort();
        var grpcPort = PortUtilities.GetAvailablePort();

        using var client = new DaprClientBuilder()
            .UseHttpEndpoint($"http://localhost:{httpPort}")
            .UseGrpcEndpoint($"http://localhost:{grpcPort}")
            .Build();

        var config = new ConfigurationBuilder()
            .AddDaprSecretStore(
                SecretStoreHarness.SecretStoreComponentName,
                client,
                TimeSpan.FromMilliseconds(500),
                optional: true)
            .Build();

        // Config should be empty since there's no sidecar.
        Assert.Null(config["secret1"]);

        // Dispose immediately to trigger cancellation of the background task.
        (config as IDisposable)?.Dispose();

        // Allow background task to process cancellation.
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Test completes within xunit default timeout — no hang.
    }
}
