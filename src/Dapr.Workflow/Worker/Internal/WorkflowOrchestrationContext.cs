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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Serialization;
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow.Worker.Internal;

/// <summary>
/// Internal orchestration context that processes gRPC history events.
/// </summary>
/// <remarks>
/// Here's the intended workflow execution model:
/// First execution: Workflow runs until first `await`, returns pending actions, task doesn't complete
/// Subsequent executions: History is replayed, tasks complete from history, workflow advances further
/// Completion: When no more awaitable operations exist, workflow returns final result
/// </remarks>
internal sealed class WorkflowOrchestrationContext : WorkflowContext
{
    private readonly List<HistoryEvent> _pastEvents;
    private readonly List<HistoryEvent> _newEvents;
    private readonly List<OrchestratorAction> _pendingActions = [];
    private readonly ILogger<WorkflowOrchestrationContext> _logger;
    
    // Index of events that have already been persisted to the DB
    private readonly Dictionary<int, HistoryEvent> _pastEventMap = new();
    // Index of events that just arrived in this work item
    private readonly Dictionary<int, HistoryEvent> _newEventMap = new();
    // Tracks which external events have been consumed by the workflow code
    private readonly HashSet<string> _consumedExternalEvents = new(StringComparer.OrdinalIgnoreCase);
    // Parse instance ID as GUID or generate one
    private readonly Guid _instanceGuid;
    // IDs of tasks that have been scheduled but may not have completed yet
    private readonly HashSet<int> _scheduledEventIds = [];

    private int _sequenceNumber;
    private int _guidCounter;
    private object? _customStatus;
    private readonly IWorkflowSerializer workflowSerializer;

    public WorkflowOrchestrationContext(string name, string instanceId, IEnumerable<HistoryEvent> pastEvents,
        IEnumerable<HistoryEvent> newEvents, DateTime currentUtcDateTime, IWorkflowSerializer workflowSerializer,
        ILoggerFactory loggerFactory)
    {
        this.workflowSerializer = workflowSerializer;
        _logger = loggerFactory.CreateLogger<WorkflowOrchestrationContext>() ??
                  throw new ArgumentNullException(nameof(loggerFactory));
        _instanceGuid = Guid.TryParse(instanceId, out var guid) ? guid : Guid.NewGuid();
        Name = name;
        InstanceId = instanceId;
        CurrentUtcDateTime = currentUtcDateTime;

        _pastEvents = pastEvents.ToList();
        _newEvents = newEvents.ToList();


        // 1. Index PAST events
        foreach (var e in _pastEvents)
        {
            if (TryGetTaskScheduledId(e, out int scheduledId))
            {
                _pastEventMap[scheduledId] = e;
            }

            // Track scheduled/created events to detect "Pending" state
            if (e.TaskScheduled != null) _scheduledEventIds.Add(e.EventId);
            if (e.SubOrchestrationInstanceCreated != null) _scheduledEventIds.Add(e.EventId);
            if (e.TimerCreated != null) _scheduledEventIds.Add(e.EventId);
        }

        // 2. Index NEW events
        foreach (var e in _newEvents)
        {
            if (TryGetTaskScheduledId(e, out int scheduledId))
            {
                _newEventMap[scheduledId] = e;
            }

            // Track scheduled/created events to detect "Pending" state
            if (e.TaskScheduled != null) _scheduledEventIds.Add(e.EventId);
            if (e.SubOrchestrationInstanceCreated != null) _scheduledEventIds.Add(e.EventId);
            if (e.TimerCreated != null) _scheduledEventIds.Add(e.EventId);
        }
        
        _logger.LogWorkflowContextConstructorSetup(name, instanceId, _pastEvents.Count, _newEvents.Count, _pastEventMap.Count, _newEventMap.Count);
    }

    /// <inheritdoc />
    public override string Name { get; }
    /// <inheritdoc />
    public override string InstanceId { get; }
    /// <inheritdoc />
    public override DateTime CurrentUtcDateTime { get; }

    /// <inheritdoc />
    public override bool IsReplaying => _pastEventMap.ContainsKey(_sequenceNumber);

    /// <summary>
    /// Gets the list of pending orchestrator actions to be sent to the Dapr sidecar.
    /// </summary>
    internal IReadOnlyList<OrchestratorAction> PendingActions => _pendingActions;
    /// <summary>
    /// Gets the custom status set by the workflow, if any.
    /// </summary>
    internal object? CustomStatus => _customStatus;

