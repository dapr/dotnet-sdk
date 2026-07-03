namespace Dapr.Actors.Next.Abstractions.Dispatching;

/// <summary>
/// Describes a dispatch response emitted by generated dispatcher code.
/// </summary>
/// <remarks>
/// <see cref="Result"/> holds the raw UTF-8 JSON result bytes (or <c>null</c> for a void method), which the
/// runtime returns to the caller without transcoding through an intermediate JSON string.
/// </remarks>
public readonly record struct ActorDispatchResponse(byte[]? Result);
