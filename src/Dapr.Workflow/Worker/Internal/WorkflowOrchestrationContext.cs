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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Serialization;
using Dapr.Workflow.Versioning;
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow.Worker.Internal;

/// <summary>
/// Internal orchestration context that processes gRPC history events.
/// </summary>
/// <remarks>
/// Here's the intended workflow execution model:
/// First execution: Workflow runs until first `await`, returns pending actions, task doesn't complete
/// Subsequent executions: History is replayed, tasks complete from history, workflow advances further
/// Completion: When no more awaitable operations exist, workflow returns the final result
/// </remarks>
internal sealed class WorkflowOrchestrationContext : WorkflowContext
{
    /// <summary>
    /// Used to track patch-based versioning semantics.
    /// </summary>
    private readonly WorkflowVersionTracker _versionTracker;
    private readonly List<HistoryEvent> _externalEventBuffer = [];
    private readonly Dictionary<string, Queue<TaskCompletionSource<HistoryEvent>>> _externalEventSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, TaskCompletionSource<HistoryEvent>> _openTasks = [];
    private readonly SortedDictionary<int, OrchestratorAction> _pendingActions = [];
    private readonly IWorkflowSerializer _workflowSerializer;
    private readonly ILogger<WorkflowOrchestrationContext> _logger;
    // Maps runtime sub-orchestration created EventId -> parent action/task id (our local task id).
    private readonly Dictionary<int, int> _subOrchestrationCreatedEventIdToParentTaskId = [];
    // Maps child instance id -> parent action/task id (our local task id).
    // Used to build the createdEventId->parentTaskId mapping when the "created" history event arrives.
    private readonly Dictionary<string, int> _subOrchestrationInstanceIdToParentTaskId = new(StringComparer.Ordinal);
    /// <summary>
    /// Buffers completion events that arrive before the corresponding task is registered in _openTasks.
    /// Key is taskedScheduledId/timerId/etc. as provided by the history event.
    /// </summary>
    private readonly Dictionary<int, HistoryEvent> _unmatchedCompletions = [];


    // Parse instance ID as GUID or generate one
    private readonly Guid _instanceGuid;

    private readonly string? _appId;
    private int _sequenceNumber;
    private int _guidCounter;
    private object? _customStatus;
    private DateTime _currentUtcDateTime;
    private bool _isReplaying;

    public WorkflowOrchestrationContext(string name, string instanceId, DateTime currentUtcDateTime,
        IWorkflowSerializer workflowSerializer, ILoggerFactory loggerFactory, WorkflowVersionTracker versionTracker, string? appId = null)
    {
        _workflowSerializer = workflowSerializer;
        _logger = loggerFactory.CreateLogger<WorkflowOrchestrationContext>() ??
                  throw new ArgumentNullException(nameof(loggerFactory));
        _instanceGuid = Guid.TryParse(instanceId, out var guid) ? guid : Guid.NewGuid();
        Name = name;
        InstanceId = instanceId;
        _currentUtcDateTime = currentUtcDateTime;
        _appId = appId; // Necessary for setting the source app ID value on the task router
        _versionTracker = versionTracker;
        
        _logger.LogWorkflowContextConstructorSetup(name, instanceId);
    }

    /// <inheritdoc />
    public override string Name { get; }

    /// <inheritdoc />
    public override string InstanceId { get; }

    /// <inheritdoc />
    public override DateTime CurrentUtcDateTime => _currentUtcDateTime;

    /// <inheritdoc />
    public override bool IsReplaying => _isReplaying;

    /// <inheritdoc />
    public override bool IsPatched(string patchName) => _versionTracker.RequestPatch(patchName, this.IsReplaying);

    /// <summary>
    /// Gets the list of pending orchestrator actions to be sent to the Dapr sidecar.
    /// </summary>
    internal IReadOnlyCollection<OrchestratorAction> PendingActions => _pendingActions.Values;

    /// <summary>
    /// Gets the custom status set by the workflow, if any.
    /// </summary>
    internal object? CustomStatus => _customStatus;

