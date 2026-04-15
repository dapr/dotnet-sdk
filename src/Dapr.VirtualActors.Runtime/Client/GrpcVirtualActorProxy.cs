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

using Dapr.Common.Serialization;

namespace Dapr.VirtualActors.Runtime.Client;

/// <summary>
/// A weakly-typed actor proxy backed by gRPC that invokes methods dynamically.
/// </summary>
internal sealed class GrpcVirtualActorProxy : IVirtualActorProxy
{
    private readonly Grpc.DaprGrpcActorClient _grpcClient;
    private readonly IDaprSerializer _serializer;

    public GrpcVirtualActorProxy(
        Grpc.DaprGrpcActorClient grpcClient,
        IDaprSerializer serializer,
        VirtualActorId actorId,
        string actorType)
    {
        ArgumentNullException.ThrowIfNull(grpcClient);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);

        _grpcClient = grpcClient;
        _serializer = serializer;
        ActorId = actorId;
        ActorType = actorType;
    }

    /// <inheritdoc />
    public VirtualActorId ActorId { get; }

    /// <inheritdoc />
    public string ActorType { get; }

    /// <inheritdoc />
    public async Task InvokeMethodAsync(string methodName, CancellationToken cancellationToken = default)
    {
        await _grpcClient.InvokeActorMethodAsync(
            ActorType, ActorId.GetId(), methodName, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task InvokeMethodAsync<TRequest>(string methodName, TRequest data, CancellationToken cancellationToken = default)
    {
        var requestBytes = _serializer.SerializeToBytes(data, typeof(TRequest));
        await _grpcClient.InvokeActorMethodAsync(
            ActorType, ActorId.GetId(), methodName, requestBytes, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TResponse> InvokeMethodAsync<TResponse>(string methodName, CancellationToken cancellationToken = default)
    {
        var responseBytes = await _grpcClient.InvokeActorMethodAsync(
            ActorType, ActorId.GetId(), methodName, cancellationToken: cancellationToken);

        return _serializer.DeserializeFromBytes<TResponse>(responseBytes)
            ?? throw new InvalidOperationException($"Failed to deserialize response from actor method '{methodName}'.");
    }

    /// <inheritdoc />
    public async Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(string methodName, TRequest data, CancellationToken cancellationToken = default)
    {
        var requestBytes = _serializer.SerializeToBytes(data, typeof(TRequest));
        var responseBytes = await _grpcClient.InvokeActorMethodAsync(
            ActorType, ActorId.GetId(), methodName, requestBytes, cancellationToken: cancellationToken);

        return _serializer.DeserializeFromBytes<TResponse>(responseBytes)
            ?? throw new InvalidOperationException($"Failed to deserialize response from actor method '{methodName}'.");
    }
}
