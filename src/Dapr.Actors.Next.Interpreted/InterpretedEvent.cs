using System.Text.Json;

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Event raised into an interpreted state-machine actor.
/// </summary>
public sealed record InterpretedEvent(string Name, JsonElement Payload);

/// <summary>
/// Result returned from an interpreted state-machine event turn.
/// </summary>
public sealed record InterpretedRaiseResult(string State, JsonElement? Reply, DynamicStateBag Data);
