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
//  ------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common;
using Dapr.Workflow.Client;

namespace Dapr.Workflow;

/// <summary>
/// Defines methods for scheduling and managing Dapr Workflow instances.
/// </summary>
public interface IDaprWorkflowClient: IDaprClient, IAsyncDisposable
{
    /// <summary>
    /// Schedules a new workflow instance for execution.
    /// </summary>
    /// <param name="name">The name of the workflow to schedule.</param>
    /// <param name="instanceId">
    /// The unique ID for the workflow instance. If not specified, a GUID is auto-generated.
    /// </param>
    /// <param name="input">
    /// The optional serializable input, or a readable <see cref="System.IO.Stream"/> when automatic
    /// workflow-input references are enabled.
    /// </param>
    /// <remarks>
    /// <inheritdoc cref="ScheduleNewWorkflowAsync(string, string, object, DateTimeOffset?, CancellationToken)" path="/remarks/*"/>
    /// </remarks>
    /// <returns>The instance ID of the scheduled workflow.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null or empty.</exception>
    Task<string> ScheduleNewWorkflowAsync(
        string name,
        string? instanceId = null,
        object? input = null);

    /// <summary>
    /// Schedules a new workflow instance for execution at a specified time.
    /// </summary>
    /// <param name="name">The name of the workflow to schedule.</param>
    /// <param name="instanceId">
    /// The unique ID for the workflow instance. If not specified, a GUID is auto-generated.
    /// </param>
    /// <param name="input">The optional serializable input, or a readable stream when automatic workflow-input references are enabled.</param>
    /// <param name="startTime">
    /// The time when the workflow should start. If in the past or <c>null</c>, the workflow starts immediately.
    /// </param>
    /// <remarks>
    /// <inheritdoc cref="ScheduleNewWorkflowAsync(string, string, object, DateTimeOffset?, CancellationToken)" path="/remarks/*"/>
    /// </remarks>
    /// <returns>The instance ID of the scheduled workflow.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null or empty.</exception>
    Task<string> ScheduleNewWorkflowAsync(
        string name,
        string? instanceId,
        object? input,
        DateTime? startTime);

    /// <summary>
    /// Schedules a new workflow instance for execution at a specified time.
    /// </summary>
    /// <param name="name">The name of the workflow to schedule.</param>
    /// <param name="instanceId">The unique ID for the workflow instance. Auto-generated if not specified.</param>
    /// <param name="input">The optional serializable input, or a readable stream when automatic workflow-input references are enabled.</param>
    /// <param name="startTime">The time when the workflow should start. If in the past or <c>null</c>, starts immediately.</param>
    /// <param name="cancellation">Token to cancel uploading a stream input and scheduling the workflow.</param>
    /// <remarks>
    /// <para>
    /// A top-level <see cref="System.IO.Stream"/> input requires binary-store payload support with
    /// <see cref="Payloads.BinaryStorePayloadOptions.WritePreference"/> set to
    /// <see cref="Payloads.PayloadWritePreference.PreferReference"/> and
    /// <see cref="Payloads.PayloadKind.WorkflowInput"/> enabled in
    /// <see cref="Payloads.BinaryStorePayloadOptions.OffloadSites"/>.
    /// Its raw bytes are stored before scheduling and the workflow receives a
    /// <see cref="Payloads.PayloadReference"/>. Streams always become references regardless of
    /// <see cref="Payloads.BinaryStorePayloadOptions.OffloadThreshold"/>, including empty streams.
    /// Without this configuration, stream inputs are rejected before reading or scheduling.
    /// </para>
    /// <para>
    /// The stream is read from its current position and remains open; it is not rewound on success
    /// or failure. Upload uses bounded buffers and temporary disk staging proportional to the
    /// remaining content, as described by <see cref="Payloads.IWorkflowPayloadResolver.StoreStreamAsync"/>.
    /// Do not modify or dispose the stream before awaiting completion. Reopen or rewind the source
    /// before retrying. If scheduling fails after upload, the stored payload is retained.
    /// </para>
    /// <para>
    /// Use a reference-typed workflow input to avoid materialization. A typed reference is appropriate
    /// only when the bytes match the configured serializer's format. This automatic stream upload
    /// applies only to initial client scheduling, not nested stream properties or other boundaries.
    /// </para>
    /// </remarks>
    /// <returns>The instance ID of the scheduled workflow.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Stream input is supplied without automatic workflow-input references enabled.</exception>
    Task<string> ScheduleNewWorkflowAsync(
        string name,
        string? instanceId,
        object? input,
        DateTimeOffset? startTime,
        CancellationToken cancellation = default);

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
    Task<WorkflowState> GetWorkflowStateAsync(
        string instanceId,
        bool getInputsAndOutputs = true,
        CancellationToken cancellation = default);

