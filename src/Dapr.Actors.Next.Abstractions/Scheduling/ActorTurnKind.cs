namespace Dapr.Actors.Next.Abstractions.Scheduling;

/// <summary>
/// Identifies the kind of actor turn.
/// </summary>
public enum ActorTurnKind
{
    /// <summary>
    /// A normal actor method invocation.
    /// </summary>
    Invoke,

    /// <summary>
    /// A reminder callback.
    /// </summary>
    Reminder,

    /// <summary>
    /// A timer callback.
    /// </summary>
    Timer,

    /// <summary>
    /// A deactivation callback.
    /// </summary>
    Deactivate,

    /// <summary>
    /// A pub/sub event forwarded to an actor.
    /// </summary>
    Subscription,
}
