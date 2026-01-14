using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;

namespace Dapr.TestContainers.Infrastructure;

/// <summary>
/// Global bounded pool of Docker networks for test runs.
/// Renting a network is an async operation that blocks when the pool is exhausted.
/// </summary>
public static class DockerNetworkPool
{
    // The name of the environment variable containing an overriding value of the pool size to create
    private const string PoolSizeEnvVar = "DAPR_TEST_DOCKER_NETWORK_POOL_SIZE";
    private static readonly int PoolSize = GetPoolSize();

    private static readonly ConcurrentQueue<INetwork> Pool = new();
    private static readonly SemaphoreSlim Gate = new(PoolSize, PoolSize);
    private static volatile bool _initialized;
    private static readonly SemaphoreSlim InitGate = new(1, 1);

    /// <summary>
    /// Used to create a new <see cref="DockerNetworkLease"/> in the pool.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask<DockerNetworkLease> RentAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (Pool.TryDequeue(out var network))
            return new DockerNetworkLease(network, Return);
        
        // Should never happen because Gate capacity matches pool size
        Gate.Release();
        throw new InvalidOperationException("Docker network pool corrupted (no network available).");
    }
    
    private static void Return(INetwork network)
    {
        Pool.Enqueue(network);
        Gate.Release();
    }

    private static async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
            return;

        await InitGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
                return;

            for (var i = 0; i < PoolSize; i++)
            {
                var network = new NetworkBuilder().Build();

                // Ensure the network is created now so we pay that cost once and reuse it
                await network.CreateAsync(cancellationToken).ConfigureAwait(false);

                Pool.Enqueue(network);
            }

            _initialized = true;
        }
        finally
        {
            InitGate.Release();
        }
    }

    private static int GetPoolSize()
    {
        var fromEnv = Environment.GetEnvironmentVariable(PoolSizeEnvVar);
        if (int.TryParse(fromEnv, out var value) && value > 0)
            return value;

        return 4; // default
    }
}
