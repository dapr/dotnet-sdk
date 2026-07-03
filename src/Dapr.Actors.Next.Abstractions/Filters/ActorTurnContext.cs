using Dapr.Actors.Next.Abstractions.Scheduling;

namespace Dapr.Actors.Next.Abstractions.Filters;

/// <summary>
/// Context passed through the actor turn filter pipeline.
/// </summary>
public readonly record struct ActorTurnContext(
    string ActorType,
    ActorId ActorId,
    string OperationName,
    ActorTurnKind Kind,
    IReadOnlyDictionary<string, string> Headers,
    ActorRequestContext RequestContext,
    CancellationToken CancellationToken);