    /// <inheritdoc />
    public override Task<T> CallActivityAsync<T>(string name, object? input = null,
        WorkflowTaskOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var taskId = _sequenceNumber++;
        
        // Check Past Events (true replay)
        if (_pastEventMap.TryGetValue(taskId, out var historyEvent))
        {
            _logger.LogCallActivityPastHistoryMatch(taskId);
            return HandleHistoryMatch<T>(name, historyEvent, taskId, isReplay: true);
        }
        
        // Check new events (advancing execution)
        if (_newEventMap.TryGetValue(taskId, out historyEvent))
        {
            _logger.LogCallActivityNewHistoryMatch(taskId);
            return HandleHistoryMatch<T>(name, historyEvent, taskId, isReplay: false);
        }

        // Check if already scheduled (Pending)
        if (_scheduledEventIds.Contains(taskId))
        {
            _logger.LogCallActivityPendingMatch(taskId, name);
            return new TaskCompletionSource<T>().Task;
        }
        
        // Not in history - schedule new activity execution
        _logger.LogSchedulingActivity(name, InstanceId, taskId);
        
        _pendingActions.Add(new OrchestratorAction
        {
            Id = taskId,
            ScheduleTask = new ScheduleTaskAction { Name = name, Input = workflowSerializer.Serialize(input) },
            Router = !string.IsNullOrEmpty(options?.AppId) ? new TaskRouter { TargetAppID = options.AppId } : null
        });

        // Return a task that will never complete on this execution. It will only complete on
        // a future replay when the result is in history
        return new TaskCompletionSource<T>().Task;
    }

    /// <inheritdoc />
    public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
    {
        var taskId = _sequenceNumber++;
        
        // Check history for timer completion in both maps
        if (_pastEventMap.TryGetValue(taskId, out var historyEvent) ||
            _newEventMap.TryGetValue(taskId, out historyEvent))
        {
            if (historyEvent.TimerFired is not null)
            {
                _logger.LogCreateTimerMatch(taskId);
                return Task.CompletedTask;
            }
        }

        // Check if already scheduled (Pending)
        if (_scheduledEventIds.Contains(taskId))
        {
            _logger.LogCreateTimerPending(taskId);
            return new TaskCompletionSource<object?>().Task;
        }
        
        // Schedule new timer
        _logger.LogSchedulingTimer(fireAt, InstanceId);
        
        _pendingActions.Add(new OrchestratorAction
        {
            Id = taskId,
            CreateTimer = new CreateTimerAction
            {
                FireAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(fireAt)
            }
        });

        return new TaskCompletionSource<object?>().Task;
    }

    /// <inheritdoc />
    public override Task<T> WaitForExternalEventAsync<T>(string eventName, CancellationToken cancellationToken = default)
    {
        // IMPORTANT: External events are matched by name in Dapr, NOT by sequence.
        // Do NOT increment _sequenceNumber here. Doing so will misalign all subsequent tasks.
        var historyEvent = _pastEvents.Concat(_newEvents)
            .FirstOrDefault(e => e.EventRaised is { } er && string.Equals(er.Name, eventName, StringComparison.OrdinalIgnoreCase));

        if (historyEvent != null)
        {
            _consumedExternalEvents.Add(eventName);
            var eventData = historyEvent.EventRaised.Input ?? string.Empty;
            return Task.FromResult(DeserializeResult<T>(eventData));
        }

        // Event not in history yet
        var tcs = new TaskCompletionSource<T>();

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        }

