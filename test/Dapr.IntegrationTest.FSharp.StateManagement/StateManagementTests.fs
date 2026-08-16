#nowarn "FS3261"

namespace Dapr.IntegrationTest.FSharp.StateManagement

open System
open Dapr.StateManagement
open Dapr.StateManagement.Extensions
open Dapr.Testcontainers
open Dapr.Testcontainers.Common
open Dapr.Testcontainers.Common.Options
open Dapr.Testcontainers.Harnesses
open global.Dapr.Testcontainers.Xunit.Attributes
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open global.Xunit

type StateManagementTests() =
    [<MinimumDaprRuntimeFact("1.17")>]
    member _.ShouldSaveAndRetrieveStateViaTypedClient() = task {
        let componentsDir = TestDirectoryManager.CreateTestDirectory("fsharp-state-components")
        let key = $"widget-{Guid.NewGuid():N}"

        use! env = DaprTestEnvironment.CreateWithPooledNetworkAsync(cancellationToken = TestContext.Current.CancellationToken)
        do! env.StartAsync(TestContext.Current.CancellationToken)

        let harness = StateManagementHarness(componentsDir, null, DaprRuntimeOptions(), env)
        use! testApp =
            DaprHarnessBuilder.ForHarness(harness)
                .ConfigureServices(fun builder ->
                    builder.Services.AddDaprStateManagementClient(
                        Action<IServiceProvider, DaprStateManagementClientBuilder>(
                            fun sp clientBuilder ->
                                let config = sp.GetRequiredService<IConfiguration>()
                                let grpcEndpoint = config["DAPR_GRPC_ENDPOINT"]
                                let httpEndpoint = config["DAPR_HTTP_ENDPOINT"]
                                if not (String.IsNullOrEmpty(grpcEndpoint)) then
                                    clientBuilder.UseGrpcEndpoint(grpcEndpoint) |> ignore
                                if not (String.IsNullOrEmpty(httpEndpoint)) then
                                    clientBuilder.UseHttpEndpoint(httpEndpoint) |> ignore))
                        .WithWidgetStore()
                    |> ignore)
                .BuildAndStartAsync()

        use scope = testApp.CreateScope()
        let store = scope.ServiceProvider.GetRequiredService<IWidgetStore>()

        let widget: Widget = { Size = "medium"; Color = "blue" }
        do! store.SaveStateAsync(key, widget, cancellationToken = TestContext.Current.CancellationToken)

        let! loaded = store.GetStateAsync<Widget>(key, cancellationToken = TestContext.Current.CancellationToken)
        match box loaded with
        | null -> Assert.Fail("Expected loaded state to be non-null")
        | _ ->
            Assert.Equal("medium", loaded.Size)
            Assert.Equal("blue", loaded.Color)

        do!
            store.DeleteStateAsync(key, cancellationToken = TestContext.Current.CancellationToken)
            |> Async.AwaitTask

        let! deleted = store.GetStateAsync<Widget>(key, cancellationToken = TestContext.Current.CancellationToken)
        Assert.Null(box deleted)
    }
