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
