using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Networks;

namespace Dapr.TestContainers.Infrastructure;

/// <summary>
/// Represents an async lease for a pooled Docker network.
/// Disposing the lease returns the network to the pool (it does not dispose the network).
/// </summary>
public sealed class DockerNetworkLease : IAsyncDisposable
{
    private readonly INetwork _network;
    private readonly Action<INetwork> _config;
    private bool _returned;
    
    internal DockerNetworkLease(INetwork network, Action<INetwork> config)
    {
        _network = network;
        _config = config;
    }

    /// <summary>
    /// The network represented in the lease.
    /// </summary>
    public INetwork Network => _network;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_returned)
            return ValueTask.CompletedTask;

        _returned = true;
        _config(_network);
        return ValueTask.CompletedTask;
    }
}
