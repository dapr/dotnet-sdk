using Dapr.DistributedLock;
using Dapr.DistributedLock.Extensions;
using Dapr.DistributedLock.Models;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.DistributedLock;

public sealed class DistributedLockTests
{
    [Fact]
    public async Task ShouldAcquireAndReleaseLock()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("distributedlock-components");
        var resourceId = $"resource-{Guid.NewGuid():N}";
        var owner = $"owner-{Guid.NewGuid():N}";

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync();
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir).BuildDistributedLock();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprDistributedLock((sp, clientBuilder) =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                    if (!string.IsNullOrEmpty(grpcEndpoint))
                        clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                });
            })
            .BuildAndStartAsync();

        const string componentName = DistributedLockHarness.DistributedLockComponentName;
        Assert.NotNull(componentName);

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprDistributedLockClient>();

        var acquired = await client.TryLockAsync(componentName, resourceId, owner, expiryInSeconds: 10);
        Assert.NotNull(acquired);

        var unlock = await client.TryUnlockAsync(componentName, resourceId, owner);
        Assert.Equal(LockStatus.Success, unlock.Status);
    }

    [Fact]
    public async Task ShouldEnforceExclusivityAndReturnExpectedUnlockStatuses()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("distributedlock-components");
        var resourceId = $"resource-{Guid.NewGuid():N}";
        var owner1 = $"owner-{Guid.NewGuid():N}";
        var owner2 = $"owner-{Guid.NewGuid():N}";

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync();
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir).BuildDistributedLock();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprDistributedLock((sp, clientBuilder) =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                    if (!string.IsNullOrEmpty(grpcEndpoint))
                        clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                });
            })
            .BuildAndStartAsync();

        const string componentName = DistributedLockHarness.DistributedLockComponentName;
        Assert.NotNull(componentName);

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprDistributedLockClient>();

        var lock1 = await client.TryLockAsync(componentName, resourceId, owner1, expiryInSeconds: 20);
        Assert.NotNull(lock1);

        // While owner1 holds the lock, owner2 should not be able to acquire it.
        var lock2 = await client.TryLockAsync(componentName, resourceId, owner2, expiryInSeconds: 20);
        Assert.Null(lock2);

        // Wrong owner tries to unlock -> should indicate ownership mismatch.
        var wrongUnlock = await client.TryUnlockAsync(componentName, resourceId, owner2);
        Assert.Equal(LockStatus.LockBelongsToOthers, wrongUnlock.Status);

        // Correct owner unlocks -> success.
        var correctUnlock = await client.TryUnlockAsync(componentName, resourceId, owner1);
        Assert.Equal(LockStatus.Success, correctUnlock.Status);

        // Unlocking again after release -> lock does not exist.
        var secondUnlock = await client.TryUnlockAsync(componentName, resourceId, owner1);
        Assert.Equal(LockStatus.LockDoesNotExist, secondUnlock.Status);
    }

    [Fact]
    public async Task ShouldAllowAcquireAfterExpiry()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("distributedlock-components");
        var resourceId = $"resource-{Guid.NewGuid():N}";
        var owner1 = $"owner-{Guid.NewGuid():N}";
        var owner2 = $"owner-{Guid.NewGuid():N}";

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync();
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir).BuildDistributedLock();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprDistributedLock((sp, clientBuilder) =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                    if (!string.IsNullOrEmpty(grpcEndpoint))
                        clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                });
            })
            .BuildAndStartAsync();

        const string componentName = DistributedLockHarness.DistributedLockComponentName;
        Assert.NotNull(componentName);

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprDistributedLockClient>();

        // Acquire a short-lived lock and *do not* unlock it.
        var first = await client.TryLockAsync(componentName, resourceId, owner1, expiryInSeconds: 2);
        Assert.NotNull(first);

        // Poll until the lock becomes available and owner2 can acquire it.
        var acquiredByOwner2 = await WaitUntilAsync(
            async () => await client.TryLockAsync(componentName, resourceId, owner2, expiryInSeconds: 10),
            isSuccess: lr => lr is not null,
            timeout: TimeSpan.FromSeconds(30),
            pollInterval: TimeSpan.FromMilliseconds(250));

        Assert.NotNull(acquiredByOwner2);

        var unlock2 = await client.TryUnlockAsync(componentName, resourceId, owner2);
        Assert.Equal(LockStatus.Success, unlock2.Status);
    }

    private static async Task<T?> WaitUntilAsync<T>(
        Func<Task<T?>> action,
        Func<T?, bool> isSuccess,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            var result = await action();
            if (isSuccess(result))
                return result;

            await Task.Delay(pollInterval, cts.Token);
        }

        throw new TimeoutException($"Condition was not met within {timeout}.");
    }
}
