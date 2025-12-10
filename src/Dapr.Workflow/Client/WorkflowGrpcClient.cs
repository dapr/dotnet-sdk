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

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using grpc = Dapr.DurableTask.Protobuf;
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow.Client;

/// <summary>
/// The gRPC-based implementation of the Workflow client.
/// </summary>
internal sealed class WorkflowGrpcClient(grpc.TaskHubSidecarService.TaskHubSidecarServiceClient grpcClient, ILogger<WorkflowGrpcClient> logger) : WorkflowClient
{
    public override async Task<string> ScheduleNewWorkflowAsync(string workflowName, object? input = null, StartWorkflowOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var instanceId = options?.InstanceId ?? Guid.NewGuid().ToString();
        
        var request = new grpc.CreateInstanceRequest
        {
            InstanceId = instanceId,
            Name = workflowName,
            Input = SerializeToJson(input),
            Version = string.Empty // Not using versions
        };
        
        // Add scheduled start time if specified
        if (options?.StartAt is { } startAt)
        {
            request.ScheduledStartTimestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(startAt);
        }

        var response = await grpcClient.StartInstanceAsync(request, cancellationToken: cancellationToken);
        logger.LogScheduleWorkflowSuccess(workflowName, instanceId);
        return response.InstanceId;
    }

    public override async Task<WorkflowMetadata?> GetWorkflowMetadataAsync(string instanceId, bool getInputsAndOutputs = true,
        CancellationToken cancellationToken = default)
    {
        var request = new grpc.GetInstanceRequest
        {
            InstanceId = instanceId,
            GetInputsAndOutputs = getInputsAndOutputs
        };

        try
        {
            var response = await grpcClient.GetInstanceAsync(request, cancellationToken: cancellationToken);

            if (!response.Exists)
            {
                logger.LogGetWorkflowMetadataInstanceNotFound(instanceId);
                return null;
            }

            return ProtoConverters.ToWorkflowMetadata(response.OrchestrationState);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            logger.LogGetWorkflowMetadataInstanceNotFound(ex, instanceId);
            return null;
        }
    }

    public override async Task<WorkflowMetadata> WaitForWorkflowStartAsync(string instanceId, bool getInputsAndOutputs = true,
        CancellationToken cancellationToken = default)
    {
        // Poll until the workflow status (not Pending)
        while (true)
        {
            var metadata = await GetWorkflowMetadataAsync(instanceId, getInputsAndOutputs, cancellationToken);

            if (metadata is null)
            {
                var ex = new InvalidOperationException($"Workflow instance '{instanceId}' does not exist");
                logger.LogWaitForStartException(ex, instanceId);
                throw ex;
            }

            if (metadata.RuntimeStatus != WorkflowRuntimeStatus.Pending)
            {
                logger.LogWaitForStartCompleted(instanceId, metadata.RuntimeStatus);
                return metadata;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
    }

    public override async Task<WorkflowMetadata> WaitForWorkflowCompletionAsync(string instanceId, bool getInputsAndOutputs = true,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var metadata = await GetWorkflowMetadataAsync(instanceId, getInputsAndOutputs, cancellationToken);

            if (metadata is null)
            {
                var ex = new InvalidOperationException($"Workflow instance '{instanceId}' does not exist");
                logger.LogWaitForCompletionException(ex, instanceId);
                throw ex;
            }

            if (IsTerminalStatus(metadata.RuntimeStatus))
            {
                logger.LogWaitForCompletionCompleted(instanceId, metadata.RuntimeStatus);
                return metadata;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    public override async Task RaiseEventAsync(string instanceId, string eventName, object? eventPayload = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);
        ArgumentException.ThrowIfNullOrEmpty(eventName);

        var request = new grpc.RaiseEventRequest
        {
            InstanceId = instanceId, Name = eventName, Input = SerializeToJson(eventPayload)
        };

        await grpcClient.RaiseEventAsync(request, cancellationToken: cancellationToken);
        logger.LogRaisedEvent(eventName, instanceId);
    }

    public override async Task TerminateWorkflowAsync(string instanceId, object? output = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);
        var request = new grpc.TerminateRequest
        {
            InstanceId = instanceId,
            Output = SerializeToJson(output),
            Recursive = true // Terminate child workflows too
        };

        await grpcClient.TerminateInstanceAsync(request, cancellationToken: cancellationToken);
        logger.LogTerminateWorkflow(instanceId);
    }

    public override async Task SuspendWorkflowAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);

        var request = new grpc.SuspendRequest { InstanceId = instanceId, Reason = reason ?? string.Empty };

        await grpcClient.SuspendInstanceAsync(request, cancellationToken: cancellationToken);
        logger.LogSuspendWorkflow(instanceId);
    }

    public override async Task ResumeWorkflowAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);

        var request = new grpc.ResumeRequest
        {
            InstanceId = instanceId,
            Reason = reason ?? string.Empty
        };

        await grpcClient.ResumeInstanceAsync(request, cancellationToken: cancellationToken);
        logger.LogResumedWorkflow(instanceId);
    }

    public override async Task<bool> PurgeInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);
        
        var request = new grpc.PurgeInstancesRequest
        {
            InstanceId = instanceId,
            Recursive = true // Purge child workflows too
        };

        var response = await grpcClient.PurgeInstancesAsync(request, cancellationToken: cancellationToken);
        var purged = response.DeletedInstanceCount > 0;

        if (purged)
        {
            logger.LogPurgedWorkflowSuccessfully(instanceId);
        }
        else
        {
            logger.LogPurgedWorkflowUnsuccessfully(instanceId);
        }

        return purged;
    }

    public override ValueTask DisposeAsync()
    {
        // The gRPC client is managed by IHttpClientFactory, no disposal needed
        return ValueTask.CompletedTask;
    }

    private static string SerializeToJson(object? obj) => obj == null ? string.Empty : JsonSerializer.Serialize(obj);

    private static bool IsTerminalStatus(WorkflowRuntimeStatus status) =>
        status is WorkflowRuntimeStatus.Completed or WorkflowRuntimeStatus.Failed
            or WorkflowRuntimeStatus.Terminated;
}
