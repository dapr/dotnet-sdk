namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Runs host-owned subscriptions and gates component acknowledgements on actor processing.
/// </summary>
public sealed class ActorStreamSubscriptionRunner(
    ActorStreamForwarder forwarder,
    IActorStreamFailureClassifier failureClassifier)
{
    /// <summary>
    /// Processes one event and returns the subscription disposition.
    /// </summary>
    public async Task<ActorStreamDeliveryAction> ProcessEventAsync(
        ActorStreamSubscription subscription,
        ActorStreamEvent evt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await forwarder.ForwardAsync(subscription, evt, cancellationToken).ConfigureAwait(false);
            return ActorStreamDeliveryAction.Ack;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return failureClassifier.Classify(ex);
        }
    }

    /// <summary>
    /// Drains a subscription stream and invokes a component acknowledgement callback for each event.
    /// </summary>
    public async Task RunAsync(
        ActorStreamSubscription subscription,
        IActorStreamSubscriptionSource source,
        Func<ActorStreamEvent, ActorStreamDeliveryAction, CancellationToken, ValueTask> acknowledgeAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(acknowledgeAsync);

        await foreach (var evt in source.SubscribeAsync(subscription, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var action = await ProcessEventAsync(subscription, evt, cancellationToken).ConfigureAwait(false);
            await acknowledgeAsync(evt, action, cancellationToken).ConfigureAwait(false);
        }
    }
}
