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

using System.Text.Json;
using Dapr.Client.Autogen.Grpc.v1;
using Dapr.Common.Serialization;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Grpc.Core;

namespace Dapr.VirtualActors.Runtime.Grpc;

/// <summary>
/// Communicates with the Dapr sidecar via gRPC for all actor operations.
/// </summary>
/// <remarks>
/// This replaces the HTTP-based <c>DaprHttpInteractor</c> from the legacy actors package,
/// providing a persistent gRPC connection with no timeout constraints.
/// </remarks>
internal sealed partial class DaprGrpcActorClient : IActorStateProvider, IActorTimerManager
{
    private readonly Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient _grpcClient;
    private readonly IDaprSerializer _serializer;
    private readonly ILogger<DaprGrpcActorClient> _logger;
    private readonly string? _daprApiToken;

    public DaprGrpcActorClient(
        Dapr.Client.Autogen.Grpc.v1.Dapr.DaprClient grpcClient,
        IDaprSerializer serializer,
        ILogger<DaprGrpcActorClient> logger,
        string? daprApiToken)
    {
        ArgumentNullException.ThrowIfNull(grpcClient);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(logger);

        _grpcClient = grpcClient;
        _serializer = serializer;
        _logger = logger;
        _daprApiToken = daprApiToken;
    }

    #region IActorStateProvider

    /// <inheritdoc />
    public async Task<ConditionalValue<T>> TryLoadStateAsync<T>(
        string actorType,
        VirtualActorId actorId,
        string stateName,
        CancellationToken cancellationToken = default)
    {
        var request = new GetActorStateRequest
        {
            ActorType = actorType,
            ActorId = actorId.GetId(),
            Key = stateName,
        };

        try
        {
            var response = await _grpcClient.GetActorStateAsync(request, CreateCallOptions(cancellationToken));

            if (response.Data.IsEmpty)
            {
                return ConditionalValue<T>.None;
            }

            var value = _serializer.DeserializeFromBytes<T>(response.Data.Span);
            return value is not null ? ConditionalValue<T>.Some(value) : ConditionalValue<T>.None;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return ConditionalValue<T>.None;
        }
    }

    /// <inheritdoc />
    public async Task SaveStateAsync(
        string actorType,
        VirtualActorId actorId,
        IReadOnlyList<ActorStateChange> stateChanges,
        CancellationToken cancellationToken = default)
    {
        if (stateChanges.Count == 0)
        {
            return;
        }

        var request = new ExecuteActorStateTransactionRequest
        {
            ActorType = actorType,
            ActorId = actorId.GetId(),
        };

        foreach (var change in stateChanges)
        {
            var operation = new TransactionalActorStateOperation
            {
                OperationType = change.ChangeKind switch
                {
                    StateChangeKind.Add => "upsert",
                    StateChangeKind.Update => "upsert",
                    StateChangeKind.Remove => "delete",
                    _ => throw new ArgumentOutOfRangeException(nameof(change), $"Unsupported change kind: {change.ChangeKind}"),
                },
                Key = change.StateName,
            };

            if (change.Value is not null && change.ChangeKind is StateChangeKind.Add or StateChangeKind.Update)
            {
                var bytes = _serializer.SerializeToBytes(change.Value);
                operation.Value = Google.Protobuf.WellKnownTypes.Any.Pack(
                    new BytesValue { Value = ByteString.CopyFrom(bytes) });
            }

            if (change.Ttl.HasValue)
            {
                operation.Metadata["ttlInSeconds"] = ((int)change.Ttl.Value.TotalSeconds).ToString();
            }

            request.Operations.Add(operation);
        }

        LogSavingActorState(actorType, actorId.GetId(), stateChanges.Count);
        await _grpcClient.ExecuteActorStateTransactionAsync(request, CreateCallOptions(cancellationToken));
    }

    #endregion

    #region IActorTimerManager

    /// <inheritdoc />
    public async Task RegisterTimerAsync(
        string actorType,
        VirtualActorId actorId,
        string timerName,
        string callbackMethodName,
        byte[]? callbackData,
        TimeSpan dueTime,
        TimeSpan period,
        TimeSpan? ttl,
        CancellationToken cancellationToken)
    {
        var request = new RegisterActorTimerRequest
        {
            ActorType = actorType,
            ActorId = actorId.GetId(),
            Name = timerName,
            Callback = callbackMethodName,
            DueTime = FormatTimeSpan(dueTime),
            Period = FormatTimeSpan(period),
        };

        if (callbackData is not null)
        {
            request.Data = ByteString.CopyFrom(callbackData);
        }

        if (ttl.HasValue)
        {
            request.Ttl = FormatTimeSpan(ttl.Value);
        }

        LogRegisteringTimer(actorType, actorId.GetId(), timerName);
        await _grpcClient.RegisterActorTimerAsync(request, CreateCallOptions(cancellationToken));
    }

