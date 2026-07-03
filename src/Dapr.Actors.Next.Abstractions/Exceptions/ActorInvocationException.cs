namespace Dapr.Actors.Next.Abstractions.Exceptions;

/// <summary>
/// Thrown when actor method invocation fails.
/// </summary>
public sealed class ActorInvocationException : DaprActorException
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorInvocationException()
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorInvocationException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public ActorInvocationException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
