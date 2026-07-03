namespace Dapr.Actors.Next.Abstractions.Dispatching;

/// <summary>
/// Describes a dispatch request delivered to generated dispatcher code.
/// </summary>
/// <remarks>
/// <see cref="Payload"/> carries the raw UTF-8 JSON argument bytes. Dispatchers deserialize directly from
/// these bytes so the runtime does not transcode through an intermediate JSON string on the hot path.
/// </remarks>
public readonly record struct ActorDispatchRequest(
    string ActorType,
    ActorId ActorId,
    string MethodName,
    ReadOnlyMemory<byte> Payload,
    IReadOnlyDictionary<string, string> Headers,
    ActorRequestContext RequestContext);
