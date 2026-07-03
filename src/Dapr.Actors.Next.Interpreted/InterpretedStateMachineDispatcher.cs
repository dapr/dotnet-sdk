using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Dispatcher for the compiled interpreted state-machine actor.
/// </summary>
public sealed class InterpretedStateMachineDispatcher : IActorDispatcher
{
    /// <inheritdoc />
    public async ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
    {
        var typed = (InterpretedStateMachineActor)actor;
        if (!string.Equals(request.MethodName, "Raise", StringComparison.Ordinal)
            && !string.Equals(request.MethodName, nameof(InterpretedStateMachineActor.RaiseAsync), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Interpreted actor operation '{request.MethodName}' is not supported.");
        }

        var evt = JsonSerializer.Deserialize<InterpretedEvent>(request.Payload.Span)
            ?? throw new InvalidOperationException("Interpreted event payload could not be deserialized.");
        var result = await typed.RaiseAsync(evt, cancellationToken).ConfigureAwait(false);
        return new ActorDispatchResponse(JsonSerializer.SerializeToUtf8Bytes(result));
    }
}
