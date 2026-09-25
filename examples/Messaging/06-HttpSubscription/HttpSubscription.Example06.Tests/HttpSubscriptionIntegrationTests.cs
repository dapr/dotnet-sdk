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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace HttpSubscription.Example06.Tests;

/// <summary>
/// Instructive integration tests demonstrating how to validate Dapr HTTP push subscriptions
/// (<see cref="DeliveryMode.Http"/>) using <see cref="Dapr.Testcontainers"/> against real Dapr infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// <b>How to Write HTTP Subscription Integration Tests:</b>
/// </para>
/// <list type="number">
///   <item>
///     <description>
///       <b>Harness Setup:</b> Start a <see cref="PubSubHarness"/> using <see cref="DaprHarnessBuilder"/>.
///       HTTP push subscriptions require the sidecar to discover subscriptions via <c>GET /dapr/subscribe</c>
///       and deliver events to configured HTTP routes (e.g., <c>POST /api/events/invoices</c>).
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Startup Order:</b> Use <c>WithDaprStartupOrder(false)</c> (app-first startup) so the ASP.NET Core
///       HTTP server is listening on its port when the Dapr sidecar boots up and invokes <c>GET /dapr/subscribe</c>.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Host Configuration:</b> Use <see cref="DaprTestApplicationBuilder"/> to configure services and endpoints.
///       Call <c>services.AddDaprMessaging()</c> and <c>app.MapDaprMessaging()</c> to map both subscription discovery
///       and topic endpoint handlers.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Publish and Verify:</b> Publish events via Dapr's HTTP publishing API or <see cref="DaprPublishSubscribeClient"/>,
///       and verify that the sidecar delivers the CloudEvent via HTTP POST to the handler route.
///     </description>
///   </item>
/// </list>
/// </remarks>
public sealed class HttpSubscriptionIntegrationTests : IAsyncLifetime
{
    private const string PubSubName = Constants.DaprComponentNames.PubSubComponentName;
    private const string InvoicesTopic = "invoices";

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(45);

    private BaseHarness? _harness;
    private DaprTestApplication? _app;
    private readonly HttpSubTestLogSink _logSink = new();

    public async ValueTask InitializeAsync()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("pubsub-httpsub-example");

        _harness = new DaprHarnessBuilder(componentsDir)
            .WithOptions(new DaprRuntimeOptions())
            .BuildPubSub();

        _app = await new DaprTestApplicationBuilder(_harness)
            .WithDaprStartupOrder(false)
            .ConfigureServices(builder =>
            {
                builder.Logging.ClearProviders();
                builder.Logging.AddProvider(new HttpSubTestLoggerProvider(_logSink));
                builder.Logging.SetMinimumLevel(LogLevel.Debug);

                builder.Services.AddDaprMessaging();
            })
            .ConfigureApp(app =>
            {
                app.UseRouting();
                app.MapDaprMessaging();
            })
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
    public void RegistryContainsInvoiceHandler()
    {
        Assert.NotNull(_app);
        var registry = _app.GetRequiredService<IDaprMessagingSubscriberRegistry>();
        Assert.NotEmpty(registry.Descriptors);

        var descriptor = Assert.Single(registry.Descriptors, d => d.TopicName == InvoicesTopic);
        Assert.Equal(DeliveryMode.Http, descriptor.Delivery);
        Assert.Equal("api/events/invoices", descriptor.Route);
    }

    [Fact]
    public async Task HttpPushSubscription_ReceivesAndProcessesInvoiceEvent()
    {
        Assert.NotNull(_harness);
        Assert.NotNull(_app);

        // 1. Publish valid invoice -> Handled by InvoiceProcessingHandler via HTTP push
        var invoice = new InvoiceGenerated("INV-E2E-101", "CUST-E2E", 750.00m, "USD", DateTimeOffset.UtcNow.AddDays(30));
        await PublishViaHttpAsync(InvoicesTopic, invoice, TestContext.Current.CancellationToken);

        var invoiceProcessed = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("INV-E2E-101"),
            TestTimeout,
            TestContext.Current.CancellationToken);

        if (!invoiceProcessed)
        {
            var allLogs = string.Join("\n", _logSink.AllMessages);
            Assert.Fail($"Invoice event was not received via HTTP push. All logs:\n{allLogs}");
        }

        // 2. Publish invalid invoice with zero/negative amount -> Handler drops it
        var invalidInvoice = new InvoiceGenerated("INV-E2E-BAD", "CUST-BAD", 0m, "USD", DateTimeOffset.UtcNow);
        await PublishViaHttpAsync(InvoicesTopic, invalidInvoice, TestContext.Current.CancellationToken);

        var invoiceDropped = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("INV-E2E-BAD"),
            TestTimeout,
            TestContext.Current.CancellationToken);

        if (!invoiceDropped)
        {
            var allLogs = string.Join("\n", _logSink.AllMessages);
            Assert.Fail($"Invalid invoice was not dropped. All logs:\n{allLogs}");
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

public sealed class HttpSubTestLogSink
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

public sealed class HttpSubTestLoggerProvider(HttpSubTestLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new HttpSubTestLogger(categoryName, sink);
    public void Dispose() { }
}

public sealed class HttpSubTestLogger(string category, HttpSubTestLogSink sink) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var msg = formatter(state, exception);
        sink.Add($"[{category}] {msg}");
    }
}
