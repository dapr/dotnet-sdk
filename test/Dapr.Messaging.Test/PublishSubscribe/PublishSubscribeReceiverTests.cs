// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using System.Threading.Channels;
using Dapr;
using Dapr.AppCallback.Autogen.Grpc.v1;
using Dapr.Messaging.PublishSubscribe;
using Grpc.Core;
using Moq;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Messaging.Test.PublishSubscribe;

public class PublishSubscribeReceiverTests
{
    [Fact]
    public void SubscribeAsync_ShouldNotBlock()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options =
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
            {
                MaximumQueuedMessages = 100, MaximumCleanupTimeout = TimeSpan.FromSeconds(1)
            };
        
        var messageHandler = new TopicMessageHandler((message, token) => Task.FromResult(TopicResponseAction.Success));
        
        //Mock the daprClient
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        
        //Create a mock AsyncDuplexStreamingCall
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        var mockCall =
            new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
                mockRequestStream.Object, mockResponseStream.Object, Task.FromResult(new Metadata()),
                () => new Status(), () => new Metadata(), () => { });
        
        //Setup the mock to return the mock call
        mockDaprClient.Setup(client =>
                client.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);
        
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, mockDaprClient.Object);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var subscribeTask = receiver.SubscribeAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 100, "SubscribeAsync should return immediately and not block");
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options =
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
            {
                MaximumQueuedMessages = 100, MaximumCleanupTimeout = TimeSpan.FromSeconds(1)
            };
        
        var messageHandler = new TopicMessageHandler((message, token) => Task.FromResult(TopicResponseAction.Success));
        
        //Mock the daprClient
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var receiver =
            new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, mockDaprClient.Object);
        Assert.NotNull(receiver);
    }

    [Fact]
    public async Task ProcessTopicChannelMessagesAsync_ShouldProcessMessages()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options =
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
            {
                MaximumQueuedMessages = 100, MaximumCleanupTimeout = TimeSpan.FromSeconds(1)
            };
        
        // Mock the message handler
        var mockMessageHandler = new Mock<TopicMessageHandler>();
        mockMessageHandler
            .Setup(handler => handler(It.IsAny<TopicMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TopicResponseAction.Success);
        
        //Mock the daprClient
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        // Create a mock AsyncDuplexStreamingCall
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object, Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });

        //Set up the mock to return the mock call
        mockDaprClient.Setup(client => client.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, mockMessageHandler.Object, mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);
        
        //Write a message to the channel
        var message = new TopicMessage("id", "source", "type", "specVersion", "dataContentType", topicName, pubSubName);
        await receiver.WriteMessageToChannelAsync(message);

        //Allow some time for the message to be processed
        await Task.Delay(100, TestContext.Current.CancellationToken);

        mockMessageHandler.Verify(handler => handler(It.IsAny<TopicMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldProcessAcknowledgements()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(30), TopicResponseAction.Success))
        {
            MaximumQueuedMessages = 100 // Example value, adjust as needed
        };

        // Mock the message handler
        var mockMessageHandler = new Mock<TopicMessageHandler>();
        mockMessageHandler
            .Setup(handler => handler(It.IsAny<TopicMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TopicResponseAction.Success);

        // Mock the DaprClient
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();

        // Create a mock AsyncDuplexStreamingCall
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object, Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });

        // Setup the mock to return the mock call
        mockDaprClient.Setup(client => client.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, mockMessageHandler.Object, mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        // Use reflection to access the private acknowledgementsChannel and write an acknowledgement
        var acknowledgementsChannelField = typeof(PublishSubscribeReceiver).GetField("acknowledgementsChannel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (acknowledgementsChannelField is null)
            Assert.Fail();
        var acknowledgementsChannel = (Channel<PublishSubscribeReceiver.TopicAcknowledgement>)acknowledgementsChannelField.GetValue(receiver)!;

        var acknowledgement = new PublishSubscribeReceiver.TopicAcknowledgement("id", TopicEventResponse.Types.TopicEventResponseStatus.Success);
        await acknowledgementsChannel.Writer.WriteAsync(acknowledgement, TestContext.Current.CancellationToken);

        // Allow some time for the acknowledgement to be processed
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Verify that the request stream's WriteAsync method was called twice (initial request + acknowledgement)
        mockRequestStream.Verify(stream => stream.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DisposeAsync_ShouldCompleteChannels()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options =
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
            {
                MaximumQueuedMessages = 100, MaximumCleanupTimeout = TimeSpan.FromSeconds(1)
            };

        var messageHandler = new TopicMessageHandler((message, topic) => Task.FromResult(TopicResponseAction.Success));
        var daprClient = new Mock<P.Dapr.DaprClient>();
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, daprClient.Object);

        await receiver.DisposeAsync();
        
        Assert.True(receiver.TopicMessagesChannelCompletion.IsCompleted);
        Assert.True(receiver.AcknowledgementsChannelCompletion.IsCompleted);
    }
    
    [Fact]
    public async Task HandleTaskCompletion_ShouldInvokeErrorHandler_WhenTaskHasException()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        DaprException? receivedException = null;
        var options =
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
            {
                ErrorHandler = ex => { receivedException = ex; return Task.CompletedTask; }
            };

        var messageHandler = new TopicMessageHandler((message, token) => Task.FromResult(TopicResponseAction.Success));
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, mockDaprClient.Object);

        var task = Task.FromException(new InvalidOperationException("Test exception"));

        await receiver.HandleTaskCompletion(task, null);

        Assert.NotNull(receivedException);
        Assert.IsType<InvalidOperationException>(receivedException.InnerException);
        Assert.Equal("Test exception", receivedException.InnerException.Message);
        Assert.Contains("testTopic", receivedException.Message);
        Assert.Contains("testPubSub", receivedException.Message);
    }

    [Fact]
    public async Task HandleTaskCompletion_ShouldCacheForNextSubscribe_WhenNoErrorHandler()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options =
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success));

        var messageHandler = new TopicMessageHandler((message, token) => Task.FromResult(TopicResponseAction.Success));
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, mockDaprClient.Object);

        var task = Task.FromException(new InvalidOperationException("Test exception"));

        // With no ErrorHandler, HandleTaskCompletion must NOT throw synchronously (doing so would
        // fault an unobserved continuation Task — the original silent-failure bug). Instead it caches
        // the fault and the next SubscribeAsync call rethrows it.
        await receiver.HandleTaskCompletion(task, null);

        var exception = await Assert.ThrowsAsync<DaprException>(() => receiver.SubscribeAsync(TestContext.Current.CancellationToken));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("Test exception", exception.InnerException!.Message);
    }

    [Fact]
    public async Task HandleTaskCompletion_SuccessfulTask_DoesNotThrow()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success));
        var messageHandler = new TopicMessageHandler((message, token) => Task.FromResult(TopicResponseAction.Success));
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, mockDaprClient.Object);
        var task = Task.CompletedTask;
        // Should not throw for a completed (non-faulted) task
        await receiver.HandleTaskCompletion(task, null);
    }

    [Fact]
    public async Task SubscribeAsync_CalledTwice_SecondCallIsNoOp()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });

        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);
        await receiver.SubscribeAsync(TestContext.Current.CancellationToken); // second call is a no-op

        // SubscribeTopicEventsAlpha1 should only be called once (for the first SubscribeAsync)
        mockDaprClient.Verify(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FetchDataFromSidecarAsync_NullEventMessage_IsSkipped()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockMessageHandler = new Mock<TopicMessageHandler>();
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();

        // Return a response whose EventMessage oneof case is not set → EventMessage is null
        var nullEventMessageResponse = new P.SubscribeTopicEventsResponseAlpha1();
        mockResponseStream.SetupSequence(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockResponseStream.Setup(s => s.Current).Returns(nullEventMessageResponse);

        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });

        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            mockMessageHandler.Object, mockDaprClient.Object);
        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Handler must never be invoked because there were no real event messages
        mockMessageHandler.Verify(h => h(It.IsAny<TopicMessage>(), It.IsAny<CancellationToken>()), Times.Never);

        await receiver.DisposeAsync();
    }

    [Fact]
    public async Task FetchDataFromSidecarAsync_ValidEventMessage_IsDeliveredToHandler()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var handlerCalled = new TaskCompletionSource<TopicMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();

        var eventMessage = new TopicEventRequest
        {
            Id = "msg-1",
            Source = "test-source",
            Type = "test.type",
            SpecVersion = "1.0",
            DataContentType = "text/plain",
            Topic = topicName,
            PubsubName = pubSubName,
            Data = Google.Protobuf.ByteString.CopyFromUtf8("hello from sidecar"),
            // Extensions must be an initialized Struct; proto3 defaults it to null and
            // PublishSubscribeReceiver accesses .Extensions.Fields which would throw NRE.
            Extensions = new Google.Protobuf.WellKnownTypes.Struct()
        };
        var streamResponse = new P.SubscribeTopicEventsResponseAlpha1 { EventMessage = eventMessage };

        mockResponseStream.SetupSequence(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockResponseStream.Setup(s => s.Current).Returns(streamResponse);

        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });

        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (msg, _) => { handlerCalled.TrySetResult(msg); return Task.FromResult(TopicResponseAction.Success); },
            mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        var received = await handlerCalled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal("msg-1", received.Id);
        Assert.Equal(topicName, received.Topic);
        Assert.Equal(pubSubName, received.PubSubName);
        Assert.Equal("hello from sidecar", System.Text.Encoding.UTF8.GetString(received.Data.Span));

        await receiver.DisposeAsync();
    }

    [Fact]
    public async Task FetchDataFromSidecarAsync_WithMetadata_IncludesMetadataInInitialRequest()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var metadata = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        {
            Metadata = metadata,
            MaximumCleanupTimeout = TimeSpan.FromSeconds(1)
        };

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();

        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var capturedRequests = new List<P.SubscribeTopicEventsRequestAlpha1>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Callback<P.SubscribeTopicEventsRequestAlpha1, CancellationToken>((req, _) => capturedRequests.Add(req))
            .Returns(Task.CompletedTask);

        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });

        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);
        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.NotEmpty(capturedRequests);
        var initialRequest = capturedRequests[0].InitialRequest;
        Assert.NotNull(initialRequest);
        Assert.Equal("value1", initialRequest.Metadata["key1"]);
        Assert.Equal("value2", initialRequest.Metadata["key2"]);

        await receiver.DisposeAsync();
    }

    [Fact]
    public async Task ProcessTopicChannelMessages_RetryAction_WritesRetryAcknowledgement()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();

        var capturedRequests = new List<P.SubscribeTopicEventsRequestAlpha1>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Callback<P.SubscribeTopicEventsRequestAlpha1, CancellationToken>((req, _) => capturedRequests.Add(req))
            .Returns(Task.CompletedTask);

        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });

        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Retry), mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        var msg = new TopicMessage("ack-id", "src", "type", "1.0", "text/plain", topicName, pubSubName);
        await receiver.WriteMessageToChannelAsync(msg);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        // capturedRequests[0] is the initial subscription; capturedRequests[1] is the acknowledgement
        Assert.True(capturedRequests.Count >= 2);
        var ack = capturedRequests[1].EventProcessed;
        Assert.NotNull(ack);
        Assert.Equal("ack-id", ack.Id);
        Assert.Equal(TopicEventResponse.Types.TopicEventResponseStatus.Retry, ack.Status.Status);

        await receiver.DisposeAsync();
    }

    [Fact]
    public async Task ProcessTopicChannelMessages_DropAction_WritesDropAcknowledgement()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();

        var capturedRequests = new List<P.SubscribeTopicEventsRequestAlpha1>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Callback<P.SubscribeTopicEventsRequestAlpha1, CancellationToken>((req, _) => capturedRequests.Add(req))
            .Returns(Task.CompletedTask);

        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });

        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Drop), mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        var msg = new TopicMessage("drop-id", "src", "type", "1.0", "text/plain", topicName, pubSubName);
        await receiver.WriteMessageToChannelAsync(msg);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.True(capturedRequests.Count >= 2);
        var ack = capturedRequests[1].EventProcessed;
        Assert.NotNull(ack);
        Assert.Equal("drop-id", ack.Id);
        Assert.Equal(TopicEventResponse.Types.TopicEventResponseStatus.Drop, ack.Status.Status);

        await receiver.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_SecondCallIsNoOp()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        await receiver.DisposeAsync();
        // Second call should complete without error (isDisposed guard)
        await receiver.DisposeAsync();

        Assert.True(receiver.TopicMessagesChannelCompletion.IsCompleted);
        Assert.True(receiver.AcknowledgementsChannelCompletion.IsCompleted);
    }

    // -------------------------------------------------------------------------
    // WriteAcknowledgementToChannelAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// WriteAcknowledgementToChannelAsync must place the acknowledgement on the internal channel so that
    /// the background ProcessAcknowledgementChannelMessagesAsync loop writes it to the gRPC request stream.
    /// This test uses the internal helper directly, avoiding the reflection used in the original
    /// SubscribeAsync_ShouldProcessAcknowledgements test.
    /// </summary>
    [Fact]
    public async Task WriteAcknowledgementToChannelAsync_AcknowledgementIsSentToRequestStream()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var capturedRequests = new List<P.SubscribeTopicEventsRequestAlpha1>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Callback<P.SubscribeTopicEventsRequestAlpha1, CancellationToken>((req, _) => capturedRequests.Add(req))
            .Returns(Task.CompletedTask);

        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);
        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        var ack = new PublishSubscribeReceiver.TopicAcknowledgement(
            "direct-ack-id", Dapr.AppCallback.Autogen.Grpc.v1.TopicEventResponse.Types.TopicEventResponseStatus.Retry);
        await receiver.WriteAcknowledgementToChannelAsync(ack);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        // capturedRequests[0] is the initial subscription request; [1] is the acknowledgement
        Assert.True(capturedRequests.Count >= 2);
        var sentAck = capturedRequests[1].EventProcessed;
        Assert.NotNull(sentAck);
        Assert.Equal("direct-ack-id", sentAck.Id);
        Assert.Equal(Dapr.AppCallback.Autogen.Grpc.v1.TopicEventResponse.Types.TopicEventResponseStatus.Retry,
            sentAck.Status.Status);

        await receiver.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // ProcessTopicChannelMessagesAsync — Success action (Retry/Drop are existing tests)
    // -------------------------------------------------------------------------

    /// <summary>
    /// When the message handler returns Success, ProcessTopicChannelMessagesAsync must write a
    /// Success acknowledgement back to the gRPC request stream via AcknowledgeMessageAsync.
    /// </summary>
    [Fact]
    public async Task ProcessTopicChannelMessages_SuccessAction_WritesSuccessAcknowledgement()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();

        var capturedRequests = new List<P.SubscribeTopicEventsRequestAlpha1>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Callback<P.SubscribeTopicEventsRequestAlpha1, CancellationToken>((req, _) => capturedRequests.Add(req))
            .Returns(Task.CompletedTask);

        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);
        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        var msg = new TopicMessage("success-id", "src", "type", "1.0", "text/plain", topicName, pubSubName);
        await receiver.WriteMessageToChannelAsync(msg);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.True(capturedRequests.Count >= 2);
        var ack = capturedRequests[1].EventProcessed;
        Assert.NotNull(ack);
        Assert.Equal("success-id", ack.Id);
        Assert.Equal(Dapr.AppCallback.Autogen.Grpc.v1.TopicEventResponse.Types.TopicEventResponseStatus.Success,
            ack.Status.Status);

        await receiver.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // AcknowledgeMessageAsync — unrecognised action faults the background task
    // -------------------------------------------------------------------------

    /// <summary>
    /// When the message handler returns an unrecognised TopicResponseAction, AcknowledgeMessageAsync
    /// throws InvalidOperationException, which causes the ProcessTopicChannelMessagesAsync background
    /// task to fault. The fault must be cached by HandleTaskCompletion and rethrown on the next
    /// SubscribeAsync call.
    /// </summary>
    [Fact]
    public async Task AcknowledgeMessageAsync_UnrecognisedAction_FaultIsCachedForNextSubscribe()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // Keep the fetch loop parked so it doesn't race the ack-processing fault.
        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult((TopicResponseAction)99), mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        // Writing the message triggers the handler which returns the invalid action.
        var msg = new TopicMessage("bad-action-id", "src", "type", "1.0", "text/plain", topicName, pubSubName);
        await receiver.WriteMessageToChannelAsync(msg);

        // Wait for the background fault to be observed and cached by HandleTaskCompletion.
        await WaitForPendingBackgroundFaultAsync(receiver);

        // The next SubscribeAsync must surface the cached fault.
        var ex = await Assert.ThrowsAsync<DaprException>(() => receiver.SubscribeAsync(TestContext.Current.CancellationToken));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("99", ex.InnerException!.Message);

        await receiver.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // FetchDataFromSidecarAsync — DeadLetterTopic & multiple messages
    // -------------------------------------------------------------------------

    /// <summary>
    /// When DeadLetterTopic is set in DaprSubscriptionOptions, FetchDataFromSidecarAsync must include
    /// it in the initial subscription request sent to the sidecar.
    /// </summary>
    [Fact]
    public async Task FetchDataFromSidecarAsync_WithDeadLetterTopic_IncludesDeadLetterTopicInInitialRequest()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        const string deadLetterTopic = "my-dead-letter-topic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        {
            DeadLetterTopic = deadLetterTopic,
            MaximumCleanupTimeout = TimeSpan.FromSeconds(1)
        };

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var capturedRequests = new List<P.SubscribeTopicEventsRequestAlpha1>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Callback<P.SubscribeTopicEventsRequestAlpha1, CancellationToken>((req, _) => capturedRequests.Add(req))
            .Returns(Task.CompletedTask);

        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);
        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.NotEmpty(capturedRequests);
        Assert.Equal(deadLetterTopic, capturedRequests[0].InitialRequest.DeadLetterTopic);

        await receiver.DisposeAsync();
    }

    /// <summary>
    /// FetchDataFromSidecarAsync must deliver every event message from the sidecar response stream
    /// to the message handler, in order.
    /// </summary>
    [Fact]
    public async Task FetchDataFromSidecarAsync_MultipleMessages_AllDeliveredToHandlerInOrder()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        static P.SubscribeTopicEventsResponseAlpha1 MakeResponse(string id) =>
            new()
            {
                EventMessage = new Dapr.AppCallback.Autogen.Grpc.v1.TopicEventRequest
                {
                    Id = id, Source = "src", Type = "type", SpecVersion = "1.0",
                    DataContentType = "text/plain", Topic = topicName, PubsubName = pubSubName,
                    Data = Google.Protobuf.ByteString.Empty,
                    Extensions = new Google.Protobuf.WellKnownTypes.Struct()
                }
            };

        mockResponseStream.SetupSequence(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(true)
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockResponseStream.SetupSequence(s => s.Current)
            .Returns(MakeResponse("msg-1"))
            .Returns(MakeResponse("msg-2"))
            .Returns(MakeResponse("msg-3"));

        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        var receivedIds = new List<string>();
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (msg, _) => { lock (receivedIds) receivedIds.Add(msg.Id); return Task.FromResult(TopicResponseAction.Success); },
            mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.Equal(["msg-1", "msg-2", "msg-3"], receivedIds);

        await receiver.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // DisposeAsync — MaximumCleanupTimeout path
    // -------------------------------------------------------------------------

    /// <summary>
    /// When there are unprocessed acknowledgements and MaximumCleanupTimeout elapses before they can
    /// be drained, DisposeAsync must catch the resulting OperationCanceledException and still complete
    /// promptly rather than hanging.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_AcknowledgementsDrainTimeout_CompletesWithinMaximumCleanupTimeout()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";

        // Very short cleanup timeout to exercise the OperationCanceledException catch path.
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromMilliseconds(50) };

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();

        // Block indefinitely so the acknowledgement channel reader never completes.
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Returns<P.SubscribeTopicEventsRequestAlpha1, CancellationToken>(
                async (_, ct) => await Task.Delay(Timeout.Infinite, ct));
        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);
        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        // Queue an acknowledgement that will never be processed because WriteAsync blocks forever.
        await receiver.WriteAcknowledgementToChannelAsync(
            new PublishSubscribeReceiver.TopicAcknowledgement(
                "stuck-id", Dapr.AppCallback.Autogen.Grpc.v1.TopicEventResponse.Types.TopicEventResponseStatus.Success));

        // DisposeAsync should complete well within one second despite the stuck ack.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await receiver.DisposeAsync();
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"DisposeAsync took {sw.ElapsedMilliseconds} ms — expected to honour MaximumCleanupTimeout of 50 ms.");
        Assert.True(receiver.TopicMessagesChannelCompletion.IsCompleted);
        Assert.True(receiver.AcknowledgementsChannelCompletion.IsCompleted);
    }

    // -------------------------------------------------------------------------
    // Background-fault surfacing — repro tests that drive the real ContinueWith wiring.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Polls until the receiver has cached a background fault or the timeout elapses. Used by tests
    /// that need to wait for HandleTaskCompletion to run through the real continuation chain.
    /// </summary>
    private static async Task WaitForPendingBackgroundFaultAsync(PublishSubscribeReceiver receiver, int timeoutMs = 2000)
    {
        var deadline = Environment.TickCount + timeoutMs;
        while (!receiver.HasPendingBackgroundFault)
        {
            if (Environment.TickCount >= deadline)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Background fault was not cached within {timeoutMs} ms — HandleTaskCompletion did not run or did not store the fault.");
            }
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Builds a mock that returns a DaprClient whose response stream faults on MoveNext with the
    /// supplied exception. Drives FetchDataFromSidecarAsync into a faulted state, exercising the
    /// real ContinueWith -> HandleTaskCompletion chain.
    /// </summary>
    private static Mock<P.Dapr.DaprClient> CreateMockDaprClientWithFaultingResponseStream(Exception faultWith, int? callCount = null)
    {
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        mockResponseStream
            .Setup(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(Task.FromException<bool>(faultWith));

        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);
        return mockDaprClient;
    }

    /// <summary>
    /// Repro for the original bug: when the response stream faults in the background (sidecar became
    /// unavailable mid-subscription) AND no ErrorHandler is configured, the fault must not be lost.
    /// It must be cached on the receiver and rethrown on the next SubscribeAsync call.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_WhenBackgroundFetchFaults_WithoutHandler_NextSubscribeRethrows()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var rpcFault = new RpcException(new Status(StatusCode.Unavailable, "sidecar went away"));
        var mockDaprClient = CreateMockDaprClientWithFaultingResponseStream(rpcFault);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        // First subscribe succeeds — the gRPC call itself is established. The fault happens in the
        // background FetchDataFromSidecarAsync loop when it tries to read the first message.
        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        await WaitForPendingBackgroundFaultAsync(receiver);

        var ex = await Assert.ThrowsAsync<DaprException>(() => receiver.SubscribeAsync(TestContext.Current.CancellationToken));
        Assert.IsType<RpcException>(ex.InnerException);
        Assert.Contains("testTopic", ex.Message);
        Assert.Contains("testPubSub", ex.Message);

        await receiver.DisposeAsync();
    }

    /// <summary>
    /// When an ErrorHandler IS configured, a single sidecar failure must invoke it exactly once —
    /// not 2–3 times, even though three background continuations all route to HandleTaskCompletion.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_WhenBackgroundFetchFaults_InvokesErrorHandlerExactlyOnce()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";

        var handlerInvoked = new TaskCompletionSource<DaprException>(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        {
            MaximumCleanupTimeout = TimeSpan.FromSeconds(1),
            ErrorHandler = ex =>
            {
                Interlocked.Increment(ref invocationCount);
                handlerInvoked.TrySetResult(ex);
                return Task.CompletedTask;
            }
        };

        var rpcFault = new RpcException(new Status(StatusCode.Unavailable, "sidecar went away"));
        var mockDaprClient = CreateMockDaprClientWithFaultingResponseStream(rpcFault);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        var received = await handlerInvoked.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.IsType<RpcException>(received.InnerException);

        // Grace period for any sibling continuations to (incorrectly) invoke the handler.
        await Task.Delay(150, TestContext.Current.CancellationToken);

        Assert.Equal(1, Volatile.Read(ref invocationCount));

        await receiver.DisposeAsync();
    }

    /// <summary>
    /// After a background fault with no handler, the caller can re-subscribe to recover. The second
    /// subscribe call must rethrow the cached fault; a third call must then succeed and establish a
    /// fresh stream (verifying that the previous stream was properly reset).
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_AfterBackgroundFault_NextSubscribeRecreatesStream()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        // First call: response stream faults on MoveNext. Second call: a healthy stream that parks.
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();

        var faultyRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        faultyRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var faultyResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        faultyResponseStream
            .Setup(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(Task.FromException<bool>(new RpcException(new Status(StatusCode.Unavailable, "boom"))));
        var faultyCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            faultyRequestStream.Object, faultyResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });

        var healthyRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        healthyRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var healthyResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        healthyResponseStream
            .Setup(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // no messages; stream completes cleanly
        var healthyCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            healthyRequestStream.Object, healthyResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });

        mockDaprClient.SetupSequence(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(faultyCall)
            .Returns(healthyCall);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);
        await WaitForPendingBackgroundFaultAsync(receiver);

        // Second call: caller observes the cached fault.
        await Assert.ThrowsAsync<DaprException>(() => receiver.SubscribeAsync(TestContext.Current.CancellationToken));

        // Third call: the fault has been drained; a fresh stream should be obtained from the client.
        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        mockDaprClient.Verify(
            c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        await receiver.DisposeAsync();
    }

    /// <summary>
    /// A background task that completes via OperationCanceledException must not be treated as an
    /// error: no fault should be cached and the next SubscribeAsync should not rethrow.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_WhenBackgroundFetchCancelled_DoesNotCachePendingFault()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockDaprClient = CreateMockDaprClientWithFaultingResponseStream(new OperationCanceledException("cancelled"));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        // Give HandleTaskCompletion a chance to run and reset state. Since this is a clean cancellation,
        // no fault should be cached.
        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.False(receiver.HasPendingBackgroundFault);

        // A subsequent SubscribeAsync must not rethrow anything cancellation-related — it should try
        // to re-establish the subscription (which will fault again here, but that second fault path
        // is orthogonal to this assertion). We only verify that the pending-fault rethrow does NOT fire.
        try
        {
            await receiver.SubscribeAsync(TestContext.Current.CancellationToken);
        }
        catch (DaprException)
        {
            // A secondary fault from the new subscribe cycle is acceptable — we only wanted to prove
            // that the cancellation did not get cached as a pending fault to rethrow at the top of
            // SubscribeAsync (which would have been an OperationCanceledException, not DaprException).
        }

        await receiver.DisposeAsync();
    }

    /// <summary>
    /// When the user-supplied ErrorHandler itself throws, the original fault plus the handler fault
    /// must both be cached and surfaced together on the next SubscribeAsync as an AggregateException.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_WhenErrorHandlerThrows_CachesCombinedFaultForNextSubscribe()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";

        var handlerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        {
            MaximumCleanupTimeout = TimeSpan.FromSeconds(1),
            ErrorHandler = _ =>
            {
                handlerInvoked.TrySetResult();
                throw new InvalidOperationException("handler bug");
            }
        };

        var rpcFault = new RpcException(new Status(StatusCode.Unavailable, "sidecar down"));
        var mockDaprClient = CreateMockDaprClientWithFaultingResponseStream(rpcFault);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);
        await handlerInvoked.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await WaitForPendingBackgroundFaultAsync(receiver);

        var ex = await Assert.ThrowsAsync<AggregateException>(() =>
            receiver.SubscribeAsync(TestContext.Current.CancellationToken));

        Assert.Contains(ex.InnerExceptions, e => e is DaprException);
        Assert.Contains(ex.InnerExceptions, e => e is InvalidOperationException { Message: "handler bug" });

        await receiver.DisposeAsync();
    }
}
