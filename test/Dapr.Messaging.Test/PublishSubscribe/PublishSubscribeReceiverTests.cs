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
}
