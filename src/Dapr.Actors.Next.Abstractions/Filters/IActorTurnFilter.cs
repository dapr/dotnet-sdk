namespace Dapr.Actors.Next.Abstractions.Filters;

/// <summary>
/// Provides cross-cutting behavior around actor turns.
/// </summary>
public interface IActorTurnFilter
{
    /// <summary>
    /// Invokes the filter.
    /// </summary>
    ValueTask InvokeAsync(ActorTurnContext context, ActorTurnDelegate next);
}
