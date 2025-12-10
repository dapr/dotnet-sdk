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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Workflow.Client;

namespace Dapr.Workflow;

/// <summary>
/// A client for scheduling and managing Dapr Workflow instances.
/// </summary>
/// <remarks>
/// This client provides high-level operations for interacting with workflows running on the Dapr sidecar.
/// It communicates directly via gRPC, bypassing the generate-purpose Dapr HTTP API for improved performance.
/// </remarks>
public sealed class DaprWorkflowClient : IDaprWorkflowClient
{
    private readonly WorkflowClient _innerClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprWorkflowClient"/> class.
    /// </summary>
    /// <param name="innerClient">The Durable Task client used to communicate with the Dapr sidecar.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="innerClient"/> is <c>null</c>.</exception>
    internal DaprWorkflowClient(WorkflowClient innerClient)
    {
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
    }
    
    /// <summary>
    /// Schedules a new workflow instance for execution.
    /// </summary>
    /// <param name="workflowName">The name of the workflow to schedule.</param>
    /// <param name="instanceId">
    /// The unique ID for the workflow instance. If not specified, a GUID is auto-generated.
    /// </param>
    /// <param name="input">
    /// The optional input to pass to the workflow. Must be serializable via System.Text.Json.
    /// </param>
    /// <returns>The instance ID of the scheduled workflow.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="workflowName"/> is null or empty.</exception>
    public Task<string> ScheduleNewWorkflowAsync(
        string workflowName,
        string? instanceId = null,
        object? input = null) =>
        ScheduleNewWorkflowAsync(workflowName, instanceId, input, null, CancellationToken.None);
    
    /// <summary>
    /// Schedules a new workflow instance for execution at a specified time.
    /// </summary>
    /// <param name="workflowName">The name of the workflow to schedule.</param>
    /// <param name="instanceId">
    /// The unique ID for the workflow instance. If not specified, a GUID is auto-generated.
    /// </param>
    /// <param name="input">The optional input to pass to the workflow.</param>
    /// <param name="startTime">
    /// The time when the workflow should start. If in the past or <c>null</c>, the workflow starts immediately.
    /// </param>
    /// <returns>The instance ID of the scheduled workflow.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="workflowName"/> is null or empty.</exception>
    public Task<string> ScheduleNewWorkflowAsync(
        string workflowName,
        string? instanceId,
        object? input,
        DateTime? startTime) =>
        ScheduleNewWorkflowAsync(workflowName, instanceId, input, startTime.HasValue ? new DateTimeOffset(startTime.Value) : null, CancellationToken.None);
    
    /// <summary>
    /// Schedules a new workflow instance for execution at a specified time.
    /// </summary>
    /// <param name="workflowName">The name of the workflow to schedule.</param>
    /// <param name="instanceId">The unique ID for the workflow instance. Auto-generated if not specified.</param>
    /// <param name="input">The optional input to pass to the workflow.</param>
    /// <param name="startTime">The time when the workflow should start. If in the past or <c>null</c>, starts immediately.</param>
    /// <param name="cancellation">Token to cancel the scheduling operation.</param>
    /// <returns>The instance ID of the scheduled workflow.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="workflowName"/> is null or empty.</exception>
    public Task<string> ScheduleNewWorkflowAsync(
        string workflowName,
        string? instanceId,
        object? input,
        DateTimeOffset? startTime,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowName);

        var options = new StartWorkflowOptions
        {
            InstanceId = instanceId,
            StartAt = startTime
        };

