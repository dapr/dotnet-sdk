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

namespace Dapr.Workflow.Client;

/// <summary>
/// Abstract base class for workflow client implementations.
/// </summary>
internal abstract class WorkflowClient : IAsyncDisposable
{
    /// <summary>
    /// Schedules a new workflow instance for execution.
    /// </summary>
    /// <param name="workflowName">The name of the workflow to schedule.</param>
    /// <param name="input">The optional input to pass to the scheduled workflow instance. This must be a serializable value using System.Text.Json.</param>
    /// <param name="options">Options configuring the start of the workflow.</param>
    /// <param name="cancellation">Token used to cancel workflow scheduling (only if canceled before it's submitted to the Dapr runtime).</param>
    /// <returns></returns>
    public abstract Task<string> ScheduleNewWorkflowAsync(
        string workflowName,
        object? input = null,
        StartWorkflowOptions? options = null,
        CancellationToken cancellation = default);

    /// <summary>
    /// Gets the metadata for a workflow instance.
    /// </summary>
    /// <param name="instanceId">The identifier of the instance to get the metadata for.</param>
    /// <param name="getInputsAndOutputs">Specify <c>true</c> to fetch the workflow instance's inputs, outputs and
    /// custom status, or <c>false</c> to omit them. Setting the value to <c>false</c> can help minimize the network
    /// bandwidth, serialization, and memory costs associated with fetching the instance metadata.</param>
    /// <param name="cancellationToken">Token used to cancel retrieval of the request for the metadata.</param>
    /// <returns></returns>
    public abstract Task<WorkflowMetadata?> GetWorkflowMetadataAsync(
        string instanceId,
        bool getInputsAndOutputs = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for a workflow to start running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A "started" workflow instance is any instance not in the <see cref="WorkflowRuntimeStatus.Pending"/> state.
    /// </para><para>
    /// This method will return a completed task if the workflow has already started running or has already completed.
    /// </para>
    /// </remarks>
    /// <param name="instanceId">The identifier of the instance to wait for.</param>
    /// <param name="getInputsAndOutputs">Specify <c>true</c> to fetch the workflow instance's inputs, outputs and
    /// custom status, or <c>false</c> to omit them. Setting the value to <c>false</c> can help minimize the network
    /// bandwidth, serialization, and memory costs associated with fetching the instance metadata.</param>
    /// <param name="cancellationToken">Token used to cancel the request to wait for the workflow to start (doesn't impact
    /// scheduling of the workflow itself as this has already happened).</param>
    /// <returns></returns>
    public abstract Task<WorkflowMetadata> WaitForWorkflowStartAsync(
        string instanceId,
        bool getInputsAndOutputs = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for a workflow to complete and returns a <see cref="WorkflowMetadata"/> object that contains metadata about
    /// the started instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A "completed" workflow instance is any instance in one of the terminal states. For example, the
    /// <see cref="WorkflowRuntimeStatus.Completed"/>, <see cref="WorkflowRuntimeStatus.Failed"/>, or
    /// <see cref="WorkflowRuntimeStatus.Terminated"/> states.
    /// </para><para>
    /// Workflows are long-running and could take hours, days, or months before completing.
    /// Workflows can also be eternal, in which case they'll never complete unless terminated.
    /// In such cases, this call may block indefinitely, so care must be taken to ensure appropriate timeouts are
    /// enforced using the <paramref name="cancellationToken"/> parameter.
    /// </para><para>
    /// If a workflow instance is already complete when this method is called, the method will return immediately.
    /// </para>
    /// </remarks>
    /// <inheritdoc cref="WaitForWorkflowStartAsync(string, bool, CancellationToken)"/>
    public abstract Task<WorkflowMetadata> WaitForWorkflowCompletionAsync(
        string instanceId,
        bool getInputsAndOutputs = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an event notification message to a waiting workflow instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To handle the event, the target workflow instance must be waiting for an
    /// event named <paramref name="eventName"/> using the
    /// <see cref="WorkflowContext.WaitForExternalEventAsync{T}(string, CancellationToken)"/> API.
    /// If the target workflow instance is not yet waiting for an event named <paramref name="eventName"/>,
    /// then the event will be saved in the workflow instance state and dispatched immediately when the
    /// workflow calls <see cref="WorkflowContext.WaitForExternalEventAsync{T}(string, CancellationToken)"/>.
    /// This event saving occurs even if the workflow has canceled its wait operation before the event was received.
    /// </para><para>
    /// Workflows can wait for the same event name multiple times, so sending multiple events with the same name is
    /// allowed. Each external event received by a workflow will complete just one task returned by the
    /// <see cref="WorkflowContext.WaitForExternalEventAsync{T}(string, CancellationToken)"/> method.
    /// </para><para>
    /// Raised events for a completed or non-existent workflow instance will be silently discarded.
    /// </para>
    /// </remarks>
    /// <param name="instanceId">The ID of the workflow instance that will handle the event.</param>
    /// <param name="eventName">The name of the event. Event names are case-sensitive.</param>
    /// <param name="eventPayload">The serializable (by System.Text.Json) data payload to include with the event.</param>
    /// <param name="cancellationToken">Token used to cancel enqueueing the event to the backend. This does not abort sending
    /// the event once enqueued.</param>
    /// <returns>A task that completes when the event notification message has been enqueued.</returns>
    public abstract Task RaiseEventAsync(string instanceId, string eventName, object? eventPayload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates a running workflow instance and updates its runtime status to
    /// <see cref="WorkflowRuntimeStatus.Terminated"/>.
    /// </summary>
    /// <param name="instanceId">The instance ID of the workflow to terminate.</param>
    /// <param name="output">The optional output to set for the terminated workflow instance.</param>
    /// <param name="cancellationToken">A token can that be used to cancel the termination request to the backend. Note
    /// that this does not abort the termination of the workflow once the cancellation request has been enqueued.</param>
    /// <returns>A task that completes when the termination message is enqueued.</returns>
    public abstract Task TerminateWorkflowAsync(string instanceId, object? output = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspends a workflow instance, halting processing of it until
    /// <see cref="ResumeWorkflowAsync(string, string, CancellationToken)"/> is used ot resume the workflow.
    /// </summary>
    /// <param name="instanceId">The instance ID of the workflow to suspend.</param>
    /// <param name="reason">The optional suspension reason.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the suspension operation. Note that cancelling
    /// this token does <b>not</b> resume hte workflow if the suspension is successful.</param>
    /// <returns>A task that complets when the suspension has been committed to the Dapr runtime.</returns>
    public abstract Task SuspendWorkflowAsync(string instanceId, string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a workflow instance that was suspended via <see cref="SuspendWorkflowAsync(string, string, CancellationToken)"/>.
    /// </summary>
    /// <param name="instanceId">The instance ID of the workflow to resume.</param>
    /// <param name="reason">The optional resume reason.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the resume operation. Note that canceling this
    /// token does <b>not</b> re-suspend the workflow if the resume is successful.</param>
    /// <returns>A task that completes when the resume operation has been committed to the Dapr runtime.</returns>
    public abstract Task ResumeWorkflowAsync(string instanceId, string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges workflow instance state from the workflow state store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method can be used to permanently delete workflow metadata from the underlying state store,
    /// including any stored inputs, outputs, and workflow history records. This is often useful for implementing
    /// data retention policies and for keeping storage costs minimal. Only workflow instances in the
    /// <see cref="WorkflowRuntimeStatus.Completed"/>, <see cref="WorkflowRuntimeStatus.Failed"/>, or
    /// <see cref="WorkflowRuntimeStatus.Terminated"/> state can be purged.
    /// </para>
    /// <para>
    /// Purging a workflow purges all the child workflows created by the target.
    /// </para>
    /// </remarks>
    /// <param name="instanceId">The instance ID of the workflow instance to purge.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the purge operation.</param>
    /// <returns>Returns a task that complets when the purge operation has completed. The value of this task will
    /// be <c>true</c> if the workflow state was found and purged successfully; otherwise it will return
    /// <c>false</c>.</returns>
    public abstract Task<bool> PurgeInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract ValueTask DisposeAsync();
}
