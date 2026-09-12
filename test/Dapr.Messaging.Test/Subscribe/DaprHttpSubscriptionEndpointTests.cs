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

using System.IO;
using System.Text;
using System.Text.Json;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.Subscribe.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Messaging.Test.Subscribe;

public class DaprHttpSubscriptionEndpointTests
{
    private static DefaultHttpContext CreateHttpContext(IDaprMessagingSubscriberRegistry registry, string? requestBody = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(registry);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = provider,
            Response = { Body = new MemoryStream() }
        };

        if (requestBody is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(requestBody);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = bytes.Length;
        }

        return context;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    // -----------------------------------------------------------------------
    //  HandleDiscoveryAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleDiscoveryAsync_OnlyReturnsHttpDeliveryModeDescriptors()
    {
        var registry = new FakeRegistry(
            new FakeDispatcher("pubsub", "http-orders", DeliveryMode.Http, route: "orders"),
            new FakeDispatcher("pubsub", "grpc-orders", DeliveryMode.Programmatic),
            new FakeDispatcher("pubsub", "streaming-orders", DeliveryMode.Streaming));

        var context = CreateHttpContext(registry);
        await DaprHttpSubscriptionEndpoint.HandleDiscoveryAsync(context);

        var body = await ReadResponseBodyAsync(context);
        using var doc = JsonDocument.Parse(body);
        var array = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, array.ValueKind);
        Assert.Equal(1, array.GetArrayLength());

