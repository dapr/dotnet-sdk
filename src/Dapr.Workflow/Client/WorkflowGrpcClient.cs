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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common;
using Dapr.Workflow.Serialization;
using Grpc.Core;
using grpc = Dapr.DurableTask.Protobuf;
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow.Client;

/// <summary>
/// The gRPC-based implementation of the Workflow client.
/// </summary>
internal sealed class WorkflowGrpcClient(
    grpc.TaskHubSidecarService.TaskHubSidecarServiceClient grpcClient,
    ILogger<WorkflowGrpcClient> logger,
    IWorkflowSerializer serializer,
    string? daprApiToken = null) : WorkflowClient
{
    /// <inheritdoc />
    public override async Task<string> ScheduleNewWorkflowAsync(string workflowName, object? input = null, StartWorkflowOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var instanceId = options?.InstanceId ?? Guid.NewGuid().ToString();
        
        var request = new grpc.CreateInstanceRequest
        {
            InstanceId = instanceId,
            Name = workflowName,
            Input = SerializeToJson(input)
        };
        
        // Add the scheduled start time if specified
        if (options?.StartAt is { } startAt)
        {
            request.ScheduledStartTimestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(startAt);
        }

        var grpcCallOptions = CreateCallOptions(cancellationToken);
        var response = await grpcClient.StartInstanceAsync(request, grpcCallOptions);
        logger.LogScheduleWorkflowSuccess(workflowName, instanceId);
        return response.InstanceId;
    }

    /// <inheritdoc />
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
            var grpcCallOptions = CreateCallOptions(cancellationToken);
            var response = await grpcClient.GetInstanceAsync(request, grpcCallOptions);

            if (!response.Exists)
            {
                logger.LogGetWorkflowMetadataInstanceNotFound(instanceId);
                return null;
            }

            return ProtoConverters.ToWorkflowMetadata(response.OrchestrationState, serializer);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            logger.LogGetWorkflowMetadataInstanceNotFound(ex, instanceId);
            return null;
        }
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public override async Task<WorkflowMetadata> WaitForWorkflowCompletionAsync(string instanceId, bool getInputsAndOutputs = true, CancellationToken cancellationToken = default)
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

    /// <inheritdoc />
    public override async Task RaiseEventAsync(string instanceId, string eventName, object? eventPayload = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);
        ArgumentException.ThrowIfNullOrEmpty(eventName);

        var request = new grpc.RaiseEventRequest
        {
            InstanceId = instanceId, Name = eventName, Input = SerializeToJson(eventPayload)
        };

        var grpcCallOptions = CreateCallOptions(cancellationToken);
        await grpcClient.RaiseEventAsync(request, grpcCallOptions);
        logger.LogRaisedEvent(eventName, instanceId);
    }

    /// <inheritdoc />
    public override async Task TerminateWorkflowAsync(string instanceId, object? output = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);
        var request = new grpc.TerminateRequest
        {
            InstanceId = instanceId,
            Output = SerializeToJson(output),
            Recursive = true // Terminate child workflows too
        };

        var grpcCallOptions = CreateCallOptions(cancellationToken);
        await grpcClient.TerminateInstanceAsync(request, grpcCallOptions);
        logger.LogTerminateWorkflow(instanceId);
    }

    /// <inheritdoc />
    public override async Task SuspendWorkflowAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);

        var request = new grpc.SuspendRequest { InstanceId = instanceId, Reason = reason ?? string.Empty };

        var grpcCallOptions = CreateCallOptions(cancellationToken);
        await grpcClient.SuspendInstanceAsync(request, grpcCallOptions);
        logger.LogSuspendWorkflow(instanceId);
    }

    /// <inheritdoc />
    public override async Task ResumeWorkflowAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);

        var request = new grpc.ResumeRequest
        {
            InstanceId = instanceId,
            Reason = reason ?? string.Empty
        };

        var grpcCallOptions = CreateCallOptions(cancellationToken);
        await grpcClient.ResumeInstanceAsync(request, grpcCallOptions);
        logger.LogResumedWorkflow(instanceId);
    }

    /// <inheritdoc />
    public override async Task<bool> PurgeInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);
        
        var request = new grpc.PurgeInstancesRequest
        {
            InstanceId = instanceId,
            Recursive = true // Purge child workflows too
        };

        var grpcCallOptions = CreateCallOptions(cancellationToken);
        var response = await grpcClient.PurgeInstancesAsync(request, grpcCallOptions);
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

    /// <inheritdoc />
    public override async Task<WorkflowInstancePage> ListInstanceIdsAsync(
        string? continuationToken = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var request = new grpc.ListInstanceIDsRequest();

        if (continuationToken is not null)
        {
            request.ContinuationToken = continuationToken;
        }

        if (pageSize.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pageSize.Value, 0, nameof(pageSize));
            request.PageSize = (uint)pageSize.Value;
        }

        try
        {
            var grpcCallOptions = CreateCallOptions(cancellationToken);
            var response = await grpcClient.ListInstanceIDsAsync(request, grpcCallOptions);

            logger.LogListInstanceIds(response.InstanceIds.Count);

            return new WorkflowInstancePage(
                response.InstanceIds.ToList().AsReadOnly(),
                response.HasContinuationToken ? response.ContinuationToken : null);
        }
        catch (RpcException ex) when (IsRpcMethodNotSupportedByRuntime(ex))
        {
            throw new NotSupportedException(
                "ListInstanceIDs is not supported by the current Dapr runtime version. Please upgrade to a newer Dapr release.", ex);
        }
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<WorkflowHistoryEvent>> GetInstanceHistoryAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);

        var request = new grpc.GetInstanceHistoryRequest
        {
            InstanceId = instanceId
        };

        try
        {
            var grpcCallOptions = CreateCallOptions(cancellationToken);
            var response = await grpcClient.GetInstanceHistoryAsync(request, grpcCallOptions);

            var events = response.Events
                .Select(ProtoConverters.ToWorkflowHistoryEvent)
                .ToList()
                .AsReadOnly();

            logger.LogGetInstanceHistory(instanceId, events.Count);

            return events;
        }
        catch (RpcException ex) when (IsRpcMethodNotSupportedByRuntime(ex))
        {
            throw new NotSupportedException(
                "GetInstanceHistory is not supported by the current Dapr runtime version. Please upgrade to a newer Dapr release.", ex);
        }
    }

    /// <inheritdoc />
    public override async Task<string> RerunWorkflowFromEventAsync(
        string sourceInstanceId,
        uint eventId,
        RerunWorkflowFromEventOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceInstanceId);

        if (options is { Input: not null, OverwriteInput: false })
        {
            throw new ArgumentException(
                $"{nameof(RerunWorkflowFromEventOptions.OverwriteInput)} must be true when {nameof(RerunWorkflowFromEventOptions.Input)} is set.",
                nameof(options));
        }

        var request = new grpc.RerunWorkflowFromEventRequest
        {
            SourceInstanceID = sourceInstanceId,
            EventID = eventId,
            OverwriteInput = options?.OverwriteInput ?? false
        };

        if (options?.NewInstanceId is not null)
        {
            request.NewInstanceID = options.NewInstanceId;
        }

        if (options is { OverwriteInput: true })
        {
            request.Input = SerializeToJson(options.Input);
        }

        try
        {
            var grpcCallOptions = CreateCallOptions(cancellationToken);
            var response = await grpcClient.RerunWorkflowFromEventAsync(request, grpcCallOptions);

            logger.LogRerunWorkflowFromEvent(sourceInstanceId, eventId, response.NewInstanceID);

            return response.NewInstanceID;
        }
        catch (RpcException ex) when (IsRpcMethodNotSupportedByRuntime(ex))
        {
            throw new NotSupportedException(
                "RerunWorkflowFromEvent is not supported by the current Dapr runtime version. Please upgrade to a newer Dapr release.", ex);
        }
    }

    /// <inheritdoc />
    public override ValueTask DisposeAsync()
    {
        // The gRPC client is managed by IHttpClientFactory, no disposal needed
        return ValueTask.CompletedTask;
    }

    private string SerializeToJson(object? obj) => obj == null ? string.Empty : serializer.Serialize(obj);

    private CallOptions CreateCallOptions(CancellationToken cancellationToken) =>
        DaprClientUtilities.ConfigureGrpcCallOptions(typeof(DaprWorkflowClient).Assembly, daprApiToken, cancellationToken);

    private static bool IsTerminalStatus(WorkflowRuntimeStatus status) =>
        status is WorkflowRuntimeStatus.Completed 
            or WorkflowRuntimeStatus.Failed
            or WorkflowRuntimeStatus.Terminated;

    /// <summary>
    /// Returns <c>true</c> when the <see cref="RpcException"/> indicates that the Dapr sidecar does not
    /// implement the requested gRPC method and fell back to its service-invocation proxy, which then
    /// failed because the workflow client never sends a <c>dapr-app-id</c> header.  This pattern
    /// occurs on older Dapr runtime versions that pre-date the <c>GetInstanceHistory</c>,
    /// <c>ListInstanceIDs</c>, and <c>RerunWorkflowFromEvent</c> RPCs.
    /// The sidecar emits "failed to proxy request: required metadata dapr-callee-app-id or dapr-app-id not found"
    /// in this case.
    /// </summary>
    private static bool IsRpcMethodNotSupportedByRuntime(RpcException ex) =>
        ex.StatusCode == StatusCode.Unknown &&
        ex.Status.Detail.Contains("required metadata", StringComparison.OrdinalIgnoreCase) &&
        ex.Status.Detail.Contains("dapr-app-id", StringComparison.OrdinalIgnoreCase);
}
