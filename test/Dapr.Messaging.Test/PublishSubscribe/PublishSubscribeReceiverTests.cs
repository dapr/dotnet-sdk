using System;
using Dapr.Messaging.PublishSubscribe;
using Moq;

namespace Dapr.Messaging.Test.PublishSubscribe;

public sealed class PublishSubscribeReceiverTests
{
    [Fact]
    public void Constructor_ShouldInitializeFields()
    {
        var daprClient = new Mock<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var connectionManager = new ConnectionManager(daprClient.Object);
        var handlerMock = new Mock<TopicMessageHandler>();
        var options =
            new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(30), TopicResponseAction.Retry));

        var receiver = new PublishSubscribeReceiver("pubsub", "dapr", options, connectionManager, handlerMock.Object);

        Assert.NotNull(receiver);
    }

    //[Fact]
    //public async Task SubscribeAsync_ShouldHandleMessages()
    //{
    //    var connectionManagerMock = new Mock<ConnectionManager>(); //Won't work - can't mock sealed types
    //    var handlerMock = new Mock<TopicMessageHandler>();
    //    var options = new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(15), TopicResponseAction.Retry));
    //    var receiver = new PublishSubscribeReceiver("pubsub", "dapr", options, connectionManagerMock.Object,
    //        handlerMock.Object);
    //    var cancellationToken = new CancellationToken();

    //    connectionManagerMock.Setup(cm => cm.GetStreamAsync(It.IsAny<CancellationToken>()))
    //        .ReturnsAsync(Mock
    //            .Of<AsyncDuplexStreamingCall<SubscribeTopicEventsRequestAlpha1, SubscribeTopicEventsResponseAlpha1>>());

    //    await receiver.SubscribeAsync(cancellationToken);

    //    //Assert various behaviors
    //}

    //[Fact]
    //public async Task DisposeAsync_ShouldDisposeResources()
    //{
    //    var connectionManagerMock = new Mock<ConnectionManager>(); //Won't work - can't mock sealed types
    //    var handlerMock = new Mock<TopicMessageHandler>();
    //    var options = new DaprSubscriptionOptions(new MessageHandlingPolicy(TimeSpan.FromSeconds(15), TopicResponseAction.Retry));
    //    var receiver = new PublishSubscribeReceiver("pubsub", "dapr", options, connectionManagerMock.Object,
    //        handlerMock.Object);

    //    await receiver.DisposeAsync();

    //    connectionManagerMock.Verify(cm => cm.DisposeAsync(), Times.Once);
    //}
}
