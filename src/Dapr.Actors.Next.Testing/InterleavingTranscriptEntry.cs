using Dapr.Actors.Next.Abstractions.Scheduling;

namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Records one scheduled actor turn for deterministic replay.
/// </summary>
public sealed record InterleavingTranscriptEntry(
    int Step,
    string Scheduler,
    int Seed,
    string ActorType,
    string ActorId,
    string OperationName,
    ActorTurnKind Kind);
