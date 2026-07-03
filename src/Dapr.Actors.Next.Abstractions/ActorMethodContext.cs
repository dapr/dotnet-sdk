namespace Dapr.Actors.Next.Abstractions;

/// <summary>
/// Describes the actor method currently being invoked.
/// </summary>
public readonly record struct ActorMethodContext(
    string ActorType,
    ActorId ActorId,
    string MethodName,
    IReadOnlyList<object?> Arguments,
    IReadOnlyDictionary<string, string> Headers);
