namespace Dapr.Actors.Next.Core.Client;

/// <summary>
/// Sends serialized actor invocations through the configured runtime path.
/// </summary>
public interface IActorInvocationClient
{
    /// <summary>
    /// Invokes an actor method and returns serialized response bytes.
    /// </summary>
    Task<byte[]?> InvokeAsync(
        string actorType,
        string actorId,
        string methodName,
        ReadOnlyMemory<byte> payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}
