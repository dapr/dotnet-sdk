namespace Dapr.Actors.Next.Abstractions.Options;

/// <summary>
/// Describes an actor type explicitly requested by the app.
/// </summary>
public sealed record DaprActorRegistration(Type ActorImplementationType, string? ActorTypeName);