    /// <summary>
    /// Reads the raw stored output of a successfully completed workflow incrementally, without
    /// materializing referenced payloads. The output must be a payload reference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Metadata and storage are read when enumeration begins. This method does not wait for
    /// completion; use <see cref="WaitForWorkflowCompletionAsync"/> with
    /// <c>getInputsAndOutputs: false</c> first if necessary. Each enumeration fetches metadata again
    /// and opens a new download. Breaking out, cancellation, or failure disposes the download.
    /// Transport may prefetch some data. Existing wait APIs may still hydrate referenced custom status.
    /// </para>
    /// <para>
    /// Requires a payload resolver, normally registered by <c>WithBinaryStorePayloads</c>.
    /// Both automatic and explicit references are supported regardless of the current write policy.
    /// Inline output is rejected rather than buffered and split into chunks. Use
    /// <see cref="GetWorkflowStateAsync"/> and <see cref="WorkflowState.ReadOutputAs{T}"/> for inline values,
    /// or configure the producer to persist an output reference.
    /// </para>
    /// <para>
    /// The runtime metadata RPC includes input, output, and custom status wire values. Their
    /// references are not hydrated; only the output reference is opened. Inline wire values still
    /// travel in that RPC, including inline output before it can be rejected. This method cannot
    /// provide bounded-memory retrieval of inline metadata.
    /// </para>
    /// <para>
    /// Consume or copy each chunk before advancing; its memory may be reused. Chunk boundaries
    /// and sizes are unspecified. These are raw stored bytes: no deserialization, payload-stage
    /// reversal, or automatic integrity verification is performed. Retain the stored payload
    /// while readers need it.
    /// </para>
    /// </remarks>
    /// <param name="instanceId">The workflow instance whose output should be read.</param>
    /// <param name="cancellationToken">A token used to cancel metadata retrieval and the download.</param>
    /// <returns>A lazy sequence of raw output chunks.</returns>
    /// <exception cref="ArgumentException"><paramref name="instanceId"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// Payload support is not registered, the instance does not exist or is not successfully
    /// completed, or the output is absent or inline.
    /// </exception>
    /// <exception cref="Payloads.PayloadNotFoundException">The referenced payload no longer exists.</exception>
    /// <exception cref="NotSupportedException">The reference version or client implementation is unsupported.</exception>
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReadWorkflowOutputChunksAsync(
        string instanceId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This workflow client does not support streaming output.");

    /// <summary>
    /// Reads a completed workflow's referenced output in chunks of <paramref name="chunkSize"/>
    /// bytes, except for the final chunk, which may be smaller.
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="ReadWorkflowOutputChunksAsync(string, CancellationToken)" path="/remarks/*"/>
    /// <para>
    /// The chunk size controls the reusable application buffer, not transport message sizes.
    /// Partial reads are combined to fill a chunk; larger sizes use more memory and delay delivery.
    /// </para>
    /// </remarks>
    /// <param name="instanceId">The workflow instance whose output should be read.</param>
    /// <param name="chunkSize">The positive number of bytes per full chunk.</param>
    /// <param name="cancellationToken">A token used to cancel metadata retrieval and the download.</param>
    /// <returns>A lazy sequence of chunks. An empty referenced payload produces no chunks.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chunkSize"/> is not positive.</exception>
    /// <inheritdoc cref="ReadWorkflowOutputChunksAsync(string, CancellationToken)" path="/exception"/>
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReadWorkflowOutputChunksAsync(
        string instanceId, int chunkSize, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This workflow client does not support streaming output.");

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
    Task<WorkflowState> WaitForWorkflowStartAsync(
        string instanceId,
        bool getInputsAndOutputs = true,
        CancellationToken cancellation = default);

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
    Task<WorkflowState> WaitForWorkflowCompletionAsync(
        string instanceId,
        bool getInputsAndOutputs = true,
        CancellationToken cancellation = default);

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
    Task RaiseEventAsync(
        string instanceId,
        string eventName,
        object? eventPayload = null,
        CancellationToken cancellation = default);

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
    Task TerminateWorkflowAsync(
        string instanceId,
        object? output = null,
        CancellationToken cancellation = default);

    /// <summary>
    /// Suspends a workflow instance, pausing its execution until resumed.
    /// </summary>
    /// <param name="instanceId">The unique ID of the workflow instance to suspend.</param>
    /// <param name="reason">Optional reason for the suspension.</param>
    /// <param name="cancellation">Token to cancel the suspension request.</param>
    /// <returns>A task that completes when the suspension has been committed.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="instanceId"/> is null or empty.</exception>
    Task SuspendWorkflowAsync(
        string instanceId,
        string? reason = null,
        CancellationToken cancellation = default);

    /// <summary>
    /// Resumes a previously suspended workflow instance.
    /// </summary>
    /// <param name="instanceId">The unique ID of the workflow instance to resume.</param>
    /// <param name="reason">Optional reason for the resumption.</param>
    /// <param name="cancellation">Token to cancel the resume request.</param>
    /// <returns>A task that completes when the resumption has been committed.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="instanceId"/> is null or empty.</exception>
    Task ResumeWorkflowAsync(
        string instanceId,
        string? reason = null,
        CancellationToken cancellation = default);

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
    Task<bool> PurgeInstanceAsync(
        string instanceId,
        CancellationToken cancellation = default);

    /// <summary>
    /// Lists workflow instance IDs with optional pagination.
    /// </summary>
    /// <param name="continuationToken">
    /// The continuation token from a previous call, or <c>null</c> to retrieve the first page.
    /// </param>
    /// <param name="pageSize">
    /// The maximum number of instance IDs to return per page, or <c>null</c> for no limit.
    /// </param>
    /// <param name="cancellation">Token to cancel the list operation.</param>
    /// <returns>
    /// A page containing the instance IDs and an optional continuation token for the next page.
    /// </returns>
    Task<WorkflowInstancePage> ListInstanceIdsAsync(
        string? continuationToken = null,
        int? pageSize = null,
        CancellationToken cancellation = default);

    /// <summary>
    /// Gets the full execution history of a workflow instance.
    /// </summary>
    /// <param name="instanceId">The unique ID of the workflow instance to get history for.</param>
    /// <param name="cancellation">Token to cancel the retrieval operation.</param>
    /// <returns>A read-only list of history events for the workflow instance.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="instanceId"/> is null or empty.</exception>
    Task<IReadOnlyList<WorkflowHistoryEvent>> GetInstanceHistoryAsync(
        string instanceId,
        CancellationToken cancellation = default);

    /// <summary>
    /// Reruns a workflow from a specific event ID, creating a new workflow instance that replays
    /// the source workflow's history up to the specified event.
    /// </summary>
    /// <param name="sourceInstanceId">The instance ID of the source workflow to rerun from.</param>
    /// <param name="eventId">The event ID in the source workflow's history to rerun from.</param>
    /// <param name="options">Optional configuration for the rerun operation.</param>
    /// <param name="cancellation">Token to cancel the rerun operation.</param>
    /// <returns>The instance ID of the newly created workflow instance.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="sourceInstanceId"/> is null or empty.</exception>
    Task<string> RerunWorkflowFromEventAsync(
        string sourceInstanceId,
        uint eventId,
        RerunWorkflowFromEventOptions? options = null,
        CancellationToken cancellation = default);
}
