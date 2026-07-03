namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Describes one host-owned pub/sub stream that forwards events into an actor method.
/// </summary>
public sealed record ActorStreamSubscription(
    string PubsubName,
    string Topic,
    string ActorType,
    string MethodName,
    string RouteBy)
{
    /// <summary>
    /// Gets subscription metadata sent in the initial dynamic subscription request.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the optional dead-letter topic configured for the dynamic subscription.
    /// </summary>
    public string? DeadLetterTopic { get; init; }

    /// <summary>
    /// Gets the maximum number of topic messages queued locally before backpressure applies.
    /// </summary>
    public int? MaximumQueuedMessages { get; init; }

    /// <summary>
    /// Gets the per-message processing timeout before the Dapr.Messaging default action applies.
    /// </summary>
    public TimeSpan MessageTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validates the subscription fields.
    /// </summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PubsubName);
        ArgumentException.ThrowIfNullOrWhiteSpace(Topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(ActorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(MethodName);
        ArgumentException.ThrowIfNullOrWhiteSpace(RouteBy);
    }
}
