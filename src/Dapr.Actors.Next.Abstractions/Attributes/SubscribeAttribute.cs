namespace Dapr.Actors.Next.Abstractions.Attributes;

/// <summary>
/// Marks an actor method as a pub/sub subscription target.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class SubscribeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new subscription attribute.
    /// </summary>
    public SubscribeAttribute(string pubsubName, string topic)
    {
        PubsubName = pubsubName;
        Topic = topic;
    }

    /// <summary>
    /// Gets the Dapr pub/sub component name.
    /// </summary>
    public string PubsubName { get; }

    /// <summary>
    /// Gets the topic name.
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// Gets or sets the CloudEvent attribute or payload member used to route to an actor id.
    /// </summary>
    public string? RouteBy { get; init; }
}
