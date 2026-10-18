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
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.Subscribe.Streaming;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Streaming.Example02.Tests;

/// <summary>
/// Demonstrates how to write end-to-end integration tests for Dapr streaming subscriptions
/// (<see cref="DeliveryMode.Streaming"/>) using <c>Dapr.Testcontainers</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Streaming Subscription Testing Pattern:</b>
/// </para>
/// <list type="number">
///   <item>
///     <description>
///       <b>Harness Setup:</b> Start a <see cref="PubSubHarness"/> using <see cref="DaprHarnessBuilder"/>.
///       Streaming subscriptions do not require an inbound HTTP/gRPC app port (the application initiates
///       a gRPC client stream to Dapr), so default harness settings work out-of-the-box.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Host Configuration:</b> Use <see cref="DaprTestApplicationBuilder"/> to configure DI and register
///       messaging via <c>AddDaprMessaging(o => o.DaprGrpcEndpoint = ...)</c>. The source generator automatically
///       discovers <see cref="OrderProcessingHandler"/> and registers the background
///       <see cref="StreamingSubscriberHostedService"/>.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Publish and Verify:</b> Publish events using <see cref="DaprPublishSubscribeClient"/> to the sidecar,
///       and assert that the streaming subscriber receives, processes, and acknowledges the messages.
///     </description>
///   </item>
/// </list>
/// </remarks>
public sealed class StreamingIntegrationTests : IAsyncLifetime
{
    private const string PubSubName = Constants.DaprComponentNames.PubSubComponentName;
    private const string OrdersTopic = "orders";
    private const string InventoryTopic = "inventory-reserved";

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(60);

    private BaseHarness? _harness;
    private DaprTestApplication? _app;
    private DaprPublishSubscribeClient? _publisherClient;
    private readonly TestLogSink _logSink = new();

    /// <summary>
    /// Initializes Testcontainers and boots the test application.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("pubsub-streaming-example");

        _harness = new DaprHarnessBuilder(componentsDir)
            .WithOptions(new DaprRuntimeOptions())
            .BuildPubSub();

        _app = await new DaprTestApplicationBuilder(_harness)
            .ConfigureServices(builder =>
            {
                builder.Logging.ClearProviders();
                builder.Logging.AddProvider(new TestLoggerProvider(_logSink));
                builder.Logging.SetMinimumLevel(LogLevel.Debug);

                // Register Dapr.Messaging pointing to the Testcontainers gRPC port
                builder.Services.AddDaprMessaging(options =>
                    options.DaprGrpcEndpoint = $"http://127.0.0.1:{_harness.DaprGrpcPort}");
            })
            .BuildAndStartAsync();

        _publisherClient = new DaprPublishSubscribeClientBuilder()
            .UseGrpcEndpoint($"http://127.0.0.1:{_harness.DaprGrpcPort}")
            .UseHttpEndpoint($"http://127.0.0.1:{_harness.DaprHttpPort}")
            .Build();
    }

    /// <summary>
    /// Cleans up application and container resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _publisherClient?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }
    }

    [Fact]
    public async Task StreamingSubscription_ReceivesAndProcessesEventsFromRealDapr()
    {
        Assert.NotNull(_harness);
        Assert.NotNull(_app);

        // 1. Verify hosted service is registered and running
        var hostedServices = _app.GetRequiredService<IEnumerable<IHostedService>>();
        Assert.Contains(hostedServices, s => s.GetType().Name == "StreamingSubscriberHostedService");

        // 2. Publish valid order -> Expect Success processing log
        var validOrder = new OrderPlaced("ord-e2e-101", "cust-stream", 75.50m, "SKU-WIDGET-1", 3);
        await PublishViaHttpAsync(OrdersTopic, validOrder, TestContext.Current.CancellationToken);

        var orderProcessed = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("Successfully processed order ord-e2e-101"),
            TestTimeout,
            TestContext.Current.CancellationToken);
        Assert.True(orderProcessed, "Valid order was not processed within the timeout window.");

        // 3. Publish inventory reservation -> Expect Inventory handler log
        var inventory = new InventoryReserved("res-e2e-1", "ord-e2e-202", "SKU-GADGET-9", 10);
        await PublishViaHttpAsync(InventoryTopic, inventory, TestContext.Current.CancellationToken);

        var inventoryProcessed = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("Reserved 10 of SKU SKU-GADGET-9 for order ord-e2e-202"),
            TestTimeout,
            TestContext.Current.CancellationToken);
        Assert.True(inventoryProcessed, "Inventory reservation was not processed within the timeout window.");

        // 4. Publish invalid order -> Expect Drop warning log
        var invalidOrder = new OrderPlaced("ord-e2e-drop", "cust-bad", 0m, "", -1);
        await PublishViaHttpAsync(OrdersTopic, invalidOrder, TestContext.Current.CancellationToken);

        var orderDropped = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("Dropping invalid order ord-e2e-drop"),
            TestTimeout,
            TestContext.Current.CancellationToken);
        Assert.True(orderDropped, "Invalid order was not dropped within the timeout window.");
    }

    private async Task PublishViaHttpAsync<T>(string topic, T payload, CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{_harness!.DaprHttpPort}"),
            Timeout = TimeSpan.FromSeconds(30)
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/v1.0/publish/{PubSubName}/{topic}", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// Captures logs emitted during integration test runs to verify handler actions without invasive mocks.
/// </summary>
public sealed class TestLogSink
{
    private readonly ConcurrentBag<string> _messages = new();
    private readonly SemaphoreSlim _signal = new(0);

    public void Add(string message)
    {
        _messages.Add(message);
        _signal.Release();
    }

    public async Task<bool> WaitForLogMessageAsync(Func<string, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (_messages.Any(predicate))
            {
                return true;
            }

            var remaining = timeout - sw.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            try
            {
                await _signal.WaitAsync(remaining, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return _messages.Any(predicate);
    }
}

public sealed class TestLoggerProvider(TestLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, sink);
    public void Dispose() { }
}

public sealed class TestLogger(string category, TestLogSink sink) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var msg = formatter(state, exception);
        sink.Add($"[{category}] {msg}");
    }
}
