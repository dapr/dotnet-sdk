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

namespace BulkSubscribe.Example04.Tests;

/// <summary>
/// Demonstrates how to write end-to-end integration tests for high-throughput
/// bulk subscriptions against a real Dapr sidecar using <c>Dapr.Testcontainers</c>.
/// </summary>
public sealed class BulkSubscribeIntegrationTests : IAsyncLifetime
{
    private const string PubSubName = Constants.DaprComponentNames.PubSubComponentName;
    private const string TelemetryTopic = "telemetry";

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(45);

    private BaseHarness? _harness;
    private DaprTestApplication? _app;
    private readonly BulkTestLogSink _logSink = new();

    public async ValueTask InitializeAsync()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("pubsub-bulk-example");

        _harness = new DaprHarnessBuilder(componentsDir)
            .WithOptions(new DaprRuntimeOptions())
            .BuildPubSub();

        _app = await new DaprTestApplicationBuilder(_harness)
            .ConfigureServices(builder =>
            {
                builder.Logging.ClearProviders();
                builder.Logging.AddProvider(new BulkTestLoggerProvider(_logSink));
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
    public async Task BulkTelemetry_PublishesBatchAndReceivesTelemetryReadings()
    {
        Assert.NotNull(_harness);
        Assert.NotNull(_app);

        // 1. Verify streaming subscriber is registered
        var hostedServices = _app.GetRequiredService<IEnumerable<IHostedService>>();
        Assert.Contains(hostedServices, s => s.GetType().Name == "StreamingSubscriberHostedService");

        // 2. Publish multiple telemetry readings
        var validReading = new DeviceTelemetry("device-sensor-01", 22.5, 45.0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await PublishViaHttpAsync(TelemetryTopic, validReading, TestContext.Current.CancellationToken);

        var validHandled = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("Processed telemetry reading from device-sensor-01"),
            TestTimeout,
            TestContext.Current.CancellationToken);
        Assert.True(validHandled, "Valid telemetry reading was not processed by TelemetryBulkHandler.");

        // 3. Publish invalid reading (humidity > 100%) -> Should be dropped
        var invalidReading = new DeviceTelemetry("device-sensor-bad", 25.0, 150.0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await PublishViaHttpAsync(TelemetryTopic, invalidReading, TestContext.Current.CancellationToken);

        var invalidDropped = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("Dropping out-of-range sensor reading for device device-sensor-bad"),
            TestTimeout,
            TestContext.Current.CancellationToken);
        Assert.True(invalidDropped, "Out-of-range telemetry reading was not dropped by TelemetryBulkHandler.");
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

public sealed class BulkTestLogSink
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

public sealed class BulkTestLoggerProvider(BulkTestLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new BulkTestLogger(categoryName, sink);
    public void Dispose() { }
}

public sealed class BulkTestLogger(string category, BulkTestLogSink sink) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var msg = formatter(state, exception);
        sink.Add($"[{category}] {msg}");
    }
}
