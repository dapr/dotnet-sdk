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
using System.Text;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Routing.Example03.Tests;

/// <summary>
/// Demonstrates how to write end-to-end integration tests for content-based routing
/// and dead-letter queue (DLQ) handling against a real Dapr sidecar using <c>Dapr.Testcontainers</c>.
/// </summary>
public sealed class RoutingIntegrationTests : IAsyncLifetime
{
    private const string PubSubName = Constants.DaprComponentNames.PubSubComponentName;
    private const string ExpressTopic = "express-shipments";
    private const string InternationalTopic = "international-shipments";
    private const string StandardTopic = "standard-shipments";

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(45);

    private BaseHarness? _harness;
    private DaprTestApplication? _app;
    private readonly RoutingTestLogSink _logSink = new();

    public async ValueTask InitializeAsync()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("pubsub-routing-example");

        _harness = new DaprHarnessBuilder(componentsDir)
            .WithOptions(new DaprRuntimeOptions())
            .BuildPubSub();

        _app = await new DaprTestApplicationBuilder(_harness)
            .ConfigureServices(builder =>
            {
                builder.Logging.ClearProviders();
                builder.Logging.AddProvider(new RoutingTestLoggerProvider(_logSink));
                builder.Logging.SetMinimumLevel(LogLevel.Debug);

                builder.Services.AddDaprMessaging(options =>
                    options.DaprGrpcEndpoint = $"http://127.0.0.1:{_harness.DaprGrpcPort}");
            })
            .BuildAndStartAsync();

        // Allow streaming subscriptions to establish connections with Dapr sidecar
        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    public async ValueTask DisposeAsync()
    {
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
    public async Task ShipmentRoutingAndDeadLetter_ExecutesExpectedHandlers()
    {
        Assert.NotNull(_harness);
        Assert.NotNull(_app);

        // 1. Verify streaming subscriber is registered
        var hostedServices = _app.GetRequiredService<IEnumerable<IHostedService>>();
        Assert.Contains(hostedServices, s => s.GetType().Name == "StreamingSubscriberHostedService");

        // 2. Publish Express shipment -> ExpressShippingHandler
        var expressShipment = new ShipmentPackage("shp-exp-001", "express", "US", 2.5m, "buyer@example.com");
        await PublishViaHttpAsync(ExpressTopic, expressShipment, TestContext.Current.CancellationToken);

        var expressHandled = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("[EXPRESS ROUTE] Processing urgent shipment shp-exp-001"),
            TestTimeout,
            TestContext.Current.CancellationToken);
        Assert.True(expressHandled, $"Express shipment was not processed by ExpressShippingHandler. Captured logs:{Environment.NewLine}{string.Join(Environment.NewLine, _logSink.AllMessages)}");

        // 3. Publish International shipment -> InternationalShippingHandler
        var intlShipment = new ShipmentPackage("shp-intl-002", "standard", "DE", 5.0m, "berlin@example.de");
        await PublishViaHttpAsync(InternationalTopic, intlShipment, TestContext.Current.CancellationToken);

        var intlHandled = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("[INTERNATIONAL ROUTE] Preparing customs clearance for shp-intl-002"),
            TestTimeout,
            TestContext.Current.CancellationToken);
        Assert.True(intlHandled, "International shipment was not processed by InternationalShippingHandler.");

        // 4. Publish Standard domestic shipment -> StandardShippingHandler
        var stdShipment = new ShipmentPackage("shp-std-003", "standard", "US", 1.2m, "local@example.com");
        await PublishViaHttpAsync(StandardTopic, stdShipment, TestContext.Current.CancellationToken);

        var stdHandled = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("[STANDARD ROUTE] Processing standard domestic shipment shp-std-003"),
            TestTimeout,
            TestContext.Current.CancellationToken);
        Assert.True(stdHandled, "Standard shipment was not processed by StandardShippingHandler.");

        // 5. Publish invalid express shipment (WeightKg <= 0) -> ExpressShippingHandler drops it
        var invalidExpress = new ShipmentPackage("shp-exp-bad", "express", "US", 0m, "bad@example.com");
        await PublishViaHttpAsync(ExpressTopic, invalidExpress, TestContext.Current.CancellationToken);

        var expressDropped = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("Dropping express shipment shp-exp-bad"),
            TestTimeout,
            TestContext.Current.CancellationToken);
        Assert.True(expressDropped, "Invalid express shipment was not dropped as expected.");
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

public sealed class RoutingTestLogSink
{
    private readonly ConcurrentBag<string> _messages = new();
    private readonly SemaphoreSlim _signal = new(0);

    public IReadOnlyCollection<string> AllMessages => _messages.ToArray();

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

public sealed class RoutingTestLoggerProvider(RoutingTestLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new RoutingTestLogger(categoryName, sink);
    public void Dispose() { }
}

public sealed class RoutingTestLogger(string category, RoutingTestLogSink sink) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var msg = formatter(state, exception);
        sink.Add($"[{category}] {msg}");
    }
}