    /// <inheritdoc />
    public async Task UnregisterTimerAsync(
        string actorType,
        VirtualActorId actorId,
        string timerName,
        CancellationToken cancellationToken)
    {
        var request = new UnregisterActorTimerRequest
        {
            ActorType = actorType,
            ActorId = actorId.GetId(),
            Name = timerName,
        };

        LogUnregisteringTimer(actorType, actorId.GetId(), timerName);
        await _grpcClient.UnregisterActorTimerAsync(request, CreateCallOptions(cancellationToken));
    }

    /// <inheritdoc />
    public async Task RegisterReminderAsync(
        string actorType,
        VirtualActorId actorId,
        string reminderName,
        byte[]? data,
        TimeSpan dueTime,
        TimeSpan period,
        TimeSpan? ttl,
        CancellationToken cancellationToken)
    {
        var request = new RegisterActorReminderRequest
        {
            ActorType = actorType,
            ActorId = actorId.GetId(),
            Name = reminderName,
            DueTime = FormatTimeSpan(dueTime),
            Period = FormatTimeSpan(period),
        };

        if (data is not null)
        {
            request.Data = ByteString.CopyFrom(data);
        }

        if (ttl.HasValue)
        {
            request.Ttl = FormatTimeSpan(ttl.Value);
        }

        LogRegisteringReminder(actorType, actorId.GetId(), reminderName);
        await _grpcClient.RegisterActorReminderAsync(request, CreateCallOptions(cancellationToken));
    }

    /// <inheritdoc />
    public async Task UnregisterReminderAsync(
        string actorType,
        VirtualActorId actorId,
        string reminderName,
        CancellationToken cancellationToken)
    {
        var request = new UnregisterActorReminderRequest
        {
            ActorType = actorType,
            ActorId = actorId.GetId(),
            Name = reminderName,
        };

        LogUnregisteringReminder(actorType, actorId.GetId(), reminderName);
        await _grpcClient.UnregisterActorReminderAsync(request, CreateCallOptions(cancellationToken));
    }

    #endregion

    #region Actor Invocation

    /// <summary>
    /// Invokes an actor method via gRPC.
    /// </summary>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor ID.</param>
    /// <param name="methodName">The method to invoke.</param>
    /// <param name="data">Optional serialized request data.</param>
    /// <param name="metadata">Optional metadata headers.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The serialized response data.</returns>
    public async Task<byte[]> InvokeActorMethodAsync(
        string actorType,
        string actorId,
        string methodName,
        byte[]? data = null,
        IDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var request = new InvokeActorRequest
        {
            ActorType = actorType,
            ActorId = actorId,
            Method = methodName,
        };

        if (data is not null)
        {
            request.Data = ByteString.CopyFrom(data);
        }

        if (metadata is not null)
        {
            foreach (var (key, value) in metadata)
            {
                request.Metadata[key] = value;
            }
        }

        LogInvokingActorMethod(actorType, actorId, methodName);
        var response = await _grpcClient.InvokeActorAsync(request, CreateCallOptions(cancellationToken));
        return response.Data.ToByteArray();
    }

    #endregion

    #region Helpers

    private CallOptions CreateCallOptions(CancellationToken cancellationToken)
    {
        var callOptions = new CallOptions(cancellationToken: cancellationToken);

        if (!string.IsNullOrWhiteSpace(_daprApiToken))
        {
            var metadata = new Metadata { { "dapr-api-token", _daprApiToken } };
            callOptions = callOptions.WithHeaders(metadata);
        }

        return callOptions;
    }

    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        // Dapr expects Go-style duration strings (e.g., "1h30m", "500ms")
        // For simplicity, we use ISO 8601 / XML duration format which Dapr also accepts
        if (timeSpan == TimeSpan.Zero)
        {
            return "0s";
        }

        return System.Xml.XmlConvert.ToString(timeSpan);
    }

    #endregion

    #region LoggerMessage Source-Generated Logs

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saving {Count} state change(s) for actor {ActorType}/{ActorId}")]
    private partial void LogSavingActorState(string actorType, string actorId, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Registering timer '{TimerName}' for actor {ActorType}/{ActorId}")]
    private partial void LogRegisteringTimer(string actorType, string actorId, string timerName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Unregistering timer '{TimerName}' for actor {ActorType}/{ActorId}")]
    private partial void LogUnregisteringTimer(string actorType, string actorId, string timerName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Registering reminder '{ReminderName}' for actor {ActorType}/{ActorId}")]
    private partial void LogRegisteringReminder(string actorType, string actorId, string reminderName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Unregistering reminder '{ReminderName}' for actor {ActorType}/{ActorId}")]
    private partial void LogUnregisteringReminder(string actorType, string actorId, string reminderName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Invoking method '{MethodName}' on actor {ActorType}/{ActorId}")]
    private partial void LogInvokingActorMethod(string actorType, string actorId, string methodName);

    #endregion
}
