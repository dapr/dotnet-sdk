using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Core;
using Dapr.Actors.Next.Core.Serialization;

namespace Dapr.Actors.Next.Core.Client;

/// <summary>
/// Weakly typed actor client that accepts and returns JSON strings.
/// </summary>
public sealed class DynamicActorClient(IActorInvocationClient client, IActorWireSerializer serializer) : IDynamicActorClient
{
    /// <inheritdoc />
    public async Task<string?> InvokeAsync(string actorType, string actorId, string method, string argsJson, CancellationToken cancellationToken = default)
    {
        var result = await client.InvokeAsync(actorType, actorId, method, serializer.JsonToBytes(argsJson), ActorHeaders.Empty, cancellationToken).ConfigureAwait(false);
        return result is null ? null : serializer.BytesToJson(result);
    }
}
