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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;

namespace Dapr.IntegrationTest.Messaging.PublishSubscribe;

public record IntegrationHttpNotification(string Message, string Severity);

public sealed class HttpNotificationState
{
    public ConcurrentBag<(IntegrationHttpNotification Notification, TopicContext Context)> Received { get; } = new();
}

[DaprTopic("pubsub", "integration-http-notifications", Delivery = DeliveryMode.Http, Route = "api/v1/notifications")]
public class IntegrationHttpNotificationHandler : ITopicHandler<IntegrationHttpNotification>
{
    private readonly HttpNotificationState _state;

    public IntegrationHttpNotificationHandler(HttpNotificationState state)
    {
        _state = state;
    }

    public Task<TopicResponseAction> HandleAsync(IntegrationHttpNotification message, TopicContext context, CancellationToken cancellationToken)
    {
        _state.Received.Add((message, context));
        return Task.FromResult(TopicResponseAction.Success);
    }
}

public record IntegrationHttpBulkItem(string ItemId, int Quantity);

public sealed class HttpBulkState
{
    public ConcurrentBag<(IntegrationHttpBulkItem Item, TopicContext Context)> Received { get; } = new();
}

[DaprTopic("pubsub", "integration-http-bulk-items", Delivery = DeliveryMode.Http, Route = "api/v1/bulk-items", BulkSubscribe = true, MaxMessagesCount = 10, MaxAwaitDurationMs = 100)]
public class IntegrationHttpBulkItemHandler : ITopicHandler<IntegrationHttpBulkItem>
{
    private readonly HttpBulkState _state;

    public IntegrationHttpBulkItemHandler(HttpBulkState state)
    {
        _state = state;
    }

    public Task<TopicResponseAction> HandleAsync(IntegrationHttpBulkItem message, TopicContext context, CancellationToken cancellationToken)
    {
        _state.Received.Add((message, context));
        return Task.FromResult(TopicResponseAction.Success);
    }
}
