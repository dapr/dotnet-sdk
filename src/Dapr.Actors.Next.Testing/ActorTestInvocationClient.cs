using Dapr.Actors.Next.Core.Client;

namespace Dapr.Actors.Next.Testing;

internal sealed class ActorTestInvocationClient(IActorInvocationClient inner, ActorFaults faults) : IActorInvocationClient
{
    public Task<byte[]?> InvokeAsync(
        string actorType,
        string actorId,
        string methodName,
        ReadOnlyMemory<byte> payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        faults.BeforeInvocation(actorType, methodName);
        return inner.InvokeAsync(actorType, actorId, methodName, payload, headers, cancellationToken);
    }
}
