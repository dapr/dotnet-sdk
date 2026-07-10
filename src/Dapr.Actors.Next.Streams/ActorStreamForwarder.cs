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
