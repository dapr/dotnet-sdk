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
    /// Provides for a handler to be provided that allows the user to dictate how various jobs should be handled without
    /// necessarily knowing the name of the job at build time.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="action">The asynchronous action provided by the developer that handles any inbound requests. The first two
    /// parameters must be a <see cref="string"/> for the jobName and the originally registered ReadOnlyMemory&lt;byte&gt; with the
    /// payload value, but otherwise can be populated with additional services to be injected into the delegate.</param>
    /// <param name="cancellationToken">Cancellation token that will be passed in as the last parameter to the delegate action.</param>
    public static IEndpointRouteBuilder MapDaprScheduledJobHandler(this IEndpointRouteBuilder endpoints,
        Delegate action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoints, nameof(endpoints));
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        endpoints.MapPost("/job/{jobName}", async context =>
        {
            //Retrieve the name of the job from the request path
            var jobName = string.Empty;
            if (context.Request.RouteValues.TryGetValue("jobName", out var capturedJobName))
            {
                jobName = (string)capturedJobName!;
            }

            //Retrieve the job payload from the request body
            ReadOnlyMemory<byte> payload = new();
            if (context.Request.ContentLength is > 0)
            {
                using var streamContent = new StreamContent(context.Request.Body);
                payload = await streamContent.ReadAsByteArrayAsync(cancellationToken);
            }

            var parameters = new Dictionary<Type, object>
            {
                { typeof(string), jobName },
                { typeof(ReadOnlyMemory<byte>), payload },
                { typeof(CancellationToken), CancellationToken.None }
            };

            var actionParameters = action.Method.GetParameters();
            var invokeParameters = new object?[actionParameters.Length];

            for (var a = 0; a < actionParameters.Length; a++)
            {
                var parameterType = actionParameters[a].ParameterType;

                if (parameters.TryGetValue(parameterType, out var value))
                {
                    invokeParameters[a] = value;
                }
                else
                {
                    invokeParameters[a] = context.RequestServices.GetService(parameterType);
                }
            }
            
            var result = action.DynamicInvoke(invokeParameters.ToArray());
            if (result is Task task)
            {
                await task;
            }
        });

        return endpoints;
    }
}
