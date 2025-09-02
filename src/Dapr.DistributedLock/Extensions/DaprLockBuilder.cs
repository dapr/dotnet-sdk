using Microsoft.Extensions.DependencyInjection;

namespace Dapr.DistributedLock.Extensions;

/// <summary>
/// Used by the fluent registration builder to configure a Dapr distributed lock client.
/// </summary>
/// <param name="services"></param>
public sealed class DaprLockBuilder(IServiceCollection services) : IDaprDistributedLockBuilder
{
    /// <summary>
    /// The registered services on the builder.
    /// </summary>
    public IServiceCollection Services { get; } = services;
}
