using Google.Protobuf.WellKnownTypes;

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// A message retrieved from a Dapr publish/subscribe topic.
/// </summary>
/// <param name="Id">The unique identifier of the topic message.</param>
/// <param name="Source">Identifies the context in which an event happened, such as the organization publishing the
/// event or the process that produced the event. The exact syntax and semantics behind the data
/// encoded in the URI is defined by the event producer.</param>
/// <param name="Type">The type of event related to the originating occurrence.</param>
/// <param name="SpecVersion">The spec version of the CloudEvents specification.</param>
/// <param name="DataContentType">The content type of the data.</param>
/// <param name="Topic">The name of the topic.</param>
/// <param name="PubSubName">The name of the Dapr publish/subscribe component.</param>
public sealed record TopicMessage(string Id, string Source, string Type, string SpecVersion, string DataContentType, string Topic, string PubSubName)
{
    /// <summary>
    /// The content of the event.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; init; }

    /// <summary>
    /// The matching path from the topic subscription/routes (if specified) for this event.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// A map of additional custom properties sent to the app. These are considered to be CloudEvent extensions.
    /// </summary>
    public Dictionary<string, Value> Extensions { get; init; } = new();
}
