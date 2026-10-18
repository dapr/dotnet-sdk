// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common.Serialization;
using Dapr.DurableTask.Protobuf;

namespace Dapr.Workflow.Payloads;

/// <summary>
/// Owns serialization and coordinates codec I/O separately from deterministic execution.
/// Each orchestrator invocation has its own queue; clients and activities use direct async calls.
/// </summary>
internal sealed class WorkflowPayloadHandler(
    IDaprSerializer serializer,
    IWorkflowPayloadConverter codec,
    CancellationToken stoppingToken = default)
{
    private readonly Queue<Func<Task>> _reads = new();
    private readonly Dictionary<int, bool> _inlineCallInputs = [];
    internal CancellationToken StoppingToken => stoppingToken;

    internal void ConfigureCallInput(int actionId, string? sourceAppId, WorkflowTaskOptions? options) =>
        _inlineCallInputs[actionId] = options?.AllowPayloadReferences != true &&
                                     !string.IsNullOrWhiteSpace(options?.TargetAppId) &&
                                     !string.Equals(options.TargetAppId, sourceAppId, StringComparison.Ordinal);

    internal string Snapshot(object? value) => value switch
    {
        PayloadReference reference => reference.ToEnvelope(),
        null => string.Empty,
        _ => serializer.Serialize(value)
    };

    internal ValueTask<string> EncodeAsync(string snapshot, PayloadSite site,
        CancellationToken cancellationToken = default) => codec.EncodeAsync(snapshot, site,
        cancellationToken.CanBeCanceled ? cancellationToken : stoppingToken);

    internal async ValueTask<PreparedWorkflowPayload> PrepareAsync(string? wire, PayloadSite site,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return new(wire, await codec.DecodeAsync(wire, site,
                cancellationToken.CanBeCanceled ? cancellationToken : stoppingToken).ConfigureAwait(false));
        }
        catch (PayloadRehydrationLimitException ex)
        {
            // State readers are synchronous. Preserve access to oversized references while
            // reporting the buffering limit if a caller requests the materialized value.
            return new(wire, wire, ExceptionDispatchInfo.Capture(ex));
        }
    }

    internal async ValueTask<T?> DecodeAsync<T>(string? wire, PayloadSite site,
        CancellationToken cancellationToken = default)
    {
        if (PayloadReference.IsReferenceType(typeof(T)))
        {
            return (T?)ReadReference(wire, typeof(T), serializer);
        }

        var serialized = await codec.DecodeAsync(wire, site,
            cancellationToken.CanBeCanceled ? cancellationToken : stoppingToken).ConfigureAwait(false);
        return string.IsNullOrEmpty(serialized) ? default : serializer.Deserialize<T>(serialized);
    }

    internal async ValueTask<object?> DecodeAsync(string? wire, Type type, PayloadSite site,
        CancellationToken cancellationToken = default)
    {
        if (PayloadReference.IsReferenceType(type))
        {
            return ReadReference(wire, type, serializer);
        }

        var serialized = await codec.DecodeAsync(wire, site,
            cancellationToken.CanBeCanceled ? cancellationToken : stoppingToken).ConfigureAwait(false);
        return string.IsNullOrEmpty(serialized) ? null : serializer.Deserialize(serialized, type);
    }

    internal static object? ReadReference(string? wire, Type type, IDaprSerializer serializer)
    {
        if (string.IsNullOrEmpty(wire))
        {
            return null;
        }

        if (PayloadReferenceParser.Parse(wire) is { } reference)
        {
            return type == typeof(PayloadReference)
                ? reference
                : serializer.Deserialize(serializer.Serialize(reference), type);
        }

        const string message = "The receiving contract requires a PayloadReference, but the payload is inline. " +
                               "Use an ordinary input type or explicitly allow references on the sending call.";
        try
        {
            // Preserve compatibility with references serialized as record objects by older callers.
            if (serializer.Deserialize(wire, type) is PayloadReference legacy)
            {
                return legacy;
            }
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new InvalidOperationException(message, ex);
        }

        throw new InvalidOperationException(message);
    }

    internal Task<T> QueueAsync<T>(Func<Task<T>> read)
    {
        // Inline continuations are intentional: drain user code to its next durable wait
        // before processing the next history event (and advancing time/replay state).
        var completion = new TaskCompletionSource<T>();
        _reads.Enqueue(async () =>
        {
            try
            {
                completion.SetResult(await read().ConfigureAwait(false));
            }
            catch (OperationCanceledException ex)
            {
                completion.SetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        return completion.Task;
    }

    internal async Task DrainAsync()
    {
        while (_reads.TryDequeue(out var read))
        {
            await read().ConfigureAwait(false);
        }
    }

    internal async Task EncodeResponseAsync(WorkflowResponse response, string instanceId, string workflowName,
        bool isChild, CancellationToken cancellationToken = default)
    {
        foreach (var action in response.Actions)
        {
            var router = action.ScheduleTask?.Router ?? action.CreateChildWorkflow?.Router ??
                         action.CreateDetachedWorkflow?.Router ?? action.Router;
            var requireInline = _inlineCallInputs.TryGetValue(action.Id, out var configured)
                ? configured
                : !string.IsNullOrWhiteSpace(router?.TargetAppID) &&
                  !string.Equals(router.TargetAppID, router.SourceAppID, StringComparison.Ordinal);
            switch (action)
            {
                case { ScheduleTask: { } activity }:
                    activity.Input = await EncodeAsync(activity.Input ?? string.Empty,
                            new(PayloadKind.ActivityInput, instanceId, activity.Name, requireInline), cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case { CreateChildWorkflow: { } child }:
                    child.Input = await EncodeAsync(child.Input ?? string.Empty,
                            new(PayloadKind.ChildWorkflowInput, child.InstanceId, child.Name, requireInline), cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case { CreateDetachedWorkflow: { } detached }:
                    detached.Input = await EncodeAsync(detached.Input ?? string.Empty,
                            new(PayloadKind.WorkflowInput, detached.InstanceId, detached.Name, requireInline), cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case { SendEvent: { } sent }:
                    sent.Data = await EncodeAsync(sent.Data ?? string.Empty,
                            new(PayloadKind.ExternalEvent, sent.Instance.InstanceId, sent.Name), cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case { CompleteWorkflow: { } completed } when completed.WorkflowStatus is
                    OrchestrationStatus.Completed or OrchestrationStatus.ContinuedAsNew:
                    var kind = completed.WorkflowStatus == OrchestrationStatus.ContinuedAsNew
                        ? PayloadKind.ContinueAsNewInput
                        : isChild
                            ? PayloadKind.ChildWorkflowOutput
                            : PayloadKind.WorkflowOutput;
                    completed.Result = await EncodeAsync(completed.Result ?? string.Empty,
                        new(kind, instanceId, workflowName), cancellationToken).ConfigureAwait(false);
                    break;
            }
        }

        if (response.CustomStatus is not null)
        {
            response.CustomStatus = await EncodeAsync(response.CustomStatus,
                new(PayloadKind.CustomStatus, instanceId, null), cancellationToken).ConfigureAwait(false);
        }
    }
}
