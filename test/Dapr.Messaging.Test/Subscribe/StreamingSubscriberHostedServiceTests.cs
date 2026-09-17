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
using Dapr.Messaging.Subscribe.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dapr.Messaging.Test.Subscribe;

/// <summary>
/// Unit tests for <see cref="StreamingSubscriberHostedService"/>: verifies descriptor filtering,
/// dispatch/DI-scoping, ack translation, reconnect-on-fault behavior, and clean shutdown.
/// </summary>
public class StreamingSubscriberHostedServiceTests
{
    private static TopicSubscriptionDescriptor StreamingDescriptor(string pubsub = "pubsub", string topic = "orders") =>
        new()
        {
            PubsubName = pubsub,
            TopicName = topic,
            Delivery = DeliveryMode.Streaming
        };

    private static IServiceProvider BuildServiceProvider() =>
        new ServiceCollection().BuildServiceProvider();

    private static IOptions<DaprMessagingOptions> Options(TimeSpan? reconnectDelay = null) =>
        Microsoft.Extensions.Options.Options.Create(new DaprMessagingOptions
        {
            StreamingReconnectDelay = reconnectDelay ?? TimeSpan.FromMilliseconds(20)
        });

    [Fact]
    public async Task StartAsync_WithNoStreamingDescriptors_DoesNotSubscribe()
    {
        var registry = new FakeRegistry(new TopicSubscriptionDescriptor
        {
            PubsubName = "pubsub",
            TopicName = "http-orders",
            Delivery = DeliveryMode.Http
        });
        var client = new FakeSubscribeClient();
        var service = new StreamingSubscriberHostedService(
            registry, client, BuildServiceProvider(), Options(), NullLogger<StreamingSubscriberHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(50, CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Empty(client.SubscribeCalls);
    }

    [Fact]
    public async Task StartAsync_WithStreamingDescriptor_SubscribesToMatchingPubsubAndTopic()
    {
        var descriptor = StreamingDescriptor();
        var registry = new FakeRegistry(descriptor);
        var client = new FakeSubscribeClient();
        var service = new StreamingSubscriberHostedService(
            registry, client, BuildServiceProvider(), Options(), NullLogger<StreamingSubscriberHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => client.SubscribeCalls.Count > 0);
        await service.StopAsync(CancellationToken.None);

        var call = Assert.Single(client.SubscribeCalls);
        Assert.Equal("pubsub", call.PubSubName);
        Assert.Equal("orders", call.TopicName);
    }

    [Fact]
    public async Task StartAsync_WithMultipleStreamingDescriptors_SubscribesToEach()
    {
        var registry = new FakeRegistry(
            StreamingDescriptor("pubsub", "orders"),
            StreamingDescriptor("pubsub", "shipments"));
        var client = new FakeSubscribeClient();
        var service = new StreamingSubscriberHostedService(
            registry, client, BuildServiceProvider(), Options(), NullLogger<StreamingSubscriberHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => client.SubscribeCalls.Count >= 2);
        await service.StopAsync(CancellationToken.None);

        Assert.Contains(client.SubscribeCalls, c => c.TopicName == "orders");
        Assert.Contains(client.SubscribeCalls, c => c.TopicName == "shipments");
    }

    [Fact]
    public async Task Dispatch_ResolvesHandlerAndReturnsItsResponseAction()
    {
        var descriptor = StreamingDescriptor();
        var dispatcher = new FakeDispatcher(descriptor, TopicResponseAction.Success);
        var registry = new FakeRegistry(dispatcher, descriptor);
        var client = new FakeSubscribeClient();
        var service = new StreamingSubscriberHostedService(
            registry, client, BuildServiceProvider(), Options(), NullLogger<StreamingSubscriberHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => client.SubscribeCalls.Count > 0);

        var message = new TopicMessage("id-1", "src", "type", "1.0", "application/json", "orders", "pubsub")
        {
            Data = new ReadOnlyMemory<byte>("\"payload\""u8.ToArray())
        };
        var result = await client.SubscribeCalls[0].Handler(message, CancellationToken.None);

        await service.StopAsync(CancellationToken.None);

        Assert.Equal(TopicResponseAction.Success, result);
        Assert.Single(dispatcher.Invocations);
        Assert.Equal("id-1", dispatcher.Invocations[0].Context.MessageId);
        Assert.Equal("orders", dispatcher.Invocations[0].Context.TopicName);
        Assert.Equal("pubsub", dispatcher.Invocations[0].Context.PubsubName);
    }

    [Fact]
    public async Task Dispatch_WithNoMatchingDispatcher_DropsMessage()
    {
        var descriptor = StreamingDescriptor();
        // Registry has the descriptor but Resolve() returns null (no handler registered).
        var registry = new FakeRegistry(descriptor);
        var client = new FakeSubscribeClient();
        var service = new StreamingSubscriberHostedService(
            registry, client, BuildServiceProvider(), Options(), NullLogger<StreamingSubscriberHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => client.SubscribeCalls.Count > 0);

        var message = new TopicMessage("id-1", "src", "type", "1.0", "application/json", "orders", "pubsub");
        var result = await client.SubscribeCalls[0].Handler(message, CancellationToken.None);

        await service.StopAsync(CancellationToken.None);

        Assert.Equal(TopicResponseAction.Drop, result);
    }

    [Fact]
    public async Task Dispatch_CreatesAScopePerInvocation()
    {
        var descriptor = StreamingDescriptor();
        var dispatcher = new FakeDispatcher(descriptor, TopicResponseAction.Success);
        var registry = new FakeRegistry(dispatcher, descriptor);
        var client = new FakeSubscribeClient();

        var services = new ServiceCollection();
        services.AddScoped<ScopeMarker>();
        var provider = services.BuildServiceProvider();

        var service = new StreamingSubscriberHostedService(
            registry, client, provider, Options(), NullLogger<StreamingSubscriberHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => client.SubscribeCalls.Count > 0);

        var message = new TopicMessage("id-1", "src", "type", "1.0", "application/json", "orders", "pubsub");
        await client.SubscribeCalls[0].Handler(message, CancellationToken.None);
        await client.SubscribeCalls[0].Handler(message, CancellationToken.None);

        await service.StopAsync(CancellationToken.None);

        Assert.Equal(2, dispatcher.Invocations.Count);
        Assert.Equal(2, dispatcher.ResolvedScopedServices.Count);
        Assert.NotSame(dispatcher.ResolvedScopedServices[0], dispatcher.ResolvedScopedServices[1]);
    }

    [Fact]
    public async Task SubscriptionFault_ReconnectsAfterDelay()
    {
        var descriptor = StreamingDescriptor();
        var registry = new FakeRegistry(descriptor);
        var client = new FakeSubscribeClient(faultFirstSubscription: true);
        var service = new StreamingSubscriberHostedService(
            registry, client, BuildServiceProvider(), Options(TimeSpan.FromMilliseconds(10)),
            NullLogger<StreamingSubscriberHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);

        // First subscription faults; the service should reconnect and subscribe a second time.
        await WaitForAsync(() => client.SubscribeCalls.Count >= 2, timeout: TimeSpan.FromSeconds(5));

        await service.StopAsync(CancellationToken.None);

        Assert.True(client.SubscribeCalls.Count >= 2);
    }

    [Fact]
    public async Task StopAsync_DisposesAllActiveSubscriptions()
    {
        var registry = new FakeRegistry(
            StreamingDescriptor("pubsub", "orders"),
            StreamingDescriptor("pubsub", "shipments"));
        var client = new FakeSubscribeClient();
        var service = new StreamingSubscriberHostedService(
            registry, client, BuildServiceProvider(), Options(), NullLogger<StreamingSubscriberHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await WaitForAsync(() => client.SubscribeCalls.Count >= 2);

        await service.StopAsync(CancellationToken.None);

        Assert.All(client.CreatedSubscriptions, s => Assert.True(s.Disposed));
    }

    [Fact]
    public async Task StopAsync_WithoutStartAsync_DoesNotThrow()
    {
        var registry = new FakeRegistry();
        var client = new FakeSubscribeClient();
        var service = new StreamingSubscriberHostedService(
            registry, client, BuildServiceProvider(), Options(), NullLogger<StreamingSubscriberHostedService>.Instance);

        await service.StopAsync(CancellationToken.None);
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(2));
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10, CancellationToken.None);
        }

        Assert.True(condition(), "Condition was not met within the timeout.");
    }

