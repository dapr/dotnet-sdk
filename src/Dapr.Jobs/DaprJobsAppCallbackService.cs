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

using Dapr.AppCallback.Autogen.Grpc.v1;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Jobs;

/// <summary>
/// Implements the <see cref="AppCallbackAlpha"/> gRPC service to receive job trigger
/// callbacks from the Dapr runtime over gRPC instead of HTTP.
/// </summary>
internal sealed class DaprJobsAppCallbackService(
    DaprJobsHandlerRegistry registry,
    IServiceProvider serviceProvider) : AppCallbackAlpha.AppCallbackAlphaBase
{
    /// <summary>
    /// Invoked by the Dapr runtime when a scheduled job triggers. The request is forwarded
    /// to the handler delegate registered via <c>MapDaprScheduledJobHandler</c>.
    /// </summary>
    /// <param name="request">The job event request containing the job name and payload.</param>
    /// <param name="context">The gRPC server call context.</param>
    /// <returns>An empty <see cref="JobEventResponse"/>.</returns>
    public override async Task<JobEventResponse> OnJobEventAlpha1(
        JobEventRequest request, ServerCallContext context)
    {
        var handler = registry.Handler
            ?? throw new InvalidOperationException(
                "No job handler has been configured. Call MapDaprScheduledJobHandler before the application starts.");

        var jobName = request.Name;
        ReadOnlyMemory<byte> payload =
            request.Data?.Value?.ToByteArray() ?? ReadOnlyMemory<byte>.Empty;

        using var cts = registry.Timeout.HasValue
            ? new CancellationTokenSource(registry.Timeout.Value)
            : new CancellationTokenSource();

        // Create a DI scope so scoped services can be resolved the same way they are
        // for HTTP requests.
        using var scope = serviceProvider.CreateScope();

        var parameters = new Dictionary<Type, object>
        {
            { typeof(string), jobName },
            { typeof(ReadOnlyMemory<byte>), payload },
            { typeof(CancellationToken), cts.Token }
        };

        var actionParameters = handler.Method.GetParameters();
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
                invokeParameters[a] = scope.ServiceProvider.GetService(parameterType);
            }
        }

        var result = handler.DynamicInvoke(invokeParameters);
        if (result is Task task)
        {
            await task;
        }

        return new JobEventResponse();
    }
}
