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

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var mockCall = CreateMockCall();

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

        var mockMessageHandler = new Mock<TopicMessageHandler>();
        mockMessageHandler
            .Setup(handler => handler(It.IsAny<TopicMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TopicResponseAction.Success);

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(client => client.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall());

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, mockMessageHandler.Object, mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        var message = new TopicMessage("id", "source", "type", "specVersion", "dataContentType", topicName, pubSubName);
        await receiver.WriteMessageToChannelAsync(message);

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
            MaximumQueuedMessages = 100
        };

        var mockMessageHandler = new Mock<TopicMessageHandler>();
        mockMessageHandler
            .Setup(handler => handler(It.IsAny<TopicMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TopicResponseAction.Success);

        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        var mockCall = CreateMockCall(mockRequestStream, mockResponseStream);

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(client => client.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(mockCall);

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options, mockMessageHandler.Object, mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        var acknowledgementsChannelField = typeof(PublishSubscribeReceiver).GetField("_acknowledgementsChannel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (acknowledgementsChannelField is null)
            Assert.Fail();
        var acknowledgementsChannel = (Channel<PublishSubscribeReceiver.TopicAcknowledgement>)acknowledgementsChannelField.GetValue(receiver)!;

        var acknowledgement = new PublishSubscribeReceiver.TopicAcknowledgement("id", TopicEventResponse.Types.TopicEventResponseStatus.Success);
        await acknowledgementsChannel.Writer.WriteAsync(acknowledgement, TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken);

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
    public async Task SubscribeAsync_ShouldThrowDaprException_WhenSidecarUnavailable()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success));

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "Connection refused")));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        var ex = await Assert.ThrowsAsync<DaprException>(() => receiver.SubscribeAsync(CancellationToken.None));
        Assert.IsType<RpcException>(ex.InnerException);
        Assert.Contains("testTopic", ex.Message);
        Assert.Contains("testPubSub", ex.Message);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldAllowRetry_AfterSidecarFailure()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success));

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var callCount = 0;
        var mockCall = CreateMockCall();

        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new RpcException(new Status(StatusCode.Unavailable, "Connection refused"));
                return mockCall;
            });

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        await Assert.ThrowsAsync<DaprException>(() => receiver.SubscribeAsync(CancellationToken.None));

        await receiver.SubscribeAsync(CancellationToken.None);
        Assert.Equal(2, callCount);
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
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall());

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);
        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

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
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        var nullEventMessageResponse = new P.SubscribeTopicEventsResponseAlpha1();
        mockResponseStream.SetupSequence(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockResponseStream.Setup(s => s.Current).Returns(nullEventMessageResponse);

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(responseStream: mockResponseStream));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            mockMessageHandler.Object, mockDaprClient.Object);
        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

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
            Extensions = new Google.Protobuf.WellKnownTypes.Struct()
        };
        var streamResponse = new P.SubscribeTopicEventsResponseAlpha1 { EventMessage = eventMessage };

        mockResponseStream.SetupSequence(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        mockResponseStream.Setup(s => s.Current).Returns(streamResponse);

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(responseStream: mockResponseStream));

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

        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var capturedRequests = new List<P.SubscribeTopicEventsRequestAlpha1>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Callback<P.SubscribeTopicEventsRequestAlpha1, CancellationToken>((req, _) => capturedRequests.Add(req))
            .Returns(Task.CompletedTask);

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(mockRequestStream, mockResponseStream));

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

        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var capturedRequests = new List<P.SubscribeTopicEventsRequestAlpha1>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Callback<P.SubscribeTopicEventsRequestAlpha1, CancellationToken>((req, _) => capturedRequests.Add(req))
            .Returns(Task.CompletedTask);

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(mockRequestStream));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Retry), mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        var msg = new TopicMessage("ack-id", "src", "type", "1.0", "text/plain", topicName, pubSubName);
        await receiver.WriteMessageToChannelAsync(msg);

        await Task.Delay(200, TestContext.Current.CancellationToken);

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

        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var capturedRequests = new List<P.SubscribeTopicEventsRequestAlpha1>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Callback<P.SubscribeTopicEventsRequestAlpha1, CancellationToken>((req, _) => capturedRequests.Add(req))
            .Returns(Task.CompletedTask);

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(mockRequestStream));

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
        await receiver.DisposeAsync();

        Assert.True(receiver.TopicMessagesChannelCompletion.IsCompleted);
        Assert.True(receiver.AcknowledgementsChannelCompletion.IsCompleted);
    }

    [Fact]
    public async Task WriteAcknowledgementToChannelAsync_AcknowledgementIsSentToRequestStream()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var capturedRequests = new List<P.SubscribeTopicEventsRequestAlpha1>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Callback<P.SubscribeTopicEventsRequestAlpha1, CancellationToken>((req, _) => capturedRequests.Add(req))
            .Returns(Task.CompletedTask);

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(mockRequestStream, mockResponseStream));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);
        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        var ack = new PublishSubscribeReceiver.TopicAcknowledgement(
            "direct-ack-id", TopicEventResponse.Types.TopicEventResponseStatus.Retry);
        await receiver.WriteAcknowledgementToChannelAsync(ack);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.True(capturedRequests.Count >= 2);
        var sentAck = capturedRequests[1].EventProcessed;
        Assert.NotNull(sentAck);
        Assert.Equal("direct-ack-id", sentAck.Id);
        Assert.Equal(TopicEventResponse.Types.TopicEventResponseStatus.Retry, sentAck.Status.Status);

        await receiver.DisposeAsync();
    }

    [Fact]
    public async Task ProcessTopicChannelMessages_SuccessAction_WritesSuccessAcknowledgement()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var capturedRequests = new List<P.SubscribeTopicEventsRequestAlpha1>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Callback<P.SubscribeTopicEventsRequestAlpha1, CancellationToken>((req, _) => capturedRequests.Add(req))
            .Returns(Task.CompletedTask);

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(mockRequestStream));

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
        Assert.Equal(TopicEventResponse.Types.TopicEventResponseStatus.Success, ack.Status.Status);

        await receiver.DisposeAsync();
    }

    [Fact]
    public async Task AcknowledgeMessageAsync_UnrecognisedAction_FaultsCompletion()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall());

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult((TopicResponseAction)99), mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        var msg = new TopicMessage("bad-action-id", "src", "type", "1.0", "text/plain", topicName, pubSubName);
        await receiver.WriteMessageToChannelAsync(msg);

        // Completion should fault with a DaprException wrapping the InvalidOperationException.
        var ex = await Assert.ThrowsAsync<DaprException>(() => receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Contains("99", ex.InnerException!.Message);

        await receiver.DisposeAsync();
    }

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

        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var capturedRequests = new List<P.SubscribeTopicEventsRequestAlpha1>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Callback<P.SubscribeTopicEventsRequestAlpha1, CancellationToken>((req, _) => capturedRequests.Add(req))
            .Returns(Task.CompletedTask);

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(mockRequestStream, mockResponseStream));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);
        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.NotEmpty(capturedRequests);
        Assert.Equal(deadLetterTopic, capturedRequests[0].InitialRequest.DeadLetterTopic);

        await receiver.DisposeAsync();
    }

    [Fact]
    public async Task FetchDataFromSidecarAsync_MultipleMessages_AllDeliveredToHandlerInOrder()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        static P.SubscribeTopicEventsResponseAlpha1 MakeResponse(string id) =>
            new()
            {
                EventMessage = new TopicEventRequest
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

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(mockRequestStream, mockResponseStream));

        var receivedIds = new List<string>();
        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (msg, _) => { lock (receivedIds) receivedIds.Add(msg.Id); return Task.FromResult(TopicResponseAction.Success); },
            mockDaprClient.Object);

        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.Equal(["msg-1", "msg-2", "msg-3"], receivedIds);

        await receiver.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_AcknowledgementsDrainTimeout_CompletesWithinMaximumCleanupTimeout()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";

        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromMilliseconds(50) };

        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();

        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Returns<P.SubscribeTopicEventsRequestAlpha1, CancellationToken>(
                async (_, ct) => await Task.Delay(Timeout.Infinite, ct));
        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(mockRequestStream, mockResponseStream));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);
        await receiver.SubscribeAsync(TestContext.Current.CancellationToken);

        await receiver.WriteAcknowledgementToChannelAsync(
            new PublishSubscribeReceiver.TopicAcknowledgement(
                "stuck-id", TopicEventResponse.Types.TopicEventResponseStatus.Success));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await receiver.DisposeAsync();
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"DisposeAsync took {sw.ElapsedMilliseconds} ms — expected to honour MaximumCleanupTimeout of 50 ms.");
        Assert.True(receiver.TopicMessagesChannelCompletion.IsCompleted);
        Assert.True(receiver.AcknowledgementsChannelCompletion.IsCompleted);
    }

    // -------------------------------------------------------------------------
    // Background-fault observation via Completion (supervisor pattern)
    // -------------------------------------------------------------------------

    /// <summary>
    /// When a background task faults and no ErrorHandler is configured, Completion faults with DaprException.
    /// </summary>
    [Fact]
    public async Task Completion_WhenBackgroundFaults_WithoutHandler_FaultsWithDaprException()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromException<bool>(new InvalidOperationException("background fetch failed")));

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(responseStream: mockResponseStream));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        await receiver.SubscribeAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DaprException>(() => receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("background fetch failed", ex.InnerException!.Message);
        Assert.Contains(topicName, ex.Message);
        Assert.Contains(pubSubName, ex.Message);

        await receiver.DisposeAsync();
    }

    /// <summary>
    /// When a background task faults and an ErrorHandler is configured, the handler is invoked
    /// exactly once and Completion completes normally.
    /// </summary>
    [Fact]
    public async Task Completion_WhenBackgroundFaults_WithHandler_InvokesHandlerOnceAndCompletes()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var handlerCalls = 0;
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        {
            MaximumCleanupTimeout = TimeSpan.FromSeconds(1),
            ErrorHandler = _ => { Interlocked.Increment(ref handlerCalls); return Task.CompletedTask; }
        };

        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromException<bool>(new InvalidOperationException("background fetch failed")));

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(responseStream: mockResponseStream));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        await receiver.SubscribeAsync(CancellationToken.None);

        // Completion should complete normally (handler absorbed the error).
        await receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(1, handlerCalls);

        await receiver.DisposeAsync();
    }

    /// <summary>
    /// After a background fault, hasInitialized is reset so the caller can re-subscribe.
    /// </summary>
    [Fact]
    public async Task Completion_AfterBackgroundFault_AllowsResubscribe()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var callCount = 0;
        mockRequestStream
            .Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                var responseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
                if (callCount == 1)
                {
                    responseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>()))
                        .Returns(() => Task.FromException<bool>(new InvalidOperationException("fetch failed")));
                }
                else
                {
                    responseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);
                }
                return CreateMockCall(mockRequestStream, responseStream);
            });

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        // First subscribe starts background tasks; the fetch faults.
        await receiver.SubscribeAsync(CancellationToken.None);

        // Wait for Completion to fault.
        await Assert.ThrowsAsync<DaprException>(() => receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        // Second subscribe should succeed (hasInitialized was reset by the supervisor).
        await receiver.SubscribeAsync(CancellationToken.None);

        mockDaprClient.Verify(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()), Times.Exactly(2));

        await receiver.DisposeAsync();
    }

    /// <summary>
    /// When the background task is cancelled (not faulted), Completion does not fault.
    /// </summary>
    [Fact]
    public async Task Completion_WhenBackgroundCancelled_DoesNotFault()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromException<bool>(new OperationCanceledException()));

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(responseStream: mockResponseStream));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        await receiver.SubscribeAsync(CancellationToken.None);

        // Completion should not fault — cancellation is a clean shutdown.
        await receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(receiver.Completion.IsCompletedSuccessfully);

        await receiver.DisposeAsync();
    }

    /// <summary>
    /// When the user-supplied ErrorHandler throws, Completion faults with an AggregateException
    /// containing both the original DaprException and the handler's exception.
    /// </summary>
    [Fact]
    public async Task Completion_WhenErrorHandlerThrows_FaultsWithCombinedAggregateException()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        {
            MaximumCleanupTimeout = TimeSpan.FromSeconds(1),
            ErrorHandler = _ => throw new InvalidOperationException("handler bug")
        };

        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromException<bool>(new InvalidOperationException("background fetch failed")));

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(responseStream: mockResponseStream));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        await receiver.SubscribeAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<AggregateException>(() => receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.IsType<DaprException>(ex.InnerExceptions[0]);
        Assert.IsType<InvalidOperationException>(ex.InnerExceptions[1]);
        Assert.Contains("handler bug", ex.InnerExceptions[1].Message);

        await receiver.DisposeAsync();
    }

    /// <summary>
    /// When multiple background tasks fault with different non-cancellation exceptions, the DaprException's
    /// InnerException is an AggregateException containing all of them.
    /// </summary>
    [Fact]
    public async Task Completion_WhenMultipleTasksFault_SurfacesAggregateException()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockRequestStream = new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();

        // Coordinator: blocks MoveNext (second call) until the handler has thrown, so both tasks
        // fault with non-OCE exceptions before the CTS cancellation propagates.
        var handlerFaulted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var moveNextCallCount = 0;

        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var callNum = Interlocked.Increment(ref moveNextCallCount);
                if (callNum == 1)
                    return Task.FromResult(true);
                // Second call: wait for the handler to fault, then throw a different exception.
                return WaitAndThrowAsync(handlerFaulted.Task, "fetch error");
            });

        var eventMessage = new TopicEventRequest
        {
            Id = "msg-1", Source = "src", Type = "type", SpecVersion = "1.0",
            DataContentType = "text/plain", Topic = topicName, PubsubName = pubSubName,
            Data = Google.Protobuf.ByteString.Empty,
            Extensions = new Google.Protobuf.WellKnownTypes.Struct()
        };
        mockResponseStream.Setup(s => s.Current)
            .Returns(new P.SubscribeTopicEventsResponseAlpha1 { EventMessage = eventMessage });

        mockRequestStream.Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(mockRequestStream, mockResponseStream));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) =>
            {
                handlerFaulted.TrySetResult(true);
                throw new InvalidOperationException("handler error");
            },
            mockDaprClient.Object);

        await receiver.SubscribeAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DaprException>(() => receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.IsType<AggregateException>(ex.InnerException);
        var agg = (AggregateException)ex.InnerException;
        Assert.Equal(2, agg.InnerExceptions.Count);
        Assert.Contains(agg.InnerExceptions, e => e is InvalidOperationException && e.Message == "fetch error");
        Assert.Contains(agg.InnerExceptions, e => e is InvalidOperationException && e.Message == "handler error");

        await receiver.DisposeAsync();
    }

    /// <summary>
    /// After the subscription is cancelled (not disposed), hasInitialized is reset and the caller can re-subscribe.
    /// </summary>
    [Fact]
    public async Task Completion_AfterCancellation_AllowsResubscribe()
    {
        const string pubSubName = "testPubSub";
        const string topicName = "testTopic";
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success))
        { MaximumCleanupTimeout = TimeSpan.FromSeconds(1) };

        var mockResponseStream = new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();
        mockResponseStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) =>
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                ct.Register(() => tcs.TrySetCanceled(ct));
                return tcs.Task;
            });

        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        mockDaprClient.Setup(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()))
            .Returns(CreateMockCall(responseStream: mockResponseStream));

        var receiver = new PublishSubscribeReceiver(pubSubName, topicName, options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        using var cts = new CancellationTokenSource();
        await receiver.SubscribeAsync(cts.Token);

        // Cancel — all three tasks get OCE, Completion completes without faulting.
        cts.Cancel();
        await receiver.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(receiver.Completion.IsCompletedSuccessfully);

        // hasInitialized was reset in finally — re-subscribe should work.
        await receiver.SubscribeAsync(CancellationToken.None);
        mockDaprClient.Verify(c => c.SubscribeTopicEventsAlpha1(null, null, It.IsAny<CancellationToken>()), Times.Exactly(2));

        await receiver.DisposeAsync();
    }

    /// <summary>
    /// Calling SubscribeAsync after DisposeAsync throws ObjectDisposedException.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(TimeSpan.FromSeconds(5), TopicResponseAction.Success));
        var mockDaprClient = new Mock<P.Dapr.DaprClient>();
        var receiver = new PublishSubscribeReceiver("pubsub", "topic", options,
            (_, _) => Task.FromResult(TopicResponseAction.Success), mockDaprClient.Object);

        await receiver.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => receiver.SubscribeAsync(CancellationToken.None));
    }

    /// <summary>
    /// Blocks until the handler has faulted, then throws — used to coordinate two simultaneous faults.
    /// </summary>
    private static async Task<bool> WaitAndThrowAsync(Task blocker, string message)
    {
        await blocker.ConfigureAwait(false);
        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Helper: creates a mock AsyncDuplexStreamingCall. Defaults (WriteAsync→Completed, MoveNext→false)
    /// are only applied to mocks the helper creates itself; caller-provided mocks are left untouched.
    /// </summary>
    private static AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>
        CreateMockCall(
            Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>? requestStream = null,
            Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>? responseStream = null)
    {
        var reqStream = requestStream ?? new Mock<IClientStreamWriter<P.SubscribeTopicEventsRequestAlpha1>>();
        var respStream = responseStream ?? new Mock<IAsyncStreamReader<P.SubscribeTopicEventsResponseAlpha1>>();

        if (requestStream is null)
        {
            reqStream.Setup(s => s.WriteAsync(It.IsAny<P.SubscribeTopicEventsRequestAlpha1>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }
        if (responseStream is null)
        {
            respStream.Setup(s => s.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        }

        return new AsyncDuplexStreamingCall<P.SubscribeTopicEventsRequestAlpha1, P.SubscribeTopicEventsResponseAlpha1>(
            reqStream.Object, respStream.Object,
            Task.FromResult(new Metadata()), () => new Status(), () => new Metadata(), () => { });
    }
}
