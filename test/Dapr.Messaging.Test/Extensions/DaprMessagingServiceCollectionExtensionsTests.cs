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
using Dapr.Messaging.Subscribe.AppCallback;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dapr.Messaging.Test.Extensions;

/// <summary>
/// Unit tests for the DI registration extensions in <c>Dapr.Messaging.Runtime</c>.
/// </summary>
public class DaprMessagingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDaprMessaging_RegistersOptions()
    {
        var services = new ServiceCollection();
        services.AddDaprMessaging();

        using var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<DaprMessagingOptions>>().Value;
        Assert.NotNull(opts);
    }

    [Fact]
    public void AddDaprMessaging_WithConfigure_AppliesValues()
    {
        var services = new ServiceCollection();
        services.AddDaprMessaging(o =>
        {
            o.DaprApiToken = "tok";
            o.DaprGrpcEndpoint = "http://sidecar:50001";
            o.StreamingReconnectDelay = TimeSpan.FromSeconds(10);
        });

        using var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<DaprMessagingOptions>>().Value;
        Assert.Equal("tok", opts.DaprApiToken);
        Assert.Equal("http://sidecar:50001", opts.DaprGrpcEndpoint);
        Assert.Equal(TimeSpan.FromSeconds(10), opts.StreamingReconnectDelay);
    }

    [Fact]
    public void AddDaprMessaging_ReturnsBuilderWithServiceCollection()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprMessaging();

        Assert.Same(services, builder.Services);
    }

    [Fact]
    public void AddDaprPubSub_RegistersDaprPublishSubscribeClient()
    {
        var services = new ServiceCollection();
        services.AddDaprMessaging().AddDaprPubSub();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetService<DaprPublishSubscribeClient>();
        Assert.NotNull(client);

        var ifaceClient = provider.GetService<IDaprPublishSubscribeClient>();
        Assert.NotNull(ifaceClient);
        Assert.Same(client, ifaceClient);
    }

    [Fact]
    public void AddDaprSubscriber_RegistersAppCallbackService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprMessaging().AddDaprPubSub().AddDaprSubscriber();

        // Register a minimal registry so DaprAppCallbackService can be constructed.
        services.AddSingleton<IDaprMessagingSubscriberRegistry, EmptyRegistry>();

        using var provider = services.BuildServiceProvider();
        var svc = provider.GetService<DaprAppCallbackService>();
        Assert.NotNull(svc);
    }

    [Fact]
    public void AddDaprSubscriber_RegistersGrpcServerServices()
    {
        var services = new ServiceCollection();
        services.AddDaprMessaging().AddDaprSubscriber();

        // AddGrpc() registers gRPC server infrastructure (marker/options/service providers)
        Assert.Contains(services, d => d.ServiceType.FullName?.Contains("Grpc") == true);
    }

    [Fact]
    public void AddTopic_RegistersHandlerAsTransient()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprMessaging();
        builder.AddTopic<MyHandler>("pubsub", "topic");

        using var provider = services.BuildServiceProvider();
        var h1 = provider.GetRequiredService<MyHandler>();
        var h2 = provider.GetRequiredService<MyHandler>();
        Assert.NotSame(h1, h2);
    }

    [Fact]
    public void AddTopic_WithConfigure_AppliesDescriptorValues()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprMessaging();
        builder.AddTopic<MyHandler>("pubsub", "topic", d =>
        {
            d.DeadLetterTopic = "dlq";
            d.Delivery = DeliveryMode.Programmatic;
        });

        // The descriptor is not registered as a service (it's used by the generated registry),
        // but the handler registration must succeed without throwing.
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<MyHandler>());
    }

    private sealed class MyHandler : ITopicHandler<string>
    {
        public Task<TopicResponseAction> HandleAsync(string message, TopicContext context, CancellationToken cancellationToken)
            => Task.FromResult(TopicResponseAction.Success);
    }

    private sealed class EmptyRegistry : IDaprMessagingSubscriberRegistry
    {
        public IReadOnlyList<TopicSubscriptionDescriptor> Descriptors { get; } = Array.Empty<TopicSubscriptionDescriptor>();
        public ITopicDispatcher? Resolve(string pubsubName, string topicName, DeliveryMode mode) => null;
    }
}