    private sealed class ScopeMarker;

    private sealed class FakeRegistry : IDaprMessagingSubscriberRegistry
    {
        private readonly ITopicDispatcher? _dispatcher;

        public FakeRegistry(params TopicSubscriptionDescriptor[] descriptors) : this(null, descriptors)
        {
        }

        public FakeRegistry(ITopicDispatcher? dispatcher, params TopicSubscriptionDescriptor[] descriptors)
        {
            _dispatcher = dispatcher;
            Descriptors = descriptors;
        }

        public IReadOnlyList<TopicSubscriptionDescriptor> Descriptors { get; }

        public ITopicDispatcher? Resolve(string pubsubName, string topicName, DeliveryMode mode)
        {
            if (_dispatcher is null)
            {
                return null;
            }

            return _dispatcher.Descriptor.PubsubName == pubsubName &&
                   _dispatcher.Descriptor.TopicName == topicName &&
                   _dispatcher.Descriptor.Delivery == mode
                ? _dispatcher
                : null;
        }
    }

    private sealed class FakeDispatcher(TopicSubscriptionDescriptor descriptor, TopicResponseAction response) : ITopicDispatcher
    {
        public TopicSubscriptionDescriptor Descriptor { get; } = descriptor;
        public List<(byte[] Payload, TopicContext Context, IServiceProvider ServiceProvider)> Invocations { get; } = [];
        public List<object> ResolvedScopedServices { get; } = [];