        var sub = array[0];
        Assert.Equal("pubsub", sub.GetProperty("pubsubname").GetString());
        Assert.Equal("http-orders", sub.GetProperty("topic").GetString());
        Assert.Equal("orders", sub.GetProperty("route").GetString());
    }

    [Fact]
    public async Task HandleDiscoveryAsync_FormatsRoutesAndMetadataCorrectly()
    {
        var descriptor = new TopicSubscriptionDescriptor
        {
            PubsubName = "pubsub",
            TopicName = "orders",
            Route = "/api/v1/orders",
            Delivery = DeliveryMode.Http,
            DeadLetterTopic = "orders-dlq",
            Metadata = new Dictionary<string, string> { ["rawPayload"] = "true" }
        };
        var registry = new FakeRegistry(new FakeDispatcher(descriptor));

        var context = CreateHttpContext(registry);
        await DaprHttpSubscriptionEndpoint.HandleDiscoveryAsync(context);

        var body = await ReadResponseBodyAsync(context);
        using var doc = JsonDocument.Parse(body);
        var sub = doc.RootElement[0];

        Assert.Equal("api/v1/orders", sub.GetProperty("route").GetString());
        Assert.Equal("orders-dlq", sub.GetProperty("deadLetterTopic").GetString());
        Assert.Equal("true", sub.GetProperty("metadata").GetProperty("rawPayload").GetString());
    }

    [Fact]
    public async Task HandleDiscoveryAsync_WithRoutingRule_FormatsRulesAndDefault()
    {
        var descriptor = new TopicSubscriptionDescriptor
        {
            PubsubName = "pubsub",
            TopicName = "orders",
            Route = "orders",
            Delivery = DeliveryMode.Http,
            Match = "event.type == \"v2\""
        };
        var registry = new FakeRegistry(new FakeDispatcher(descriptor));

        var context = CreateHttpContext(registry);
        await DaprHttpSubscriptionEndpoint.HandleDiscoveryAsync(context);

        var body = await ReadResponseBodyAsync(context);
        using var doc = JsonDocument.Parse(body);
        var routes = doc.RootElement[0].GetProperty("routes");

        Assert.Equal("orders", routes.GetProperty("default").GetString());
        var rule = routes.GetProperty("rules")[0];
        Assert.Equal("event.type == \"v2\"", rule.GetProperty("match").GetString());
        Assert.Equal("orders", rule.GetProperty("path").GetString());
    }

    [Fact]
    public async Task HandleDiscoveryAsync_WithBulkSubscribe_FormatsBulkSubscribeOptions()
    {
        var descriptor = new TopicSubscriptionDescriptor
        {
            PubsubName = "pubsub",
            TopicName = "orders",
            Route = "orders",
            Delivery = DeliveryMode.Http,
            BulkSubscribe = new BulkSubscribeOptions
            {
                Enabled = true,
                MaxMessagesCount = 50,
                MaxAwaitDurationMs = 250
            }
        };
        var registry = new FakeRegistry(new FakeDispatcher(descriptor));

        var context = CreateHttpContext(registry);
        await DaprHttpSubscriptionEndpoint.HandleDiscoveryAsync(context);

        var body = await ReadResponseBodyAsync(context);
        using var doc = JsonDocument.Parse(body);
        var bulk = doc.RootElement[0].GetProperty("bulkSubscribe");

        Assert.True(bulk.GetProperty("enabled").GetBoolean());
        Assert.Equal(50, bulk.GetProperty("maxMessagesCount").GetInt32());
        Assert.Equal(250, bulk.GetProperty("maxAwaitDurationMs").GetInt32());
    }

    [Fact]
    public async Task HandleDiscoveryAsync_WithRawPayload_AddsRawPayloadMetadata()
    {
        var descriptor = new TopicSubscriptionDescriptor
        {
            PubsubName = "pubsub",
            TopicName = "orders",
            Route = "orders",
            Delivery = DeliveryMode.Http,
            EnableRawPayload = true
        };
        var registry = new FakeRegistry(new FakeDispatcher(descriptor));

        var context = CreateHttpContext(registry);
        await DaprHttpSubscriptionEndpoint.HandleDiscoveryAsync(context);

        var body = await ReadResponseBodyAsync(context);
        using var doc = JsonDocument.Parse(body);
        var metadata = doc.RootElement[0].GetProperty("metadata");

        Assert.Equal("true", metadata.GetProperty("rawPayload").GetString());
    }

    [Fact]
    public async Task HandleDiscoveryAsync_EmptyRegistry_ReturnsEmptyArray()
    {
        var registry = new FakeRegistry();
        var context = CreateHttpContext(registry);

        await DaprHttpSubscriptionEndpoint.HandleDiscoveryAsync(context);

        var body = await ReadResponseBodyAsync(context);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    // -----------------------------------------------------------------------
    //  HandleEventAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleEventAsync_CloudEventPayload_ExtractsDataAndDispatches()
    {
        byte[]? receivedPayload = null;
        var dispatcher = new FakeInterceptorDispatcher("pubsub", "orders", DeliveryMode.Http,
            (bytes, ctx) => receivedPayload = bytes, TopicResponseAction.Success);
        var registry = new FakeRegistry(dispatcher);

        var json = """
        {
            "id": "evt-123",
            "source": "/app",
            "type": "order.created",
            "data": { "orderId": "ord-99" }
        }
        """;

        var context = CreateHttpContext(registry, json);
        await DaprHttpSubscriptionEndpoint.HandleEventAsync(context, dispatcher.Descriptor);

        Assert.True(dispatcher.WasCalled);
        Assert.NotNull(receivedPayload);
        var str = Encoding.UTF8.GetString(receivedPayload!);
        Assert.Contains("ord-99", str);

        var body = await ReadResponseBodyAsync(context);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("SUCCESS", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HandleEventAsync_Base64Payload_DecodesAndDispatches()
    {
        byte[]? receivedPayload = null;
        var dispatcher = new FakeInterceptorDispatcher("pubsub", "orders", DeliveryMode.Http,
            (bytes, ctx) => receivedPayload = bytes, TopicResponseAction.Success);
        var registry = new FakeRegistry(dispatcher);

        var original = Encoding.UTF8.GetBytes("hello-binary-world");
        var base64 = Convert.ToBase64String(original);
        var json = $$"""
        {
            "id": "evt-456",
            "data_base64": "{{base64}}"
        }
        """;

        var context = CreateHttpContext(registry, json);
        await DaprHttpSubscriptionEndpoint.HandleEventAsync(context, dispatcher.Descriptor);

        Assert.NotNull(receivedPayload);
        Assert.Equal(original, receivedPayload);
    }

    [Fact]
    public async Task HandleEventAsync_RawPayload_DispatchesDirectly()
    {
        byte[]? receivedPayload = null;
        var dispatcher = new FakeInterceptorDispatcher("pubsub", "orders", DeliveryMode.Http,
            (bytes, ctx) => receivedPayload = bytes, TopicResponseAction.Success);
        var registry = new FakeRegistry(dispatcher);

        var json = """{"unwrappedKey": "unwrappedValue"}""";

        var context = CreateHttpContext(registry, json);
        await DaprHttpSubscriptionEndpoint.HandleEventAsync(context, dispatcher.Descriptor);

        Assert.NotNull(receivedPayload);
        var str = Encoding.UTF8.GetString(receivedPayload!);
        Assert.Contains("unwrappedKey", str);
    }

    [Theory]
    [InlineData(TopicResponseAction.Success, "SUCCESS")]
    [InlineData(TopicResponseAction.Retry, "RETRY")]
    [InlineData(TopicResponseAction.Drop, "DROP")]
    public async Task HandleEventAsync_MapsTopicResponseActionToStatus(TopicResponseAction action, string expectedStatus)
    {
        var dispatcher = new FakeDispatcher("pubsub", "orders", DeliveryMode.Http, action, route: "orders");
        var registry = new FakeRegistry(dispatcher);

        var context = CreateHttpContext(registry, """{"data": {}}""");
        await DaprHttpSubscriptionEndpoint.HandleEventAsync(context, dispatcher.Descriptor);

        var body = await ReadResponseBodyAsync(context);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(expectedStatus, doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HandleEventAsync_MissingDispatcher_ReturnsNotFound()
    {
        var registry = new FakeRegistry();
        var descriptor = new TopicSubscriptionDescriptor
        {
            PubsubName = "pubsub",
            TopicName = "unknown",
            Delivery = DeliveryMode.Http,
            Route = "unknown"
        };

        var context = CreateHttpContext(registry, """{"data": {}}""");
        await DaprHttpSubscriptionEndpoint.HandleEventAsync(context, descriptor);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleEventAsync_PopulatesTopicContextWithHeadersAndIds()
    {
        TopicContext? capturedCtx = null;
        var dispatcher = new FakeInterceptorDispatcher("pubsub", "orders", DeliveryMode.Http,
            (bytes, ctx) => capturedCtx = ctx, TopicResponseAction.Success);
        var registry = new FakeRegistry(dispatcher);

        var json = """
        {
            "id": "msg-custom-id",
            "source": "my-source",
            "type": "my-type",
            "specversion": "1.0",
            "datacontenttype": "application/json",
            "topic": "orders",
            "pubsubname": "pubsub",
            "data": "content"
        }
        """;

        var context = CreateHttpContext(registry, json);
        await DaprHttpSubscriptionEndpoint.HandleEventAsync(context, dispatcher.Descriptor);

        Assert.NotNull(capturedCtx);
        Assert.Equal("msg-custom-id", capturedCtx!.MessageId);
        Assert.Equal("pubsub", capturedCtx.PubsubName);
        Assert.Equal("orders", capturedCtx.TopicName);
        Assert.Equal("my-source", capturedCtx.Headers["source"]);
        Assert.Equal("my-type", capturedCtx.Headers["type"]);
        Assert.Equal("1.0", capturedCtx.Headers["specversion"]);
        Assert.Equal("application/json", capturedCtx.Headers["datacontenttype"]);
    }

    [Fact]
    public async Task HandleEventAsync_BulkSubscribe_DispatchesEachEntryAndAggregatesStatuses()
    {
        var processedEntries = new List<string>();
        var dispatcher = new FakeInterceptorDispatcher("pubsub", "bulk-orders", DeliveryMode.Http,
            (bytes, ctx) => processedEntries.Add(ctx.MessageId), TopicResponseAction.Success,
            new BulkSubscribeOptions { Enabled = true });
        var registry = new FakeRegistry(dispatcher);

        var json = """
        {
            "entries": [
                { "entryId": "entry-1", "data": { "item": "apple" } },
                { "entryId": "entry-2", "data": { "item": "banana" } },
                { "entryId": "entry-3", "data": { "item": "cherry" } }
            ]
        }
        """;

        var context = CreateHttpContext(registry, json);
        await DaprHttpSubscriptionEndpoint.HandleEventAsync(context, dispatcher.Descriptor);

        Assert.Equal(3, processedEntries.Count);
        Assert.Contains("entry-1", processedEntries);
        Assert.Contains("entry-2", processedEntries);
        Assert.Contains("entry-3", processedEntries);

        var body = await ReadResponseBodyAsync(context);
        using var doc = JsonDocument.Parse(body);
        var statuses = doc.RootElement.GetProperty("statuses");
        Assert.Equal(3, statuses.GetArrayLength());

        foreach (var statusElement in statuses.EnumerateArray())
        {
            Assert.Equal("SUCCESS", statusElement.GetProperty("status").GetString());
        }
    }

    // -----------------------------------------------------------------------
    //  Fakes
    // -----------------------------------------------------------------------

    private sealed class FakeDispatcher : ITopicDispatcher
    {
        private readonly TopicResponseAction _result;

        public FakeDispatcher(string pubsub, string topic, DeliveryMode mode, TopicResponseAction result = TopicResponseAction.Success, string? route = null)
        {
            _result = result;
            Descriptor = new TopicSubscriptionDescriptor
            {
                PubsubName = pubsub,
                TopicName = topic,
                Delivery = mode,
                Route = route ?? topic
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
        private readonly Action<byte[], TopicContext> _onDispatch;
        private readonly TopicResponseAction _result;

        public FakeInterceptorDispatcher(string pubsub, string topic, DeliveryMode mode, Action<byte[], TopicContext> onDispatch, TopicResponseAction result, BulkSubscribeOptions? bulk = null)
        {
            _onDispatch = onDispatch;
            _result = result;
            Descriptor = new TopicSubscriptionDescriptor
            {
                PubsubName = pubsub,
                TopicName = topic,
                Delivery = mode,
                Route = topic,
                BulkSubscribe = bulk
            };
        }

        public TopicSubscriptionDescriptor Descriptor { get; }
        public bool WasCalled { get; private set; }

        public Task<TopicResponseAction> DispatchAsync(byte[] payload, TopicContext context, IServiceProvider serviceProvider, CancellationToken ct)
        {
            WasCalled = true;
            _onDispatch(payload, context);
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
