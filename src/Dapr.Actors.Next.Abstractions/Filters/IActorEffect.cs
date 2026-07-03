namespace Dapr.Actors.Next.Abstractions.Filters;

/// <summary>
/// Represents a named interpreted actor effect.
/// </summary>
public interface IActorEffect
{
    /// <summary>
    /// Executes the effect.
    /// </summary>
    ValueTask ExecuteAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default);
}
