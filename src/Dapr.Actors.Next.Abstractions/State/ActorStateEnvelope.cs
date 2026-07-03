namespace Dapr.Actors.Next.Abstractions.State;

/// <summary>
/// The persisted state envelope carrying schema version and typed payload.
/// </summary>
public sealed record ActorStateEnvelope<T>(int SchemaVersion, T Value);