        return tcs.Task;
    }

    /// <inheritdoc />
    public override async Task<T> WaitForExternalEventAsync<T>(string eventName, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return await WaitForExternalEventAsync<T>(eventName, cts.Token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void SendEvent(string instanceId, string eventName, object payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        
        _pendingActions.Add(new OrchestratorAction
        {
            Id = _sequenceNumber++,
            SendEvent = new SendEventAction
            {
                Instance = new OrchestrationInstance{InstanceId = instanceId},
                Name = eventName,
                Data = workflowSerializer.Serialize(payload)
            }
        });
    }

    /// <inheritdoc />
    public override void SetCustomStatus(object? customStatus) => _customStatus = customStatus;

    public override Task<TResult> CallChildWorkflowAsync<TResult>(string workflowName, object? input = null,
        ChildWorkflowTaskOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);

        // CRITICAL: Child instance IDs must be deterministic across replays
        // Generate the child instance ID BEFORE incrementing sequence
        var childInstanceId = options?.InstanceId ?? NewGuid().ToString();
        var taskId = _sequenceNumber++;

        // Try standard TaskScheduledId-based matching first (fast path)
        if (_pastEventMap.TryGetValue(taskId, out var historyEvent))
        {
            _logger.LogCallChildWorkflowPastHistoryMatch(taskId);
            return HandleHistoryMatch<TResult>(workflowName, historyEvent, taskId, isReplay: true);
        }

        if (_newEventMap.TryGetValue(taskId, out historyEvent))
        {
            _logger.LogCallChildWorkflowNewHistoryMatch(taskId);
            return HandleHistoryMatch<TResult>(workflowName, historyEvent, taskId, isReplay: false);
        }

        // Check if already scheduled (Pending) via deterministic TaskID
        if (_scheduledEventIds.Contains(taskId))
        {
            _logger.LogCallChildWorkflowPendingMatch(taskId, workflowName);
            return new TaskCompletionSource<TResult>().Task;
        }

        // FALLBACK: If TaskScheduledId doesn't match, search by child instance ID
        // This handles cases where Dapr returns events with mismatched TaskScheduledId values
        var completionByInstanceId = _newEvents
            .Concat(_pastEvents)
            .FirstOrDefault(e =>
            {
                // Check if this is a SubOrchestrationInstanceCreated event with matching instance ID
                if (e.SubOrchestrationInstanceCreated != null)
                {
                    return string.Equals(e.SubOrchestrationInstanceCreated.InstanceId, childInstanceId,
                        StringComparison.OrdinalIgnoreCase);
                }

                return false;
            });

        if (completionByInstanceId != null)
        {
            // We found the CREATION event. The task is at least running.
            var createdTaskId = completionByInstanceId.EventId;

            // Try to find the completion using the ID we found in the creation event
            var completion = _newEvents.Concat(_pastEvents)
                .FirstOrDefault(e =>
                    (e.SubOrchestrationInstanceCompleted != null &&
                     e.SubOrchestrationInstanceCompleted.TaskScheduledId == createdTaskId) ||
                    (e.SubOrchestrationInstanceFailed != null &&
                     e.SubOrchestrationInstanceFailed.TaskScheduledId == createdTaskId));

            if (completion != null)
            {
                _logger.LogCallChildWorkflowFoundCompletion(taskId, childInstanceId);
                return HandleHistoryMatch<TResult>(workflowName, completion, taskId, isReplay: false);
            }

            // Found Creation but NO Completion -> Task is PENDING
            _logger.LogCallChildWorkflowFoundRunning(taskId);
            return new TaskCompletionSource<TResult>().Task;
        }

        _logger.LogCallChildWorkflowSchedulingNew(workflowName, taskId, childInstanceId);
        var action = new OrchestratorAction
        {
            Id = taskId,
            CreateSubOrchestration = new CreateSubOrchestrationAction
            {
                Name = workflowName, InstanceId = childInstanceId, Input = workflowSerializer.Serialize(input)
            },
            Router = !string.IsNullOrEmpty(options?.AppId) ? new TaskRouter { TargetAppID = options.AppId } : null
        };

        _pendingActions.Add(action);

        return new TaskCompletionSource<TResult>().Task;
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
                Result = workflowSerializer.Serialize(newInput),
            }
        };

        if (preserveUnprocessedEvents)
        {
            // Find all EventRaised events that were not consumed via WaitForExternalEventAsync
            var carryover = _pastEvents.Concat(_newEvents)
                .Where(e => e.EventRaised != null && !_consumedExternalEvents.Contains(e.EventRaised.Name));
                
            action.CompleteOrchestration.CarryoverEvents.AddRange(carryover);
        }

        _pendingActions.Add(action);
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
    
    private Task<T> HandleHistoryMatch<T>(string name, HistoryEvent e, int taskId, bool isReplay)
    {
        _logger.LogHandleHistoryMatch(isReplay ? "Replaying" : "Executing", taskId, name);

        return e switch
        {
            { TaskCompleted: { } completed } => HandleCompletedActivityFromHistory<T>(name, completed),
            { TaskFailed: { } failed } => HandleFailedActivityFromHistory<T>(name, failed),
            { SubOrchestrationInstanceCompleted: { } completed } => HandleCompletedChildWorkflowFromHistory<T>(name, completed),
            { SubOrchestrationInstanceFailed: { } failed } => HandleFailedChildWorkflowFromHistory<T>(name, failed),
            _ => throw new InvalidOperationException($"Unexpected history event type for task ID {taskId}")
        };
    }
    
    /// <summary>
    /// Extracts the TaskID/ScheduledID from a history event to correlate it with an action.
    /// </summary>
    private static bool TryGetTaskScheduledId(HistoryEvent e, out int scheduledId)
    {
        if (e.TaskCompleted != null) { scheduledId = e.TaskCompleted.TaskScheduledId; return true; }
        if (e.TaskFailed != null) { scheduledId = e.TaskFailed.TaskScheduledId; return true; }
        if (e.TimerFired != null) { scheduledId = e.TimerFired.TimerId; return true; }
        if (e.SubOrchestrationInstanceCompleted != null) { scheduledId = e.SubOrchestrationInstanceCompleted.TaskScheduledId; return true; }
        if (e.SubOrchestrationInstanceFailed != null) { scheduledId = e.SubOrchestrationInstanceFailed.TaskScheduledId; return true; }
            
        scheduledId = -1;
        return false;
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
        => string.IsNullOrEmpty(value) ? default! : workflowSerializer.Deserialize<T>(value)!;

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
