namespace Dapr.Actors.Next.Abstractions.Scheduling;

/// <summary>
/// Represents a pending actor turn in a mailbox.
/// </summary>
public readonly record struct ActorTurn(
    string ActorType,
    ActorId ActorId,
    string OperationName,
    ActorTurnKind Kind,
    ActorRequestContext RequestContext,
    IReadOnlyDictionary<string, string> Headers);
