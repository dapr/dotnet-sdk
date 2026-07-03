namespace Dapr.Actors.Next.Abstractions.Exceptions;

/// <summary>
/// Base exception for Dapr Actors Next failures.
/// </summary>
public class DaprActorException : Exception
{
    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public DaprActorException()
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public DaprActorException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new exception.
    /// </summary>
    public DaprActorException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
