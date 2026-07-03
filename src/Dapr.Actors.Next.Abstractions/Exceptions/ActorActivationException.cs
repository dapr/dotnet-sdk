namespace Dapr.Actors.Next.Abstractions.Exceptions;

/// <summary>
/// Thrown when actor activation fails.
/// </summary>
public sealed class ActorActivationException : DaprActorException
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorActivationException()
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorActivationException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorActivationException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
