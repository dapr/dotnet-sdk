using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using DynamicStreaming.Example07;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DynamicStreaming.Example07.Tests;

/// <summary>
/// Instructive integration tests demonstrating how to validate dynamic runtime streaming subscriptions
/// (<see cref="DaprPublishSubscribeClient.SubscribeAsync"/>) using <see cref="Dapr.Testcontainers"/> against real Dapr infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// <b>How to Write Dynamic Streaming Subscription Integration Tests:</b>
/// </para>
/// <list type="number">
///   <item>
///     <description>
///       <b>Harness Setup:</b> Start a <see cref="PubSubHarness"/> using <see cref="DaprHarnessBuilder"/>.
///       Dynamic subscriptions operate via client-initiated gRPC streams (<c>SubscribeAsync</c>), so no inbound app ports are required.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Client Configuration:</b> Configure <see cref="DaprPublishSubscribeClient"/> in DI pointing to the containerized
///       Dapr gRPC endpoint (<c>builder.UseGrpcEndpoint($"http://127.0.0.1:{_harness.DaprGrpcPort}")</c>).
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Worker Registration:</b> Register the <see cref="DynamicSubscriberWorker"/> background hosted service, which opens
///       the dynamic subscription stream during application startup.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Publish and Verify:</b> Publish events via <see cref="DaprPublishSubscribeClient"/> and assert that the dynamic
///       subscription receives, parses, and processes the stream messages in real time.
///     </description>
///   </item>
/// </list>
/// </remarks>
public sealed class DynamicStreamingIntegrationTests : IAsyncLifetime
{
    private const string PubSubName = Constants.DaprComponentNames.PubSubComponentName;
    private const string TenantEventsTopic = "tenant-events";

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(45);

    private BaseHarness? _harness;
    private DaprTestApplication? _app;
    private DaprPublishSubscribeClient? _publisherClient;
    private readonly DynamicStreamTestLogSink _logSink = new();

    public async ValueTask InitializeAsync()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("pubsub-dynamic-example");

        _harness = new DaprHarnessBuilder(componentsDir)
            .WithOptions(new DaprRuntimeOptions())
            .BuildPubSub();

        _app = await new DaprTestApplicationBuilder(_harness)
            .ConfigureServices(builder =>
            {
                builder.Logging.ClearProviders();
                builder.Logging.AddProvider(new DynamicStreamTestLoggerProvider(_logSink));
                builder.Logging.SetMinimumLevel(LogLevel.Debug);

                // Configure Dapr publish/subscribe client pointing to the Testcontainers gRPC port
                builder.Services.AddDaprPubSubClient((_, clientBuilder) =>
                {
                    clientBuilder.UseGrpcEndpoint($"http://127.0.0.1:{_harness.DaprGrpcPort}");
                    clientBuilder.UseHttpEndpoint($"http://127.0.0.1:{_harness.DaprHttpPort}");
                });

                // Register dynamic subscriber worker
                builder.Services.AddHostedService<DynamicSubscriberWorker>();
            })
            .BuildAndStartAsync();

        _publisherClient = new DaprPublishSubscribeClientBuilder()
            .UseGrpcEndpoint($"http://127.0.0.1:{_harness.DaprGrpcPort}")
            .UseHttpEndpoint($"http://127.0.0.1:{_harness.DaprHttpPort}")
            .Build();

        // Allow background worker stream connection to establish
        await Task.Delay(TimeSpan.FromSeconds(2));
    }

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
    public async Task DynamicStreamingSubscription_ReceivesAndProcessesStreamEvents()
    {
        Assert.NotNull(_harness);
        Assert.NotNull(_app);
        Assert.NotNull(_publisherClient);

        // 1. Verify dynamic subscriber worker is registered
        var hostedServices = _app.GetRequiredService<IEnumerable<IHostedService>>();
        Assert.Contains(hostedServices, s => s is DynamicSubscriberWorker);

        // 2. Publish valid tenant event -> Expect dynamic stream handler log
        var tenantEvent = new TenantEvent("TENANT-E2E-001", "UserCreated", "{\"userId\":\"usr_99\"}", DateTimeOffset.UtcNow);
        await _publisherClient.PublishEventAsync(PubSubName, TenantEventsTopic, tenantEvent, TestContext.Current.CancellationToken);

        var eventProcessed = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("TENANT-E2E-001"),
            TestTimeout,
            TestContext.Current.CancellationToken);

        if (!eventProcessed)
        {
            var allLogs = string.Join("\n", _logSink.AllMessages);
            Assert.Fail($"Tenant event was not received via dynamic stream. All logs:\n{allLogs}");
        }

        // 3. Publish invalid tenant event with empty TenantId -> Expect drop log
        var invalidEvent = new TenantEvent("", "UserDeleted", "{}", DateTimeOffset.UtcNow);
        await _publisherClient.PublishEventAsync(PubSubName, TenantEventsTopic, invalidEvent, TestContext.Current.CancellationToken);

        var eventDropped = await _logSink.WaitForLogMessageAsync(
            msg => msg.Contains("Dropping invalid tenant event"),
            TestTimeout,
            TestContext.Current.CancellationToken);

        if (!eventDropped)
        {
            var allLogs = string.Join("\n", _logSink.AllMessages);
            Assert.Fail($"Invalid tenant event was not dropped by dynamic stream handler. All logs:\n{allLogs}");
        }
    }
}

public sealed class DynamicStreamTestLogSink
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

public sealed class DynamicStreamTestLoggerProvider(DynamicStreamTestLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new DynamicStreamTestLogger(categoryName, sink);
    public void Dispose() { }
}

public sealed class DynamicStreamTestLogger(string category, DynamicStreamTestLogSink sink) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var msg = formatter(state, exception);
        sink.Add($"[{category}] {msg}");
    }
}
