namespace Dapr.Actors.Next.Core.Transport;

/// <summary>
/// Represents an open SubscribeActorEvents bidirectional stream.
/// </summary>
public interface ISubscribeActorEventsStream : IAsyncDisposable
{
    /// <summary>
    /// Reads request frames from the runtime.
    /// </summary>
    IAsyncEnumerable<SubscribeActorEventsRequest> ReadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a response frame to the runtime.
    /// </summary>
    ValueTask WriteAsync(SubscribeActorEventsResponse response, CancellationToken cancellationToken = default);
}
