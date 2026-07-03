namespace Dapr.Actors.Next.Abstractions.Exceptions;

/// <summary>
/// Thrown when an event is invalid for an actor state machine state.
/// </summary>
public sealed class InvalidActorEventException : DaprActorException
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public InvalidActorEventException()
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public InvalidActorEventException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public InvalidActorEventException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new exception from the state and event that failed.
    /// </summary>
    public InvalidActorEventException(object? state, object? actorEvent)
        : base($"Event '{actorEvent}' is invalid for actor state '{state}'.")
    {
        StateName = state?.ToString();
        EventName = actorEvent?.GetType().FullName ?? actorEvent?.ToString();
    }

    /// <summary>
    /// Gets the state name associated with the invalid event.
    /// </summary>
    public string? StateName { get; }

    /// <summary>
    /// Gets the event name associated with the invalid event.
    /// </summary>
    public string? EventName { get; }
}
