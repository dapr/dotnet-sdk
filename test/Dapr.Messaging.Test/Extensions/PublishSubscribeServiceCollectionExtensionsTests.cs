using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Dapr.Messaging.Test.Extensions;

public class PublishSubscribeServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDaprPubSubClient_RegistersServicesCorrectly()
    {
        var services = new ServiceCollection();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        services.AddSingleton(httpClientFactoryMock.Object);

        services.AddDaprPubSubClient();

        var serviceProvider = services.BuildServiceProvider();
        var daprClient = serviceProvider.GetService<DaprPublishSubscribeClient>();
        Assert.NotNull(daprClient);
    }

    [Fact]
    public void AddDaprPubSubClient_CallsConfigureAction()
    {
        var services = new ServiceCollection();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        services.AddSingleton(httpClientFactoryMock.Object);

        var configureCalled = false;

        services.AddDaprPubSubClient(Configure);

        var serviceProvider = services.BuildServiceProvider();
        var daprClient = serviceProvider.GetService<DaprPublishSubscribeClient>();
        Assert.NotNull(daprClient);
        Assert.True(configureCalled);
        return;

        void Configure(IServiceProvider sp, DaprPublishSubscribeClientBuilder builder)
        {
            configureCalled = true;
        }
    }   
}