    /// <inheritdoc />
    public override async Task<T> CallActivityAsync<T>(string name, object? input = null,
        WorkflowTaskOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var taskId = _sequenceNumber++;

        var router = CreateRouter(options?.TargetAppId);
        
        // If the completion arrived before we registered the task, consume it now
        if (_unmatchedCompletions.Remove(taskId, out var earlyCompletion))
        {
            _logger.LogDebug("Found early completion in buffer for task {TaskId} ({ActivityName})", taskId, name);
            return await HandleHistoryMatch<T>(name, earlyCompletion, taskId);
        }

        _pendingActions.Add(taskId, new OrchestratorAction
        {
            Id = taskId,
            ScheduleTask = new ScheduleTaskAction
            {
                Name = name, 
                Input = _workflowSerializer.Serialize(input),
                Router = router
            },
            Router = router
        });

        var tcs = new TaskCompletionSource<HistoryEvent>();
        _openTasks.Add(taskId, tcs);
        
        var historyEvent = await tcs.Task;
        return await HandleHistoryMatch<T>(name, historyEvent, taskId);
    }

    /// <inheritdoc />
    public override async Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
    {
        var taskId = _sequenceNumber++;

        _pendingActions.Add(taskId, new OrchestratorAction
        {
            Id = taskId,
            CreateTimer = new CreateTimerAction
            {
                FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(fireAt)
            }
        });

        var tcs = new TaskCompletionSource<HistoryEvent>();
        _openTasks.Add(taskId, tcs);
        
        // If the timer fired before we registered the task, consume it now
        if (_unmatchedCompletions.Remove(taskId, out var earlyCompletion))
        {
            tcs.TrySetResult(earlyCompletion);
            _openTasks.Remove(taskId);
        }

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                if (tcs.TrySetCanceled())
                {
                    _openTasks.Remove(taskId);
                }
            });
        }

        await tcs.Task;
    }

    /// <inheritdoc />
    public override async Task<T> WaitForExternalEventAsync<T>(string eventName, CancellationToken cancellationToken = default)
    {
        if (TryTakeExternalEvent(eventName, out string? eventData))
        {
            return DeserializeResult<T>(eventData!);
        }

        // Create a task completion source that will be set when the external event arrives.
        TaskCompletionSource<HistoryEvent> eventSource = new();
        if (_externalEventSources.TryGetValue(eventName, out Queue<TaskCompletionSource<HistoryEvent>>? existing))
        {
            existing.Enqueue(eventSource);
        }
        else
        {
            Queue<TaskCompletionSource<HistoryEvent>> eventSourceQueue = new();
            eventSourceQueue.Enqueue(eventSource);
            _externalEventSources.Add(eventName, eventSourceQueue);
        }

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => eventSource.TrySetCanceled(cancellationToken));
        }

        var historyEvent = await eventSource.Task;

        eventData = historyEvent.EventRaised.Input ?? string.Empty;
        return DeserializeResult<T>(eventData);
    }

    /// <summary>
    /// Try take external event by name
    /// </summary>
    private bool TryTakeExternalEvent(string eventName, out string? eventData)
    {
        var historyEvent = _externalEventBuffer.Find(e => string.Equals(e.EventRaised.Name, eventName, StringComparison.OrdinalIgnoreCase));
        if (historyEvent is not null)
        {
            _externalEventBuffer.Remove(historyEvent);
            eventData = historyEvent.EventRaised.Input ?? string.Empty;
            return true;
        }

        eventData = null;
        return false;
    }

    /// <inheritdoc />
    public override void SendEvent(string instanceId, string eventName, object payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        var taskId = _sequenceNumber++;

        _pendingActions.Add(taskId, new OrchestratorAction
        {
            Id = taskId,
            SendEvent = new SendEventAction
            {
                Instance = new OrchestrationInstance { InstanceId = instanceId },
                Name = eventName,
                Data = _workflowSerializer.Serialize(payload)
            }
        });
    }

    /// <inheritdoc />
    public override void SetCustomStatus(object? customStatus) => _customStatus = customStatus;

    public override async Task<TResult> CallChildWorkflowAsync<TResult>(string workflowName, object? input = null,
        ChildWorkflowTaskOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);

        // CRITICAL: Child instance IDs must be deterministic across replays
        // Generate the child instance ID BEFORE incrementing sequence
        var childInstanceId = options?.InstanceId ?? NewGuid().ToString();
        var taskId = _sequenceNumber++;

        var router = CreateRouter(options?.TargetAppId);

        _pendingActions.Add(taskId, new OrchestratorAction
        {
            Id = taskId,
            CreateSubOrchestration = new CreateSubOrchestrationAction
            {
                Name = workflowName,
                InstanceId = childInstanceId,
                Input = _workflowSerializer.Serialize(input),
                Router = router
            },
            Router = router
        });
        
        // Remember which parent task is associated with this child instance id
        _subOrchestrationInstanceIdToParentTaskId[childInstanceId] = taskId;

        var tcs = new TaskCompletionSource<HistoryEvent>();
        _openTasks.Add(taskId, tcs);
        
        // If the sub-orchestration completed before we registered the task, consume it now
        if (_unmatchedCompletions.Remove(taskId, out var earlyCompletion))
        {
            tcs.TrySetResult(earlyCompletion);
            _openTasks.Remove(taskId);
        }

        var historyEvent = await tcs.Task;
        return await HandleHistoryMatch<TResult>(workflowName, historyEvent, taskId);
    }

    /// <inheritdoc />
    public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true)
    {
        var action = new OrchestratorAction
        {
            Id = _sequenceNumber++,
            CompleteOrchestration = new CompleteOrchestrationAction
            {
                OrchestrationStatus = OrchestrationStatus.ContinuedAsNew,
                Result = _workflowSerializer.Serialize(newInput),
            }
        };

        if (preserveUnprocessedEvents)
        {
            // all EventRaised events that were not consumed via WaitForExternalEventAsync
            action.CompleteOrchestration.CarryoverEvents.AddRange(_externalEventBuffer);
        }

        _pendingActions.Add(action.Id, action);
    }

    /// <inheritdoc />
    public override Guid NewGuid()
    {
        // Create deterministic Guid based on instance ID and counter
        var guidCounter = _guidCounter++;
        var name = $"{InstanceId}_{guidCounter}"; // Stable name
        return CreateGuidFromName(_instanceGuid, Encoding.UTF8.GetBytes(name));
    }

    /// <inheritdoc />
    public override ILogger CreateReplaySafeLogger(string categoryName) => new ReplaySafeLogger(_logger, () => IsReplaying);

    /// <inheritdoc />
    public override ILogger CreateReplaySafeLogger(Type type) => CreateReplaySafeLogger(type.FullName ?? type.Name);

    /// <inheritdoc />
    public override ILogger CreateReplaySafeLogger<T>() => CreateReplaySafeLogger(typeof(T));

    private Task<T> HandleHistoryMatch<T>(string name, HistoryEvent e, int taskId)
    {
        _logger.LogHandleHistoryMatch(IsReplaying ? "Replaying" : "Executing", taskId, name);

        return e switch
        {
            { TaskCompleted: { } completed } => HandleCompletedActivityFromHistory<T>(name, completed),
            { TaskFailed: { } failed } => HandleFailedActivityFromHistory<T>(name, failed),
            { SubOrchestrationInstanceCompleted: { } completed } => HandleCompletedChildWorkflowFromHistory<T>(name, completed),
            { SubOrchestrationInstanceFailed: { } failed } => HandleFailedChildWorkflowFromHistory<T>(name, failed),
            _ => throw new InvalidOperationException($"Unexpected history event type for task ID {taskId}")
        };
    }

    internal void ProcessEvents(IEnumerable<HistoryEvent> events, bool isReplaying)
    {
        _isReplaying = isReplaying;
        
        foreach (HistoryEvent historyEvent in events)
        {
            switch (historyEvent)
            {
                case { OrchestratorStarted: { } started }:
                    HandleOrchestratorStarted(historyEvent, started);
                    break;

                case { TaskScheduled: not null }:
                    HandleActionCreated(historyEvent);
                    break;

                case { TaskCompleted: { } completed }:
                    HandleActionCompleted(historyEvent, completed.TaskScheduledId);
                    break;

                case { TaskFailed: { } failed }:
                    HandleActionCompleted(historyEvent, failed.TaskScheduledId);
                    break;

                case { SubOrchestrationInstanceCreated: {} created }:
                    HandleSubOrchestrationCreated(historyEvent, created);
                    break;

                case { SubOrchestrationInstanceCompleted: { } completed }:
                    HandleActionCompleted(historyEvent, completed.TaskScheduledId);
                    break;

                case { SubOrchestrationInstanceFailed: { } failed }:
                    HandleActionCompleted(historyEvent, failed.TaskScheduledId);
                    break;

                case { TimerCreated: not null }:
                    HandleActionCreated(historyEvent);
                    break;

                case { TimerFired: { } fired }:
                    HandleActionCompleted(historyEvent, fired.TimerId);
                    break;

                case { EventSent: not null }:
                    HandleActionCreated(historyEvent);
                    break;

                case { EventRaised: { } raised }:
                    HandleEventRaisedEvent(historyEvent, raised.Name);
                    break;
            }
        }
    }

    private void HandleOrchestratorStarted(HistoryEvent historyEvent, OrchestratorStartedEvent started)
    {
        _currentUtcDateTime = historyEvent.Timestamp.ToDateTime();
        
        // Notify the tracker of the versioning data provided by the runtime for this turn
        _versionTracker.OnOrchestratorStarted(started.Version);
    }

    private void HandleActionCreated(HistoryEvent historyEvent)
    {
        _pendingActions.Remove(historyEvent.EventId);
    }

    private void HandleSubOrchestrationCreated(HistoryEvent historyEvent,
        SubOrchestrationInstanceCreatedEvent created)
    {
        // The runtime may assign an EventId that does not match our local taskId.
        // Try to correlate using the child instance id (which we control).
        var createdEventId = historyEvent.EventId;

        // SubOrchestrationInstanceCreatedEvent should carry the child instance id.
        // If it's missing/empty, we can't build a mapping.
        if (!string.IsNullOrWhiteSpace(created.InstanceId) &&
            _subOrchestrationInstanceIdToParentTaskId.TryGetValue(created.InstanceId, out var parentTaskId))
        {
            if (createdEventId != parentTaskId)
            {
                _subOrchestrationCreatedEventIdToParentTaskId[createdEventId] = parentTaskId;
            }

            // The "created" history event means the schedule action is no longer pending.
            // Remove by parentTaskId (our action id), not by createdEventId.
            _pendingActions.Remove(parentTaskId);

            // Optional: prevent unbounded growth
            _subOrchestrationInstanceIdToParentTaskId.Remove(created.InstanceId);
            return;
        }

        // Fallback to old behavior if we can't correlate
        _pendingActions.Remove(createdEventId);
    }
    
    private void HandleActionCompleted(HistoryEvent historyEvent, int taskId)
    {
        if (_openTasks.TryGetValue(taskId, out var tcs))
        {
            tcs.SetResult(historyEvent);
            _openTasks.Remove(taskId);
            return;
        }
        
        // Sub-orchestration completion may correlate via "created event id" rather than our local parent task id.
        if (_subOrchestrationCreatedEventIdToParentTaskId.TryGetValue(taskId, out var parentTaskId) &&
            _openTasks.TryGetValue(parentTaskId, out var mappedTcs))
        {
            mappedTcs.SetResult(historyEvent);
            _openTasks.Remove(parentTaskId);
            return;
        }
        
        // Buffer the completion so the next replay pass can consume it when the workflow schedules/await the task.
        // Ignore duplicates (first completion wins)
        if (!_unmatchedCompletions.TryAdd(taskId, historyEvent))
        {
            // If we get here, the runtime delivered a completion for an unknown task id.
            // This will otherwise manifest as "await never resumes".
            _logger.LogWarning(
                "Received completion for unknown taskId {TaskId} in instance {InstanceId}. OpenTasks=[{OpenTasks}] EventType={EventType}",
                taskId,
                InstanceId,
                string.Join(",", _openTasks.Keys),
                historyEvent.EventTypeCase);
        }
    }

    private void HandleEventRaisedEvent(HistoryEvent historyEvent, string eventName)
    {
        if (_externalEventSources.TryGetValue(eventName, out Queue<TaskCompletionSource<HistoryEvent>>? waiters))
        {
            var tcs = waiters.Dequeue();

            // Events are completed in FIFO order. Remove the key if the last event was delivered.
            if (waiters.Count is 0)
            {
                _externalEventSources.Remove(eventName);
            }

            tcs.TrySetResult(historyEvent);
        }
        else
        {
            // The orchestrator isn't waiting for this event (yet?). Save it in case
            // the orchestrator wants it later.
            _externalEventBuffer.Add(historyEvent);
        }
    }

    /// <summary>
    /// Creates a new TaskRouter in a centralized location.
    /// </summary>
    /// <param name="targetAppId">The app ID containing the target workflow resource.</param>
    /// <returns>Either a <see cref="TaskRouter"/> if the requisite values are provided or a <c>null</c>.</returns>
    private TaskRouter? CreateRouter(string? targetAppId)
    {
        return !string.IsNullOrWhiteSpace(targetAppId) && !string.IsNullOrWhiteSpace(_appId)
            ? new TaskRouter { TargetAppID = targetAppId, SourceAppID = _appId }
            : null;
    }

    /// <summary>
    /// Handles an activity that completed in the workflow history.
    /// </summary>
    private Task<T> HandleCompletedActivityFromHistory<T>(string activityName, TaskCompletedEvent completed)
    {
        _logger.LogActivityCompletedFromHistory(activityName, InstanceId);
        return Task.FromResult(DeserializeResult<T>(completed.Result ?? string.Empty));
    }

    /// <summary>
    /// Handles an activity that failed in the workflow history.
    /// </summary>
    private Task<T> HandleFailedActivityFromHistory<T>(string activityName, TaskFailedEvent failed)
    {
        _logger.LogActivityFailedFromHistory(activityName, InstanceId);
        throw CreateTaskFailedException(failed);
    }

    /// <summary>
    /// Handles a child workflow that completed in the workflow history.
    /// </summary>
    private Task<TResult> HandleCompletedChildWorkflowFromHistory<TResult>(string workflowName,
        SubOrchestrationInstanceCompletedEvent completed)
    {
        _logger.LogChildWorkflowCompletedFromHistory(workflowName, InstanceId);
        return Task.FromResult(DeserializeResult<TResult>(completed.Result ?? string.Empty));
    }

    /// <summary>
    /// Handles a child workflow that failed in the workflow history.
    /// </summary>
    private Task<TResult> HandleFailedChildWorkflowFromHistory<TResult>(string workflowName,
        SubOrchestrationInstanceFailedEvent failed)
    {
        _logger.LogChildWorkflowFailedFromHistory(workflowName, InstanceId);
        throw new WorkflowTaskFailedException($"Child workflow '{workflowName}' failed",
            new WorkflowTaskFailureDetails(failed.FailureDetails?.ErrorType ?? "Exception",
                failed.FailureDetails?.ErrorMessage ?? "Unknown message",
                failed.FailureDetails?.StackTrace ?? string.Empty));
    }

    /// <summary>
    /// Creates a deterministic GUID from a namespace and name using RFC 4122 UUID v5 (SHA-1).
    /// </summary>
    private static Guid CreateGuidFromName(Guid namespaceId, byte[] name)
    {
        // RFC 4122 §4.3 - Algorithm for Creating a Name-Based UUID (Version 5 - SHA-1)
        var namespaceBytes = namespaceId.ToByteArray();
        SwapByteOrder(namespaceBytes);

        byte[] hash;
        using (var sha1 = SHA1.Create())
        {
            sha1.TransformBlock(namespaceBytes, 0, namespaceBytes.Length, null, 0);
            sha1.TransformFinalBlock(name, 0, name.Length);
            hash = sha1.Hash!;
        }

        var newGuid = new byte[16];
        Array.Copy(hash, 0, newGuid, 0, 16);

        // Set version to 5 (SHA-1) and variant to RFC 4122
        newGuid[6] = (byte)((newGuid[6] & 0x0F) | 0x50);
        newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80);

        SwapByteOrder(newGuid);
        return new Guid(newGuid);
    }

    /// <summary>
    /// Swaps byte order for GUID conversion.
    /// </summary>
    private static void SwapByteOrder(byte[] guid)
    {
        SwapBytes(guid, 0, 3);
        SwapBytes(guid, 1, 2);
        SwapBytes(guid, 4, 5);
        SwapBytes(guid, 6, 7);
    }

    /// <summary>
    /// Swaps two bytes in an array.
    /// </summary>
    private static void SwapBytes(byte[] guid, int left, int right)
        => (guid[left], guid[right]) = (guid[right], guid[left]);

    /// <summary>
    /// Deserializes string value to a specific type.
    /// </summary>
    /// <param name="value">The string value to deserialize.</param>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <returns>The strongly typed deserialized data.</returns>
    private T DeserializeResult<T>(string value)
        => string.IsNullOrEmpty(value) ? default! : _workflowSerializer.Deserialize<T>(value)!;

    /// <summary>
    /// An exception that represents a task failed event.
    /// </summary>
    private static WorkflowTaskFailedException CreateTaskFailedException(TaskFailedEvent failedEvent)
    {
        var failureDetails = new WorkflowTaskFailureDetails(failedEvent.FailureDetails?.ErrorType ?? "Exception",
            failedEvent.FailureDetails?.ErrorMessage ?? "Unknown error",
            failedEvent.FailureDetails?.StackTrace ?? string.Empty);

        return new WorkflowTaskFailedException($"Task failed: {failureDetails.ErrorMessage}", failureDetails);
    }
}
