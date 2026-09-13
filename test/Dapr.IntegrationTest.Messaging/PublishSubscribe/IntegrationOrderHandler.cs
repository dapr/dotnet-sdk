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

using System.Collections.Concurrent;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;

namespace Dapr.IntegrationTest.Messaging.PublishSubscribe;

// This handler is discovered at compile time by the Dapr.Messaging.Generators source generator,
// which emits the AddDaprMessaging extension exercised by GeneratedSubscriberWiringTests.

public record IntegrationOrder(string Id, int Total);

/// <summary>
/// Records the orders delivered to <see cref="IntegrationOrderHandler"/> so that tests driving a real
/// Dapr sidecar can assert the message actually reached the handler.
/// </summary>
public sealed class IntegrationOrderState
{
    private readonly ConcurrentQueue<(IntegrationOrder Order, TopicContext Context)> _received = new();

    public IReadOnlyCollection<(IntegrationOrder Order, TopicContext Context)> Received => _received;

    public void Add(IntegrationOrder order, TopicContext context) => _received.Enqueue((order, context));
}

[DaprTopic("pubsub", "integration-orders", Delivery = DeliveryMode.Programmatic)]
public class IntegrationOrderHandler : ITopicHandler<IntegrationOrder>
{
    private readonly IntegrationOrderState _state;

    public IntegrationOrderHandler(IntegrationOrderState state)
    {
        _state = state;
    }

    public Task<TopicResponseAction> HandleAsync(IntegrationOrder message, TopicContext context, CancellationToken cancellationToken)
    {
        _state.Add(message, context);
        return Task.FromResult(TopicResponseAction.Success);
    }
}
