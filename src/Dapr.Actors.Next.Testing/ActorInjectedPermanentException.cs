namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Exception thrown for injected permanent test faults.
/// </summary>
public sealed class ActorInjectedPermanentException(string message) : Exception(message);
