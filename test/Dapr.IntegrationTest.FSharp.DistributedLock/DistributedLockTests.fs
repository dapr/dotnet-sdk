#nowarn "FS3261"
#nowarn "57"

namespace Dapr.IntegrationTest.FSharp.DistributedLock

open System
open Dapr.DistributedLock
open Dapr.DistributedLock.Extensions
open Dapr.DistributedLock.Models
open Dapr.Testcontainers
open Dapr.Testcontainers.Common
open Dapr.Testcontainers.Harnesses
open global.Dapr.Testcontainers.Xunit.Attributes
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open global.Xunit

type DistributedLockTests() =
    [<Fact>]
    member _.ShouldAcquireAndReleaseLock() = task {
        let componentsDir = TestDirectoryManager.CreateTestDirectory("fsharp-distributedlock-components")
        let resourceId = $"resource-{Guid.NewGuid():N}"
        let owner = $"owner-{Guid.NewGuid():N}"

        use! env = DaprTestEnvironment.CreateWithPooledNetworkAsync(cancellationToken = TestContext.Current.CancellationToken)
        do! env.StartAsync(TestContext.Current.CancellationToken)

        let harness = DaprHarnessBuilder(componentsDir).WithEnvironment(env).BuildDistributedLock()
        use! testApp =
            DaprHarnessBuilder.ForHarness(harness)
                .ConfigureServices(fun builder ->
                    builder.Services.AddDaprDistributedLock(
                        Action<IServiceProvider, DaprDistributedLockBuilder>(
                            fun sp clientBuilder ->
                                let config = sp.GetRequiredService<IConfiguration>()
                                let grpcEndpoint = config["DAPR_GRPC_ENDPOINT"]
                                if not (String.IsNullOrEmpty(grpcEndpoint)) then
                                    clientBuilder.UseGrpcEndpoint(grpcEndpoint) |> ignore))
                    |> ignore)
                .BuildAndStartAsync()

        let componentName = DistributedLockHarness.DistributedLockComponentName
        Assert.NotNull(componentName)

        use scope = testApp.CreateScope()
        let client = scope.ServiceProvider.GetRequiredService<DaprDistributedLockClient>()

        let! acquired =
            client.TryLockAsync(
                componentName,
                resourceId,
                owner,
                expiryInSeconds = 10,
                cancellationToken = TestContext.Current.CancellationToken
            )
        Assert.NotNull(acquired)

        let! unlock =
            client.TryUnlockAsync(
                componentName,
                resourceId,
                owner,
                TestContext.Current.CancellationToken
            )
        Assert.Equal(LockStatus.Success, unlock.Status)
    }
