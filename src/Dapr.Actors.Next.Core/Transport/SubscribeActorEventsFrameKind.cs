namespace Dapr.Actors.Next.Core.Transport;

/// <summary>
/// Identifies an SubscribeActorEvents stream frame kind.
/// </summary>
public enum SubscribeActorEventsFrameKind
{
    /// <summary>
    /// Actor-type advertisement sent by the app.
    /// </summary>
    RegisteredActors = 0,

    /// <summary>
    /// Actor method invocation callback.
    /// </summary>
    Invoke = 1,

    /// <summary>
    /// Actor reminder callback.
    /// </summary>
    Reminder = 2,

    /// <summary>
    /// Actor timer callback.
    /// </summary>
    Timer = 3,

    /// <summary>
    /// Actor deactivation callback.
    /// </summary>
    Deactivate = 4,
}
