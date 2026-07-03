namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Exception thrown for injected transient test faults.
/// </summary>
public sealed class ActorInjectedTransientException(string message) : Exception(message);
