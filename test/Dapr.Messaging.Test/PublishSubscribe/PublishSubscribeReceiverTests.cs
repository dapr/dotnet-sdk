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
    public void HandleTaskCompletion_ShouldInvokeErrorHandler_WhenTaskHasException()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        DaprException? receivedException = null;
        var options =
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
            {
                ErrorHandler = ex => receivedException = ex
            };

        var messageHandler = new TopicMessageHandler((message, token) => Task.FromResult(TopicResponseAction.Success));
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, mockDaprClient.Object);

        var task = Task.FromException(new InvalidOperationException("Test exception"));

        receiver.HandleTaskCompletion(task, null);

        Assert.NotNull(receivedException);
        Assert.IsType<InvalidOperationException>(receivedException.InnerException);
        Assert.Equal("Test exception", receivedException.InnerException.Message);
        Assert.Contains("testTopic", receivedException.Message);
        Assert.Contains("testPubSub", receivedException.Message);
    }

    [Fact]
    public void HandleTaskCompletion_ShouldNotThrow_WhenNoErrorHandler()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options =
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success));

        var messageHandler = new TopicMessageHandler((message, token) => Task.FromResult(TopicResponseAction.Success));
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, mockDaprClient.Object);

        var task = Task.FromException(new InvalidOperationException("Test exception"));

        var exception = Record.Exception(() => receiver.HandleTaskCompletion(task, null));

        Assert.Null(exception);
    }

    [Fact]
    public void HandleTaskCompletion_ShouldNotThrow_WhenErrorHandlerThrows()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options =
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
            {
                ErrorHandler = _ => throw new InvalidOperationException("Handler failed")
            };

        var messageHandler = new TopicMessageHandler((message, token) => Task.FromResult(TopicResponseAction.Success));
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, mockDaprClient.Object);

        var task = Task.FromException(new InvalidOperationException("Test exception"));

        var exception = Record.Exception(() => receiver.HandleTaskCompletion(task, null));

        Assert.Null(exception);
    }

    [Fact]
    public void HandleTaskCompletion_ShouldNotInvokeErrorHandler_WhenTaskSucceeded()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var handlerInvoked = false;
        var options =
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
            {
                ErrorHandler = _ => handlerInvoked = true
            };

        var messageHandler = new TopicMessageHandler((message, token) => Task.FromResult(TopicResponseAction.Success));
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, mockDaprClient.Object);

        receiver.HandleTaskCompletion(Task.CompletedTask, null);

        Assert.False(handlerInvoked);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldThrowDaprException_WhenSidecarUnavailable()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options =
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success));

        var messageHandler = new TopicMessageHandler((message, token) => Task.FromResult(TopicResponseAction.Success));
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();

        // Setup the mock to throw RpcException (simulating unavailable sidecar)
        mockDaprClient.Setup(client =>
                client.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "Connect Failed")));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, mockDaprClient.Object);

        var exception = await Assert.ThrowsAsync<DaprException>(() => receiver.SubscribeAsync(TestContext.Current.CancellationToken));

        Assert.Contains("testTopic", exception.Message);
        Assert.Contains("testPubSub", exception.Message);
        Assert.IsType<RpcException>(exception.InnerException);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldAllowRetry_AfterSidecarFailure()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options =
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success));

        var messageHandler = new TopicMessageHandler((message, token) => Task.FromResult(TopicResponseAction.Success));
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();

        // First call throws RpcException
        mockDaprClient.Setup(client =>
                client.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "Connect Failed")));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, mockDaprClient.Object);

        await Assert.ThrowsAsync<DaprException>(() => receiver.SubscribeAsync(TestContext.Current.CancellationToken));

        // Now setup the mock to succeed on retry
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        var mockCall =
            new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
                mockRequestStream.Object, mockResponseStream.Object, Task.FromResult(new Metadata()),
                () => new Status(), () => new Metadata(), () => { });

        mockDaprClient.Setup(client =>
                client.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        // Second call should succeed (hasInitialized was reset)
        var retryException = await Record.ExceptionAsync(() => receiver.SubscribeAsync(TestContext.Current.CancellationToken));
        Assert.Null(retryException);

        // Verify the client was called twice
        mockDaprClient.Verify(client =>
            client.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SubscribeAsync_ShouldResetHasInitialized_WhenNonRpcExceptionThrown()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options =
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success));

        var messageHandler = new TopicMessageHandler((message, token) => Task.FromResult(TopicResponseAction.Success));
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();

        // First call throws a non-RPC exception
        mockDaprClient.Setup(client =>
                client.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Throws(new ObjectDisposedException("client"));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, messageHandler, mockDaprClient.Object);

        await Assert.ThrowsAsync<ObjectDisposedException>(() => receiver.SubscribeAsync(TestContext.Current.CancellationToken));

        // Now setup the mock to succeed on retry
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        var mockCall =
            new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
                mockRequestStream.Object, mockResponseStream.Object, Task.FromResult(new Metadata()),
                () => new Status(), () => new Metadata(), () => { });

        mockDaprClient.Setup(client =>
                client.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        // Second call should succeed (hasInitialized was reset)
        var retryException = await Record.ExceptionAsync(() => receiver.SubscribeAsync(TestContext.Current.CancellationToken));
        Assert.Null(retryException);
    }
}
