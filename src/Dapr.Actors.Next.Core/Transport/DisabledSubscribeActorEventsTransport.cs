namespace Dapr.Actors.Next.Core.Transport;

/// <summary>
/// Placeholder transport used when no daprd stream client has been configured.
/// </summary>
public sealed class DisabledSubscribeActorEventsTransport : ISubscribeActorEventsTransport
{
    /// <inheritdoc />
    public ValueTask<ISubscribeActorEventsStream> OpenStreamAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("No SubscribeActorEvents transport is configured.");
}
