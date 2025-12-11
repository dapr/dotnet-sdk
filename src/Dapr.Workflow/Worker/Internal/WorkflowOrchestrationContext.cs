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
internal sealed class WorkflowOrchestrationContext(string name, string instanceId, IEnumerable<HistoryEvent> history, DateTime currentUtcDateTime, IWorkflowSerializer workflowSerializer, ILoggerFactory loggerFactory) : WorkflowContext
{
    private readonly List<HistoryEvent> _pastEvents = [..history];
    private readonly List<OrchestratorAction> _pendingActions = [];
    private readonly ILogger<WorkflowOrchestrationContext> _logger = loggerFactory?.CreateLogger<WorkflowOrchestrationContext>() ?? throw new ArgumentNullException(nameof(loggerFactory));
    // Parse instance ID as GUID or generate one
    private readonly Guid _instanceGuid = Guid.TryParse(instanceId, out var guid) ? guid : Guid.NewGuid();
    
    private int _sequenceNumber;
    // Process existing history to set replay state
    private int _historyIndex = 0;
    private int _guidCounter;
    private object? _customStatus;

    /// <inheritdoc />
    public override string Name { get; } = name;
    /// <inheritdoc />
    public override string InstanceId { get; } = instanceId;
    /// <inheritdoc />
    public override DateTime CurrentUtcDateTime { get; } = currentUtcDateTime;
    /// <inheritdoc />
    public override bool IsReplaying
    {
        get => _historyIndex < _pastEvents.Count;
    } 

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
        
        // Check if this task already exists in history (replay scenario)
        if (TryGetHistoryEvent(out var historyEvent))
        {
            return historyEvent switch
            {
                { TaskCompleted: { } completed } => HandleCompletedActivityFromHistory<T>(name, completed),
                { TaskFailed: { } failed } => HandleFailedActivityFromHistory<T>(name, failed),
                _ => throw new InvalidOperationException($"Unexpected history event type '{historyEvent.EventTypeCase}' at index {_historyIndex - 1}")
            };
        }
        
        // Not in history - schedule new activity execution
        _logger.LogSchedulingActivity(name, InstanceId);
        
        _pendingActions.Add(new OrchestratorAction
        {
            Id = _sequenceNumber++,
            ScheduleTask = new ScheduleTaskAction
            {
                Name = name,
                Input = workflowSerializer.Serialize(input)
            }
        });

        // Return a task that will never complete on this execution. It will only complete on
        // a future replay when the result is in history
        return new TaskCompletionSource<T>().Task;
    }

    /// <inheritdoc />
    public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
    {
        var taskId = _sequenceNumber++;
        
        // Check history for timer 
        if (TryGetHistoryEvent(out var historyEvent) && historyEvent.TimerFired is not null)
        {
            _logger.LogTimerFiredFromHistory(InstanceId);
            return Task.CompletedTask;
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

        // Return a task that will never complete on this execution
        // On the next replay after the timer fires, this will return completed task from history.
        var tcs = new TaskCompletionSource();
        
        // Handle cancellation by removing the pending action
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                // Remove the timer action if it hasn't been sent yet
                var actionIndex = _pendingActions.FindIndex(a => a.Id == taskId);
                if (actionIndex >= 0)
                {
                    _pendingActions.RemoveAt(actionIndex);
                }

                tcs.TrySetCanceled(cancellationToken);
            });
        }

        return tcs.Task;
    }

    /// <inheritdoc />
    public override Task<T> WaitForExternalEventAsync<T>(string eventName, CancellationToken cancellationToken = default)
    {
        // Check for event in history
        if (TryGetHistoryEvent(out var historyEvent) && historyEvent.EventRaised is { } eventRaised &&
            string.Equals(eventRaised.Name, eventName, StringComparison.OrdinalIgnoreCase))
        {
            var eventData = eventRaised.Input ?? string.Empty;
            return Task.FromResult(DeserializeResult<T>(eventData));
        }

        // Event not in history yet - return a task that won't complete on this execution.
        // When the event is raised and added to history, a future replay will complete this.
        var tcs = new TaskCompletionSource<T>();

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        }

        return tcs.Task;
    }

    /// <inheritdoc />
    public override Task<T> WaitForExternalEventAsync<T>(string eventName, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return WaitForExternalEventAsync<T>(eventName, cts.Token);
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

    /// <inheritdoc />
    public override Task<TResult> CallChildWorkflowAsync<TResult>(string workflowName, object? input = null,
        ChildWorkflowTaskOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        
        var childInstanceId = options?.InstanceId ?? Guid.NewGuid().ToString();
        
        // Check history
        if (TryGetHistoryEvent(out var historyEvent))
        {
            return historyEvent switch
            {
                { SubOrchestrationInstanceCompleted: { } completed } => HandleCompletedChildWorkflowFromHistory<TResult>(workflowName, completed),
                { SubOrchestrationInstanceFailed: { } failed } => HandleFailedChildWorkflowFromHistory<TResult>(workflowName, failed),
                _ => throw new InvalidOperationException($"Unexpected history event type at index {_historyIndex - 1}")
            };
        }

        _logger.LogSchedulingChildWorkflow(workflowName, childInstanceId, InstanceId);
        
        _pendingActions.Add(new OrchestratorAction
        {
            Id = _sequenceNumber++,
            CreateSubOrchestration = new CreateSubOrchestrationAction
            {
                Name = workflowName,
                InstanceId = childInstanceId,
                Input = workflowSerializer.Serialize(input)
            }
        });
        
        // Return a task that will never complete on this execution.
        // It will only complete on a future replay when the result is in history.
        return new TaskCompletionSource<TResult>().Task;
    }

    /// <inheritdoc />
    public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true)
    {
        _pendingActions.Add(new OrchestratorAction
        {
            Id = _sequenceNumber++,
            CompleteOrchestration = new CompleteOrchestrationAction
            {
                OrchestrationStatus = OrchestrationStatus.ContinuedAsNew,
                Result = workflowSerializer.Serialize(newInput),
                CarryoverEvents = { preserveUnprocessedEvents ? GetUnprocessedEvents() : [] }
            }
        });
    }

    /// <inheritdoc />
    public override Guid NewGuid()
    {
        // Create deterministic Guid based on instance ID and counter
        var guidCounter = _guidCounter++;
        var name = $"{InstanceId}_{CurrentUtcDateTime:O}_{guidCounter}";
        return CreateGuidFromName(_instanceGuid, Encoding.UTF8.GetBytes(name));
    }
    
    /// <inheritdoc />
    public override ILogger CreateReplaySafeLogger(string categoryName) => new ReplaySafeLogger(_logger, () => IsReplaying);
    /// <inheritdoc />
    public override ILogger CreateReplaySafeLogger(Type type) => CreateReplaySafeLogger(type.FullName ?? type.Name);
    /// <inheritdoc />
    public override ILogger CreateReplaySafeLogger<T>() => CreateReplaySafeLogger(typeof(T));

    /// <summary>
    /// Gets unprocessed external events from the workflow history
    /// </summary>
    /// <returns></returns>
    private IEnumerable<HistoryEvent> GetUnprocessedEvents()
        => _pastEvents
            .Skip(_historyIndex)
            .Where(e => e.EventRaised is not null);
    
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
    /// Attempts to get the next history event if still replaying.
    /// </summary>
    private bool TryGetHistoryEvent(out HistoryEvent historyEvent)
    {
        if (_historyIndex < _pastEvents.Count)
        {
            historyEvent = _pastEvents[_historyIndex++];
            return true;
        }
        
        historyEvent = null!;
        return false;
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
