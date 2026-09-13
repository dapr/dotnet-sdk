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

using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;

namespace Dapr.IntegrationTest.Messaging.PublishSubscribe;

// This handler is discovered at compile time by the Dapr.Messaging.Generators source generator,
// which emits the AddDaprMessaging extension exercised by GeneratedSubscriberWiringTests.

public record IntegrationOrder(string Id, int Total);

[DaprTopic("pubsub", "integration-orders", Delivery = DeliveryMode.Programmatic)]
public class IntegrationOrderHandler : ITopicHandler<IntegrationOrder>
{
    public Task<TopicResponseAction> HandleAsync(IntegrationOrder message, TopicContext context, CancellationToken cancellationToken)
        => Task.FromResult(TopicResponseAction.Success);
}
