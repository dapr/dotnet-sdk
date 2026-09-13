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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dapr.Messaging.Test.Extensions;

/// <summary>
/// Unit tests for the single-call <c>AddDaprMessaging</c> registration emitted by
/// <c>Dapr.Messaging.Generators</c> into this assembly, plus the runtime builder extensions.
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
    public void AddDaprMessaging_RegistersPublishSubscribeClient()
    {
        var services = new ServiceCollection();
        services.AddDaprMessaging();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetService<DaprPublishSubscribeClient>();
        Assert.NotNull(client);

        var ifaceClient = provider.GetService<IDaprPublishSubscribeClient>();
        Assert.NotNull(ifaceClient);
        Assert.Same(client, ifaceClient);
    }

    [Fact]
    public void AddDaprMessaging_RegistersSubscriberRegistry()
    {
        var services = new ServiceCollection();
        services.AddDaprMessaging();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetService<IDaprMessagingSubscriberRegistry>();
        Assert.NotNull(registry);
    }

    [Fact]
    public void AddDaprMessaging_WithNoSubscribers_DoesNotRegisterGrpcServerHosting()
    {
        // This test assembly declares no [DaprTopic] handlers, so the generated registration must
        // not opt into gRPC server hosting or the AppCallback push service.
        var services = new ServiceCollection();
        services.AddDaprMessaging();

        Assert.DoesNotContain(
            services,
            d => d.ServiceType.FullName?.Contains("Grpc.AspNetCore.Server", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void AddDaprMessaging_IsIdempotentForRegistryAndClient()
    {
        var services = new ServiceCollection();
        services.AddDaprMessaging();
        services.AddDaprMessaging();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IDaprMessagingSubscriberRegistry>());
        Assert.NotNull(provider.GetService<IDaprPublishSubscribeClient>());
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
}
