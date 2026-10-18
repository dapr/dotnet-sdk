// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Dapr.AppCallback.Autogen.Grpc.v1;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Jobs;

internal sealed class DaprJobsAppCallbackHandler(
    DaprJobsHandlerRegistry registry,
    IServiceProvider serviceProvider)
{
    public async Task<JobEventResponse> HandleAsync(JobEventRequest request)
    {
        var registeredHandler = registry.Handler
            ?? throw new InvalidOperationException(
                "No job handler has been configured. Call MapDaprScheduledJobHandler before the application starts.");

        var jobName = request.Name;
        ReadOnlyMemory<byte> payload =
            request.Data?.Value?.ToByteArray() ?? ReadOnlyMemory<byte>.Empty;

        using var cts = registry.Timeout.HasValue
            ? new CancellationTokenSource(registry.Timeout.Value)
            : new CancellationTokenSource();

        using var scope = serviceProvider.CreateScope();

        var parameters = new Dictionary<Type, object>
        {
            { typeof(string), jobName },
            { typeof(ReadOnlyMemory<byte>), payload },
            { typeof(CancellationToken), cts.Token }
        };

        var actionParameters = registeredHandler.Method.GetParameters();
        var invokeParameters = new object?[actionParameters.Length];

        for (var index = 0; index < actionParameters.Length; index++)
        {
            var parameterType = actionParameters[index].ParameterType;
            invokeParameters[index] = parameters.TryGetValue(parameterType, out var value)
                ? value
                : scope.ServiceProvider.GetService(parameterType);
        }

        var result = registeredHandler.DynamicInvoke(invokeParameters);
        if (result is Task task)
        {
            await task;
        }

        return new JobEventResponse();
    }
}
