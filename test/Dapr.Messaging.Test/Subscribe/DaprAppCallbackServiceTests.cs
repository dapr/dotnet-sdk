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
using Dapr.Messaging.Subscribe.AppCallback;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using P = Dapr.AppCallback.Autogen.Grpc.v1;

namespace Dapr.Messaging.Test.Subscribe;

public class DaprAppCallbackServiceTests
{
    private static DaprAppCallbackService CreateService(IDaprMessagingSubscriberRegistry registry, IServiceProvider? sp = null)
        => new DaprAppCallbackService(registry, sp ?? new ServiceCollection().BuildServiceProvider(), NullLogger<DaprAppCallbackService>.Instance);

    private static ServerCallContext Context() => new Mock<ServerCallContext>().Object;

    [Fact]
    public async Task ListTopicSubscriptions_ReturnsOnlyProgrammaticDescriptors()
    {
        var registry = new FakeRegistry(
            new FakeDispatcher("pubsub", "orders", DeliveryMode.Programmatic),
            new FakeDispatcher("pubsub", "events", DeliveryMode.Streaming));

        var service = CreateService(registry);
        var response = await service.ListTopicSubscriptions(new Google.Protobuf.WellKnownTypes.Empty(), Context());

        var sub = Assert.Single(response.Subscriptions);
        Assert.Equal("pubsub", sub.PubsubName);
        Assert.Equal("orders", sub.Topic);
    }

    [Fact]
    public async Task OnTopicEvent_DispatchesToResolvedHandler()
    {
        var dispatcher = new FakeDispatcher("pubsub", "orders", DeliveryMode.Programmatic, TopicResponseAction.Success);
        var registry = new FakeRegistry(dispatcher);

        var service = CreateService(registry, new ServiceCollection().BuildServiceProvider());

        var request = new P.TopicEventRequest
        {
            PubsubName = "pubsub",
            Topic = "orders",
            Id = "msg-1",
        };
        request.Data = Google.Protobuf.ByteString.CopyFrom(System.Text.Encoding.UTF8.GetBytes("{}"));

        var response = await service.OnTopicEvent(request, Context());

        Assert.Equal(P.TopicEventResponse.Types.TopicEventResponseStatus.Success, response.Status);
        Assert.True(dispatcher.WasCalled);
    }

    [Fact]
    public async Task OnTopicEvent_ReturnsDropWhenNoDispatcher()
    {
        var registry = new FakeRegistry();
        var service = CreateService(registry);

        var request = new P.TopicEventRequest { PubsubName = "pubsub", Topic = "unknown" };
        request.Data = Google.Protobuf.ByteString.CopyFrom(System.Text.Encoding.UTF8.GetBytes("{}"));

        var response = await service.OnTopicEvent(request, Context());

        Assert.Equal(P.TopicEventResponse.Types.TopicEventResponseStatus.Drop, response.Status);
    }

    [Theory]
    [InlineData(TopicResponseAction.Retry, P.TopicEventResponse.Types.TopicEventResponseStatus.Retry)]
    [InlineData(TopicResponseAction.Drop, P.TopicEventResponse.Types.TopicEventResponseStatus.Drop)]
    [InlineData(TopicResponseAction.Success, P.TopicEventResponse.Types.TopicEventResponseStatus.Success)]
    public async Task OnTopicEvent_MapsResponseActionToGrpcStatus(
        TopicResponseAction action, P.TopicEventResponse.Types.TopicEventResponseStatus expected)
    {
        var dispatcher = new FakeDispatcher("pubsub", "orders", DeliveryMode.Programmatic, action);
        var service = CreateService(new FakeRegistry(dispatcher));

        var request = new P.TopicEventRequest { PubsubName = "pubsub", Topic = "orders" };
        request.Data = Google.Protobuf.ByteString.CopyFrom(System.Text.Encoding.UTF8.GetBytes("{}"));

        var response = await service.OnTopicEvent(request, Context());
        Assert.Equal(expected, response.Status);
    }

    [Fact]
    public async Task OnTopicEvent_ForwardsPubsubAndTopicToContext()
    {
        TopicContext? capturedCtx = null;
        var dispatcher = new FakeInterceptorDispatcher("pubsub", "orders", DeliveryMode.Programmatic,
            ctx => capturedCtx = ctx, TopicResponseAction.Success);
        var service = CreateService(new FakeRegistry(dispatcher));

        var request = new P.TopicEventRequest
        {
            PubsubName = "pubsub",
            Topic = "orders",
            Id = "msg-42",
        };
        request.Data = Google.Protobuf.ByteString.CopyFrom(System.Text.Encoding.UTF8.GetBytes("{}"));

        await service.OnTopicEvent(request, Context());

        Assert.NotNull(capturedCtx);
        Assert.Equal("pubsub", capturedCtx!.PubsubName);
        Assert.Equal("orders", capturedCtx.TopicName);
        Assert.Equal("msg-42", capturedCtx.MessageId);
    }

