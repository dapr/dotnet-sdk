// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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
using System.Text;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Harnesses;

// Disable parallel execution: each test class spins up its own Docker network and Dapr
// sidecar, and running them concurrently would exhaust available ports on the host.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Dapr.IntegrationTest.Messaging.PublishSubscribe;

/// <summary>
/// End-to-end integration tests for the Dapr dynamic pub/sub subscription feature
/// via <see cref="DaprPublishSubscribeClient"/>.
/// </summary>
public sealed class DynamicSubscriptionIntegrationTests : IAsyncLifetime
{
    private const string PubSubName = Constants.DaprComponentNames.PubSubComponentName;
    private const string TestTopic = "integration-test-topic";
    private const string DeadLetterTopic = "integration-test-deadletter";

    /// <summary>Default timeout for waiting on message receipt in tests.</summary>
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(45);

    private BaseHarness? _harness;
    private DaprPublishSubscribeClient? _pubSubClient;
    private HttpClient? _publisherHttpClient;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("pubsub-components");
        _harness = new DaprHarnessBuilder(componentsDir)
            .WithOptions(new DaprRuntimeOptions())
            .BuildPubSub();
        await _harness.InitializeAsync();

        _pubSubClient = new DaprPublishSubscribeClientBuilder()
            .UseGrpcEndpoint($"http://127.0.0.1:{_harness.DaprGrpcPort}")
            .UseHttpEndpoint($"http://127.0.0.1:{_harness.DaprHttpPort}")
            .Build();

