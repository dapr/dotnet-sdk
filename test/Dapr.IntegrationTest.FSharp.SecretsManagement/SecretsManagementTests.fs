#nowarn "FS3261"

namespace Dapr.IntegrationTest.FSharp.SecretsManagement

open System
open Dapr.SecretsManagement
open Dapr.SecretsManagement.Extensions
open Dapr.Testcontainers
open Dapr.Testcontainers.Common
open Dapr.Testcontainers.Common.Options
open Dapr.Testcontainers.Harnesses
open global.Dapr.Testcontainers.Xunit.Attributes
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open global.Xunit

type SecretsManagementTests() =
    [<MinimumDaprRuntimeFact("1.17")>]
    member _.ShouldLoadSecretsViaTypedClient() = task {
        let componentsDir = TestDirectoryManager.CreateTestDirectory("fsharp-secrets-components")

        use! env = DaprTestEnvironment.CreateWithPooledNetworkAsync(cancellationToken = TestContext.Current.CancellationToken)
        do! env.StartAsync(TestContext.Current.CancellationToken)

        let harness = SecretStoreHarness(componentsDir, null, DaprRuntimeOptions(), env)
        use! testApp =
            DaprHarnessBuilder.ForHarness(harness)
                .ConfigureServices(fun builder ->
                    builder.Services.AddDaprSecretsManagementClient(
                        Action<IServiceProvider, DaprSecretsManagementClientBuilder>(
                            fun sp clientBuilder ->
                                let config = sp.GetRequiredService<IConfiguration>()
                                let grpcEndpoint = config["DAPR_GRPC_ENDPOINT"]
                                let httpEndpoint = config["DAPR_HTTP_ENDPOINT"]
                                if not (String.IsNullOrEmpty(grpcEndpoint)) then
                                    clientBuilder.UseGrpcEndpoint(grpcEndpoint) |> ignore
                                if not (String.IsNullOrEmpty(httpEndpoint)) then
                                    clientBuilder.UseHttpEndpoint(httpEndpoint) |> ignore))
                        .AddLocalSecrets()
                    |> ignore)
                .BuildAndStartAsync()

        use scope = testApp.CreateScope()
        let secrets = scope.ServiceProvider.GetRequiredService<ILocalSecrets>()

        Assert.Equal("value1", secrets.Secret1)
        Assert.Equal("value2", secrets.Secret2)
    }
