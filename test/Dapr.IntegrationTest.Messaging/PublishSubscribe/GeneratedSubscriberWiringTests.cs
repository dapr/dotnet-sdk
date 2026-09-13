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

using System.Linq;
using System.Threading.Tasks;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.Subscribe.AppCallback;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dapr.IntegrationTest.Messaging.PublishSubscribe;

/// <summary>
/// Verifies the end-to-end wiring produced by the Dapr.Messaging.Generators source generator:
/// AddDaprMessaging produces a registry whose descriptors flow into the AppCallback push service.
/// This is a no-sidecar wiring test; full end-to-end delivery against a real Dapr runtime is covered separately.
/// </summary>
public class GeneratedSubscriberWiringTests
{
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<HttpNotificationState>();
        services.AddSingleton<HttpBulkState>();
        services.AddDaprMessaging();
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void AddDaprMessaging_RegistersRegistryWithExpectedDescriptors()
    {
        using var provider = BuildServices();
        var registry = provider.GetRequiredService<IDaprMessagingSubscriberRegistry>();

        var descriptor = Assert.Single(registry.Descriptors, d => d.Delivery == DeliveryMode.Programmatic);
        Assert.Equal("pubsub", descriptor.PubsubName);
        Assert.Equal("integration-orders", descriptor.TopicName);
        Assert.Equal(DeliveryMode.Programmatic, descriptor.Delivery);
        Assert.Equal(typeof(IntegrationOrderHandler), descriptor.HandlerType);
        Assert.Equal(typeof(IntegrationOrder), descriptor.MessageType);

        var httpDesc = Assert.Single(registry.Descriptors, d => d.TopicName == "integration-http-notifications");
        Assert.Equal(DeliveryMode.Http, httpDesc.Delivery);
        Assert.Equal("api/v1/notifications", httpDesc.Route);

        var bulkDesc = Assert.Single(registry.Descriptors, d => d.TopicName == "integration-http-bulk-items");
        Assert.Equal(DeliveryMode.Http, bulkDesc.Delivery);
        Assert.Equal("api/v1/bulk-items", bulkDesc.Route);
        Assert.NotNull(bulkDesc.BulkSubscribe);
        Assert.True(bulkDesc.BulkSubscribe!.Enabled);
    }

    [Fact]
    public void Registry_ResolvesDispatcherForProgrammatic()
    {
        using var provider = BuildServices();
        var registry = provider.GetRequiredService<IDaprMessagingSubscriberRegistry>();

        var dispatcher = registry.Resolve("pubsub", "integration-orders", DeliveryMode.Programmatic);
        Assert.NotNull(dispatcher);
        Assert.Equal(DeliveryMode.Programmatic, dispatcher!.Descriptor.Delivery);
    }

    [Fact]
    public async Task AppCallbackService_ListTopicSubscriptionsIncludesGeneratedDescriptor()
    {
        await using var provider = BuildServices();
        var registry = provider.GetRequiredService<IDaprMessagingSubscriberRegistry>();
        var svc = new DaprAppCallbackService(
            registry,
            provider,
            NullLogger<DaprAppCallbackService>.Instance);

        var response = await svc.ListTopicSubscriptions(
            new Google.Protobuf.WellKnownTypes.Empty(),
            new Moq.Mock<Grpc.Core.ServerCallContext>().Object);

        var sub = Assert.Single(response.Subscriptions);
        Assert.Equal("pubsub", sub.PubsubName);
        Assert.Equal("integration-orders", sub.Topic);
    }

    [Fact]
    public async Task GeneratedDispatcher_DeserializesPayloadAndInvokesHandler()
    {
        await using var provider = BuildServices();
        var registry = provider.GetRequiredService<IDaprMessagingSubscriberRegistry>();
        var dispatcher = registry.Resolve("pubsub", "integration-orders", DeliveryMode.Programmatic);
        Assert.NotNull(dispatcher);

        var order = new IntegrationOrder("order-99", 42);
        var payload = System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(order));

        // The handler returns Success for any message. Register a scope that can resolve the handler.
        using var scope = provider.CreateScope();
        var action = await dispatcher!.DispatchAsync(payload, new TopicContext(), scope.ServiceProvider, CancellationToken.None);
        Assert.Equal(TopicResponseAction.Success, action);
    }

    [Fact]
    public void Registry_ResolveReturnsNullForUnknownTopic()
    {
        using var provider = BuildServices();
        var registry = provider.GetRequiredService<IDaprMessagingSubscriberRegistry>();

        Assert.Null(registry.Resolve("pubsub", "nonexistent", DeliveryMode.Programmatic));
    }

    [Fact]
    public void Registry_ResolveReturnsNullForWrongDeliveryMode()
    {
        using var provider = BuildServices();
        var registry = provider.GetRequiredService<IDaprMessagingSubscriberRegistry>();

        // The handler is registered as Programmatic; resolving for Streaming must fail.
        Assert.Null(registry.Resolve("pubsub", "integration-orders", DeliveryMode.Streaming));
    }

    [Fact]
    public async Task AppCallbackService_OnTopicEvent_DispatchesGeneratedHandler()
    {
        await using var provider = BuildServices();
        var registry = provider.GetRequiredService<IDaprMessagingSubscriberRegistry>();
        var svc = new DaprAppCallbackService(
            registry, provider, NullLogger<DaprAppCallbackService>.Instance);

        var order = new IntegrationOrder("order-77", 10);
        var request = new Dapr.AppCallback.Autogen.Grpc.v1.TopicEventRequest
        {
            PubsubName = "pubsub",
            Topic = "integration-orders",
            Id = "msg-1",
        };
        request.Data = Google.Protobuf.ByteString.CopyFrom(
            System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(order)));

        var response = await svc.OnTopicEvent(request, new Moq.Mock<Grpc.Core.ServerCallContext>().Object);

        Assert.Equal(Dapr.AppCallback.Autogen.Grpc.v1.TopicEventResponse.Types.TopicEventResponseStatus.Success, response.Status);
    }
}
