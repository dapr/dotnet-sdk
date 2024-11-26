using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Messaging.Test.Extensions;

public sealed class PublishSubscribeServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDaprPubSubClient_RegistersIHttpClientFactory()
    {
        var services = new ServiceCollection();

        services.AddDaprPubSubClient();

        var serviceProvider = services.BuildServiceProvider();

        var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
        Assert.NotNull(httpClientFactory);

        var daprPubSubClient = serviceProvider.GetService<DaprPublishSubscribeClient>();
        Assert.NotNull(daprPubSubClient);
    }
    
    [Fact]
    public void RegisterPubsubClient_ShouldRegisterSingleton_WhenLifetimeIsSingleton()
    {
        var services = new ServiceCollection();

        services.AddDaprPubSubClient(lifetime: ServiceLifetime.Singleton);
        var serviceProvider = services.BuildServiceProvider();

        var daprPubSubClient1 = serviceProvider.GetService<DaprPublishSubscribeClient>();
        var daprPubSubClient2 = serviceProvider.GetService<DaprPublishSubscribeClient>();

        Assert.NotNull(daprPubSubClient1);
        Assert.NotNull(daprPubSubClient2);
        
        Assert.Same(daprPubSubClient1, daprPubSubClient2);
    }

    [Fact]
    public async Task RegisterPubsubClient_ShouldRegisterScoped_WhenLifetimeIsScoped()
    {
        var services = new ServiceCollection();

        services.AddDaprPubSubClient(lifetime: ServiceLifetime.Scoped);
        var serviceProvider = services.BuildServiceProvider();

        await using var scope1 = serviceProvider.CreateAsyncScope();
        var daprPubSubClient1 = scope1.ServiceProvider.GetService<DaprPublishSubscribeClient>();

        await using var scope2 = serviceProvider.CreateAsyncScope();
        var daprPubSubClient2 = scope2.ServiceProvider.GetService<DaprPublishSubscribeClient>();
                
        Assert.NotNull(daprPubSubClient1);
        Assert.NotNull(daprPubSubClient2);
        Assert.NotSame(daprPubSubClient1, daprPubSubClient2);
    }

    [Fact]
    public void RegisterPubsubClient_ShouldRegisterTransient_WhenLifetimeIsTransient()
    {
        var services = new ServiceCollection();

        services.AddDaprPubSubClient(lifetime: ServiceLifetime.Transient);
        var serviceProvider = services.BuildServiceProvider();

        var daprPubSubClient1 = serviceProvider.GetService<DaprPublishSubscribeClient>();
        var daprPubSubClient2 = serviceProvider.GetService<DaprPublishSubscribeClient>();

        Assert.NotNull(daprPubSubClient1);
        Assert.NotNull(daprPubSubClient2);
        Assert.NotSame(daprPubSubClient1, daprPubSubClient2);
    }
}
