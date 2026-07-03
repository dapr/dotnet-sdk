using Dapr.Actors.Next.Core.Client;

namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Forwards subscription events through the normal actor invocation path.
/// </summary>
public sealed class ActorStreamForwarder(
    IActorInvocationClient invocationClient,
    ActorStreamRoutingKeyExtractor routingKeyExtractor)
{
    /// <summary>
    /// Invokes the target actor selected by the subscription routing key.
    /// </summary>
    public async Task ForwardAsync(ActorStreamSubscription subscription, ActorStreamEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(evt);
        subscription.Validate();

        var actorId = routingKeyExtractor.ExtractActorId(subscription, evt);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(evt.TraceParent))
        {
            headers["traceparent"] = evt.TraceParent;
        }

        await invocationClient.InvokeAsync(
            subscription.ActorType,
            actorId,
            subscription.MethodName,
            evt.Data,
            headers,
            cancellationToken).ConfigureAwait(false);
    }
}
