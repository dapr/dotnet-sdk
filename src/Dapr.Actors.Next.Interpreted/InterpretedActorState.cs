using System.Text.Json;

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Persisted dynamic state for an interpreted actor instance.
/// </summary>
public sealed record InterpretedActorState(
    int DocumentVersion,
    string CurrentState,
    IReadOnlyDictionary<string, JsonElement> Data);
