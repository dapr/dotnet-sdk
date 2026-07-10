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

using Google.Protobuf;
using Grpc.Core;
using Dapr.Actors.Next.Core.Transport;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Actors.Next.Core.State;

/// <summary>
/// Persists actor state through the sidecar state API.
/// </summary>
public sealed class DaprSidecarActorStateStore : IActorStateStore
{
    private static readonly byte[] JsonNull = "null"u8.ToArray();
    private readonly Lazy<P.Dapr.DaprClient> client;
    private readonly string storeName;
    private readonly string? daprApiToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprSidecarActorStateStore"/> class.
    /// </summary>
    public DaprSidecarActorStateStore(P.Dapr.DaprClient client, string storeName = "statestore", string? daprApiToken = null)
        : this(CreateEagerAccessor(client), storeName, daprApiToken)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprSidecarActorStateStore"/> class whose Dapr gRPC
    /// client is resolved on first use, so constructing the store does not eagerly build the transport channel.
    /// </summary>
    public DaprSidecarActorStateStore(Lazy<P.Dapr.DaprClient> client, string storeName = "statestore", string? daprApiToken = null)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.storeName = storeName;
        this.daprApiToken = daprApiToken;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default)
    {
        var response = await client.Value.GetStateAsync(
            new P.GetStateRequest { StoreName = storeName, Key = CreateKey(actorType, actorId, name) },
            DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);

        if (response.Data.IsEmpty || response.Data.Span.SequenceEqual(JsonNull))
        {
            return null;
        }

        return response.Data.ToByteArray();
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(string actorType, string actorId, string name, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        var request = new P.SaveStateRequest { StoreName = storeName };
        request.States.Add(new P.StateItem
        {
            Key = CreateKey(actorType, actorId, name),
            Value = ByteString.CopyFrom(value.Span),
        });

        await client.Value.SaveStateAsync(
            request,
            DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DeleteAsync(string actorType, string actorId, string name, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.Value.DeleteStateAsync(
                new P.DeleteStateRequest { StoreName = storeName, Key = CreateKey(actorType, actorId, name) },
                DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken)).ConfigureAwait(false);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
        }
    }

    private static Lazy<P.Dapr.DaprClient> CreateEagerAccessor(P.Dapr.DaprClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new Lazy<P.Dapr.DaprClient>(() => client);
    }

    private static string CreateKey(string actorType, string actorId, string name) =>
        $"actors-next:{Uri.EscapeDataString(actorType)}:{Uri.EscapeDataString(actorId)}:{Uri.EscapeDataString(name)}";
}
