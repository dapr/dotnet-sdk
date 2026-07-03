namespace Dapr.Actors.Next.Core.Transport;

/// <summary>
/// Opens SubscribeActorEvents streams to daprd.
/// </summary>
public interface ISubscribeActorEventsTransport
{
    /// <summary>
    /// Opens a new bidirectional callback stream.
    /// </summary>
    ValueTask<ISubscribeActorEventsStream> OpenStreamAsync(CancellationToken cancellationToken = default);
}
