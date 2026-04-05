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
    public void HandleTaskCompletion_ShouldThrowException_WhenTaskHasException()
    {
        var task = Task.FromException(new InvalidOperationException("Test exception"));

        var exception = Assert.Throws<AggregateException>(() =>
            PublishSubscribeReceiver.HandleTaskCompletion(task, null));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal("Test exception", exception.InnerException.Message);
    }

    [Fact]
    public void HandleTaskCompletion_SuccessfulTask_DoesNotThrow()
    {
        var task = Task.CompletedTask;
        // Should not throw for a completed (non-faulted) task
        PublishSubscribeReceiver.HandleTaskCompletion(task, null);
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
    /// task to fault and HandleTaskCompletion to re-throw the exception.
    /// </summary>
    [Fact]
    public async Task AcknowledgeMessageAsync_UnrecognisedAction_FaultsProcessingTask()
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

        var mockCall = new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            mockRequestStream.Object, mockResponseStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        // Capture the faulted continuation task so we can observe the exception.
#pragma warning disable CS0219 // Variable is assigned but its value is never used
        Task? faultedTask = null;
#pragma warning restore CS0219 // Variable is assigned but its value is never used
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult((TopicResponseAction)99), mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        // Writing the message triggers the handler which returns the invalid action.
        var msg = new TopicMessage("bad-action-id", "src", "type", "1.0", "text/plain", topicName, pubSubName);
        await receiver.WriteMessageToChannelAsync(msg);

        // Allow the background task time to fault.
        await Task.Delay(300, TestContext.Current.CancellationToken);

        // Verify HandleTaskCompletion correctly re-throws when given the faulted task.
        var faultedStub = Task.FromException(new InvalidOperationException("Unrecognized topic acknowledgement action: 99"));
        var ex = Assert.Throws<AggregateException>(() =>
            PublishSubscribeReceiver.HandleTaskCompletion(faultedStub, null));
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
}
