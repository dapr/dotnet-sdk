namespace Dapr.Actors.Next.Core.Transport;

/// <summary>
/// Represents one request frame received from the SubscribeActorEvents stream.
/// </summary>
public sealed record SubscribeActorEventsRequest(
    string Id,
    SubscribeActorEventsFrameKind Kind,
    string ActorType,
    string ActorId,
    string MethodName,
    ReadOnlyMemory<byte> Payload,
    IReadOnlyDictionary<string, string> Headers);
