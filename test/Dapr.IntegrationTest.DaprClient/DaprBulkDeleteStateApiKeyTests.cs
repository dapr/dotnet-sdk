using Dapr.Client;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Harnesses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.DaprClient;

public sealed class DaprBulkDeleteStateApiKeyTests
{
    private const string DaprApiTokenEnvVarName = "DAPR_API_TOKEN";

    [Fact]
    public async Task DeleteBulkStateAsync_WithDaprApiToken_DeletesStateSuccessfully()
    {
        const string daprApiToken = "state-bulk-delete-token";
        var originalToken = Environment.GetEnvironmentVariable(DaprApiTokenEnvVarName);

        Environment.SetEnvironmentVariable(DaprApiTokenEnvVarName, daprApiToken);

        try
        {
            var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");

            var options = new DaprRuntimeOptions().WithDaprApiToken(daprApiToken);

            await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(options, needsActorState: true, cancellationToken: TestContext.Current.CancellationToken);
            await environment.StartAsync(TestContext.Current.CancellationToken);

            var harness = new DaprHarnessBuilder(componentsDir)
                .WithEnvironment(environment)
                .WithOptions(options)
                .BuildWorkflow();
            await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
                .ConfigureServices(builder =>
                {
                    builder.Services.AddDaprClient((sp, b) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            b.UseGrpcEndpoint(grpcEndpoint);
                        b.UseDaprApiToken(daprApiToken);
                    });
                })
                .BuildAndStartAsync();

            using var scope = testApp.CreateScope();
            var daprClient = scope.ServiceProvider.GetRequiredService<Client.DaprClient>();

            const string storeName = Testcontainers.Constants.DaprComponentNames.StateManagementComponentName;
            const string key1 = "bulk-delete-key-1";
            const string key2 = "bulk-delete-key-2";

            // Save two items so we can bulk-delete them
            await daprClient.SaveStateAsync(storeName, key1, "value1", cancellationToken: TestContext.Current.CancellationToken);
            await daprClient.SaveStateAsync(storeName, key2, "value2", cancellationToken: TestContext.Current.CancellationToken);

            var deleteItems = new List<BulkDeleteStateItem>
            {
                new BulkDeleteStateItem(key1, null!),
                new BulkDeleteStateItem(key2, null!),
            };

            // DeleteBulkStateAsync must propagate the API token; without the fix this throws Unauthenticated
            await daprClient.DeleteBulkStateAsync(storeName, deleteItems, cancellationToken: TestContext.Current.CancellationToken);

            // Verify both items are gone
            var result1 = await daprClient.GetStateAsync<string>(storeName, key1, cancellationToken: TestContext.Current.CancellationToken);
            var result2 = await daprClient.GetStateAsync<string>(storeName, key2, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Null(result1);
            Assert.Null(result2);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DaprApiTokenEnvVarName, originalToken);
        }
    }
}
