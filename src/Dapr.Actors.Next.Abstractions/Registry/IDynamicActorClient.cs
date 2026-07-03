namespace Dapr.Actors.Next.Abstractions.Registry;

/// <summary>
/// Invokes actors whose compile-time interface is not known by the caller.
/// </summary>
public interface IDynamicActorClient
{
    /// <summary>
    /// Invokes an actor with JSON arguments and returns a JSON result.
    /// </summary>
    Task<string?> InvokeAsync(string actorType, string actorId, string method, string argsJson, CancellationToken cancellationToken = default);
}
