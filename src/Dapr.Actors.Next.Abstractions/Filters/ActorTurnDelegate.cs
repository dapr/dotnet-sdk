namespace Dapr.Actors.Next.Abstractions.Filters;

/// <summary>
/// Invokes the next actor turn pipeline component.
/// </summary>
public delegate ValueTask ActorTurnDelegate(ActorTurnContext context);
