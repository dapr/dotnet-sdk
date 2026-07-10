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
