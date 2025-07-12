// ------------------------------------------------------------------------
// Copyright 2022 The Dapr Authors
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

using Microsoft.Extensions.Logging;

namespace Dapr.Workflow;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Context object used by workflow implementations to perform actions such as scheduling activities, durable timers, waiting for
/// external events, and for getting basic information about the current workflow instance.
/// </summary>
public abstract class WorkflowContext : IWorkflowContext
{
    /// <summary>
    /// Gets the name of the current workflow.
    /// </summary>
    public abstract string Name { get; }
        
    /// <summary>
    /// Gets the instance ID of the current workflow.
    /// </summary>
    public abstract string InstanceId { get; }

    /// <summary>
    /// Gets the current workflow time in UTC.
    /// </summary>
    /// <remarks>
    /// The current workflow time is stored in the workflow history and this API will
    /// return the same value each time it is called from a particular point in the workflow's
    /// execution. It is a deterministic, replay-safe replacement for existing .NET APIs for getting
    /// the current time, such as <see cref="DateTime.UtcNow"/> and <see cref="DateTimeOffset.UtcNow"/>
    /// (which should not be used).
    /// </remarks>
    public abstract DateTime CurrentUtcDateTime { get; }

    /// <summary>
    /// Gets a value indicating whether the workflow is currently replaying a previous execution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Workflow functions are "replayed" after being unloaded from memory to reconstruct local variable state.
    /// During a replay, previously executed tasks will be completed automatically with previously seen values
    /// that are stored in the workflow history. One the workflow reaches the point in the workflow logic
    /// where it's no longer replaying existing history, the <see cref="IsReplaying"/> property will return <c>false</c>.
    /// </para><para>
    /// You can use this property if you have logic that needs to run only when <em>not</em> replaying. For example,
    /// certain types of application logging may become too noisy when duplicated as part of replay. The
    /// application code could check to see whether the function is being replayed and then issue the log statements
    /// when this value is <c>false</c>.
    /// </para>
    /// </remarks>
    /// <value>
    /// <c>true</c> if the workflow is currently replaying a previous execution; otherwise <c>false</c>.
    /// </value>
    public abstract bool IsReplaying { get; }

    /// <summary>
    /// Asynchronously invokes an activity by name and with the specified input value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Activities are the basic unit of work in a workflow. Unlike workflows, which are not
    /// allowed to do any I/O or call non-deterministic APIs, activities have no implementation restrictions.
    /// </para><para>
    /// An activity may execute in the local machine or a remote machine. The exact behavior depends on the underlying
    /// workflow engine, which is responsible for distributing tasks across machines. In general, you should never make
    /// any assumptions about where an activity will run. You should also assume at-least-once execution guarantees for
    /// activities, meaning that an activity may be executed twice if, for example, there is a process failure before
    /// the activities result is saved into storage.
    /// </para><para>
    /// Both the inputs and outputs of activities are serialized and stored in durable storage. It's highly recommended
    /// to not include any sensitive data in activity inputs or outputs. It's also recommended to not use large payloads
    /// for activity inputs and outputs, which can result in expensive serialization and network utilization. For data
    /// that cannot be cheaply or safely persisted to storage, it's recommended to instead pass <em>references</em>
    /// (for example, a URL to a storage blob/bucket) to the data and have activities fetch the data directly as part of their
    /// implementation.
    /// </para>
    /// </remarks>
    /// <param name="name">The name of the activity to call.</param>
    /// <param name="input">The serializable input to pass to the activity.</param>
    /// <param name="options">Additional options that control the execution and processing of the activity.</param>
    /// <returns>A task that completes when the activity completes or fails.</returns>
    /// <exception cref="ArgumentException">The specified activity does not exist.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the calling thread is not the workflow dispatch thread.
    /// </exception>
    /// <exception cref="WorkflowTaskFailedException">
    /// The activity failed with an unhandled exception. The details of the failure can be found in the
    /// <see cref="WorkflowTaskFailedException.FailureDetails"/> property.
    /// </exception>
    public virtual Task CallActivityAsync(string name, object? input = null, WorkflowTaskOptions? options = null)
    {
        return this.CallActivityAsync<object>(name, input, options);
    }

    /// <returns>
    /// A task that completes when the activity completes or fails. The result of the task is the activity's return value.
    /// </returns>
    /// <inheritdoc cref="CallActivityAsync"/>
    public abstract Task<T> CallActivityAsync<T>(string name, object? input = null, WorkflowTaskOptions? options = null);

    /// <summary>
    /// Creates a durable timer that expires after the specified delay.
    /// </summary>
    /// <param name="delay">The amount of time before the timer should expire.</param>
    /// <param name="cancellationToken">Used to cancel the durable timer.</param>
    /// <returns>A task that completes when the durable timer expires.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the calling thread is not the workflow dispatch thread.
    /// </exception>
    public virtual Task CreateTimer(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        return this.CreateTimer(this.CurrentUtcDateTime.Add(delay), cancellationToken);
    }

