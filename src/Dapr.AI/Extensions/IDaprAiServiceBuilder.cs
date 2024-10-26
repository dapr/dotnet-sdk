using Microsoft.Extensions.DependencyInjection;

namespace Dapr.AI.Extensions;

/// <summary>
/// Responsible for registering Dapr AI service functionality.
/// </summary>
public interface IDaprAiServiceBuilder
{
    /// <summary>
    /// The registered services on the builder.
    /// </summary>
    public IServiceCollection Services { get; }
}
