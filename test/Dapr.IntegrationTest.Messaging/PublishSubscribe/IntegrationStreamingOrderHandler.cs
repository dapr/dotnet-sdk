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

// This handler is discovered at compile time by the Dapr.Messaging.Generators source generator. Its
// Streaming delivery mode means it is hosted exclusively by StreamingSubscriberHostedService - there is
// no HTTP route or AppCallback gRPC service involved.

public record IntegrationStreamingOrder(string Id, int Total);

/// <summary>
/// Records the orders delivered to <see cref="IntegrationStreamingOrderHandler"/> so that tests driving a
/// real Dapr sidecar can assert the message reached the handler via the streaming subscription path.
/// </summary>
public sealed class StreamingOrderState
{
    private readonly ConcurrentQueue<(IntegrationStreamingOrder Order, TopicContext Context)> _received = new();

    public IReadOnlyCollection<(IntegrationStreamingOrder Order, TopicContext Context)> Received => _received;

    public void Add(IntegrationStreamingOrder order, TopicContext context) => _received.Enqueue((order, context));
}

[DaprTopic("pubsub", "integration-streaming-orders", Delivery = DeliveryMode.Streaming)]
public class IntegrationStreamingOrderHandler : ITopicHandler<IntegrationStreamingOrder>
{
    private readonly StreamingOrderState _state;

    public IntegrationStreamingOrderHandler(StreamingOrderState state)
    {
        _state = state;
    }

    public Task<TopicResponseAction> HandleAsync(IntegrationStreamingOrder message, TopicContext context, CancellationToken cancellationToken)
    {
        _state.Add(message, context);
        return Task.FromResult(TopicResponseAction.Success);
    }
}