    /// <summary>
    /// Creates a durable timer that expires at a set date and time.
    /// </summary>
    /// <param name="fireAt">The time at which the timer should expire.</param>
    /// <param name="cancellationToken">Used to cancel the durable timer.</param>
    /// <inheritdoc cref="CreateTimer(TimeSpan, CancellationToken)"/>
    public abstract Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken);

    /// <summary>
    /// Waits for an event to be raised with name <paramref name="eventName"/> and returns the event data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// External clients can raise events to a waiting workflow instance. Similarly, workflows can raise
    /// events to other workflows using the <see cref="SendEvent"/> method.
    /// </para><para>
    /// If the current workflow instance is not yet waiting for an event named <paramref name="eventName"/>,
    /// then the event will be saved in the workflow instance state and dispatched immediately when this method is
    /// called. This event saving occurs even if the current workflow cancels the wait operation before the event is
    /// received.
    /// </para><para>
    /// Workflows can wait for the same event name multiple times, so waiting for multiple events with the same name
    /// is allowed. Each external event received by a workflow will complete just one task returned by this method.
    /// </para>
    /// </remarks>
    /// <param name="eventName">
    /// The name of the event to wait for. Event names are case-insensitive. External event names can be reused any
    /// number of times; they are not required to be unique.
    /// </param>
    /// <param name="cancellationToken">A <c>CancellationToken</c> to use to abort waiting for the event.</param>
    /// <typeparam name="T">Any serializable type that represents the event payload.</typeparam>
    /// <returns>
    /// A task that completes when the external event is received. The value of the task is the deserialized event payload.
    /// </returns>
    /// <exception cref="TaskCanceledException">
    /// Thrown if <paramref name="cancellationToken"/> is cancelled before the external event is received.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the calling thread is not the workflow dispatch thread.
    /// </exception>
    public abstract Task<T> WaitForExternalEventAsync<T>(string eventName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for an event to be raised with name <paramref name="eventName"/> and returns the event data.
    /// </summary>
    /// <param name="eventName">
    /// The name of the event to wait for. Event names are case-insensitive. External event names can be reused any
    /// number of times; they are not required to be unique.
    /// </param>
    /// <param name="timeout">The amount of time to wait before cancelling the external event task.</param>
    /// <exception cref="TaskCanceledException">
    /// Thrown if <paramref name="timeout"/> elapses before the external event is received.
    /// </exception>
    /// <inheritdoc cref="WaitForExternalEventAsync{T}(string, CancellationToken)"/>
    public abstract Task<T> WaitForExternalEventAsync<T>(string eventName, TimeSpan timeout);

    /// <summary>
    /// Raises an external event for the specified workflow instance.
    /// </summary>
    /// <remarks>
    /// <para>The target workflow can handle the sent event using the
    /// <see cref="WaitForExternalEventAsync{T}(string, CancellationToken)"/> method.
    /// </para><para>
    /// If the target workflow doesn't exist, the event will be silently dropped.
    /// </para>
    /// </remarks>
    /// <param name="instanceId">The ID of the workflow instance to send the event to.</param>
    /// <param name="eventName">The name of the event to wait for. Event names are case-insensitive.</param>
    /// <param name="payload">The serializable payload of the external event.</param>
    public abstract void SendEvent(string instanceId, string eventName, object payload);

    /// <summary>
    /// Assigns a custom status value to the current workflow.
    /// </summary>
    /// <remarks>
    /// The <paramref name="customStatus"/> value is serialized and stored in workflow state and will
    /// be made available to the workflow status query APIs.
    /// </remarks>
    /// <param name="customStatus">
    /// A serializable value to assign as the custom status value or <c>null</c> to clear the custom status.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the calling thread is not the workflow dispatch thread.
    /// </exception>
    public abstract void SetCustomStatus(object? customStatus);

    /// <summary>
    /// Executes the specified workflow as a child workflow and returns the result.
    /// </summary>
    /// <typeparam name="TResult">
    /// The type into which to deserialize the child workflow's output.
    /// </typeparam>
    /// <inheritdoc cref="CallChildWorkflowAsync(string, object?, ChildWorkflowTaskOptions?)"/>
    public abstract Task<TResult> CallChildWorkflowAsync<TResult>(
        string workflowName,
        object? input = null,
        ChildWorkflowTaskOptions? options = null);

    /// <summary>
    /// Executes the specified workflow as a child workflow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In addition to activities, workflows can schedule other workflows as <i>child workflows</i>.
    /// A child workflow has its own instance ID, history, and status that is independent of the parent workflow
    /// that started it. You can use <see cref="ChildWorkflowTaskOptions.InstanceId" /> to specify an instance ID
    /// for the child workflow. Otherwise, the instance ID will be randomly generated.
    /// </para><para>
    /// Child workflows have many benefits:
    /// <list type="bullet">
    ///  <item>You can split large workflows into a series of smaller child workflows, making your code more maintainable.</item>
    ///  <item>You can distribute workflow logic across multiple compute nodes concurrently, which is useful if
    ///  your workflow logic otherwise needs to coordinate a lot of tasks.</item>
    ///  <item>You can reduce memory usage and CPU overhead by keeping the history of parent workflow smaller.</item>
    /// </list>
    /// </para><para>
    /// The return value of a child workflow is its output. If a child workflow fails with an exception, then that
    /// exception will be surfaced to the parent workflow, just like it is when an activity task fails with an
    /// exception. Child workflows also support automatic retry policies.
    /// </para><para>
    /// Terminating a parent workflow terminates all the child workflows created by the workflow instance. See the documentation at
    /// https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-features-concepts/#child-workflows regarding
    /// the terminate workflow API for more information.
    /// </para>
    /// </remarks>
    /// <param name="workflowName">The name of the workflow to call.</param>
    /// <param name="input">The serializable input to pass to the child workflow.</param>
    /// <param name="options">
    /// Additional options that control the execution and processing of the child workflow.
    /// </param>
    /// <returns>A task that completes when the child workflow completes or fails.</returns>
    /// <exception cref="ArgumentException">The specified workflow does not exist.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the calling thread is not the workflow dispatch thread.
    /// </exception>
    /// <exception cref="WorkflowTaskFailedException">
    /// The child workflow failed with an unhandled exception. The details of the failure can be found in the
    /// <see cref="WorkflowTaskFailedException.FailureDetails"/> property.
    /// </exception>
    public virtual Task CallChildWorkflowAsync(
        string workflowName,
        object? input = null,
        ChildWorkflowTaskOptions? options = null)
    {
        return this.CallChildWorkflowAsync<object>(workflowName, input, options);
    }
        
    /// <summary>
    /// Returns an instance of <see cref="ILogger"/> that is replay-safe, meaning that the logger only
    /// writes logs when the orchestrator is not replaying previous history.
    /// </summary>
    /// <param name="categoryName">The logger's category name.</param>
    /// <returns>An instance of <see cref="ILogger"/> that is replay-safe.</returns>
    public abstract ILogger CreateReplaySafeLogger(string categoryName);

    /// <inheritdoc cref="CreateReplaySafeLogger(string)" />
    /// <param name="type">The type to derive the category name from.</param>
    public abstract ILogger CreateReplaySafeLogger(Type type);

    /// <inheritdoc cref="CreateReplaySafeLogger(string)" />
    /// <typeparam name="T">The type to derive category name from.</typeparam>
    public abstract ILogger CreateReplaySafeLogger<T>();

    /// <summary>
    /// Restarts the workflow with a new input and clears its history.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is primarily designed for eternal workflows, which are workflows that
    /// may not ever complete. It works by restarting the workflow, providing it with a new input,
    /// and truncating the existing workflow history. It allows the workflow to continue
    /// running indefinitely without having its history grow unbounded. The benefits of periodically
    /// truncating history include decreased memory usage, decreased storage volumes, and shorter workflow
    /// replays when rebuilding state.
    /// </para><para>
    /// The results of any incomplete tasks will be discarded when a workflow calls
    /// <see cref="ContinueAsNew"/>. For example, if a timer is scheduled and then <see cref="ContinueAsNew"/>
    /// is called before the timer fires, the timer event will be discarded. The only exception to this
    /// is external events. By default, if an external event is received by an workflow but not yet
    /// processed, the event is saved in the workflow state unit it is received by a call to
    /// <see cref="WaitForExternalEventAsync{T}(string, CancellationToken)"/>. These events will continue to remain in memory
    /// even after an workflow restarts using <see cref="ContinueAsNew"/>. You can disable this behavior and
    /// remove any saved external events by specifying <c>false</c> for the <paramref name="preserveUnprocessedEvents"/>
    /// parameter value.
    /// </para><para>
    /// Workflow implementations should complete immediately after calling the <see cref="ContinueAsNew"/> method.
    /// </para>
    /// </remarks>
    /// <param name="newInput">The JSON-serializable input data to re-initialize the instance with.</param>
    /// <param name="preserveUnprocessedEvents">
    /// If set to <c>true</c>, re-adds any unprocessed external events into the new execution
    /// history when the workflow instance restarts. If <c>false</c>, any unprocessed
    /// external events will be discarded when the workflow instance restarts.
    /// </param>
    public abstract void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true);

    /// <summary>
    /// Creates a new GUID that is safe for replay within a workflow.
    /// </summary>
    /// <remarks>
    /// The default implementation of this method creates a name-based UUID V5 using the algorithm from RFC 4122 §4.3.
    /// The name input used to generate this value is a combination of the workflow instance ID, the current time,
    /// and an internally managed sequence number.
    /// </remarks>
    /// <returns>The new <see cref="Guid"/> value.</returns>
    public abstract Guid NewGuid();
}