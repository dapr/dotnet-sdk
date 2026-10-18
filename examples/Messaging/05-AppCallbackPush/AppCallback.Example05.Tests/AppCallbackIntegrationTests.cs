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
using AppCallback.Example05;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AppCallback.Example05.Tests;

/// <summary>
/// Demonstrates how to write end-to-end integration tests for programmatic gRPC push delivery
/// (<see cref="DeliveryMode.Programmatic"/>) using <c>Dapr.Testcontainers</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Programmatic gRPC Push Testing Pattern:</b>
/// </para>
/// <list type="number">
///   <item>
///     <description>
///       <b>Configure Sidecar for gRPC App Protocol:</b> Pass <c>new DaprRuntimeOptions().WithAppProtocol("grpc")</c>
///       to <see cref="DaprHarnessBuilder"/>. This tells Dapr to discover subscriptions via gRPC <c>ListTopicSubscriptions</c>
///       and push events via gRPC <c>OnTopicEvent</c>.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>App-First Startup Order:</b> Set <c>.WithDaprStartupOrder(false)</c> so that the application's
///       HTTP/2 gRPC server is listening before the sidecar starts and calls discovery endpoints.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Map Messaging Endpoints:</b> In <c>ConfigureApp</c>, invoke <c>app.MapDaprMessaging()</c> to expose
///       the generated <c>DaprAppCallbackService</c>.
///     </description>
///   </item>
/// </list>
/// </remarks>
public sealed class AppCallbackIntegrationTests : IAsyncLifetime
{
    private const string PubSubName = Constants.DaprComponentNames.PubSubComponentName;
    private const string PaymentsTopic = "payments";

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(45);

    private BaseHarness? _harness;
    private DaprTestApplication? _app;
    private readonly AppCallbackTestLogSink _logSink = new();

    public async ValueTask InitializeAsync()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("pubsub-appcallback-example");

        // Dapr sidecar needs to speak gRPC to the application to push events
        _harness = new DaprHarnessBuilder(componentsDir)
            .WithOptions(new DaprRuntimeOptions().WithAppProtocol("grpc"))
            .BuildPubSub();

        _app = await new DaprTestApplicationBuilder(_harness)
            .WithDaprStartupOrder(false)
            .ConfigureServices(builder =>
            {
                // Plaintext gRPC requires HTTP/2 without TLS negotiation
                builder.WebHost.ConfigureKestrel(options =>
                    options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http2));

                builder.Logging.ClearProviders();
                builder.Logging.AddProvider(new AppCallbackTestLoggerProvider(_logSink));
                builder.Logging.SetMinimumLevel(LogLevel.Debug);

                builder.Services.AddDaprMessaging();
            })
            .ConfigureApp(app => app.MapDaprAppCallback())
            .BuildAndStartAsync();

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
    public void RegistryContainsPaymentHandler()
    {
        Assert.NotNull(_app);
        var registry = _app.GetRequiredService<IDaprMessagingSubscriberRegistry>();
        Assert.NotEmpty(registry.Descriptors);
    }

    [Fact]
    public async Task AppCallbackGrpcPush_ReceivesAndProcessesPaymentEvent()
    {
        Assert.NotNull(_harness);
        Assert.NotNull(_app);

        // 1. Publish valid payment -> Handled by PaymentProcessingHandler via gRPC push
        var payment = new PaymentReceived("pay-e2e-100", "cust-grpc", 125.00m, "USD", "CreditCard");
        await PublishViaHttpAsync(PaymentsTopic, payment, TestContext.Current.CancellationToken);

        var paymentProcessed = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("pay-e2e-100"),
            TestTimeout,
            TestContext.Current.CancellationToken);
        if (!paymentProcessed)
        {
            var allLogs = string.Join("\n", _logSink.AllMessages);
            Assert.Fail($"Payment event was not received via gRPC push. All logs:\n{allLogs}");
        }

        // 2. Publish invalid payment with non-positive amount -> Handler drops it
        var invalidPayment = new PaymentReceived("pay-e2e-bad", "cust-bad", 0m, "USD", "DebitCard");
        await PublishViaHttpAsync(PaymentsTopic, invalidPayment, TestContext.Current.CancellationToken);

        var paymentDropped = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("pay-e2e-bad"),
            TestTimeout,
            TestContext.Current.CancellationToken);
        if (!paymentDropped)
        {
            var allLogs = string.Join("\n", _logSink.AllMessages);
            Assert.Fail($"Invalid payment was not dropped. All logs:\n{allLogs}");
        }
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

public sealed class AppCallbackTestLogSink
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

public sealed class AppCallbackTestLoggerProvider(AppCallbackTestLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new AppCallbackTestLogger(categoryName, sink);
    public void Dispose() { }
}

public sealed class AppCallbackTestLogger(string category, AppCallbackTestLogSink sink) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var msg = formatter(state, exception);
        sink.Add($"[{category}] {msg}");
    }
}
