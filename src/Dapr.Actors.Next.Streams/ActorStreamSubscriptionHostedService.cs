using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Opens all generated actor stream subscriptions for this app instance.
/// </summary>
public sealed class ActorStreamSubscriptionHostedService(
    IActorStreamSubscriptionRegistry registry,
    DaprMessagingActorStreamSubscriber subscriber,
    ILogger<ActorStreamSubscriptionHostedService> logger) : IHostedService, IAsyncDisposable
{
    private readonly List<IAsyncDisposable> subscriptions = [];

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var subscription in registry.Subscriptions)
        {
            var handle = await subscriber.SubscribeAsync(subscription, cancellationToken).ConfigureAwait(false);
            subscriptions.Add(handle);
            logger.LogInformation(
                "Opened actor stream subscription {PubsubName}/{Topic} for {ActorType}.{MethodName}.",
                subscription.PubsubName,
                subscription.Topic,
                subscription.ActorType,
                subscription.MethodName);
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var subscription in subscriptions)
        {
            await subscription.DisposeAsync().ConfigureAwait(false);
        }

        subscriptions.Clear();
    }
}
