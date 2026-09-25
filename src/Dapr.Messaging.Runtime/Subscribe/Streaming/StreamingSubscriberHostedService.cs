// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using Dapr.Messaging.PublishSubscribe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dapr.Messaging.Subscribe.Streaming;

/// <summary>
/// Hosts the <see cref="DeliveryMode.Streaming"/> subscriptions discovered in the source-generated
/// subscriber registry, opening one bidirectional gRPC subscription per streaming descriptor via the
/// existing <see cref="PublishSubscribeReceiver"/> plumbing (through
/// <see cref="DaprPublishSubscribeClient.SubscribeAsync"/>) and dispatching received messages through the
/// matching generated <see cref="ITopicDispatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registered unconditionally by <c>DaprMessagingRegistration.Register</c> (i.e. independent of
/// <see cref="DaprMessagingFeatures"/>) so declarative streaming handlers work out of the box with a
/// single <c>AddDaprMessaging()</c> call. When the registry contains no streaming descriptors,
/// <see cref="StartAsync"/> is a no-op.
/// </para>
/// <para>
/// Each streaming subscription is independently supervised: if its underlying
/// <see cref="PublishSubscribeReceiver"/> completes (cleanly or with a fault), the service waits
/// <see cref="DaprMessagingOptions.StreamingReconnectDelay"/> and re-subscribes, until the host is
/// stopped. This keeps the alpha <c>SubscribeTopicEventsAlpha1</c> stream connected across transient
/// sidecar restarts or connection interruptions without surfacing the fault to the generic host.
/// </para>
/// </remarks>
internal sealed class StreamingSubscriberHostedService(
    IDaprMessagingSubscriberRegistry registry,
    DaprPublishSubscribeClient client,
    IServiceProvider serviceProvider,
    IOptions<DaprMessagingOptions> options,
    ILogger<StreamingSubscriberHostedService> logger) : IHostedService, IAsyncDisposable
{
    private readonly List<Task> _runners = [];
    private CancellationTokenSource? _stoppingCts;

    /// <summary>
    /// Starts one supervised subscription loop per <see cref="DeliveryMode.Streaming"/> descriptor
    /// discovered in the subscriber registry. Returns immediately; the loops run in the background
    /// for the lifetime of the host.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var streamingDescriptors = registry.Descriptors
            .Where(d => d.Delivery == DeliveryMode.Streaming)
            .ToList();

        if (streamingDescriptors.Count == 0)
        {
            return Task.CompletedTask;
        }

        _stoppingCts = new CancellationTokenSource();

        foreach (var descriptor in streamingDescriptors)
        {
            _runners.Add(RunSubscriptionLoopAsync(descriptor, _stoppingCts.Token));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Signals cancellation to every running subscription loop and waits (best-effort) for them to
    /// unwind. Individual loop faults are already handled internally, so this does not rethrow.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stoppingCts is null)
        {
            return;
        }

        await _stoppingCts.CancelAsync().ConfigureAwait(false);

        try
        {
            await Task.WhenAll(_runners).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Host shutdown deadline elapsed or was cancelled; the loops will still observe
            // the cancelled token and unwind independently.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "One or more streaming subscription loops faulted while stopping.");
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _stoppingCts?.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Supervises a single streaming subscription: subscribes, awaits completion, and — unless the
    /// service is stopping — waits <see cref="DaprMessagingOptions.StreamingReconnectDelay"/> before
    /// re-subscribing. Runs until <paramref name="stoppingToken"/> is cancelled.
    /// </summary>
    private async Task RunSubscriptionLoopAsync(TopicSubscriptionDescriptor descriptor, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IAsyncDisposable? subscription = null;

            try
            {
                var subscriptionOptions = BuildSubscriptionOptions(descriptor);

                subscription = await client.SubscribeAsync(
                    descriptor.PubsubName,
                    descriptor.TopicName,
                    subscriptionOptions,
                    (message, ct) => DispatchAsync(descriptor, message, ct),
                    stoppingToken).ConfigureAwait(false);

                if (subscription is IDaprSubscription observable)
                {
                    // Race subscription completion against cancellation: StopAsync must be able to
                    // unblock this loop even if the underlying stream never naturally completes.
                    var cancellationTask = Task.Delay(Timeout.Infinite, stoppingToken);
                    var completed = await Task.WhenAny(observable.Completion, cancellationTask).ConfigureAwait(false);
                    await completed.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "The streaming subscription for pubsub '{PubsubName}' topic '{TopicName}' faulted; reconnecting in {Delay}.",
                    descriptor.PubsubName, descriptor.TopicName, options.Value.StreamingReconnectDelay);
            }
            finally
            {
                if (subscription is not null)
                {
                    try
                    {
                        await subscription.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Failed to cleanly dispose the streaming subscription for pubsub '{PubsubName}' topic '{TopicName}'.",
                            descriptor.PubsubName, descriptor.TopicName);
                    }
                }
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(options.Value.StreamingReconnectDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Resolves the matching dispatcher for the descriptor's (pubsub, topic) under
    /// <see cref="DeliveryMode.Streaming"/>, builds the delivery context, and dispatches within a
    /// per-message DI scope.
    /// </summary>
    private async Task<TopicResponseAction> DispatchAsync(
        TopicSubscriptionDescriptor descriptor, TopicMessage message, CancellationToken cancellationToken)
    {
        var dispatcher = registry.Resolve(descriptor.PubsubName, descriptor.TopicName, DeliveryMode.Streaming);
        if (dispatcher is null)
        {
            // No handler registered for this (pubsub, topic) under Streaming delivery; drop to avoid
            // an endless retry loop against the sidecar.
            return TopicResponseAction.Drop;
        }

        var context = BuildContext(message);
        using var scope = serviceProvider.CreateScope();
        return await dispatcher.DispatchAsync(message.Data.ToArray(), context, scope.ServiceProvider, cancellationToken)
            .ConfigureAwait(false);
    }

    private static TopicContext BuildContext(TopicMessage message)
    {
        var headers = new Dictionary<string, string>
        {
            ["id"] = message.Id,
            ["source"] = message.Source,
            ["type"] = message.Type,
            ["specversion"] = message.SpecVersion,
            ["datacontenttype"] = message.DataContentType,
            ["topic"] = message.Topic,
            ["pubsubname"] = message.PubSubName,
        };

        if (!string.IsNullOrEmpty(message.Path))
        {
            headers["path"] = message.Path;
        }

        return new TopicContext
        {
            PubsubName = message.PubSubName,
            TopicName = message.Topic,
            MessageId = message.Id,
            Headers = headers,
            RawPayload = message.Data,
        };
    }

    private static DaprSubscriptionOptions BuildSubscriptionOptions(TopicSubscriptionDescriptor descriptor) =>
        new(new MessageHandlingPolicy(TimeSpan.FromSeconds(30), TopicResponseAction.Retry))
        {
            Metadata = descriptor.Metadata,
            DeadLetterTopic = descriptor.DeadLetterTopic,
        };
}
