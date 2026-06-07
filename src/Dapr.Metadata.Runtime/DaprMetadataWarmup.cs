using Microsoft.Extensions.Hosting;

namespace Dapr.Metadata.Runtime;

/// <summary>
/// Forches an initial fetch at startup.
/// </summary>
internal sealed class DaprMetadataWarmup(IDaprMetadataProvider provider) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => provider.GetAsync(cancellationToken).AsTask();

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
