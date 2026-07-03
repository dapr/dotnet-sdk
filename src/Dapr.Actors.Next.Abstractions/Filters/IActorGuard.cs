namespace Dapr.Actors.Next.Abstractions.Filters;

/// <summary>
/// Represents a named interpreted actor guard.
/// </summary>
public interface IActorGuard
{
    /// <summary>
    /// Evaluates the guard.
    /// </summary>
    ValueTask<bool> EvaluateAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default);
}
