// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using Dapr.DurableTask;
using Dapr.DurableTask.Client;
using Dapr.DurableTask.Client.Grpc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dapr.Workflow.Test;

/// <summary>
/// Wraps the gRPC Durable Task Client to provide an implementation for mocking.
/// </summary>
public class GrpcDurableTaskClientWrapper : DurableTaskClient
{
    private GrpcDurableTaskClient grpcClient = new("test", new GrpcDurableTaskClientOptions(), NullLogger.Instance);

    public GrpcDurableTaskClientWrapper() : base("fake")
    {
    }

    public override ValueTask DisposeAsync()
    {
        return grpcClient.DisposeAsync();
    }

    public override AsyncPageable<OrchestrationMetadata> GetAllInstancesAsync(OrchestrationQuery? filter = null)
    {
        return grpcClient.GetAllInstancesAsync(filter);
    }

    public override Task<OrchestrationMetadata?> GetInstancesAsync(string instanceId, bool getInputsAndOutputs = false, CancellationToken cancellation = default)
    {
        return grpcClient.GetInstancesAsync(instanceId, getInputsAndOutputs, cancellation);
    }

    public override Task RaiseEventAsync(string instanceId, string eventName, object? eventPayload = null, CancellationToken cancellation = default)
    {
        return grpcClient.RaiseEventAsync(instanceId, eventName, eventPayload, cancellation);
    }

    public override Task ResumeInstanceAsync(string instanceId, string? reason = null, CancellationToken cancellation = default)
    {
        return grpcClient.ResumeInstanceAsync(instanceId, reason, cancellation);
    }

    public override Task<string> ScheduleNewOrchestrationInstanceAsync(TaskName orchestratorName, object? input = null, StartOrchestrationOptions? options = null, CancellationToken cancellation = default)
    {
        return grpcClient.ScheduleNewOrchestrationInstanceAsync(orchestratorName, input, options, cancellation);
    }

    public override Task SuspendInstanceAsync(string instanceId, string? reason = null, CancellationToken cancellation = default)
    {
        return grpcClient.SuspendInstanceAsync(instanceId, reason, cancellation);
    }

    public override Task<OrchestrationMetadata> WaitForInstanceCompletionAsync(string instanceId, bool getInputsAndOutputs = false, CancellationToken cancellation = default)
    {
        return grpcClient.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs, cancellation);
    }

    public override Task<OrchestrationMetadata> WaitForInstanceStartAsync(string instanceId, bool getInputsAndOutputs = false, CancellationToken cancellation = default)
    {
        return grpcClient.WaitForInstanceStartAsync(instanceId, getInputsAndOutputs, cancellation);
    }
}
