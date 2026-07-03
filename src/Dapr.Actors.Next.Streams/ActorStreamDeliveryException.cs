namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Base exception for explicit stream delivery classification.
/// </summary>
public abstract class ActorStreamDeliveryException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActorStreamDeliveryException"/> class.
    /// </summary>
    protected ActorStreamDeliveryException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Exception that marks a stream delivery failure as transient.
/// </summary>
public sealed class ActorStreamTransientException(string message) : ActorStreamDeliveryException(message);

/// <summary>
/// Exception that marks a stream delivery failure as poison.
/// </summary>
public sealed class ActorStreamPoisonException(string message) : ActorStreamDeliveryException(message);