        _publisherHttpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{_harness.DaprHttpPort}"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _pubSubClient?.Dispose();
        _publisherHttpClient?.Dispose();
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }
    }

    /// <summary>
    /// Verifies that a message published to a topic is received by a dynamic subscriber.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_ReceivesPublishedMessage()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var received = new TaskCompletionSource<TopicMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Retry));

        await using var subscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            TestTopic,
            options,
            (message, _) =>
            {
                received.TrySetResult(message);
                return Task.FromResult(TopicResponseAction.Success);
            },
            cts.Token);

        // Allow the subscription to register with Dapr before publishing
        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

        const string payload = """{"text":"hello world"}""";
        await PublishMessageAsync(TestTopic, payload, cts.Token);

        var message = await received.Task.WaitAsync(cts.Token);

        Assert.Equal(TestTopic, message.Topic);
        Assert.Equal(PubSubName, message.PubSubName);
        Assert.Contains("hello world", Encoding.UTF8.GetString(message.Data.Span));
    }

    /// <summary>
    /// Verifies that multiple messages published in sequence are all received by the subscriber.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_MultipleMessages_AllReceived()
    {
        const int messageCount = 3;
        using var cts = new CancellationTokenSource(TestTimeout);
        var receivedBag = new ConcurrentBag<string>();
        using var allReceived = new SemaphoreSlim(0, messageCount);

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Retry));

        await using var subscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            TestTopic,
            options,
            (message, _) =>
            {
                receivedBag.Add(Encoding.UTF8.GetString(message.Data.Span));
                allReceived.Release();
                return Task.FromResult(TopicResponseAction.Success);
            },
            cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

        for (var i = 1; i <= messageCount; i++)
        {
            await PublishMessageAsync(TestTopic, $"{{\"index\":{i}}}", cts.Token);
        }

        // Wait until all messages are received
        for (var i = 0; i < messageCount; i++)
        {
            await allReceived.WaitAsync(cts.Token);
        }

        Assert.Equal(messageCount, receivedBag.Count);
        for (var i = 1; i <= messageCount; i++)
        {
            Assert.Contains(receivedBag, body => body.Contains($"\"index\":{i}"));
        }
    }

    /// <summary>
    /// Verifies that returning <see cref="TopicResponseAction.Retry"/> causes the message to be
    /// redelivered and that the message is eventually acknowledged with Success.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_HandlerReturnsRetry_MessageIsRedelivered()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var deliveryCount = 0;
        var finallySucceeded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Retry));

        await using var subscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            TestTopic,
            options,
            (_, _) =>
            {
                var count = Interlocked.Increment(ref deliveryCount);
                if (count == 1)
                {
                    // Signal retry on first delivery
                    return Task.FromResult(TopicResponseAction.Retry);
                }

                // Succeed on subsequent deliveries
                finallySucceeded.TrySetResult(true);
                return Task.FromResult(TopicResponseAction.Success);
            },
            cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

        await PublishMessageAsync(TestTopic, """{"retryTest":true}""", cts.Token);

        var succeeded = await finallySucceeded.Task.WaitAsync(cts.Token);

        Assert.True(succeeded);
        Assert.True(deliveryCount >= 2, $"Expected at least 2 deliveries (retry + success), got {deliveryCount}.");
    }

    /// <summary>
    /// Verifies that returning <see cref="TopicResponseAction.Drop"/> means the message is not
    /// redelivered to the same subscription.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_HandlerReturnsDrop_MessageNotRedelivered()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var deliveryCount = 0;
        var firstDelivery = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Retry));

        await using var subscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            TestTopic,
            options,
            (_, _) =>
            {
                Interlocked.Increment(ref deliveryCount);
                firstDelivery.TrySetResult(true);
                return Task.FromResult(TopicResponseAction.Drop);
            },
            cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

        await PublishMessageAsync(TestTopic, """{"dropTest":true}""", cts.Token);

        // Wait for first (and ideally only) delivery
        await firstDelivery.Task.WaitAsync(cts.Token);

        // Allow a brief window for any unexpected redelivery
        await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);

        Assert.Equal(1, deliveryCount);
    }

    /// <summary>
    /// Verifies that when the message handler exceeds its configured timeout, the configured
    /// default <see cref="TopicResponseAction"/> is applied.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_MessageHandlerTimeout_DefaultPolicyDropIsApplied()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var deliveryCount = 0;
        var firstDelivery = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Configure a very short timeout; the handler will exceed it
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(
                TimeoutDuration: TimeSpan.FromMilliseconds(250),
                DefaultResponseAction: TopicResponseAction.Drop));

        await using var subscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            TestTopic,
            options,
            async (_, handlerToken) =>
            {
                Interlocked.Increment(ref deliveryCount);
                firstDelivery.TrySetResult(true);

                // Delay intentionally longer than the timeout to trigger the default policy
                await Task.Delay(TimeSpan.FromSeconds(5), handlerToken);
                return TopicResponseAction.Success;
            },
            cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

        await PublishMessageAsync(TestTopic, """{"timeoutTest":true}""", cts.Token);

        await firstDelivery.Task.WaitAsync(cts.Token);

        // Allow time for any redelivery that would indicate Drop was not applied
        await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);

        // With Drop as default policy on timeout, the message should not be redelivered
        Assert.Equal(1, deliveryCount);
    }

    /// <summary>
    /// Verifies that dropped messages are routed to the configured dead-letter topic.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_WithDeadLetterTopic_DroppedMessageRoutedToDeadLetter()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var deadLetterReceived = new TaskCompletionSource<TopicMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var primaryDropped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var primaryOptions = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Drop))
        {
            DeadLetterTopic = DeadLetterTopic
        };

        var deadLetterOptions = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Success));

        // Subscribe to the dead-letter topic first
        await using var deadLetterSubscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            DeadLetterTopic,
            deadLetterOptions,
            (message, _) =>
            {
                deadLetterReceived.TrySetResult(message);
                return Task.FromResult(TopicResponseAction.Success);
            },
            cts.Token);

        // Subscribe to the primary topic with a dead-letter topic configured
        await using var primarySubscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            TestTopic,
            primaryOptions,
            (_, _) =>
            {
                primaryDropped.TrySetResult(true);
                return Task.FromResult(TopicResponseAction.Drop);
            },
            cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

        var payload = """{"deadLetterTest":true}""";
        await PublishMessageAsync(TestTopic, payload, cts.Token);

        await primaryDropped.Task.WaitAsync(cts.Token);

        var deadLetterMessage = await deadLetterReceived.Task.WaitAsync(cts.Token);

        Assert.Equal(DeadLetterTopic, deadLetterMessage.Topic);
        Assert.Equal(PubSubName, deadLetterMessage.PubSubName);
    }

    /// <summary>
    /// Verifies that after the subscription is disposed, no further messages are delivered
    /// to the handler.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_AfterDispose_StopsReceivingMessages()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var deliveryCount = 0;
        var firstDelivery = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Success));

        var subscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            TestTopic,
            options,
            (_, _) =>
            {
                Interlocked.Increment(ref deliveryCount);
                firstDelivery.TrySetResult(true);
                return Task.FromResult(TopicResponseAction.Success);
            },
            cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

        // Publish first message and wait for receipt
        await PublishMessageAsync(TestTopic, """{"seq":1}""", cts.Token);
        await firstDelivery.Task.WaitAsync(cts.Token);

        // Dispose the subscription
        await subscription.DisposeAsync();

        // Capture delivery count immediately after disposal
        var countAfterDispose = deliveryCount;

        // Publish a second message; it should not reach the disposed handler
        await PublishMessageAsync(TestTopic, """{"seq":2}""", cts.Token);

        // Allow brief window for any erroneous delivery
        await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);

        Assert.Equal(countAfterDispose, deliveryCount);
    }

    /// <summary>
    /// Verifies that the subscription correctly propagates the message's topic metadata.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_ReceivedMessage_HasCorrectTopicMetadata()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var received = new TaskCompletionSource<TopicMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Success));

        await using var subscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            TestTopic,
            options,
            (message, _) =>
            {
                received.TrySetResult(message);
                return Task.FromResult(TopicResponseAction.Success);
            },
            cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

        await PublishMessageAsync(TestTopic, """{"meta":"check"}""", cts.Token);

        var message = await received.Task.WaitAsync(cts.Token);

        Assert.Equal(TestTopic, message.Topic);
        Assert.Equal(PubSubName, message.PubSubName);
        Assert.NotEmpty(message.Id);
        Assert.NotEmpty(message.SpecVersion);
        Assert.NotEmpty(message.Type);
    }

    /// <summary>
    /// Verifies that a subscription with <see cref="DaprSubscriptionOptions.MaximumQueuedMessages"/>
    /// configured creates a bounded channel that correctly processes messages without loss.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_WithMaximumQueuedMessages_ProcessesMessagesCorrectly()
    {
        const int messageCount = 5;
        using var cts = new CancellationTokenSource(TestTimeout);
        var received = new ConcurrentBag<int>();
        using var allReceived = new SemaphoreSlim(0, messageCount);

        // Bound the queue to 2 messages; more than 2 pending at any moment will cause back-pressure
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Retry))
        {
            MaximumQueuedMessages = 2
        };

        await using var subscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            TestTopic,
            options,
            async (message, _) =>
            {
                // Simulate brief processing delay to exercise the bounded channel
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                var body = Encoding.UTF8.GetString(message.Data.Span);
                if (System.Text.Json.JsonDocument.Parse(body).RootElement
                        .TryGetProperty("seq", out var seqElem))
                {
                    received.Add(seqElem.GetInt32());
                    allReceived.Release();
                }
                return TopicResponseAction.Success;
            },
            cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

        for (var i = 1; i <= messageCount; i++)
        {
            await PublishMessageAsync(TestTopic, $"{{\"seq\":{i}}}", cts.Token);
        }

        for (var i = 0; i < messageCount; i++)
        {
            await allReceived.WaitAsync(cts.Token);
        }

        Assert.Equal(messageCount, received.Count);
        for (var i = 1; i <= messageCount; i++)
        {
            Assert.Contains(i, received);
        }
    }

    // -------------------------------------------------------------------------
    // SubscribeAsync fault, cancellation, and reconnection integration tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// <summary>
    /// Verifies that when the Dapr sidecar's gRPC endpoint is unreachable, the subscription's
    /// Completion faults with a DaprException wrapping the underlying gRPC error.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_WhenSidecarUnavailable_FaultsCompletionWithDaprException()
    {
        // Point the gRPC endpoint at the sidecar's HTTP port — the TCP connection succeeds but
        // the HTTP/2 gRPC handshake fails quickly, producing an RpcException (not a timeout).
        var badClient = new DaprPublishSubscribeClientBuilder()
            .UseGrpcEndpoint($"http://127.0.0.1:{_harness!.DaprHttpPort}")
            .UseHttpEndpoint($"http://127.0.0.1:{_harness.DaprHttpPort}")
            .Build();

        try
        {
            var options = new DaprSubscriptionOptions(
                new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
            { MaximumCleanupTimeout = TimeSpan.FromSeconds(5) };

            // SubscribeAsync succeeds — gRPC returns a streaming call object lazily.
            // The connection failure surfaces when background tasks try to use the stream.
            var subscription = await badClient.SubscribeAsync(PubSubName, "unreachable-topic", options,
                (_, _) => Task.FromResult(TopicResponseAction.Success), CancellationToken.None);

            // The fault should surface on Completion as a DaprException.
            var observable = (IDaprSubscription)subscription;
            var ex = await Assert.ThrowsAsync<DaprException>(() =>
                observable.Completion.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None));

            Assert.Contains("unreachable-topic", ex.Message);
            Assert.Contains(PubSubName, ex.Message);

            await subscription.DisposeAsync();
        }
        finally
        {
            badClient.Dispose();
        }
    }

    /// <summary>
    /// Verifies that when the message handler throws, the fault surfaces on Completion
    /// as a DaprException (when no ErrorHandler is configured).
    /// </summary>
    [Fact]
    public async Task Completion_WhenMessageHandlerThrows_FaultsWithDaprException()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var handlerInvoked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Success));

        var subscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            "fault-handler-topic",
            options,
            (_, _) =>
            {
                handlerInvoked.TrySetResult(true);
                throw new InvalidOperationException("Handler failure");
            },
            cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
        await PublishMessageAsync("fault-handler-topic", """{"faultTest":true}""", cts.Token);
        await handlerInvoked.Task.WaitAsync(cts.Token);

        // Completion should fault with DaprException wrapping the handler's exception.
        var observable = (IDaprSubscription)subscription;
        var ex = await Assert.ThrowsAsync<DaprException>(() =>
            observable.Completion.WaitAsync(TimeSpan.FromSeconds(10), cts.Token));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("Handler failure", ex.InnerException!.Message);

        await subscription.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when an ErrorHandler is configured, it is invoked exactly once when
    /// the message handler throws, and Completion completes normally.
    /// </summary>
    [Fact]
    public async Task Completion_WhenErrorHandlerConfigured_InvokesHandlerOnceAndCompletesNormally()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var handlerInvoked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorHandlerCallCount = 0;
        var errorHandlerInvoked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Success))
        {
            ErrorHandler = ex =>
            {
                Interlocked.Increment(ref errorHandlerCallCount);
                errorHandlerInvoked.TrySetResult(true);
                return Task.CompletedTask;
            }
        };

        var subscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            "error-handler-topic",
            options,
            (_, _) =>
            {
                handlerInvoked.TrySetResult(true);
                throw new InvalidOperationException("Handler failure");
            },
            cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
        await PublishMessageAsync("error-handler-topic", """{"errorHandlerTest":true}""", cts.Token);
        await handlerInvoked.Task.WaitAsync(cts.Token);

        // Wait for the error handler to be invoked.
        await errorHandlerInvoked.Task.WaitAsync(TimeSpan.FromSeconds(10), cts.Token);
        Assert.Equal(1, errorHandlerCallCount);

        // Completion should complete normally (handler absorbed the fault).
        var observable = (IDaprSubscription)subscription;
        await observable.Completion.WaitAsync(TimeSpan.FromSeconds(10), cts.Token);
        Assert.True(observable.Completion.IsCompletedSuccessfully);

        await subscription.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when the ErrorHandler itself throws, Completion faults with an
    /// AggregateException containing both the original DaprException and the handler's exception.
    /// </summary>
    [Fact]
    public async Task Completion_WhenErrorHandlerThrows_FaultsWithAggregateException()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var handlerInvoked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Success))
        {
            ErrorHandler = _ => throw new InvalidOperationException("Error handler bug")
        };

        var subscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            "handler-throws-topic",
            options,
            (_, _) =>
            {
                handlerInvoked.TrySetResult(true);
                throw new InvalidOperationException("Handler failure");
            },
            cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
        await PublishMessageAsync("handler-throws-topic", """{"handlerThrowsTest":true}""", cts.Token);
        await handlerInvoked.Task.WaitAsync(cts.Token);

        var observable = (IDaprSubscription)subscription;
        var ex = await Assert.ThrowsAsync<AggregateException>(() =>
            observable.Completion.WaitAsync(TimeSpan.FromSeconds(10), cts.Token));
        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.IsType<DaprException>(ex.InnerExceptions[0]);
        Assert.IsType<InvalidOperationException>(ex.InnerExceptions[1]);
        Assert.Contains("Error handler bug", ex.InnerExceptions[1].Message);

        await subscription.DisposeAsync();
    }

    /// <summary>
    /// Verifies that when the subscription is cancelled, Completion completes without faulting.
    /// </summary>
    [Fact]
    public async Task Completion_WhenCancelled_CompletesWithoutFault()
    {
        using var subscribeCts = new CancellationTokenSource();
        var firstDelivery = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Success));

        var subscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            "cancellation-topic",
            options,
            (_, _) =>
            {
                firstDelivery.TrySetResult(true);
                return Task.FromResult(TopicResponseAction.Success);
            },
            subscribeCts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), subscribeCts.Token);
        await PublishMessageAsync("cancellation-topic", """{"cancelTest":true}""", subscribeCts.Token);
        await firstDelivery.Task.WaitAsync(TestTimeout, subscribeCts.Token);

        // Cancel the subscription token — Completion should complete without faulting.
        subscribeCts.Cancel();

        var observable = (IDaprSubscription)subscription;
        await observable.Completion.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        Assert.True(observable.Completion.IsCompletedSuccessfully);

        await subscription.DisposeAsync();
    }

    /// <summary>
    /// Verifies that after the subscription is cancelled and Completion completes, the caller can
    /// re-subscribe (on a new receiver) and receive new messages.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_AfterCancellation_AllowsResubscribeAndReceivesMessages()
    {
        using var subscribeCts = new CancellationTokenSource();
        var firstDelivery = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Success));

        // First subscription — will be cancelled.
        var firstSubscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            "cancel-first-topic",
            options,
            (_, _) =>
            {
                firstDelivery.TrySetResult(true);
                return Task.FromResult(TopicResponseAction.Success);
            },
            subscribeCts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), subscribeCts.Token);
        await PublishMessageAsync("cancel-first-topic", """{"first":true}""", subscribeCts.Token);
        await firstDelivery.Task.WaitAsync(TestTimeout, subscribeCts.Token);

        subscribeCts.Cancel();
        var firstObservable = (IDaprSubscription)firstSubscription;
        await firstObservable.Completion.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await firstSubscription.DisposeAsync();

        // Second subscription — use a different topic to avoid sidecar subscription cleanup races.
        var received = new TaskCompletionSource<TopicMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSubscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            "cancel-second-topic",
            options,
            (message, _) =>
            {
                received.TrySetResult(message);
                return Task.FromResult(TopicResponseAction.Success);
            },
            CancellationToken.None);

        await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None);
        await PublishMessageAsync("cancel-second-topic", """{"second":true}""", CancellationToken.None);

        var msg = await received.Task.WaitAsync(TestTimeout, CancellationToken.None);
        Assert.Contains("second", Encoding.UTF8.GetString(msg.Data.Span));

        await secondSubscription.DisposeAsync();
    }

    /// <summary>
    /// Verifies that after a handler fault causes Completion to fault, the caller can
    /// re-subscribe and receive new messages.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_AfterHandlerFault_AllowsResubscribeAndReceivesMessages()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var firstHandlerInvoked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Success));

        // First subscription — handler throws on first message.
        var firstSubscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            "resubscribe-after-fault-topic",
            options,
            (_, _) =>
            {
                firstHandlerInvoked.TrySetResult(true);
                throw new InvalidOperationException("Intentional fault");
            },
            cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
        await PublishMessageAsync("resubscribe-after-fault-topic", """{"first":true}""", cts.Token);
        await firstHandlerInvoked.Task.WaitAsync(cts.Token);

        // Wait for Completion to fault.
        var firstObservable = (IDaprSubscription)firstSubscription;
        await Assert.ThrowsAsync<DaprException>(() =>
            firstObservable.Completion.WaitAsync(TimeSpan.FromSeconds(10), cts.Token));
        await firstSubscription.DisposeAsync();

        // Second subscription — use a different topic to avoid sidecar subscription cleanup races.
        var received = new TaskCompletionSource<TopicMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSubscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            "resubscribe-after-fault-second-topic",
            options,
            (message, _) =>
            {
                received.TrySetResult(message);
                return Task.FromResult(TopicResponseAction.Success);
            },
            CancellationToken.None);

        await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None);
        await PublishMessageAsync("resubscribe-after-fault-second-topic", """{"second":true}""", CancellationToken.None);

        var msg = await received.Task.WaitAsync(TestTimeout, CancellationToken.None);
        Assert.Contains("second", Encoding.UTF8.GetString(msg.Data.Span));

        await secondSubscription.DisposeAsync();
    }

    /// <summary>
    /// Verifies that after the subscription is disposed, Completion is completed and
    /// a second DisposeAsync call is a no-op.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_CompletesSubscriptionAndIsIdempotent()
    {
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success));

        var subscription = await _pubSubClient!.SubscribeAsync(
            PubSubName,
            "dispose-idempotent-topic",
            options,
            (_, _) => Task.FromResult(TopicResponseAction.Success),
            CancellationToken.None);

        await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);

        await subscription.DisposeAsync();

        // Completion should be completed after disposal.
        var observable = (IDaprSubscription)subscription;
        Assert.True(observable.Completion.IsCompleted);

        // Second dispose should be a no-op (no exception).
        await subscription.DisposeAsync();
    }

    /// <summary>
    /// Publishes a message to the specified topic via the Dapr HTTP API.
    /// </summary>
    private async Task PublishMessageAsync(string topic, string jsonPayload, CancellationToken cancellationToken)
    {
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        var response = await _publisherHttpClient!.PostAsync(
            $"/v1.0/publish/{PubSubName}/{topic}",
            content,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
