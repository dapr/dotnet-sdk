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
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Messaging.Test.Subscribe;

public class DaprMessagingEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public void MapDaprMessaging_NullEndpoints_ThrowsArgumentNullException()
    {
        IEndpointRouteBuilder endpoints = null!;
        Assert.Throws<ArgumentNullException>(() => endpoints.MapDaprMessaging());
    }

    [Fact]
    public void MapDaprMessaging_NoSubscribers_MapsNothing()
    {
        var services = new ServiceCollection();
        var registry = new FakeRegistry();
        services.AddLogging();
        services.AddSingleton<IDaprMessagingSubscriberRegistry>(registry);
        services.AddRouting();
        services.AddGrpc();

        var app = new DefaultEndpointRouteBuilder(services.BuildServiceProvider());
        var result = app.MapDaprMessaging();

        Assert.Same(app, result);
        Assert.Empty(app.DataSources.SelectMany(ds => ds.Endpoints));
    }

    [Fact]
    public void MapDaprMessaging_OnlyStreaming_MapsNothing()
    {
        var services = new ServiceCollection();
        var registry = new FakeRegistry(new TopicSubscriptionDescriptor
        {
            PubsubName = "pubsub",
            TopicName = "streaming-orders",
            Route = "streaming-orders",
            Delivery = DeliveryMode.Streaming
        });
        services.AddLogging();
        services.AddSingleton<IDaprMessagingSubscriberRegistry>(registry);
        services.AddRouting();
        services.AddGrpc();

        var app = new DefaultEndpointRouteBuilder(services.BuildServiceProvider());
        var result = app.MapDaprMessaging();

        Assert.Same(app, result);
        Assert.Empty(app.DataSources.SelectMany(ds => ds.Endpoints));
    }

    [Fact]
    public void MapDaprMessaging_OnlyHttp_MapsHttpEndpoints()
    {
        var services = new ServiceCollection();
        var registry = new FakeRegistry(new TopicSubscriptionDescriptor
        {
            PubsubName = "pubsub",
            TopicName = "http-orders",
            Route = "api/orders",
            Delivery = DeliveryMode.Http
        });
        services.AddLogging();
        services.AddSingleton<IDaprMessagingSubscriberRegistry>(registry);
        services.AddRouting();
        services.AddGrpc();

        var app = new DefaultEndpointRouteBuilder(services.BuildServiceProvider());
        var result = app.MapDaprMessaging();

        Assert.Same(app, result);
        var endpoints = app.DataSources.SelectMany(ds => ds.Endpoints).OfType<RouteEndpoint>().ToList();
        Assert.Contains(endpoints, e => e.RoutePattern.RawText == "dapr/subscribe");
        Assert.Contains(endpoints, e => e.RoutePattern.RawText == "api/orders");
    }

    [Fact]
    public void MapDaprMessaging_OnlyProgrammatic_MapsAppCallbackGrpcService()
    {
        var services = new ServiceCollection();
        var registry = new FakeRegistry(new TopicSubscriptionDescriptor
        {
            PubsubName = "pubsub",
            TopicName = "grpc-orders",
            Route = "grpc-orders",
            Delivery = DeliveryMode.Programmatic
        });
        services.AddLogging();
        services.AddSingleton<IDaprMessagingSubscriberRegistry>(registry);
        services.AddRouting();
        services.AddGrpc();

        var app = new DefaultEndpointRouteBuilder(services.BuildServiceProvider());
        var result = app.MapDaprMessaging();

        Assert.Same(app, result);
        // gRPC mapping adds a ModelEndpointDataSource to DataSources
        Assert.NotEmpty(app.DataSources);
        // No HTTP route endpoints mapped
        var routeEndpoints = app.DataSources.SelectMany(ds => ds.Endpoints).OfType<RouteEndpoint>().ToList();
        Assert.DoesNotContain(routeEndpoints, e => e.RoutePattern.RawText == "dapr/subscribe");
    }

    [Fact]
    public void MapDaprMessaging_BothHttpAndProgrammatic_MapsBoth()
    {
        var services = new ServiceCollection();
        var registry = new FakeRegistry(
            new TopicSubscriptionDescriptor
            {
                PubsubName = "pubsub",
                TopicName = "http-orders",
                Route = "api/orders",
                Delivery = DeliveryMode.Http
            },
            new TopicSubscriptionDescriptor
            {
                PubsubName = "pubsub",
                TopicName = "grpc-orders",
                Route = "grpc-orders",
                Delivery = DeliveryMode.Programmatic
            });
        services.AddLogging();
        services.AddSingleton<IDaprMessagingSubscriberRegistry>(registry);
        services.AddRouting();
        services.AddGrpc();

        var app = new DefaultEndpointRouteBuilder(services.BuildServiceProvider());
        var result = app.MapDaprMessaging();

        Assert.Same(app, result);
        var routeEndpoints = app.DataSources.SelectMany(ds => ds.Endpoints).OfType<RouteEndpoint>().ToList();
        Assert.Contains(routeEndpoints, e => e.RoutePattern.RawText == "dapr/subscribe");
        Assert.Contains(routeEndpoints, e => e.RoutePattern.RawText == "api/orders");
        Assert.NotEmpty(app.DataSources);
    }

    private sealed class DefaultEndpointRouteBuilder : IEndpointRouteBuilder
    {
        public DefaultEndpointRouteBuilder(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }

    private sealed class FakeRegistry : IDaprMessagingSubscriberRegistry
    {
        private readonly TopicSubscriptionDescriptor[] _descriptors;
        public FakeRegistry(params TopicSubscriptionDescriptor[] descriptors) => _descriptors = descriptors;
        public IReadOnlyList<TopicSubscriptionDescriptor> Descriptors => _descriptors;
        public ITopicDispatcher? Resolve(string pubsubName, string topicName, DeliveryMode mode) => null;
    }
}
