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
using Dapr.Messaging.Subscribe.AppCallback;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.IntegrationTest.Messaging.PublishSubscribe;

/// <summary>
/// End-to-end proof that a single <c>builder.Services.AddDaprMessaging()</c> call is sufficient to
/// receive programmatic (AppCallback push) subscriptions from a real Dapr sidecar.
/// </summary>
/// <remarks>
/// <para>
/// These tests exist to close a specific coverage gap. Every other test in this assembly constructs
/// <see cref="DaprAppCallbackService"/> directly via <c>new</c>, which means the
/// <c>TryAddTransient&lt;DaprAppCallbackService&gt;()</c> registration inside
/// <c>DaprMessagingRegistration.Register</c> was never actually exercised. Deleting that line would have
/// left the entire suite green while breaking gRPC push delivery at runtime.
/// </para>
/// <para>
/// The test below cannot pass unless the service resolves from the container: the sidecar is started with
/// <c>--app-protocol grpc</c>, calls <c>ListTopicSubscriptions</c> on the app to discover the topic, and
/// then pushes the published message through <c>OnTopicEvent</c>. Both calls are routed by
/// <c>MapGrpcService&lt;DaprAppCallbackService&gt;()</c>, which resolves the service from DI per request.
/// </para>
/// </remarks>
public sealed class AppCallbackSubscriptionIntegrationTests : IAsyncLifetime
{
    private const string PubSubName = Constants.DaprComponentNames.PubSubComponentName;
    private const string OrdersTopic = "integration-orders";

    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(60);

    private DaprTestApplication? _application;
    private PubSubHarnessAccessor? _accessor;

    /// <summary>
    /// Holds the harness so tests can reach the sidecar's HTTP port for publishing.
    /// </summary>
    private sealed record PubSubHarnessAccessor(Dapr.Testcontainers.Harnesses.PubSubHarness Harness);

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("appcallback-components");

        // The sidecar must speak gRPC to the application channel so that it uses the AppCallback
        // protocol (ListTopicSubscriptions / OnTopicEvent) rather than HTTP subscription discovery.
        var harness = new DaprHarnessBuilder(componentsDir)
            .WithOptions(new DaprRuntimeOptions().WithAppProtocol("grpc"))
            .BuildPubSub();

        _accessor = new PubSubHarnessAccessor(harness);

        _application = await new DaprTestApplicationBuilder(harness)
            // App-first startup: the sidecar calls ListTopicSubscriptions as soon as it comes up, so
            // the application's gRPC endpoint has to already be listening.
            .WithDaprStartupOrder(false)
            .ConfigureServices(builder =>
            {
                // Plaintext gRPC requires HTTP/2 without an upgrade handshake.
                builder.WebHost.ConfigureKestrel(options =>
                    options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http2));

                builder.Services.AddSingleton<IntegrationOrderState>();
                builder.Services.AddSingleton<HttpNotificationState>();
                builder.Services.AddSingleton<HttpBulkState>();

                // The entire point of the test: one call, no AddGrpc, no manual service registration.
                builder.Services.AddDaprMessaging();
            })
            .ConfigureApp(app => app.MapDaprAppCallback())
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
    /// The fast-failing signal: if <c>TryAddTransient&lt;DaprAppCallbackService&gt;()</c> is missing from
    /// the registration, this throws immediately rather than timing out in the delivery test below.
    /// </summary>
    [Fact]
    public void AddDaprMessaging_RegistersAppCallbackServiceInContainer()
    {
        Assert.NotNull(_application);

        var service = _application!.GetRequiredService<DaprAppCallbackService>();
        Assert.NotNull(service);
    }

    /// <summary>
    /// <c>MapGrpcService&lt;T&gt;()</c> resolves its service from a request scope, so the registration has
    /// to be resolvable from a scope and not just the root provider.
    /// </summary>
    [Fact]
    public void AppCallbackService_ResolvesFromRequestScope()
    {
        Assert.NotNull(_application);

        using var scope = _application!.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<DaprAppCallbackService>();
        Assert.NotNull(service);
    }

    /// <summary>
    /// The decisive test. A message published through the real sidecar can only reach the handler if the
    /// sidecar discovered the subscription via <c>ListTopicSubscriptions</c> and pushed it via
    /// <c>OnTopicEvent</c> - both served by the DI-resolved <see cref="DaprAppCallbackService"/>.
    /// </summary>
    [Fact]
    public async Task PublishedMessage_IsPushedToGeneratedHandlerViaAppCallback()
    {
        Assert.NotNull(_application);
        Assert.NotNull(_accessor);

        var state = _application!.GetRequiredService<IntegrationOrderState>();
        var order = new IntegrationOrder("order-e2e-1", 250);

        await PublishAsync(order, TestContext.Current.CancellationToken);

        var received = await WaitForAsync(
            () => state.Received.FirstOrDefault(r => r.Order.Id == order.Id),
            r => r.Order is not null,
            TestContext.Current.CancellationToken);

        Assert.Equal(order.Id, received.Order.Id);
        Assert.Equal(order.Total, received.Order.Total);
        Assert.Equal(OrdersTopic, received.Context.TopicName);
        Assert.Equal(PubSubName, received.Context.PubsubName);
    }

    /// <summary>
    /// Verifies multiple published messages are each pushed through the callback service, guarding
    /// against a registration that happens to work once (for example a mis-scoped singleton that caches
    /// per-request state).
    /// </summary>
    [Fact]
    public async Task MultiplePublishedMessages_AreAllPushedToGeneratedHandler()
    {
        Assert.NotNull(_application);

        var state = _application!.GetRequiredService<IntegrationOrderState>();
        var orders = Enumerable.Range(1, 5)
            .Select(i => new IntegrationOrder($"order-batch-{i}", i * 10))
            .ToList();

        foreach (var order in orders)
        {
            await PublishAsync(order, TestContext.Current.CancellationToken);
        }

        await WaitForAsync(
            () => state.Received.Count(r => r.Order.Id.StartsWith("order-batch-", StringComparison.Ordinal)),
            count => count >= orders.Count,
            TestContext.Current.CancellationToken);

        foreach (var order in orders)
        {
            Assert.Contains(state.Received, r => r.Order.Id == order.Id && r.Order.Total == order.Total);
        }
    }

    private async Task PublishAsync(IntegrationOrder order, CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{_accessor!.Harness.DaprHttpPort}"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        var content = new StringContent(JsonSerializer.Serialize(order), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/v1.0/publish/{PubSubName}/{OrdersTopic}", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<T> WaitForAsync<T>(
        Func<T> read,
        Func<T, bool> isSatisfied,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < DeliveryTimeout)
        {
            var value = read();
            if (isSatisfied(value))
            {
                return value;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        throw new TimeoutException(
            $"The expected message was not delivered through the AppCallback service within {DeliveryTimeout}. " +
            "This usually means DaprAppCallbackService was not registered in the service collection, or gRPC " +
            "server hosting was not enabled by AddDaprMessaging.");
    }
}