    [Fact]
    public async Task ListTopicSubscriptions_IncludesDeadLetterAndMetadata()
    {
        var descriptor = new TopicSubscriptionDescriptor
        {
            PubsubName = "pubsub",
            TopicName = "orders",
            Delivery = DeliveryMode.Programmatic,
            DeadLetterTopic = "orders-dlq",
            Metadata = new Dictionary<string, string> { ["rawPayload"] = "true" },
        };
        var registry = new FakeRegistry(new FakeDispatcher(descriptor));
        var service = CreateService(registry);

        var response = await service.ListTopicSubscriptions(new Google.Protobuf.WellKnownTypes.Empty(), Context());

        var sub = Assert.Single(response.Subscriptions);
        Assert.Equal("orders-dlq", sub.DeadLetterTopic);
        Assert.Contains(sub.Metadata, kvp => kvp.Key == "rawPayload" && kvp.Value == "true");
    }

    [Fact]
    public async Task OnBulkTopicEvent_AggregatesPerEntryStatuses()
    {
        var dispatcher = new FakeDispatcher("pubsub", "orders", DeliveryMode.Programmatic, TopicResponseAction.Retry);
        var service = CreateService(new FakeRegistry(dispatcher));

        var request = new P.TopicEventBulkRequest
        {
            PubsubName = "pubsub",
            Topic = "orders",
        };
        request.Entries.Add(new P.TopicEventBulkRequestEntry { EntryId = "e0" });
        request.Entries.Add(new P.TopicEventBulkRequestEntry { EntryId = "e1" });

        var response = await service.OnBulkTopicEvent(request, Context());

        Assert.Equal(2, response.Statuses.Count);
        Assert.All(response.Statuses, s => Assert.Equal(P.TopicEventResponse.Types.TopicEventResponseStatus.Retry, s.Status));
        Assert.Contains(response.Statuses, s => s.EntryId == "e0");
        Assert.Contains(response.Statuses, s => s.EntryId == "e1");
    }

    [Fact]
    public async Task OnBulkTopicEvent_NoDispatcher_ReturnsDropForEntries()
    {
        var service = CreateService(new FakeRegistry());

        var request = new P.TopicEventBulkRequest { PubsubName = "pubsub", Topic = "unknown" };
        request.Entries.Add(new P.TopicEventBulkRequestEntry { EntryId = "e0" });

        var response = await service.OnBulkTopicEvent(request, Context());

        var status = Assert.Single(response.Statuses);
        Assert.Equal(P.TopicEventResponse.Types.TopicEventResponseStatus.Drop, status.Status);
    }

    // -----------------------------------------------------------------------
    //  Fakes
    // -----------------------------------------------------------------------

    private sealed class FakeDispatcher : ITopicDispatcher
    {
        private readonly TopicResponseAction _result;

        public FakeDispatcher(string pubsub, string topic, DeliveryMode mode, TopicResponseAction result = TopicResponseAction.Success)
        {
            _result = result;
            Descriptor = new TopicSubscriptionDescriptor
            {
                PubsubName = pubsub,
                TopicName = topic,
                Delivery = mode,
            };
        }

        public FakeDispatcher(TopicSubscriptionDescriptor descriptor, TopicResponseAction result = TopicResponseAction.Success)
        {
            _result = result;
            Descriptor = descriptor;
        }

        public TopicSubscriptionDescriptor Descriptor { get; }
        public bool WasCalled { get; private set; }

        public Task<TopicResponseAction> DispatchAsync(byte[] payload, TopicContext context, IServiceProvider serviceProvider, CancellationToken ct)
        {
            WasCalled = true;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeInterceptorDispatcher : ITopicDispatcher
    {
        private readonly Action<TopicContext> _onDispatch;
        private readonly TopicResponseAction _result;

        public FakeInterceptorDispatcher(string pubsub, string topic, DeliveryMode mode, Action<TopicContext> onDispatch, TopicResponseAction result)
        {
            _onDispatch = onDispatch;
            _result = result;
            Descriptor = new TopicSubscriptionDescriptor { PubsubName = pubsub, TopicName = topic, Delivery = mode };
        }

        public TopicSubscriptionDescriptor Descriptor { get; }

        public Task<TopicResponseAction> DispatchAsync(byte[] payload, TopicContext context, IServiceProvider serviceProvider, CancellationToken ct)
        {
            _onDispatch(context);
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeRegistry : IDaprMessagingSubscriberRegistry
    {
        private readonly ITopicDispatcher[] _dispatchers;
        public FakeRegistry(params ITopicDispatcher[] dispatchers) => _dispatchers = dispatchers;
        public IReadOnlyList<TopicSubscriptionDescriptor> Descriptors => _dispatchers.Select(d => d.Descriptor).ToArray();
        public ITopicDispatcher? Resolve(string pubsubName, string topicName, DeliveryMode mode)
            => _dispatchers.FirstOrDefault(d => d.Descriptor.PubsubName == pubsubName && d.Descriptor.TopicName == topicName && d.Descriptor.Delivery == mode);
    }
}
