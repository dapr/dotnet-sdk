using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Dapr.Jobs.Extensions;

/// <summary>
/// Provides extension methods to register endpoints for Dapr Job Scheduler invocations.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Adds a <see cref="RouteEndpoint"/> to the <see cref="IEndpointConventionBuilder"/> that registers a
    /// Dapr scheduled job trigger invocation.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="jobName">The name of the job that should trigger this method when invoked.</param>
    /// <param name="handler">The delegate executed when the endpoint is matched.</param>
    /// <returns></returns>
    public static IEndpointConventionBuilder MapDaprScheduledJob(this IEndpointRouteBuilder endpoints, string jobName,
        Delegate handler)
    {
        return endpoints.MapPost($"/job/{jobName}", handler);
    }
}
