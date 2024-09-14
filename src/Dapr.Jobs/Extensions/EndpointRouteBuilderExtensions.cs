// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

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
        ArgumentNullException.ThrowIfNull(endpoints, nameof(endpoints));
        ArgumentNullException.ThrowIfNull(jobName, nameof(jobName));
        ArgumentNullException.ThrowIfNull(handler, nameof(handler));
        
        return endpoints.MapPost($"/job/{jobName}", handler);
    }
}
