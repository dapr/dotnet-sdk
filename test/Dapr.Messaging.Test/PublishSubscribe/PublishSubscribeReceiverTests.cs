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
        
        //var options = new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(10), TopicResponseAction.Drop));
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
        var subscribeTask = receiver.SubscribeAsync();
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 100, "SubscribeAsync should return immediately and not block");
    }
}