        return _innerClient.ScheduleNewWorkflowAsync(workflowName, input, options, cancellation);
    }
    
    /// <summary>
    /// Gets the current metadata and state of a workflow instance.
    /// </summary>
    /// <param name="instanceId">The unique ID of the workflow instance to retrieve.</param>
    /// <param name="getInputsAndOutputs">
    /// <c>true</c> to include serialized inputs, outputs, and custom status; <c>false</c> to omit them.
    /// Omitting can reduce network bandwidth and memory usage.
    /// </param>
    /// <param name="cancellation">Token to cancel the retrieval operation.</param>
    /// <returns>
    /// A <see cref="WorkflowMetadata"/> object, or <c>null</c> if the workflow instance does not exist.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="instanceId"/> is null or empty.</exception>
    public async Task<WorkflowState?> GetWorkflowStateAsync(
        string instanceId,
        bool getInputsAndOutputs = true,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);
        var metadata = await _innerClient.GetWorkflowMetadataAsync(instanceId, getInputsAndOutputs, cancellation);
        return metadata is null ? null : new WorkflowState(metadata);
    }
    
    /// <summary>
    /// Waits for a workflow instance to transition from the pending state to an active state.
    /// </summary>
    /// <remarks>
    /// Returns immediately if the workflow has already started or completed.
    /// </remarks>
    /// <param name="instanceId">The unique ID of the workflow instance to wait for.</param>
    /// <param name="getInputsAndOutputs">
    /// <c>true</c> to include serialized inputs, outputs, and custom status; <c>false</c> to omit them.
    /// </param>
    /// <param name="cancellation">Token to cancel the wait operation.</param>
    /// <returns>
    /// A <see cref="WorkflowMetadata"/> object describing the workflow state once it has started.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="instanceId"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the workflow instance does not exist.</exception>
    public async Task<WorkflowState> WaitForWorkflowStartAsync(
        string instanceId,
        bool getInputsAndOutputs = true,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);
        var metadata = await _innerClient.WaitForWorkflowStartAsync(instanceId, getInputsAndOutputs, cancellation);
        return new WorkflowState(metadata);
    }
    
    /// <summary>
    /// Waits for a workflow instance to reach a terminal state (completed, failed, or terminated).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This operation may block indefinitely for eternal workflows. Ensure appropriate timeouts
    /// are enforced using the <paramref name="cancellation"/> parameter.
    /// </para>
    /// <para>
    /// Returns immediately if the workflow is already in a terminal state.
    /// </para>
    /// </remarks>
    /// <param name="instanceId">The unique ID of the workflow instance to wait for.</param>
    /// <param name="getInputsAndOutputs">
    /// <c>true</c> to include serialized inputs, outputs, and custom status; <c>false</c> to omit them.
    /// </param>
    /// <param name="cancellation">Token to cancel the wait operation.</param>
    /// <returns>
    /// A <see cref="WorkflowMetadata"/> object containing the final state of the completed workflow.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="instanceId"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the workflow instance does not exist.</exception>
    public async Task<WorkflowState> WaitForWorkflowCompletionAsync(
        string instanceId,
        bool getInputsAndOutputs = true,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);
        var metadata = await _innerClient.WaitForWorkflowCompletionAsync(instanceId, getInputsAndOutputs, cancellation);
        return new WorkflowState(metadata);
    }
    
    /// <summary>
    /// Raises an external event for a workflow instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The target workflow must be waiting for this event via <see cref="WorkflowContext.WaitForExternalEventAsync{T}(string, CancellationToken)"/>.
    /// If the workflow is not currently waiting, the event is buffered and delivered when the workflow begins waiting.
    /// </para>
    /// <para>
    /// Events for non-existent or completed workflows are silently discarded.
    /// </para>
    /// </remarks>
    /// <param name="instanceId">The unique ID of the target workflow instance.</param>
    /// <param name="eventName">The name of the event (case-sensitive).</param>
    /// <param name="eventPayload">The optional event data, must be serializable via System.Text.Json.</param>
    /// <param name="cancellation">Token to cancel the event submission operation.</param>
    /// <returns>A task that completes when the event has been enqueued.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="instanceId"/> or <paramref name="eventName"/> is null or empty.</exception>
    public async Task RaiseEventAsync(
        string instanceId,
        string eventName,
        object? eventPayload = null,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        await _innerClient.RaiseEventAsync(instanceId, eventName, eventPayload, cancellation);
    }

    /// <summary>
    /// Terminates a running workflow instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Termination updates the workflow status to <see cref="WorkflowRuntimeStatus.Terminated"/>.
    /// Child workflows are also terminated, but in-flight activities continue to completion.
    /// </para>
    /// </remarks>
    /// <param name="instanceId">The unique ID of the workflow instance to terminate.</param>
    /// <param name="output">Optional output to set as the workflow's result.</param>
    /// <param name="cancellation">Token to cancel the termination request.</param>
    /// <returns>A task that completes when the termination has been enqueued.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="instanceId"/> is null or empty.</exception>
    public async Task TerminateWorkflowAsync(
        string instanceId,
        object? output = null,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);
        await _innerClient.TerminateWorkflowAsync(instanceId, output, cancellation);
    }

    /// <summary>
    /// Suspends a workflow instance, pausing its execution until resumed.
    /// </summary>
    /// <param name="instanceId">The unique ID of the workflow instance to suspend.</param>
    /// <param name="reason">Optional reason for the suspension.</param>
    /// <param name="cancellation">Token to cancel the suspension request.</param>
    /// <returns>A task that completes when the suspension has been committed.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="instanceId"/> is null or empty.</exception>
    public async Task SuspendWorkflowAsync(
        string instanceId,
        string? reason = null,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);
        await _innerClient.SuspendWorkflowAsync(instanceId, reason, cancellation);
    }

    /// <summary>
    /// Resumes a previously suspended workflow instance.
    /// </summary>
    /// <param name="instanceId">The unique ID of the workflow instance to resume.</param>
    /// <param name="reason">Optional reason for the resumption.</param>
    /// <param name="cancellation">Token to cancel the resume request.</param>
    /// <returns>A task that completes when the resumption has been committed.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="instanceId"/> is null or empty.</exception>
    public async Task ResumeWorkflowAsync(
        string instanceId,
        string? reason = null,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);
        await _innerClient.ResumeWorkflowAsync(instanceId, reason, cancellation);
    }

    /// <summary>
    /// Permanently deletes a workflow instance from the state store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only workflows in terminal states (<see cref="WorkflowRuntimeStatus.Completed"/>,
    /// <see cref="WorkflowRuntimeStatus.Failed"/>, or <see cref="WorkflowRuntimeStatus.Terminated"/>) can be purged.
    /// </para>
    /// <para>
    /// Purging also removes all child workflows and their history records.
    /// </para>
    /// </remarks>
    /// <param name="instanceId">The unique ID of the workflow instance to purge.</param>
    /// <param name="cancellation">Token to cancel the purge operation.</param>
    /// <returns>
    /// A task that completes when the purge operation finishes.
    /// The result is <c>true</c> if successfully purged; <c>false</c> if the workflow doesn't exist or isn't in a terminal state.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="instanceId"/> is null or empty.</exception>
    public async Task<bool> PurgeInstanceAsync(
        string instanceId,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceId);
        return await _innerClient.PurgeInstanceAsync(instanceId, cancellation);
    }

    /// <summary>
    /// Disposes any unmanaged resources associated with this client.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _innerClient.DisposeAsync();
    }
}
