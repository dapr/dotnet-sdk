namespace Dapr.Actors.Next.Abstractions.Exceptions;

/// <summary>
/// Thrown when actor state handling fails.
/// </summary>
public sealed class ActorStateException : DaprActorException
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorStateException()
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorStateException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorStateException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
