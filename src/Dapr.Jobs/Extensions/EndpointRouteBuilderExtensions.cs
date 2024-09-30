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

using System.Text.Json;
using Dapr.Jobs.Models.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
    /// <param name="action">The asynchronous action provided by the developer that handles any inbound requests. This is provided with an <see cref="IServiceProvider"/>,
    /// the name of the job and the deserialized <see cref="DaprJobDetails"/> payload and is expected to return a <see cref="Task"/>.
    /// by the callback and which expects to be returned a task.</param>
    public static IEndpointRouteBuilder MapDaprScheduledJobHandler(this IEndpointRouteBuilder endpoints, InjectableDaprJobHandler action)
    {
        ArgumentNullException.ThrowIfNull(endpoints, nameof(endpoints));
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        endpoints.MapPost("/job/{jobName}", async context =>
        {
            var serviceProvider = context.RequestServices;
            await HandleDaprJobAsync(context,
                (jobName, jobPayload) => action(serviceProvider, jobName, jobPayload));
        });

        return endpoints;
    }

    /// <summary>
    /// Provides for a handler to be provided that allows the user to dictate how various jobs should be handled without
    /// necessarily knowing the name of the job at build time.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="action">The asynchronous action provided by the developer that handles any inbound requests. This is provided with an <see cref="IServiceProvider"/>,
    /// the name of the job and the deserialized <see cref="DaprJobDetails"/> payload and is expected to return a <see cref="Task"/>.
    /// by the callback and which expects to be returned a task.</param>
    public static IEndpointRouteBuilder MapDaprScheduledJobHandler(this IEndpointRouteBuilder endpoints, DaprJobHandler action)
    {
        ArgumentNullException.ThrowIfNull(endpoints, nameof(endpoints));
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        endpoints.MapPost("/job/{jobName}", async context =>
        {
            await HandleDaprJobAsync(context,
                (jobName, jobPayload) => action(jobName, jobPayload));
        });

        return endpoints;
    }
    

    private static async Task HandleDaprJobAsync(HttpContext context, Func<string?, DaprJobDetails?, Task> action)
    {
        var jobName = (string?)context.Request.RouteValues["jobName"];

        if (context.Request.ContentLength is null or 0)
        {
            await action(jobName, null);
        }
        else
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            DaprJobDetails? jobPayload = null;

            try
            {
                var deserializedJobPayload = JsonSerializer.Deserialize<DeserializableDaprJobDetails>(body);
                jobPayload = deserializedJobPayload?.ToType() ?? null;
            }
            catch (JsonException)
            {
                jobPayload = null;
            }

            await action(jobName, jobPayload);
        }
    }
}
