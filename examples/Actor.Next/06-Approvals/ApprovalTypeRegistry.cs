using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Interpreted;

namespace Dapr.Actors.Next.Examples.Approvals;

/// <summary>
/// Describes the single compiled interpreted actor type this app hosts and the one method it exposes,
/// so a generic caller can discover it without a compiled document contract.
/// </summary>
public sealed class ApprovalTypeRegistry : IActorRegistry
{
    private readonly ActorTypeDescriptor _descriptor = new(
        ApprovalDefinitions.ActorType,
        1,
        typeof(InterpretedStateMachineActor),
        typeof(InterpretedStateMachineActor),
        [
            new ActorMethodDescriptor(
                "Raise",
                "Raise",
                typeof(InterpretedRaiseResult),
                [new ActorParameterDescriptor("evt", typeof(InterpretedEvent), 0, false, false, null)]),
        ]);

    public IReadOnlyList<ActorTypeDescriptor> Actors => [_descriptor];

    public bool TryGet(string actorType, out ActorTypeDescriptor value)
    {
        if (string.Equals(actorType, ApprovalDefinitions.ActorType, StringComparison.Ordinal))
        {
            value = _descriptor;
            return true;
        }

        value = null!;
        return false;
    }
}
