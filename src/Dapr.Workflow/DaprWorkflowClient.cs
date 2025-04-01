// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace Dapr.Workflow;

/// <summary>
/// Defines client operations for managing Dapr Workflow instances.
/// </summary>
/// <remarks>
/// This is an alternative to the general purpose Dapr client. It uses a gRPC connection to send
/// commands directly to the workflow engine, bypassing the Dapr API layer.
/// </remarks>
/// <param name="innerClient">The Durable Task client used to communicate with the Dapr sidecar.</param>
/// <exception cref="ArgumentNullException">Thrown if <paramref name="innerClient"/> is <c>null</c>.</exception>
public class DaprWorkflowClient(DurableTaskClient innerClient) : IAsyncDisposable
{
    readonly DurableTaskClient innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));

    /// <summary>
    /// Schedules a new workflow instance for execution.
    /// </summary>
    /// <param name="name">The name of the workflow to schedule.</param>
    /// <param name="instanceId">
    /// The unique ID of the workflow instance to schedule. If not specified, a new GUID value is used.
    /// </param>
    /// <param name="startTime">
    /// The time when the workflow instance should start executing. If not specified or if a date-time in the past
    /// is specified, the workflow instance will be scheduled immediately.
    /// </param>
    /// <param name="input">
    /// The optional input to pass to the scheduled workflow instance. This must be a serializable value.
    /// </param>
    public Task<string> ScheduleNewWorkflowAsync(
        string name,
        string? instanceId = null,
        object? input = null,
        DateTime? startTime = null)
    {
        StartOrchestrationOptions options = new(instanceId, startTime);
        return this.innerClient.ScheduleNewOrchestrationInstanceAsync(name, input, options);
    }

    /// <summary>
    /// Fetches runtime state for the specified workflow instance.
    /// </summary>
    /// <param name="instanceId">The unique ID of the workflow instance to fetch.</param>
    /// <param name="getInputsAndOutputs">
    /// Specify <c>true</c> to fetch the workflow instance's inputs, outputs, and custom status, or <c>false</c> to
    /// omit them. Defaults to true.
    /// </param>
    public async Task<WorkflowState> GetWorkflowStateAsync(string instanceId, bool getInputsAndOutputs = true)
    {
        OrchestrationMetadata? metadata = await this.innerClient.GetInstancesAsync(
            instanceId,
            getInputsAndOutputs);
        return new WorkflowState(metadata);
    }

    /// <summary>
    /// Waits for a workflow to start running and returns a <see cref="WorkflowState"/> object that contains metadata
    /// about the started workflow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A "started" workflow instance is any instance not in the <see cref="WorkflowRuntimeStatus.Pending"/> state.
    /// </para><para>
    /// This method will return a completed task if the workflow has already started running or has already completed.
    /// </para>
    /// </remarks>
    /// <param name="instanceId">The unique ID of the workflow instance to wait for.</param>
    /// <param name="getInputsAndOutputs">
    /// Specify <c>true</c> to fetch the workflow instance's inputs, outputs, and custom status, or <c>false</c> to
    /// omit them. Setting this value to <c>false</c> can help minimize the network bandwidth, serialization, and memory costs
    /// associated with fetching the instance metadata.
    /// </param>
    /// <param name="cancellation">A <see cref="CancellationToken"/> that can be used to cancel the wait operation.</param>
    /// <returns>
    /// Returns a <see cref="WorkflowState"/> record that describes the workflow instance and its execution
    /// status. If the specified workflow isn't found, the <see cref="WorkflowState.Exists"/> value will be <c>false</c>.
    /// </returns>
    public async Task<WorkflowState> WaitForWorkflowStartAsync(
        string instanceId,
        bool getInputsAndOutputs = true,
        CancellationToken cancellation = default)
    {
        OrchestrationMetadata metadata = await this.innerClient.WaitForInstanceStartAsync(
            instanceId,
            getInputsAndOutputs,
            cancellation);
        return new WorkflowState(metadata);
    }

    /// <summary>
    /// Waits for a workflow to complete and returns a <see cref="WorkflowState"/>
    /// object that contains metadata about the started instance.
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
    /// enforced using the <paramref name="cancellation"/> parameter.
    /// </para><para>
    /// If a workflow instance is already complete when this method is called, the method will return immediately.
    /// </para>
    /// </remarks>
    /// <inheritdoc cref="WaitForWorkflowStartAsync(string, bool, CancellationToken)"/>
    public async Task<WorkflowState> WaitForWorkflowCompletionAsync(
        string instanceId,
        bool getInputsAndOutputs = true,
        CancellationToken cancellation = default)
    {
        OrchestrationMetadata metadata = await this.innerClient.WaitForInstanceCompletionAsync(
            instanceId,
            getInputsAndOutputs,
            cancellation);
        return new WorkflowState(metadata);
    }

    /// <summary>
    /// Terminates a running workflow instance and updates its runtime status to
    /// <see cref="WorkflowRuntimeStatus.Terminated"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method internally enqueues a "terminate" message in the task hub. When the task hub worker processes
    /// this message, it will update the runtime status of the target instance to
    /// <see cref="WorkflowRuntimeStatus.Terminated"/>. You can use the
    /// <see cref="WaitForWorkflowCompletionAsync(string, bool, CancellationToken)"/> to wait for the instance to reach
    /// the terminated state.
    /// </para>
    /// <para>
    /// Terminating a workflow terminates all of the child workflow instances that were created by the target. But it
    /// has no effect on any in-flight activity function executions
    /// that were started by the terminated instance. Those actions will continue to run
    /// without interruption. However, their results will be discarded.
    /// </para><para>
    /// At the time of writing, there is no way to terminate an in-flight activity execution.
    /// </para>
    /// </remarks>
    /// <param name="instanceId">The ID of the workflow instance to terminate.</param>
    /// <param name="output">The optional output to set for the terminated workflow instance.</param>
    /// <param name="cancellation">
    /// The cancellation token. This only cancels enqueueing the termination request to the backend. Does not abort
    /// termination of the workflow once enqueued.
    /// </param>
    /// <returns>A task that completes when the terminate message is enqueued.</returns>
    public Task TerminateWorkflowAsync(
        string instanceId,
        string? output = null,
        CancellationToken cancellation = default)
    {
        TerminateInstanceOptions options = new TerminateInstanceOptions {
            Output = output,
            Recursive = true,
        };
        return this.innerClient.TerminateInstanceAsync(instanceId, options, cancellation);
    }

    /// <summary>
    /// Sends an event notification message to a waiting workflow instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In order to handle the event, the target workflow instance must be waiting for an
    /// event named <paramref name="eventName"/> using the
    /// <see cref="WorkflowContext.WaitForExternalEventAsync{T}(string, CancellationToken)"/> API.
    /// If the target workflow instance is not yet waiting for an event named <paramref name="eventName"/>,
    /// then the event will be saved in the workflow instance state and dispatched immediately when the
    /// workflow calls <see cref="WorkflowContext.WaitForExternalEventAsync{T}(string, CancellationToken)"/>.
    /// This event saving occurs even if the workflow has canceled its wait operation before the event was received.
    /// </para><para>
    /// Workflows can wait for the same event name multiple times, so sending multiple events with the same name is
    /// allowed. Each external event received by an workflow will complete just one task returned by the
    /// <see cref="WorkflowContext.WaitForExternalEventAsync{T}(string, CancellationToken)"/> method.
    /// </para><para>
    /// Raised events for a completed or non-existent workflow instance will be silently discarded.
    /// </para>
    /// </remarks>
    /// <param name="instanceId">The ID of the workflow instance that will handle the event.</param>
    /// <param name="eventName">The name of the event. Event names are case-insensitive.</param>
    /// <param name="eventPayload">The serializable data payload to include with the event.</param>
    /// <param name="cancellation">
    /// The cancellation token. This only cancels enqueueing the event to the backend. Does not abort sending the event
    /// once enqueued.
    /// </param>
    /// <returns>A task that completes when the event notification message has been enqueued.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="instanceId"/> or <paramref name="eventName"/> is null or empty.
    /// </exception>
    public Task RaiseEventAsync(
        string instanceId,
        string eventName,
        object? eventPayload = null,
        CancellationToken cancellation = default)
    {
        return this.innerClient.RaiseEventAsync(instanceId, eventName, eventPayload, cancellation);
    }

    /// <summary>
    /// Suspends a workflow instance, halting processing of it until
    /// <see cref="ResumeWorkflowAsync(string, string, CancellationToken)" /> is used to resume the workflow.
    /// </summary>
    /// <param name="instanceId">The instance ID of the workflow to suspend.</param>
    /// <param name="reason">The optional suspension reason.</param>
    /// <param name="cancellation">
    /// A <see cref="CancellationToken"/> that can be used to cancel the suspend operation. Note, cancelling this token
    /// does <b>not</b> resume the workflow if suspend was successful.
    /// </param>
    /// <returns>A task that completes when the suspend has been committed to the backend.</returns>
    public Task SuspendWorkflowAsync(
        string instanceId,
        string? reason = null,
        CancellationToken cancellation = default)
    {
        return this.innerClient.SuspendInstanceAsync(instanceId, reason, cancellation);
    }

    /// <summary>
    /// Resumes a workflow instance that was suspended via <see cref="SuspendWorkflowAsync(string, string, CancellationToken)" />.
    /// </summary>
    /// <param name="instanceId">The instance ID of the workflow to resume.</param>
    /// <param name="reason">The optional resume reason.</param>
    /// <param name="cancellation">
    /// A <see cref="CancellationToken"/> that can be used to cancel the resume operation. Note, cancelling this token
    /// does <b>not</b> re-suspend the workflow if resume was successful.
    /// </param>
    /// <returns>A task that completes when the resume has been committed to the backend.</returns>
    public Task ResumeWorkflowAsync(
        string instanceId,
        string? reason = null,
        CancellationToken cancellation = default)
    {
        return this.innerClient.ResumeInstanceAsync(instanceId, reason, cancellation);
    }

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
    /// Purging a workflow purges all of the child workflows that were created by the target.
    /// </para>
    /// </remarks>
    /// <param name="instanceId">The unique ID of the workflow instance to purge.</param>
    /// <param name="cancellation">
    /// A <see cref="CancellationToken"/> that can be used to cancel the purge operation.
    /// </param>
    /// <returns>
    /// Returns a task that completes when the purge operation has completed. The value of this task will be
    /// <c>true</c> if the workflow state was found and purged successfully; <c>false</c> otherwise.
    /// </returns>
    public async Task<bool> PurgeInstanceAsync(string instanceId, CancellationToken cancellation = default)
    {
        PurgeInstanceOptions options = new PurgeInstanceOptions {Recursive = true};
        PurgeResult result = await this.innerClient.PurgeInstanceAsync(instanceId, options, cancellation);
        return result.PurgedInstanceCount > 0;
    }

    /// <summary>
    /// Disposes any unmanaged resources associated with this client.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        return ((IAsyncDisposable)this.innerClient).DisposeAsync();
    }
}
