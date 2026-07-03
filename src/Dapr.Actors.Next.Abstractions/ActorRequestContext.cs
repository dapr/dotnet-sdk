namespace Dapr.Actors.Next.Abstractions;

/// <summary>
/// Captures causal request metadata for an actor turn.
/// </summary>
public readonly record struct ActorRequestContext(
    string? TraceParent,
    string? TraceState,
    IReadOnlyDictionary<string, string> Baggage);
