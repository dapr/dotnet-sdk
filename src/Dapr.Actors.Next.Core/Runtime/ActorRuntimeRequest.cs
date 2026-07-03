using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Scheduling;

namespace Dapr.Actors.Next.Core.Runtime;

/// <summary>
/// Describes one inbound actor callback.
/// </summary>
public readonly record struct ActorRuntimeRequest(
    string ActorType,
    ActorId ActorId,
    string OperationName,
    ActorTurnKind Kind,
    ReadOnlyMemory<byte> Payload,
    IReadOnlyDictionary<string, string> Headers,
    ActorRequestContext RequestContext);