        public Task<TopicResponseAction> DispatchAsync(byte[] payload, TopicContext context, IServiceProvider serviceProvider, CancellationToken ct)
        {
            Invocations.Add((payload, context, serviceProvider));

            // Resolve any scoped services eagerly, since the scope is disposed once DispatchAsync
            // returns (mirroring how the real dispatcher must resolve handlers synchronously within
            // the dispatch call).
            var marker = serviceProvider.GetService(typeof(ScopeMarker));
            if (marker is not null)
            {
                ResolvedScopedServices.Add(marker);
            }

            return Task.FromResult(response);
        }
    }

    private sealed record SubscribeCall(string PubSubName, string TopicName, TopicMessageHandler Handler);

    private sealed class FakeSubscription : IDaprSubscription
    {
        private readonly TaskCompletionSource _completionSource = new();

        public bool Disposed { get; private set; }
        public Task Completion => _completionSource.Task;

        public void Fault(Exception ex) => _completionSource.TrySetException(ex);

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            _completionSource.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// A minimal fake standing in for <see cref="DaprPublishSubscribeClient"/> that records
    /// subscription requests instead of opening a real gRPC stream.
    /// </summary>
    private sealed class FakeSubscribeClient(bool faultFirstSubscription = false) : DaprPublishSubscribeClient(
        null!, new HttpClient(), new System.Text.Json.JsonSerializerOptions())
    {
        private int _subscribeCount;

        public List<SubscribeCall> SubscribeCalls { get; } = [];
        public List<FakeSubscription> CreatedSubscriptions { get; } = [];

        public override Task<IAsyncDisposable> SubscribeAsync(
            string pubSubName, string topicName, DaprSubscriptionOptions options, TopicMessageHandler messageHandler,
            CancellationToken cancellationToken = default)
        {
            SubscribeCalls.Add(new SubscribeCall(pubSubName, topicName, messageHandler));

            var subscription = new FakeSubscription();
            CreatedSubscriptions.Add(subscription);

            var attempt = Interlocked.Increment(ref _subscribeCount);
            if (faultFirstSubscription && attempt == 1)
            {
                subscription.Fault(new DaprException("simulated fault"));
            }

            return Task.FromResult<IAsyncDisposable>(subscription);
        }

        public override Task PublishEventAsync<TData>(string pubsubName, string topicName, TData data, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override Task PublishEventAsync<TData>(string pubsubName, string topicName, TData data, PublishOptions options, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override Task PublishEventAsync(string pubsubName, string topicName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override Task PublishByteEventAsync(string pubsubName, string topicName, ReadOnlyMemory<byte> data, string dataContentType = "application/json", PublishOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override Task<BulkPublishResponse<TValue>> BulkPublishEventAsync<TValue>(string pubsubName, string topicName, IReadOnlyList<TValue> events, PublishOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
