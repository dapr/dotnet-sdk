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

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Dapr.Messaging;
using Dapr.Messaging.Subscribe.Streaming;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Dapr.IntegrationTest.Messaging.PublishSubscribe;

/// <summary>
/// End-to-end proof that a single <c>builder.Services.AddDaprMessaging()</c> call is sufficient to
/// receive <see cref="DeliveryMode.Streaming"/> subscriptions from a real Dapr sidecar, closing the gap
/// where streaming descriptors were generated but never hosted.
/// </summary>
/// <remarks>
/// Unlike the Programmatic (AppCallback gRPC push) and Http subscription paths, a Streaming subscription
/// requires no ASP.NET Core server, no <c>--app-protocol grpc</c>, and no <c>/dapr/subscribe</c> discovery
/// endpoint: the application itself opens the <c>SubscribeTopicEventsAlpha1</c> bidirectional gRPC stream
/// to the sidecar and pulls messages. That means <see cref="StreamingSubscriberHostedService"/> is the
/// only thing that can make this test pass - if it were missing (as it originally was), a published
/// message would simply never reach <see cref="IntegrationStreamingOrderHandler"/>.
/// </remarks>
public sealed class StreamingSubscriptionIntegrationTests : IAsyncLifetime
{
    private const string PubSubName = Constants.DaprComponentNames.PubSubComponentName;
    private const string OrdersTopic = "integration-streaming-orders";

    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(60);

    private DaprTestApplication? _application;
    private Dapr.Testcontainers.Harnesses.PubSubHarness? _harness;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("streaming-components");

        // No special app protocol is required: the application is a pure gRPC client of the sidecar
        // for streaming subscriptions, so the default (HTTP) app-protocol harness is sufficient.
        _harness = new DaprHarnessBuilder(componentsDir).BuildPubSub();

        _application = await new DaprTestApplicationBuilder(_harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddSingleton<StreamingOrderState>();

                // The entire point of the test: one call, no manual SubscribeAsync, no AddGrpc.
                // The gRPC endpoint must be pointed at the harness's dynamically assigned port, since
                // the default (http://localhost:50001) will not match a Testcontainers-assigned port.
                builder.Services.AddDaprMessaging(options =>
                    options.DaprGrpcEndpoint = $"http://127.0.0.1:{_harness.DaprGrpcPort}");
            })
            .BuildAndStartAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_application is not null)
        {
            await _application.DisposeAsync();
        }
    }

    /// <summary>
    /// The fast-failing signal: if <c>AddHostedService&lt;StreamingSubscriberHostedService&gt;()</c> is
    /// missing from the registration, this throws immediately rather than timing out in the delivery
    /// test below.
    /// </summary>
    [Fact]
    public void AddDaprMessaging_RegistersStreamingHostedServiceInContainer()
    {
        Assert.NotNull(_application);

        var hostedServices = _application!.GetRequiredService<IEnumerable<IHostedService>>();
        Assert.Contains(hostedServices, s => s is StreamingSubscriberHostedService);
    }

    /// <summary>
    /// The decisive test. A message published through the real sidecar can only reach the handler if the
    /// hosted service opened the streaming subscription for this (pubsub, topic) at startup and correctly
    /// dispatched delivered messages to the generated dispatcher.
    /// </summary>
    [Fact]
    public async Task PublishedMessage_IsDeliveredToGeneratedHandlerViaStreamingSubscription()
    {
        Assert.NotNull(_application);
        Assert.NotNull(_harness);

        var state = _application!.GetRequiredService<StreamingOrderState>();
        var order = new IntegrationStreamingOrder("streaming-order-e2e-1", 400);

        var received = await PublishUntilDeliveredAsync(
            order,
            () => state.Received.FirstOrDefault(r => r.Order.Id == order.Id),
            r => r.Order is not null,
            TestContext.Current.CancellationToken);

        Assert.Equal(order.Id, received.Order.Id);
        Assert.Equal(order.Total, received.Order.Total);
        Assert.Equal(OrdersTopic, received.Context.TopicName);
        Assert.Equal(PubSubName, received.Context.PubsubName);
    }

    /// <summary>
    /// Verifies multiple published messages are each pulled off the same long-lived stream, guarding
    /// against a hosted service that only pulls a single message before its receiver stalls.
    /// </summary>
    [Fact]
    public async Task MultiplePublishedMessages_AreAllDeliveredToGeneratedHandler()
    {
        Assert.NotNull(_application);

        var state = _application!.GetRequiredService<StreamingOrderState>();
        var orders = Enumerable.Range(1, 5)
            .Select(i => new IntegrationStreamingOrder($"streaming-order-batch-{i}", i * 10))
            .ToList();

        await PublishUntilDeliveredAsync(
            orders,
            order => state.Received.Any(r => r.Order.Id == order.Id),
            TestContext.Current.CancellationToken);

        foreach (var order in orders)
        {
            Assert.Contains(state.Received, r => r.Order.Id == order.Id && r.Order.Total == order.Total);
        }
    }

    private async Task PublishAsync(IntegrationStreamingOrder order, CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{_harness!.DaprHttpPort}"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        var content = new StringContent(JsonSerializer.Serialize(order), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/v1.0/publish/{PubSubName}/{OrdersTopic}", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Publishes <paramref name="order"/>, re-publishing on an interval until <paramref name="isSatisfied"/>
    /// holds. The pub/sub component drops messages published before the sidecar has registered the
    /// streaming subscription, and the hosted service opens that stream asynchronously at startup - so a
    /// single up-front publish races stream establishment and flakes on slower environments.
    /// </summary>
    private async Task<T> PublishUntilDeliveredAsync<T>(
        IntegrationStreamingOrder order,
        Func<T> read,
        Func<T, bool> isSatisfied,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var nextPublishAt = TimeSpan.Zero;

        while (stopwatch.Elapsed < DeliveryTimeout)
        {
            var value = read();
            if (isSatisfied(value))
            {
                return value;
            }

            if (stopwatch.Elapsed >= nextPublishAt)
            {
                await PublishAsync(order, cancellationToken);
                nextPublishAt = stopwatch.Elapsed + TimeSpan.FromSeconds(5);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        throw NotDeliveredException();
    }

    /// <summary>
    /// Re-publishes each not-yet-delivered order on an interval until every order has been received.
    /// See <see cref="PublishUntilDeliveredAsync{T}"/> for why re-publishing is necessary.
    /// </summary>
    private async Task PublishUntilDeliveredAsync(
        IReadOnlyCollection<IntegrationStreamingOrder> orders,
        Func<IntegrationStreamingOrder, bool> isDelivered,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < DeliveryTimeout)
        {
            var pending = orders.Where(order => !isDelivered(order)).ToList();
            if (pending.Count == 0)
            {
                return;
            }

            foreach (var order in pending)
            {
                await PublishAsync(order, cancellationToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }

        throw NotDeliveredException();
    }

    private static TimeoutException NotDeliveredException() =>
        new($"The expected message was not delivered through the streaming subscription within {DeliveryTimeout}. " +
            "This usually means StreamingSubscriberHostedService was not registered in the service " +
            "collection, or failed to open the SubscribeTopicEventsAlpha1 stream.");
}
